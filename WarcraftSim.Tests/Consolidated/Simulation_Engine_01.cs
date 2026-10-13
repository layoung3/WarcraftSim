// Consolidated tests for Simulation Engine.
// Original class names are retained for xUnit test filtering.

// ==================== DuplicateRuntimeKeyTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation.Engine;

public sealed class DuplicateRuntimeKeyTests
{
    [Fact]
    public void AddActor_RejectsDuplicateKeysCaseInsensitively()
    {
        var context =
            new SimulationContext(
                new SimulationRunOptions
                {
                    Seed =
                        1,

                    DurationSeconds =
                        10m,

                    PrimaryActorKey =
                        "player"
                }
            );

        context.AddActor(
            CreateActor(
                "Player"
            )
        );

        var exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    context.AddActor(
                        CreateActor(
                            "player"
                        )
                    )
            );

        Assert.Contains(
            "Duplicate simulation actor key",
            exception.Message
        );
    }

    [Fact]
    public void AddResource_RejectsDuplicateKeysCaseInsensitively()
    {
        var actor =
            CreateActor(
                "player"
            );

        actor.AddResource(
            new ResourceState(
                resourceKey:
                    "mana",

                maximum:
                    100m,

                startingValue:
                    100m
            )
        );

        var exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    actor.AddResource(
                        new ResourceState(
                            resourceKey:
                                "MANA",

                            maximum:
                                200m,

                            startingValue:
                                200m
                        )
                    )
            );

        Assert.Contains(
            "Duplicate simulation resource key",
            exception.Message
        );
    }

    [Fact]
    public void AddAbility_RejectsDuplicateKeysCaseInsensitively()
    {
        var actor =
            CreateActor(
                "player"
            );

        actor.AddAbility(
            new AbilityDefinition
            {
                Key =
                    "fireball",

                Name =
                    "Fireball"
            }
        );

        var exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    actor.AddAbility(
                        new AbilityDefinition
                        {
                            Key =
                                "FIREBALL",

                            Name =
                                "Other Fireball"
                        }
                    )
            );

        Assert.Contains(
            "Duplicate simulation ability key",
            exception.Message
        );
    }

    private static SimulationActorState CreateActor(
        string key)
    {
        var actor =
            new SimulationActorState
            {
                Key =
                    key,

                Name =
                    key
            };

        actor.InitializeHealth(
            maximumHealth:
                1000m
        );

        return actor;
    }
}
}

// ==================== EventDispatchTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

