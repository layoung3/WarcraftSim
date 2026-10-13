// Consolidated tests for Ability Lifecycle.
// Original class names are retained for xUnit test filtering.

// ==================== AbilityCombatSummaryTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

public sealed class AbilityCombatSummaryTests
{
    [Fact]
    public void DirectDamage_RecordsAmountResultAndDelivery()
    {
        var result = Run(
            new CombatEvent
            {
                TimeSeconds = 0.1m,
                Type = CombatEventType.Damage,
                SourceActorKey = "player",
                TargetActorKey = "target",
                AbilityKey = "strike",
                ResultKey = CombatResultTypes.Hit,
                Amount = 125m,
                EffectDeliveryType = CombatEffectDeliveryType.Direct
            }
        );

        var summary = result.Summary.Abilities["strike"];

        Assert.Equal(1, summary.DamageOccurrenceCount);
        Assert.Equal(125m, summary.DamageDone);
        Assert.Equal(1, summary.DirectOccurrenceCount);
        Assert.Equal(0, summary.PeriodicOccurrenceCount);
        Assert.Equal(0, summary.ChannelTickOccurrenceCount);
        Assert.Equal(1, summary.ResultCounts[CombatResultTypes.Hit]);
    }

    [Fact]
    public void AvoidedDamage_StillRecordsOutcomeOccurrence()
    {
        var result = Run(
            new CombatEvent
            {
                TimeSeconds = 0.1m,
                Type = CombatEventType.Damage,
                SourceActorKey = "player",
                TargetActorKey = "target",
                AbilityKey = "strike",
                ResultKey = CombatResultTypes.Miss,
                Amount = 0m
            }
        );

        var summary = result.Summary.Abilities["strike"];

        Assert.Equal(1, summary.DamageOccurrenceCount);
        Assert.Equal(0m, summary.DamageDone);
        Assert.Equal(1, summary.ResultCounts[CombatResultTypes.Miss]);
    }

    [Fact]
    public void CriticalDamage_RecordsCriticalCount()
    {
        var result = Run(
            new CombatEvent
            {
                TimeSeconds = 0.1m,
                Type = CombatEventType.Damage,
                SourceActorKey = "player",
                TargetActorKey = "target",
                AbilityKey = "crit-spell",
                ResultKey = CombatResultTypes.Critical,
                Amount = 240m,
                IsCritical = true
            }
        );

        var summary = result.Summary.Abilities["crit-spell"];

        Assert.Equal(1, summary.CriticalDamageCount);
        Assert.Equal(1, summary.ResultCounts[CombatResultTypes.Critical]);
    }

    [Fact]
    public void PeriodicAndChannelTickOccurrences_AreSeparated()
    {
        var result = Run(
            new CombatEvent
            {
                TimeSeconds = 0.1m,
                Type = CombatEventType.Damage,
                SourceActorKey = "player",
                TargetActorKey = "target",
                AbilityKey = "multi",
                ResultKey = CombatResultTypes.Hit,
                Amount = 10m,
                EffectDeliveryType = CombatEffectDeliveryType.Periodic
            },
            new CombatEvent
            {
                TimeSeconds = 0.2m,
                Type = CombatEventType.Damage,
                SourceActorKey = "player",
                TargetActorKey = "target",
                AbilityKey = "multi",
                ResultKey = CombatResultTypes.Hit,
                Amount = 20m,
                EffectDeliveryType = CombatEffectDeliveryType.ChannelTick
            }
        );

        var summary = result.Summary.Abilities["multi"];

        Assert.Equal(2, summary.DamageOccurrenceCount);
        Assert.Equal(30m, summary.DamageDone);
        Assert.Equal(1, summary.PeriodicOccurrenceCount);
        Assert.Equal(1, summary.ChannelTickOccurrenceCount);
        Assert.Equal(0, summary.DirectOccurrenceCount);
    }

