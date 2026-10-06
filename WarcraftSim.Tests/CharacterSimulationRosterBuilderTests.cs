using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Building;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests;

public sealed class CharacterSimulationRosterBuilderTests
{
    [Fact]
    public void Build_CreatesMultipleCharacterActorsInRequestOrder()
    {
        var builder =
            CreateRosterBuilder();

        var tank =
            CreateProfile(
                id:
                    "1d6b1c9a-3497-49bd-a069-13e89defb60b",

                name:
                    "Tank"
            );

        var healer =
            CreateProfile(
                id:
                    "93081b1a-68f5-46d3-abbb-c86af862ba48",

                name:
                    "Healer"
            );

        var result =
            builder.Build(
                new CharacterSimulationRosterBuildRequest
                {
                    Key =
                        "guild-raid",

                    Name =
                        "Guild Raid",

                    Members =
                    [
                        CreateMember(
                            tank,
                            actorKey:
                                "tank-1",

                            assignedRole:
                                SimulationType.Tank
                        ),

                        CreateMember(
                            healer,
                            actorKey:
                                "healer-1",

                            assignedRole:
                                SimulationType.Healing
                        )
                    ]
                }
            );

        Assert.Equal(
            "guild-raid",
            result.Key
        );

        Assert.Equal(
            "Guild Raid",
            result.Name
        );

        Assert.Equal(
            2,
            result.Members.Count
        );

        Assert.Same(
            tank,
            result.Members[0].Profile
        );

        Assert.Same(
            healer,
            result.Members[1].Profile
        );

        Assert.Equal(
            "tank-1",
            result.Members[0].Actor.Key
        );

        Assert.Equal(
            SimulationType.Tank,
            result.Members[0].Actor.AssignedRole
        );

        Assert.Equal(
            "healer-1",
            result.Members[1].Actor.Key
        );

        Assert.Equal(
            SimulationType.Healing,
            result.Members[1].Actor.AssignedRole
        );
    }

    [Fact]
    public void Build_UsesProfileIdAsActorKeyWhenNoOverrideExists()
    {
        var profile =
            CreateProfile(
                id:
                    "9e38630e-8e05-411f-8493-f26522048b6c",

                name:
                    "Default Key"
            );

        var result =
            CreateRosterBuilder()
                .Build(
                    new CharacterSimulationRosterBuildRequest
                    {
                        Members =
                        [
                            CreateMember(
                                profile
                            )
                        ]
                    }
                );

        var member =
            Assert.Single(
                result.Members
            );

        Assert.Equal(
            profile.Id.ToString("N"),
            member.Actor.Key
        );
    }

    [Fact]
    public void Build_RejectsDuplicateActorKeysCaseInsensitively()
    {
        var builder =
            CreateRosterBuilder();

        var exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    builder.Build(
                        new CharacterSimulationRosterBuildRequest
                        {
                            Members =
                            [
                                CreateMember(
                                    CreateProfile(
                                        id:
                                            "392a20f1-f085-476e-a6d9-e5f181232086",

                                        name:
                                            "First"
                                    ),
                                    actorKey:
                                        "Player-One"
                                ),

                                CreateMember(
                                    CreateProfile(
                                        id:
                                            "5c78ad6c-4333-41e2-a494-a075668a4ed4",

                                        name:
                                            "Second"
                                    ),
                                    actorKey:
                                        "PLAYER-ONE"
                                )
                            ]
                        }
                    )
            );

