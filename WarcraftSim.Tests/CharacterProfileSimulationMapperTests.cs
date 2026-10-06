using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Building;
using WarcraftSim.Core.Stats;

namespace WarcraftSim.Tests;

public sealed class CharacterProfileSimulationMapperTests
{
    [Fact]
    public void ToBuildDefinition_MapsCharacterIdentityLevelAndBaseStats()
    {
        var id =
            Guid.Parse(
                "4cc9830d-2b47-47b0-9b3a-a6e544d5f56d"
            );

        var profile =
            new CharacterProfile
            {
                Id =
                    id,

                Name =
                    "Mapped Character",

                RulesetKey =
                    "development",

                ClassKey =
                    "test-class",

                SpecializationKey =
                    "test-spec",

                Level =
                    70
            };

        profile.BaseStats.Set(
            "armor",
            4200m
        );

        profile.BaseStats.Set(
            "spell-power",
            650m
        );

        var build =
            CharacterProfileSimulationMapper
                .ToBuildDefinition(
                    profile,
                    new CharacterSimulationMappingOptions
                    {
                        MaximumHealth =
                            9000m
                    }
                );

        Assert.Equal(
            id.ToString("N"),
            build.Key
        );

        Assert.Equal(
            "Mapped Character",
            build.Name
        );

        Assert.Equal(
            70,
            build.Level
        );

        Assert.Equal(
            4200m,
            build.Stats[
                "armor"
            ]
        );

        Assert.Equal(
            650m,
            build.Stats[
                "spell-power"
            ]
        );
    }

    [Fact]
    public void ToBuildDefinition_UsesEffectiveStatsWhenProvided()
    {
        var profile =
            CreateProfile();

        profile.BaseStats.Set(
            "armor",
            1000m
        );

        var effectiveStats =
            new StatCollection();

        effectiveStats.Set(
            "armor",
            5500m
        );

        effectiveStats.Set(
            "fire-resistance",
            75m
        );

        var build =
            CharacterProfileSimulationMapper
                .ToBuildDefinition(
                    profile,
                    new CharacterSimulationMappingOptions
                    {
                        MaximumHealth =
                            12000m,

                        EffectiveStats =
                            effectiveStats
                    }
                );

        Assert.Equal(
            5500m,
            build.Stats[
                "armor"
            ]
        );

        Assert.Equal(
            75m,
            build.Stats[
                "fire-resistance"
            ]
        );
    }

    [Fact]
    public void ToActor_MapsRuntimeRoleHealthTeamAndTiming()
    {
        var profile =
            CreateProfile();

        var actor =
            CharacterProfileSimulationMapper
                .ToActor(
                    profile,
                    new CharacterSimulationMappingOptions
                    {
                        ActorKey =
                            "raid-tank-1",

                        TeamKey =
                            "raid",

                        AssignedRole =
                            SimulationType.Tank,

                        MaximumHealth =
                            15000m,

                        StartingHealth =
                            11000m,

                        InitialActionDelaySeconds =
                            0.2m,

                        InputDelaySeconds =
                            0.1m
                    }
                );

        Assert.Equal(
            "raid-tank-1",
            actor.Key
        );

        Assert.Equal(
            "Profile Character",
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
            15000m,
            actor.MaximumHealth
        );

        Assert.Equal(
            11000m,
            actor.CurrentHealth
        );

        Assert.Equal(
            0.2m,
            actor.InitialActionDelaySeconds
        );

        Assert.Equal(
            0.1m,
            actor.InputDelaySeconds
        );
    }

    [Fact]
    public void ToBuildDefinition_RejectsMissingRuntimeHealth()
    {
        var profile =
            CreateProfile();

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                CharacterProfileSimulationMapper
                    .ToBuildDefinition(
                        profile,
                        new CharacterSimulationMappingOptions()
                    )
        );
    }

    private static CharacterProfile CreateProfile()
    {
        return new CharacterProfile
        {
            Id =
                Guid.Parse(
                    "ab019b27-0b56-4626-a5ab-7447a8183bdf"
                ),

            Name =
                "Profile Character",

            RulesetKey =
                "development",

            ClassKey =
                "test-class",

            SpecializationKey =
                null,

            Level =
                70
        };
    }
}
