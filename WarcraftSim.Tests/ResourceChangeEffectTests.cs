using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Validation;

namespace WarcraftSim.Tests;

public sealed class ResourceChangeEffectTests
{
    [Fact]
    public void ResourceChange_GainAddsResourceAndReportsActualDelta()
    {
        var source = CreateActor("source", "raid");
        var target = CreateActor("target", "raid", "mana", 100m, 80m);

        source.AddAbility(
            CreateResourceAbility(
                "restore-mana",
                "mana",
                ResourceChangeOperationTypes.Gain,
                50m,
                AbilityTargetTypes.Friendly
            )
        );

        var result = RunAbility(source, target);

        Assert.Equal(100m, target.Resources["mana"].Current);

        var resourceEvent =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.ResourceChanged
            );

        Assert.Equal(20m, resourceEvent.Amount);
    }

    [Fact]
    public void ResourceChange_SpendDrainsOnlyAvailableResource()
    {
        var source = CreateActor("source", "raid");
        var target = CreateActor("target", "enemy", "mana", 100m, 30m);

        source.AddAbility(
            CreateResourceAbility(
                "mana-drain",
                "mana",
                ResourceChangeOperationTypes.Spend,
                50m,
                AbilityTargetTypes.Enemy
            )
        );

        var result = RunAbility(source, target);

        Assert.Equal(0m, target.Resources["mana"].Current);

        var resourceEvent =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.ResourceChanged
            );

        Assert.Equal(-30m, resourceEvent.Amount);
    }

    [Fact]
    public void ResourceChange_SetAssignsAbsoluteCurrentValue()
    {
        var source = CreateActor("source", "raid", "rage", 100m, 10m);

        source.AddAbility(
            CreateResourceAbility(
                "set-rage",
                "rage",
                ResourceChangeOperationTypes.Set,
                60m,
                AbilityTargetTypes.Self
            )
        );

        var result = RunAbility(source, source);

        Assert.Equal(60m, source.Resources["rage"].Current);

        var resourceEvent =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.ResourceChanged
            );

        Assert.Equal(50m, resourceEvent.Amount);
    }

    [Fact]
    public void ResourceChange_CanUsePercentOfMaximum()
    {
        var source = CreateActor("source", "raid");
        var target = CreateActor("target", "raid", "mana", 200m, 50m);

        source.AddAbility(
            CreateResourceAbility(
                "percent-restore",
                "mana",
                ResourceChangeOperationTypes.Gain,
                25m,
                AbilityTargetTypes.Friendly,
                amountIsPercentOfMaximum:
                    true
            )
        );

        RunAbility(source, target);

        Assert.Equal(100m, target.Resources["mana"].Current);
    }

    [Fact]
    public void ResourceChange_MultiTargetAffectsEachResolvedTarget()
    {
        var source = CreateActor("source", "raid");
        var targetA = CreateActor("a-target", "raid", "mana", 100m, 20m);
        var targetB = CreateActor("b-target", "raid", "mana", 100m, 30m);

        source.AddAbility(
            CreateResourceAbility(
                "group-restore",
                "mana",
                ResourceChangeOperationTypes.Gain,
                25m,
                AbilityTargetTypes.Friendly,
                maxTargets:
                    2
            )
        );

        var result =
            RunAbility(
                source,
                targetA,
                [targetB]
            );

        Assert.Equal(45m, targetA.Resources["mana"].Current);
        Assert.Equal(55m, targetB.Resources["mana"].Current);

        Assert.Equal(
            2,
            result.Timeline.Count(
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.ResourceChanged
            )
        );
    }

    [Fact]
    public void ResourceChange_MissDoesNotChangeResource()
    {
        var source = CreateActor("source", "raid");
        var target = CreateActor("target", "enemy", "mana", 100m, 75m);

        source.AddAbility(
            CreateResourceAbility(
                "missed-drain",
                "mana",
                ResourceChangeOperationTypes.Spend,
                50m,
                AbilityTargetTypes.Enemy
            )
        );

        var result =
            RunAbility(
                source,
                target,
                combatRollResolver:
                    new SimpleCombatRollResolver(
                        hitChancePercent:
                            0m
                    )
            );

        Assert.Equal(75m, target.Resources["mana"].Current);

        Assert.DoesNotContain(
            result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                    CombatEventType.ResourceChanged
        );
    }

    [Fact]
    public void Validator_RejectsInvalidResourceChangeDefinition()
    {
        var source = CreateActor("source", "raid");

        source.AddAbility(
            new AbilityDefinition
            {
                Key = "invalid-resource",
                Name = "Invalid Resource",
                IsOffGlobalCooldown = true,
                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key = "invalid-resource-effect",
                        EffectType = AbilityEffectTypes.ResourceChange,
                        TargetType = AbilityTargetTypes.Self,
                        ResourceChangeOperation = "invent-resource-operation",
                        MinimumValue = -10m,
                        MaximumValue = -10m
                    }
                ]
            }
        );

        var context = CreateContext(source.Key);
        context.AddActor(source);

        var exception =
            Assert.Throws<SimulationDefinitionValidationException>(
                () =>
                    new SimulationEngine()
                        .Run(context)
            );

        Assert.Contains(
            exception.Errors,
            error =>
                error.Contains(
                    "requires ResourceKey",
                    StringComparison.OrdinalIgnoreCase
                )
        );

        Assert.Contains(
            exception.Errors,
            error =>
                error.Contains(
                    "unknown resource operation",
                    StringComparison.OrdinalIgnoreCase
                )
        );

        Assert.Contains(
            exception.Errors,
            error =>
                error.Contains(
                    "requires non-negative effect values",
                    StringComparison.OrdinalIgnoreCase
                )
        );
    }

    private static AbilityDefinition CreateResourceAbility(
        string key,
        string resourceKey,
        string operation,
        decimal value,
        string targetType,
        bool amountIsPercentOfMaximum = false,
        int maxTargets = 1)
    {
        return new AbilityDefinition
        {
            Key = key,
            Name = key,
            IsOffGlobalCooldown = true,
            Effects =
            [
                new AbilityEffectDefinition
                {
                    Key = $"{key}-effect",
                    EffectType = AbilityEffectTypes.ResourceChange,
                    TargetType = targetType,
                    ResolutionType =
                        string.Equals(
                            targetType,
                            AbilityTargetTypes.Enemy,
                            StringComparison.OrdinalIgnoreCase)
                            ? CombatResolutionTypes.Spell
                            : CombatResolutionTypes.AlwaysHits,
                    CanMiss =
                        string.Equals(
                            targetType,
                            AbilityTargetTypes.Enemy,
                            StringComparison.OrdinalIgnoreCase),
                    CanCrit = false,
                    ResourceKey = resourceKey,
                    ResourceChangeOperation = operation,
                    ResourceAmountIsPercentOfMaximum = amountIsPercentOfMaximum,
                    MinimumValue = value,
                    MaximumValue = value,
                    MaxTargets = maxTargets
                }
            ]
        };
    }

    private static SimulationRunResult RunAbility(
        SimulationActorState source,
        SimulationActorState primaryTarget,
        IReadOnlyList<SimulationActorState>? additionalActors = null,
        ICombatRollResolver? combatRollResolver = null)
    {
        var context =
            CreateContext(
                source.Key,
                captureTimeline:
                    true
            );

        context.AddActor(source);

        if (!string.Equals(
                source.Key,
                primaryTarget.Key,
                StringComparison.OrdinalIgnoreCase))
        {
            context.AddActor(primaryTarget);
        }

        foreach (var actor in additionalActors ?? [])
        {
            context.AddActor(actor);
        }

        var executor =
            new AbilityExecutor(
                new AuraManager(),
                combatRollResolver ??
                new SimpleCombatRollResolver(),
                new RulesetDamageMitigationResolver(
                    new CombatRulesetDefinition
                    {
                        RulesetKey = "resource-change-tests",
                        Version = "1"
                    }
                )
            );

        return new SimulationEngine(
            [executor]
        )
        .Run(
            context,
            startedContext =>
            {
                var result =
                    executor.TryStartAbility(
                        startedContext,
                        source.Key,
                        primaryTarget.Key,
                        source.Abilities.Values
                            .Single()
                            .Definition.Key
                    );

                Assert.True(result.Success);
            }
        );
    }

    private static SimulationContext CreateContext(
        string primaryActorKey,
        bool captureTimeline = false)
    {
        return new SimulationContext(
            new SimulationRunOptions
            {
                Seed = 12345,
                DurationSeconds = 1m,
                PrimaryActorKey = primaryActorKey,
                CaptureTimeline = captureTimeline
            }
        );
    }

    private static SimulationActorState CreateActor(
        string key,
        string teamKey,
        string? resourceKey = null,
        decimal maximum = 0m,
        decimal current = 0m)
    {
        var actor =
            new SimulationActorState
            {
                Key = key,
                Name = key,
                TeamKey = teamKey,
                Level = 20
            };

        actor.InitializeHealth(
            maximumHealth:
                5000m
        );

        actor.ConfigureActionTiming(
            initialActionDelaySeconds:
                0m,
            inputDelaySeconds:
                0m
        );

        if (!string.IsNullOrWhiteSpace(resourceKey))
        {
            actor.AddResource(
                new ResourceState(
                    resourceKey,
                    maximum,
                    current
                )
            );
        }

        return actor;
    }
}
