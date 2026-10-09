using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Abilities.Warrior;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Tests;

public sealed class ForeverDeepWoundsTests
{
    [Theory]
    [InlineData(1, 20)]
    [InlineData(2, 40)]
    [InlineData(3, 60)]
    public void EachRank_UsesBaseWeaponAverageAndDealsFullTwelveSecondPool(
        int rank, int expectedDamage)
    {
        var source = CreateWarrior("warrior", rank);
        var target = CreateTarget("target");
        var (result, _) = Run(source, [target], 12.1m,
            started => EmitCritical(started, source.Key, target.Key));

        var ticks = DeepWoundsTicks(result);
        Assert.Equal(6, ticks.Count);
        Assert.Equal((decimal)expectedDamage, ticks.Sum(tick => tick.RawAmount ?? 0m));
        Assert.Equal(new[] { 2m, 4m, 6m, 8m, 10m, 12m },
            ticks.Select(tick => tick.TimeSeconds).ToArray());
        Assert.All(ticks, tick =>
        {
            Assert.True(tick.IsPeriodic);
            Assert.False(tick.IsCritical);
        });
    }

    [Fact]
    public void SnapshotIgnoresAttackPower_AndSupportsDistinctOffHandWeaponDamage()
    {
        var source = CreateWarrior("warrior", 3, offHandAverageDamage: 50m);
        source.Stats.Set(ForeverCombatStatKeys.AttackPower, 10000m);
        var main = CreateTarget("main");
        var off = CreateTarget("off");

        var (result, _) = Run(source, [main, off], 12.1m, started =>
        {
            EmitCritical(started, source.Key, main.Key, WeaponHandKeys.MainHand);
            EmitCritical(started, source.Key, off.Key, WeaponHandKeys.OffHand,
                ForeverCombatResolutionTypes.PlayerDualWieldMeleeAuto);
        });

        Assert.Equal(60m, DeepWoundsTicks(result)
            .Where(tick => tick.TargetActorKey == main.Key)
            .Sum(tick => tick.RawAmount ?? 0m));
        Assert.Equal(30m, DeepWoundsTicks(result)
            .Where(tick => tick.TargetActorKey == off.Key)
            .Sum(tick => tick.RawAmount ?? 0m));
    }

    [Fact]
    public void Reapplication_CarriesUndeliveredDamageAndInvalidatesOldTickSchedule()
    {
        var source = CreateWarrior("warrior", 3);
        var target = CreateTarget("target");

        var (result, _) = Run(source, [target], 16m, started =>
        {
            EmitCritical(started, source.Key, target.Key);
            ScheduleCritical(started, source.Key, target.Key, 3m);
        });

        var ticks = DeepWoundsTicks(result);
        Assert.Equal(new[] { 2m, 5m, 7m, 9m, 11m, 13m, 15m },
            ticks.Select(tick => tick.TimeSeconds).ToArray());
        // First tick was 10. Remaining 50 rolls into the next 60,
        // giving 110 spread across the six refreshed ticks.
        Assert.Equal(10m, ticks[0].RawAmount!.Value);
        Assert.Equal(120m, ticks.Sum(tick => tick.RawAmount ?? 0m));
        Assert.Single(result.Timeline, combatEvent =>
            combatEvent.Type == CombatEventType.AuraRemoved &&
            combatEvent.AbilityKey == ForeverDeepWoundsFactory.AbilityKey);
    }

    [Fact]
    public void FractionalDamagePool_ConservesDamageAcrossRollingRefresh()
    {
        var source = new SimulationActorState
        {
            Key = "warrior", Name = "warrior", TeamKey = "raid", Level = 60
        };
        source.InitializeHealth(1000000m);
        source.AddCriticalStrikeRollingDamageProc(
            ForeverDeepWoundsFactory.Create(1, 101m, 101m));
        var target = CreateTarget("target");

        var (result, _) = Run(source, [target], 16m, started =>
        {
            EmitCritical(started, source.Key, target.Key);
            ScheduleCritical(started, source.Key, target.Key, 3m);
        });

        var ticks = DeepWoundsTicks(result);
        Assert.Equal(7, ticks.Count);
        // Two 20.2-damage procs, including the fraction carried on refresh.
        Assert.Equal(40.4m, ticks.Sum(tick => tick.RawAmount ?? 0m));
    }

