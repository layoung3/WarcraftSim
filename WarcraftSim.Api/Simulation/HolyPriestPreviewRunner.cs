using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Encounters;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Healing;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// First isolated tank-plus-healer encounter. Reuses CP124's shared boss
/// melee, the ordinary cast/heal/resource pipeline, and the class-independent
/// healer policy. The tank is a training actor without Warrior rotation yet.
/// </summary>
public sealed class HolyPriestPreviewRunner
{
    public HolyPriestPreviewResponse Run(
        HolyPriestPreviewRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);
        var context = new SimulationContext(new SimulationRunOptions
        {
            Seed = request.Seed,
            DurationSeconds = request.DurationSeconds,
            PrimaryActorKey = "tank",
            CaptureTimeline = request.CaptureTimeline,
            MaximumProcessedEvents = 20000,
            CancellationToken = cancellationToken
        });

        static SimulationActorState NewActor(string key, string team, decimal maxHp) => new()
        {
            Key = key, Name = key, TeamKey = team, Level = 60,
            MaximumHealth = maxHp
        };
        var boss = NewActor("boss", "enemy", 1000000m);
        var tank = NewActor("tank", "raid", request.TankMaximumHealth);
        var priest = NewActor("priest", "raid", request.HealerMaximumHealth);
        boss.InitializeHealth(boss.MaximumHealth);
        tank.InitializeHealth(tank.MaximumHealth);
        priest.InitializeHealth(priest.MaximumHealth);
        priest.AddResource(new ResourceState("mana", request.PriestMaximumMana,
            request.PriestMaximumMana, request.PriestManaRegenPerSecond));
        context.AddActor(boss);
        context.AddActor(tank);
        context.AddActor(priest);

        priest.AddAbility(CreateHeal("flash-heal", "Flash Heal",
            request.FlashHealAmount, request.FlashHealManaCost, request.FlashHealCastSeconds));
        priest.AddAbility(CreateHeal("greater-heal", "Greater Heal",
            request.GreaterHealAmount, request.GreaterHealManaCost,
            request.GreaterHealCastSeconds));

        var roll = new GuaranteedHitResolver();
        var mitigation = new NoDamageMitigation();
        var executor = new AbilityExecutor(new AuraManager(), roll, mitigation);
        var melee = new EncounterBossMeleeProcessor(new EncounterBossMeleeDefinition
        {
            BossActorKey = boss.Key,
            AbilityKey = "boss-main-hand-auto",
            PreferredTargetActorKey = tank.Key,
            EligibleTargetTeamKey = tank.TeamKey,
            TargetMode = EncounterBossMeleeTargetModes.Fixed,
            FirstSwingSeconds = 1m,
            SwingSpeedSeconds = request.BossSwingIntervalSeconds,
            MinimumDamage = request.BossSwingDamage,
            MaximumDamage = request.BossSwingDamage,
            PreserveTargetHealth = false,
            EnableBossParryHaste = false
        }, roll, mitigation);
        var healer = new HealerPriorityProcessor(new HealerPriorityDefinition
        {
            HealerActorKey = priest.Key,
            EligibleTeamKey = "raid",
            DecisionIntervalSeconds = 0.25m,
            Rules =
            [
                new HealerPriorityRule
                {
                    Priority = 0, AbilityKey = "greater-heal",
                    CastAtOrBelowHealthPercent = request.GreaterHealHealthThresholdPercent,
                    MinimumMissingHealth = 1m
                },
                new HealerPriorityRule
                {
                    Priority = 1, AbilityKey = "flash-heal",
                    CastAtOrBelowHealthPercent = request.FlashHealHealthThresholdPercent,
                    MinimumMissingHealth = 1m
                }
            ]
        }, executor);

        var processors = new List<ICombatEventProcessor> { executor, melee };
        if (request.EnableHealer) processors.Add(healer);
        var run = new SimulationEngine(processors).Run(context, ctx =>
        {
            melee.ScheduleFirst(ctx);
            if (request.EnableHealer) healer.ScheduleFirst(ctx);
        });
        priest.RefreshResources(request.DurationSeconds);

