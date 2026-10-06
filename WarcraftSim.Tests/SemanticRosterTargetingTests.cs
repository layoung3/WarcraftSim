using WarcraftSim.Core.Encounters;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests;

public sealed class SemanticRosterTargetingTests
{
    [Fact]
    public void SemanticSelector_CanFindTankEnemyWithoutHardcodedTeamKey()
    {
        var context =
            CreateContext();

        var boss =
            AddActor(
                context,
                "boss",
                "enemy",
                null
            );

        var tank =
            AddActor(
                context,
                "tank",
                "raid",
                SimulationType.Tank
            );

        AddActor(
            context,
            "healer",
            "raid",
            SimulationType.Healing
        );

        var targets =
            SimulationActorSemanticSelector
                .ResolveMatching(
                    context,
                    boss,
                    SimulationActorRelationshipTypes.Enemy,
                    allowedRoles:
                        [
                            SimulationType.Tank
                        ]
                );

        var selected =
            Assert.Single(
                targets
            );

        Assert.Same(
            tank,
            selected
        );
    }

    [Fact]
    public void EncounterSelector_CanTargetAllNonTanksByRelationship()
    {
        var context =
            CreateContext();

        AddActor(
            context,
            "boss",
            "enemy",
            null
        );

        AddActor(
            context,
            "tank",
            "raid",
            SimulationType.Tank
        );

        AddActor(
            context,
            "healer",
            "raid",
            SimulationType.Healing
        );

        AddActor(
            context,
            "damage",
            "raid",
            SimulationType.Dps
        );

        var pattern =
            new EncounterDamagePatternDefinition
            {
                SourceActorKey =
                    "boss",

                TargetSelection =
                    new EncounterTargetSelectionDefinition
                    {
                        Mode =
                            EncounterTargetSelectionModes.AllMatchingActors,

                        Relationship =
                            SimulationActorRelationshipTypes.Enemy,

                        ExcludedRoles =
                        [
                            SimulationType.Tank
                        ]
                    }
            };

        var targets =
            EncounterTargetSelector.Resolve(
                context,
                pattern
            );

        Assert.Equal(
            new[]
            {
                "damage",
                "healer"
            },
            targets
                .Select(
                    target =>
                        target.Key
                )
                .ToArray()
        );
    }

    [Fact]
    public void EncounterSelector_CanTargetTankByRoleWithoutActorKey()
    {
        var context =
            CreateContext();

        AddActor(
            context,
            "boss",
            "enemy",
            null
        );

        var tank =
            AddActor(
                context,
                "tank",
                "raid",
                SimulationType.Tank
            );

        AddActor(
            context,
            "damage",
            "raid",
            SimulationType.Dps
        );

        var pattern =
            new EncounterDamagePatternDefinition
            {
                SourceActorKey =
                    "boss",

                TargetSelection =
                    new EncounterTargetSelectionDefinition
                    {
                        Mode =
                            EncounterTargetSelectionModes.AllMatchingActors,

                        Relationship =
                            SimulationActorRelationshipTypes.Enemy,

                        AllowedRoles =
                        [
                            SimulationType.Tank
                        ]
                    }
            };

        var selected =
            Assert.Single(
                EncounterTargetSelector.Resolve(
                    context,
                    pattern
                )
            );

        Assert.Same(
            tank,
            selected
        );
    }

    [Fact]
    public void Rotation_FirstMatchingActor_CanSelectTankAllyWithoutActorKey()
    {
        var context =
            CreateContext();

        var healer =
            AddActor(
                context,
                "healer",
                "raid",
                SimulationType.Healing
            );

        var tank =
            AddActor(
                context,
                "tank",
                "raid",
                SimulationType.Tank
            );

        AddActor(
            context,
            "damage",
            "raid",
            SimulationType.Dps
        );

        var target =
            RotationTargetSelector.Resolve(
                context,
                healer,
                new RotationEntry
                {
                    AbilityKey =
                        "test",

                    Target =
                        new RotationTargetDefinition
                        {
                            Mode =
                                RotationTargetSelectionModes.FirstMatchingActor,

                            Relationship =
                                SimulationActorRelationshipTypes.Ally,

                            IncludeSelf =
                                false,

                            AllowedRoles =
                            [
                                SimulationType.Tank
                            ]
                        }
                },
                defaultTargetKey:
                    "unused"
            );

        Assert.Same(
            tank,
            target
        );
    }