    [Fact]
    public void TargetsAndSources_HaveIndependentRollingPools()
    {
        var first = CreateWarrior("warrior-1", 3);
        var second = CreateWarrior("warrior-2", 1);
        var targetOne = CreateTarget("target-1");
        var targetTwo = CreateTarget("target-2");
        var (result, _) = Run(first, [targetOne, targetTwo, second], 12.1m, started =>
        {
            EmitCritical(started, first.Key, targetOne.Key);
            EmitCritical(started, first.Key, targetTwo.Key);
            EmitCritical(started, second.Key, targetOne.Key);
        });

        Assert.Equal(60m, DeepWoundsTicks(result)
            .Where(e => e.SourceActorKey == first.Key && e.TargetActorKey == targetOne.Key)
            .Sum(e => e.RawAmount ?? 0m));
        Assert.Equal(60m, DeepWoundsTicks(result)
            .Where(e => e.SourceActorKey == first.Key && e.TargetActorKey == targetTwo.Key)
            .Sum(e => e.RawAmount ?? 0m));
        Assert.Equal(20m, DeepWoundsTicks(result)
            .Where(e => e.SourceActorKey == second.Key && e.TargetActorKey == targetOne.Key)
            .Sum(e => e.RawAmount ?? 0m));
    }

    [Theory]
    [InlineData(false, "forever-player-melee-special", "main-hand", false)]
    [InlineData(true, "forever-player-spell", "main-hand", false)]
    [InlineData(true, "forever-player-melee-special", "unknown", false)]
    [InlineData(true, "forever-player-melee-special", "main-hand", true)]
    public void OnlyEligibleDirectWeaponCriticalStrikesTrigger(
        bool isCritical, string resolution, string hand, bool isPeriodic)
    {
        var source = CreateWarrior("warrior", 3);
        var target = CreateTarget("target");
        var (result, _) = Run(source, [target], 12.1m, started =>
            EmitCritical(started, source.Key, target.Key, hand, resolution,
                isCritical, isPeriodic));
        Assert.Empty(DeepWoundsTicks(result));
    }

    [Fact]
    public void PassiveAbility_CannotBeCastDirectly()
    {
        var source = CreateWarrior("warrior", 3);
        var target = CreateTarget("target");
        var context = CreateContext(source, [target], 3m);
        var executor = CreateExecutor(new AuraManager());

        var result = executor.TryStartAbility(context, source.Key, target.Key,
            ForeverDeepWoundsFactory.AbilityKey);
        Assert.False(result.Success);
        Assert.Contains("Passive", result.FailureReason ?? "");
    }

    [Fact]
    public void MissingCriticalProcProcessor_IsRejectedAtSimulationValidation()
    {
        var source = CreateWarrior("warrior", 3);
        var target = CreateTarget("target");
        var auraManager = new AuraManager();
        var context = CreateContext(source, [target], 3m);

        var error = Assert.Throws<WarcraftSim.Core.Simulation.Validation.SimulationDefinitionValidationException>(
            () => new SimulationEngine([auraManager, CreateExecutor(auraManager)])
                .Run(context));
        Assert.Contains("CriticalStrikeRollingDamageProcessor", error.Message);
    }

    [Fact]
    public void NoTalentProc_NoDeepWoundsEvenWhenAttackIsCritical()
    {
        var source = new SimulationActorState
        {
            Key = "warrior", Name = "warrior", TeamKey = "raid", Level = 60
        };
        source.InitializeHealth(1000000m);
        var target = CreateTarget("target");
        var (result, _) = Run(source, [target], 12.1m,
            started => EmitCritical(started, source.Key, target.Key));
        Assert.Empty(DeepWoundsTicks(result));
    }

    [Fact]
    public void AutoAttackCritical_TriggersProcThroughRealDamageEvent()
    {
        var source = CreateWarrior("warrior", 3);
        var target = CreateTarget("target");
        var auraManager = new AuraManager();
        var executor = new AbilityExecutor(auraManager,
            new SimpleCombatRollResolver(criticalChancePercent: 100m,
                useSingleRollTable: true), new UnmitigatedDamageResolver());
        var auto = new AutoAttackProcessor(executor);
        var proc = new CriticalStrikeRollingDamageProcessor(auraManager);
        var context = CreateContext(source, [target], 2.2m);

        var definition = ForeverWarriorAutoAttackFactory.CreateTwoHandedMainHand(
            "main-hand-auto", "Main-Hand Auto", 100m, 100m, 3m, "weapon-skill");
        var result = new SimulationEngine([auraManager, executor, auto, proc]).Run(
            context, started => Assert.True(auto.Start(
                started, source.Key, target.Key, definition,
                firstSwingDelaySeconds: 0.1m)));

        Assert.Contains(result.Timeline, combatEvent =>
            combatEvent.Type == CombatEventType.Damage &&
            combatEvent.AbilityKey == "main-hand-auto" &&
            combatEvent.IsCritical &&
            combatEvent.WeaponHandKey == WeaponHandKeys.MainHand);
        var tick = Assert.Single(DeepWoundsTicks(result));
        Assert.Equal(2.1m, tick.TimeSeconds);
        Assert.Equal(10m, tick.RawAmount!.Value);
    }

