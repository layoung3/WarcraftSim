// Consolidated tests for Characters Foundation.
// Original class names are retained for xUnit test filtering.

// ==================== CharacterSimulationActorBuilderTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Building;
using WarcraftSim.Core.Stats;

public sealed class CharacterSimulationActorBuilderTests
{
    [Fact]
    public void Build_ResolvesExactDefinitionAndCreatesRuntimeActor()
    {
        var classWide =
            CreateDefinition(
                specializationKey:
                    null,
                abilityKey:
                    "class-wide-ability"
            );

        var protection =
            CreateDefinition(
                specializationKey:
                    "protection",
                abilityKey:
                    "shield-slam"
            );

        var builder =
            CreateBuilder(
                classWide,
                protection
            );

        var profile =
            CreateProfile(
                specializationKey:
                    "protection"
            );

        profile.BaseStats.Set(
            "armor",
            4800m
        );

        var result =
            builder.Build(
                new CharacterSimulationBuildRequest
                {
                    Profile =
                        profile,

                    Options =
                        new CharacterSimulationMappingOptions
                        {
                            ActorKey =
                                "tank-1",

                            TeamKey =
                                "raid",

                            AssignedRole =
                                SimulationType.Tank,

                            MaximumHealth =
                                14000m,

                            StartingHealth =
                                12000m
                        }
                }
            );

        Assert.Same(
            protection,
            result.ClassDefinition
        );

        Assert.Equal(
            "tank-1",
            result.BuildDefinition.Key
        );

        Assert.Equal(
            4800m,
            result.BuildDefinition.Stats[
                "armor"
            ]
        );

        Assert.Equal(
            14000m,
            result.Actor.MaximumHealth
        );

        Assert.Equal(
            12000m,
            result.Actor.CurrentHealth
        );

        Assert.Equal(
            SimulationType.Tank,
            result.Actor.AssignedRole
        );

        Assert.True(
            result.Actor.Resources.ContainsKey(
                "rage"
            )
        );

        Assert.True(
            result.Actor.Abilities.ContainsKey(
                "shield-slam"
            )
        );

        Assert.False(
            result.Actor.Abilities.ContainsKey(
                "class-wide-ability"
            )
        );
    }

    [Fact]
    public void Build_UsesClassWideDefinitionWhenNoExactSpecExists()
    {
        var classWide =
            CreateDefinition(
                specializationKey:
                    null,
                abilityKey:
                    "class-wide-ability"
            );

        var builder =
            CreateBuilder(
                classWide
            );

        var result =
            builder.Build(
                new CharacterSimulationBuildRequest
                {
                    Profile =
                        CreateProfile(
                            specializationKey:
                                "arms"
                        ),

                    Options =
                        new CharacterSimulationMappingOptions
                        {
                            MaximumHealth =
                                9000m
                        }
                }
            );

        Assert.Same(
            classWide,
            result.ClassDefinition
        );

        Assert.True(
            result.Actor.Abilities.ContainsKey(
                "class-wide-ability"
            )
        );
    }

    [Fact]
    public void Build_UsesEffectiveStatsFromMappingOptions()
    {
        var builder =
            CreateBuilder(
                CreateDefinition(
                    specializationKey:
                        "protection",
                    abilityKey:
                        "shield-slam"
                )
            );

        var profile =
            CreateProfile(
                specializationKey:
                    "protection"
            );

        profile.BaseStats.Set(
            "armor",
            1000m
        );

        var effectiveStats =
            new StatCollection();

        effectiveStats.Set(
            "armor",
            6200m
        );

        effectiveStats.Set(
            "fire-resistance",
            80m
        );

        var result =
            builder.Build(
                new CharacterSimulationBuildRequest
                {
                    Profile =
                        profile,

                    Options =
                        new CharacterSimulationMappingOptions
                        {
                            MaximumHealth =
                                15000m,

                            EffectiveStats =
                                effectiveStats
                        }
                }
            );

        Assert.Equal(
            6200m,
            result.BuildDefinition.Stats[
                "armor"
            ]
        );

        Assert.Equal(
            80m,
            result.Actor.Stats.Get(
                "fire-resistance"
            )
        );
    }