    [Fact]
    public void Healing_RecordsCriticalOverhealingAndDelivery()
    {
        var result = Run(
            new CombatEvent
            {
                TimeSeconds = 0.1m,
                Type = CombatEventType.Healing,
                SourceActorKey = "player",
                TargetActorKey = "player",
                AbilityKey = "hot",
                ResultKey = CombatResultTypes.Critical,
                Amount = 80m,
                OverhealingAmount = 20m,
                IsCritical = true,
                EffectDeliveryType = CombatEffectDeliveryType.Periodic
            }
        );

        var summary = result.Summary.Abilities["hot"];

        Assert.Equal(1, summary.HealingOccurrenceCount);
        Assert.Equal(80m, summary.HealingDone);
        Assert.Equal(20m, summary.OverhealingDone);
        Assert.Equal(1, summary.CriticalHealingCount);
        Assert.Equal(1, summary.PeriodicOccurrenceCount);
    }

    [Fact]
    public void AbilityLifecycle_RecordsCastAndChannelCounts()
    {
        var result = Run(
            new CombatEvent
            {
                TimeSeconds = 0.05m,
                Type = CombatEventType.AbilityCastStarted,
                SourceActorKey = "player",
                TargetActorKey = "target",
                AbilityKey = "channel"
            },
            new CombatEvent
            {
                TimeSeconds = 0.10m,
                Type = CombatEventType.AbilityCastCompleted,
                SourceActorKey = "player",
                TargetActorKey = "target",
                AbilityKey = "channel"
            },
            new CombatEvent
            {
                TimeSeconds = 0.10m,
                Type = CombatEventType.AbilityChannelStarted,
                SourceActorKey = "player",
                TargetActorKey = "target",
                AbilityKey = "channel"
            },
            new CombatEvent
            {
                TimeSeconds = 0.50m,
                Type = CombatEventType.AbilityChannelCompleted,
                SourceActorKey = "player",
                TargetActorKey = "target",
                AbilityKey = "channel"
            }
        );

        var summary = result.Summary.Abilities["channel"];

        Assert.Equal(1, summary.CastStartedCount);
        Assert.Equal(1, summary.CastCompletedCount);
        Assert.Equal(1, summary.ChannelStartedCount);
        Assert.Equal(1, summary.ChannelCompletedCount);
    }

    [Fact]
    public void InternalChannelCompletionDriver_IsNotDoubleCounted()
    {
        var result = Run(
            new CombatEvent
            {
                TimeSeconds = 0.40m,
                Type = CombatEventType.AbilityChannelCompleted,
                SourceActorKey = "player",
                TargetActorKey = "target",
                AbilityKey = "channel",
                IsInternal = true
            },
            new CombatEvent
            {
                TimeSeconds = 0.40m,
                Type = CombatEventType.AbilityChannelCompleted,
                SourceActorKey = "player",
                TargetActorKey = "target",
                AbilityKey = "channel"
            }
        );

        Assert.Equal(
            1,
            result.Summary.Abilities["channel"].ChannelCompletedCount
        );
    }

    [Fact]
    public void CancelledExecution_StaleCastCompletionIsNotReported()
    {
        var context =
            CreateContext();

        var execution =
            new AbilityExecutionState
            {
                SourceActorKey = "player",
                TargetActorKey = "target",
                AbilityKey = "cancelled-cast"
            };

        context.AddAbilityExecution(execution);
        context.CancelAbilityExecution(execution.Id, 0m);

        context.ScheduleEvent(
            new CombatEvent
            {
                TimeSeconds = 0.1m,
                Type = CombatEventType.AbilityCastCancelled,
                SourceActorKey = "player",
                TargetActorKey = "target",
                AbilityKey = "cancelled-cast",
                AbilityExecutionId = execution.Id
            }
        );

        context.ScheduleEvent(
            new CombatEvent
            {
                TimeSeconds = 0.2m,
                Type = CombatEventType.AbilityCastCompleted,
                SourceActorKey = "player",
                TargetActorKey = "target",
                AbilityKey = "cancelled-cast",
                AbilityExecutionId = execution.Id
            }
        );

        var result =
            new SimulationEngine().Run(context);

        var summary =
            result.Summary.Abilities["cancelled-cast"];

        Assert.Equal(1, summary.CastCancelledCount);
        Assert.Equal(0, summary.CastCompletedCount);
    }

