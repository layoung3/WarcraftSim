using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Building;

namespace WarcraftSim.Tests;

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
