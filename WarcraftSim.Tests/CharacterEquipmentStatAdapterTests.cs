using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Core.Simulation.Building;

namespace WarcraftSim.Tests;

public sealed class CharacterEquipmentStatAdapterTests
{
    [Fact]
    public void Adapter_ConvertsEquipmentIntoNormalizedStatSources()
    {
        var profile =
            CreateProfile();

        profile.Equipment.Items.Add(
            CreateItem(
                slotKey:
                    "head",

                itemKey:
                    "item-helmet-001",

                name:
                    "Heavy Helmet",

                stats:
                    new Dictionary<string, decimal>
                    {
                        ["armor"] =
                            900m,

                        ["strength"] =
                            20m
                    }
            )
        );

        var sources =
            new CharacterEquipmentStatSourceAdapter()
                .CreateSources(
                    profile
                );

        var source =
            Assert.Single(
                sources
            );

        Assert.Equal(
            "equipment:head:item-helmet-001",
            source.Key
        );

        Assert.Equal(
            "equipment",
            source.SourceType
        );

        Assert.Equal(
            "Heavy Helmet",
            source.Name
        );

        Assert.Equal(
            900m,
            source.StatBonuses[
                "armor"
            ]
        );

        Assert.Equal(
            20m,
            source.StatBonuses[
                "strength"
            ]
        );
    }

    [Fact]
    public void Adapter_AllowsSameItemKeyInDifferentSlots()
    {
        var profile =
            CreateProfile();

        profile.Equipment.Items.Add(
            CreateItem(
                slotKey:
                    "finger-1",

                itemKey:
                    "ring-001",

                stats:
                    new Dictionary<string, decimal>
                    {
                        ["strength"] =
                            10m
                    }
            )
        );

        profile.Equipment.Items.Add(
            CreateItem(
                slotKey:
                    "finger-2",

                itemKey:
                    "ring-001",

                stats:
                    new Dictionary<string, decimal>
                    {
                        ["strength"] =
                            10m
                    }
            )
        );

        var sources =
            new CharacterEquipmentStatSourceAdapter()
                .CreateSources(
                    profile
                );

        Assert.Equal(
            2,
            sources.Count
        );

        Assert.Contains(
            sources,
            source =>
                source.Key ==
                    "equipment:finger-1:ring-001"
        );

        Assert.Contains(
            sources,
            source =>
                source.Key ==
                    "equipment:finger-2:ring-001"
        );
    }

    [Fact]
    public void Adapter_RejectsDuplicateEquipmentSlotsCaseInsensitively()
    {
        var profile =
            CreateProfile();

        profile.Equipment.Items.Add(
            CreateItem(
                slotKey:
                    "head",

                itemKey:
                    "helmet-a"
            )
        );

        profile.Equipment.Items.Add(
            CreateItem(
                slotKey:
                    "HEAD",

                itemKey:
                    "helmet-b"
            )
        );

        var exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    new CharacterEquipmentStatSourceAdapter()
                        .CreateSources(
                            profile
                        )
            );

        Assert.Contains(
            "duplicate slot key",
            exception.Message,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void Contributor_AddsEquipmentStatsOnTopOfBaseStats()
    {
        var profile =
            CreateProfile();

        profile.BaseStats.Set(
            "strength",
            100m
        );

        profile.Equipment.Items.Add(
            CreateItem(
                slotKey:
                    "head",

                itemKey:
                    "helmet",

                stats:
                    new Dictionary<string, decimal>
                    {
                        ["strength"] =
                            20m,

                        ["armor"] =
                            500m
                    }
            )
        );

        profile.Equipment.Items.Add(
            CreateItem(
                slotKey:
                    "chest",

                itemKey:
                    "chestpiece",

                stats:
                    new Dictionary<string, decimal>
                    {
                        ["strength"] =
                            30m,

                        ["armor"] =
                            1000m
                    }
            )
        );

        var result =
            CreateCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            150m,
            result.EffectiveStats.Get(
                "strength"
            )
        );

        Assert.Equal(
            1500m,
            result.EffectiveStats.Get(
                "armor"
            )
        );
    }

    [Fact]
    public void Contributor_PreservesPerItemContributionProvenance()
    {
        var profile =
            CreateProfile();

        profile.BaseStats.Set(
            "armor",
            3000m
        );

        profile.Equipment.Items.Add(
            CreateItem(
                slotKey:
                    "head",

                itemKey:
                    "helmet",

                name:
                    "Heavy Helmet",

                stats:
                    new Dictionary<string, decimal>
                    {
                        ["armor"] =
                            900m
                    }
            )
        );

        profile.Equipment.Items.Add(
            CreateItem(
                slotKey:
                    "chest",

                itemKey:
                    "chestpiece",

                name:
                    "Heavy Chestpiece",

                stats:
                    new Dictionary<string, decimal>
                    {
                        ["armor"] =
                            1200m
                    }
            )
        );

        var result =
            CreateCalculator()
                .Calculate(
                    profile
                );

        var equipmentContributions =
            result.StatContributions
                .Where(
                    contribution =>
                        contribution.SourceType ==
                            "equipment"
                )
                .ToArray();

        Assert.Equal(
            2,
            equipmentContributions.Length
        );

        var helmet =
            equipmentContributions.Single(
                contribution =>
                    contribution.SourceKey ==
                        "equipment:head:helmet"
            );

        Assert.Equal(
            "equipment",
            helmet.ContributorKey
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
            result.EffectiveStats.Get(
                "armor"
            ),
            result.StatContributions
                .Where(
                    contribution =>
                        contribution.StatKey ==
                            "armor"
                )
                .Sum(
                    contribution =>
                        contribution.Amount
                )
        );
    }

    [Fact]
    public void Adapter_CopiesItemStatsIntoSimulationSource()
    {
        var profile =
            CreateProfile();

        var item =
            CreateItem(
                slotKey:
                    "head",

                itemKey:
                    "helmet",

                stats:
                    new Dictionary<string, decimal>
                    {
                        ["armor"] =
                            900m
                    }
            );

        profile.Equipment.Items.Add(
            item
        );

        var source =
            Assert.Single(
                new CharacterEquipmentStatSourceAdapter()
                    .CreateSources(
                        profile
                    )
            );

        item.Stats.Set(
            "armor",
            9000m
        );

        Assert.Equal(
            900m,
            source.StatBonuses[
                "armor"
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
                    new CharacterEquipmentStatContributor(
                        order:
                            100
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
                    "b513e2c8-81a1-4b5b-b40f-72e25c645705"
                ),

            Name =
                "Equipment Character",

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

    private static CharacterEquipmentItem CreateItem(
        string slotKey,
        string itemKey,
        string? name = null,
        IReadOnlyDictionary<string, decimal>? stats = null)
    {
        var item =
            new CharacterEquipmentItem
            {
                SlotKey =
                    slotKey,

                ItemKey =
                    itemKey,

                Name =
                    name ??
                    itemKey
            };

        foreach (
            var stat in
            stats ??
            new Dictionary<string, decimal>())
        {
            item.Stats.Set(
                stat.Key,
                stat.Value
            );
        }

        return item;
    }
}