    [Fact]
    public void AbsorbConsumption_RecordsActualAbsorptionDone()
    {
        var result = Run(
            new CombatEvent
            {
                TimeSeconds = 0.1m,
                Type = CombatEventType.AbsorbConsumed,
                SourceActorKey = "player",
                TargetActorKey = "player",
                AbilityKey = "shield",
                Amount = 75m
            }
        );

        var summary = result.Summary.Abilities["shield"];

        Assert.Equal(1, summary.AbsorbConsumptionCount);
        Assert.Equal(75m, summary.AbsorptionDone);
    }

    [Fact]
    public void PrimaryAbilitySummary_IsolatedFromOtherActors()
    {
        var result = Run(
            new CombatEvent
            {
                TimeSeconds = 0.1m,
                Type = CombatEventType.Damage,
                SourceActorKey = "target",
                TargetActorKey = "player",
                AbilityKey = "enemy-hit",
                ResultKey = CombatResultTypes.Hit,
                Amount = 50m
            }
        );

        Assert.False(result.Summary.Abilities.ContainsKey("enemy-hit"));
        Assert.Equal(
            50m,
            result.Summary.ActorSummaries["target"]
                .Abilities["enemy-hit"]
                .DamageDone
        );
    }

    private static SimulationRunResult Run(
        params CombatEvent[] events)
    {
        var context =
            CreateContext();

        foreach (var combatEvent in events)
        {
            context.ScheduleEvent(combatEvent);
        }

        return new SimulationEngine().Run(context);
    }

    private static SimulationContext CreateContext()
    {
        var context =
            new SimulationContext(
                new SimulationRunOptions
                {
                    Seed = 12345,
                    DurationSeconds = 1m,
                    PrimaryActorKey = "player"
                }
            );

        var player =
            new SimulationActorState
            {
                Key = "player",
                Name = "Player"
            };

        player.InitializeHealth(1000m);

        var target =
            new SimulationActorState
            {
                Key = "target",
                Name = "Target"
            };

        target.InitializeHealth(1000m);

        context.AddActor(player);
        context.AddActor(target);

        return context;
    }
}
}

// ==================== AbilityEffectTargetingTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