public sealed class EventDispatchTests
{
    [Fact]
    public void AbilityGameplayEvents_AreRecordedAndDispatchedExactlyOnce()
    {
        var source =
            CreateActor(
                key:
                    "source",
                name:
                    "Source",
                teamKey:
                    "raid"
            );

        var target =
            CreateActor(
                key:
                    "target",
                name:
                    "Target",
                teamKey:
                    "enemy"
            );

        source.AddResource(
            new ResourceState(
                resourceKey:
                    "mana",

                maximum:
                    100m,

                startingValue:
                    100m,

                regenerationPerSecond:
                    0m
            )
        );

        source.AddAbility(
            new AbilityDefinition
            {
                Key =
                    "test-strike",

                Name =
                    "Test Strike",

                CastTimeSeconds =
                    0m,

                IsOffGlobalCooldown =
                    true,

                ResourceCosts =
                [
                    new AbilityResourceCost
                    {
                        ResourceKey =
                            "mana",

                        Amount =
                            10m
                    }
                ],

                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key =
                            "test-strike-damage",

                        EffectType =
                            AbilityEffectTypes.DirectDamage,

                        ResolutionType =
                            CombatResolutionTypes.AlwaysHits,

                        MitigationType =
                            DamageMitigationTypes.None,

                        CanMiss =
                            false,

                        CanCrit =
                            false,

                        MinimumValue =
                            250m,

                        MaximumValue =
                            250m
                    }
                ]
            }
        );

        var context =
            CreateContext(
                primaryActorKey:
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

        var auraManager =
            new AuraManager();

        var abilityExecutor =
            CreateAbilityExecutor(
                auraManager
            );

        var observer =
            new RecordingEventProcessor();

        var engine =
            new SimulationEngine(
                [
                    abilityExecutor,
                    auraManager,
                    observer
                ]
            );

        var result =
            engine.Run(
                context,
                startedContext =>
                {
                    var startResult =
                        abilityExecutor.TryStartAbility(
                            startedContext,
                            source.Key,
                            target.Key,
                            "test-strike"
                        );

                    Assert.True(
                        startResult.Success
                    );
                }
            );

        Assert.Equal(
            750m,
            target.CurrentHealth
        );

        Assert.Equal(
            90m,
            source.Resources[
                "mana"
            ].Current
        );

        Assert.Single(
            observer.Events,
            combatEvent =>
                combatEvent.Type ==
                CombatEventType.AbilityCastStarted
        );

        Assert.Single(
            observer.Events,
            combatEvent =>
                combatEvent.Type ==
                CombatEventType.ResourceChanged
        );

        Assert.Single(
            observer.Events,
            combatEvent =>
                combatEvent.Type ==
                CombatEventType.Damage
        );

        Assert.Single(
            result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                CombatEventType.Damage
        );

        Assert.Equal(
            250m,
            result.Summary.DamageDone
        );

        Assert.Equal(
            250m,
            result.Summary.ActorSummaries[
                source.Key
            ].DamageDone
        );

        Assert.Equal(
            250m,
            result.Summary.ActorSummaries[
                target.Key
            ].DamageTaken
        );
    }

    [Fact]
    public void AuraAppliedAndRemoved_AreDispatchedToProcessors()
    {
        var target =
            CreateActor(
                key:
                    "target",
                name:
                    "Aura Target",
                teamKey:
                    "raid"
            );

        var context =
            CreateContext(
                primaryActorKey:
                    target.Key,

                durationSeconds:
                    2m
            );

        context.AddActor(
            target
        );

        var auraManager =
            new AuraManager();

        var observer =
            new RecordingEventProcessor();

        var engine =
            new SimulationEngine(
                [
                    auraManager,
                    observer
                ]
            );

        var result =
            engine.Run(
                context,
                startedContext =>
                {
                    auraManager.ApplyAura(
                        startedContext,
                        target,
                        new AuraDefinition
                        {
                            Key =
                                "test-aura",

                            Name =
                                "Test Aura",

                            DurationSeconds =
                                1m,

                            StackingMode =
                                AuraStackingMode.Refresh,

                            MaxStacks =
                                1
                        },
                        sourceActorKey:
                            "source",
                        abilityKey:
                            "test-ability",
                        effectKey:
                            "test-effect",
                        scheduleExpiration:
                            true
                    );
                }
            );

        Assert.Empty(
            target.ActiveAuras
        );

        var applied =
            Assert.Single(
                observer.Events,
                combatEvent =>
                    combatEvent.Type ==
                    CombatEventType.AuraApplied
            );

        var removed =
            Assert.Single(
                observer.Events,
                combatEvent =>
                    combatEvent.Type ==
                    CombatEventType.AuraRemoved
            );

        Assert.Equal(
            0m,
            applied.TimeSeconds
        );

        Assert.Equal(
            1m,
            removed.TimeSeconds
        );

        Assert.Contains(
            result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                CombatEventType.AuraApplied
        );

        Assert.Contains(
            result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                CombatEventType.AuraRemoved
        );

        Assert.DoesNotContain(
            result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                CombatEventType.AuraExpiration
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

        return actor;
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

    private static AbilityExecutor CreateAbilityExecutor(
        AuraManager auraManager)
    {
        return new AbilityExecutor(
            auraManager,
            new SimpleCombatRollResolver(),
            new RulesetDamageMitigationResolver(
                new CombatRulesetDefinition
                {
                    RulesetKey =
                        "event-dispatch-tests",

                    Version =
                        "1"
                }
            )
        );
    }

    private sealed class RecordingEventProcessor :
        ICombatEventProcessor
    {
        public List<CombatEvent> Events { get; } =
            [];

        public void Process(
            SimulationContext context,
            CombatEvent combatEvent)
        {
            Events.Add(
                combatEvent
            );
        }
    }
}
}

// ==================== RotationDecisionCoalescingTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Simulation.Engine;

/// <summary>Ordinary rotation reevaluations are level-triggered; named gameplay
/// ticks remain distinct. Protects large combat batches from multiplying wakeups.</summary>
public sealed class RotationDecisionCoalescingTests
{
    private static CombatEvent Decision(string actor = "warrior", string target = "boss",
        decimal at = 1m, string? effect = null) => new()
    {
        Type = CombatEventType.RotationDecision, TimeSeconds = at,
        SourceActorKey = actor, TargetActorKey = target, EffectKey = effect,
        IsInternal = true
    };

    [Fact]
    public void PendingOrdinaryDecisions_AreCoalescedByActorTargetAndTimestamp()
    {
        var context = new SimulationContext(new SimulationRunOptions { DurationSeconds = 10m });
        context.ScheduleEvent(Decision());
        context.ScheduleEvent(Decision());
        context.ScheduleEvent(Decision());
        context.ScheduleEvent(Decision(target: "second-boss"));
        context.ScheduleEvent(Decision(actor: "second-warrior"));
        context.ScheduleEvent(Decision(effect: "anger-management-tick"));
        context.ScheduleEvent(Decision(effect: "anger-management-tick"));
        var results = new List<CombatEvent>();
        while (context.TryGetNextEvent(out var ev))
            results.Add(ev!);
        Assert.Equal(5, results.Count);
        Assert.Single(results, ev => ev.SourceActorKey == "warrior" &&
            ev.TargetActorKey == "boss" && ev.EffectKey is null);
        Assert.Equal(2, results.Count(ev => ev.EffectKey == "anger-management-tick"));
    }

    [Fact]
    public void DequeuedDecision_CanBeScheduledAgainAtSameTime()
    {
        var context = new SimulationContext(new SimulationRunOptions { DurationSeconds = 10m });
        context.ScheduleEvent(Decision(at: 2m));
        Assert.True(context.TryGetNextEvent(out var first));
        Assert.NotNull(first);
        Assert.Equal(2m, context.CurrentTimeSeconds);
        context.ScheduleEvent(Decision(at: 2m));
        Assert.True(context.TryGetNextEvent(out var second));
        Assert.NotNull(second);
        Assert.Equal(2m, second.TimeSeconds);
        Assert.False(context.TryGetNextEvent(out _));
    }
}
}

// ==================== RotationWakeUpTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation.Engine;

public sealed class RotationWakeUpTests
{
    [Fact]
    public void Rotation_ReevaluatesWhenAuraExpires()
    {
        var player =
            CreateActor(
                "player",
                "Player"
            );

        var target =
            CreateActor(
                "target",
                "Target"
            );

        player.AddAbility(
            CreateInstantAbility(
                "aura-missing-action"
            )
        );

        var rotation =
            CreateRotation(
                "aura-missing-action",
                new RotationConditionDefinition
                {
                    ConditionType =
                        RotationConditionTypes.TargetAuraMissing,

                    Key =
                        "test-dot"
                }
            );

        var auraManager =
            new AuraManager();

        var abilityExecutor =
            CreateAbilityExecutor(
                auraManager
            );

        var rotationExecutor =
            new PriorityRotationExecutor(
                rotation,
                player.Key,
                target.Key,
                abilityExecutor
            );

        var context =
            CreateContext(
                durationSeconds:
                    4m
            );

        context.AddActor(
            player
        );

        context.AddActor(
            target
        );

        var engine =
            new SimulationEngine(
                [
                    abilityExecutor,
                    auraManager,
                    rotationExecutor
                ]
            );

        var result =
            engine.Run(
                context,
                startedContext =>
                {
                    auraManager.ApplyAura(
                        startedContext,
                        target,
                        new AuraDefinition
                        {
                            Key =
                                "test-dot",

                            Name =
                                "Test DoT",

                            DurationSeconds =
                                2m
                        },
                        sourceActorKey:
                            player.Key,
                        abilityKey:
                            null,
                        effectKey:
                            null
                    );
                }
            );

        var firstCast =
            result.Timeline
                .First(
                    combatEvent =>
                        combatEvent.Type ==
                            CombatEventType.AbilityCastStarted &&
                        string.Equals(
                            combatEvent.AbilityKey,
                            "aura-missing-action",
                            StringComparison.OrdinalIgnoreCase
                        )
                );

        Assert.Equal(
            2m,
            firstCast.TimeSeconds
        );
    }

