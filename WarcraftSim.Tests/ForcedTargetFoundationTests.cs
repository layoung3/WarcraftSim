using WarcraftSim.Core.Encounters;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests;

public sealed class ForcedTargetFoundationTests
{
    [Fact]
    public void ApplyForcedTarget_SetsRuntimeStateAndEmitsAppliedEvent()
    {
        var context =
            CreateContext(
                durationSeconds:
                    5m,

                captureTimeline:
                    true
            );

        var boss =
            AddActor(
                context,
                "boss",
                "enemy"
            );

        var tank =
            AddActor(
                context,
                "tank",
                "raid"
            );

        var manager =
            new ForcedTargetManager();

        var result =
            new SimulationEngine(
                [
                    manager
                ]
            )
            .Run(
                context,
                startedContext =>
                {
                    manager.ApplyForcedTarget(
                        startedContext,
                        boss,
                        tank,
                        durationSeconds:
                            3m,

                        abilityKey:
                            "test-taunt"
                    );
                }
            );

        Assert.Null(
            boss.ForcedTarget
        );

        var applied =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.ForcedTargetApplied
            );

        Assert.Equal(
            tank.Key,
            applied.SourceActorKey
        );

        Assert.Equal(
            boss.Key,
            applied.TargetActorKey
        );

        Assert.Equal(
            tank.Key,
            applied.ForcedTargetActorKey
        );

        Assert.Equal(
            "test-taunt",
            applied.AbilityKey
        );

