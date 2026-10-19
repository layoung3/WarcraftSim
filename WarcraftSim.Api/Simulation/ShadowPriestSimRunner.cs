using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Healing;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Priest;

namespace WarcraftSim.Api.Simulation;

/// <summary>CP129 first true Shadow DPS actor over the shared Core combat engine.</summary>
public sealed class ShadowPriestSimRunner
{
    public ShadowPriestSimResponse Run(ShadowPriestSimRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.DurationSeconds <= 0m || request.DurationSeconds > 1200m ||
            request.TargetMaximumHealth <= 0m || request.PriestMaximumHealth <= 0m ||
            request.TargetInitialHealthPercent is <= 0m or > 100m ||
            request.BaseSpellHitPercent is < 0m or > 100m ||
            request.ManaRegenerationWhileCastingFraction is < 0m or > 1m ||
            request.SpellCritOverridePercent > 100m || request.SpellPowerOverride < -1m)
            throw new ArgumentException("Invalid Shadow Priest encounter parameters.");
        if (request.Character is null || request.Talents is null || request.Tuning is null ||
            request.StatAssumptions is null)
            throw new ArgumentException("Shadow Priest requires character, talents, tuning and stat assumptions.");
        if (request.Character.Level != 60 || request.Character.SpecializationKey != "shadow")
            throw new ArgumentException("CP129 supports a level-60 Shadow Priest character.");
        request.Talents.Validate(request.Character.Level);

        var derived = ForeverPriestCharacterCalculator.Calculate(request.Character,
            request.StatAssumptions);
        var spellPower = request.SpellPowerOverride >= 0m ? request.SpellPowerOverride :
            Math.Max(0m, derived.EffectiveStats.GetValueOrDefault("spell-power") +
                derived.EffectiveStats.GetValueOrDefault("shadow-power"));
        var crit = request.SpellCritOverridePercent >= 0m ? request.SpellCritOverridePercent :
            derived.HealingCritPercent;
        var context = new SimulationContext(new SimulationRunOptions
        {
            Seed = request.Seed, DurationSeconds = request.DurationSeconds,
            PrimaryActorKey = "priest", CaptureTimeline = request.CaptureTimeline,
            MaximumProcessedEvents = 30000, CancellationToken = cancellationToken
        });
        var priest = new SimulationActorState
        {
            Key = "priest", Name = request.Character.Name, TeamKey = "raid",
            Level = 60, MaximumHealth = request.PriestMaximumHealth,
            Stats = new StatCollection { Values = new Dictionary<string, decimal>(
                derived.EffectiveStats, StringComparer.OrdinalIgnoreCase) }
        };
        priest.InitializeHealth(priest.MaximumHealth);
        priest.AddResource(new ResourceState("mana", derived.MaximumMana, derived.MaximumMana));
        var boss = new SimulationActorState
        {
            Key = "target", Name = "Level 63 target (Shadow preview)", TeamKey = "enemy",
            Level = 63, MaximumHealth = request.TargetMaximumHealth
        };
        boss.InitializeHealth(request.TargetMaximumHealth,
            request.TargetMaximumHealth * request.TargetInitialHealthPercent / 100m);
        context.AddActor(priest);
        context.AddActor(boss);
        var form = request.Talents.Rank("shadowform") > 0;
        var baseDamageBonus = request.Talents.Rank("darkness") * 2m + (form ? 10m : 0m);
        priest.Stats.Set("shadow-damage-bonus-percent", baseDamageBonus);
        foreach (var ability in ForeverPriestShadowSpellbook.Create(request.Tuning,
            request.Talents, spellPower)) priest.AddAbility(ability);