public sealed class AbilityEffectTargetingTests
{
    [Fact]
    public void Ability_CanDamagePrimaryEnemyAndHealSource()
    {
        var source =
            CreateActor(
                "source",
                "raid",
                maximumHealth:
                    1000m,

                startingHealth:
                    500m
            );

        var target =
            CreateActor(
                "target",
                "enemy"
            );

        source.AddAbility(
            new AbilityDefinition
            {
                Key =
                    "draining-strike",

                Name =
                    "Draining Strike",

                IsOffGlobalCooldown =
                    true,

                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key =
                            "strike-damage",

                        EffectType =
                            AbilityEffectTypes.DirectDamage,

                        TargetType =
                            AbilityTargetTypes.Enemy,

                        ResolutionType =
                            CombatResolutionTypes.AlwaysHits,

                        CanMiss =
                            false,

                        CanCrit =
                            false,

                        MinimumValue =
                            100m,

                        MaximumValue =
                            100m
                    },

                    new AbilityEffectDefinition
                    {
                        Key =
                            "self-heal",

                        EffectType =
                            AbilityEffectTypes.DirectHealing,

                        TargetType =
                            AbilityTargetTypes.Self,

                        ResolutionType =
                            CombatResolutionTypes.AlwaysHits,

                        CanMiss =
                            false,

                        CanCrit =
                            false,

                        MinimumValue =
                            200m,

                        MaximumValue =
                            200m
                    }
                ]
            }
        );

        var result =
            RunSingleAbility(
                source,
                target,
                []
            );

        Assert.Equal(
            900m,
            target.CurrentHealth
        );

        Assert.Equal(
            700m,
            source.CurrentHealth
        );

        var damage =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.Damage
            );

        Assert.Equal(
            target.Key,
            damage.TargetActorKey
        );

        var healing =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.Healing
            );

        Assert.Equal(
            source.Key,
            healing.TargetActorKey
        );
    }

    [Fact]
    public void Ability_MultiTargetDamageUsesPrimaryThenDeterministicAdditionalEnemies()
    {
        var source =
            CreateActor(
                "source",
                "raid"
            );

        var primary =
            CreateActor(
                "boss",
                "enemy"
            );

        var addZ =
            CreateActor(
                "z-add",
                "enemy"
            );

        var addA =
            CreateActor(
                "a-add",
                "enemy"
            );

        var addM =
            CreateActor(
                "m-add",
                "enemy"
            );

        source.AddAbility(
            new AbilityDefinition
            {
                Key =
                    "cleave",

                Name =
                    "Cleave",

                IsOffGlobalCooldown =
                    true,

                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key =
                            "cleave-damage",

                        EffectType =
                            AbilityEffectTypes.DirectDamage,

                        TargetType =
                            AbilityTargetTypes.Enemy,

                        ResolutionType =
                            CombatResolutionTypes.AlwaysHits,

                        CanMiss =
                            false,

                        CanCrit =
                            false,

                        MinimumValue =
                            100m,

                        MaximumValue =
                            100m,

                        MaxTargets =
                            3
                    }
                ]
            }
        );

        var result =
            RunSingleAbility(
                source,
                primary,
                [
                    addZ,
                    addM,
                    addA
                ]
            );

        Assert.Equal(
            900m,
            primary.CurrentHealth
        );

        Assert.Equal(
            900m,
            addA.CurrentHealth
        );

        Assert.Equal(
            900m,
            addM.CurrentHealth
        );

        Assert.Equal(
            1000m,
            addZ.CurrentHealth
        );

        Assert.Equal(
            new[]
            {
                "boss",
                "a-add",
                "m-add"
            },
            result.Timeline
                .Where(
                    combatEvent =>
                        combatEvent.Type ==
                            CombatEventType.Damage
                )
                .Select(
                    combatEvent =>
                        combatEvent.TargetActorKey!
                )
                .ToArray()
        );
    }

    private static SimulationRunResult RunSingleAbility(
        SimulationActorState source,
        SimulationActorState primaryTarget,
        IReadOnlyList<SimulationActorState> additionalActors)
    {
        var context =
            new SimulationContext(
                new SimulationRunOptions
                {
                    Seed =
                        12345,

                    DurationSeconds =
                        1m,

                    PrimaryActorKey =
                        source.Key,

                    CaptureTimeline =
                        true
                }
            );

        context.AddActor(
            source
        );

        context.AddActor(
            primaryTarget
        );

        foreach (
            var additionalActor in
            additionalActors)
        {
            context.AddActor(
                additionalActor
            );
        }

        var executor =
            CreateAbilityExecutor();

        var engine =
            new SimulationEngine(
                [
                    executor
                ]
            );

        return engine.Run(
            context,
            startedContext =>
            {
                var startResult =
                    executor.TryStartAbility(
                        startedContext,
                        source.Key,
                        primaryTarget.Key,
                        source.Abilities.Values
                            .Single()
                            .Definition.Key
                    );

                Assert.True(
                    startResult.Success
                );
            }
        );
    }

    private static SimulationActorState CreateActor(
        string key,
        string teamKey,
        decimal maximumHealth = 1000m,
        decimal? startingHealth = null)
    {
        var actor =
            new SimulationActorState
            {
                Key =
                    key,

                Name =
                    key,

                TeamKey =
                    teamKey
            };

        actor.InitializeHealth(
            maximumHealth,
            startingHealth
        );

        actor.ConfigureActionTiming(
            initialActionDelaySeconds:
                0m,

            inputDelaySeconds:
                0m
        );

        return actor;
    }

    private static AbilityExecutor CreateAbilityExecutor()
    {
        return new AbilityExecutor(
            new AuraManager(),
            new SimpleCombatRollResolver(),
            new RulesetDamageMitigationResolver(
                new CombatRulesetDefinition
                {
                    RulesetKey =
                        "effect-targeting-tests",

                    Version =
                        "1"
                }
            )
        );
    }
}
}

// ==================== AbilityFinisherResourceFoundationTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Validation;

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
}

// ==================== AbilityLifecycleOwnershipTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

