using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation.Building;
using WarcraftSim.Core.Stats;

namespace WarcraftSim.Tests;

public sealed class ExplicitStatBonusContributorTests
{
    [Fact]
    public void Contribute_AddsBonusToExistingStat()
    {
        var stats =
            new StatCollection();

        stats.Set(
            "armor",
            3000m
        );

        var contributor =
            CreateContributor(
                new Dictionary<string, decimal>
                {
                    ["armor"] =
                        1200m
                }
            );

        contributor.Contribute(
            CreateProfile(),
            stats
        );

        Assert.Equal(
            4200m,
            stats.Get(
                "armor"
            )
        );
    }

    [Fact]
    public void Contribute_AddsPreviouslyMissingStat()
    {
        var stats =
            new StatCollection();

        var contributor =
            CreateContributor(
                new Dictionary<string, decimal>
                {
                    ["spell-power"] =
                        35m
                }
            );

        contributor.Contribute(
            CreateProfile(),
            stats
        );

        Assert.Equal(
            35m,
            stats.Get(
                "spell-power"
            )
        );
    }

    [Fact]
    public void Contribute_AppliesMultipleStatBonuses()
    {
        var stats =
            new StatCollection();

        stats.Set(
            "stamina",
            100m
        );

        var contributor =
            CreateContributor(
                new Dictionary<string, decimal>
                {
                    ["stamina"] =
                        20m,

                    ["hit-rating"] =
                        15m,

                    ["fire-resistance"] =
                        10m
                }
            );

        contributor.Contribute(
            CreateProfile(),
            stats
        );

        Assert.Equal(
            120m,
            stats.Get(
                "stamina"
            )
        );

        Assert.Equal(
            15m,
            stats.Get(
                "hit-rating"
            )
        );

        Assert.Equal(
            10m,
            stats.Get(
                "fire-resistance"
            )
        );
    }

    [Fact]
    public void Contribute_AllowsNegativeAdjustments()
    {
        var stats =
            new StatCollection();

        stats.Set(
            "armor",
            5000m
        );

        var contributor =
            CreateContributor(
                new Dictionary<string, decimal>
                {
                    ["armor"] =
                        -500m
                }
            );

        contributor.Contribute(
            CreateProfile(),
            stats
        );

        Assert.Equal(
            4500m,
            stats.Get(
                "armor"
            )
        );
    }

    [Fact]
    public void Constructor_CopiesBonusDictionary()
    {
        var source =
            new Dictionary<string, decimal>
            {
                ["armor"] =
                    1000m
            };

        var contributor =
            CreateContributor(
                source
            );

        source["armor"] =
            9000m;

        var stats =
            new StatCollection();

        contributor.Contribute(
            CreateProfile(),
            stats
        );

        Assert.Equal(
            1000m,
            stats.Get(
                "armor"
            )
        );
    }

    [Fact]
    public void Constructor_RejectsBlankStatKey()
    {
        var bonuses =
            new Dictionary<string, decimal>
            {
                [""] =
                    100m
            };

        Assert.Throws<ArgumentException>(
            () =>
                CreateContributor(
                    bonuses
                )
        );
    }

    private static ExplicitStatBonusContributor CreateContributor(
        IReadOnlyDictionary<string, decimal> bonuses)
    {
        return new ExplicitStatBonusContributor(
            key:
                "test-bonuses",

            order:
                100,

            statBonuses:
                bonuses
        );
    }

    private static CharacterProfile CreateProfile()
    {
        return new CharacterProfile
        {
            Id =
                Guid.Parse(
                    "9b928a16-5f86-4bb4-a03c-8d8701cf0642"
                ),

            Name =
                "Stat Bonus Character",

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