    [Fact]
    public void Rotation_ReevaluatesWhenResourceThresholdIsReached()
    {
        var player =
            CreateActor(
                "player",
                "Player"
            );

        var target =
            CreateActor(
                "target",
                "Target"
            );

        player.AddResource(
            new ResourceState(
                resourceKey:
                    "mana",

                maximum:
                    100m,

                startingValue:
                    0m,

                regenerationPerSecond:
                    10m
            )
        );

        player.AddAbility(
            CreateInstantAbility(
                "resource-action"
            )
        );

        var rotation =
            CreateRotation(
                "resource-action",
                new RotationConditionDefinition
                {
                    ConditionType =
                        RotationConditionTypes.SourceResourceCurrent,

                    ComparisonOperator =
                        RotationComparisonOperators.GreaterThanOrEqual,

                    Key =
                        "mana",

                    Value =
                        50m
                }
            );

        var auraManager =
            new AuraManager();

        var abilityExecutor =
            CreateAbilityExecutor(
                auraManager
            );

        var context =
            CreateContext(
                durationSeconds:
                    6m
            );

        context.AddActor(
            player
        );

        context.AddActor(
            target
        );

        var engine =
            new SimulationEngine(
                [
                    abilityExecutor,
                    auraManager,
                    new PriorityRotationExecutor(
                        rotation,
                        player.Key,
                        target.Key,
                        abilityExecutor
                    )
                ]
            );

        var result =
            engine.Run(
                context
            );

        var firstCast =
            result.Timeline
                .First(
                    combatEvent =>
                        combatEvent.Type ==
                            CombatEventType.AbilityCastStarted &&
                        string.Equals(
                            combatEvent.AbilityKey,
                            "resource-action",
                            StringComparison.OrdinalIgnoreCase
                        )
                );

        Assert.Equal(
            5m,
            firstCast.TimeSeconds
        );
    }

    [Fact]
    public void Rotation_ReevaluatesWhenTargetHealthChanges()
    {
        var player =
            CreateActor(
                "player",
                "Player"
            );

        var target =
            CreateActor(
                "target",
                "Target"
            );

        player.AddAbility(
            CreateInstantAbility(
                "execute-action"
            )
        );

        var rotation =
            CreateRotation(
                "execute-action",
                new RotationConditionDefinition
                {
                    ConditionType =
                        RotationConditionTypes.TargetHealthPercent,

                    ComparisonOperator =
                        RotationComparisonOperators.LessThanOrEqual,

                    Value =
                        50m
                }
            );

        var auraManager =
            new AuraManager();

        var abilityExecutor =
            CreateAbilityExecutor(
                auraManager
            );

        var healthChangeProcessor =
            new ScheduledDamageProcessor(
                targetActorKey:
                    target.Key,

                damageAtSeconds:
                    2m,

                damageAmount:
                    600m
            );

        var context =
            CreateContext(
                durationSeconds:
                    4m
            );

        context.AddActor(
            player
        );

        context.AddActor(
            target
        );

        var engine =
            new SimulationEngine(
                [
                    healthChangeProcessor,
                    abilityExecutor,
                    auraManager,
                    new PriorityRotationExecutor(
                        rotation,
                        player.Key,
                        target.Key,
                        abilityExecutor
                    )
                ]
            );

        var result =
            engine.Run(
                context
            );

        Assert.Equal(
            400m,
            target.CurrentHealth
        );

        var firstCast =
            result.Timeline
                .First(
                    combatEvent =>
                        combatEvent.Type ==
                            CombatEventType.AbilityCastStarted &&
                        string.Equals(
                            combatEvent.AbilityKey,
                            "execute-action",
                            StringComparison.OrdinalIgnoreCase
                        )
                );

        Assert.Equal(
            2m,
            firstCast.TimeSeconds
        );
    }

