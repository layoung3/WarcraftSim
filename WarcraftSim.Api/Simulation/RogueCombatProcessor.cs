using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Rogue;

namespace WarcraftSim.Api.Simulation;

/// <summary>CP142 target-owned combo points, landed-hit generators, finishers,
/// seeded poison procs and temporary Energy regeneration. This is one processor
/// per solo Rogue run; it is not shared/static state.</summary>
public sealed class RogueCombatProcessor : ICombatEventProcessor, IAbilityResourceCostPolicy
{
    private static readonly HashSet<string> Generators = new(StringComparer.OrdinalIgnoreCase)
    { "sinister-strike", "backstab", "mutilate", "hemorrhage", "ambush", "garrote", "ghostly-strike", "cheap-shot" };
    private readonly RogueSimRequest _request;
    private readonly SimulationActorState _actor;
    private readonly RotationProfile _rotation;
    private readonly AbilityExecutor _executor;
    private readonly AuraManager _auras;
    private readonly AutoAttackProcessor _auto;
    private readonly AutoAttackDefinition[] _weapons;
    private bool _inCombat;
    private bool _gougeStopped;
    private int _stealthOpenerCount;
    public int StealthOpeners => _stealthOpenerCount;
    public bool IsStealthed(SimulationContext context) => HasAura(context, "rogue-stealth");
    private readonly Dictionary<string, int> _points = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<Guid> _generatorsAwarded = [];
    private readonly HashSet<Guid> _finishersSpent = [];
    private readonly HashSet<Guid> _sealFateAwarded = [];
    private int _ruptureSnapshotPoints;
    private readonly Dictionary<string, int> _ruptureSnapshotPointsByTarget =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<Guid> _quickRecoveryRefunded = [];
    public int PreparationResets { get; private set; }
    public int HackAndSlashAttacks { get; private set; }
    public decimal QuickRecoveryEnergyRestored { get; private set; }
    public int ExposeArmorPoints { get; private set; }
    private readonly Dictionary<string, (Guid Id, string Target)> _pendingAuraFinisherExecutions =
        new(StringComparer.OrdinalIgnoreCase);
    // Historical Initiative names three openers and 33/67/100% ranks. Successful
    // execution correlation and one draw (even at rank 3/cap) are local conventions.
    // Allocate no correlation state for absent/zero ranks.
    private readonly InitiativeState? _initiative;
    private sealed record InitiativeApplication(Guid Aura, Guid Execution, string Source,
        string Target, string Ability, string Effect, string AuraKey, decimal Time);
    private sealed class InitiativeState(int rank)
    {
        public int Rank { get; } = rank;
        public HashSet<Guid> CompletionsObserved { get; } = [];
        public HashSet<Guid> ApplicationsBound { get; } = [];
        public HashSet<Guid> Attempts { get; } = [];
        public Dictionary<Guid, InitiativeApplication> Pending { get; } = [];
    }
    private readonly RogueThousandCutsState? _thousandCuts;
    public RogueClassWideMechanics? ClassWide { get; }
    public int ThousandCutsStacks => _thousandCuts?.Stacks ?? 0;
    public int FinishersUsed { get; private set; }
    public int ComboPointsFor(string target) => _points.GetValueOrDefault(target);
    public int RuptureSnapshotPoints => _ruptureSnapshotPoints;

    public RogueCombatProcessor(RogueSimRequest request, SimulationActorState actor,
        RotationProfile rotation, AbilityExecutor executor, AuraManager auras,
        AutoAttackProcessor auto, AutoAttackDefinition[] weapons)
    {
        _request = request; _actor = actor; _rotation = rotation; _executor = executor;
        _auras = auras; _auto = auto; _weapons = weapons;
        if (!request.UseClassWideTalents && request.Talents.Rank("thousand-cuts") > 0) _thousandCuts = new(actor, auras);
        if (request.UseClassWideTalents) ClassWide=new(request,actor,auras,executor,ComboPointsFor,
            AddPoints,(target,amount)=>_points[target]=amount);
        if (request.Talents.Rank("initiative") is > 0 and <= 3)
            _initiative = new(request.Talents.Rank("initiative"));
    }

    public void Synchronize(SimulationContext context, SimulationActorState actor, AbilityDefinition ability)
    {
        if (ReferenceEquals(actor, _actor))
        {
            _thousandCuts?.Synchronize(context);
            ClassWide?.Synchronize(context);
        }
    }