        var auraManager = new AuraManager();
        var roll = new ShadowPriestRollResolver(
            Math.Min(100m, request.BaseSpellHitPercent + request.Talents.Rank("shadow-focus") * 2m +
                priest.Stats.Get("spell-hit-percent")),
            Math.Clamp(crit, 0m, 100m), form ? 3m : 2m);
        var executor = new AbilityExecutor(auraManager, roll, new ShadowPriestNoMitigation());
        var rotation = BuildRotation(request, priest);
        var priority = new PriorityRotationExecutor(rotation, "priest", "target", executor,
            reactToTargetStateChanges: true);
        var regen = new FiveSecondManaRegenerationProcessor("priest",
            derived.SpiritRegenerationPerSecond, derived.ManaPerFiveSeconds,
            request.ManaRegenerationWhileCastingFraction);
        var reactive = new ForeverPriestShadowReactiveProcessor("priest", auraManager,
            request.Talents, request.Tuning, baseDamageBonus);
        // Must precede AbilityExecutor so each channel tick uses its own growth
        // multiplier rather than a static bonus on every Mind Flay tick.
        var progressiveFlay = new ShadowPriestProgressiveMindFlayProcessor(
            priest, request.Talents.Rank("improved-mind-flay"));
        var engine = new SimulationEngine([progressiveFlay, executor, auraManager,
            reactive, regen, priority]);
        regen.Initialize(context);
        var run = engine.Run(context);
        var actorSummary = run.Summary.ActorSummaries["priest"];
                return new ShadowPriestSimResponse
        {
            Dps = actorSummary.DamageDone / request.DurationSeconds,
            DamageDone = actorSummary.DamageDone,
            HealingDone = actorSummary.HealingDone,
            EndingMana = priest.Resources["mana"].Current,
            MaximumMana = derived.MaximumMana,
            SpellPower = spellPower, SpellCritPercent = crit,
            BossEndingHealthPercent = boss.MaximumHealth > 0m ?
                boss.CurrentHealth * 100m / boss.MaximumHealth : 0m,
            MaximumShadowWeavingStacks = reactive.MaximumStacksReached,
            DamageByAbility = new Dictionary<string, decimal>(actorSummary.DamageDoneByAbility,
                StringComparer.OrdinalIgnoreCase),
            PriestDied = !priest.IsAlive, Run = run,
            ModelingNotes =
            [
                "CP129: spell bases/mana costs, level-63 hit and regen are explicit provisional values.",
                "Shadow Weaving modifies only the Priest's shadow damage, not raid-wide damage.",
                "Shadowform is assumed active from the pull when talented; damage, mana and critical bonus are applied.",
                "Periodic damage is crit-enabled; proc chance and internal RNG use the configured seed.",
                "Devouring Contagion cost reduction works; on-death multi-target plague spread is deferred.",
                "No trinket procs, wand auto-attacks, boss resistances or raid buffs are modeled in this first Shadow preview."
            ]
        };
    }

    private static RotationProfile BuildRotation(ShadowPriestSimRequest request, SimulationActorState priest)
    {
        var rotation = new RotationProfile
        {
            Name = "Shadow Priest — CP129 priority", ClassKey = "priest",
            RulesetKey = "wow-forever", SimulationType = SimulationType.Dps
        };
        void add(int priority, string key, params RotationConditionDefinition[] conditions)
        {
            if (!priest.Abilities.ContainsKey(key)) return;
            rotation.Entries.Add(new RotationEntry
            {
                Priority = priority, AbilityKey = key, Conditions = conditions.ToList()
            });
        }
        static RotationConditionDefinition missing(string key) => new()
        {
            ConditionType = RotationConditionTypes.TargetAuraMissing, Key = key
        };
        if (request.UseVampiricEmbrace)
            add(0, "vampiric-embrace", missing("vampiric-embrace"));
        if (request.UseShadowWordPain)
            add(1, "shadow-word-pain", missing("shadow-word-pain"));
        if (request.UseDevouringPlague)
            add(2, "devouring-plague", missing("devouring-plague"));
        if (request.UseShadowWordDeathExecute)
            add(3, "shadow-word-death", new RotationConditionDefinition
            {
                ConditionType = RotationConditionTypes.TargetHealthPercent,
                ComparisonOperator = RotationComparisonOperators.LessThanOrEqual, Value = 20m
            });
        if (request.UseMindBlast) add(4, "mind-blast");
        if (request.UseMindFlay) add(5, "mind-flay");
        return rotation;
    }

    private sealed class ShadowPriestRollResolver(decimal hit, decimal crit,
        decimal criticalMultiplier) : ICombatRollResolver
    {
        public CombatRollResult Resolve(SimulationContext context, SimulationActorState source,
            SimulationActorState target, AbilityDefinition ability, AbilityEffectDefinition effect) =>
            Resolve(context, source, target, ability, effect, CombatEffectDeliveryType.Direct);
        public CombatRollResult Resolve(SimulationContext context, SimulationActorState source,
            SimulationActorState target, AbilityDefinition ability, AbilityEffectDefinition effect,
            CombatEffectDeliveryType deliveryType)
        {
            if (effect.CanMiss && (decimal)context.Random.NextDouble() * 100m >= hit)
                return CombatRollResult.Miss();
            if (!effect.CanCrit) return CombatRollResult.Hit();
            var bonus = effect.BonusCriticalChancePercent;
            if (ability.Key == "shadow-word-death" && target.MaximumHealth > 0m &&
                target.CurrentHealth * 100m / target.MaximumHealth > 20m)
                bonus = 0m;
            if ((decimal)context.Random.NextDouble() * 100m < Math.Min(100m, crit + bonus))
                return CombatRollResult.Critical(criticalMultiplier);
            return CombatRollResult.Hit();
        }
    }

    private sealed class ShadowPriestNoMitigation : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext context,
            SimulationActorState source, SimulationActorState target,
            AbilityDefinition ability, AbilityEffectDefinition effect, decimal rawAmount) =>
            DamageMitigationResult.Unmitigated(rawAmount);
    }
}