        Assert.Contains(
            "duplicate actor key",
            exception.Message,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void Build_RejectsEmptyRoster()
    {
        var exception =
            Assert.Throws<ArgumentException>(
                () =>
                    CreateRosterBuilder()
                        .Build(
                            new CharacterSimulationRosterBuildRequest()
                        )
            );

        Assert.Contains(
            "at least one member",
            exception.Message,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void AddActorsTo_AddsEntireRosterToSimulationContext()
    {
        var result =
            CreateRosterBuilder()
                .Build(
                    new CharacterSimulationRosterBuildRequest
                    {
                        Members =
                        [
                            CreateMember(
                                CreateProfile(
                                    id:
                                        "92a19a45-4990-4bd2-8993-71a25bc9a6c8",

                                    name:
                                        "Tank"
                                ),
                                actorKey:
                                    "tank"
                            ),

                            CreateMember(
                                CreateProfile(
                                    id:
                                        "e86d4595-51ac-43f3-83dd-02fcf97e8272",

                                    name:
                                        "Damage"
                                ),
                                actorKey:
                                    "damage",
                                assignedRole:
                                    SimulationType.Dps
                            )
                        ]
                    }
                );

        var context =
            new SimulationContext(
                new SimulationRunOptions
                {
                    Seed =
                        12345,

                    DurationSeconds =
                        60m,

                    PrimaryActorKey =
                        "tank"
                }
            );

        result.AddActorsTo(
            context
        );

        Assert.Equal(
            2,
            context.Actors.Count
        );

        Assert.Same(
            result.Members[0].Actor,
            context.GetActor(
                "tank"
            )
        );

        Assert.Same(
            result.Members[1].Actor,
            context.GetActor(
                "damage"
            )
        );
    }

    [Fact]
    public void AddActorsTo_DetectsConflictBeforeAddingAnyRosterMembers()
    {
        var result =
            CreateRosterBuilder()
                .Build(
                    new CharacterSimulationRosterBuildRequest
                    {
                        Members =
                        [
                            CreateMember(
                                CreateProfile(
                                    id:
                                        "ce376eaa-a1fc-4b6a-bf1e-9b32438cd004",

                                    name:
                                        "First"
                                ),
                                actorKey:
                                    "first"
                            ),

                            CreateMember(
                                CreateProfile(
                                    id:
                                        "9ba2c907-55d6-44bb-afdb-b50d62bc2d91",

                                    name:
                                        "Second"
                                ),
                                actorKey:
                                    "second"
                            )
                        ]
                    }
                );

        var context =
            new SimulationContext(
                new SimulationRunOptions
                {
                    Seed =
                        12345,

                    DurationSeconds =
                        60m,

                    PrimaryActorKey =
                        "existing"
                }
            );

        var existing =
            new SimulationActorState
            {
                Key =
                    "SECOND",

                Name =
                    "Existing Actor",

                TeamKey =
                    "raid"
            };

        existing.InitializeHealth(
            maximumHealth:
                1000m
        );

        context.AddActor(
            existing
        );

        Assert.Throws<InvalidOperationException>(
            () =>
                result.AddActorsTo(
                    context
                )
        );

        Assert.Single(
            context.Actors
        );

        Assert.Null(
            context.GetActor(
                "first"
            )
        );

        Assert.Same(
            existing,
            context.GetActor(
                "second"
            )
        );
    }

    private static CharacterSimulationRosterBuilder
        CreateRosterBuilder()
    {
        var classDefinition =
            new CharacterSimulationClassDefinition
            {
                RulesetKey =
                    "development",

                ClassKey =
                    "warrior",

                Resources =
                [
                    new SimulationResourceBuildDefinition
                    {
                        ResourceKey =
                            "rage",

                        Maximum =
                            100m,

                        StartingValue =
                            0m
                    }
                ],

                Abilities =
                [
                    new AbilityDefinition
                    {
                        Key =
                            "strike",

                        Name =
                            "Strike"
                    }
                ]
            };

        return new CharacterSimulationRosterBuilder(
            new CharacterSimulationActorBuilder(
                new CharacterSimulationClassCatalog(
                    [
                        classDefinition
                    ]
                )
            )
        );
    }

    private static CharacterSimulationBuildRequest CreateMember(
        CharacterProfile profile,
        string? actorKey = null,
        SimulationType? assignedRole = null)
    {
        return new CharacterSimulationBuildRequest
        {
            Profile =
                profile,

            Options =
                new CharacterSimulationMappingOptions
                {
                    ActorKey =
                        actorKey,

                    TeamKey =
                        "raid",

                    AssignedRole =
                        assignedRole,

                    MaximumHealth =
                        10000m
                }
        };
    }

    private static CharacterProfile CreateProfile(
        string id,
        string name)
    {
        return new CharacterProfile
        {
            Id =
                Guid.Parse(
                    id
                ),

            Name =
                name,

            RulesetKey =
                "development",

            ClassKey =
                "warrior",

            SpecializationKey =
                "protection",

            Level =
                70
        };
    }
}
