// Consolidated tests for Ability Lifecycle.
// Original class names are retained for xUnit test filtering.

// ==================== ChannelingFoundationTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Validation;

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
            {
                Assert.Equal(
                    CombatEffectDeliveryType.ChannelTick,
                    combatEvent.EffectDeliveryType
                );

                Assert.False(
                    combatEvent.IsPeriodic
                );

                Assert.True(
                    combatEvent.IsChannelTick
                );
            }
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
}

// ==================== PeriodicAuraIntegrationTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Validation;

public sealed class PeriodicAuraIntegrationTests
{
    [Fact]
    public void PeriodicEffect_DefaultScheduleExcludesExpirationBoundary()
    {
        var source = CreateActor("caster", "raid");
        var target = CreateActor("target", "enemy");

        source.AddAbility(
            CreatePeriodicDamageAbility(
                "dot",
                6m,
                2m,
                100m
            )
        );

        var result =
            RunAbility(
                source,
                target,
                durationSeconds:
                    7m
            );

        Assert.Equal(
            4800m,
            target.CurrentHealth
        );

        var damageTimes =
            result.Timeline
                .Where(
                    combatEvent =>
                        combatEvent.Type ==
                            CombatEventType.Damage
                )
                .Select(
                    combatEvent =>
                        combatEvent.TimeSeconds
                )
                .ToArray();

        Assert.Equal(
            new[]
            {
                2m,
                4m
            },
            damageTimes
        );
    }

    [Fact]
    public void PeriodicEffect_CanIncludeAlignedExpirationBoundaryTick()
    {
        var source = CreateActor("caster", "raid");
        var target = CreateActor("target", "enemy");

        source.AddAbility(
            CreatePeriodicDamageAbility(
                "boundary-dot",
                6m,
                2m,
                100m,
                includeExpirationBoundaryTick:
                    true
            )
        );

        var result =
            RunAbility(
                source,
                target,
                durationSeconds:
                    7m
            );

        Assert.Equal(
            4700m,
            target.CurrentHealth
        );

        Assert.Equal(
            3,
            result.Timeline.Count(
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.Damage
            )
        );

        Assert.Contains(
            result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                    CombatEventType.Damage &&
                combatEvent.TimeSeconds ==
                    6m
        );
    }

