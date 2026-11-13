using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Healing;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Data.Forever.Shaman;

/// <summary>Per-run Flurry, Maelstrom Weapon and Stormstrike Nature vulnerability.
/// All proc decisions use the simulation's seeded RNG, not expected values.</summary>
public sealed class ForeverEnhancementShamanReactiveProcessor : ICombatEventProcessor
{
    private readonly string _actorKey;
    private readonly AutoAttackProcessor _autos;
    private readonly AuraManager _auras;
    private readonly ForeverEnhancementShamanTalents _talents;
    private readonly ForeverEnhancementShamanTuning _tuning;
    private readonly bool _classWide;
    private readonly decimal _mainHandSpeed;
    public bool ExternalLightningCostManagement { get; set; }
    private int _flurryCharges;
    private int _maelstromStacks;
    private decimal _maelstromExpiresAt;
    private decimal _baseLightningMana = -1m;
    private Guid? _pendingLightningCastId;
    private int _reservedLightningStacks;
    public int CurrentMaelstromStacks => _maelstromStacks;
    private bool _farseerActive;
    private bool _improvedStormstrikeRegenActive;
    private FiveSecondManaRegenerationProcessor? _regen;
    public int ImprovedStormstrikeRegenProcs { get; private set; }
    public int ImprovedStormstrikeCooldownResets { get; private set; }
    public decimal ImprovedStormstrikeCastingSpiritFraction =>
        _improvedStormstrikeRegenActive ? .5m : 0m;

    public void AttachManaRegeneration(FiveSecondManaRegenerationProcessor regen) =>
        _regen = regen;
    public int MaxMaelstromStacks { get; private set; }
    public int MaelstromInstantBolts { get; private set; }

