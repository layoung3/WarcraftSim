using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Building;
using WarcraftSim.Core.Stats;

namespace WarcraftSim.Tests;

public sealed class CharacterSimulationRuntimeCalculatorTests
{
    [Fact]
    public void Build_UsesCalculatedHealthAndStatsWhenCalculatorIsConfigured()
    {
        var effectiveStats =
            new StatCollection();

        effectiveStats.Set(
            "armor",
            7200m
        );

        effectiveStats.Set(
            "fire-resistance",
            90m
        );

        var calculator =
            new StubRuntimeCalculator(
                new CharacterSimulationRuntimeCalculationResult
                {
                    MaximumHealth =
                        18000m,

                    StartingHealth =
                        15000m,

                    EffectiveStats =
                        effectiveStats
                }
            );

        var builder =
            CreateBuilder(
                calculator
            );

        var result =
            builder.Build(
                new CharacterSimulationBuildRequest
                {
                    Profile =
                        CreateProfile(),

                    // MaximumHealth intentionally omitted here. The runtime
                    // calculator now owns calculated health and final stats.
                    Options =
                        new CharacterSimulationMappingOptions
                        {
                            ActorKey =
                                "calculated-tank",

                            TeamKey =
                                "raid",

                            AssignedRole =
                                SimulationType.Tank
                        }
                }
            );

        Assert.Equal(
            18000m,
            result.Actor.MaximumHealth
        );

        Assert.Equal(
            15000m,
            result.Actor.CurrentHealth
        );

        Assert.Equal(
            7200m,
            result.Actor.Stats.Get(
                "armor"
            )
        );

        Assert.Equal(
            90m,
            result.Actor.Stats.Get(
                "fire-resistance"
            )
        );

        Assert.Equal(
            "calculated-tank",
            result.Actor.Key
        );

        Assert.Equal(
            SimulationType.Tank,
            result.Actor.AssignedRole
        );

        Assert.Equal(
            1,
            calculator.CallCount
        );
    }

    [Fact]
    public void Build_CalculatedValuesOverrideTemporaryManualCombatValues()
    {
        var profile =
            CreateProfile();

        profile.BaseStats.Set(
            "armor",
            1000m
        );

        var calculatedStats =
            new StatCollection();

        calculatedStats.Set(
            "armor",
            6400m
        );

        var builder =
            CreateBuilder(
                new StubRuntimeCalculator(
                    new CharacterSimulationRuntimeCalculationResult
                    {
                        MaximumHealth =
                            16000m,

                        EffectiveStats =
                            calculatedStats
                    }
                )
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
                            // These values represent the old/manual path.
                            // When a calculator is configured, calculated
                            // combat values take precedence.
                            MaximumHealth =
                                5000m,

                            StartingHealth =
                                2500m,

                            EffectiveStats =
                                profile.BaseStats
                        }
                }
            );

        Assert.Equal(
            16000m,
            result.Actor.MaximumHealth
        );

        Assert.Equal(
            16000m,
            result.Actor.CurrentHealth
        );

        Assert.Equal(
            6400m,
            result.Actor.Stats.Get(
                "armor"
            )
        );
    }

    [Fact]
    public void Build_RejectsInvalidCalculatedHealth()
    {
        var builder =
            CreateBuilder(
                new StubRuntimeCalculator(
                    new CharacterSimulationRuntimeCalculationResult
                    {
                        MaximumHealth =
                            0m
                    }
                )
            );

        Assert.Throws<InvalidOperationException>(
            () =>
                builder.Build(
                    new CharacterSimulationBuildRequest
                    {
                        Profile =
                            CreateProfile(),

                        Options =
                            new CharacterSimulationMappingOptions()
                    }
                )
        );
    }

    private static CharacterSimulationActorBuilder CreateBuilder(
        ICharacterSimulationRuntimeCalculator runtimeCalculator)
    {
        var definition =
            new CharacterSimulationClassDefinition
            {
                RulesetKey =
                    "development",

                ClassKey =
                    "warrior"
            };

        return new CharacterSimulationActorBuilder(
            new CharacterSimulationClassCatalog(
                [
                    definition
                ]
            ),
            runtimeCalculator
        );
    }

    private static CharacterProfile CreateProfile()
    {
        return new CharacterProfile
        {
            Id =
                Guid.Parse(
                    "205c8785-2d2b-4d47-9dc6-2e6db097b7ba"
                ),

            Name =
                "Calculated Warrior",

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

    private sealed class StubRuntimeCalculator :
        ICharacterSimulationRuntimeCalculator
    {
        private readonly CharacterSimulationRuntimeCalculationResult
            _result;

        public StubRuntimeCalculator(
            CharacterSimulationRuntimeCalculationResult result)
        {
            _result =
                result;
        }

        public int CallCount { get; private set; }

        public CharacterSimulationRuntimeCalculationResult Calculate(
            CharacterProfile profile)
        {
            CallCount++;

            return _result;
        }
    }
}
