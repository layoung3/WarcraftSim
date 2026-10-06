using WarcraftSim.Core.Encounters;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests;

public sealed class EncounterLiveSequenceTargetingTests
{
    [Fact]
    public void ReselectEachHit_UsesOnlyActorsAliveAtThatHitTime()
    {
        var result =
            RunSequence(
                targetMode:
                    EncounterDamageSequenceTargetModes.ReselectEachHit,

                targetSelection:
                    new EncounterTargetSelectionDefinition
                    {
                        Mode =
                            EncounterTargetSelectionModes.AllMatchingActors,

                        TeamKey =
                            "raid",

                        AllowedRoles =
                        [
                            SimulationType.Dps
                        ]
                    },

                lockedActorKey:
                    null
            );

        var sequenceHits =
            GetSequenceDamage(
                result
            );

        var firstHitTargets =
            sequenceHits
                .Where(hit =>
                    hit.TimeSeconds == 1m)
                .Select(hit =>
                    hit.TargetActorKey)
                .ToList();

        var secondHitTargets =
            sequenceHits
                .Where(hit =>
                    hit.TimeSeconds == 2m)
                .Select(hit =>
                    hit.TargetActorKey)
                .ToList();

        Assert.Contains(
            "dps-1",
            firstHitTargets
        );

        Assert.Contains(
            "dps-2",
            firstHitTargets
        );

        // DPS 1 is killed at 1.5 seconds.
        Assert.DoesNotContain(
            "dps-1",
            secondHitTargets
        );

        Assert.Contains(
            "dps-2",
            secondHitTargets
        );
    }

    [Fact]
    public void SameSelection_DoesNotRetargetAfterLockedActorDies()
    {
        var result =
            RunSequence(
                targetMode:
                    EncounterDamageSequenceTargetModes.SameSelection,

                targetSelection:
                    new EncounterTargetSelectionDefinition
                    {
                        Mode =
                            EncounterTargetSelectionModes.FixedActor,

                        ActorKey =
                            "dps-1",

                        TeamKey =
                            "raid"
                    },

                lockedActorKey:
                    "dps-1"
            );

        var sequenceHits =
            GetSequenceDamage(
                result
            );

        Assert.Single(
            sequenceHits
        );

        Assert.Equal(
            1m,
            sequenceHits[0].TimeSeconds
        );

        Assert.Equal(
            "dps-1",
            sequenceHits[0].TargetActorKey
        );

        Assert.DoesNotContain(
            sequenceHits,
            hit =>
                string.Equals(
                    hit.TargetActorKey,
                    "dps-2",
                    StringComparison.OrdinalIgnoreCase
                )
        );
    }

    private static SimulationRunResult RunSequence(
        string targetMode,
        EncounterTargetSelectionDefinition targetSelection,
        string? lockedActorKey)
    {
        var encounter =
            new EncounterProfile
            {
                Name =
                    "Live Sequence Targeting Test",

                RulesetKey =
                    "development",

                DurationSeconds =
                    3m,

                DamageEvents =
                [
                    new EncounterDamageEventDefinition
                    {
                        Key =
                            "kill-dps-1",

                        Name =
                            "Kill DPS One",

                        TimeSeconds =
                            1.5m,

                        SourceActorKey =
                            "boss",

                        TargetActorKey =
                            "dps-1",

                        Amount =
                            10000m,

                        SchoolKey =
                            "shadow"
                    }
                ],

                DamagePatterns =
                [
                    new EncounterDamagePatternDefinition
                    {
                        Key =
                            "live-sequence",

                        Name =
                            "Live Sequence",

                        StartTimeSeconds =
                            1m,

                        EndTimeSeconds =
                            1m,

                        IntervalSeconds =
                            1m,

                        SourceActorKey =
                            "boss",

                        TargetActorKey =
                            lockedActorKey ?? "",

                        TargetSelection =
                            targetSelection,

                        Sequence =
                            new EncounterDamageSequenceDefinition
                            {
                                HitCount =
                                    2,

                                HitIntervalSeconds =
                                    1m,

                                TargetMode =
                                    targetMode
                            },

                        Amount =
                            100m,

                        SchoolKey =
                            "arcane"
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
                        encounter.DurationSeconds,

                    PrimaryActorKey =
                        "boss",

                    CaptureTimeline =
                        true
                },
                encounter
            );

        context.AddActor(
            CreateActor(
                "boss",
                "enemy",
                null
            )
        );

        context.AddActor(
            CreateActor(
                "dps-1",
                "raid",
                SimulationType.Dps
            )
        );

        context.AddActor(
            CreateActor(
                "dps-2",
                "raid",
                SimulationType.Dps
            )
        );

        var mitigationResolver =
            new RulesetDamageMitigationResolver(
                new CombatRulesetDefinition
                {
                    RulesetKey =
                        "development-live-targeting",

                    Version =
                        "1"
                }
            );

        var engine =
            new SimulationEngine(
                [
                    new EncounterTimelineProcessor(),

                    new ScriptedEncounterDamageProcessor(
                        mitigationResolver
                    )
                ]
            );

        return engine.Run(
            context
        );
    }

    private static SimulationActorState CreateActor(
        string key,
        string teamKey,
        SimulationType? assignedRole)
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

                AssignedRole =
                    assignedRole,

                Level =
                    20
            };

        actor.InitializeHealth(
            5000m
        );

        return actor;
    }

    private static List<CombatEvent> GetSequenceDamage(
        SimulationRunResult result)
    {
        return result.Timeline
            .Where(
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.Damage &&
                    string.Equals(
                        combatEvent.AbilityKey,
                        "encounter:live-sequence",
                        StringComparison.OrdinalIgnoreCase
                    )
            )
            .OrderBy(
                combatEvent =>
                    combatEvent.TimeSeconds
            )
            .ToList();
    }
}
