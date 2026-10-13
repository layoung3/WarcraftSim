// Consolidated tests for Characters Modifiers.
// Original class names are retained for xUnit test filtering.

// ==================== CharacterBuffStatAdapterTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Characters.Buffs;
using WarcraftSim.Core.Simulation.Building;

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
}

// ==================== CharacterEquipmentModifierTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Core.Simulation.Building;

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
}

// ==================== CharacterEquipmentStatAdapterTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Core.Simulation.Building;

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
}

// ==================== CharacterProfileSimulationMapperTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Building;
using WarcraftSim.Core.Stats;

public sealed class CharacterProfileSimulationMapperTests
{
    [Fact]
    public void ToBuildDefinition_MapsCharacterIdentityLevelAndBaseStats()
    {
        var id =
            Guid.Parse(
                "4cc9830d-2b47-47b0-9b3a-a6e544d5f56d"
            );

        var profile =
            new CharacterProfile
            {
                Id =
                    id,

                Name =
                    "Mapped Character",

                RulesetKey =
                    "development",

                ClassKey =
                    "test-class",

                SpecializationKey =
                    "test-spec",

                Level =
                    70
            };

        profile.BaseStats.Set(
            "armor",
            4200m
        );

        profile.BaseStats.Set(
            "spell-power",
            650m
        );

        var build =
            CharacterProfileSimulationMapper
                .ToBuildDefinition(
                    profile,
                    new CharacterSimulationMappingOptions
                    {
                        MaximumHealth =
                            9000m
                    }
                );

        Assert.Equal(
            id.ToString("N"),
            build.Key
        );

        Assert.Equal(
            "Mapped Character",
            build.Name
        );

        Assert.Equal(
            70,
            build.Level
        );

        Assert.Equal(
            4200m,
            build.Stats[
                "armor"
            ]
        );

        Assert.Equal(
            650m,
            build.Stats[
                "spell-power"
            ]
        );
    }

    [Fact]
    public void ToBuildDefinition_UsesEffectiveStatsWhenProvided()
    {
        var profile =
            CreateProfile();

        profile.BaseStats.Set(
            "armor",
            1000m
        );

        var effectiveStats =
            new StatCollection();

        effectiveStats.Set(
            "armor",
            5500m
        );

        effectiveStats.Set(
            "fire-resistance",
            75m
        );

        var build =
            CharacterProfileSimulationMapper
                .ToBuildDefinition(
                    profile,
                    new CharacterSimulationMappingOptions
                    {
                        MaximumHealth =
                            12000m,

                        EffectiveStats =
                            effectiveStats
                    }
                );

        Assert.Equal(
            5500m,
            build.Stats[
                "armor"
            ]
        );

        Assert.Equal(
            75m,
            build.Stats[
                "fire-resistance"
            ]
        );
    }

    [Fact]
    public void ToActor_MapsRuntimeRoleHealthTeamAndTiming()
    {
        var profile =
            CreateProfile();

        var actor =
            CharacterProfileSimulationMapper
                .ToActor(
                    profile,
                    new CharacterSimulationMappingOptions
                    {
                        ActorKey =
                            "raid-tank-1",

                        TeamKey =
                            "raid",

                        AssignedRole =
                            SimulationType.Tank,

                        MaximumHealth =
                            15000m,

                        StartingHealth =
                            11000m,

                        InitialActionDelaySeconds =
                            0.2m,

                        InputDelaySeconds =
                            0.1m
                    }
                );

        Assert.Equal(
            "raid-tank-1",
            actor.Key
        );

        Assert.Equal(
            "Profile Character",
            actor.Name
        );

        Assert.Equal(
            "raid",
            actor.TeamKey
        );

        Assert.Equal(
            SimulationType.Tank,
            actor.AssignedRole
        );

        Assert.Equal(
            15000m,
            actor.MaximumHealth
        );

        Assert.Equal(
            11000m,
            actor.CurrentHealth
        );

        Assert.Equal(
            0.2m,
            actor.InitialActionDelaySeconds
        );

        Assert.Equal(
            0.1m,
            actor.InputDelaySeconds
        );
    }

    [Fact]
    public void ToBuildDefinition_RejectsMissingRuntimeHealth()
    {
        var profile =
            CreateProfile();

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                CharacterProfileSimulationMapper
                    .ToBuildDefinition(
                        profile,
                        new CharacterSimulationMappingOptions()
                    )
        );
    }

    private static CharacterProfile CreateProfile()
    {
        return new CharacterProfile
        {
            Id =
                Guid.Parse(
                    "ab019b27-0b56-4626-a5ab-7447a8183bdf"
                ),

            Name =
                "Profile Character",

            RulesetKey =
                "development",

            ClassKey =
                "test-class",

            SpecializationKey =
                null,

            Level =
                70
        };
    }
}
}

// ==================== CharacterTalentStatAdapterTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Characters.Talents;
using WarcraftSim.Core.Simulation.Building;

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
}
