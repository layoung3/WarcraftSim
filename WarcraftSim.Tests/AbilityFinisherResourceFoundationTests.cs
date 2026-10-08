using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Validation;

namespace WarcraftSim.Tests;

public sealed class AbilityFinisherResourceFoundationTests
{
    [Fact]
    public void TargetAboveMaximumHealthPercent_CannotStartAbility()
    {
        var source = CreateActor("source", 100m, 30m);
        var target = CreateActor("target", 100m, 0m, startingHealth: 21m);
        source.AddAbility(CreateFinisher());

        var context = CreateContext(source, target);
        var executor = CreateExecutor();

        var result = executor.TryStartAbility(context, source.Key, target.Key, "finisher");

        Assert.False(result.Success);
        Assert.Contains("20", result.FailureReason ?? "");
        Assert.Equal(30m, source.Resources["rage"].Current);
    }

    [Fact]
    public void TargetExactlyAtMaximumHealthPercent_CanStartAbility()
    {
        var source = CreateActor("source", 100m, 15m);
        var target = CreateActor("target", 100m, 0m, startingHealth: 20m);
        source.AddAbility(CreateFinisher());

        var context = CreateContext(source, target);
        var executor = CreateExecutor();

        var result = executor.TryStartAbility(context, source.Key, target.Key, "finisher");

        Assert.True(result.Success);
    }

    [Fact]
    public void AdditionalResource_IsConsumedAfterBaseCostAndScalesDamage()
    {
        var source = CreateActor("source", 100m, 30m);
        var target = CreateActor("target", 1000m, 0m, startingHealth: 200m);
        source.AddAbility(CreateFinisher());

        var result = RunAbility(source, target, "finisher", durationSeconds: 0.1m);

        Assert.Equal(0m, source.Resources["rage"].Current);

        var damage = Assert.Single(
            result.Timeline,
            e => e.Type == CombatEventType.Damage && e.AbilityKey == "finisher"
        );

        Assert.Equal(130m, damage.RawAmount);
        Assert.Equal(130m, damage.Amount);

        var resourceEvents = result.Timeline
            .Where(e => e.Type == CombatEventType.ResourceChanged && e.AbilityKey == "finisher")
            .ToList();

        Assert.Equal(2, resourceEvents.Count);
        Assert.Equal(-15m, resourceEvents[0].Amount);
        Assert.Equal(-15m, resourceEvents[1].Amount);
    }

    [Fact]
    public void AdditionalResourceMaximum_CapsConsumptionAndScaling()
    {
        var ability = CreateFinisher();
        ability.AdditionalResourceConsumptions[0].MaximumAmount = 5m;

        var source = CreateActor("source", 100m, 40m);
        var target = CreateActor("target", 1000m, 0m, startingHealth: 200m);
        source.AddAbility(ability);

        var result = RunAbility(source, target, "finisher", durationSeconds: 0.1m);

        Assert.Equal(20m, source.Resources["rage"].Current);
        var damage = Assert.Single(result.Timeline, e => e.Type == CombatEventType.Damage);
        Assert.Equal(110m, damage.RawAmount);
    }