        Assert.DoesNotContain(
            result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                    CombatEventType.ForcedTargetExpiration
        );
    }

    [Fact]
    public void HighestThreatTargeting_ActiveForcedTargetOverridesThreatLeader()
    {
        var context =
            CreateContext();

        var boss =
            AddActor(
                context,
                "boss",
                "enemy"
            );

        var tank =
            AddActor(
                context,
                "tank",
                "raid"
            );

        var damage =
            AddActor(
                context,
                "damage",
                "raid"
            );

        boss.ThreatTable.AddThreat(
            tank.Key,
            100m
        );

        boss.ThreatTable.AddThreat(
            damage.Key,
            500m
        );

        new ForcedTargetManager()
            .ApplyForcedTarget(
                context,
                boss,
                tank,
                durationSeconds:
                    5m
            );

        var selected =
            Assert.Single(
                EncounterTargetSelector.Resolve(
                    context,
                    CreateThreatPattern()
                )
            );

        Assert.Same(
            tank,
            selected
        );
    }

    [Fact]
    public void HighestThreatTargeting_DeadForcedTargetFallsBackToThreatLeader()
    {
        var context =
            CreateContext();

        var boss =
            AddActor(
                context,
                "boss",
                "enemy"
            );

        var tank =
            AddActor(
                context,
                "tank",
                "raid"
            );

        var damage =
            AddActor(
                context,
                "damage",
                "raid"
            );

        boss.ThreatTable.AddThreat(
            tank.Key,
            100m
        );

        boss.ThreatTable.AddThreat(
            damage.Key,
            500m
        );

        new ForcedTargetManager()
            .ApplyForcedTarget(
                context,
                boss,
                tank,
                durationSeconds:
                    5m
            );

        tank.TakeDamage(
            tank.MaximumHealth
        );

        var selected =
            Assert.Single(
                EncounterTargetSelector.Resolve(
                    context,
                    CreateThreatPattern()
                )
            );

        Assert.Same(
            damage,
            selected
        );
    }

    [Fact]
    public void ForcedTargetExpiration_ReturnsTargetingToThreatLeader()
    {
        var encounter =
            new EncounterProfile
            {
                Name =
                    "Forced Target Expiration Test",

                RulesetKey =
                    "development",

                DurationSeconds =
                    4m,

                DamagePatterns =
                [
                    new EncounterDamagePatternDefinition
                    {
                        Key =
                            "melee",

                        Name =
                            "Melee",

                        StartTimeSeconds =
                            1m,

                        EndTimeSeconds =
                            3m,

                        IntervalSeconds =
                            2m,

                        SourceActorKey =
                            "boss",

                        TargetSelection =
                            new EncounterTargetSelectionDefinition
                            {
                                Mode =
                                    EncounterTargetSelectionModes.HighestThreatActor,

                                Relationship =
                                    SimulationActorRelationshipTypes.Enemy
                            },

                        Amount =
                            100m,

                        SchoolKey =
                            "physical"
                    }
                ]
            };

        var context =
            new SimulationContext(
                new SimulationRunOptions
                {
                    Seed =
                        12345,

                    DurationSeconds =
                        4m,

                    PrimaryActorKey =
                        "boss",

                    CaptureTimeline =
                        true
                },
                encounter
            );

        var boss =
            AddActor(
                context,
                "boss",
                "enemy"
            );

        var tank =
            AddActor(
                context,
                "tank",
                "raid"
            );

        var damage =
            AddActor(
                context,
                "damage",
                "raid"
            );

        boss.ThreatTable.AddThreat(
            tank.Key,
            100m
        );

        boss.ThreatTable.AddThreat(
            damage.Key,
            500m
        );

        var forcedTargetManager =
            new ForcedTargetManager();

        var mitigationResolver =
            new RulesetDamageMitigationResolver(
                new CombatRulesetDefinition
                {
                    RulesetKey =
                        "forced-target-tests",

                    Version =
                        "1"
                }
            );

        var result =
            new SimulationEngine(
                [
                    new EncounterTimelineProcessor(),
                    new ScriptedEncounterDamageProcessor(
                        mitigationResolver
                    ),
                    forcedTargetManager
                ]
            )
            .Run(
                context,
                startedContext =>
                {
                    forcedTargetManager.ApplyForcedTarget(
                        startedContext,
                        boss,
                        tank,
                        durationSeconds:
                            2m
                    );
                }
            );

        var damageEvents =
            result.Timeline
                .Where(
                    combatEvent =>
                        combatEvent.Type ==
                            CombatEventType.Damage &&
                        string.Equals(
                            combatEvent.AbilityKey,
                            "encounter:melee",
                            StringComparison.OrdinalIgnoreCase
                        )
                )
                .OrderBy(
                    combatEvent =>
                        combatEvent.TimeSeconds
                )
                .ToArray();

        Assert.Equal(
            2,
            damageEvents.Length
        );

        Assert.Equal(
            1m,
            damageEvents[0].TimeSeconds
        );

        Assert.Equal(
            tank.Key,
            damageEvents[0].TargetActorKey
        );

        Assert.Equal(
            3m,
            damageEvents[1].TimeSeconds
        );

        Assert.Equal(
            damage.Key,
            damageEvents[1].TargetActorKey
        );

        Assert.Single(
            result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                    CombatEventType.ForcedTargetRemoved
        );
    }

    [Fact]
    public void ReapplyingForcedTarget_IgnoresStaleExpirationEvent()
    {
        var context =
            CreateContext(
                durationSeconds:
                    2m
            );

        var boss =
            AddActor(
                context,
                "boss",
                "enemy"
            );

        var firstTank =
            AddActor(
                context,
                "tank-a",
                "raid"
            );

        var secondTank =
            AddActor(
                context,
                "tank-b",
                "raid"
            );

        var manager =
            new ForcedTargetManager();

        var reapplyDriver =
            new ReapplyForcedTargetDriver(
                manager,
                boss,
                secondTank
            );

        new SimulationEngine(
            [
                manager,
                reapplyDriver
            ])
            .Run(
                context,
                startedContext =>
                {
                    manager.ApplyForcedTarget(
                        startedContext,
                        boss,
                        firstTank,
                        durationSeconds:
                            1m
                    );
                }
            );

        Assert.NotNull(
            boss.ForcedTarget
        );

        Assert.Equal(
            secondTank.Key,
            boss.ForcedTarget!.ForcedTargetActorKey
        );

        Assert.True(
            boss.ForcedTarget.ExpiresAtSeconds >
            context.Options.DurationSeconds
        );
    }

    [Fact]
    public void RemoveForcedTarget_ClearsOverrideAndEmitsRemovedEvent()
    {
        var context =
            CreateContext(
                durationSeconds:
                    2m,

                captureTimeline:
                    true
            );

        var boss =
            AddActor(
                context,
                "boss",
                "enemy"
            );

        var tank =
            AddActor(
                context,
                "tank",
                "raid"
            );

        var manager =
            new ForcedTargetManager();

        var result =
            new SimulationEngine(
                [
                    manager
                ]
            )
            .Run(
                context,
                startedContext =>
                {
                    manager.ApplyForcedTarget(
                        startedContext,
                        boss,
                        tank,
                        durationSeconds:
                            5m
                    );

                    Assert.True(
                        manager.RemoveForcedTarget(
                            startedContext,
                            boss,
                            "cancelled"
                        )
                    );
                }
            );

        Assert.Null(
            boss.ForcedTarget
        );

        var removed =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.ForcedTargetRemoved
            );

        Assert.Equal(
            tank.Key,
            removed.ForcedTargetActorKey
        );

        Assert.DoesNotContain(
            result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                    CombatEventType.ForcedTargetExpiration
        );
    }

    [Fact]
    public void ApplyForcedTarget_RejectsNonPositiveDuration()
    {
        var context =
            CreateContext();

        var boss =
            AddActor(
                context,
                "boss",
                "enemy"
            );

        var tank =
            AddActor(
                context,
                "tank",
                "raid"
            );

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new ForcedTargetManager()
                    .ApplyForcedTarget(
                        context,
                        boss,
                        tank,
                        durationSeconds:
                            0m
                    )
        );
    }

    private static EncounterDamagePatternDefinition CreateThreatPattern()
    {
        return new EncounterDamagePatternDefinition
        {
            Key =
                "threat-target",

            Name =
                "Threat Target",

            SourceActorKey =
                "boss",

            TargetSelection =
                new EncounterTargetSelectionDefinition
                {
                    Mode =
                        EncounterTargetSelectionModes.HighestThreatActor,

                    Relationship =
                        SimulationActorRelationshipTypes.Enemy
                }
        };
    }

    private static SimulationContext CreateContext(
        decimal durationSeconds = 10m,
        bool captureTimeline = false)
    {
        return new SimulationContext(
            new SimulationRunOptions
            {
                Seed =
                    12345,

                DurationSeconds =
                    durationSeconds,

                PrimaryActorKey =
                    "boss",

                CaptureTimeline =
                    captureTimeline
            }
        );
    }

    private static SimulationActorState AddActor(
        SimulationContext context,
        string key,
        string teamKey)
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
                5000m
        );

        context.AddActor(
            actor
        );

        return actor;
    }

    private sealed class ReapplyForcedTargetDriver :
        ICombatEventProcessor
    {
        private readonly ForcedTargetManager
            _manager;

        private readonly SimulationActorState
            _targetOwner;

        private readonly SimulationActorState
            _forcedTarget;

        public ReapplyForcedTargetDriver(
            ForcedTargetManager manager,
            SimulationActorState targetOwner,
            SimulationActorState forcedTarget)
        {
            _manager =
                manager;

            _targetOwner =
                targetOwner;

            _forcedTarget =
                forcedTarget;
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
                            0.5m,

                        Type =
                            CombatEventType.RotationDecision,

                        SourceActorKey =
                            _targetOwner.Key,

                        IsInternal =
                            true,

                        Description =
                            "Reapply forced target."
                    }
                );

                return;
            }

            if (
                combatEvent.Type ==
                    CombatEventType.RotationDecision &&
                combatEvent.TimeSeconds ==
                    0.5m
            )
            {
                _manager.ApplyForcedTarget(
                    context,
                    _targetOwner,
                    _forcedTarget,
                    durationSeconds:
                        5m
                );
            }
        }
    }
}
