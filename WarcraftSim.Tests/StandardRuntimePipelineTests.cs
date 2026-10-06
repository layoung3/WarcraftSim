using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Characters.Buffs;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Core.Characters.Talents;
using WarcraftSim.Core.Simulation.Building;
using WarcraftSim.Core.Stats;

namespace WarcraftSim.Tests;

public sealed class StandardRuntimePipelineTests
{
    [Fact]
    public void CreateStandard_UsesOfficialContributorOrder()
    {
        var contributors =
            CharacterSimulationStatContributorPipeline
                .CreateStandard();

        Assert.Equal(
            new[]
            {
                "equipment",
                "talents",
                "buffs"
            },
            contributors
                .Select(
                    contributor =>
                        contributor.Key
                )
                .ToArray()
        );

        Assert.Equal(
            CharacterSimulationStatContributorOrders.Equipment,
            contributors[0].Order
        );

        Assert.Equal(
            CharacterSimulationStatContributorOrders.Talents,
            contributors[1].Order
        );

        Assert.Equal(
            CharacterSimulationStatContributorOrders.Buffs,
            contributors[2].Order
        );
    }

    [Fact]
    public void StandardContributors_DefaultToOfficialOrders()
    {
        Assert.Equal(
            CharacterSimulationStatContributorOrders.Equipment,
            new CharacterEquipmentStatContributor().Order
        );

        Assert.Equal(
            CharacterSimulationStatContributorOrders.Talents,
            new CharacterTalentStatContributor().Order
        );

        Assert.Equal(
            CharacterSimulationStatContributorOrders.Buffs,
            new CharacterBuffStatContributor().Order
        );
    }

    [Fact]
    public void CreateStandardCalculator_AggregatesEquipmentTalentsAndBuffs()
    {
        var profile =
            CreateProfile();

        profile.BaseStats.Set(
            "strength",
            100m
        );

        var item =
            new CharacterEquipmentItem
            {
                SlotKey =
                    "head",

                ItemKey =
                    "helmet",

                Name =
                    "Heavy Helmet"
            };

        item.Stats.Set(
            "strength",
            20m
        );

        profile.Equipment.Items.Add(
            item
        );

        var talent =
            new CharacterTalentSelection
            {
                TalentKey =
                    "battle-conditioning",

                Name =
                    "Battle Conditioning",

                Rank =
                    1
            };

        talent.Stats.Set(
            "strength",
            15m
        );

        profile.Talents.Selections.Add(
            talent
        );

        var buff =
            new CharacterBuffSelection
            {
                BuffKey =
                    "raid-strength",

                Name =
                    "Raid Strength",

                IsEnabled =
                    true
            };

        buff.Stats.Set(
            "strength",
            25m
        );

        profile.Buffs.Selections.Add(
            buff
        );

        var result =
            CharacterSimulationRuntimeCalculatorFactory
                .CreateStandard(
                    maximumHealthResolver:
                        _ =>
                            10000m
                )
                .Calculate(
                    profile
                );

        Assert.Equal(
            160m,
            result.EffectiveStats.Get(
                "strength"
            )
        );
    }

    [Fact]
    public void CreateStandardCalculator_PreservesSourceProvenanceAcrossPipeline()
    {
        var profile =
            CreateProfile();

        var item =
            new CharacterEquipmentItem
            {
                SlotKey =
                    "head",

                ItemKey =
                    "helmet",

                Name =
                    "Heavy Helmet"
            };

        item.Stats.Set(
            "armor",
            900m
        );

        profile.Equipment.Items.Add(
            item
        );

        var talent =
            new CharacterTalentSelection
            {
                TalentKey =
                    "armor-training",

                Name =
                    "Armor Training",

                Rank =
                    1
            };

        talent.Stats.Set(
            "armor",
            300m
        );

        profile.Talents.Selections.Add(
            talent
        );

        var buff =
            new CharacterBuffSelection
            {
                BuffKey =
                    "raid-armor",

                Name =
                    "Raid Armor",

                IsEnabled =
                    true
            };

        buff.Stats.Set(
            "armor",
            100m
        );

        profile.Buffs.Selections.Add(
            buff
        );

        var result =
            CharacterSimulationRuntimeCalculatorFactory
                .CreateStandard(
                    maximumHealthResolver:
                        _ =>
                            10000m
                )
                .Calculate(
                    profile
                );

        Assert.Contains(
            result.StatContributions,
            contribution =>
                contribution.SourceType ==
                    "equipment" &&
                contribution.SourceName ==
                    "Heavy Helmet" &&
                contribution.Amount ==
                    900m
        );

        Assert.Contains(
            result.StatContributions,
            contribution =>
                contribution.SourceType ==
                    "talent" &&
                contribution.SourceName ==
                    "Armor Training" &&
                contribution.Amount ==
                    300m
        );

        Assert.Contains(
            result.StatContributions,
            contribution =>
                contribution.SourceType ==
                    "buff" &&
                contribution.SourceName ==
                    "Raid Armor" &&
                contribution.Amount ==
                    100m
        );
    }

    [Fact]
    public void CreateStandardCalculator_AllowsAdditionalContributors()
    {
        var profile =
            CreateProfile();

        profile.BaseStats.Set(
            "strength",
            100m
        );

        var additionalContributor =
            new ExplicitStatBonusContributor(
                key:
                    "ruleset-adjustment",

                order:
                    CharacterSimulationStatContributorOrders.Buffs +
                    100,

                statBonuses:
                    new Dictionary<string, decimal>
                    {
                        ["strength"] =
                            50m
                    }
            );

        var result =
            CharacterSimulationRuntimeCalculatorFactory
                .CreateStandard(
                    maximumHealthResolver:
                        _ =>
                            10000m,

                    additionalStatContributors:
                        [
                            additionalContributor
                        ]
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

        Assert.Contains(
            result.StatContributions,
            contribution =>
                contribution.ContributorKey ==
                    "ruleset-adjustment" &&
                contribution.Amount ==
                    50m
        );
    }

    [Fact]
    public void CreateStandardCalculator_RejectsDuplicateStandardContributorKey()
    {
        var duplicateEquipmentContributor =
            new TestContributor(
                key:
                    "EQUIPMENT",

                order:
                    500
            );

        var exception =
            Assert.Throws<ArgumentException>(
                () =>
                    CharacterSimulationRuntimeCalculatorFactory
                        .CreateStandard(
                            maximumHealthResolver:
                                _ =>
                                    10000m,

                            additionalStatContributors:
                                [
                                    duplicateEquipmentContributor
                                ]
                        )
            );

        Assert.Contains(
            "Duplicate character stat contributor key",
            exception.Message,
            StringComparison.OrdinalIgnoreCase
        );
    }

    private static CharacterProfile CreateProfile()
    {
        return new CharacterProfile
        {
            Id =
                Guid.Parse(
                    "fc0da533-4df6-4a2a-9302-c5d7f3b1c227"
                ),

            Name =
                "Standard Pipeline Character",

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
        public TestContributor(
            string key,
            int order)
        {
            Key =
                key;

            Order =
                order;
        }

        public string Key { get; }

        public int Order { get; }

        public void Contribute(
            CharacterProfile profile,
            StatCollection effectiveStats)
        {
        }
    }
}