    private static SimulationActorState CreateActor(
        string key,
        string name)
    {
        var actor =
            new SimulationActorState
            {
                Key =
                    key,

                Name =
                    name,

                TeamKey =
                    key == "player"
                        ? "raid"
                        : "enemy"
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

    private static AbilityDefinition CreateInstantAbility(
        string key)
    {
        return new AbilityDefinition
        {
            Key =
                key,

            Name =
                key,

            CastTimeSeconds =
                0m,

            GlobalCooldownSeconds =
                1.5m
        };
    }

    private static RotationProfile CreateRotation(
        string abilityKey,
        RotationConditionDefinition condition)
    {
        return new RotationProfile
        {
            Name =
                "Wake-up Test Rotation",

            Entries =
            [
                new RotationEntry
                {
                    AbilityKey =
                        abilityKey,

                    Priority =
                        1,

                    Conditions =
                    [
                        condition
                    ]
                }
            ]
        };
    }

    private static SimulationContext CreateContext(
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
                    "player",

                CaptureTimeline =
                    true
            }
        );
    }

    private static AbilityExecutor CreateAbilityExecutor(
        AuraManager auraManager)
    {
        return new AbilityExecutor(
            auraManager,
            new SimpleCombatRollResolver(),
            new RulesetDamageMitigationResolver(
                new CombatRulesetDefinition
                {
                    RulesetKey =
                        "rotation-wakeup-tests",

                    Version =
                        "1"
                }
            )
        );
    }

    private sealed class ScheduledDamageProcessor :
        ICombatEventProcessor
    {
        private readonly string
            _targetActorKey;

        private readonly decimal
            _damageAtSeconds;

        private readonly decimal
            _damageAmount;

        public ScheduledDamageProcessor(
            string targetActorKey,
            decimal damageAtSeconds,
            decimal damageAmount)
        {
            _targetActorKey =
                targetActorKey;

            _damageAtSeconds =
                damageAtSeconds;

            _damageAmount =
                damageAmount;
        }

        public void Process(
            SimulationContext context,
            CombatEvent combatEvent)
        {
            if (
                combatEvent.Type ==
                CombatEventType.SimulationStarted
            )
            {
                context.ScheduleEvent(
                    new CombatEvent
                    {
                        TimeSeconds =
                            _damageAtSeconds,

                        Type =
                            CombatEventType.Damage,

                        SourceActorKey =
                            "test-damage-source",

                        TargetActorKey =
                            _targetActorKey,

                        Amount =
                            _damageAmount,

                        Description =
                            "Scheduled test damage."
                    }
                );

                return;
            }

            if (
                combatEvent.Type !=
                    CombatEventType.Damage ||
                !string.Equals(
                    combatEvent.TargetActorKey,
                    _targetActorKey,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return;
            }

            context.GetActor(
                    _targetActorKey
                )?
                .TakeDamage(
                    _damageAmount
                );
        }
    }
}
}

// ==================== SimulationActorFactoryTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Building;