    public decimal? GetNextAffordableTime(SimulationContext context, SimulationActorState actor, AbilityDefinition ability) =>
        _thousandCuts is null
            ? ResourceAvailabilityCalculator.GetNextAffordableTime(actor, ability, context.CurrentTimeSeconds)
            : _thousandCuts.GetNextAffordableTime(context, actor, ability);

    public void Process(SimulationContext context, CombatEvent ev)
    {
        ClassWide?.Process(context,ev);
        _thousandCuts?.Synchronize(context);
        if (_initiative is not null)
        {
            // Immediate applications may wait behind other same-time work. Retain
            // them through refresh/death/removal, but never revive stale entries.
            if (ev.Type == CombatEventType.SimulationEnded)
            {
                _initiative.Pending.Clear();
                return;
            }
            foreach (var stale in _initiative.Pending.Values
                .Where(p => p.Time < context.CurrentTimeSeconds).ToArray())
                _initiative.Pending.Remove(stale.Aura);
        }
        if (ev.Type == CombatEventType.SimulationStarted)
        {
            if (_request.StartInStealth) EnterStealth(context, "opening-stealth");
            UpdateRegen(context); return;
        }
        if (!string.Equals(ev.SourceActorKey, _actor.Key, StringComparison.OrdinalIgnoreCase))
        {
            if (ev.Type == CombatEventType.AuraRemoved && ev.TargetActorKey == _actor.Key)
                UpdateRegen(context);
            return;
        }
        if (_initiative is not null && ev.Type == CombatEventType.AbilityCastCompleted)
            CaptureInitiativeApplication(context, ev);
        if (ev.Type == CombatEventType.AuraApplied || ev.Type == CombatEventType.AuraRemoved)
        {
            // Expose Armor is the Rogue finisher whose combo points are spent
            // only when the debuff actually lands, not on cast completion.
            // Garrote grants its combo point only when the missable bleed actually applies.
            // Awarding at cast completion incorrectly gave points after a failed application.
            if (ev.Type == CombatEventType.AuraApplied && ev.AbilityKey == "garrote" &&
                ev.TargetActorKey is { } garroteTarget)
                AddPoints(garroteTarget, 1);
            if (_initiative is not null && ev.Type == CombatEventType.AuraApplied)
                ConsumeInitiativeApplication(context, ev);
            if (ev.Type == CombatEventType.AuraApplied &&
                ev.TargetActorKey is { } auraTarget)
            {
                if (ev.AbilityKey == "expose-armor")
                {
                    if (_pendingAuraFinisherExecutions.Remove("expose-armor", out var exposeExecution) &&
                        exposeExecution.Target == auraTarget)
                        SpendFinisher(context, exposeExecution.Id, exposeExecution.Target, "expose-armor");
                }
                else if (ev.AbilityKey is ("rupture" or "venom" or "slice-and-dice" or "kidney-shot" or "death-mark") &&
                    _pendingAuraFinisherExecutions.Remove(ev.AbilityKey, out var auraExecution))
                    SpendFinisher(context, auraExecution.Id, auraExecution.Target, ev.AbilityKey);
            }
            UpdateRegen(context);
            return;
        }
        if (ev.Type == CombatEventType.RotationDecision)
        {
            UpdateRotation(context); return;
        }
        if (_request.UseClassWideTalents && ev.Type==CombatEventType.AbilityCastCompleted)
        {
            if(ev.AbilityKey=="gouge")
            {
                foreach(var weapon in _weapons)_auto.Stop(context,_actor.Key,weapon.Key);
                _gougeStopped=true;
            }
            else if(_gougeStopped && ev.AbilityKey is ("sinister-strike" or "backstab" or "ambush" or "mutilate" or "shank" or "ghostly-strike" or "hemorrhage" or "riposte"))
            {
                foreach(var weapon in _weapons)_auto.Start(context,_actor.Key,ev.TargetActorKey!,weapon);
                _gougeStopped=false;
            }
        }
        if (ev.Type == CombatEventType.AbilityCastCompleted &&
            ev.AbilityKey == "preparation")
        {
            // Reset only explicitly eligible Rogue cooldowns. Do not reset
            // Preparation itself, shared GCDs, resources or auras.
            foreach (var key in _request.UseClassWideTalents ?
                _actor.Abilities.Values.Where(a=>a.Definition.ClassKey=="rogue" && a.Definition.Key!="preparation")
                    .Select(a=>a.Definition.Key).ToArray() :
                new[] { "vanish", "evasion", "sprint", "blind", "cold-blood", "ghostly-strike" })
                if (_actor.Abilities.TryGetValue(key, out var state))
                {
                    state.ResetCooldown(context.CurrentTimeSeconds);
                    PreparationResets++;
                }
            return;
        }
        if (ev.Type == CombatEventType.AbilityCastCompleted &&
            ev.AbilityKey == "vanish")
        {
            EnterStealth(context, "vanish");
            return;
        }
        if (ev.Type == CombatEventType.AbilityCastCompleted &&
            ev.AbilityKey == "riposte")
        {
            _auras.RemoveAuras(context, _actor, "riposte-ready", _actor.Key,
                "riposte opportunity spent");
        }
        if (ev.Type == CombatEventType.AbilityCastCompleted &&
            ev.AbilityKey == "premeditation" && ev.TargetActorKey is not null)
        {
            var before=ComboPointsFor(ev.TargetActorKey);
            AddPoints(ev.TargetActorKey, 2);
            ClassWide?.RegisterPremeditation(context,ev.TargetActorKey,ComboPointsFor(ev.TargetActorKey)-before);
            return;
        }
        if (ev.Type == CombatEventType.AbilityCastCompleted &&
            ev.AbilityKey is ("ambush" or "garrote" or "cheap-shot"))
        {
            _stealthOpenerCount++;
            _inCombat = true;
            if (ev.AbilityKey == "ambush")
            {
                _auras.RemoveAuras(context, _actor, "rogue-cutthroat", _actor.Key);
            }
            ExitStealth(context);
        }
        if (ev.Type == CombatEventType.AbilityCastCompleted &&
            ev.AbilityKey == "cheap-shot" && ev.TargetActorKey is not null)
            AddPoints(ev.TargetActorKey, 2);
        // Finishers spend only after their landed damage or aura application.
        // Spending at cast completion happened before damage resolution and
        // reduced Eviscerate's multiplier to one combo point.
        if (ev.Type == CombatEventType.AbilityCastCompleted &&
            ev.AbilityKey is ("expose-armor" or "rupture" or "venom" or "slice-and-dice" or "kidney-shot" or "death-mark") &&
            ev.AbilityExecutionId is Guid auraFinisherId &&
            ev.TargetActorKey is { } castTarget)
            _pendingAuraFinisherExecutions[ev.AbilityKey] = (auraFinisherId, castTarget);
        if (ev.Type == CombatEventType.AbilityCastCompleted &&
            ev.AbilityKey is ("backstab" or "hemorrhage") &&
            _request.Talents.Rank("thousand-cuts") > 0)
        {
            _thousandCuts?.Consume(context);
            return;
        }
        // Missed finishers do not earn damage or combo-point-on-hit bonuses.
        // Quick Recovery refunds a configured portion of the spent Energy.
        if (ev.Type == CombatEventType.Damage && !ev.IsPeriodic &&
            ev.ResultKey == CombatResultTypes.Miss &&
            ev.AbilityExecutionId is Guid missId &&
            ev.AbilityKey is ("eviscerate" or "rupture") &&
            _quickRecoveryRefunded.Add(missId) &&
            _request.Talents.Rank("quick-recovery") > 0)
        {
            // Refund against the actually configured cast cost. Talent-driven
            // energy discounts change the AbilityDefinition, so a separate
            // hard-coded base cost would over- or under-refund missed finishers.
            var energyCost = _actor.Abilities.TryGetValue(ev.AbilityKey!, out var castState)
                ? castState.Definition.ResourceCosts
                    .Where(c => string.Equals(c.ResourceKey, "energy", StringComparison.OrdinalIgnoreCase))
                    .Sum(c => c.Amount)
                : 0m;
            var refund = energyCost * _request.Talents.Rank("quick-recovery") *
                _request.Tuning.QuickRecoveryEnergyRefundPercentPerRank / 100m;
            _actor.RefreshResources(context.CurrentTimeSeconds);
            var gained = _actor.Resources["energy"].Gain(refund);
            QuickRecoveryEnergyRestored += gained;
            if (gained > 0m)
                context.EmitEvent(new CombatEvent {
                    Type = CombatEventType.ResourceChanged,
                    TimeSeconds = context.CurrentTimeSeconds,
                    SourceActorKey = _actor.Key, TargetActorKey = _actor.Key,
                    AbilityKey = "quick-recovery", ResourceKey = "energy",
                    Amount = gained,
                    ResourceTotalAfter = _actor.Resources["energy"].Current,
                    Description = "Quick Recovery refunded Energy after missed finisher."
                });
        }
        if (ev.Type == CombatEventType.Damage &&
            ev.AbilityKey == "eviscerate" &&
            ev.ResultKey is not (CombatResultTypes.Miss or "dodge" or "parry") &&
            ev.AbilityExecutionId.HasValue && ev.TargetActorKey is { } finisherTarget)
            SpendFinisher(context, ev.AbilityExecutionId, finisherTarget, "eviscerate");
        if (ev.Type != CombatEventType.Damage || ev.Amount is not > 0m)
        {
            // A genuine landed zero-damage Ambush earns only Initiative; preserve
            // the existing positive-damage base/Seal Fate/poison gates unchanged.
            if (_initiative is not null) TryAmbushInitiative(context, ev);
            return;
        }
        if (!ev.IsPeriodic && ev.AbilityKey != "blade-flurry-cleave")
        {
            _inCombat = true;
            if (IsStealthed(context)) ExitStealth(context);
            if (ev.AbilityKey == "backstab" && _request.Talents.Rank("cutthroat") > 0 &&
                (decimal)context.Random.NextDouble() * 100m < 3m * _request.Talents.Rank("cutthroat"))
            {
                // A narrow source-aura gate allows an Ambush without Stealth.
                _auras.ApplyAura(context, _actor, new AuraDefinition {
                    Key = "rogue-cutthroat", Name = "Cutthroat opportunity", DurationSeconds = 10m
                }, _actor.Key, "cutthroat", "cutthroat-opportunity");
            }
            if (ev.AbilityKey != "instant-poison" &&
                ev.AbilityKey != "blade-flurry-cleave" &&
                HasAura(context, "blade-flurry"))
                Cleave(context, ev);
            TriggerHackAndSlash(context, ev);
        }
        if (ev.AbilityKey is not null && ev.TargetActorKey is not null &&
            !ev.IsPeriodic && (Generators.Contains(ev.AbilityKey) || _request.UseClassWideTalents && ev.AbilityKey=="gouge") &&
            ev.AbilityExecutionId is Guid generatorId)
        {
            if (_generatorsAwarded.Add(generatorId))
                AddPoints(ev.TargetActorKey, ev.AbilityKey == "mutilate" ? 2 : 1);
            // Puncturing Wounds gives Backstab a rank-dependent extra point.
            if (ev.AbilityKey == "backstab" && _request.Talents.Rank("puncturing-wounds") > 0 &&
                (decimal)context.Random.NextDouble() * 100m <
                    _request.Talents.Rank("puncturing-wounds") *
                    _request.Tuning.PuncturingWoundsExtraPointChancePerRank)
                AddPoints(ev.TargetActorKey, 1);
            // Either hit of Mutilate can trigger Seal Fate, but at most once.
            // A failed Seal Fate chance on the first Mutilate crit must not
            // block a later critical hit from rolling. Only a SUCCESS reserves
            // the single bonus point for this ability execution.
            if (ev.IsCritical && _request.Talents.Rank("seal-fate") > 0 &&
                (_request.UseClassWideTalents ? _sealFateAwarded.Add(generatorId) : !_sealFateAwarded.Contains(generatorId)) &&
                (decimal)context.Random.NextDouble() * 100m <
                (_request.UseClassWideTalents ? ForeverRogueClassTalents.SealFatePercent(_request.Talents.Rank("seal-fate")) :
                _request.Talents.Rank("seal-fate") * 20m))
            {
                _sealFateAwarded.Add(generatorId);
                AddPoints(ev.TargetActorKey, 1);
            }
        }
        // Existing base point and Seal Fate precede Initiative; poison's separate
        // processor observes this event afterward, without reordering earlier work.
        if (_initiative is not null) TryAmbushInitiative(context, ev);
        if (ev.IsPeriodic && ev.AbilityKey == "rupture" &&
            _request.Talents.Rank("thousand-cuts") > 0)
            _thousandCuts?.Gain(context);

    }