    [Fact]
    public void Build_ThrowsWhenCatalogCannotResolveCharacter()
    {
        var builder =
            CreateBuilder(
                CreateDefinition(
                    specializationKey:
                        "protection",
                    abilityKey:
                        "shield-slam"
                )
            );

        var profile =
            CreateProfile(
                specializationKey:
                    "arms"
            );

        Assert.Throws<InvalidOperationException>(
            () =>
                builder.Build(
                    new CharacterSimulationBuildRequest
                    {
                        Profile =
                            profile,

                        Options =
                            new CharacterSimulationMappingOptions
                            {
                                MaximumHealth =
                                    9000m
                            }
                    }
                )
        );
    }

    private static CharacterSimulationActorBuilder CreateBuilder(
        params CharacterSimulationClassDefinition[] definitions)
    {
        return new CharacterSimulationActorBuilder(
            new CharacterSimulationClassCatalog(
                definitions
            )
        );
    }

    private static CharacterProfile CreateProfile(
        string? specializationKey)
    {
        return new CharacterProfile
        {
            Id =
                Guid.Parse(
                    "16e23faf-7f2d-464d-8f9b-b00e59fc0856"
                ),

            Name =
                "Pipeline Warrior",

            RulesetKey =
                "development",

            ClassKey =
                "warrior",

            SpecializationKey =
                specializationKey,

            Level =
                70
        };
    }

    private static CharacterSimulationClassDefinition CreateDefinition(
        string? specializationKey,
        string abilityKey)
    {
        return new CharacterSimulationClassDefinition
        {
            RulesetKey =
                "development",

            ClassKey =
                "warrior",

            SpecializationKey =
                specializationKey,

            Resources =
            [
                new SimulationResourceBuildDefinition
                {
                    ResourceKey =
                        "rage",

                    Maximum =
                        100m,

                    StartingValue =
                        0m,

                    RegenerationPerSecond =
                        0m
                }
            ],

            Abilities =
            [
                new AbilityDefinition
                {
                    Key =
                        abilityKey,

                    Name =
                        abilityKey
                }
            ]
        };
    }
}
}

// ==================== CharacterSimulationClassCatalogTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Building;

public sealed class CharacterSimulationClassCatalogTests
{
    [Fact]
    public void Resolve_PrefersExactSpecializationOverClassWideDefinition()
    {
        var classWide =
            CreateDefinition(
                specializationKey:
                    null,
                abilityKey:
                    "class-wide-ability"
            );

        var protection =
            CreateDefinition(
                specializationKey:
                    "protection",
                abilityKey:
                    "protection-ability"
            );

        var catalog =
            new CharacterSimulationClassCatalog(
                [
                    classWide,
                    protection
                ]
            );

        var resolved =
            catalog.Resolve(
                CreateProfile(
                    specializationKey:
                        "protection"
                )
            );

        Assert.Same(
            protection,
            resolved
        );
    }

    [Fact]
    public void Resolve_FallsBackToClassWideDefinition()
    {
        var classWide =
            CreateDefinition(
                specializationKey:
                    null,
                abilityKey:
                    "class-wide-ability"
            );

        var catalog =
            new CharacterSimulationClassCatalog(
                [
                    classWide
                ]
            );

        var resolved =
            catalog.Resolve(
                CreateProfile(
                    specializationKey:
                        "holy"
                )
            );

        Assert.Same(
            classWide,
            resolved
        );
    }

    [Fact]
    public void Resolve_ThrowsWhenNoMatchingDefinitionExists()
    {
        var catalog =
            new CharacterSimulationClassCatalog(
                [
                    CreateDefinition(
                        specializationKey:
                            "protection",
                        abilityKey:
                            "protection-ability"
                    )
                ]
            );

        Assert.Throws<InvalidOperationException>(
            () =>
                catalog.Resolve(
                    CreateProfile(
                        specializationKey:
                            "holy"
                    )
                )
        );
    }

