using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation.Building;
using WarcraftSim.Core.Stats;

namespace WarcraftSim.Tests;

public sealed class CharacterSimulationStatContributorTests
{
    [Fact]
    public void Calculate_AppliesContributorsAfterBaseStats()
    {
        var profile =
            CreateProfile();

        profile.BaseStats.Set(
            "armor",
            3000m
        );

        profile.BaseStats.Set(
            "strength",
            100m
        );

        var calculator =
            CreateCalculator(
                new TestContributor(
                    key:
                        "gear",

                    order:
                        100,

                    contribute:
                        (_, stats) =>
                        {
                            stats.Set(
                                "armor",
                                stats.Get(
                                    "armor"
                                ) +
                                1500m
                            );

                            stats.Set(
                                "strength",
                                stats.Get(
                                    "strength"
                                ) +
                                25m
                            );
                        }
                )
            );

        var result =
            calculator.Calculate(
                profile
            );

        Assert.Equal(
            4500m,
            result.EffectiveStats.Get(
                "armor"
            )
        );

        Assert.Equal(
            125m,
            result.EffectiveStats.Get(
                "strength"
            )
        );
    }

    [Fact]
    public void Calculate_AppliesContributorsInConfiguredOrder()
    {
        var profile =
            CreateProfile();

        profile.BaseStats.Set(
            "strength",
            100m
        );

        var multiply =
            new TestContributor(
                key:
                    "talent-conversion",

                order:
                    200,

                contribute:
                    (_, stats) =>
                        stats.Set(
                            "strength",
                            stats.Get(
                                "strength"
                            ) *
                            2m
                        )
            );

        var add =
            new TestContributor(
                key:
                    "gear",

                order:
                    100,

                contribute:
                    (_, stats) =>
                        stats.Set(
                            "strength",
                            stats.Get(
                                "strength"
                            ) +
                            50m
                        )
            );

        var calculator =
            CreateCalculator(
                // Intentionally provided in reverse order. The pipeline
                // should still run gear first because its Order is lower.
                multiply,
                add
            );

        var result =
            calculator.Calculate(
                profile
            );

        Assert.Equal(
            300m,
            result.EffectiveStats.Get(
                "strength"
            )
        );
    }

    [Fact]
    public void Calculate_ContributorCanUseCharacterContext()
    {
        var profile =
            CreateProfile();

        var calculator =
            CreateCalculator(
                new TestContributor(
                    key:
                        "level-scaling",

                    order:
                        100,

                    contribute:
                        (character, stats) =>
                            stats.Set(
                                "level-derived-stat",
                                character.Level *
                                10m
                            )
                )
            );

        var result =
            calculator.Calculate(
                profile
            );

        Assert.Equal(
            700m,
            result.EffectiveStats.Get(
                "level-derived-stat"
            )
        );
    }

    [Fact]
    public void Calculate_ContributorsDoNotMutateStoredBaseStats()
    {
        var profile =
            CreateProfile();

        profile.BaseStats.Set(
            "armor",
            2500m
        );

        var calculator =
            CreateCalculator(
                new TestContributor(
                    key:
                        "gear",

                    order:
                        100,

                    contribute:
                        (_, stats) =>
                            stats.Set(
                                "armor",
                                6000m
                            )
                )
            );

        var result =
            calculator.Calculate(
                profile
            );

        Assert.Equal(
            2500m,
            profile.BaseStats.Get(
                "armor"
            )
        );

        Assert.Equal(
            6000m,
            result.EffectiveStats.Get(
                "armor"
            )
        );
    }

    [Fact]
    public void Constructor_RejectsDuplicateContributorKeys()
    {
        Assert.Throws<ArgumentException>(
            () =>
                CreateCalculator(
                    new TestContributor(
                        key:
                            "gear",

                        order:
                            100,

                        contribute:
                            (_, _) =>
                            {
                            }
                    ),

                    new TestContributor(
                        key:
                            "GEAR",

                        order:
                            200,

                        contribute:
                            (_, _) =>
                            {
                            }
                    )
                )
        );
    }

    private static BaseStatsCharacterSimulationRuntimeCalculator
        CreateCalculator(
            params ICharacterSimulationStatContributor[] contributors)
    {
        return new BaseStatsCharacterSimulationRuntimeCalculator(
            maximumHealthResolver:
                _ =>
                    10000m,

            statContributors:
                contributors
        );
    }

    private static CharacterProfile CreateProfile()
    {
        return new CharacterProfile
        {
            Id =
                Guid.Parse(
                    "b8f8b786-3f41-4c90-908a-cfbccae46f7a"
                ),

            Name =
                "Contributor Character",

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

    private sealed class TestContributor :
        ICharacterSimulationStatContributor
    {
        private readonly Action<CharacterProfile, StatCollection>
            _contribute;

        public TestContributor(
            string key,
            int order,
            Action<CharacterProfile, StatCollection> contribute)
        {
            Key =
                key;

            Order =
                order;

            _contribute =
                contribute;
        }

        public string Key { get; }

        public int Order { get; }

        public void Contribute(
            CharacterProfile profile,
            StatCollection effectiveStats)
        {
            _contribute(
                profile,
                effectiveStats
            );
        }
    }
}
