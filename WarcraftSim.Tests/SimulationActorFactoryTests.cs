using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Building;

namespace WarcraftSim.Tests;

public sealed class SimulationActorFactoryTests
{
    [Fact]
    public void Create_MapsIdentityHealthRoleAndStats()
    {
        var actor =
            SimulationActorFactory.Create(
                new SimulationActorBuildDefinition
                {
                    Key =
                        "character-1",

                    Name =
                        "Imported Character",

                    TeamKey =
                        "raid",

                    AssignedRole =
                        SimulationType.Tank,

                    Level =
                        70,

                    MaximumHealth =
                        12000m,

                    StartingHealth =
                        9000m,

                    Stats =
                    {
                        ["armor"] =
                            5500m,

                        ["fire-resistance"] =
                            75m
                    }
                }
            );

        Assert.Equal(
            "character-1",
            actor.Key
        );

        Assert.Equal(
            "Imported Character",
            actor.Name
        );

        Assert.Equal(
            "raid",
            actor.TeamKey
        );

        Assert.Equal(
            SimulationType.Tank,
            actor.AssignedRole
        );

        Assert.Equal(
            70,
            actor.Level
        );

        Assert.Equal(
            12000m,
            actor.MaximumHealth
        );

        Assert.Equal(
            9000m,
            actor.CurrentHealth
        );

        Assert.Equal(
            5500m,
            actor.Stats.Get(
                "armor"
            )
        );

        Assert.Equal(
            75m,
            actor.Stats.Get(
                "fire-resistance"
            )
        );
    }

    [Fact]
    public void Create_MapsResourcesAndActionTiming()
    {
        var actor =
            SimulationActorFactory.Create(
                new SimulationActorBuildDefinition
                {
                    Key =
                        "healer",

                    Name =
                        "Imported Healer",

                    TeamKey =
                        "raid",

                    AssignedRole =
                        SimulationType.Healing,

                    MaximumHealth =
                        5000m,

                    InitialActionDelaySeconds =
                        0.25m,

                    InputDelaySeconds =
                        0.1m,

                    Resources =
                    [
                        new SimulationResourceBuildDefinition
                        {
                            ResourceKey =
                                "mana",

                            Maximum =
                                4000m,

                            StartingValue =
                                3000m,

                            RegenerationPerSecond =
                                20m
                        }
                    ]
                }
            );

        Assert.True(
            actor.Resources.ContainsKey(
                "mana"
            )
        );

        var mana =
            actor.Resources[
                "mana"
            ];

        Assert.Equal(
            4000m,
            mana.Maximum
        );

        Assert.Equal(
            3000m,
            mana.Current
        );

        Assert.Equal(
            20m,
            mana.RegenerationPerSecond
        );

        Assert.Equal(
            0.25m,
            actor.InitialActionDelaySeconds
        );

        Assert.Equal(
            0.1m,
            actor.InputDelaySeconds
        );

        Assert.Equal(
            0.25m,
            actor.InputReadyAtSeconds
        );
    }

    [Fact]
    public void Create_MapsAbilityDefinitionsIntoRuntimeAbilityStates()
    {
        var ability =
            new AbilityDefinition
            {
                Key =
                    "test-ability",

                Name =
                    "Test Ability"
            };

        var actor =
            SimulationActorFactory.Create(
                new SimulationActorBuildDefinition
                {
                    Key =
                        "caster",

                    Name =
                        "Imported Caster",

                    TeamKey =
                        "raid",

                    MaximumHealth =
                        4000m,

                    Abilities =
                    [
                        ability
                    ]
                }
            );

        Assert.True(
            actor.Abilities.ContainsKey(
                "test-ability"
            )
        );

        Assert.Same(
            ability,
            actor.Abilities[
                "test-ability"
            ].Definition
        );
    }

    [Fact]
    public void Create_RejectsDuplicateResourceKeys()
    {
        var build =
            new SimulationActorBuildDefinition
            {
                Key =
                    "duplicate-resource",

                Name =
                    "Duplicate Resource",

                MaximumHealth =
                    1000m,

                Resources =
                [
                    new SimulationResourceBuildDefinition
                    {
                        ResourceKey =
                            "mana",

                        Maximum =
                            100m,

                        StartingValue =
                            100m
                    },

                    new SimulationResourceBuildDefinition
                    {
                        ResourceKey =
                            "MANA",

                        Maximum =
                            200m,

                        StartingValue =
                            200m
                    }
                ]
            };

        Assert.Throws<ArgumentException>(
            () =>
                SimulationActorFactory.Create(
                    build
                )
        );
    }
}
