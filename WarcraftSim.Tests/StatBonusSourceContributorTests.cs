using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation.Building;

namespace WarcraftSim.Tests;

public sealed class StatBonusSourceContributorTests
{
    [Fact]
    public void Calculate_AggregatesActiveSourcesOnTopOfBaseStats()
    {
        var profile =
            CreateProfile();

        profile.BaseStats.Set(
            "strength",
            100m
        );

        var contributor =
            new StatBonusSourceContributor(
                key:
                    "character-bonuses",

                order:
                    100,

                sources:
                [
                    CreateSource(
                        key:
                            "helmet",

                        sourceType:
                            "gear",

                        bonuses:
                            new Dictionary<string, decimal>
                            {
                                ["strength"] =
                                    20m,

                                ["armor"] =
                                    500m
                            }
                    ),

                    CreateSource(
                        key:
                            "raid-buff",

                        sourceType:
                            "buff",

                        bonuses:
                            new Dictionary<string, decimal>
                            {
                                ["strength"] =
                                    15m,

                                ["hit-rating"] =
                                    10m
                            }
                    )
                ]
            );

        var calculator =
            CreateCalculator(
                contributor
            );

        var result =
            calculator.Calculate(
                profile
            );

        Assert.Equal(
            135m,
            result.EffectiveStats.Get(
                "strength"
            )
        );

        Assert.Equal(
            500m,
            result.EffectiveStats.Get(
                "armor"
            )
        );

        Assert.Equal(
            10m,
            result.EffectiveStats.Get(
                "hit-rating"
            )
        );
    }

    [Fact]
    public void Calculate_IgnoresInactiveSources()
    {
        var contributor =
            new StatBonusSourceContributor(
                key:
                    "character-bonuses",

                order:
                    100,

                sources:
                [
                    CreateSource(
                        key:
                            "active-source",

                        sourceType:
                            "gear",

                        bonuses:
                            new Dictionary<string, decimal>
                            {
                                ["armor"] =
                                    1000m
                            }
                    ),

                    CreateSource(
                        key:
                            "inactive-source",

                        sourceType:
                            "buff",

                        bonuses:
                            new Dictionary<string, decimal>
                            {
                                ["armor"] =
                                    9000m
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

        Assert.Equal(
            1000m,
            result.EffectiveStats.Get(
                "armor"
            )
        );
    }

    [Fact]
    public void Constructor_PreservesSourceMetadata()
    {
        var source =
            new CharacterSimulationStatBonusSource(
                key:
                    "set-bonus-2pc",

                sourceType:
                    "set-bonus",

                name:
                    "Two Piece Bonus",

                statBonuses:
                    new Dictionary<string, decimal>
                    {
                        ["spell-power"] =
                            35m
                    }
            );

        Assert.Equal(
            "set-bonus-2pc",
            source.Key
        );

        Assert.Equal(
            "set-bonus",
            source.SourceType
        );

        Assert.Equal(
            "Two Piece Bonus",
            source.Name
        );

        Assert.True(
            source.IsActive
        );
    }

    [Fact]
    public void Source_CopiesStatBonusDictionary()
    {
        var bonuses =
            new Dictionary<string, decimal>
            {
                ["armor"] =
                    1000m
            };

        var source =
            CreateSource(
                key:
                    "helmet",

                sourceType:
                    "gear",

                bonuses:
                    bonuses
            );

        bonuses["armor"] =
            9000m;

        Assert.Equal(
            1000m,
            source.StatBonuses[
                "armor"
            ]
        );
    }

    [Fact]
    public void Contributor_RejectsDuplicateSourceKeysCaseInsensitively()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new StatBonusSourceContributor(
                    key:
                        "character-bonuses",

                    order:
                        100,

                    sources:
                    [
                        CreateSource(
                            key:
                                "helmet",

                            sourceType:
                                "gear",

                            bonuses:
                                new Dictionary<string, decimal>()
                        ),

                        CreateSource(
                            key:
                                "HELMET",

                            sourceType:
                                "gear",

                            bonuses:
                                new Dictionary<string, decimal>()
                        )
                    ]
                )
        );
    }

    [Fact]
    public void Source_RejectsInvalidMetadataAndStatKeys()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new CharacterSimulationStatBonusSource(
                    key:
                        "helmet",

                    sourceType:
                        "",

                    statBonuses:
                        new Dictionary<string, decimal>()
                )
        );

        Assert.Throws<ArgumentException>(
            () =>
                new CharacterSimulationStatBonusSource(
                    key:
                        "helmet",

                    sourceType:
                        "gear",

                    statBonuses:
                        new Dictionary<string, decimal>
                        {
                            [""] =
                                100m
                        }
                )
        );
    }

    private static CharacterSimulationStatBonusSource CreateSource(
        string key,
        string sourceType,
        IReadOnlyDictionary<string, decimal> bonuses,
        bool isActive = true)
    {
        return new CharacterSimulationStatBonusSource(
            key:
                key,

            sourceType:
                sourceType,

            statBonuses:
                bonuses,

            isActive:
                isActive
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
                    "c35ab5bd-a4e7-4ae7-a690-ae48859c1d39"
                ),

            Name =
                "Stat Source Character",

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
