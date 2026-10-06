using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests;

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
