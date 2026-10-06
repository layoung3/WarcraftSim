using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Building;

namespace WarcraftSim.Tests;

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