public sealed class AbilityLifecycleOwnershipTests
{
    [Fact]
    public void TryStartAbility_OwnsCastTrackingAndActionTiming()
    {
        var source =
            CreateActor(
                "source",
                "Source",
                "raid"
            );

        var target =
            CreateActor(
                "target",
                "Target",
                "enemy"
            );

        source.ConfigureActionTiming(
            initialActionDelaySeconds:
                0m,

            inputDelaySeconds:
                0.2m
        );

        source.AddAbility(
            CreateAbility(
                key:
                    "long-cast",

                castTimeSeconds:
                    2m,

                globalCooldownSeconds:
                    1.5m
            )
        );

        var context =
            CreateContext(
                source.Key,
                durationSeconds:
                    3m
            );

        context.AddActor(
            source
        );

        context.AddActor(
            target
        );

        var executor =
            CreateAbilityExecutor();

        var result =
            executor.TryStartAbility(
                context,
                source.Key,
                target.Key,
                "long-cast"
            );

        Assert.True(
            result.Success
        );

        Assert.True(
            source.CurrentCastExecutionId.HasValue
        );

        Assert.Equal(
            "long-cast",
            source.CurrentCastAbilityKey
        );

        Assert.Equal(
            2m,
            source.CastReadyAtSeconds
        );

        Assert.Equal(
            1.5m,
            source.GlobalCooldownReadyAtSeconds
        );

        Assert.Equal(
            2.2m,
            source.InputReadyAtSeconds
        );
    }

    [Fact]
    public void TryStartAbility_EnforcesActionInputLockWithoutRotationExecutor()
    {
        var source =
            CreateActor(
                "source",
                "Source",
                "raid"
            );

        var target =
            CreateActor(
                "target",
                "Target",
                "enemy"
            );

        source.ConfigureActionTiming(
            initialActionDelaySeconds:
                0m,

            inputDelaySeconds:
                0.25m
        );

        source.AddAbility(
            CreateAbility(
                key:
                    "instant",

                castTimeSeconds:
                    0m,

                globalCooldownSeconds:
                    0m,

                isOffGlobalCooldown:
                    true
            )
        );

        var context =
            CreateContext(
                source.Key,
                durationSeconds:
                    1m
            );

        context.AddActor(
            source
        );

        context.AddActor(
            target
        );

        var executor =
            CreateAbilityExecutor();

        var first =
            executor.TryStartAbility(
                context,
                source.Key,
                target.Key,
                "instant"
            );

        var second =
            executor.TryStartAbility(
                context,
                source.Key,
                target.Key,
                "instant"
            );

        Assert.True(
            first.Success
        );

        Assert.False(
            second.Success
        );

        Assert.Equal(
            0.25m,
            source.InputReadyAtSeconds
        );
    }

    [Fact]
    public void AbilityCompletion_ClearsCurrentCastWithoutRotationExecutor()
    {
        var source =
            CreateActor(
                "source",
                "Source",
                "raid"
            );

        var target =
            CreateActor(
                "target",
                "Target",
                "enemy"
            );

        source.AddAbility(
            CreateAbility(
                key:
                    "one-second-cast",

                castTimeSeconds:
                    1m,

                globalCooldownSeconds:
                    1m
            )
        );

        var context =
            CreateContext(
                source.Key,
                durationSeconds:
                    2m
            );

        context.AddActor(
            source
        );

        context.AddActor(
            target
        );

        var executor =
            CreateAbilityExecutor();

        var engine =
            new SimulationEngine(
                [
                    executor
                ]
            );

        engine.Run(
            context,
            startedContext =>
            {
                var startResult =
                    executor.TryStartAbility(
                        startedContext,
                        source.Key,
                        target.Key,
                        "one-second-cast"
                    );

                Assert.True(
                    startResult.Success
                );

                Assert.True(
                    source.CurrentCastExecutionId.HasValue
                );
            }
        );

        Assert.Null(
            source.CurrentCastExecutionId
        );

        Assert.Null(
            source.CurrentCastAbilityKey
        );
    }