    public ForeverEnhancementShamanReactiveProcessor(string actorKey,
        AutoAttackProcessor autos, AuraManager auras,
        ForeverEnhancementShamanTalents talents, ForeverEnhancementShamanTuning tuning,
        bool classWide = false, decimal mainHandSpeed = 3.6m)
    {
        _actorKey = actorKey; _autos = autos; _auras = auras;
        _talents = talents; _tuning = tuning;
        _classWide=classWide; _mainHandSpeed=mainHandSpeed;
    }
    public void Process(SimulationContext context, CombatEvent e)
    {
        var actor = context.GetActor(_actorKey);
        if (actor is null || !actor.IsAlive) return;
        if (_baseLightningMana < 0m &&
            actor.Abilities.TryGetValue("lightning-bolt",out var normalBolt))
            _baseLightningMana=normalBolt.Definition.ResourceCosts
                .First(c=>c.ResourceKey=="mana").Amount;
        // The executor leaves a completion unclaimed when its target or learned
        // ability vanished. Terminate only this owned Bolt, retaining its unused
        // stacks and releasing the opt-in completion-order gate and reservation.
        if (e.Type == CombatEventType.AbilityCastCompleted &&
            e.SourceActorKey == _actorKey && e.AbilityKey == "lightning-bolt" &&
            e.AbilityExecutionId is Guid failedId &&
            context.GetAbilityExecution(failedId) is
            { AbilityKey: "lightning-bolt", IsCancelled: false, CastCompletionProcessed: false } failedBolt &&
            failedBolt.SourceActorKey == _actorKey)
        {
            context.CancelAbilityExecution(failedId, context.CurrentTimeSeconds);
            actor.CompleteCurrentCast(failedId);
            if (_pendingLightningCastId == failedId)
            {
                _pendingLightningCastId = null;
                _reservedLightningStacks = 0;
                RefreshLightningCost(actor);
            }
        }
        // TryStartAbility records the execution synchronously, but Started is queued.
        // Latch before any intervening same-time hit can alter the stack/cost snapshot.
        // Instant normal Bolts have no CurrentCastExecutionId, so use the latest
        // matching execution only while its completion is still unprocessed.
        var lightningExecutionId = actor.CurrentCastExecutionId ??
            context.GetLatestAbilityExecutionId(actor.Key);
        if (lightningExecutionId is Guid liveId && _pendingLightningCastId != liveId &&
            context.GetAbilityExecution(liveId) is
            { AbilityKey: "lightning-bolt", IsCancelled: false, CastCompletionProcessed: false } liveBolt &&
            liveBolt.SourceActorKey == _actorKey)
        {
            _pendingLightningCastId = liveId;
            _reservedLightningStacks = _maelstromStacks;
        }
        // Partially stacked Maelstrom also lasts 30s; the older implementation
        // expired only the five-stack ready aura, leaving 1–4 stacks forever.
        if (_maelstromStacks>0 && context.CurrentTimeSeconds>=_maelstromExpiresAt)
        {
            _maelstromStacks=0;
            // Keep the active cast's frozen cost/timing, but its expired stacks
            // no longer own any fresh stacks gained before it completes.
            _reservedLightningStacks=0;
            _auras.RemoveAuras(context,actor,"maelstrom-ready",actor.Key);
            RefreshLightningCost(actor);
        }
        if (e.Type==CombatEventType.AbilityCastCancelled &&
            e.SourceActorKey==_actorKey &&
            e.AbilityExecutionId==_pendingLightningCastId)
        {
            _pendingLightningCastId=null;
            _reservedLightningStacks=0;
            RefreshLightningCost(actor);
        }
        var improvedRank = _talents.Rank("improved-stormstrike");
        if (improvedRank > 0)
        {
            // Official 70532 rank text: Stormstrike itself has a 50/100%
            // chance to keep 50% Mana regen while casting for 15 seconds.
            if (e.Type == CombatEventType.AbilityCastCompleted &&
                e.SourceActorKey == _actorKey && e.AbilityKey == "stormstrike" &&
                (decimal)context.Random.NextDouble() * 100m < improvedRank * 50m)
            {
                ImprovedStormstrikeRegenProcs++;
                _auras.ApplyAura(context, actor, new AuraDefinition
                {
                    Key = "improved-stormstrike-regen",
                    Name = "Improved Stormstrike (casting mana regeneration)",
                    DurationSeconds = 15m
                }, actor.Key, "improved-stormstrike", "stormstrike-regen");
            }
            // React to actual aura lifecycle, including expiration at 15s
            // and refresh after another successful Stormstrike.
            if (e.SourceActorKey == _actorKey && e.TargetActorKey == _actorKey &&
                e.EffectKey == "stormstrike-regen" &&
                e.Type is (CombatEventType.AuraApplied or CombatEventType.AuraRemoved))
            {
                _improvedStormstrikeRegenActive = actor.ActiveAuras.Any(a =>
                    a.Definition.Key == "improved-stormstrike-regen" &&
                    a.IsActiveAt(context.CurrentTimeSeconds));
                _regen?.RefreshRate(context);
            }
            // Avoidance is the SHAMAN's incoming dodge/parry, not an
            // attack dodged by the enemy. Reset Stormstrike, not its GCD.
            if (e.Type == CombatEventType.Damage &&
                e.TargetActorKey == _actorKey &&
                e.ResultKey is ("dodge" or "parry") &&
                (decimal)context.Random.NextDouble() * 100m < improvedRank * 50m &&
                actor.Abilities.TryGetValue("stormstrike", out var strike))
            {
                strike.ResetCooldown(context.CurrentTimeSeconds);
                ImprovedStormstrikeCooldownResets++;
            }
        }

        // Rage of the Farseer's haste must affect the ALREADY-SCHEDULED
        // main/off-hand swings, and disappear exactly at aura expiration.
        // Keep it additive with the independently tracked Flurry bonus.
        if (e.SourceActorKey == _actorKey && e.TargetActorKey == _actorKey &&
            e.EffectKey == "farseer-haste")
        {
            if (e.Type == CombatEventType.AuraApplied &&
                e.AbilityKey == "rage-of-the-farseer" && !_farseerActive)
            {
                _farseerActive = true;
                UpdateFarseer(context, actor);
            }
            else if (e.Type == CombatEventType.AuraRemoved && _farseerActive)
            {
                _farseerActive = false;
                UpdateFarseer(context, actor);
            }
        }
        // A five-stack proc can expire unspent; do not lock future proc generation.
        if (_maelstromStacks == 5 &&
            !actor.ActiveAuras.Any(a => a.Definition.Key == "maelstrom-ready" &&
                a.IsActiveAt(context.CurrentTimeSeconds)))
            _maelstromStacks = 0;
        if (e.Type == CombatEventType.AbilityCastCompleted &&
            e.SourceActorKey == _actorKey)
        {
            if (e.AbilityKey == "maelstrom-lightning-bolt")
            {
                MaelstromInstantBolts++;
                _maelstromStacks=0;
                _maelstromExpiresAt=0m;
                _auras.RemoveAuras(context,actor,"maelstrom-ready",actor.Key);
                RefreshLightningCost(actor);
            }
            else if (e.AbilityKey == "lightning-bolt" &&
                _pendingLightningCastId.HasValue &&
                e.AbilityExecutionId == _pendingLightningCastId)
            {
                // The cost and timing are captured at cast start. Do not
                // consume stacks acquired by weapon hits during a long cast.
                _maelstromStacks=Math.Max(0,_maelstromStacks-_reservedLightningStacks);
                _pendingLightningCastId=null;
                _reservedLightningStacks=0;
                if (_maelstromStacks<5)
                    _auras.RemoveAuras(context,actor,"maelstrom-ready",actor.Key);
                if (_maelstromStacks==0)_maelstromExpiresAt=0m;
                RefreshLightningCost(actor);
            }
        }
        if (e.Type == CombatEventType.AutoAttackSwing && e.SourceActorKey == _actorKey &&
            e.AbilityKey is ("main-hand-auto" or "off-hand-auto") && _flurryCharges > 0)
        {
            _flurryCharges--;
            if (_flurryCharges == 0) UpdateFlurry(context, actor);
        }
        if (e.Type != CombatEventType.Damage || e.SourceActorKey != _actorKey || e.Amount is not > 0m)
            return;
        if (_talents.Rank("stormstrike") > 0 &&
            e.AbilityKey == "stormstrike" && e.WeaponHandKey == "main-hand" &&
            e.TargetActorKey is not null && context.GetActor(e.TargetActorKey) is { } struckTarget)
        {
            // Historical tree 1cc5ae1: +20% Nature damage YOU deal to this
            // target for 12 seconds, with no charge/next-hit consumption.
            // Keep landed positive-damage gating; AuraManager refreshes only
            // this source/target pair and invalidates stale expiration events.
            _auras.ApplyAura(context, struckTarget, new AuraDefinition
            {
                Key = "shaman-stormstrike-nature", Name = "Stormstrike (Nature vulnerability)",
                DurationSeconds = 12m, SourceOwnedDamageSchoolKey = _classWide ? null : "nature",
                SourceOwnedDamageMultiplier = _classWide ? 1m : 1.2m
            }, actor.Key, "stormstrike", "stormstrike-nature");
        }
        // Either equipped hand can trigger melee-crit Flurry or Maelstrom.
        // Totem and Windfury background swings are nonrecursive by design;
        // only real weapon hit events enter this specialization listener.
        if (e.WeaponHandKey is not ("main-hand" or "off-hand") ||
            e.IsPeriodic || e.IsChannelTick) return;
        var flurryRank = _talents.Rank("flurry");
        if (flurryRank > 0 && e.IsCritical)
        {
            _flurryCharges = 3;
            UpdateFlurry(context, actor);
        }
        var maelstromRank = _talents.Rank("maelstrom-weapon");
        if (maelstromRank > 0 && _maelstromStacks < 5 &&
            (decimal)context.Random.NextDouble() * 100m <
                (_classWide ? (5m+maelstromRank)*_mainHandSpeed/60m*100m :
                    _tuning.MaelstromChancePerMeleePercentPerRank * maelstromRank))
        {
            _maelstromStacks++;
            _maelstromExpiresAt=context.CurrentTimeSeconds+30m;
            if (_maelstromExpiresAt<context.Options.DurationSeconds)
                context.ScheduleEvent(new CombatEvent
                {
                    TimeSeconds=_maelstromExpiresAt,
                    Type=CombatEventType.RotationDecision,
                    SourceActorKey=_actorKey,EffectKey="maelstrom-expiration",
                    IsInternal=true
                });
            RefreshLightningCost(actor);
            MaxMaelstromStacks = Math.Max(MaxMaelstromStacks, _maelstromStacks);
            if (_maelstromStacks == 5)
                _auras.ApplyAura(context, actor, new AuraDefinition
                {
                    Key = "maelstrom-ready", Name = "Maelstrom Weapon (5 stacks)",
                    DurationSeconds = 30m
                }, actor.Key, "maelstrom-weapon", null);
        }
    }
    private void RefreshLightningCost(SimulationActorState actor)
    {
        if (ExternalLightningCostManagement || _baseLightningMana<0m || _pendingLightningCastId.HasValue ||
            !actor.Abilities.TryGetValue("lightning-bolt",out var spell))
            return;
        var mana=spell.Definition.ResourceCosts.First(c=>c.ResourceKey=="mana");
        var reduction=Math.Clamp((_classWide ? .2m : .04m*_talents.Rank("maelstrom-weapon"))*
            _maelstromStacks,0m,1m);
        mana.Amount=_baseLightningMana*(1m-reduction);
    }

