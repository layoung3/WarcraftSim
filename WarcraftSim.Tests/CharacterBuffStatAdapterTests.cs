using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Characters.Buffs;
using WarcraftSim.Core.Simulation.Building;

namespace WarcraftSim.Tests;

public sealed class CharacterBuffStatAdapterTests
{
    [Fact]
    public void Adapter_ConvertsEnabledBuffIntoNormalizedStatSource()
    {
        var profile =
            CreateProfile();

        profile.Buffs.Selections.Add(
            CreateBuff(
                buffKey:
                    "raid-strength",

                name:
                    "Raid Strength",

                stats:
                    new Dictionary<string, decimal>
                    {
                        ["strength"] =
                            25m,

                        ["stamina"] =
                            30m
                    }
            )
        );

        var source =
            Assert.Single(
                new CharacterBuffStatSourceAdapter()
                    .CreateSources(
                        profile
                    )
            );

        Assert.Equal(
            "buff:raid-strength",
            source.Key
        );

        Assert.Equal(
            "buff",
            source.SourceType
        );

        Assert.Equal(
            "Raid Strength",
            source.Name
        );

        Assert.True(
            source.IsActive
        );

        Assert.Equal(
            25m,
            source.StatBonuses[
                "strength"
            ]
        );

        Assert.Equal(
            30m,
            source.StatBonuses[
                "stamina"
            ]
        );
    }

    [Fact]
    public void Adapter_PreservesDisabledBuffAsInactiveSource()
    {
        var profile =
            CreateProfile();

        profile.Buffs.Selections.Add(
            CreateBuff(
                buffKey:
                    "disabled-buff",

                isEnabled:
                    false,

                stats:
                    new Dictionary<string, decimal>
                    {
                        ["strength"] =
                            100m
                    }
            )
        );

        var source =
            Assert.Single(
                new CharacterBuffStatSourceAdapter()
                    .CreateSources(
                        profile
                    )
            );

        Assert.False(
            source.IsActive
        );
    }

    [Fact]
    public void Adapter_RejectsDuplicateBuffKeysCaseInsensitively()
    {
        var profile =
            CreateProfile();

        profile.Buffs.Selections.Add(
            CreateBuff(
                buffKey:
                    "raid-strength"
            )
        );

        profile.Buffs.Selections.Add(
            CreateBuff(
                buffKey:
                    "RAID-STRENGTH"
            )
        );

        var exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    new CharacterBuffStatSourceAdapter()
                        .CreateSources(
                            profile
                        )
            );

        Assert.Contains(
            "duplicate buff key",
            exception.Message,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void Contributor_AppliesEnabledBuffsAndIgnoresDisabledBuffs()
    {
        var profile =
            CreateProfile();

        profile.BaseStats.Set(
            "strength",
            100m
        );

        profile.Buffs.Selections.Add(
            CreateBuff(
                buffKey:
                    "enabled-strength",

                stats:
                    new Dictionary<string, decimal>
                    {
                        ["strength"] =
                            20m
                    }
            )
        );

        profile.Buffs.Selections.Add(
            CreateBuff(
                buffKey:
                    "disabled-strength",

                isEnabled:
                    false,

                stats:
                    new Dictionary<string, decimal>
                    {
                        ["strength"] =
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

        Assert.DoesNotContain(
            result.StatContributions,
            contribution =>
                contribution.SourceKey ==
                    "buff:disabled-strength"
        );
    }

    [Fact]
    public void Contributor_PreservesPerBuffContributionProvenance()
    {
        var profile =
            CreateProfile();

        profile.Buffs.Selections.Add(
            CreateBuff(
                buffKey:
                    "raid-strength",

                name:
                    "Raid Strength",

                stats:
                    new Dictionary<string, decimal>
                    {
                        ["strength"] =
                            25m
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
            "buffs",
            contribution.ContributorKey
        );

        Assert.Equal(
            "buff:raid-strength",
            contribution.SourceKey
        );

        Assert.Equal(
            "buff",
            contribution.SourceType
        );

        Assert.Equal(
            "Raid Strength",
            contribution.SourceName
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
    public void Adapter_CopiesBuffStatsIntoSimulationSource()
    {
        var profile =
            CreateProfile();

        var buff =
            CreateBuff(
                buffKey:
                    "raid-strength",

                stats:
                    new Dictionary<string, decimal>
                    {
                        ["strength"] =
                            25m
                    }
            );

        profile.Buffs.Selections.Add(
            buff
        );

        var source =
            Assert.Single(
                new CharacterBuffStatSourceAdapter()
                    .CreateSources(
                        profile
                    )
            );

        buff.Stats.Set(
            "strength",
            2500m
        );

        Assert.Equal(
            25m,
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
                    new CharacterBuffStatContributor(
                        order:
                            300
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
                    "883bb26c-b01f-4827-a22b-d4e5cd2dcc5e"
                ),

            Name =
                "Buff Character",

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

    private static CharacterBuffSelection CreateBuff(
        string buffKey,
        string? name = null,
        bool isEnabled = true,
        IReadOnlyDictionary<string, decimal>? stats = null)
    {
        var selection =
            new CharacterBuffSelection
            {
                BuffKey =
                    buffKey,

                Name =
                    name ??
                    buffKey,

                IsEnabled =
                    isEnabled
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
