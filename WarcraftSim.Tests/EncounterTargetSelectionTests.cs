using WarcraftSim.Core.Encounters;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests;

public sealed class EncounterTargetSelectionTests
{
    [Fact]
    public void AllMatchingActors_HitsEveryMatchingRole()
    {
        var result =
            RunPattern(
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
                }
            );

        var targets =
            GetPatternDamageTargets(
                result
            );

        Assert.Equal(
            3,
            targets.Count
        );

        Assert.Contains(
            "dps-1",
            targets
        );

        Assert.Contains(
            "dps-2",
            targets
        );

        Assert.Contains(
            "dps-3",
            targets
        );

        Assert.DoesNotContain(
            "tank",
            targets
        );

        Assert.DoesNotContain(
            "healer",
            targets
        );
    }

    [Fact]
    public void RandomMatchingActors_CanChooseUniqueNonTanks()
    {
        var result =
            RunPattern(
                new EncounterTargetSelectionDefinition
                {
                    Mode =
                        EncounterTargetSelectionModes.RandomMatchingActors,

                    TeamKey =
                        "raid",

                    ExcludedRoles =
                    [
                        SimulationType.Tank
                    ],

                    Count =
                        3,

                    AllowDuplicateTargets =
                        false
                }
            );

        var targets =
            GetPatternDamageTargets(
                result
            );

        Assert.Equal(
            3,
            targets.Count
        );

        Assert.Equal(
            3,
            targets.Distinct(
                StringComparer.OrdinalIgnoreCase
            ).Count()
        );

        Assert.DoesNotContain(
            "tank",
            targets
        );
    }

    [Fact]
    public void RandomMatchingActors_CanRepeatTheSameTarget()
    {
        var result =
            RunPattern(
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
                        3,

                    AllowDuplicateTargets =
                        true
                }
            );

        var targets =
            GetPatternDamageTargets(
                result
            );

        Assert.Equal(
            3,
            targets.Count
        );

        Assert.All(
            targets,
            target =>
                Assert.Equal(
                    "healer",
                    target
                )
        );
    }

    private static SimulationRunResult RunPattern(
        EncounterTargetSelectionDefinition selection)
    {
        var encounter =
            new EncounterProfile
            {
                Name =
                    "Encounter Target Selection Test",

                RulesetKey =
                    "development",

                DurationSeconds =
                    2m,

                DamagePatterns =
                [
                    new EncounterDamagePatternDefinition
                    {
                        Key =
                            "multi-target-test",

                        Name =
                            "Multi Target Test",

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
                        2m,

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
                "tank",
                "raid",
                SimulationType.Tank
            )
        );

        context.AddActor(
            CreateActor(
                "healer",
                "raid",
                SimulationType.Healing
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

        context.AddActor(
            CreateActor(
                "dps-3",
                "raid",
                SimulationType.Dps
            )
        );

        var mitigationResolver =
            new RulesetDamageMitigationResolver(
                new CombatRulesetDefinition
                {
                    RulesetKey =
                        "development-targeting",

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

    private static List<string> GetPatternDamageTargets(
        SimulationRunResult result)
    {
        return result.Timeline
            .Where(
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.Damage &&
                    string.Equals(
                        combatEvent.AbilityKey,
                        "encounter:multi-target-test",
                        StringComparison.OrdinalIgnoreCase
                    )
            )
            .Select(
                combatEvent =>
                    combatEvent.TargetActorKey
            )
            .Where(
                actorKey =>
                    !string.IsNullOrWhiteSpace(
                        actorKey
                    )
            )
            .Select(
                actorKey =>
                    actorKey!
            )
            .ToList();
    }
}
