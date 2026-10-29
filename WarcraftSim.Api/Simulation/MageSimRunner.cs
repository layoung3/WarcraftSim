using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Building;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Healing;
using WarcraftSim.Data.Forever.Combat;
using WarcraftSim.Data.Forever.Simulation;
using WarcraftSim.Data.Forever.Mage;

namespace WarcraftSim.Api.Simulation;

/// <summary>CP132: three Mage single-target rotations with event-driven Arcane,
/// Fire, Frost proc states. All damage calibration remains provisional.</summary>
public sealed class MageSimRunner
{
    public MageSimResponse Run(MageSimRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Character is null || request.Tuning is null || request.Talents is null ||
            request.Character.Level != 60 ||
            !string.Equals(request.Character.ClassKey, "mage", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(request.Character.SpecializationKey, request.Specialization, StringComparison.OrdinalIgnoreCase) ||
            request.DurationSeconds is <= 0m or > 1200m || request.TargetMaximumHealth <= 0m ||
            request.AdditionalTargets is < 0 or > 8 ||
            request.MageMaximumHealth <= 0m || request.StartingManaPercent is < 0m or > 100m ||
            request.IncomingDamageAtSeconds is <0m ||
            request.IncomingDamageAmount <0m ||
            string.IsNullOrWhiteSpace(request.IncomingDamageSchool) ||
            request.BaseSpellHitPercent is < 0m or > 100m || request.SpellCritPercent is < 0m or > 100m ||
            request.ArcaneBlastsBeforeRelease is < 1 or > 4 ||
            request.EvocationBelowManaPercent is < 0m or > 100m ||
            request.MajorManaPotionBelowManaPercent is < 0m or > 100m ||
            request.MajorManaPotionCharges is < 0 or > 20 || request.CombatItems is null)
            throw new ArgumentException("Mage requires a valid level-60 configuration, duration, health, hit and crit.");
        request.CombatItems.Validate("mage");
        request.Talents.Validate(request.Specialization);
        ValidateTuning(request.Tuning);
        var stats = ForeverCharacterSimulationRuntimeCalculatorFactory.CreateStandard(_ => request.MageMaximumHealth)
            .Calculate(request.Character).EffectiveStats;
        var intellect = Math.Max(0m, stats.Get("intellect")) *
            (1m + request.Talents.Rank("arcane-mind") * .02m);
        var spellPower = Math.Max(0m, stats.Get("spell-power"));
        var spirit = Math.Max(0m, stats.Get("spirit"));
        var maxMana = 1376m + Math.Max(0m, intellect - 20m) * 15m;
        var context = new SimulationContext(new SimulationRunOptions
        {
            Seed = request.Seed, DurationSeconds = request.DurationSeconds,
            CaptureTimeline = request.CaptureTimeline, PrimaryActorKey = "mage",
            MaximumProcessedEvents = 60000, CancellationToken = cancellationToken
        });
        var mage = new SimulationActorState
        {
            Key = "mage", Name = request.Character.Name, TeamKey = "raid",
            Level = 60, MaximumHealth = request.MageMaximumHealth, Stats = stats
        };
        mage.InitializeHealth(mage.MaximumHealth);
        mage.AddResource(new ResourceState("mana", maxMana, maxMana * request.StartingManaPercent / 100m));
        var boss = new SimulationActorState
        {
            Key = "target", Name = "Level 63 Mage training boss", TeamKey = "enemy",
            Level = 63, MaximumHealth = request.TargetMaximumHealth
        };
        boss.InitializeHealth(boss.MaximumHealth);
        context.AddActor(mage);
        context.AddActor(boss);
        for (var i = 1; i <= request.AdditionalTargets; i++)
        {
            var target = new SimulationActorState { Key = $"target-{i}",
                Name = $"Additional Mage target {i}", TeamKey = "enemy", Level = 63,
                MaximumHealth = request.TargetMaximumHealth };
            target.InitializeHealth(target.MaximumHealth);
            context.AddActor(target);
        }
        foreach (var ability in ForeverMageDpsSpellbook.Create(request.Talents, request.Tuning, spellPower, spirit, maxMana))
            mage.AddAbility(ability);
        var rotation = MageAdaptiveRotation.Build(request);
        var state = new MageSpecializationProcessor(request, rotation, mage);
        var auraManager = new AuraManager();
        var scorchVulnerability = new MageImprovedScorchProcessor(mage,auraManager,
            request.Talents.Rank("improved-scorch"));
        var absorbs = new AbsorbManager();
        var executor = new AbilityExecutor(auraManager, new MageRollResolver(request, state,scorchVulnerability),
            new MageNoMitigation(),absorbManager:absorbs,abilityTimingProvider: state);
        var incoming = new ScriptedCasterIncomingDamageProcessor(mage,boss,absorbs,
            request.IncomingDamageAtSeconds,request.IncomingDamageAmount,
            request.IncomingDamageSchool,request.IncomingDamageFullyResisted);
        var burningSoulChance=request.Talents.Rank("burning-soul") switch {
            1=>23m,2=>47m,3=>70m,_=>0m };
        var magicAbsorption=new MageMagicAbsorptionProcessor(
            request.Talents.Rank("magic-absorption"));
        var improvedChanneling=new MageImprovedChannelingProcessor(
            executor,request.Talents.Rank("improved-channeling"));
        var firePushback=new CastDamagePushbackProcessor(executor,"mage",
            burningSoulChance,["fire"]);
        state.Attach(auraManager);
        var items = new CombatUseItemProcessor(request.CombatItems, executor, mage, "target", "mage");
        var priority = new PriorityRotationExecutor(rotation, "mage", "target", executor, true);
        var regen = new FiveSecondManaRegenerationProcessor("mage",
            request.Tuning.ManaRegenPerSecond, Math.Max(0m, stats.Get("mp5")),
            request.Talents.Rank("arcane-meditation") * .05m,
            ()=>mage.ActiveAuras.Any(a=>a.Definition.Key=="mage-armor" &&
                a.IsActiveAt(context.CurrentTimeSeconds)) ? .5m : 0m);
        var armorState=new MageArmorResistanceProcessor(
            mage,request.Talents.Rank("arcane-shielding"));
        var engine = new SimulationEngine([executor, auraManager, regen, state,scorchVulnerability,armorState,
            incoming,firePushback,magicAbsorption,improvedChanneling,
            new MageIgniteRollingProcessor(auraManager,
                request.UseIgnite ? request.Talents.Rank("ignite") : 0),
            items, new ThreatManager(new MageDamageThreatResolver(request)), priority]);
        regen.Initialize(context);
        var run = engine.Run(context,started=>incoming.Start(started));
        run.CustomMetrics["mage-burning-soul-resisted-hits"]=firePushback.ResistedHits;
        run.CustomMetrics["mage-cast-pushback-seconds"]=firePushback.TotalPushbackSeconds;
        run.CustomMetrics["mage-improved-channeling-resisted"]=improvedChanneling.ResistedHits;
        run.CustomMetrics["mage-arcane-channel-cancellations"]=improvedChanneling.ChannelsCancelled;
        run.CustomMetrics["mage-arcane-cast-pushback-seconds"]=improvedChanneling.CastPushbackSeconds;
        run.CustomMetrics["mage-magic-absorption-refunds"]=magicAbsorption.Refunds;
        run.CustomMetrics["mage-armor-arcane-resistance"] = mage.Stats.Get("arcane-resistance");
        run.CustomMetrics["mage-mana-regen-rate-end"] = mage.Resources["mana"].RegenerationPerSecond;
        run.CustomMetrics["mage-missile-barrage-active-at-end"]=
            state.MissileBarrageActiveAt(request.DurationSeconds)?1m:0m;
        run.CustomMetrics["mage-fingers-charges-granted"] = state.FingersChargesGranted;
        run.CustomMetrics["mage-fingers-charges-end"] = state.FingersChargesAt(request.DurationSeconds);
        run.CustomMetrics["mage-fingers-chill-triggers"] = state.ChillTriggers;
        run.CustomMetrics["mage-heating-up-charges-granted"] = state.HeatingUpChargesGranted;
        run.CustomMetrics["mage-heating-up-charges-at-end"] = state.HeatingUpChargesAt(request.DurationSeconds);
        run.CustomMetrics["mage-master-of-elements-refund"] = state.MasterOfElementsManaRestored;
        run.CustomMetrics["mage-improved-scorch-applications"] = scorchVulnerability.Applications;
        var owner = run.Summary.ActorSummaries["mage"];
        var breakdown = new Dictionary<string, decimal>(owner.DamageDoneByAbility, StringComparer.OrdinalIgnoreCase);
        return new MageSimResponse
        {
            Specialization = request.Specialization,
            ResolvedRotation = string.Join(" > ",rotation.Entries.Select(x=>x.AbilityKey)),
            DamageDone = owner.DamageDone, Dps = owner.DamageDone / request.DurationSeconds,
            EndingHealth = mage.CurrentHealth,
            EndingMana = mage.Resources["mana"].Current, MaximumMana = maxMana, SpellPower = spellPower,
            MissileBarrageProcs = state.MissileBarrageProcs,
            ClearcastingProcs = state.ClearcastingProcs,
            ClearcastingConsumed = state.ClearcastingConsumed,
            EvocationCasts = owner.Abilities.TryGetValue("evocation", out var evoc) ? evoc.ChannelStartedCount : 0,
            MajorManaPotionUses = state.MajorManaPotionUses,
            CombatItemUses = new Dictionary<string, int>(items.Uses, StringComparer.OrdinalIgnoreCase),
            ManaRestoredByEvocation = state.EvocationManaGained,
            ManaRestoredByPotions = state.PotionManaGained,
            ArcaneWrathCasts = owner.Abilities.TryGetValue("arcane-wrath", out var wrath) ? wrath.CastCompletedCount : 0,
            ArcaneBlastCasts = owner.Abilities.TryGetValue("arcane-blast", out var blast) ? blast.CastCompletedCount : 0,
            HeatingUpPyroblasts = state.HeatingUpPyroblasts,
            FingersOfFrostProcs = state.FingersOfFrostProcs,
            FingersOfFrostIceLances = state.FingersOfFrostIceLances,
            IgniteDamage = breakdown.GetValueOrDefault("mage-ignite"),
            DamageByAbility = breakdown, Run = run,
            ModelingNotes =
            [
                "CP132 models three Mage solo DPS priorities with the shared cast, channel, aura and resource event engine.",
                "70425 Arcane Blast casts in 1.5s and grants +15% Intellect as Spell Damage per stack, max 4, for 15 seconds from first application; the stacks persist through other spells.",
                "70425 Missile Barrage has 2 ranks: 20/40% Blast, 10/20% other eligible spell and 5/10% per Missiles hit. Procs affect only one channel.",
                "Heating Up stacks on non-periodic eligible Fire criticals, reducing Pyroblast casting time by 25% per stack up to 3.",
                "70425 frozen Ice Lance bonus is +200%, not +300%; Winter's Chill no longer gives crit chance and instead stores a Cone of Cold damage bonus (Cone not yet modeled).",
                "Frostfire Bolt is modeled as Fire damage with both Fire/Frost talent multipliers and a periodic effect; dual-school resistance selection and stacking require later validation.",
                "Ignite uses source-owned 4-second two-tick rolling damage; its exact server rollover and final damage coefficients remain numerically provisional.",
                "Talents include modeled subset with non-effective filler points; full 51-point tree prerequisites and final spell ranks are not verified.",
                "CP132.1 adds cross-spec Evocation (8s/480s, Spirit-based restoration) and optional Major Mana Potion (1350-2250, 120s cooldown, configured inventory charge limit).",
                "CP132.2 adds a reusable item catalog with Mana Ruby, Dark/Demonic Runes, Dense Dynamite, Goblin Sapper and Mighty Rage Potion. Their inventory, resource use, cooldown group and engineering requirements are modeled.",
                "CP139 adaptive default: Arcane varies Blast release stacks by remaining duration and Mana; Fire limits proc Pyro use at the encounter end; Frost only uses Ice Lance with Fingers charges. CustomPriority overrides default spell decisions.",
                "CP139.1 replaces old Arcane Blast release damage with timed Int-based Spell Damage, adds provisional Arcane Wrath and Frost Blast ranks, and applies the Fire/Frost changes from October 15. Remaining levels/consumes and crowd-control/AoE are provisional.",
                "CP167 70685: Mage Armor, Arcane Power, Combustion and explicit-target AoE are executable. Raid geometry, external buffs and Water Elemental companion remain outside this solo certification."
            ]
        };
    }