    [Fact]
    public void TryCancelCurrentCast_OwnsCancellationStateAndEvent()
    {
        var source =
            CreateActor(
                "source",
                "Source",
                "raid"
            );

        var target =
            CreateActor(
                "target",
                "Target",
                "enemy"
            );

        source.AddAbility(
            CreateAbility(
                key:
                    "long-cast",

                castTimeSeconds:
                    5m,

                globalCooldownSeconds:
                    1.5m
            )
        );

        var context =
            CreateContext(
                source.Key,
                durationSeconds:
                    2m
            );

        context.AddActor(
            source
        );

        context.AddActor(
            target
        );

        var executor =
            CreateAbilityExecutor();

        var driver =
            new CancellationDriver(
                executor,
                source.Key,
                target.Key,
                "long-cast"
            );

        var engine =
            new SimulationEngine(
                [
                    executor,
                    driver
                ]
            );

        var result =
            engine.Run(
                context
            );

        Assert.True(
            driver.CancelSucceeded
        );

        Assert.True(
            driver.StartedExecutionId.HasValue
        );

        Assert.Null(
            source.CurrentCastExecutionId
        );

        Assert.Null(
            source.CurrentCastAbilityKey
        );

        var execution =
            context.GetAbilityExecution(
                driver.StartedExecutionId!.Value
            );

        Assert.NotNull(
            execution
        );

        Assert.True(
            execution!.IsCancelled
        );

        Assert.Equal(
            1m,
            execution.CancelledAtSeconds
        );

        var cancelledEvent =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type ==
                    CombatEventType.AbilityCastCancelled
            );

        Assert.Equal(
            source.Key,
            cancelledEvent.SourceActorKey
        );

        Assert.Equal(
            target.Key,
            cancelledEvent.TargetActorKey
        );

        Assert.Equal(
            "long-cast",
            cancelledEvent.AbilityKey
        );
    }

    private static SimulationActorState CreateActor(
        string key,
        string name,
        string teamKey)
    {
        var actor =
            new SimulationActorState
            {
                Key =
                    key,

                Name =
                    name,

                TeamKey =
                    teamKey
            };

        actor.InitializeHealth(
            maximumHealth:
                1000m
        );

        actor.ConfigureActionTiming(
            initialActionDelaySeconds:
                0m,

            inputDelaySeconds:
                0m
        );

        return actor;
    }

    private static AbilityDefinition CreateAbility(
        string key,
        decimal castTimeSeconds,
        decimal globalCooldownSeconds,
        bool isOffGlobalCooldown = false)
    {
        return new AbilityDefinition
        {
            Key =
                key,

            Name =
                key,

            CastTimeSeconds =
                castTimeSeconds,

            GlobalCooldownSeconds =
                globalCooldownSeconds,

            IsOffGlobalCooldown =
                isOffGlobalCooldown
        };
    }

    private static SimulationContext CreateContext(
        string primaryActorKey,
        decimal durationSeconds)
    {
        return new SimulationContext(
            new SimulationRunOptions
            {
                Seed =
                    12345,

                DurationSeconds =
                    durationSeconds,

                PrimaryActorKey =
                    primaryActorKey,

                CaptureTimeline =
                    true
            }
        );
    }

    private static AbilityExecutor CreateAbilityExecutor()
    {
        return new AbilityExecutor(
            new AuraManager(),
            new SimpleCombatRollResolver(),
            new RulesetDamageMitigationResolver(
                new CombatRulesetDefinition
                {
                    RulesetKey =
                        "ability-lifecycle-tests",

                    Version =
                        "1"
                }
            )
        );
    }

    private sealed class CancellationDriver :
        ICombatEventProcessor
    {
        private readonly AbilityExecutor
            _executor;

        private readonly string
            _sourceActorKey;

        private readonly string
            _targetActorKey;

        private readonly string
            _abilityKey;

        public CancellationDriver(
            AbilityExecutor executor,
            string sourceActorKey,
            string targetActorKey,
            string abilityKey)
        {
            _executor =
                executor;

            _sourceActorKey =
                sourceActorKey;

            _targetActorKey =
                targetActorKey;

            _abilityKey =
                abilityKey;
        }

        public Guid? StartedExecutionId { get; private set; }

        public bool CancelSucceeded { get; private set; }

        public void Process(
            SimulationContext context,
            CombatEvent combatEvent)
        {
            if (
                combatEvent.Type ==
                CombatEventType.SimulationStarted
            )
            {
                var result =
                    _executor.TryStartAbility(
                        context,
                        _sourceActorKey,
                        _targetActorKey,
                        _abilityKey
                    );

                Assert.True(
                    result.Success
                );

                StartedExecutionId =
                    context.GetActor(
                            _sourceActorKey
                        )?
                        .CurrentCastExecutionId;

                context.ScheduleEvent(
                    new CombatEvent
                    {
                        TimeSeconds =
                            1m,

                        Type =
                            CombatEventType.RotationDecision,

                        SourceActorKey =
                            _sourceActorKey,

                        IsInternal =
                            true,

                        Description =
                            "Test cancellation trigger."
                    }
                );

                return;
            }

            if (
                combatEvent.Type ==
                    CombatEventType.RotationDecision &&
                string.Equals(
                    combatEvent.SourceActorKey,
                    _sourceActorKey,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                CancelSucceeded =
                    _executor.TryCancelCurrentCast(
                        context,
                        _sourceActorKey
                    );
            }
        }
    }
}
}