    [Fact]
    public void Constructor_RejectsDuplicateDefinitionKeys()
    {
        var first =
            CreateDefinition(
                specializationKey:
                    "holy",
                abilityKey:
                    "holy-light"
            );

        var duplicate =
            CreateDefinition(
                specializationKey:
                    "HOLY",
                abilityKey:
                    "flash-of-light"
            );

        Assert.Throws<ArgumentException>(
            () =>
                new CharacterSimulationClassCatalog(
                    [
                        first,
                        duplicate
                    ]
                )
        );
    }

    [Fact]
    public void Mapper_UsesCatalogToBuildRuntimeActor()
    {
        var classWide =
            CreateDefinition(
                specializationKey:
                    null,
                abilityKey:
                    "class-wide-ability"
            );

        var holy =
            CreateDefinition(
                specializationKey:
                    "holy",
                abilityKey:
                    "holy-light"
            );

        var catalog =
            new CharacterSimulationClassCatalog(
                [
                    classWide,
                    holy
                ]
            );

        var actor =
            CharacterProfileSimulationMapper
                .ToActor(
                    CreateProfile(
                        specializationKey:
                            "holy"
                    ),
                    new CharacterSimulationMappingOptions
                    {
                        ActorKey =
                            "healer-1",

                        TeamKey =
                            "raid",

                        AssignedRole =
                            SimulationType.Healing,

                        MaximumHealth =
                            6500m
                    },
                    catalog
                );

        Assert.Equal(
            "healer-1",
            actor.Key
        );

        Assert.True(
            actor.Resources.ContainsKey(
                "mana"
            )
        );

        Assert.True(
            actor.Abilities.ContainsKey(
                "holy-light"
            )
        );

        Assert.False(
            actor.Abilities.ContainsKey(
                "class-wide-ability"
            )
        );
    }

    private static CharacterProfile CreateProfile(
        string? specializationKey)
    {
        return new CharacterProfile
        {
            Id =
                Guid.Parse(
                    "3c3d615f-1274-4fa7-807f-80923840452f"
                ),

            Name =
                "Catalog Paladin",

            RulesetKey =
                "development",

            ClassKey =
                "paladin",

            SpecializationKey =
                specializationKey,

            Level =
                70
        };
    }

    private static CharacterSimulationClassDefinition CreateDefinition(
        string? specializationKey,
        string abilityKey)
    {
        return new CharacterSimulationClassDefinition
        {
            RulesetKey =
                "development",

            ClassKey =
                "paladin",

            SpecializationKey =
                specializationKey,

            Resources =
            [
                new SimulationResourceBuildDefinition
                {
                    ResourceKey =
                        "mana",

                    Maximum =
                        5000m,

                    StartingValue =
                        5000m,

                    RegenerationPerSecond =
                        20m
                }
            ],

            Abilities =
            [
                new AbilityDefinition
                {
                    Key =
                        abilityKey,

                    Name =
                        abilityKey
                }
            ]
        };
    }
}
}

// ==================== CharacterSimulationClassMappingTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Building;

public sealed class CharacterSimulationClassMappingTests
{
    [Fact]
    public void ToBuildDefinition_AddsClassResourcesAndAbilities()
    {
        var profile =
            CreateProfile(
                specializationKey:
                    "holy"
            );

        var definition =
            CreateClassDefinition(
                specializationKey:
                    "holy"
            );

        var build =
            CharacterProfileSimulationMapper
                .ToBuildDefinition(
                    profile,
                    new CharacterSimulationMappingOptions
                    {
                        MaximumHealth =
                            6000m
                    },
                    definition
                );

        Assert.Single(
            build.Resources
        );

        Assert.Equal(
            "mana",
            build.Resources[0].ResourceKey
        );

        Assert.Equal(
            5000m,
            build.Resources[0].Maximum
        );

        Assert.Equal(
            5000m,
            build.Resources[0].StartingValue
        );

        Assert.Equal(
            25m,
            build.Resources[0].RegenerationPerSecond
        );

        Assert.Single(
            build.Abilities
        );

        Assert.Equal(
            "holy-light",
            build.Abilities[0].Key
        );
    }