    private static void ValidateTuning(ForeverMageTuning t)
    {
        var values = new[] { t.FireballDamage,t.FireballMana,t.FireballCastSeconds,
            t.FrostboltDamage,t.FrostboltMana,t.FrostboltCastSeconds,
            t.ArcaneBlastDamage,t.ArcaneBlastMana,t.ArcaneBlastBonusDamagePerStackPercent,
            t.ScorchDamage,t.ScorchMana,
            t.ArcaneBlastExtraManaPerStackPercent,t.ArcaneWrathDamage,t.ArcaneWrathMana,t.FrostBlastDamage,t.FrostBlastMana,t.FrostBlastSpellPowerCoefficient,t.MissileBarrageArcaneMissileChancePercentPerRank,t.ArcaneMissilesTick,t.ArcaneMissilesMana,
            t.PyroblastDamage,t.PyroblastMana,t.PyroblastDotTick,t.PyroblastDotPowerPerTick,
            t.FireBlastDamage,t.FireBlastMana,
            t.FrostfireBoltDamage,t.FrostfireBoltMana,t.FrostfireDotTick,
            t.IceLanceDamage,t.IceLanceMana,t.MissileBarrageArcaneBlastChancePercent,
            t.MissileBarrageOtherChancePercent,t.FingersOfFrostChancePercentPerRank,
            t.ManaRegenPerSecond };
        if (values.Any(v => v < 0m) || t.FireballCastSeconds <= 0m ||
            t.FrostboltCastSeconds <= 0m ||
            t.MissileBarrageArcaneBlastChancePercent > 100m ||
            t.MissileBarrageOtherChancePercent > 100m ||
            t.MissileBarrageArcaneMissileChancePercentPerRank > 50m ||
            t.FingersOfFrostChancePercentPerRank > 50m)
            throw new ArgumentException("Mage damage/cost/timing must be nonnegative; proc chances must be valid.");
    }