    [Fact]
    public void InvalidTalentRankAndIncompleteOffHandRangesAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverDeepWoundsFactory.Create(0, 100m, 100m));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverDeepWoundsFactory.Create(4, 100m, 100m));
        Assert.Throws<ArgumentException>(() =>
            ForeverDeepWoundsFactory.Create(3, 100m, 100m, 50m));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverDeepWoundsFactory.Create(3, 100m, 100m,
                tickIntervalSeconds: 5m));
    }

    private static SimulationActorState CreateWarrior(
        string key, int rank, decimal? offHandAverageDamage = null)
    {
        var actor = new SimulationActorState
        {
            Key = key, Name = key, TeamKey = "raid", Level = 60
        };
        actor.InitializeHealth(1000000m);
        actor.AddResource(new ResourceState("rage", 100m, 0m));
        actor.AddCriticalStrikeRollingDamageProc(
            ForeverDeepWoundsFactory.Create(
                rank, 100m, 100m, offHandAverageDamage, offHandAverageDamage));
        return actor;
    }

    private static SimulationActorState CreateTarget(string key)
    {
        var actor = new SimulationActorState
        {
            Key = key, Name = key, TeamKey = "enemy", Level = 60
        };
        actor.InitializeHealth(1000000m);
        return actor;
    }

    private static SimulationContext CreateContext(
        SimulationActorState source,
        IEnumerable<SimulationActorState> otherActors,
        decimal duration)
    {
        var context = new SimulationContext(new SimulationRunOptions
        {
            DurationSeconds = duration,
            CaptureTimeline = true,
            PrimaryActorKey = source.Key,
            Seed = 12
        });
        context.AddActor(source);
        foreach (var other in otherActors)
            context.AddActor(other);
        return context;
    }

    private static (SimulationRunResult Result, SimulationContext Context) Run(
        SimulationActorState source,
        IEnumerable<SimulationActorState> otherActors,
        decimal duration,
        Action<SimulationContext> onStart)
    {
        var auraManager = new AuraManager();
        var executor = CreateExecutor(auraManager);
        var proc = new CriticalStrikeRollingDamageProcessor(auraManager);
        var context = CreateContext(source, otherActors, duration);
        var result = new SimulationEngine([auraManager, executor, proc])
            .Run(context, onStart);
        return (result, context);
    }

    private static AbilityExecutor CreateExecutor(AuraManager auraManager) =>
        new(auraManager, new SimpleCombatRollResolver(),
            new UnmitigatedDamageResolver());

    private static List<CombatEvent> DeepWoundsTicks(SimulationRunResult result) =>
        result.Timeline.Where(e =>
            e.Type == CombatEventType.Damage &&
            e.AbilityKey == ForeverDeepWoundsFactory.AbilityKey)
            .ToList();

    private static void EmitCritical(
        SimulationContext context, string source, string target,
        string hand = WeaponHandKeys.MainHand,
        string resolution = ForeverCombatResolutionTypes.PlayerMeleeSpecial,
        bool isCritical = true, bool isPeriodic = false)
    {
        context.EmitEvent(CreateCriticalEvent(context.CurrentTimeSeconds,
            source, target, hand, resolution, isCritical, isPeriodic));
    }

    private static void ScheduleCritical(
        SimulationContext context, string source, string target, decimal when)
    {
        context.ScheduleEvent(CreateCriticalEvent(when, source, target,
            WeaponHandKeys.MainHand,
            ForeverCombatResolutionTypes.PlayerMeleeSpecial, true, false));
    }

    private static CombatEvent CreateCriticalEvent(
        decimal when, string source, string target,
        string hand, string resolution, bool isCritical, bool isPeriodic) =>
        new()
        {
            Type = CombatEventType.Damage,
            TimeSeconds = when,
            SourceActorKey = source,
            TargetActorKey = target,
            AbilityKey = "critical-strike-test",
            EffectKey = "damage",
            IsCritical = isCritical,
            WeaponHandKey = hand,
            ResolutionType = resolution,
            Amount = 100m,
            EffectDeliveryType = isPeriodic
                ? CombatEffectDeliveryType.Periodic
                : CombatEffectDeliveryType.Direct
        };

    private sealed class UnmitigatedDamageResolver : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(
            SimulationContext context, SimulationActorState source,
            SimulationActorState target, AbilityDefinition ability,
            AbilityEffectDefinition effect, decimal rawAmount) =>
            DamageMitigationResult.Unmitigated(rawAmount);
    }
}
