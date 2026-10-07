using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Validation;

namespace WarcraftSim.Tests;

public sealed class ChannelingFoundationTests
{
    [Fact]
    public void Channel_ExecutesConfiguredTickEffectsAndCompletes()
    {
        var source =
            CreateActor(
                "caster",
                "raid"
            );

        var target =
            CreateActor(
                "target",
                "enemy"
            );

        source.AddAbility(
            CreateDamageChannel(
                "beam",
                durationSeconds:
                    3m,
                tickIntervalSeconds:
                    1m,
                damagePerTick:
                    100m
            )
        );

        var run =
            RunAbility(
                source,
                target,
                durationSeconds:
                    4m
            );

        Assert.Equal(
            4700m,
            target.CurrentHealth
        );

        Assert.Equal(
            3,
            run.Result.Timeline.Count(
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.Damage
            )
        );

        Assert.Single(
            run.Result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                    CombatEventType.AbilityChannelStarted
        );

        Assert.Single(
            run.Result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                    CombatEventType.AbilityChannelCompleted
        );

        Assert.DoesNotContain(
            run.Result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                    CombatEventType.AbilityChannelTick
        );

        Assert.Null(
            source.CurrentCastExecutionId
        );
    }

    [Fact]
    public void Channel_LocksActorForCastPlusChannelDuration()
    {
        var source =
            CreateActor(
                "caster",
                "raid"
            );

        var target =
            CreateActor(
                "target",
                "enemy"
            );

        source.AddAbility(
            CreateDamageChannel(
                "cast-then-channel",
                durationSeconds:
                    3m,
                tickIntervalSeconds:
                    1m,
                damagePerTick:
                    10m,
                castTimeSeconds:
                    2m
            )
        );

        var context =
            CreateContext(
                source.Key,
                durationSeconds:
                    6m
            );

        context.AddActor(
            source
        );

        context.AddActor(
            target
        );

        var executor =
            CreateExecutor();

        var result =
            new SimulationEngine(
                [
                    executor
                ])
                .Run(
                    context,
                    startedContext =>
                    {
                        var useResult =
                            executor.TryStartAbility(
                                startedContext,
                                source.Key,
                                target.Key,
                                "cast-then-channel"
                            );

                        Assert.True(
                            useResult.Success
                        );

                        Assert.Equal(
                            5m,
                            source.CastReadyAtSeconds
                        );

                        Assert.Equal(
                            5m,
                            source.InputReadyAtSeconds
                        );
                    }
                );

        var channelStarted =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.AbilityChannelStarted
            );

        Assert.Equal(
            2m,
            channelStarted.TimeSeconds
        );