// ==================== AbilitySourceAuraAndDamageMultiplierTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Validation;

public sealed class AbilitySourceAuraAndDamageMultiplierTests
{
    [Fact]
    public void MissingRequiredSourceAura_PreventsAbilityUseWithoutSpendingResource()
    {
        var source = CreateActor("source", "raid");
        var target = CreateActor("target", "enemy");
        source.AddResource(new ResourceState("rage", 100m, 50m));
        source.AddAbility(CreateRequiredAuraAbility());

        var context = CreateContext(source, target);
        var executor = CreateExecutor();

        var result = executor.TryStartAbility(
            context,
            source.Key,
            target.Key,
            "stance-strike"
        );

        Assert.False(result.Success);
        Assert.Contains(
            "requires source aura",
            result.FailureReason ?? string.Empty,
            StringComparison.OrdinalIgnoreCase
        );
        Assert.Equal(50m, source.Resources["rage"].Current);
    }

    [Fact]
    public void ActiveRequiredSourceAura_AllowsAbilityUse()
    {
        var source = CreateActor("source", "raid");
        var target = CreateActor("target", "enemy");
        source.AddResource(new ResourceState("rage", 100m, 50m));
        source.AddAbility(CreateRequiredAuraAbility());
        AddAura(source, "required-stance", expiresAtSeconds: 10m);

        var context = CreateContext(source, target);
        var executor = CreateExecutor();

        var use = executor.TryStartAbility(
            context,
            source.Key,
            target.Key,
            "stance-strike"
        );

        Assert.True(use.Success, use.FailureReason ?? "Ability use failed.");
    }

    [Fact]
    public void ExpiredRequiredSourceAura_DoesNotSatisfyRequirement()
    {
        var source = CreateActor("source", "raid");
        var target = CreateActor("target", "enemy");
        source.AddResource(new ResourceState("rage", 100m, 50m));
        source.AddAbility(CreateRequiredAuraAbility());
        AddAura(source, "required-stance", expiresAtSeconds: 0m);

        var context = CreateContext(source, target);
        var executor = CreateExecutor();

        var use = executor.TryStartAbility(
            context,
            source.Key,
            target.Key,
            "stance-strike"
        );

        Assert.False(use.Success);
    }