    [Fact]
    public void MissingAdditionalResource_FailsBeforeActionStarts()
    {
        var source = CreateActorWithoutRage("source", 100m);
        var target = CreateActor("target", 100m, 0m, startingHealth: 20m);
        var ability = CreateFinisher();
        ability.ResourceCosts.Clear();
        source.AddAbility(ability);

        var context = CreateContext(source, target);
        var executor = CreateExecutor();

        var result = executor.TryStartAbility(context, source.Key, target.Key, "finisher");

        Assert.False(result.Success);
        Assert.Contains("Additional resource 'rage'", result.FailureReason ?? "");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void InvalidTargetHealthThreshold_FailsValidation(decimal threshold)
    {
        var source = CreateActor("source", 100m, 30m);
        source.AddAbility(new AbilityDefinition
        {
            Key = "invalid-threshold",
            Name = "Invalid Threshold",
            MaximumTargetHealthPercent = threshold
        });

        var context = new SimulationContext(new SimulationRunOptions { DurationSeconds = 0.1m });
        context.AddActor(source);

        var exception = Assert.Throws<SimulationDefinitionValidationException>(
            () => new SimulationEngine().Run(context)
        );

        Assert.Contains(exception.Errors, error =>
            error.Contains("maximum target health percent", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ConsumedResourceScalingWithoutMatchingConsumption_FailsValidation()
    {
        var source = CreateActor("source", 100m, 30m);
        source.AddAbility(new AbilityDefinition
        {
            Key = "invalid-scaling",
            Name = "Invalid Scaling",
            Effects =
            [
                new AbilityEffectDefinition
                {
                    Key = "damage",
                    EffectType = AbilityEffectTypes.DirectDamage,
                    TargetType = AbilityTargetTypes.Enemy,
                    ResolutionType = CombatResolutionTypes.AlwaysHits,
                    CanMiss = false,
                    CanCrit = false,
                    MinimumValue = 1m,
                    MaximumValue = 1m,
                    ConsumedResourceScalingKey = "rage",
                    ConsumedResourceScalingCoefficient = 1m
                }
            ]
        });

        var context = new SimulationContext(new SimulationRunOptions { DurationSeconds = 0.1m });
        context.AddActor(source);

        var exception = Assert.Throws<SimulationDefinitionValidationException>(
            () => new SimulationEngine().Run(context)
        );

        Assert.Contains(exception.Errors, error =>
            error.Contains("does not consume that additional resource", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void InvalidAdditionalResourceConsumption_FailsValidation()
    {
        var source = CreateActor("source", 100m, 30m);
        source.AddAbility(new AbilityDefinition
        {
            Key = "invalid-consumption",
            Name = "Invalid Consumption",
            AdditionalResourceConsumptions =
            [
                new AbilityAdditionalResourceConsumption { ResourceKey = "rage" },
                new AbilityAdditionalResourceConsumption { ResourceKey = "rage", MaximumAmount = -1m }
            ]
        });

        var context = new SimulationContext(new SimulationRunOptions { DurationSeconds = 0.1m });
        context.AddActor(source);

        var exception = Assert.Throws<SimulationDefinitionValidationException>(
            () => new SimulationEngine().Run(context)
        );

        Assert.Contains(exception.Errors, error =>
            error.Contains("duplicate additional resource", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(exception.Errors, error =>
            error.Contains("negative maximum amount", StringComparison.OrdinalIgnoreCase));
    }

    private static AbilityDefinition CreateFinisher()
    {
        return new AbilityDefinition
        {
            Key = "finisher",
            Name = "Finisher",
            MaximumTargetHealthPercent = 20m,
            ResourceCosts =
            [
                new AbilityResourceCost { ResourceKey = "rage", Amount = 15m }
            ],
            AdditionalResourceConsumptions =
            [
                new AbilityAdditionalResourceConsumption { ResourceKey = "rage" }
            ],
            Effects =
            [
                new AbilityEffectDefinition
                {
                    Key = "damage",
                    EffectType = AbilityEffectTypes.DirectDamage,
                    TargetType = AbilityTargetTypes.Enemy,
                    ResolutionType = CombatResolutionTypes.AlwaysHits,
                    CanMiss = false,
                    CanCrit = false,
                    MinimumValue = 100m,
                    MaximumValue = 100m,
                    ConsumedResourceScalingKey = "rage",
                    ConsumedResourceScalingCoefficient = 2m,
                    MitigationType = DamageMitigationTypes.None
                }
            ]
        };
    }

    private static SimulationActorState CreateActor(
        string key,
        decimal maximumHealth,
        decimal rage,
        decimal? startingHealth = null)
    {
        var actor = CreateActorWithoutRage(key, maximumHealth, startingHealth);
        actor.AddResource(new ResourceState("rage", 100m, rage));
        return actor;
    }

    private static SimulationActorState CreateActorWithoutRage(
        string key,
        decimal maximumHealth,
        decimal? startingHealth = null)
    {
        var actor = new SimulationActorState
        {
            Key = key,
            Name = key,
            TeamKey = key,
            Level = 30
        };
        actor.InitializeHealth(maximumHealth, startingHealth);
        return actor;
    }

    private static SimulationContext CreateContext(
        SimulationActorState source,
        SimulationActorState target)
    {
        var context = new SimulationContext(new SimulationRunOptions
        {
            DurationSeconds = 0.1m,
            CaptureTimeline = true,
            PrimaryActorKey = source.Key
        });
        context.AddActor(source);
        context.AddActor(target);
        return context;
    }

    private static SimulationRunResult RunAbility(
        SimulationActorState source,
        SimulationActorState target,
        string abilityKey,
        decimal durationSeconds)
    {
        var context = new SimulationContext(new SimulationRunOptions
        {
            DurationSeconds = durationSeconds,
            CaptureTimeline = true,
            PrimaryActorKey = source.Key
        });
        context.AddActor(source);
        context.AddActor(target);

        var executor = CreateExecutor();
        return new SimulationEngine([executor]).Run(
            context,
            started =>
            {
                var use = executor.TryStartAbility(started, source.Key, target.Key, abilityKey);
                Assert.True(use.Success, use.FailureReason ?? "Ability use failed.");
            }
        );
    }

    private static AbilityExecutor CreateExecutor()
    {
        return new AbilityExecutor(
            new AuraManager(),
            new SimpleCombatRollResolver(),
            new RulesetDamageMitigationResolver(new CombatRulesetDefinition
            {
                RulesetKey = "finisher-tests",
                Version = "1"
            })
        );
    }
}