        var channelCompleted =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.AbilityChannelCompleted
            );

        Assert.Equal(
            5m,
            channelCompleted.TimeSeconds
        );
    }

    [Fact]
    public void Channel_PaysResourceCostOnceWhenCastCompletes()
    {
        var source =
            CreateActor(
                "caster",
                "raid",
                resourceKey:
                    "mana",
                maximum:
                    100m,
                current:
                    100m
            );

        var target =
            CreateActor(
                "target",
                "enemy"
            );

        var ability =
            CreateDamageChannel(
                "mana-channel",
                durationSeconds:
                    3m,
                tickIntervalSeconds:
                    1m,
                damagePerTick:
                    10m
            );

        ability.ResourceCosts.Add(
            new AbilityResourceCost
            {
                ResourceKey =
                    "mana",

                Amount =
                    20m
            }
        );

        source.AddAbility(
            ability
        );

        var run =
            RunAbility(
                source,
                target,
                durationSeconds:
                    4m
            );

        Assert.Equal(
            80m,
            source.Resources["mana"].Current
        );

        var resourceEvent =
            Assert.Single(
                run.Result.Timeline,
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.ResourceChanged
            );

        Assert.Equal(
            -20m,
            resourceEvent.Amount
        );
    }

    [Fact]
    public void Channel_CancellationStopsFutureTicksAndEmitsChannelCancelled()
    {
        var source =
            CreateActor(
                "caster",
                "raid"
            );

        var target =
            CreateActor(
                "target",
                "enemy"
            );

        source.AddAbility(
            CreateDamageChannel(
                "interruptible-channel",
                durationSeconds:
                    4m,
                tickIntervalSeconds:
                    1m,
                damagePerTick:
                    100m
            )
        );

        var context =
            CreateContext(
                source.Key,
                durationSeconds:
                    5m
            );

        context.AddActor(
            source
        );

        context.AddActor(
            target
        );

        var executor =
            CreateExecutor();

        var cancelDriver =
            new ChannelCancelDriver(
                executor,
                source.Key
            );

        var result =
            new SimulationEngine(
                [
                    executor,
                    cancelDriver
                ])
                .Run(
                    context,
                    startedContext =>
                    {
                        var useResult =
                            executor.TryStartAbility(
                                startedContext,
                                source.Key,
                                target.Key,
                                "interruptible-channel"
                            );

                        Assert.True(
                            useResult.Success
                        );

                        startedContext.ScheduleEvent(
                            new CombatEvent
                            {
                                TimeSeconds =
                                    1.5m,

                                Type =
                                    CombatEventType.RotationDecision,

                                SourceActorKey =
                                    source.Key,

                                IsInternal =
                                    true
                            }
                        );
                    }
                );

        Assert.True(
            cancelDriver.CancelSucceeded
        );

        Assert.Equal(
            4900m,
            target.CurrentHealth
        );

        Assert.Single(
            result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                    CombatEventType.Damage
        );

        Assert.Single(
            result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                    CombatEventType.AbilityChannelCancelled
        );

        Assert.DoesNotContain(
            result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                    CombatEventType.AbilityChannelCompleted
        );

        Assert.Null(
            source.CurrentCastExecutionId
        );
    }

    [Fact]
    public void Channel_CanUseHealingTickEffects()
    {
        var source =
            CreateActor(
                "healer",
                "raid"
            );

        var target =
            CreateActor(
                "ally",
                "raid",
                startingHealth:
                    4000m
            );

        source.AddAbility(
            new AbilityDefinition
            {
                Key =
                    "healing-channel",

                Name =
                    "Healing Channel",

                IsOffGlobalCooldown =
                    true,

                ChannelDurationSeconds =
                    3m,

                ChannelTickIntervalSeconds =
                    1m,

                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key =
                            "healing-channel-tick",

                        EffectType =
                            AbilityEffectTypes.DirectHealing,

                        TargetType =
                            AbilityTargetTypes.Friendly,

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

                        ApplyOnChannelTick =
                            true
                    }
                ]
            }
        );

        var run =
            RunAbility(
                source,
                target,
                durationSeconds:
                    4m
            );

        Assert.Equal(
            4300m,
            target.CurrentHealth
        );

        Assert.Equal(
            3,
            run.Result.Timeline.Count(
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.Healing
            )
        );

        Assert.All(
            run.Result.Timeline
                .Where(
                    combatEvent =>
                        combatEvent.Type ==
                        CombatEventType.Healing
                ),
            combatEvent =>
                Assert.True(
                    combatEvent.IsPeriodic
                )
        );
    }

    [Fact]
    public void Channel_NonTickEffectExecutesOnceAtChannelStart()
    {
        var source =
            CreateActor(
                "caster",
                "raid",
                resourceKey:
                    "mana",
                maximum:
                    100m,
                current:
                    0m
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
                    "mixed-channel",

                Name =
                    "Mixed Channel",

                IsOffGlobalCooldown =
                    true,

                ChannelDurationSeconds =
                    3m,

                ChannelTickIntervalSeconds =
                    1m,

                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key =
                            "initial-resource",

                        EffectType =
                            AbilityEffectTypes.ResourceChange,

                        TargetType =
                            AbilityTargetTypes.Self,

                        ResolutionType =
                            CombatResolutionTypes.AlwaysHits,

                        CanMiss =
                            false,

                        CanCrit =
                            false,

                        ResourceKey =
                            "mana",

                        ResourceChangeOperation =
                            ResourceChangeOperationTypes.Gain,

                        MinimumValue =
                            10m,

                        MaximumValue =
                            10m
                    },

                    new AbilityEffectDefinition
                    {
                        Key =
                            "tick-damage",

                        EffectType =
                            AbilityEffectTypes.DirectDamage,

                        TargetType =
                            AbilityTargetTypes.Enemy,

                        ResolutionType =
                            CombatResolutionTypes.AlwaysHits,

                        MitigationType =
                            DamageMitigationTypes.None,

                        CanMiss =
                            false,

                        CanCrit =
                            false,

                        MinimumValue =
                            25m,

                        MaximumValue =
                            25m,

                        ApplyOnChannelTick =
                            true
                    }
                ]
            }
        );

        var run =
            RunAbility(
                source,
                target,
                durationSeconds:
                    4m
            );

        Assert.Equal(
            10m,
            source.Resources["mana"].Current
        );

        Assert.Single(
            run.Result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                    CombatEventType.ResourceChanged
        );

        Assert.Equal(
            3,
            run.Result.Timeline.Count(
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.Damage
            )
        );
    }

    [Fact]
    public void Validator_RejectsInvalidChannelDefinitions()
    {
        var source =
            CreateActor(
                "caster",
                "raid"
            );

        source.AddAbility(
            new AbilityDefinition
            {
                Key =
                    "invalid-channel",

                Name =
                    "Invalid Channel",

                IsOffGlobalCooldown =
                    true,

                ChannelDurationSeconds =
                    2m,

                ChannelTickIntervalSeconds =
                    3m,

                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key =
                            "invalid-channel-effect",

                        EffectType =
                            AbilityEffectTypes.DirectDamage,

                        TargetType =
                            AbilityTargetTypes.Enemy,

                        ApplyOnChannelTick =
                            true,

                        TravelTimeSeconds =
                            1m,

                        DependsOnEffectKey =
                            "missing"
                    }
                ]
            }
        );

        source.AddAbility(
            new AbilityDefinition
            {
                Key =
                    "not-a-channel",

                Name =
                    "Not A Channel",

                IsOffGlobalCooldown =
                    true,

                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key =
                            "invalid-tick-effect",

                        EffectType =
                            AbilityEffectTypes.DirectDamage,

                        TargetType =
                            AbilityTargetTypes.Enemy,

                        ApplyOnChannelTick =
                            true
                    }
                ]
            }
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

        var exception =
            Assert.Throws<SimulationDefinitionValidationException>(
                () =>
                    new SimulationEngine()
                        .Run(
                            context
                        )
            );

        Assert.Contains(
            exception.Errors,
            error =>
                error.Contains(
                    "channel tick interval cannot exceed",
                    StringComparison.OrdinalIgnoreCase
                )
        );

        Assert.Contains(
            exception.Errors,
            error =>
                error.Contains(
                    "cannot use travel time yet",
                    StringComparison.OrdinalIgnoreCase
                )
        );

        Assert.Contains(
            exception.Errors,
            error =>
                error.Contains(
                    "cannot use effect dependencies yet",
                    StringComparison.OrdinalIgnoreCase
                )
        );

        Assert.Contains(
            exception.Errors,
            error =>
                error.Contains(
                    "configured for channel ticks, but the ability is not channeled",
                    StringComparison.OrdinalIgnoreCase
                )
        );
    }

    private static AbilityDefinition CreateDamageChannel(
        string key,
        decimal durationSeconds,
        decimal tickIntervalSeconds,
        decimal damagePerTick,
        decimal castTimeSeconds = 0m)
    {
        return new AbilityDefinition
        {
            Key =
                key,

            Name =
                key,

            IsOffGlobalCooldown =
                true,

            CastTimeSeconds =
                castTimeSeconds,

            ChannelDurationSeconds =
                durationSeconds,

            ChannelTickIntervalSeconds =
                tickIntervalSeconds,

            Effects =
            [
                new AbilityEffectDefinition
                {
                    Key =
                        $"{key}-tick",

                    EffectType =
                        AbilityEffectTypes.DirectDamage,

                    TargetType =
                        AbilityTargetTypes.Enemy,

                    ResolutionType =
                        CombatResolutionTypes.AlwaysHits,

                    MitigationType =
                        DamageMitigationTypes.None,

                    CanMiss =
                        false,

                    CanCrit =
                        false,

                    MinimumValue =
                        damagePerTick,

                    MaximumValue =
                        damagePerTick,

                    ApplyOnChannelTick =
                        true
                }
            ]
        };
    }

    private static AbilityRunResult RunAbility(
        SimulationActorState source,
        SimulationActorState target,
        decimal durationSeconds)
    {
        var context =
            CreateContext(
                source.Key,
                durationSeconds
            );

        context.AddActor(
            source
        );

        if (!string.Equals(
                source.Key,
                target.Key,
                StringComparison.OrdinalIgnoreCase))
        {
            context.AddActor(
                target
            );
        }

        var executor =
            CreateExecutor();

        var result =
            new SimulationEngine(
                [
                    executor
                ])
                .Run(
                    context,
                    startedContext =>
                    {
                        var useResult =
                            executor.TryStartAbility(
                                startedContext,
                                source.Key,
                                target.Key,
                                source.Abilities.Values
                                    .Single()
                                    .Definition.Key
                            );

                        Assert.True(
                            useResult.Success
                        );
                    }
                );

        return new AbilityRunResult(
            context,
            result
        );
    }

    private static AbilityExecutor CreateExecutor()
    {
        return new AbilityExecutor(
            new AuraManager(),
            new SimpleCombatRollResolver(),
            new RulesetDamageMitigationResolver(
                new CombatRulesetDefinition
                {
                    RulesetKey =
                        "channel-tests",

                    Version =
                        "1"
                }
            )
        );
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

    private static SimulationActorState CreateActor(
        string key,
        string teamKey,
        string? resourceKey = null,
        decimal maximum = 0m,
        decimal current = 0m,
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
                    teamKey,

                Level =
                    20
            };

        actor.InitializeHealth(
            maximumHealth:
                5000m,

            startingHealth:
                startingHealth
        );

        actor.ConfigureActionTiming(
            initialActionDelaySeconds:
                0m,

            inputDelaySeconds:
                0m
        );

        if (!string.IsNullOrWhiteSpace(
                resourceKey))
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

    private sealed class ChannelCancelDriver :
        ICombatEventProcessor
    {
        private readonly AbilityExecutor
            _executor;

        private readonly string
            _sourceActorKey;

        public ChannelCancelDriver(
            AbilityExecutor executor,
            string sourceActorKey)
        {
            _executor =
                executor;

            _sourceActorKey =
                sourceActorKey;
        }

        public bool CancelSucceeded { get; private set; }

        public void Process(
            SimulationContext context,
            CombatEvent combatEvent)
        {
            if (
                combatEvent.Type !=
                    CombatEventType.RotationDecision ||
                combatEvent.TimeSeconds !=
                    1.5m)
            {
                return;
            }

            CancelSucceeded =
                _executor.TryCancelCurrentCast(
                    context,
                    _sourceActorKey
                );
        }
    }

    private sealed record AbilityRunResult(
        SimulationContext Context,
        SimulationRunResult Result);
}
