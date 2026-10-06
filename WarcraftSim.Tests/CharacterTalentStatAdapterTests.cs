using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Characters.Talents;
using WarcraftSim.Core.Simulation.Building;

namespace WarcraftSim.Tests;

public sealed class CharacterTalentStatAdapterTests
{
    [Fact]
    public void Adapter_ConvertsSelectedTalentIntoNormalizedStatSource()
    {
        var profile =
            CreateProfile();

        profile.Talents.Selections.Add(
            CreateTalent(
                talentKey:
                    "talent-armor-training",

                name:
                    "Armor Training",

                rank:
                    3,

                stats:
                    new Dictionary<string, decimal>
                    {
                        ["armor"] =
                            450m,

                        ["strength"] =
                            15m
                    }
            )
        );

        var source =
            Assert.Single(
                new CharacterTalentStatSourceAdapter()
                    .CreateSources(
                        profile
                    )
            );

        Assert.Equal(
            "talent:talent-armor-training",
            source.Key
        );

        Assert.Equal(
            "talent",
            source.SourceType
        );

        Assert.Equal(
            "Armor Training",
            source.Name
        );

        Assert.Equal(
            450m,
            source.StatBonuses[
                "armor"
            ]
        );

        Assert.Equal(
            15m,
            source.StatBonuses[
                "strength"
            ]
        );
    }

    [Fact]
    public void Adapter_RejectsDuplicateTalentKeysCaseInsensitively()
    {
        var profile =
            CreateProfile();

        profile.Talents.Selections.Add(
            CreateTalent(
                talentKey:
                    "talent-a"
            )
        );

        profile.Talents.Selections.Add(
            CreateTalent(
                talentKey:
                    "TALENT-A"
            )
        );

        var exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    new CharacterTalentStatSourceAdapter()
                        .CreateSources(
                            profile
                        )
            );

        Assert.Contains(
            "duplicate talent key",
            exception.Message,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void Adapter_RejectsTalentRankBelowOne()
    {
        var profile =
            CreateProfile();

        profile.Talents.Selections.Add(
            CreateTalent(
                talentKey:
                    "invalid-rank",

                rank:
                    0
            )
        );

        var exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    new CharacterTalentStatSourceAdapter()
                        .CreateSources(
                            profile
                        )
            );

        Assert.Contains(
            "rank of at least 1",
            exception.Message,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void Contributor_AddsTalentStatsOnTopOfExistingCharacterStats()
    {
        var profile =
            CreateProfile();

        profile.BaseStats.Set(
            "strength",
            100m
        );

        profile.Talents.Selections.Add(
            CreateTalent(
                talentKey:
                    "talent-strength",

                stats:
                    new Dictionary<string, decimal>
                    {
                        ["strength"] =
                            20m
                    }
            )
        );

        profile.Talents.Selections.Add(
            CreateTalent(
                talentKey:
                    "talent-defense",

                stats:
                    new Dictionary<string, decimal>
                    {
                        ["armor"] =
                            500m
                    }
            )
        );

        var result =
            CreateCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            120m,
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
    }

    [Fact]
    public void Contributor_PreservesPerTalentContributionProvenance()
    {
        var profile =
            CreateProfile();

        profile.Talents.Selections.Add(
            CreateTalent(
                talentKey:
                    "talent-strength",

                name:
                    "Battle Conditioning",

                rank:
                    2,

                stats:
                    new Dictionary<string, decimal>
                    {
                        ["strength"] =
                            20m
                    }
            )
        );

        var result =
            CreateCalculator()
                .Calculate(
                    profile
                );

        var contribution =
            Assert.Single(
                result.StatContributions
            );

        Assert.Equal(
            "talents",
            contribution.ContributorKey
        );

        Assert.Equal(
            "talent:talent-strength",
            contribution.SourceKey
        );

        Assert.Equal(
            "talent",
            contribution.SourceType
        );

        Assert.Equal(
            "Battle Conditioning",
            contribution.SourceName
        );

        Assert.Equal(
            "strength",
            contribution.StatKey
        );

        Assert.Equal(
            20m,
            contribution.Amount
        );
    }

    [Fact]
    public void Adapter_CopiesTalentStatsIntoSimulationSource()
    {
        var profile =
            CreateProfile();

        var talent =
            CreateTalent(
                talentKey:
                    "talent-strength",

                stats:
                    new Dictionary<string, decimal>
                    {
                        ["strength"] =
                            20m
                    }
            );

        profile.Talents.Selections.Add(
            talent
        );

        var source =
            Assert.Single(
                new CharacterTalentStatSourceAdapter()
                    .CreateSources(
                        profile
                    )
            );

        talent.Stats.Set(
            "strength",
            2000m
        );

        Assert.Equal(
            20m,
            source.StatBonuses[
                "strength"
            ]
        );
    }

    private static BaseStatsCharacterSimulationRuntimeCalculator
        CreateCalculator()
    {
        return new BaseStatsCharacterSimulationRuntimeCalculator(
            maximumHealthResolver:
                _ =>
                    10000m,

            statContributors:
                [
                    new CharacterTalentStatContributor(
                        order:
                            200
                    )
                ]
        );
    }

    private static CharacterProfile CreateProfile()
    {
        return new CharacterProfile
        {
            Id =
                Guid.Parse(
                    "68722676-c636-4df9-8e0e-15385be84724"
                ),

            Name =
                "Talent Character",

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

    private static CharacterTalentSelection CreateTalent(
        string talentKey,
        string? name = null,
        int rank = 1,
        IReadOnlyDictionary<string, decimal>? stats = null)
    {
        var selection =
            new CharacterTalentSelection
            {
                TalentKey =
                    talentKey,

                Name =
                    name ??
                    talentKey,

                Rank =
                    rank
            };

        foreach (
            var stat in
            stats ??
            new Dictionary<string, decimal>())
        {
            selection.Stats.Set(
                stat.Key,
                stat.Value
            );
        }

        return selection;
    }
}
