using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Core.Simulation.Building;

namespace WarcraftSim.Tests;

public sealed class CharacterEquipmentModifierTests
{
    [Fact]
    public void Adapter_CreatesDistinctItemEnchantAndGemSources()
    {
        var profile =
            CreateProfile();

        var item =
            CreateItem(
                slotKey:
                    "head",

                itemKey:
                    "helmet-001",

                name:
                    "Heavy Helmet",

                stats:
                    new Dictionary<string, decimal>
                    {
                        ["armor"] =
                            900m
                    }
            );

        item.Modifiers.Add(
            CreateModifier(
                placementKey:
                    "enchant",

                modifierKey:
                    "enchant-001",

                modifierType:
                    CharacterEquipmentModifierTypes.Enchant,

                name:
                    "Greater Strength",

                stats:
                    new Dictionary<string, decimal>
                    {
                        ["strength"] =
                            20m
                    }
            )
        );

        item.Modifiers.Add(
            CreateModifier(
                placementKey:
                    "socket-1",

                modifierKey:
                    "gem-001",

                modifierType:
                    CharacterEquipmentModifierTypes.Gem,

                name:
                    "Bold Ruby",

                stats:
                    new Dictionary<string, decimal>
                    {
                        ["strength"] =
                            10m
                    }
            )
        );

        profile.Equipment.Items.Add(
            item
        );

        var sources =
            new CharacterEquipmentStatSourceAdapter()
                .CreateSources(
                    profile
                );

        Assert.Equal(
            3,
            sources.Count
        );

        Assert.Contains(
            sources,
            source =>
                source.Key ==
                    "equipment:head:helmet-001" &&
                source.SourceType ==
                    "equipment"
        );

        Assert.Contains(
            sources,
            source =>
                source.Key ==
                    "equipment:head:helmet-001:enchant:enchant:enchant-001" &&
                source.SourceType ==
                    "enchant" &&
                source.Name ==
                    "Greater Strength"
        );

        Assert.Contains(
            sources,
            source =>
                source.Key ==
                    "equipment:head:helmet-001:gem:socket-1:gem-001" &&
                source.SourceType ==
                    "gem" &&
                source.Name ==
                    "Bold Ruby"
        );
    }

    [Fact]
    public void Adapter_AllowsSameGemInDifferentSockets()
    {
        var profile =
            CreateProfile();

        var item =
            CreateItem(
                slotKey:
                    "chest",

                itemKey:
                    "chest-001"
            );

        item.Modifiers.Add(
            CreateModifier(
                placementKey:
                    "socket-1",

                modifierKey:
                    "gem-001",

                modifierType:
                    CharacterEquipmentModifierTypes.Gem
            )
        );

        item.Modifiers.Add(
            CreateModifier(
                placementKey:
                    "socket-2",

                modifierKey:
                    "gem-001",

                modifierType:
                    CharacterEquipmentModifierTypes.Gem
            )
        );

        profile.Equipment.Items.Add(
            item
        );

        var sources =
            new CharacterEquipmentStatSourceAdapter()
                .CreateSources(
                    profile
                );

        Assert.Contains(
            sources,
            source =>
                source.Key ==
                    "equipment:chest:chest-001:gem:socket-1:gem-001"
        );

        Assert.Contains(
            sources,
            source =>
                source.Key ==
                    "equipment:chest:chest-001:gem:socket-2:gem-001"
        );
    }