    private void UpdateFarseer(SimulationContext context, SimulationActorState actor)
    {
        var oldBonus = actor.Stats.Get("shaman-farseer-haste-percent");
        var newBonus = _farseerActive ? 30m : 0m;
        if (oldBonus == newBonus) return;
        var oldHaste = actor.Stats.Get(ForeverCombatStatKeys.HastePercent);
        var newHaste = _classWide ?
            ((1m+oldHaste/100m)/(1m+oldBonus/100m)*(1m+newBonus/100m)-1m)*100m :
            oldHaste - oldBonus + newBonus;
        actor.Stats.Set("shaman-farseer-haste-percent", newBonus);
        actor.Stats.Set(ForeverCombatStatKeys.HastePercent, newHaste);
        _autos.RescheduleForHasteChange(context, actor.Key, oldHaste, newHaste);
    }

    private void UpdateFlurry(SimulationContext context, SimulationActorState actor)
    {
        var oldBonus = actor.Stats.Get("shaman-flurry-haste-percent");
        var newBonus = _flurryCharges > 0 ? (_classWide ? 6m : 5m) * _talents.Rank("flurry") : 0m;
        if (oldBonus == newBonus) return;
        var haste = actor.Stats.Get(ForeverCombatStatKeys.HastePercent);
        var newHaste=_classWide ?
            ((1m+haste/100m)/(1m+oldBonus/100m)*(1m+newBonus/100m)-1m)*100m : haste-oldBonus+newBonus;
        actor.Stats.Set("shaman-flurry-haste-percent", newBonus);
        actor.Stats.Set(ForeverCombatStatKeys.HastePercent, newHaste);
        _autos.RescheduleForHasteChange(context, actor.Key, haste, newHaste);
    }
}
