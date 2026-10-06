using WarcraftSim.Core.Encounters;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests;

public sealed class EncounterDamageSequenceTests
{
    [Fact]
    public void SameSelection_HitsOneSelectedActorThreeTimes()
    {
        var result =
            RunSequence(
                new EncounterTargetSelectionDefinition
                {
                    Mode =
                        EncounterTargetSelectionModes.RandomMatchingActors,

                    TeamKey =
                        "raid",

                    AllowedRoles =
                    [
                        SimulationType.Dps
                    ],

                    Count =
                        1,

                    AllowDuplicateTargets =
                        false
                },

                new EncounterDamageSequenceDefinition
                {
                    HitCount =
                        3,

                    HitIntervalSeconds =
                        1m,

                    TargetMode =
                        EncounterDamageSequenceTargetModes.SameSelection
                }
            );

        var hits =
            GetSequenceHits(
                result
            );

        Assert.Equal(
            [
                1m,
                2m,
                3m
            ],
            hits.Select(
                hit =>
                    hit.TimeSeconds
            ).ToList()
        );

        Assert.Single(
            hits.Select(
                    hit =>
                        hit.TargetActorKey
                )
                .Distinct(
                    StringComparer.OrdinalIgnoreCase
                )
        );
    }

    [Fact]
    public void SequentialSelection_HitsThreeUniqueTargetsOnePerSecond()
    {
        var result =
            RunSequence(
                new EncounterTargetSelectionDefinition
                {
                    Mode =
                        EncounterTargetSelectionModes.RandomMatchingActors,

                    TeamKey =
                        "raid",

                    AllowedRoles =
                    [
                        SimulationType.Dps
                    ],

                    Count =
                        3,

                    AllowDuplicateTargets =
                        false
                },

                new EncounterDamageSequenceDefinition
                {
                    HitCount =
                        3,

                    HitIntervalSeconds =
                        1m,

                    TargetMode =
                        EncounterDamageSequenceTargetModes.SequentialSelection
                }
            );

        var hits =
            GetSequenceHits(
                result
            );

        Assert.Equal(
            3,
            hits.Count
        );

        Assert.Equal(
            [
                1m,
                2m,
                3m
            ],
            hits.Select(
                hit =>
                    hit.TimeSeconds
            ).ToList()
        );

        Assert.Equal(
            3,
            hits.Select(
                    hit =>
                        hit.TargetActorKey
                )
                .Distinct(
                    StringComparer.OrdinalIgnoreCase
                )
                .Count()
        );
    }

    [Fact]
    public void ReselectEachHit_CanHitTheSameEligibleActorRepeatedly()
    {
        var result =
            RunSequence(
                new EncounterTargetSelectionDefinition
                {
                    Mode =
                        EncounterTargetSelectionModes.RandomMatchingActors,

                    TeamKey =
                        "raid",

                    AllowedRoles =
                    [
                        SimulationType.Healing
                    ],

                    Count =
                        1,

                    AllowDuplicateTargets =
                        true
                },

                new EncounterDamageSequenceDefinition
                {
                    HitCount =
                        3,

                    HitIntervalSeconds =
                        0.5m,

                    TargetMode =
                        EncounterDamageSequenceTargetModes.ReselectEachHit
                }
            );

        var hits =
            GetSequenceHits(
                result
            );

        Assert.Equal(
            [
                1m,
                1.5m,
                2m
            ],
            hits.Select(
                hit =>
                    hit.TimeSeconds
            ).ToList()
        );

        Assert.All(
            hits,
            hit =>
                Assert.Equal(
                    "healer",
                    hit.TargetActorKey
                )
        );
    }

    private static SimulationRunResult RunSequence(
        EncounterTargetSelectionDefinition selection,
        EncounterDamageSequenceDefinition sequence)
    {
        var encounter =
            new EncounterProfile
            {
                Name =
                    "Encounter Damage Sequence Test",

                RulesetKey =
                    "development",

                DurationSeconds =
                    4m,

                DamagePatterns =
                [
                    new EncounterDamagePatternDefinition
                    {
                        Key =
                            "sequence-test",

                        Name =
                            "Sequence Test",

                        StartTimeSeconds =
                            1m,

                        EndTimeSeconds =
                            1m,

                        IntervalSeconds =
                            1m,

                        SourceActorKey =
                            "boss",

                        TargetSelection =
                            selection,

                        Sequence =
                            sequence,

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

        context.AddActor(CreateActor("boss", "enemy", null));
        context.AddActor(CreateActor("tank", "raid", SimulationType.Tank));
        context.AddActor(CreateActor("healer", "raid", SimulationType.Healing));
        context.AddActor(CreateActor("dps-1", "raid", SimulationType.Dps));
        context.AddActor(CreateActor("dps-2", "raid", SimulationType.Dps));
        context.AddActor(CreateActor("dps-3", "raid", SimulationType.Dps));

        var mitigationResolver =
            new RulesetDamageMitigationResolver(
                new CombatRulesetDefinition
                {
                    RulesetKey =
                        "development-sequence",

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

    private static List<CombatEvent> GetSequenceHits(
        SimulationRunResult result)
    {
        return result.Timeline
            .Where(
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.Damage &&
                    string.Equals(
                        combatEvent.AbilityKey,
                        "encounter:sequence-test",
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