    private sealed class MageNoMitigation : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext context, SimulationActorState source,
            SimulationActorState target, AbilityDefinition ability,
            AbilityEffectDefinition effect, decimal rawAmount) => DamageMitigationResult.Unmitigated(rawAmount);
    }

    private sealed class MageDamageThreatResolver(MageSimRequest request) : IThreatGenerationResolver
    {
        public IReadOnlyList<ThreatGenerationContribution> Resolve(
            SimulationContext context, CombatEvent ev)
        {
            if (ev.Type != CombatEventType.Damage || ev.SourceActorKey != "mage" ||
                ev.TargetActorKey is null || ev.Amount is not > 0m ||
                context.GetActor(ev.TargetActorKey) is not { TeamKey: "enemy" })
                return [];
            // Frost Channeling reduces FROST threat by 10% per talent rank.
            // Frostfire Bolt is a dual-school spell, represented as Fire for
            // damage; its Frost threat qualification is kept explicit.
            var frost = ev.SchoolKey == "frost" || ev.AbilityKey == "frostfire-bolt";
            var modifier = frost ? 1m - .10m * request.Talents.Rank("frost-channeling") : 1m;
            // Burning Soul reduces Fire threat by 10/20/30%.
            if(ev.SchoolKey=="fire" || ev.AbilityKey=="frostfire-bolt")
                modifier*=1m-.10m*request.Talents.Rank("burning-soul");
            return [new ThreatGenerationContribution {
                ThreatOwnerActorKey = ev.TargetActorKey,
                ThreatSourceActorKey = "mage",
                Amount = Math.Max(0m, ev.Amount.Value * modifier)
            }];
        }
    }

    private sealed class MageRollResolver(MageSimRequest request,
        MageSpecializationProcessor state, MageImprovedScorchProcessor scorch) : ICombatRollResolver
    {
        public CombatRollResult Resolve(SimulationContext context, SimulationActorState source,
            SimulationActorState target, AbilityDefinition ability, AbilityEffectDefinition effect) =>
            Resolve(context, source, target, ability, effect, CombatEffectDeliveryType.Direct);
        public CombatRollResult Resolve(SimulationContext context, SimulationActorState source,
            SimulationActorState target, AbilityDefinition ability, AbilityEffectDefinition effect,
            CombatEffectDeliveryType deliveryType)
        {
            // Ignite is derived from the already-modified Fire critical hit;
            // do not apply the vulnerability twice on its separate ticks.
            var fireVulnerability = effect.SchoolKey=="fire" &&
                ability.Key!="mage-ignite" &&
                source.TeamKey != target.TeamKey
                ? scorch.FireDamageMultiplier(target.Key,context.CurrentTimeSeconds) : 1m;
            // Fire Power is target-owned and reads actual active Fire auras
            // at damage resolution, not a global "recent casts" tracker.
            // Frostfire Bolt mixes schools and must never qualify.
            if(effect.SchoolKey=="fire" && ability.Key is not ("mage-ignite" or "frostfire-bolt") &&
                source.TeamKey!=target.TeamKey && request.Talents.Rank("fire-power")>0)
            {
                var fireEffects=target.ActiveAuras.Count(a=>
                    a.SourceActorKey==source.Key &&
                    a.IsActiveAt(context.CurrentTimeSeconds) &&
                    a.Definition.Key is ("fireball-dot" or "pyroblast-dot" or
                        "flamestrike" or "mage-ignite"));
                // Improved Scorch is a Fire vulnerability, not a distinct
                // damaging Fire effect for the Fire Power count.
                fireVulnerability*=1m+
                    Math.Min(4,fireEffects)*request.Talents.Rank("fire-power")*.01m;
            }
            // Winter's Chill is owned by the actual enemy. Applying the
            // modifier at each target's resolution (rather than mutating the
            // shared ability on cast start) also makes Cone AoE target-safe.
            if(ability.Key=="cone-of-cold" && effect.Key=="cone-of-cold-impact")
                fireVulnerability*=state.WintersChillConeMultiplier(
                    target.Key,context.CurrentTimeSeconds,effect.MinimumValue,
                    source.Stats.Get("spell-power"));
            // Build 70685 Ice Barrier: 3% Frost damage for the full
            // one-minute aura, another 3% while its actual absorb survives.
            // Source/target state is evaluated at damage resolution; shield
            // depletion immediately removes only the conditional component.
            if(effect.SchoolKey=="frost" && source.TeamKey!=target.TeamKey &&
                source.ActiveAuras.Any(a=>
                    a.Definition.Key=="mage-ice-barrier-frost-offense" &&
                    a.SourceActorKey==source.Key &&
                    a.IsActiveAt(context.CurrentTimeSeconds)))
            {
                var shieldHolds=source.ActiveAbsorbs.Any(a=>
                    a.AbsorbKey=="mage-ice-barrier" &&
                    a.SourceActorKey==source.Key &&
                    a.IsActiveAt(context.CurrentTimeSeconds));
                // Additive +3 percentage points while the shield holds,
                // not compounding 1.03*1.03 into an invented 6.09%.
                fireVulnerability*=shieldHolds?1.06m:1.03m;
            }
            if (!effect.CanMiss && !effect.CanCrit)
                return new CombatRollResult {Landed=true,ResultKey=CombatResultTypes.Hit,
                    AmountMultiplier=fireVulnerability};
            var arcane = effect.SchoolKey == "arcane";
            // 70685 removes Arcane Focus; Precision now grants 2% hit
            // per rank to EVERY spell school, including Arcane.
            var hitChance = Math.Min(100m, request.BaseSpellHitPercent +
                source.Stats.Get("spell-hit-percent") +
                source.Stats.Get("spell-hit-chance-percent") +
                2m*request.Talents.Rank("precision"));
            if (effect.CanMiss && (decimal)context.Random.NextDouble() * 100m >= hitChance)
                return CombatRollResult.Miss();
            var crit = request.SpellCritPercent + source.Stats.Get("spell-critical-chance-percent") +
                effect.BonusCriticalChancePercent + request.Talents.Rank("arcane-instability");
            if (effect.SchoolKey == "arcane") crit += request.Talents.Rank("arcane-impact") * 2m;
            if (effect.SchoolKey == "fire") crit += request.Talents.Rank("critical-mass") * 2m;
            if (effect.SchoolKey == "fire" && deliveryType == CombatEffectDeliveryType.Direct &&
                state.CombustionActive) crit += state.CombustionHits * 10m;
            // Forever Incineration affects the named spells across all three
            // schools, not every Fire spell. Three ranks, +2% each.
            if (ability.Key is "fire-blast" or "arcane-blast" or "ice-lance" or "scorch")
                crit += request.Talents.Rank("incineration") * 2m;
            // Current Forever Shatter: 17/33/50% crit for ALL spells
            // against targets treated as Frozen by a Fingers of Frost charge.
            var frozen=state.IsFrozenSpellDamage(
                context.CurrentResolvingAbilityExecutionId,ability.Key,deliveryType);
            if(frozen)
                crit += request.Talents.Rank("shatter") switch {
                    1=>17m,2=>33m,3=>50m,_=>0m};
            // Ice Lance's triple damage belongs to the original
            // execution that spent FoF, not a mutable global spell value.
            if(frozen && ability.Key=="ice-lance")
                fireVulnerability*=3m;
            if (effect.CanCrit && (decimal)context.Random.NextDouble() * 100m < Math.Min(100m, crit))
            {
                var multiplier = effect.SchoolKey == "frost" || ability.Key == "frostfire-bolt" ?
                    1.5m + request.Talents.Rank("ice-shards") * .1m : 1.5m;
                // +20% of the ordinary +50% CRITICAL BONUS per rank,
                // rather than a flat +2% critical multiplier per rank.
                if (arcane) multiplier += request.Talents.Rank("arcane-mind") * .10m;
                return CombatRollResult.Critical(multiplier*fireVulnerability);
            }
            return new CombatRollResult {Landed=true,ResultKey=CombatResultTypes.Hit,
                AmountMultiplier=fireVulnerability};
        }
    }

    private sealed class MageSpecializationProcessor : ICombatEventProcessor, IAbilityTimingProvider
    {
        private readonly MageSimRequest _r;
        private readonly RotationProfile _rotation;
        private readonly SimulationActorState _actor;
        private readonly Dictionary<string, decimal> _baseMultipliers = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, decimal> _baseAmounts = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, decimal> _baseManaCosts = new(StringComparer.OrdinalIgnoreCase);
        private AuraManager? _auras;
        private bool _clearcasting;
        private decimal _clearcastingExpiresAt;
        private decimal _clearcastingNextProcAt;
        private Guid? _clearcastingReservedCast;
        // A proc from a previously completed impact can arrive after the
        // priority executor already STARTED another cast at this timestamp.
        // That in-flight cast paid/committed its original cost and cannot
        // consume a newly earned Clearcasting benefit retroactively.
        private Guid? _castAlreadyInProgressAtClearcastProc;
        public int ClearcastingProcs { get; private set; }
        public int ClearcastingConsumed { get; private set; }
        private decimal _intellect;
        private decimal _arcaneSpellDamage;
        public decimal EvocationManaGained { get; private set; }
        public decimal PotionManaGained { get; private set; }
        public decimal MasterOfElementsManaRestored { get; private set; }
        private bool _presenceOfMind;
        private bool _arcanePower;
        private int _combustionHits;
        private int _combustionCrits;
        private bool _combustionActive;
        private int _arcaneStacks;
        private decimal _currentTime;
        private decimal _arcaneExpires;
        private int _heatingUp;
        private decimal _heatingUpExpiresAt;
        public int HeatingUpChargesGranted { get; private set; }
        public int HeatingUpChargesAt(decimal now) => now>=_heatingUpExpiresAt?0:_heatingUp;
        private int _fingers;
        private decimal _fingersExpiresAt;
        public int FingersChargesGranted { get; private set; }
        public int FingersChargesAt(decimal now) =>
            now >= _fingersExpiresAt ? 0 : _fingers;
        public int ChillTriggers { get; private set; }
        private bool _barrage;
        private decimal _barrageExpiresAt;
        public bool MissileBarrageActiveAt(decimal now) => _barrage && now<_barrageExpiresAt;
        private bool _barrageCastPending;
        // A distinct 15s source-owned Winter's Chill window per enemy.
        // An AoE Cone consumes only the targets it actually damages.
        private readonly Dictionary<string,(int Stacks,decimal ExpiresAt)> _winterByTarget=
            new(StringComparer.OrdinalIgnoreCase);
        public int MajorManaPotionUses { get; private set; }
        public int MissileBarrageProcs { get; private set; }
        public int HeatingUpPyroblasts { get; private set; }
        public int FingersOfFrostProcs { get; private set; }
        public int FingersOfFrostIceLances { get; private set; }
        public int WintersChillStacks => WinterStacks("target",_currentTime);
        public int WinterStacks(string targetKey,decimal now) =>
            _winterByTarget.TryGetValue(targetKey,out var state) &&
            state.ExpiresAt>now?state.Stacks:0;
        public decimal WintersChillConeMultiplier(
            string targetKey,decimal now,decimal baseDamage,decimal spellPower)
        {
            var stacks=WinterStacks(targetKey,now);
            if(stacks<=0)return 1m;
            // The 70532 source describes +5% Cone damage and +7% SP
            // per stack. A competing client export describes 40% total.
            // Preserve existing provisional numbers while fixing ownership.
            var extra=stacks*.07m*Math.Max(0m,spellPower);
            return (1m+stacks*.05m)*
                (baseDamage>0m?1m+extra/baseDamage:1m);
        }
        public bool CurrentIceLanceFrozen { get; private set; }
        public bool CurrentSpellFrozen { get; private set; }
        private string? _frozenSpellKey;
        // FoF belongs to the cast that SPENT a charge, not whichever new
        // cast the actor may have started before prior damage arrives.
        private readonly Dictionary<Guid,string> _frozenExecutions=new();
        public bool IsFrozenSpellDamage(Guid? executionId,string key,
            CombatEffectDeliveryType delivery) =>
            executionId is { } id && _frozenExecutions.TryGetValue(id,out var usedKey) &&
            string.Equals(key,usedKey,StringComparison.OrdinalIgnoreCase) &&
            delivery!=CombatEffectDeliveryType.Periodic;
        public bool CombustionActive => _combustionActive;
        public bool ArcanePowerActive => _arcanePower; 
        public int CombustionHits => _combustionHits;
        public MageSpecializationProcessor(MageSimRequest r, RotationProfile rotation, SimulationActorState actor)
        {
            _r = r; _rotation = rotation; _actor = actor;
            foreach (var a in actor.Abilities.Values)
                foreach (var effect in a.Definition.Effects)
                {
                    var id=$"{a.Definition.Key}:{effect.Key}";
                    _baseMultipliers[id] = effect.DamageMultiplier;
                    _baseAmounts[id] = effect.MinimumValue;
                }

            foreach (var entry in actor.Abilities.Values)
                if (IsDamageSpell(entry.Definition) &&
                    entry.Definition.ResourceCosts.FirstOrDefault(c => c.ResourceKey == "mana") is { } mana)
                    _baseManaCosts[entry.Definition.Key] = mana.Amount;
            _intellect=Math.Max(0m,actor.Stats.Get("intellect"))*(1m+r.Talents.Rank("arcane-mind")*.02m);
            RefreshRotation();
        }
        public void Attach(AuraManager auras) => _auras = auras;

        private static bool IsDamageSpell(AbilityDefinition spell) =>
            spell.Key != "mage-ignite" &&
            spell.Effects.Any(e => e.EffectType is
                AbilityEffectTypes.DirectDamage or AbilityEffectTypes.PeriodicDamage);

        // The executor validates resource availability when the cast starts
        // and spends the cost when it completes. Keep a free spell's cost at
        // zero throughout its cast, restoring its current stacked cost only
        // after the successful completion event.
        private bool IsSpellCastInProgress(string key) =>
            _actor.CurrentCastExecutionId.HasValue &&
            string.Equals(_actor.CurrentCastAbilityKey,key,
                StringComparison.OrdinalIgnoreCase);

        private void RefreshClearcastingCosts()
        {
            foreach (var (key, baseCost) in _baseManaCosts)
            {
                if (key is "arcane-blast" or "arcane-missiles" ||
                    IsSpellCastInProgress(key)) continue;
                if (_actor.Abilities.TryGetValue(key, out var entry))
                {
                    var cost = entry.Definition.ResourceCosts
                        .FirstOrDefault(c => c.ResourceKey == "mana");
                    if (cost is not null) cost.Amount = _clearcasting ? 0m :
                        baseCost * (_arcanePower ? 1.30m : 1m);
                }
            }
            RefreshArcaneCost();
            RefreshMissilesCost();
        }

        private void EndClearcasting(SimulationContext context, string reason)
        {
            if (!_clearcasting) return;
            _clearcasting = false;
            _clearcastingReservedCast = null;
            _castAlreadyInProgressAtClearcastProc = null;
            _auras?.RemoveAuras(context, _actor, "mage-clearcasting", _actor.Key, reason);
            RefreshClearcastingCosts();
        }

        private void TryProcClearcasting(SimulationContext context, CombatEvent ev)
        {
            var rank = _r.Talents.Rank("arcane-concentration");
            if (rank <= 0 || _clearcasting || context.CurrentTimeSeconds < _clearcastingNextProcAt ||
                ev.IsPeriodic || ev.Amount is not > 0m ||
                ev.ResultKey is not ("hit" or "critical") ||
                ev.AbilityKey is null || !_actor.Abilities.TryGetValue(ev.AbilityKey, out var spell) ||
                !IsDamageSpell(spell.Definition)) return;
            // Source-backed 2% per rank; one-second proc cooldown prevents
            // multiple simultaneous AoE/channel ticks from granting duplicates.
            if ((decimal)context.Random.NextDouble() * 100m >= 2m * rank) return;
            _clearcasting = true;
            _castAlreadyInProgressAtClearcastProc=_actor.CurrentCastExecutionId;
            _clearcastingExpiresAt = context.CurrentTimeSeconds + 15m;
            _clearcastingNextProcAt = context.CurrentTimeSeconds + 1m;
            ClearcastingProcs++;
            _auras?.ApplyAura(context, _actor, new AuraDefinition {
                Key = "mage-clearcasting", Name = "Clearcasting",
                DurationSeconds = 15m
            }, _actor.Key, "arcane-concentration", "clearcasting");
            RefreshClearcastingCosts();
        }
        public AbilityTimingSnapshot Resolve(SimulationContext context, SimulationActorState source, AbilityDefinition ability)
        {
            var pyroTime = ability.Key == "pyroblast" && _r.UseHeatingUp ?
                Math.Max(1.5m, ability.CastTimeSeconds * (1m - _heatingUp * .25m)) : ability.CastTimeSeconds;
            return new AbilityTimingSnapshot
            {
                CastTimeSeconds = _presenceOfMind && pyroTime > 0m && pyroTime < 10m &&
                    IsDamageSpell(ability) ? 0m : pyroTime,
                GlobalCooldownSeconds = ability.GlobalCooldownSeconds,
                ChannelDurationSeconds = ability.Key == "arcane-missiles" && _barrage ? 2.5m : ability.ChannelDurationSeconds,
                ChannelTickIntervalSeconds = ability.Key == "arcane-missiles" && _barrage ? .5m : ability.ChannelTickIntervalSeconds
            };
        }
        public void Process(SimulationContext context, CombatEvent ev)
        {
            if (ev.Type == CombatEventType.SimulationEnded) return;
            _currentTime=context.CurrentTimeSeconds;
            if (_clearcasting && _clearcastingReservedCast is null &&
                context.CurrentTimeSeconds >= _clearcastingExpiresAt)
                EndClearcasting(context, "clearcasting expired");
            if (_arcaneStacks > 0 && context.CurrentTimeSeconds >= _arcaneExpires)
            {
                _arcaneStacks = 0;
                _arcaneSpellDamage=0m;
                RefreshArcaneCost();
            }
            // No global Winter expiry: each target uses its own exact
            // time boundary in WinterStacks(). Stale entries are harmless.
            if (_fingers > 0 && context.CurrentTimeSeconds >= _fingersExpiresAt)
                _fingers = 0;
            if (_heatingUp > 0 && context.CurrentTimeSeconds >= _heatingUpExpiresAt)
                _heatingUp = 0;
            if (_barrage && context.CurrentTimeSeconds>=_barrageExpiresAt)
            {
                _barrage=false;
                // The channel already in progress retains its start snapshot.
                RefreshMissilesCost();
            }
            if (!string.Equals(ev.SourceActorKey, "mage", StringComparison.OrdinalIgnoreCase))
            {
                if (ev.Type == CombatEventType.RotationDecision) RefreshRotation();
                return;
            }
            switch (ev.Type)
            {
                case CombatEventType.AbilityCastStarted:
                    // The October 15 change makes Presence of Mind take
                    // precedence: a PoM Pyroblast does NOT consume Heating Up.
                    var pyroStartedWithPom = ev.AbilityKey=="pyroblast" && _presenceOfMind;
                    // A queued Presence of Mind applies only to an eligible hard cast,
                    // not to channels, passive procs or other instant abilities.
                    if (_presenceOfMind && ev.AbilityKey is not null &&
                        _actor.Abilities.TryGetValue(ev.AbilityKey, out var pomAbility) &&
                        IsDamageSpell(pomAbility.Definition) &&
                        pomAbility.Definition.CastTimeSeconds is > 0m and < 10m)
                    {
                        _presenceOfMind = false;
                        _auras?.RemoveAuras(context, _actor, "mage-presence-of-mind",
                            _actor.Key, "eligible Mage spell started");
                    }
                    if (_clearcasting && ev.AbilityExecutionId.HasValue &&
                        ev.AbilityExecutionId!=_castAlreadyInProgressAtClearcastProc &&
                        ev.AbilityKey is not null &&
                        _actor.Abilities.TryGetValue(ev.AbilityKey, out var freeSpell) &&
                        IsDamageSpell(freeSpell.Definition))
                        _clearcastingReservedCast = ev.AbilityExecutionId;
                    if (ev.AbilityKey == "mage-ignite") break;
                    CurrentSpellFrozen = false;
                    _frozenSpellKey = null;
                    if(ev.AbilityKey is not null &&
                        _actor.Abilities.TryGetValue(ev.AbilityKey,out var frozenCandidate) &&
                        IsDamageSpell(frozenCandidate.Definition) && _fingers>0)
                    {
                        CurrentSpellFrozen = true;
                        _frozenSpellKey = ev.AbilityKey;
                        if(ev.AbilityExecutionId is { } frozenExecution)
                            _frozenExecutions[frozenExecution]=ev.AbilityKey;
                        _fingers--;
                        if(ev.AbilityKey=="ice-lance") FingersOfFrostIceLances++;
                    }
                    if (ev.AbilityKey == "arcane-missiles")
                    {
                        _barrageCastPending = _barrage;
                        _barrage = false;
                    }
                    if (ev.AbilityKey == "pyroblast" && _heatingUp > 0 && !pyroStartedWithPom)
                    {
                        HeatingUpPyroblasts++;
                        _heatingUp = 0;
                    }
                    if (ev.AbilityKey == "ice-lance")
                    {
                        CurrentIceLanceFrozen = CurrentSpellFrozen;
                    }
                    if (ev.AbilityKey is not null && _actor.Abilities.TryGetValue(ev.AbilityKey, out var castAbility))
                    {
                        // Snapshot additional spell damage when the cast starts.
                        // Do not mutate reactive Ignite, potions or item effects.
                        foreach (var effect in castAbility.Definition.Effects)
                        {
                            var id=$"{castAbility.Definition.Key}:{effect.Key}";
                            if (!_baseMultipliers.TryGetValue(id,out var multiplier) ||
                                !_baseAmounts.TryGetValue(id,out var raw) ||
                                ev.AbilityKey == "mage-ignite") continue;
                            if (ev.AbilityKey == "cone-of-cold")
                            {
                                // Winter's Chill 70532: each stack increases
                                // the NEXT Cone's damage by 5%, and adds 7%
                                // of the Mage's spell power to its scaling.
                                // Only spells that actually LAND consume it.
                                // Only snapshot Arcane spell-damage and Power
                                // on this cast. Winter's Chill is resolved
                                // separately for EACH target by the roll
                                // resolver and consumed on a landed hit.
                                effect.MinimumValue=raw+.129m*_arcaneSpellDamage;
                                effect.MaximumValue=effect.MinimumValue;
                                effect.DamageMultiplier=multiplier*
                                    (_arcanePower ? 1.3m : 1m);
                                continue;
                            }
                            effect.DamageMultiplier=multiplier*
                                (_arcanePower ? 1.3m : 1m);
                            decimal coef=ev.AbilityKey switch
                            {
                                "arcane-blast" => .4284m, "arcane-wrath" => .857m,
                                "arcane-missiles" => .19m, "fireball" => .857m,
                                "frostbolt" => .814m, "frost-blast" => _r.Tuning.FrostBlastSpellPowerCoefficient,
                                "fire-blast" => .429m, "scorch" => .429m, "pyroblast" => 1.15m,
                                "blast-wave" => .129m,
                                "ice-lance" => .143m, "cone-of-cold" => .129m,
                                "frostfire-bolt" => .857m,
                                _ => 0m
                            };
                            // Periodic components use their own coefficients.
                            // They never inherit a direct impact's full spell
                            // coefficient from Arcane Blast's spell-damage aura.
                            if (effect.EffectType == AbilityEffectTypes.PeriodicDamage)
                                coef = ev.AbilityKey switch {
                                    "frostfire-bolt" => .08m,
                                    // The periodic coefficient for Fireball
                                    // is not independently validated; never
                                    // apply its full direct coefficient per tick.
                                    "fireball" => 0m,
                                    "pyroblast" => _r.Tuning.PyroblastDotPowerPerTick,
                                    _ => coef
                                };
                            effect.MinimumValue=raw+coef*_arcaneSpellDamage;
                            effect.MaximumValue=raw+coef*_arcaneSpellDamage;
                        }
                    }
                    RefreshRotation();
                    break;
                case CombatEventType.AbilityCastCompleted:
                    if(ev.AbilityExecutionId==_castAlreadyInProgressAtClearcastProc)
                        _castAlreadyInProgressAtClearcastProc=null;
                    if (ev.AbilityKey == "cold-snap")
                    {
                        foreach (var frostKey in new[] { "frost-blast", "frost-nova", "cone-of-cold", "ice-barrier", "ice-block" })
                            if (_actor.Abilities.TryGetValue(frostKey, out var frostAbility))
                                frostAbility.ResetCooldown(context.CurrentTimeSeconds);
                    }
                    if (_clearcastingReservedCast.HasValue &&
                        ev.AbilityExecutionId == _clearcastingReservedCast)
                    {
                        ClearcastingConsumed++;
                        EndClearcasting(context, "clearcasting consumed");
                    }
                    if (ev.AbilityKey == "major-mana-potion") MajorManaPotionUses++;
                    // Heating Up is resolved from the landed damage event,
                    // not cast completion: a missed Fire Blast grants no charge.
                    if (ev.AbilityKey == "arcane-missiles") _barrageCastPending = false;
                    if (ev.AbilityKey == "arcane-blast")
                    {
                        if (_arcaneStacks == 0) _arcaneExpires = context.CurrentTimeSeconds + 15m;
                        _arcaneStacks = Math.Min(4, _arcaneStacks + 1);
                        _arcaneSpellDamage = _arcaneStacks * _intellect * .15m;
                        RefreshArcaneCost();
                    }
                    // The just-completed hardcast has paid its reserved
                    // cost. Recompute definitions for the NEXT cast only.
                    RefreshClearcastingCosts(); RefreshRotation();
                    break;
                case CombatEventType.AbilityCastCancelled:
                    if(ev.AbilityExecutionId is { } cancelledId)
                        _frozenExecutions.Remove(cancelledId);
                    if(ev.AbilityExecutionId==_castAlreadyInProgressAtClearcastProc)
                        _castAlreadyInProgressAtClearcastProc=null;
                    if (ev.AbilityExecutionId == _clearcastingReservedCast)
                        _clearcastingReservedCast = null;
                    // Interrupted casts pay no Mana. Restore the up-to-date
                    // next-cast cost after releasing their reserved snapshot.
                    RefreshClearcastingCosts();
                    break;
                case CombatEventType.Damage:
                    // Forever Master of Elements rank 1/2/3: each eligible direct
                    // Fire/Frost critical strike returns 10/20/30% of the spell's
                    // base Mana expense, not its Arcane Power-inflated cost.
                    // A fully absorbed/missed attack never qualifies.
                    if (_r.Talents.Rank("master-of-elements") is > 0 &&
                        ev.IsCritical && ev.Amount is > 0m &&
                        !ev.IsPeriodic && !ev.IsChannelTick &&
                        ev.SchoolKey is ("fire" or "frost") &&
                        ev.AbilityKey is { } criticalKey &&
                        _baseManaCosts.TryGetValue(criticalKey, out var initialCost) &&
                        _actor.Resources.TryGetValue("mana", out var refundMana))
                    {
                        refundMana.AdvanceTo(context.CurrentTimeSeconds);
                        var gained = refundMana.Gain(initialCost *
                            .10m * _r.Talents.Rank("master-of-elements"));
                        if (gained > 0m)
                        {
                            MasterOfElementsManaRestored += gained;
                            context.EmitEvent(new CombatEvent {
                                TimeSeconds = context.CurrentTimeSeconds,
                                Type = CombatEventType.ResourceChanged,
                                SourceActorKey = _actor.Key, TargetActorKey = _actor.Key,
                                AbilityKey = "master-of-elements",
                                EffectKey = criticalKey + "-critical-mana-refund",
                                ResourceKey = "mana", Amount = gained,
                                ResourceTotalAfter = refundMana.Current,
                                Description = "Master of Elements returned base spell Mana."
                            });
                        }
                    }
                    if (_combustionActive && !ev.IsPeriodic && !ev.IsChannelTick &&
                        ev.SchoolKey == "fire" && ev.ResultKey is ("hit" or "critical"))
                    {
                        _combustionHits++;
                        if (ev.IsCritical) _combustionCrits++;
                        if (_combustionCrits >= 3)
                        {
                            _combustionActive = false;
                            _combustionHits = 0;
                            _auras?.RemoveAuras(context, _actor, "mage-combustion", _actor.Key, "three direct Fire crits");
                        }
                    }
                    var landed = ev.ResultKey is "hit" or "critical";
                    var empoweredLance = ev.AbilityKey=="ice-lance" &&
                        IsFrozenSpellDamage(ev.AbilityExecutionId,
                            "ice-lance",ev.EffectDeliveryType);
                    if (ev.AbilityKey == "ice-lance") CurrentIceLanceFrozen = false;
                    if(ev.AbilityKey=="cone-of-cold" && landed &&
                        ev.Amount is >0m && ev.TargetActorKey is { } coneTarget)
                        _winterByTarget.Remove(coneTarget);
                    if (ev.IsChannelTick && ev.AbilityKey == "arcane-missiles" &&
                        (ev.ResultKey is "hit" or "critical") && !_barrage &&
                        _r.UseMissileBarrage && _r.Talents.Rank("missile-barrage") > 0 &&
                        (decimal)context.Random.NextDouble()*100m <
                        _r.Tuning.MissileBarrageArcaneMissileChancePercentPerRank*_r.Talents.Rank("missile-barrage"))
                    {
                        _barrage=true;_barrageExpiresAt=context.CurrentTimeSeconds+15m;
                        MissileBarrageProcs++;
                    }
                    // Forever 70532 Fingers of Frost: each eligible landed Chill
                    // has a 15% chance regardless of rank. Rank 1 grants ONE
                    // frozen-treatment charge; rank 2 grants TWO, for 15 seconds.
                    // Improved Blizzard provides Chill from each successful tick,
                    // without pretending an immune raid boss is actually slowed.
                    var chillApplied = ev.ResultKey is ("hit" or "critical") &&
                        ev.Amount is >0m && ev.TargetActorKey is not null &&
                        context.GetActor(ev.TargetActorKey) is { TeamKey:"enemy" } &&
                        ((!ev.IsPeriodic && !ev.IsChannelTick &&
                            ev.AbilityKey is ("frostbolt" or "frost-blast" or "frostfire-bolt")) ||
                         (ev.AbilityKey=="blizzard" && ev.IsChannelTick &&
                            _r.Talents.Rank("improved-blizzard")>0));
                    if (chillApplied)
                    {
                        ChillTriggers++;
                        if(ev.AbilityKey=="blizzard" && _auras is not null &&
                            context.GetActor(ev.TargetActorKey??"") is { } chillTarget)
                        {
                            var slow = _r.Talents.Rank("improved-blizzard") switch {
                                1=>15,2=>25,3=>40,_=>0};
                            if(slow>0)
                                _auras.ApplyAura(context,chillTarget,new AuraDefinition {
                                    Key="mage-improved-blizzard-chill",
                                    Name=$"Blizzard Chill ({slow}% movement slow, no boss-motion model)",
                                    DurationSeconds=1.5m*
                                        (1m+.11m*_r.Talents.Rank("permafrost"))
                                },_actor.Key,"blizzard","improved-blizzard-chill");
                        }
                        if (_r.UseFingersOfFrost && _r.Talents.Rank("fingers-of-frost") is >0 &&
                            (decimal)context.Random.NextDouble()*100m <
                                _r.Tuning.FingersOfFrostChancePercentPerRank)
                        {
                            _fingers = _r.Talents.Rank("fingers-of-frost");
                            _fingersExpiresAt = context.CurrentTimeSeconds+15m;
                            FingersChargesGranted += _fingers;
                            FingersOfFrostProcs++;
                        }
                    }
                    // Winter's Chill only procs from Frostbolt, Frost Blast,
                    // and Fingers of Frost-empowered Ice Lance after a hit.
                    if (landed && !ev.IsPeriodic && !ev.IsChannelTick &&
                        _r.UseWintersChill &&
                        _r.Talents.Rank("winters-chill") > 0 &&
                        (ev.AbilityKey is "frostbolt" or "frost-blast" ||
                            empoweredLance) &&
                        (decimal)context.Random.NextDouble() * 100m <
                            20m * _r.Talents.Rank("winters-chill"))
                    {
                        if(ev.TargetActorKey is { } chilledTarget)
                            _winterByTarget[chilledTarget]=(
                                Math.Min(5,WinterStacks(chilledTarget,
                                    context.CurrentTimeSeconds)+1),
                                context.CurrentTimeSeconds+15m);
                    }
                    if (!ev.IsPeriodic && !ev.IsChannelTick &&
                        ev.AbilityKey is ("fireball" or "frostfire-bolt" or "fire-blast" or "scorch") &&
                        ev.ResultKey is ("hit" or "critical") && ev.Amount is >0m &&
                        _r.UseHeatingUp && _r.Talents.Rank("heating-up") > 0)
                    {
                        // Current client 70532: one stack on an eligible direct
                        // crit, plus one ADDITIONAL stack when Fire Blast deals
                        // damage (even without a crit). One damage event cannot
                        // generate more than those two stacks.
                        var gain=(ev.IsCritical?1:0)+(ev.AbilityKey=="fire-blast"?1:0);
                        if(gain>0)
                        {
                            var next=Math.Min(3,_heatingUp+gain);
                            HeatingUpChargesGranted+=next-_heatingUp;
                            _heatingUp=next;
                            _heatingUpExpiresAt=context.CurrentTimeSeconds+20m;
                        }
                    }

                    // Barrage procs only from a successfully resolved hit,
                    // never cast completion alone. The hit may resolve after
                    // another action started at the same timestamp.
                    if (!ev.IsPeriodic && !ev.IsChannelTick &&
                        ev.ResultKey is ("hit" or "critical") && ev.Amount is >0m &&
                        _r.UseMissileBarrage && _r.Talents.Rank("missile-barrage")>0 &&
                        ev.AbilityKey is ("arcane-blast" or "arcane-wrath" or "fireball" or
                            "frostbolt" or "frost-blast" or "frostfire-bolt"))
                    {
                        var rank=_r.Talents.Rank("missile-barrage");
                        var chance=(ev.AbilityKey=="arcane-blast"?
                            _r.Tuning.MissileBarrageArcaneBlastChancePercent:
                            _r.Tuning.MissileBarrageOtherChancePercent)*rank/2m;
                        if(!_barrage && (decimal)context.Random.NextDouble()*100m<chance)
                        {
                            _barrage=true;_barrageExpiresAt=context.CurrentTimeSeconds+15m;
                            MissileBarrageProcs++;
                        }
                    }

                    TryProcClearcasting(context, ev);
                    RefreshRotation();
                    break;
                case CombatEventType.AuraApplied:
                    if (ev.AbilityKey == "presence-of-mind")
                        _presenceOfMind = true;
                    if (ev.AbilityKey == "arcane-power")
                    {
                        _arcanePower = true;
                        RefreshClearcastingCosts();
                    }
                    if (ev.AbilityKey == "combustion")
                    {
                        _combustionActive = true;
                        _combustionHits = 0;
                        _combustionCrits = 0;
                    }
                    break;
                case CombatEventType.AuraRemoved:
                    if (ev.EffectKey=="ice-block-aura" && ev.AbilityKey=="ice-block")
                        context.ScheduleEvent(new CombatEvent {
                            TimeSeconds=context.CurrentTimeSeconds,
                            Type=CombatEventType.RotationDecision,
                            SourceActorKey=_actor.Key,TargetActorKey="target",IsInternal=true,
                            Description="Ice Block ended; Mage can cast again"
                        });
                    if (ev.AbilityKey == "presence-of-mind") _presenceOfMind = false;
                    if (ev.AbilityKey == "arcane-power")
                    {
                        _arcanePower = false;
                        RefreshClearcastingCosts();
                    }
                    if (ev.AbilityKey == "combustion") _combustionActive = false;
                    break;
                case CombatEventType.ActorDied:
                    if(ev.TargetActorKey is { } deadTarget)
                        _winterByTarget.Remove(deadTarget);
                    break;
                case CombatEventType.ResourceChanged:
                    if (ev.Amount > 0m && ev.AbilityKey=="evocation")
                        EvocationManaGained+=ev.Amount.Value;
                    if (ev.Amount > 0m && ev.AbilityKey=="major-mana-potion")
                        PotionManaGained+=ev.Amount.Value;
                    break;
                case CombatEventType.RotationDecision: RefreshRotation(); break;
            }
        }
        private void RefreshArcaneCost()
        {
            if (IsSpellCastInProgress("arcane-blast") ||
                !_actor.Abilities.TryGetValue("arcane-blast", out var s)) return;
            s.Definition.ResourceCosts[0].Amount = _clearcasting ? 0m :
                _r.Tuning.ArcaneBlastMana *
                (1m + _arcaneStacks * _r.Tuning.ArcaneBlastExtraManaPerStackPercent / 100m)*
                (_arcanePower ? 1.30m : 1m);
        }
        private void RefreshMissilesCost()
        {
            if (IsSpellCastInProgress("arcane-missiles") ||
                !_actor.Abilities.TryGetValue("arcane-missiles", out var s)) return;
            s.Definition.ResourceCosts[0].Amount = _clearcasting || _barrage || _barrageCastPending
                ? 0m : _r.Tuning.ArcaneMissilesMana * (_arcanePower ? 1.30m : 1m);
        }
        private void RefreshRotation()
        {
            var remaining=Math.Max(0m,_r.DurationSeconds-_currentTime);
            var mana=_actor.Resources.TryGetValue("mana",out var pool) ? pool.Current : 0m;
            var manaPercent=pool is not null&&pool.Maximum>0m ? mana*100m/pool.Maximum : 100m;
            // Longer fights with low resources favor shorter Arcane Blast cycles.
            // Never raise the user-configured stack ceiling.
            var releaseAt=_r.ArcaneBlastsBeforeRelease;
            if(_r.RotationPolicy=="adaptive"&&_r.CustomPriority.Count==0&&remaining>30m)
            {
                if(manaPercent<40m)releaseAt=Math.Min(releaseAt,1);
                else if(manaPercent<70m)releaseAt=Math.Min(releaseAt,2);
            }
            foreach (var entry in _rotation.Entries)
                entry.IsEnabled = entry.AbilityKey switch
                {
                    "evocation" => _r.UseEvocation &&
                        (_r.RotationPolicy=="legacy" || remaining>9m),
                    "major-mana-potion" => _r.UseMajorManaPotion &&
                        MajorManaPotionUses < _r.MajorManaPotionCharges &&
                        (_r.RotationPolicy=="legacy" || remaining>2m),
                    "arcane-missiles" => _r.CustomPriority.Count>0 || _barrage,
                    "arcane-wrath" => _r.CustomPriority.Count>0 || _r.Talents.Rank("arcane-wrath")>0 && !_barrage,
                    "arcane-blast" => _r.CustomPriority.Count>0 || !_barrage &&
                        (_arcaneStacks < releaseAt || _arcaneExpires-_currentTime < 2m),
                    "fireball" or "frostfire-bolt" when _r.Specialization == "arcane" =>
                        _r.CustomPriority.Count>0 || _r.Talents.Rank("arcane-blast")==0 ||
                        !_barrage && _arcaneStacks >= releaseAt,
                    "pyroblast" => _r.CustomPriority.Count>0 || _heatingUp>=3 ||
                        (_r.RotationPolicy=="adaptive"&&remaining<=6m&&_heatingUp>=2),
                    "ice-lance" => _r.CustomPriority.Count>0 || _fingers > 0,
                    _ => true
                };
            RefreshMissilesCost();
        }
    }
}