    [Fact]
    public void PeriodicEffect_RefreshInvalidatesOldTicksAndRestartsSchedule()
    {
        var source = CreateActor("caster", "raid");
        var target = CreateActor("target", "enemy");

        source.AddAbility(
            CreatePeriodicDamageAbility(
                "refresh-dot",
                4m,
                1m,
                100m
            )
        );

        var context =
            CreateContext(
                source.Key,
                6m
            );

        context.AddActor(source);
        context.AddActor(target);

        var auraManager =
            new AuraManager();

        var executor =
            CreateExecutor(
                auraManager
            );

        var recastDriver =
            new RecastDriver(
                executor,
                source.Key,
                target.Key,
                "refresh-dot",
                1.5m
            );

        var result =
            new SimulationEngine(
                [
                    executor,
                    auraManager,
                    recastDriver
                ])
                .Run(
                    context,
                    startedContext =>
                    {
                        Assert.True(
                            executor.TryStartAbility(
                                startedContext,
                                source.Key,
                                target.Key,
                                "refresh-dot"
                            ).Success
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
            recastDriver.RecastSucceeded
        );

        var damageTimes =
            result.Timeline
                .Where(
                    combatEvent =>
                        combatEvent.Type ==
                            CombatEventType.Damage
                )
                .Select(
                    combatEvent =>
                        combatEvent.TimeSeconds
                )
                .ToArray();

        Assert.Equal(
            new[]
            {
                1m,
                2.5m,
                3.5m,
                4.5m
            },
            damageTimes
        );
    }

    [Fact]
    public void PeriodicEffect_RemovingAuraCancelsFutureTicks()
    {
        var source = CreateActor("caster", "raid");
        var target = CreateActor("target", "enemy");

        source.AddAbility(
            CreatePeriodicDamageAbility(
                "removable-dot",
                6m,
                1m,
                100m
            )
        );

        var context =
            CreateContext(
                source.Key,
                7m
            );

        context.AddActor(source);
        context.AddActor(target);

        var auraManager =
            new AuraManager();

        var executor =
            CreateExecutor(
                auraManager
            );

        var removalDriver =
            new AuraRemovalDriver(
                auraManager,
                target.Key,
                "removable-dot-aura",
                2.5m
            );

        var result =
            new SimulationEngine(
                [
                    executor,
                    auraManager,
                    removalDriver
                ])
                .Run(
                    context,
                    startedContext =>
                    {
                        Assert.True(
                            executor.TryStartAbility(
                                startedContext,
                                source.Key,
                                target.Key,
                                "removable-dot"
                            ).Success
                        );

                        startedContext.ScheduleEvent(
                            new CombatEvent
                            {
                                TimeSeconds =
                                    2.5m,

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
            removalDriver.RemovedCount >
            0
        );

        Assert.Equal(
            2,
            result.Timeline.Count(
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.Damage
            )
        );

        Assert.Equal(
            4800m,
            target.CurrentHealth
        );
    }

    [Fact]
    public void PeriodicEffect_RuntimeAuraCarriesTimingAndTags()
    {
        var source = CreateActor("caster", "raid");
        var target = CreateActor("target", "enemy");

        var ability =
            CreatePeriodicDamageAbility(
                "tagged-dot",
                8m,
                2m,
                100m
            );

        ability.Effects[0].Tags.Add("bleed");
        ability.Effects[0].Tags.Add("physical-periodic");

        source.AddAbility(ability);

        RunAbility(
            source,
            target,
            durationSeconds:
                1m
        );

        var aura =
            Assert.Single(
                target.ActiveAuras
            );

        Assert.True(
            aura.Definition.IsPeriodic
        );

        Assert.Equal(
            2m,
            aura.Definition
                .PeriodicTickIntervalSeconds
        );

        Assert.False(
            aura.Definition
                .IncludeExpirationBoundaryTick
        );

        Assert.Contains(
            "bleed",
            aura.Definition.Tags
        );

        Assert.Contains(
            "physical-periodic",
            aura.Definition.Tags
        );
    }

    [Fact]
    public void PeriodicHealing_UsesSameAuraLifecycleAndBoundaryPolicy()
    {
        var source = CreateActor("healer", "raid");
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
                Key = "hot",
                Name = "hot",
                IsOffGlobalCooldown = true,
                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key = "hot-effect",
                        EffectType =
                            AbilityEffectTypes.PeriodicHealing,
                        TargetType =
                            AbilityTargetTypes.Friendly,
                        ResolutionType =
                            CombatResolutionTypes.AlwaysHits,
                        CanMiss = false,
                        CanCrit = false,
                        AuraKey = "hot-aura",
                        DurationSeconds = 3m,
                        TickIntervalSeconds = 1m,
                        IncludeExpirationBoundaryTick = true,
                        MinimumValue = 100m,
                        MaximumValue = 100m
                    }
                ]
            }
        );

        var result =
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
            result.Timeline.Count(
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.Healing
            )
        );

        Assert.All(
            result.Timeline
                .Where(
                    combatEvent =>
                        combatEvent.Type ==
                            CombatEventType.Healing
                ),
            combatEvent =>
            {
                Assert.Equal(
                    CombatEffectDeliveryType.Periodic,
                    combatEvent.EffectDeliveryType
                );

                Assert.True(
                    combatEvent.IsPeriodic
                );

                Assert.False(
                    combatEvent.IsChannelTick
                );
            }
        );

        Assert.Empty(
            target.ActiveAuras
        );
    }