    [Fact]
    public void ToActor_CreatesRuntimeResourceAndAbilityStates()
    {
        var actor =
            CharacterProfileSimulationMapper
                .ToActor(
                    CreateProfile(
                        specializationKey:
                            "holy"
                    ),
                    new CharacterSimulationMappingOptions
                    {
                        ActorKey =
                            "healer-1",

                        TeamKey =
                            "raid",

                        AssignedRole =
                            SimulationType.Healing,

                        MaximumHealth =
                            6000m
                    },
                    CreateClassDefinition(
                        specializationKey:
                            "holy"
                    )
                );

        Assert.True(
            actor.Resources.ContainsKey(
                "mana"
            )
        );

        Assert.Equal(
            5000m,
            actor.Resources[
                "mana"
            ].Maximum
        );

        Assert.True(
            actor.Abilities.ContainsKey(
                "holy-light"
            )
        );

        Assert.Equal(
            "Holy Light",
            actor.Abilities[
                "holy-light"
            ].Definition.Name
        );
    }

    [Fact]
    public void ClassWideDefinition_CanMapACharacterWithSpecialization()
    {
        var build =
            CharacterProfileSimulationMapper
                .ToBuildDefinition(
                    CreateProfile(
                        specializationKey:
                            "protection"
                    ),
                    new CharacterSimulationMappingOptions
                    {
                        MaximumHealth =
                            9000m
                    },
                    CreateClassDefinition(
                        specializationKey:
                            null
                    )
                );

        Assert.Single(
            build.Resources
        );

        Assert.Single(
            build.Abilities
        );
    }

    [Theory]
    [InlineData(
        "other-ruleset",
        "paladin",
        "holy"
    )]
    [InlineData(
        "development",
        "warrior",
        "holy"
    )]
    [InlineData(
        "development",
        "paladin",
        "protection"
    )]
    public void ToBuildDefinition_RejectsMismatchedClassDefinition(
        string definitionRuleset,
        string definitionClass,
        string? definitionSpecialization)
    {
        var profile =
            CreateProfile(
                specializationKey:
                    "holy"
            );

        var definition =
            CreateClassDefinition(
                specializationKey:
                    definitionSpecialization
            );

        definition.RulesetKey =
            definitionRuleset;

        definition.ClassKey =
            definitionClass;

        Assert.Throws<InvalidOperationException>(
            () =>
                CharacterProfileSimulationMapper
                    .ToBuildDefinition(
                        profile,
                        new CharacterSimulationMappingOptions
                        {
                            MaximumHealth =
                                6000m
                        },
                        definition
                    )
        );
    }

    private static CharacterProfile CreateProfile(
        string? specializationKey)
    {
        return new CharacterProfile
        {
            Id =
                Guid.Parse(
                    "f244dc08-4800-4d30-9224-bd932928d840"
                ),

            Name =
                "Test Paladin",

            RulesetKey =
                "development",

            ClassKey =
                "paladin",

            SpecializationKey =
                specializationKey,

            Level =
                70
        };
    }

    private static CharacterSimulationClassDefinition
        CreateClassDefinition(
            string? specializationKey)
    {
        return new CharacterSimulationClassDefinition
        {
            RulesetKey =
                "development",

            ClassKey =
                "paladin",

            SpecializationKey =
                specializationKey,

            Resources =
            [
                new SimulationResourceBuildDefinition
                {
                    ResourceKey =
                        "mana",

                    Maximum =
                        5000m,

                    StartingValue =
                        5000m,

                    RegenerationPerSecond =
                        25m
                }
            ],

            Abilities =
            [
                new AbilityDefinition
                {
                    Key =
                        "holy-light",

                    Name =
                        "Holy Light",

                    CastTimeSeconds =
                        2.5m,

                    GlobalCooldownSeconds =
                        1.5m
                }
            ]
        };
    }
}
}

// ==================== CharacterSimulationRosterBuilderTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Building;
using WarcraftSim.Core.Simulation.Engine;

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
}