    [Fact]
    public void Adapter_RejectsDuplicateModifierPlacementsCaseInsensitively()
    {
        var profile =
            CreateProfile();

        var item =
            CreateItem(
                slotKey:
                    "head",

                itemKey:
                    "helmet-001"
            );

        item.Modifiers.Add(
            CreateModifier(
                placementKey:
                    "socket-1",

                modifierKey:
                    "gem-a",

                modifierType:
                    CharacterEquipmentModifierTypes.Gem
            )
        );

        item.Modifiers.Add(
            CreateModifier(
                placementKey:
                    "SOCKET-1",

                modifierKey:
                    "gem-b",

                modifierType:
                    CharacterEquipmentModifierTypes.Gem
            )
        );

        profile.Equipment.Items.Add(
            item
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
            "duplicate modifier placement key",
            exception.Message,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void Contributor_AggregatesItemEnchantAndGemStats()
    {
        var profile =
            CreateProfile();

        profile.BaseStats.Set(
            "strength",
            100m
        );

        var item =
            CreateItem(
                slotKey:
                    "head",

                itemKey:
                    "helmet-001",

                stats:
                    new Dictionary<string, decimal>
                    {
                        ["strength"] =
                            20m,

                        ["armor"] =
                            900m
                    }
            );

        item.Modifiers.Add(
            CreateModifier(
                placementKey:
                    "enchant",

                modifierKey:
                    "enchant-001",

                modifierType:
                    CharacterEquipmentModifierTypes.Enchant,

                stats:
                    new Dictionary<string, decimal>
                    {
                        ["strength"] =
                            15m
                    }
            )
        );

        item.Modifiers.Add(
            CreateModifier(
                placementKey:
                    "socket-1",

                modifierKey:
                    "gem-001",

                modifierType:
                    CharacterEquipmentModifierTypes.Gem,

                stats:
                    new Dictionary<string, decimal>
                    {
                        ["strength"] =
                            10m,

                        ["hit-rating"] =
                            8m
                    }
            )
        );

        profile.Equipment.Items.Add(
            item
        );

        var result =
            CreateCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            145m,
            result.EffectiveStats.Get(
                "strength"
            )
        );

        Assert.Equal(
            900m,
            result.EffectiveStats.Get(
                "armor"
            )
        );

        Assert.Equal(
            8m,
            result.EffectiveStats.Get(
                "hit-rating"
            )
        );
    }

    [Fact]
    public void Contributor_PreservesModifierProvenanceSeparatelyFromItem()
    {
        var profile =
            CreateProfile();

        var item =
            CreateItem(
                slotKey:
                    "head",

                itemKey:
                    "helmet-001",

                name:
                    "Heavy Helmet",

                stats:
                    new Dictionary<string, decimal>
                    {
                        ["strength"] =
                            20m
                    }
            );

        item.Modifiers.Add(
            CreateModifier(
                placementKey:
                    "enchant",

                modifierKey:
                    "enchant-001",

                modifierType:
                    CharacterEquipmentModifierTypes.Enchant,

                name:
                    "Greater Strength",

                stats:
                    new Dictionary<string, decimal>
                    {
                        ["strength"] =
                            15m
                    }
            )
        );

        item.Modifiers.Add(
            CreateModifier(
                placementKey:
                    "socket-1",

                modifierKey:
                    "gem-001",

                modifierType:
                    CharacterEquipmentModifierTypes.Gem,

                name:
                    "Bold Ruby",

                stats:
                    new Dictionary<string, decimal>
                    {
                        ["strength"] =
                            10m
                    }
            )
        );

        profile.Equipment.Items.Add(
            item
        );

        var result =
            CreateCalculator()
                .Calculate(
                    profile
                );

        var strengthContributions =
            result.StatContributions
                .Where(
                    contribution =>
                        contribution.StatKey ==
                            "strength"
                )
                .ToArray();

        Assert.Contains(
            strengthContributions,
            contribution =>
                contribution.SourceType ==
                    "equipment" &&
                contribution.SourceName ==
                    "Heavy Helmet" &&
                contribution.Amount ==
                    20m
        );

        Assert.Contains(
            strengthContributions,
            contribution =>
                contribution.SourceType ==
                    "enchant" &&
                contribution.SourceName ==
                    "Greater Strength" &&
                contribution.Amount ==
                    15m
        );

        Assert.Contains(
            strengthContributions,
            contribution =>
                contribution.SourceType ==
                    "gem" &&
                contribution.SourceName ==
                    "Bold Ruby" &&
                contribution.Amount ==
                    10m
        );
    }

    [Fact]
    public void Adapter_CopiesModifierStatsIntoSimulationSource()
    {
        var profile =
            CreateProfile();

        var modifier =
            CreateModifier(
                placementKey:
                    "socket-1",

                modifierKey:
                    "gem-001",

                modifierType:
                    CharacterEquipmentModifierTypes.Gem,

                stats:
                    new Dictionary<string, decimal>
                    {
                        ["strength"] =
                            10m
                    }
            );

        var item =
            CreateItem(
                slotKey:
                    "head",

                itemKey:
                    "helmet-001"
            );

        item.Modifiers.Add(
            modifier
        );

        profile.Equipment.Items.Add(
            item
        );

        var source =
            new CharacterEquipmentStatSourceAdapter()
                .CreateSources(
                    profile
                )
                .Single(
                    candidate =>
                        candidate.SourceType ==
                            "gem"
                );

        modifier.Stats.Set(
            "strength",
            1000m
        );

        Assert.Equal(
            10m,
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
                    "d4adbb49-b7a6-4b89-bef9-c32f7b68e166"
                ),

            Name =
                "Modifier Character",

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

    private static CharacterEquipmentModifier CreateModifier(
        string placementKey,
        string modifierKey,
        string modifierType,
        string? name = null,
        IReadOnlyDictionary<string, decimal>? stats = null)
    {
        var modifier =
            new CharacterEquipmentModifier
            {
                PlacementKey =
                    placementKey,

                ModifierKey =
                    modifierKey,

                ModifierType =
                    modifierType,

                Name =
                    name ??
                    modifierKey
            };

        foreach (
            var stat in
            stats ??
            new Dictionary<string, decimal>())
        {
            modifier.Stats.Set(
                stat.Key,
                stat.Value
            );
        }

        return modifier;
    }
}
