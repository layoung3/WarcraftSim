using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation.Building;
using WarcraftSim.Core.Stats;

namespace WarcraftSim.Tests;

public sealed class CharacterSimulationStatContributionTests
{
    [Fact]
    public void Calculate_ReportsBaseAndSourceContributionsPerStat()
    {
        var profile =
            CreateProfile();

        profile.BaseStats.Set(
            "armor",
            3000m
        );

        var contributor =
            new StatBonusSourceContributor(
                key:
                    "character-bonuses",

                order:
                    100,

                sources:
                [
                    new CharacterSimulationStatBonusSource(
                        key:
                            "helmet",

                        sourceType:
                            "gear",

                        name:
                            "Heavy Helmet",

                        statBonuses:
                            new Dictionary<string, decimal>
                            {
                                ["armor"] =
                                    900m
                            }
                    ),

                    new CharacterSimulationStatBonusSource(
                        key:
                            "armor-enchant",

                        sourceType:
                            "enchant",

                        name:
                            "Armor Enchant",

                        statBonuses:
                            new Dictionary<string, decimal>
                            {
                                ["armor"] =
                                    300m
                            }
                    )
                ]
            );

        var result =
            CreateCalculator(
                    contributor
                )
                .Calculate(
                    profile
                );

        Assert.Equal(
            4200m,
            result.EffectiveStats.Get(
                "armor"
            )
        );

        var armorContributions =
            result.StatContributions
                .Where(
                    contribution =>
                        string.Equals(
                            contribution.StatKey,
                            "armor",
                            StringComparison.OrdinalIgnoreCase
                        )
                )
                .ToArray();

        Assert.Equal(
            3,
            armorContributions.Length
        );

        Assert.Equal(
            3000m,
            armorContributions.Single(
                contribution =>
                    contribution.SourceType ==
                        "base"
            ).Amount
        );

        var helmet =
            armorContributions.Single(
                contribution =>
                    contribution.SourceKey ==
                        "helmet"
            );

        Assert.Equal(
            "character-bonuses",
            helmet.ContributorKey
        );

        Assert.Equal(
            "gear",
            helmet.SourceType
        );

        Assert.Equal(
            "Heavy Helmet",
            helmet.SourceName
        );

        Assert.Equal(
            900m,
            helmet.Amount
        );

        Assert.Equal(
            300m,
            armorContributions.Single(
                contribution =>
                    contribution.SourceKey ==
                        "armor-enchant"
            ).Amount
        );

        Assert.Equal(
            result.EffectiveStats.Get(
                "armor"
            ),
            armorContributions.Sum(
                contribution =>
                    contribution.Amount
            )
        );
    }

    [Fact]
    public void Calculate_OmitsInactiveAndZeroValueSourceContributions()
    {
        var contributor =
            new StatBonusSourceContributor(
                key:
                    "character-bonuses",

                order:
                    100,

                sources:
                [
                    new CharacterSimulationStatBonusSource(
                        key:
                            "active",

                        sourceType:
                            "gear",

                        statBonuses:
                            new Dictionary<string, decimal>
                            {
                                ["strength"] =
                                    25m,

                                ["armor"] =
                                    0m
                            }
                    ),

                    new CharacterSimulationStatBonusSource(
                        key:
                            "inactive",

                        sourceType:
                            "buff",

                        statBonuses:
                            new Dictionary<string, decimal>
                            {
                                ["strength"] =
                                    100m
                            },

                        isActive:
                            false
                    )
                ]
            );

        var result =
            CreateCalculator(
                    contributor
                )
                .Calculate(
                    CreateProfile()
                );

        var contribution =
            Assert.Single(
                result.StatContributions
            );

        Assert.Equal(
            "active",
            contribution.SourceKey
        );

        Assert.Equal(
            "strength",
            contribution.StatKey
        );

        Assert.Equal(
            25m,
            contribution.Amount
        );
    }

    [Fact]
    public void Calculate_UsesContributorDeltaFallbackForGenericContributor()
    {
        var profile =
            CreateProfile();

        profile.BaseStats.Set(
            "strength",
            100m
        );

        var contributor =
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
                            1.5m
                        )
            );

        var result =
            CreateCalculator(
                    contributor
                )
                .Calculate(
                    profile
                );

        Assert.Equal(
            150m,
            result.EffectiveStats.Get(
                "strength"
            )
        );

        var fallback =
            result.StatContributions.Single(
                contribution =>
                    contribution.ContributorKey ==
                        "talent-conversion"
            );

        Assert.Equal(
            "talent-conversion",
            fallback.SourceKey
        );

        Assert.Equal(
            "contributor",
            fallback.SourceType
        );

        Assert.Equal(
            50m,
            fallback.Amount
        );
    }

    [Fact]
    public void Calculate_ContributionBreakdownReconcilesToEffectiveStats()
    {
        var profile =
            CreateProfile();

        profile.BaseStats.Set(
            "strength",
            100m
        );

        profile.BaseStats.Set(
            "armor",
            2000m
        );

        var calculator =
            CreateCalculator(
                new ExplicitStatBonusContributor(
                    key:
                        "manual-adjustments",

                    order:
                        100,

                    statBonuses:
                        new Dictionary<string, decimal>
                        {
                            ["strength"] =
                                20m,

                            ["armor"] =
                                -250m,

                            ["hit-rating"] =
                                15m
                        }
                )
            );

        var result =
            calculator.Calculate(
                profile
            );

        foreach (
            var statKey in
            result.EffectiveStats.Values.Keys)
        {
            var explainedValue =
                result.StatContributions
                    .Where(
                        contribution =>
                            string.Equals(
                                contribution.StatKey,
                                statKey,
                                StringComparison.OrdinalIgnoreCase
                            )
                    )
                    .Sum(
                        contribution =>
                            contribution.Amount
                    );

            Assert.Equal(
                result.EffectiveStats.Get(
                    statKey
                ),
                explainedValue
            );
        }
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
                    "73927f9d-c2d4-44f3-8aed-3437a692df15"
                ),

            Name =
                "Contribution Character",

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