    private static bool SameIdentity(string? left, string? right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private void TryAmbushInitiative(SimulationContext context, CombatEvent ev)
    {
        if (ev.Type != CombatEventType.Damage || ev.EffectDeliveryType != CombatEffectDeliveryType.Direct ||
            ev.TimeSeconds != context.CurrentTimeSeconds ||
            ev.AbilityKey != "ambush" || ev.EffectKey != "ambush-hit" ||
            ev.AbilityExecutionId is not Guid id || id == Guid.Empty ||
            context.GetAbilityExecution(id) is not { CastCompletionProcessed: true } execution ||
            !SameIdentity(execution.SourceActorKey, _actor.Key) ||
            !SameIdentity(execution.SourceActorKey, ev.SourceActorKey) ||
            !SameIdentity(execution.TargetActorKey, ev.TargetActorKey) ||
            !SameIdentity(execution.AbilityKey, ev.AbilityKey) ||
            context.GetActor(execution.TargetActorKey) is not { } target ||
            !execution.EffectResultsByTarget.TryGetValue(("ambush-hit", target.Key), out var result) ||
            !result.Landed || !SameIdentity(result.ResultKey, ev.ResultKey)) return;
        AttemptInitiative(context, id, target.Key);
    }

    private void CaptureInitiativeApplication(SimulationContext context, CombatEvent ev)
    {
        if (ev.AbilityKey is not ("garrote" or "cheap-shot") ||
            ev.AbilityExecutionId is not Guid id || id == Guid.Empty ||
            context.GetAbilityExecution(id) is not { } execution ||
            !SameIdentity(execution.SourceActorKey, _actor.Key) ||
            !SameIdentity(execution.SourceActorKey, ev.SourceActorKey) ||
            !SameIdentity(execution.TargetActorKey, ev.TargetActorKey) ||
            !SameIdentity(execution.AbilityKey, ev.AbilityKey) ||
            !_initiative!.CompletionsObserved.Add(id)) return;
        // The first inspection owns failure too: a duplicate completion cannot
        // capture a later same-time refresh. Never use the actor's latest cast.
        if (!execution.CastCompletionProcessed ||
            !_actor.Abilities.TryGetValue(execution.AbilityKey, out var state) ||
            context.GetActor(execution.TargetActorKey) is not { } target) return;
        var ability = state.Definition;
        if (ability.IsChanneled || ability.Effects.Count != 1) return;
        var effect = ability.Effects[0];
        var garrote = ability.Key == "garrote";
        if (effect.Key != (garrote ? "garrote-tick" : "cheap-shot-control") ||
            effect.AuraKey != (garrote ? "garrote" : "cheap-shot-stun") ||
            effect.EffectType != (garrote ? AbilityEffectTypes.PeriodicDamage : AbilityEffectTypes.ApplyAura) ||
            effect.TargetType != AbilityTargetTypes.Enemy || effect.AuraStackingMode != AuraStackingMode.Refresh ||
            effect.TravelTimeSeconds != 0m ||
            effect.ApplyOnChannelTick || !string.IsNullOrEmpty(effect.DependsOnEffectKey) ||
            effect.DurationSeconds is not > 0m || (garrote && effect.TickIntervalSeconds is not > 0m) ||
            !execution.EffectResultsByTarget.TryGetValue((effect.Key, target.Key), out var result) || !result.Landed) return;
        // Executor runs first. Each fixed immediate effect applied this owned aura
        // before this completion observer, and refresh/stack assigned a fresh GUID.
        // Copy scalars now; a later completion may mutate the same AuraInstance.
        var matching = target.ActiveAuras.Where(a => a.Definition.Key == effect.AuraKey &&
            SameIdentity(a.SourceActorKey, _actor.Key) && SameIdentity(a.TargetActorKey, target.Key) &&
            SameIdentity(a.AbilityKey, ability.Key) && SameIdentity(a.EffectKey, effect.Key) &&
            a.AppliedAtSeconds == ev.TimeSeconds && a.InstanceId != Guid.Empty).Take(2).ToArray();
        if (matching.Length != 1) return;
        var aura = matching[0];
        if (!_initiative.ApplicationsBound.Add(aura.InstanceId)) return;
        _initiative.Pending.Add(aura.InstanceId, new(aura.InstanceId, id, _actor.Key,
            target.Key, ability.Key, effect.Key, effect.AuraKey!, aura.AppliedAtSeconds));
    }

    private void ConsumeInitiativeApplication(SimulationContext context, CombatEvent ev)
    {
        if (ev.AuraInstanceId is not Guid id || !_initiative!.Pending.TryGetValue(id, out var p) ||
            !SameIdentity(p.Source, ev.SourceActorKey) || !SameIdentity(p.Target, ev.TargetActorKey) ||
            !SameIdentity(p.Ability, ev.AbilityKey) || !SameIdentity(p.Effect, ev.EffectKey) ||
            p.Time != ev.TimeSeconds) return;
        // Wrong notifications cannot steal entries; matched notifications remain
        // valid even after successful application followed by removal or death.
        _initiative.Pending.Remove(id);
        AttemptInitiative(context, p.Execution, p.Target);
    }

    private void AttemptInitiative(SimulationContext context, Guid execution, string target)
    {
        if (!_initiative!.Attempts.Add(execution)) return;
        var chance = _initiative.Rank switch { 1 => 33m, 2 => 67m, _ => 100m };
        if ((decimal)context.Random.NextDouble() * 100m < chance) AddPoints(target, 1);
    }

    public bool ConsumeColdBlood(SimulationContext context, string ability)
    {
        if (_request.UseClassWideTalents && ability=="hemorrhage")return false;
        if (ability is not ("ambush" or "mutilate" or "eviscerate" or "backstab" or
            "sinister-strike" or "hemorrhage") || !HasAura(context, "cold-blood")) return false;
        _auras.RemoveAuras(context, _actor, "cold-blood", _actor.Key, "consumed by successful special");
        return true;
    }

    public decimal DamageMultiplier(string abilityKey, CombatEffectDeliveryType delivery,
        SimulationContext context, SimulationActorState target)
    {
        decimal conditional = 1m;
        if (HasAura(context, "master-of-subtlety-window"))
            conditional *= 1m + _request.Talents.Rank("master-of-subtlety") *
                _request.Tuning.MasterOfSubtletyDamagePercentPerRank / 100m;
        if (target.ActiveAuras.Any(a => a.Definition.Key == "kidney-shot-stun" &&
            a.SourceActorKey == _actor.Key && a.IsActiveAt(context.CurrentTimeSeconds)))
            conditional += _request.Talents.Rank("improved-kidney-shot") *
                (_request.UseClassWideTalents ? .05m : _request.Tuning.ImprovedKidneyShotDamagePerRank);
        if (abilityKey == "garrote" && delivery == CombatEffectDeliveryType.Periodic)
            conditional *= 1m + _request.Talents.Rank("improved-garrote") *
                _request.Tuning.ImprovedGarroteDamagePerRank;
        if (_request.UseClassWideTalents && abilityKey=="garrote" && target.ActiveAuras.Any(a=>
            a.Definition.Key=="hemorrhage-rupture" && a.SourceActorKey==_actor.Key && a.IsActiveAt(context.CurrentTimeSeconds)))
            conditional*=1.15m;
        if (abilityKey == "eviscerate") return conditional * Math.Max(1, ComboPointsFor(target.Key));
        if (abilityKey == "rupture")
        {
            var buff = target.ActiveAuras.Any(a => a.Definition.Key == "hemorrhage-rupture" &&
                string.Equals(a.SourceActorKey, _actor.Key, StringComparison.OrdinalIgnoreCase) &&
                a.IsActiveAt(context.CurrentTimeSeconds));
            var multiplier = conditional * Math.Max(1, _ruptureSnapshotPointsByTarget.GetValueOrDefault(target.Key)) *
                (buff ? 1.15m : 1m);
            // Historical 10/20/30% Rupture damage, once after the existing CP
            // and live owned mark factors; stacking is a provisional convention.
            var serrated = _request.Talents.Rank("serrated-blades");
            return serrated > 0 ? multiplier * (1m + .10m * serrated) : multiplier;
        }
        if (abilityKey is "instant-poison" or "deadly-poison")
            return conditional * (1m + _request.Talents.Rank("vile-poisons") * .04m) *
                (HasAura(context, "venom") ? 1.30m : 1m);
        if (abilityKey == "mutilate")
        {
            // Existing target-state convention is source-unfiltered. Only live
            // modeled poison auras qualify; Instant Poison has no lingering aura.
            var poisoned = target.ActiveAuras.Any(a =>
                a.Definition.Key is ("deadly-poison" or "wound-poison" or
                    "mind-numbing-poison" or "crippling-poison") &&
                a.IsActiveAt(context.CurrentTimeSeconds));
            return (_request.UseClassWideTalents ? conditional : 1m) * (poisoned ? 1.20m : 1m);
        }
        return conditional;
    }

    private void UpdateRotation(SimulationContext context)
    {
        var cp = ComboPointsFor("target");
        foreach (var entry in _rotation.Entries)
            entry.IsEnabled = entry.AbilityKey switch
            {
                "slice-and-dice" => cp >= 2 && !HasAura(context, "slice-and-dice"),
                "venom" => cp >= 3 && !HasAura(context, "venom"),
                "rupture" => cp >= 4 && !context.GetActor("target")!.ActiveAuras.Any(a =>
                    a.Definition.Key == "rupture" && a.IsActiveAt(context.CurrentTimeSeconds)),
                "eviscerate" => cp >= 4,
                "death-mark" => cp>=4,
                "shank" => _actor.Resources["energy"].Current <= _actor.Resources["energy"].Maximum -
                    (25m+3m*_request.Talents.Rank("flash-of-steel")-5m),
                "ambush" => HasAura(context, "rogue-stealth") || HasAura(context, "rogue-cutthroat") || (_request.UseClassWideTalents && HasAura(context,"rogue-ghostly-opener")),
                "garrote" => HasAura(context,"rogue-stealth") || (_request.UseClassWideTalents && HasAura(context,"rogue-ghostly-opener")),
                "cheap-shot" or "sap" => HasAura(context, "rogue-stealth"),
                "premeditation" => HasAura(context, "rogue-stealth") && cp <= 3,
                "vanish" => !HasAura(context, "rogue-stealth") && _inCombat &&
                    !HasAura(context, "rogue-cutthroat"),
                "expose-armor" => cp >= 4 && !context.GetActor("target")!.ActiveAuras.Any(a =>
                    a.Definition.Key == "expose-armor" && a.IsActiveAt(context.CurrentTimeSeconds)),
                "kidney-shot" => cp >= 4,
                "adrenaline-rush" => !HasAura(context, "adrenaline-rush"),
                "blade-flurry" => !HasAura(context, "blade-flurry"),
                _ => true
            };
        _thousandCuts?.Synchronize(context);
        if (!_request.UseClassWideTalents && _actor.Abilities.TryGetValue("eviscerate", out var eviscerate))
            eviscerate.Definition.ResourceCosts[0].Amount =
                _request.Talents.Rank("flawless-execution") > 0 ? 25m : 35m;
        if (!_request.UseClassWideTalents && _thousandCuts is null && _actor.Abilities.TryGetValue("backstab", out var backstab))
            backstab.Definition.ResourceCosts[0].Amount = 60m;
        if (!_request.UseClassWideTalents && _thousandCuts is null && _actor.Abilities.TryGetValue("hemorrhage", out var hemorrhage))
            hemorrhage.Definition.ResourceCosts[0].Amount = 35m;
        if (_actor.Abilities.TryGetValue("slice-and-dice", out var slice))
            slice.Definition.Effects[0].DurationSeconds = (6m + 3m * Math.Max(1, cp)) *
                (1m + _request.Talents.Rank("improved-slice-and-dice") * .15m);
        if (_actor.Abilities.TryGetValue("venom", out var venom))
            venom.Definition.Effects[0].DurationSeconds = 6m + Math.Max(1, cp) * 3m;
        if (_actor.Abilities.TryGetValue("rupture", out var rupture))
            rupture.Definition.Effects[0].DurationSeconds = 6m + Math.Max(1, cp) * 2m +
                (_request.UseClassWideTalents ? 6m*_request.Talents.Rank("blood-splatter") : 0m);
        ClassWide?.Synchronize(context);
    }

    private void SpendFinisher(SimulationContext context, Guid? id, string target, string key)
    {
        if (id is not Guid executionId || !_finishersSpent.Add(executionId)) return;
        var spent = ComboPointsFor(target);
        if (spent == 0) return;
        if (key == "rupture")
        {
            _ruptureSnapshotPoints = spent; // Legacy last-cast reporting metric.
            _ruptureSnapshotPointsByTarget[target] = spent;
        }
        if (key == "expose-armor") ExposeArmorPoints = spent;
        _points[target] = 0;
        FinishersUsed++;
        ClassWide?.OnFinisher(context,target,spent);
        // Historical pinned source refunds 1/2 at ranks 1/2 after five spent.
        // Preserve an explicit legacy absolute override, including zero and five.
        var exposeRank = _request.Talents.Rank("improved-expose-armor");
        if (key == "expose-armor" && spent == 5 &&
            exposeRank > 0)
            AddPoints(target, _request.Tuning.ImprovedExposeArmorFivePointRefund ?? exposeRank);
        if (_request.Talents.Rank("ruthlessness") > 0 &&
            (decimal)context.Random.NextDouble() * 100m < _request.Talents.Rank("ruthlessness") * 20m)
            AddPoints(target, 1);
    }
    private void AddPoints(string target, int amount)
    {
        ClassWide?.OnPointsAdded(target);
        _points[target] = Math.Min(5, ComboPointsFor(target) + amount);
    }
    private bool HasAura(SimulationContext context, string key) =>
        _actor.ActiveAuras.Any(a => a.Definition.Key == key && a.IsActiveAt(context.CurrentTimeSeconds));
    private void UpdateRegen(SimulationContext context)
    {
        // Settle elapsed regeneration at the OLD rate before changing the
        // rate at an Adrenaline Rush aura boundary.
        _actor.RefreshResources(context.CurrentTimeSeconds);
        _actor.Resources["energy"].RegenerationPerSecond = _request.Tuning.EnergyPerSecond *
            (HasAura(context, "adrenaline-rush") ? 2m : 1m);
    }
    private void EnterStealth(SimulationContext context, string source)
    {
        foreach (var weapon in _weapons)
            _auto.Stop(context, _actor.Key, weapon.Key);
        _auras.ApplyAura(context, _actor, new AuraDefinition {
            Key = "rogue-stealth", Name = "Stealth", DurationSeconds = 3600m
        }, _actor.Key, source, "stealth-state");
    }

    private void ExitStealth(SimulationContext context)
    {
        if (!IsStealthed(context)) return;
        _auras.RemoveAuras(context, _actor, "rogue-stealth", _actor.Key, "stealth broken");
        if (_request.Talents.Rank("master-of-subtlety") > 0)
            _auras.ApplyAura(context, _actor, new AuraDefinition {
                Key = "master-of-subtlety-window", Name = "Master of Subtlety",
                DurationSeconds = _request.Tuning.MasterOfSubtletyDurationSeconds
            }, _actor.Key, "master-of-subtlety", "post-stealth-damage");
        var target = context.GetActor("target");
        if (target is not { IsAlive: true }) return;
        foreach (var weapon in _weapons)
            _auto.Start(context, _actor.Key, target.Key, weapon);
    }

    private void TriggerHackAndSlash(SimulationContext context, CombatEvent ev)
    {
        var rank = _request.Talents.Rank("hack-and-slash");
        if (rank <= 0 || ev.IsPeriodic || ev.SchoolKey != "physical" ||
            ev.AbilityKey is null ||
            ev.AbilityKey is ("hack-and-slash-extra" or "blade-flurry-cleave" or
                "instant-poison" or "deadly-poison") ||
            ev.WeaponHandKey is not (WeaponHandKeys.MainHand or WeaponHandKeys.OffHand))
            return;
        var weaponType = ev.WeaponHandKey == WeaponHandKeys.MainHand
            ? _request.MainHandWeaponType : _request.OffHandWeaponType;
        if (weaponType is not ("sword" or "axe") ||
            (decimal)context.Random.NextDouble() * 100m >= rank *
                _request.Tuning.HackAndSlashExtraAttackPercentPerRank)
            return;
        var target = ev.TargetActorKey is null ? null : context.GetActor(ev.TargetActorKey);
        if (target is not { IsAlive: true }) return;
        var hand = ev.WeaponHandKey;
        // An extra weapon attack must use the same AP-derived weapon damage and
        // off-hand damage modifiers as the normal swing, not only the weapon's
        // unscaled base damage. Resolve its own hit, crit and mitigation.
        var weapon = _weapons.FirstOrDefault(w => w.WeaponHandKey == hand);
        if (weapon is null) return;
        _executor.ExecuteBackgroundDirectDamage(context, _actor, target,
            "hack-and-slash-extra", "Hack and Slash (extra attack)",
            weapon.DamageEffect);
        HackAndSlashAttacks++;
    }

    private void Cleave(SimulationContext context, CombatEvent ev)
    {
        // Only a real second enemy receives cleave; prevent self-retriggering.
        var other = context.Actors.Values.FirstOrDefault(x => x.IsAlive &&
            !string.Equals(x.TeamKey, _actor.TeamKey, StringComparison.OrdinalIgnoreCase) &&
            x.Key != ev.TargetActorKey);
        if (other is null) return;
        var amount = Math.Max(0m, ev.Amount ?? 0m);
        if (amount <= 0m) return;
        _executor.ExecuteBackgroundDirectDamage(context, _actor, other,
            "blade-flurry-cleave", "Blade Flurry (additional target)",
            new AbilityEffectDefinition {
                Key = "cleave-hit", EffectType = AbilityEffectTypes.DirectDamage,
                TargetType = AbilityTargetTypes.Enemy, SchoolKey = "physical",
                ResolutionType = CombatResolutionTypes.AlwaysHits, CanMiss = false,
                CanCrit = false, MinimumValue = amount, MaximumValue = amount
            });
    }

}