public sealed class SimulationActorFactoryTests
{
    [Fact]
    public void Create_MapsIdentityHealthRoleAndStats()
    {
        var actor =
            SimulationActorFactory.Create(
                new SimulationActorBuildDefinition
                {
                    Key =
                        "character-1",

                    Name =
                        "Imported Character",

                    TeamKey =
                        "raid",

                    AssignedRole =
                        SimulationType.Tank,

                    Level =
                        70,

                    MaximumHealth =
                        12000m,

                    StartingHealth =
                        9000m,

                    Stats =
                    {
                        ["armor"] =
                            5500m,

                        ["fire-resistance"] =
                            75m
                    }
                }
            );

        Assert.Equal(
            "character-1",
            actor.Key
        );

        Assert.Equal(
            "Imported Character",
            actor.Name
        );

        Assert.Equal(
            "raid",
            actor.TeamKey
        );

        Assert.Equal(
            SimulationType.Tank,
            actor.AssignedRole
        );

        Assert.Equal(
            70,
            actor.Level
        );

        Assert.Equal(
            12000m,
            actor.MaximumHealth
        );

        Assert.Equal(
            9000m,
            actor.CurrentHealth
        );

        Assert.Equal(
            5500m,
            actor.Stats.Get(
                "armor"
            )
        );

        Assert.Equal(
            75m,
            actor.Stats.Get(
                "fire-resistance"
            )
        );
    }

    [Fact]
    public void Create_MapsResourcesAndActionTiming()
    {
        var actor =
            SimulationActorFactory.Create(
                new SimulationActorBuildDefinition
                {
                    Key =
                        "healer",

                    Name =
                        "Imported Healer",

                    TeamKey =
                        "raid",

                    AssignedRole =
                        SimulationType.Healing,

                    MaximumHealth =
                        5000m,

                    InitialActionDelaySeconds =
                        0.25m,

                    InputDelaySeconds =
                        0.1m,

                    Resources =
                    [
                        new SimulationResourceBuildDefinition
                        {
                            ResourceKey =
                                "mana",

                            Maximum =
                                4000m,

                            StartingValue =
                                3000m,

                            RegenerationPerSecond =
                                20m
                        }
                    ]
                }
            );

        Assert.True(
            actor.Resources.ContainsKey(
                "mana"
            )
        );

        var mana =
            actor.Resources[
                "mana"
            ];

        Assert.Equal(
            4000m,
            mana.Maximum
        );

        Assert.Equal(
            3000m,
            mana.Current
        );

        Assert.Equal(
            20m,
            mana.RegenerationPerSecond
        );

        Assert.Equal(
            0.25m,
            actor.InitialActionDelaySeconds
        );

        Assert.Equal(
            0.1m,
            actor.InputDelaySeconds
        );

        Assert.Equal(
            0.25m,
            actor.InputReadyAtSeconds
        );
    }

    [Fact]
    public void Create_MapsAbilityDefinitionsIntoRuntimeAbilityStates()
    {
        var ability =
            new AbilityDefinition
            {
                Key =
                    "test-ability",

                Name =
                    "Test Ability"
            };

        var actor =
            SimulationActorFactory.Create(
                new SimulationActorBuildDefinition
                {
                    Key =
                        "caster",

                    Name =
                        "Imported Caster",

                    TeamKey =
                        "raid",

                    MaximumHealth =
                        4000m,

                    Abilities =
                    [
                        ability
                    ]
                }
            );

        Assert.True(
            actor.Abilities.ContainsKey(
                "test-ability"
            )
        );

        Assert.Same(
            ability,
            actor.Abilities[
                "test-ability"
            ].Definition
        );
    }

    [Fact]
    public void Create_RejectsDuplicateResourceKeys()
    {
        var build =
            new SimulationActorBuildDefinition
            {
                Key =
                    "duplicate-resource",

                Name =
                    "Duplicate Resource",

                MaximumHealth =
                    1000m,

                Resources =
                [
                    new SimulationResourceBuildDefinition
                    {
                        ResourceKey =
                            "mana",

                        Maximum =
                            100m,

                        StartingValue =
                            100m
                    },

                    new SimulationResourceBuildDefinition
                    {
                        ResourceKey =
                            "MANA",

                        Maximum =
                            200m,

                        StartingValue =
                            200m
                    }
                ]
            };

        Assert.Throws<ArgumentException>(
            () =>
                SimulationActorFactory.Create(
                    build
                )
        );
    }
}
}
