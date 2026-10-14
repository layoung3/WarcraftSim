using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Healing;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests;

/// <summary>CP125 deterministic real-health healer integration checks.</summary>
public sealed class HealingHolyPriestCp125Tests
{
    private static HolyPriestPreviewRequest Scenario() => new()
    {
        DurationSeconds = 12m, TankMaximumHealth = 600m,
        BossSwingDamage = 300m, BossSwingIntervalSeconds = 2m,
        FlashHealAmount = 400m, FlashHealManaCost = 100m,
        FlashHealCastSeconds = 1.5m,
        GreaterHealHealthThresholdPercent = 0m,
        FlashHealHealthThresholdPercent = 90m,
        PriestMaximumMana = 5000m, PriestManaRegenPerSecond = 0m,
        CaptureTimeline = true
    };

    [Fact]
    public void HealerPreventsDeathThatOccursWithoutHealing()
    {
        var request = Scenario();
        var withHeals = new HolyPriestPreviewRunner().Run(request);
        request.EnableHealer = false;
        var withoutHeals = new HolyPriestPreviewRunner().Run(request);
        Assert.True(withHeals.TankSurvived);
        Assert.False(withoutHeals.TankSurvived);
        Assert.True(withHeals.HealingDone > 0m);
        Assert.Equal(0m, withoutHeals.HealingDone);
    }

    [Fact]
    public void HealLandsAfterItsCastTimeNotAtCastStart()
    {
        var result = new HolyPriestPreviewRunner().Run(Scenario());
        Assert.Contains(result.Run.Timeline, ev =>
            ev.Type == CombatEventType.AbilityCastStarted &&
            ev.AbilityKey == "flash-heal" && ev.TimeSeconds == 1m);
        Assert.Contains(result.Run.Timeline, ev =>
            ev.Type == CombatEventType.Healing &&
            ev.AbilityKey == "flash-heal" && ev.TimeSeconds == 2.5m);
        Assert.DoesNotContain(result.Run.Timeline, ev =>
            ev.Type == CombatEventType.Healing && ev.TimeSeconds < 2.5m);
    }

    [Fact]
    public void AFullHealthActorIsNeverHealedBeforeDamage()
    {
        var result = new HolyPriestPreviewRunner().Run(Scenario());
        Assert.DoesNotContain(result.Run.Timeline, ev =>
            ev.Type == CombatEventType.AbilityCastStarted &&
            ev.SourceActorKey == "priest" && ev.TimeSeconds < 1m);
    }

    [Fact]
    public void ManaIsSpentOnCompletedHealsAndCannotBecomeNegative()
    {
        var request = Scenario();
        request.DurationSeconds = 4m;
        request.PriestMaximumMana = 1000m;
        var result = new HolyPriestPreviewRunner().Run(request);
        Assert.Equal(1, result.FlashHealCasts);
        Assert.Equal(900m, result.PriestEndingMana);
        Assert.Equal(result.PriestEndingMana,
            result.Run.Summary.ActorSummaries["priest"].EndingResources["mana"]);
        Assert.All(result.Run.Timeline.Where(ev =>
            ev.Type == CombatEventType.ResourceChanged && ev.SourceActorKey == "priest"),
            ev => Assert.True(ev.ResourceTotalAfter >= 0m));
    }

    [Fact]
    public void NoManaMeansThePolicyCannotHeal()
    {
        var request = Scenario();
        request.PriestMaximumMana = 50m;
        var result = new HolyPriestPreviewRunner().Run(request);
        Assert.False(result.TankSurvived);
        Assert.Equal(0m, result.HealingDone);
        Assert.Equal(0, result.FlashHealCasts);
    }

    [Fact]
    public void AnUnfinishedCastCannotResurrectAnAlreadyDeadTank()
    {
        var request = Scenario();
        request.FlashHealCastSeconds = 5m;
        var result = new HolyPriestPreviewRunner().Run(request);
        Assert.False(result.TankSurvived);
        Assert.Equal(0m, result.HealingDone);
        Assert.DoesNotContain(result.Run.Timeline, ev =>
            ev.Type == CombatEventType.Healing && ev.TargetActorKey == "tank");
    }