        var priestSummary = run.Summary.ActorSummaries[priest.Key];
        priestSummary.EndingResources["mana"] = priest.Resources["mana"].Current;
        var tankSummary = run.Summary.ActorSummaries[tank.Key];
        return new HolyPriestPreviewResponse
        {
            TankSurvived = tank.IsAlive,
            TankEndingHealth = tank.CurrentHealth,
            PriestEndingMana = priest.Resources["mana"].Current,
            HealingDone = priestSummary.HealingDone,
            OverhealingDone = priestSummary.OverhealingDone,
            TankDamageTaken = tankSummary.DamageTaken,
            FlashHealCasts = priestSummary.Abilities.TryGetValue("flash-heal", out var flash)
                ? flash.CastCompletedCount : 0,
            GreaterHealCasts = priestSummary.Abilities.TryGetValue("greater-heal", out var greater)
                ? greater.CastCompletedCount : 0,
            Run = run
        };
    }

    private static AbilityDefinition CreateHeal(string key, string name,
        decimal amount, decimal mana, decimal castSeconds) => new()
    {
        Key = key, Name = name, ClassKey = "priest", SpecializationKey = "holy",
        CastTimeSeconds = castSeconds, GlobalCooldownSeconds = 1.5m,
        ResourceCosts = [new AbilityResourceCost { ResourceKey = "mana", Amount = mana }],
        Effects = [new AbilityEffectDefinition
        {
            Key = key + "-heal", EffectType = AbilityEffectTypes.DirectHealing,
            TargetType = AbilityTargetTypes.Friendly, SchoolKey = "holy",
            ResolutionType = CombatResolutionTypes.Spell,
            CanMiss = false, CanCrit = false,
            MinimumValue = amount, MaximumValue = amount
        }]
    };

    private static void Validate(HolyPriestPreviewRequest r)
    {
        if (r.DurationSeconds <= 0m || r.DurationSeconds > 1800m ||
            r.TankMaximumHealth <= 0m || r.HealerMaximumHealth <= 0m ||
            r.BossSwingDamage < 0m || r.BossSwingIntervalSeconds <= 0m ||
            r.PriestMaximumMana <= 0m || r.PriestManaRegenPerSecond < 0m ||
            r.FlashHealAmount <= 0m || r.GreaterHealAmount <= 0m ||
            r.FlashHealManaCost < 0m || r.GreaterHealManaCost < 0m ||
            r.FlashHealCastSeconds <= 0m || r.GreaterHealCastSeconds <= 0m ||
            r.FlashHealHealthThresholdPercent is < 0m or > 100m ||
            r.GreaterHealHealthThresholdPercent is < 0m or > 100m)
            throw new ArgumentException("Invalid CP125 healer preview settings.");
    }

    // A deterministic control scenario deliberately isolates healing and cast
    // decisions from the uncalibrated Forever avoidance/critical formulas.
    private sealed class GuaranteedHitResolver : ICombatRollResolver
    {
        public CombatRollResult Resolve(SimulationContext context,
            SimulationActorState source, SimulationActorState target,
            AbilityDefinition ability, AbilityEffectDefinition effect) => CombatRollResult.Hit();
    }

    private sealed class NoDamageMitigation : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext context,
            SimulationActorState source, SimulationActorState target,
            AbilityDefinition ability, AbilityEffectDefinition effect,
            decimal rawAmount) => DamageMitigationResult.Unmitigated(rawAmount);
    }
}

public sealed class HolyPriestPreviewResponse
{
    public bool TankSurvived { get; init; }
    public decimal TankEndingHealth { get; init; }
    public decimal PriestEndingMana { get; init; }
    public decimal HealingDone { get; init; }
    public decimal OverhealingDone { get; init; }
    public decimal TankDamageTaken { get; init; }
    public int FlashHealCasts { get; init; }
    public int GreaterHealCasts { get; init; }
    public SimulationRunResult Run { get; init; } = new();
}
