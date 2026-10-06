using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Building;
using WarcraftSim.Core.Stats;

namespace WarcraftSim.Tests;

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
