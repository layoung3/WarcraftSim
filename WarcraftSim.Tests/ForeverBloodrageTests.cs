using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Abilities.Warrior;
using WarcraftSim.Data.Forever.Characters;

namespace WarcraftSim.Tests;

public sealed class ForeverBloodrageTests
{
    [Fact]
    public void Factory_UsesForeverClientValues()
    {
        var bloodrage = ForeverBloodrageFactory.Create(baseHealth: 500m);

        Assert.Equal(ForeverBloodrageFactory.AbilityKey, bloodrage.Key);
        Assert.Equal("Bloodrage", bloodrage.Name);
        Assert.Equal(ForeverCharacterClassKeys.Warrior, bloodrage.ClassKey);
        Assert.Equal(10, bloodrage.RequiredLevel);
        Assert.Equal(60m, bloodrage.CooldownSeconds);
        Assert.True(bloodrage.IsOffGlobalCooldown);
        Assert.Equal(0m, bloodrage.GlobalCooldownSeconds);
        Assert.Empty(bloodrage.ResourceCosts);

        Assert.Equal(3, bloodrage.Effects.Count);

        var healthCost = Assert.Single(
            bloodrage.Effects,
            effect => effect.Key == "health-cost"
        );
        Assert.Equal(AbilityEffectTypes.HealthChange, healthCost.EffectType);
        Assert.Equal(HealthChangeOperationTypes.Damage, healthCost.HealthChangeOperation);
        Assert.Equal(100m, healthCost.MinimumValue);
        Assert.Equal(100m, healthCost.MaximumValue);

        var immediate = Assert.Single(
            bloodrage.Effects,
            effect => effect.Key == "immediate-rage"
        );
        Assert.Equal(10m, immediate.MinimumValue);
        Assert.Equal("rage", immediate.ResourceKey);

        var periodic = Assert.Single(
            bloodrage.Effects,
            effect => effect.Key == "periodic-rage"
        );
        Assert.Equal(AbilityEffectTypes.PeriodicResourceChange, periodic.EffectType);
        Assert.Equal(1m, periodic.MinimumValue);
        Assert.Equal(10m, periodic.DurationSeconds);
        Assert.Equal(1m, periodic.TickIntervalSeconds);
        Assert.True(periodic.IncludeExpirationBoundaryTick);
    }

    [Fact]
    public void Activation_SpendsTwentyPercentBaseHealthAndGrantsImmediateRage()
    {
        var warrior = CreateWarrior(startingRage: 0m);
        warrior.AddAbility(ForeverBloodrageFactory.Create(baseHealth: 500m));

        var result = RunBloodrage(warrior, durationSeconds: 0.1m);

        Assert.Equal(900m, warrior.CurrentHealth);
        Assert.Equal(10m, warrior.Resources["rage"].Current);

        var healthEvent = Assert.Single(
            result.Timeline,
            e => e.Type == CombatEventType.HealthChanged
        );
        Assert.Equal(-100m, healthEvent.Amount);
        Assert.DoesNotContain(
            result.Timeline,
            e => e.Type == CombatEventType.Damage &&
                 e.AbilityKey == ForeverBloodrageFactory.AbilityKey
        );
    }

    [Fact]
    public void FullDuration_GrantsTenImmediateAndTenPeriodicRage()
    {
        var warrior = CreateWarrior(startingRage: 0m);
        warrior.AddAbility(ForeverBloodrageFactory.Create(baseHealth: 500m));

        var result = RunBloodrage(warrior, durationSeconds: 10.1m);

        Assert.Equal(20m, warrior.Resources["rage"].Current);

        var periodicGains = result.Timeline
            .Where(e =>
                e.Type == CombatEventType.ResourceChanged &&
                e.AbilityKey == ForeverBloodrageFactory.AbilityKey &&
                e.EffectKey == "periodic-rage")
            .ToList();

        Assert.Equal(10, periodicGains.Count);
        Assert.All(periodicGains, e => Assert.Equal(1m, e.Amount));
    }

    [Fact]
    public void ImprovedBloodrageMultiplier_AppliesToImmediateAndPeriodicRage()
    {
        var warrior = CreateWarrior(startingRage: 0m);
        warrior.AddAbility(
            ForeverBloodrageFactory.Create(
                baseHealth: 500m,
                rageGenerationMultiplier: 1.5m
            )
        );

        RunBloodrage(warrior, durationSeconds: 10.1m);

        Assert.Equal(30m, warrior.Resources["rage"].Current);
    }

    [Fact]
    public void RageGeneration_IsClampedToResourceMaximumAndReportsActualGain()
    {
        var warrior = CreateWarrior(startingRage: 95m);
        warrior.AddAbility(ForeverBloodrageFactory.Create(baseHealth: 500m));

        var result = RunBloodrage(warrior, durationSeconds: 10.1m);

        Assert.Equal(100m, warrior.Resources["rage"].Current);

        var totalReportedGain = result.Timeline
            .Where(e =>
                e.Type == CombatEventType.ResourceChanged &&
                e.AbilityKey == ForeverBloodrageFactory.AbilityKey)
            .Sum(e => e.Amount ?? 0m);

        Assert.Equal(5m, totalReportedGain);
    }

    [Fact]
    public void InvalidFactoryInputs_FailExplicitly()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverBloodrageFactory.Create(0m));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverBloodrageFactory.Create(500m, -0.1m));
    }

    private static SimulationRunResult RunBloodrage(
        SimulationActorState warrior,
        decimal durationSeconds)
    {
        var context = new SimulationContext(new SimulationRunOptions
        {
            DurationSeconds = durationSeconds,
            CaptureTimeline = true,
            PrimaryActorKey = warrior.Key
        });
        context.AddActor(warrior);

        var executor = new AbilityExecutor(
            new AuraManager(),
            new SimpleCombatRollResolver(),
            new RulesetDamageMitigationResolver(new CombatRulesetDefinition
            {
                RulesetKey = "bloodrage-tests",
                Version = "1"
            })
        );

        return new SimulationEngine([executor]).Run(
            context,
            started =>
            {
                var use = executor.TryStartAbility(
                    started,
                    warrior.Key,
                    warrior.Key,
                    ForeverBloodrageFactory.AbilityKey
                );
                Assert.True(use.Success, use.FailureReason ?? "Ability use failed.");
            }
        );
    }

    private static SimulationActorState CreateWarrior(decimal startingRage)
    {
        var warrior = new SimulationActorState
        {
            Key = "warrior",
            Name = "Warrior",
            Level = 30
        };
        warrior.InitializeHealth(1000m);
        warrior.AddResource(new ResourceState("rage", 100m, startingRage));
        return warrior;
    }
}