    [Fact]
    public void HealingAndOverhealingMetricsAreAttributedToPriest()
    {
        var request = Scenario();
        request.FlashHealAmount = 800m;
        var result = new HolyPriestPreviewRunner().Run(request);
        var priest = result.Run.Summary.ActorSummaries["priest"];
        var tank = result.Run.Summary.ActorSummaries["tank"];
        Assert.True(result.HealingDone > 0m);
        Assert.True(result.OverhealingDone > 0m);
        Assert.Equal(priest.HealingDone, tank.HealingReceived);
        Assert.Equal(priest.OverhealingDone, tank.OverhealingReceived);
    }

    [Fact]
    public void ASevereInjuryUsesTheHigherPriorityGreaterHeal()
    {
        var request = Scenario();
        request.TankMaximumHealth = 1000m;
        request.BossSwingDamage = 650m;
        request.GreaterHealHealthThresholdPercent = 50m;
        request.GreaterHealCastSeconds = 1.5m;
        request.GreaterHealAmount = 750m;
        var result = new HolyPriestPreviewRunner().Run(request);
        Assert.Contains(result.Run.Timeline, ev =>
            ev.Type == CombatEventType.AbilityCastStarted &&
            ev.AbilityKey == "greater-heal" && ev.TimeSeconds == 1m);
    }

    [Fact]
    public void GenericHealerPriorityTargetsLowestHealthAllyRegardlessOfClass()
    {
        var context = new SimulationContext(new SimulationRunOptions
        {
            Seed = 41, DurationSeconds = 1m,
            PrimaryActorKey = "healer", CaptureTimeline = true
        });
        SimulationActorState Make(string key, decimal current)
        {
            var actor = new SimulationActorState
            {
                Key = key, Name = key, TeamKey = "raid", Level = 60
            };
            actor.InitializeHealth(1000m, current);
            context.AddActor(actor);
            return actor;
        }
        var healer = Make("healer", 1000m);
        Make("paladin-tank", 400m);
        Make("druid-tank", 200m);
        healer.AddAbility(new AbilityDefinition
        {
            Key = "generic-heal", Name = "Generic Heal", CastTimeSeconds = 0m,
            Effects = [new AbilityEffectDefinition
            {
                Key = "direct-heal", EffectType = AbilityEffectTypes.DirectHealing,
                TargetType = AbilityTargetTypes.Friendly, CanCrit = false,
                MinimumValue = 100m, MaximumValue = 100m
            }]
        });
        var roll = new TestHitResolver();
        var executor = new AbilityExecutor(new AuraManager(), roll, new TestMitigation());
        var policy = new HealerPriorityProcessor(new HealerPriorityDefinition
        {
            HealerActorKey = "healer", EligibleTeamKey = "raid",
            Rules = [new HealerPriorityRule
            {
                AbilityKey = "generic-heal", CastAtOrBelowHealthPercent = 90m
            }]
        }, executor);
        var result = new SimulationEngine([executor, policy]).Run(context,
            ctx => policy.ScheduleFirst(ctx));
        Assert.Equal(100m, result.Summary.ActorSummaries["druid-tank"].HealingReceived);
        Assert.Equal(0m, result.Summary.ActorSummaries["paladin-tank"].HealingReceived);
    }

    private sealed class TestHitResolver : ICombatRollResolver
    {
        public CombatRollResult Resolve(SimulationContext context,
            SimulationActorState source, SimulationActorState target,
            AbilityDefinition ability, AbilityEffectDefinition effect) => CombatRollResult.Hit();
    }

    private sealed class TestMitigation : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext context,
            SimulationActorState source, SimulationActorState target,
            AbilityDefinition ability, AbilityEffectDefinition effect, decimal rawAmount) =>
            DamageMitigationResult.Unmitigated(rawAmount);
    }

    [Fact]
    public void InvalidTimingIsRejectedBeforeRunning()
    {
        var request = Scenario();
        request.BossSwingIntervalSeconds = 0m;
        Assert.Throws<ArgumentException>(() => new HolyPriestPreviewRunner().Run(request));
    }
}