    [Fact]
    public void Rotation_LowestHealthMatchingActor_CanExcludeTank()
    {
        var context =
            CreateContext();

        var healer =
            AddActor(
                context,
                "healer",
                "raid",
                SimulationType.Healing
            );

        var tank =
            AddActor(
                context,
                "tank",
                "raid",
                SimulationType.Tank
            );

        var damageA =
            AddActor(
                context,
                "damage-a",
                "raid",
                SimulationType.Dps
            );

        var damageB =
            AddActor(
                context,
                "damage-b",
                "raid",
                SimulationType.Dps
            );

        tank.TakeDamage(
            900m
        );

        damageA.TakeDamage(
            600m
        );

        damageB.TakeDamage(
            300m
        );

        var target =
            RotationTargetSelector.Resolve(
                context,
                healer,
                new RotationEntry
                {
                    AbilityKey =
                        "test",

                    Target =
                        new RotationTargetDefinition
                        {
                            Mode =
                                RotationTargetSelectionModes.LowestHealthMatchingActor,

                            Relationship =
                                SimulationActorRelationshipTypes.Ally,

                            IncludeSelf =
                                false,

                            ExcludedRoles =
                            [
                                SimulationType.Tank
                            ]
                        }
                },
                defaultTargetKey:
                    "unused"
            );

        Assert.Same(
            damageA,
            target
        );
    }

    [Fact]
    public void Rotation_WatchesActor_RecognizesSemanticCandidates()
    {
        var context =
            CreateContext();

        var healer =
            AddActor(
                context,
                "healer",
                "raid",
                SimulationType.Healing
            );

        AddActor(
            context,
            "tank",
            "raid",
            SimulationType.Tank
        );

        AddActor(
            context,
            "damage",
            "raid",
            SimulationType.Dps
        );

        var entries =
            new[]
            {
                new RotationEntry
                {
                    AbilityKey =
                        "test",

                    Target =
                        new RotationTargetDefinition
                        {
                            Mode =
                                RotationTargetSelectionModes.LowestHealthMatchingActor,

                            Relationship =
                                SimulationActorRelationshipTypes.Ally,

                            IncludeSelf =
                                false,

                            AllowedRoles =
                            [
                                SimulationType.Dps
                            ]
                        }
                }
            };

        Assert.True(
            RotationTargetSelector.WatchesActor(
                context,
                healer,
                entries,
                defaultTargetKey:
                    "unused",
                actorKey:
                    "damage"
            )
        );

        Assert.False(
            RotationTargetSelector.WatchesActor(
                context,
                healer,
                entries,
                defaultTargetKey:
                    "unused",
                actorKey:
                    "tank"
            )
        );
    }

    [Fact]
    public void SemanticSelector_RejectsUnknownRelationship()
    {
        var context =
            CreateContext();

        var source =
            AddActor(
                context,
                "source",
                "raid",
                SimulationType.Dps
            );

        var exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    SimulationActorSemanticSelector
                        .ResolveMatching(
                            context,
                            source,
                            "sometimes-friendly"
                        )
            );

        Assert.Contains(
            "Unknown simulation actor relationship",
            exception.Message
        );
    }

    private static SimulationContext CreateContext()
    {
        return new SimulationContext(
            new SimulationRunOptions
            {
                Seed =
                    12345,

                DurationSeconds =
                    60m,

                PrimaryActorKey =
                    "boss"
            }
        );
    }

    private static SimulationActorState AddActor(
        SimulationContext context,
        string key,
        string teamKey,
        SimulationType? role)
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
                    role
            };

        actor.InitializeHealth(
            maximumHealth:
                1000m
        );

        context.AddActor(
            actor
        );

        return actor;
    }
}
