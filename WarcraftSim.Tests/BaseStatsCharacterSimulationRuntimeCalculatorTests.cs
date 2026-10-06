using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation.Building;

namespace WarcraftSim.Tests;

public sealed class BaseStatsCharacterSimulationRuntimeCalculatorTests
{
    [Fact]
    public void Calculate_UsesResolvedHealthAndCopiesBaseStats()
    {
        var profile =
            CreateProfile();

        profile.BaseStats.Set(
            "armor",
            4200m
        );

        profile.BaseStats.Set(
            "strength",
            150m
        );

        var calculator =
            new BaseStatsCharacterSimulationRuntimeCalculator(
                maximumHealthResolver:
                    character =>
                        character.Level * 100m
            );

        var result =
            calculator.Calculate(
                profile
            );

        Assert.Equal(
            7000m,
            result.MaximumHealth
        );

        Assert.Null(
            result.StartingHealth
        );

        Assert.Equal(
            4200m,
            result.EffectiveStats.Get(
                "armor"
            )
        );

        Assert.Equal(
            150m,
            result.EffectiveStats.Get(
                "strength"
            )
        );
    }

    [Fact]
    public void Calculate_CreatesIndependentEffectiveStatCollection()
    {
        var profile =
            CreateProfile();

        profile.BaseStats.Set(
            "armor",
            3000m
        );

        var calculator =
            new BaseStatsCharacterSimulationRuntimeCalculator(
                maximumHealthResolver:
                    _ =>
                        8000m
            );

        var result =
            calculator.Calculate(
                profile
            );

        result.EffectiveStats.Set(
            "armor",
            9000m
        );

        Assert.Equal(
            3000m,
            profile.BaseStats.Get(
                "armor"
            )
        );

        Assert.Equal(
            9000m,
            result.EffectiveStats.Get(
                "armor"
            )
        );
    }

    [Fact]
    public void Calculate_UsesOptionalStartingHealthResolver()
    {
        var profile =
            CreateProfile();

        var calculator =
            new BaseStatsCharacterSimulationRuntimeCalculator(
                maximumHealthResolver:
                    _ =>
                        10000m,

                startingHealthResolver:
                    _ =>
                        7500m
            );

        var result =
            calculator.Calculate(
                profile
            );

        Assert.Equal(
            10000m,
            result.MaximumHealth
        );

        Assert.Equal(
            7500m,
            result.StartingHealth
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Calculate_RejectsInvalidMaximumHealth(
        int maximumHealth)
    {
        var calculator =
            new BaseStatsCharacterSimulationRuntimeCalculator(
                maximumHealthResolver:
                    _ =>
                        maximumHealth
            );

        Assert.Throws<InvalidOperationException>(
            () =>
                calculator.Calculate(
                    CreateProfile()
                )
        );
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10001)]
    public void Calculate_RejectsInvalidStartingHealth(
        int startingHealth)
    {
        var calculator =
            new BaseStatsCharacterSimulationRuntimeCalculator(
                maximumHealthResolver:
                    _ =>
                        10000m,

                startingHealthResolver:
                    _ =>
                        startingHealth
            );

        Assert.Throws<InvalidOperationException>(
            () =>
                calculator.Calculate(
                    CreateProfile()
                )
        );
    }

    private static CharacterProfile CreateProfile()
    {
        return new CharacterProfile
        {
            Id =
                Guid.Parse(
                    "38aaf89e-917a-489b-97a8-f14acbbf9f88"
                ),

            Name =
                "Base Stats Character",

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