    [Fact]
    public void Validator_RejectsInvalidPeriodicDefinitions()
    {
        var source = CreateActor("caster", "raid");

        source.AddAbility(
            new AbilityDefinition
            {
                Key = "invalid-periodics",
                Name = "Invalid Periodics",
                IsOffGlobalCooldown = true,
                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key = "missing-duration",
                        EffectType =
                            AbilityEffectTypes.PeriodicDamage,
                        TargetType =
                            AbilityTargetTypes.Enemy,
                        TickIntervalSeconds = 1m
                    },

                    new AbilityEffectDefinition
                    {
                        Key = "missing-interval",
                        EffectType =
                            AbilityEffectTypes.PeriodicHealing,
                        TargetType =
                            AbilityTargetTypes.Friendly,
                        DurationSeconds = 4m
                    },

                    new AbilityEffectDefinition
                    {
                        Key = "interval-too-long",
                        EffectType =
                            AbilityEffectTypes.PeriodicDamage,
                        TargetType =
                            AbilityTargetTypes.Enemy,
                        DurationSeconds = 2m,
                        TickIntervalSeconds = 3m
                    }
                ]
            }
        );

        var context =
            CreateContext(
                source.Key,
                1m
            );

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
                    "missing-duration",
                    StringComparison.OrdinalIgnoreCase
                ) &&
                error.Contains(
                    "DurationSeconds greater than zero",
                    StringComparison.OrdinalIgnoreCase
                )
        );

        Assert.Contains(
            exception.Errors,
            error =>
                error.Contains(
                    "missing-interval",
                    StringComparison.OrdinalIgnoreCase
                ) &&
                error.Contains(
                    "TickIntervalSeconds greater than zero",
                    StringComparison.OrdinalIgnoreCase
                )
        );

        Assert.Contains(
            exception.Errors,
            error =>
                error.Contains(
                    "interval-too-long",
                    StringComparison.OrdinalIgnoreCase
                ) &&
                error.Contains(
                    "tick interval longer than its duration",
                    StringComparison.OrdinalIgnoreCase
                )
        );
    }

    private static AbilityDefinition CreatePeriodicDamageAbility(
        string key,
        decimal durationSeconds,
        decimal tickIntervalSeconds,
        decimal damagePerTick,
        bool includeExpirationBoundaryTick = false)
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
                    EffectType =
                        AbilityEffectTypes.PeriodicDamage,
                    TargetType =
                        AbilityTargetTypes.Enemy,
                    ResolutionType =
                        CombatResolutionTypes.AlwaysHits,
                    MitigationType =
                        DamageMitigationTypes.None,
                    CanMiss = false,
                    CanCrit = false,
                    AuraKey = $"{key}-aura",
                    DurationSeconds = durationSeconds,
                    TickIntervalSeconds = tickIntervalSeconds,
                    IncludeExpirationBoundaryTick =
                        includeExpirationBoundaryTick,
                    MinimumValue = damagePerTick,
                    MaximumValue = damagePerTick
                }
            ]
        };
    }

    private static SimulationRunResult RunAbility(
        SimulationActorState source,
        SimulationActorState target,
        decimal durationSeconds)
    {
        var context =
            CreateContext(
                source.Key,
                durationSeconds
            );

        context.AddActor(source);

        if (!string.Equals(
                source.Key,
                target.Key,
                StringComparison.OrdinalIgnoreCase))
        {
            context.AddActor(target);
        }

        var auraManager =
            new AuraManager();

        var executor =
            CreateExecutor(
                auraManager
            );

        return new SimulationEngine(
            [
                executor,
                auraManager
            ])
            .Run(
                context,
                startedContext =>
                {
                    Assert.True(
                        executor.TryStartAbility(
                            startedContext,
                            source.Key,
                            target.Key,
                            source.Abilities.Values
                                .Single()
                                .Definition.Key
                        ).Success
                    );
                }
            );
    }

    private static AbilityExecutor CreateExecutor(
        AuraManager auraManager)
    {
        return new AbilityExecutor(
            auraManager,
            new SimpleCombatRollResolver(),
            new RulesetDamageMitigationResolver(
                new CombatRulesetDefinition
                {
                    RulesetKey =
                        "periodic-aura-tests",
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
                Seed = 12345,
                DurationSeconds = durationSeconds,
                PrimaryActorKey = primaryActorKey,
                CaptureTimeline = true
            }
        );
    }

    private static SimulationActorState CreateActor(
        string key,
        string teamKey,
        decimal? startingHealth = null)
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

        return actor;
    }

    private sealed class RecastDriver :
        ICombatEventProcessor
    {
        private readonly AbilityExecutor _executor;
        private readonly string _sourceActorKey;
        private readonly string _targetActorKey;
        private readonly string _abilityKey;
        private readonly decimal _recastAtSeconds;

        public RecastDriver(
            AbilityExecutor executor,
            string sourceActorKey,
            string targetActorKey,
            string abilityKey,
            decimal recastAtSeconds)
        {
            _executor = executor;
            _sourceActorKey = sourceActorKey;
            _targetActorKey = targetActorKey;
            _abilityKey = abilityKey;
            _recastAtSeconds = recastAtSeconds;
        }

        public bool RecastSucceeded { get; private set; }

        public void Process(
            SimulationContext context,
            CombatEvent combatEvent)
        {
            if (
                combatEvent.Type !=
                    CombatEventType.RotationDecision ||
                combatEvent.TimeSeconds !=
                    _recastAtSeconds)
            {
                return;
            }

            RecastSucceeded =
                _executor.TryStartAbility(
                    context,
                    _sourceActorKey,
                    _targetActorKey,
                    _abilityKey
                ).Success;
        }
    }

    private sealed class AuraRemovalDriver :
        ICombatEventProcessor
    {
        private readonly AuraManager _auraManager;
        private readonly string _targetActorKey;
        private readonly string _auraKey;
        private readonly decimal _removeAtSeconds;

        public AuraRemovalDriver(
            AuraManager auraManager,
            string targetActorKey,
            string auraKey,
            decimal removeAtSeconds)
        {
            _auraManager = auraManager;
            _targetActorKey = targetActorKey;
            _auraKey = auraKey;
            _removeAtSeconds = removeAtSeconds;
        }

        public int RemovedCount { get; private set; }

        public void Process(
            SimulationContext context,
            CombatEvent combatEvent)
        {
            if (
                combatEvent.Type !=
                    CombatEventType.RotationDecision ||
                combatEvent.TimeSeconds !=
                    _removeAtSeconds)
            {
                return;
            }

            var target =
                context.GetActor(
                    _targetActorKey
                );

            if (target is null)
            {
                return;
            }

            RemovedCount =
                _auraManager.RemoveAuras(
                    context,
                    target,
                    _auraKey,
                    reason:
                        "test removal"
                );
        }
    }
}
}