    [Fact]
    public void DamageMultiplierAndLiveStatPercent_AreAppliedBeforeMitigation()
    {
        var source = CreateActor("source", "raid");
        var target = CreateActor("target", "enemy");
        source.Stats.Set("off-hand-damage-percent", 20m);
        source.AddAbility(
            new AbilityDefinition
            {
                Key = "multiplier-test",
                Name = "Multiplier Test",
                IsOffGlobalCooldown = true,
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
                        DamageMultiplier = 0.5m,
                        DamageMultiplierStatKey = "off-hand-damage-percent"
                    }
                ]
            }
        );

        var context = CreateContext(source, target);
        var executor = CreateExecutor();
        var result = new SimulationEngine([executor]).Run(
            context,
            started =>
            {
                var use = executor.TryStartAbility(
                    started,
                    source.Key,
                    target.Key,
                    "multiplier-test"
                );
                Assert.True(use.Success, use.FailureReason ?? "Ability use failed.");
            }
        );

        var damage = Assert.Single(
            result.Timeline,
            combatEvent => combatEvent.Type == CombatEventType.Damage
        );

        Assert.Equal(60m, damage.RawAmount);
        Assert.Equal(60m, damage.Amount);
    }

    [Fact]
    public void Validator_RejectsNegativeEffectDamageMultiplier()
    {
        var source = CreateActor("source", "raid");
        source.AddAbility(
            new AbilityDefinition
            {
                Key = "bad-multiplier",
                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key = "damage",
                        EffectType = AbilityEffectTypes.DirectDamage,
                        DamageMultiplier = -1m
                    }
                ]
            }
        );

        var context = new SimulationContext(
            new SimulationRunOptions
            {
                DurationSeconds = 0.1m,
                PrimaryActorKey = source.Key
            }
        );
        context.AddActor(source);

        var exception = Assert.Throws<SimulationDefinitionValidationException>(
            () => new SimulationEngine().Run(context)
        );

        Assert.Contains(
            exception.Errors,
            error => error.Contains("negative damage multiplier", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void Validator_RejectsDuplicateRequiredSourceAuraKeys()
    {
        var source = CreateActor("source", "raid");
        source.AddAbility(
            new AbilityDefinition
            {
                Key = "duplicate-aura-requirement",
                RequiredSourceAuraKeys =
                [
                    "berserker-stance",
                    "BERSERKER-STANCE"
                ]
            }
        );

        var context = new SimulationContext(
            new SimulationRunOptions
            {
                DurationSeconds = 0.1m,
                PrimaryActorKey = source.Key
            }
        );
        context.AddActor(source);

        var exception = Assert.Throws<SimulationDefinitionValidationException>(
            () => new SimulationEngine().Run(context)
        );

        Assert.Contains(
            exception.Errors,
            error => error.Contains("duplicate required source aura key", StringComparison.OrdinalIgnoreCase)
        );
    }

    private static AbilityDefinition CreateRequiredAuraAbility()
    {
        return new AbilityDefinition
        {
            Key = "stance-strike",
            Name = "Stance Strike",
            RequiredSourceAuraKeys = ["required-stance"],
            ResourceCosts =
            [
                new AbilityResourceCost
                {
                    ResourceKey = "rage",
                    Amount = 10m
                }
            ],
            Effects =
            [
                new AbilityEffectDefinition
                {
                    Key = "damage",
                    EffectType = AbilityEffectTypes.DirectDamage,
                    ResolutionType = CombatResolutionTypes.AlwaysHits,
                    CanMiss = false,
                    CanCrit = false,
                    MinimumValue = 1m,
                    MaximumValue = 1m
                }
            ]
        };
    }

    private static void AddAura(
        SimulationActorState actor,
        string key,
        decimal expiresAtSeconds)
    {
        actor.ActiveAuras.Add(
            new AuraInstance
            {
                Definition = new AuraDefinition
                {
                    Key = key,
                    Name = key,
                    DurationSeconds = Math.Max(0m, expiresAtSeconds)
                },
                SourceActorKey = actor.Key,
                TargetActorKey = actor.Key,
                AppliedAtSeconds = 0m,
                ExpiresAtSeconds = expiresAtSeconds
            }
        );
    }

    private static SimulationContext CreateContext(
        SimulationActorState source,
        SimulationActorState target)
    {
        var context = new SimulationContext(
            new SimulationRunOptions
            {
                DurationSeconds = 0.1m,
                CaptureTimeline = true,
                PrimaryActorKey = source.Key,
                Seed = 1
            }
        );
        context.AddActor(source);
        context.AddActor(target);
        return context;
    }

    private static SimulationActorState CreateActor(
        string key,
        string teamKey)
    {
        var actor = new SimulationActorState
        {
            Key = key,
            Name = key,
            TeamKey = teamKey,
            Level = 60
        };
        actor.InitializeHealth(5000m);
        return actor;
    }

    private static AbilityExecutor CreateExecutor()
    {
        return new AbilityExecutor(
            new AuraManager(),
            new SimpleCombatRollResolver(
                hitChancePercent: 100m,
                criticalChancePercent: 0m
            ),
            new RulesetDamageMitigationResolver(
                new CombatRulesetDefinition
                {
                    RulesetKey = "ability-state-tests",
                    Version = "1"
                }
            )
        );
    }
}
}
