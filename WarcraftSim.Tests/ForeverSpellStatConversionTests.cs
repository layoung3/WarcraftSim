using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Characters.Buffs;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Core.Characters.Talents;
using WarcraftSim.Core.Simulation.Building;
using WarcraftSim.Data.Forever.Characters;
using WarcraftSim.Data.Forever.Combat;
using WarcraftSim.Data.Forever.Simulation;

namespace WarcraftSim.Tests;

public sealed class ForeverSpellStatConversionTests
{
    [Fact]
    public void HasteRatingConstantMatchesCurrentForeverClientValue()
    {
        Assert.Equal(
            10m,
            ForeverSpellStatConversions.HasteRatingPerPercent
        );
    }

    [Fact]
    public void MageLevelThirtyIntellectProvidesSpellCriticalChance()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Mage,
                level:
                    30
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Intellect,
            27.3m
        );

        var result =
            CreateSpellCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            1m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.SpellCriticalChancePercent
            )
        );
    }

    [Fact]
    public void WarlockLevelSixtyUsesClientEmbeddedIntellectCritConversion()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Warlock,
                level:
                    60
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Intellect,
            121.2m
        );

        var result =
            CreateSpellCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            2m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.SpellCriticalChancePercent
            )
        );
    }

    [Fact]
    public void SharedHitAndCritRatingsProduceSpellFacingPercentStats()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Priest,
                level:
                    30
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.HitRating,
            20m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.CriticalStrikeRating,
            28m
        );

        var result =
            CreateSpellCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            2m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.SpellHitChancePercent
            )
        );

        Assert.Equal(
            2m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.SpellCriticalChancePercent
            )
        );
    }

    [Fact]
    public void HasteRatingProducesHastePercentForTimingConsumers()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Hunter,
                level:
                    30
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.HasteRating,
            25m
        );

        var result =
            CreateSpellCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            2.5m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.HastePercent
            )
        );
    }

    [Fact]
    public void StandardForeverPipelineCombinesIntellectAndCriticalRating()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Mage,
                level:
                    30
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Intellect,
            27.3m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.CriticalStrikeRating,
            14m
        );

        var result =
            ForeverCharacterSimulationRuntimeCalculatorFactory
                .CreateStandard(
                    maximumHealthResolver:
                        _ =>
                            5000m
                )
                .Calculate(
                    profile
                );

        Assert.Equal(
            2m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.SpellCriticalChancePercent
            )
        );

        Assert.Equal(
            1m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.AttackCriticalChancePercent
            )
        );
    }

    [Fact]
    public void StandardForeverPipelineProducesPhysicalAndSpellHitFromSharedRating()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Shaman,
                level:
                    30
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.HitRating,
            30m
        );

        var result =
            ForeverCharacterSimulationRuntimeCalculatorFactory
                .CreateStandard(
                    maximumHealthResolver:
                        _ =>
                            5000m
                )
                .Calculate(
                    profile
                );

        Assert.Equal(
            3m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.HitChancePercent
            )
        );

        Assert.Equal(
            3m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.SpellHitChancePercent
            )
        );
    }

    [Fact]
    public void SpellConversionRunsAfterEquipmentTalentsAndStaticBuffs()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Mage,
                level:
                    30
            );

        profile.Equipment.Items.Add(
            new CharacterEquipmentItem
            {
                SlotKey =
                    "head",

                ItemKey =
                    "intellect-helm",

                Name =
                    "Intellect Helm",

                Stats =
                {
                    Values =
                    {
                        [ForeverCombatStatKeys.Intellect] =
                            9.1m
                    }
                }
            }
        );

        profile.Talents.Selections.Add(
            new CharacterTalentSelection
            {
                TalentKey =
                    "intellect-talent",

                Name =
                    "Intellect Talent",

                Rank =
                    1,

                Stats =
                {
                    Values =
                    {
                        [ForeverCombatStatKeys.Intellect] =
                            9.1m
                    }
                }
            }
        );

        profile.Buffs.Selections.Add(
            new CharacterBuffSelection
            {
                BuffKey =
                    "intellect-buff",

                Name =
                    "Intellect Buff",

                Stats =
                {
                    Values =
                    {
                        [ForeverCombatStatKeys.Intellect] =
                            9.1m
                    }
                }
            }
        );

        var result =
            ForeverCharacterSimulationRuntimeCalculatorFactory
                .CreateStandard(
                    maximumHealthResolver:
                        _ =>
                            5000m
                )
                .Calculate(
                    profile
                );

        Assert.Equal(
            27.3m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.Intellect
            )
        );

        Assert.Equal(
            1m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.SpellCriticalChancePercent
            )
        );
    }

    [Fact]
    public void ExistingDirectSpellPercentBonusesArePreservedAndAddedTo()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Druid,
                level:
                    30
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Intellect,
            28.4m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.HitRating,
            10m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.CriticalStrikeRating,
            14m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.HasteRating,
            10m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.SpellHitChancePercent,
            2m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.SpellCriticalChancePercent,
            3m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.HastePercent,
            4m
        );

        var result =
            CreateSpellCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            3m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.SpellHitChancePercent
            )
        );

        Assert.Equal(
            5m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.SpellCriticalChancePercent
            )
        );

        Assert.Equal(
            5m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.HastePercent
            )
        );
    }

    [Theory]
    [InlineData(ForeverCharacterClassKeys.Warrior)]
    [InlineData(ForeverCharacterClassKeys.Rogue)]
    public void NonManaClassesDoNotGainSpellCritFromIntellect(
        string classKey)
    {
        var profile =
            CreateProfile(
                classKey,
                level:
                    30
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Intellect,
            100m
        );

        var result =
            CreateSpellCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            0m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.SpellCriticalChancePercent
            )
        );
    }

    [Fact]
    public void UnsupportedSpellCritLevelFailsInsteadOfInterpolating()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Mage,
                level:
                    45
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Intellect,
            20m
        );

        var exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    CreateSpellCalculator()
                        .Calculate(
                            profile
                        )
            );

        Assert.Contains(
            "not configured yet",
            exception.Message,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void UnsupportedClassFailsInsteadOfGuessingSpellConversions()
    {
        var profile =
            CreateProfile(
                "new-forever-class",
                level:
                    30
            );

        var exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    CreateSpellCalculator()
                        .Calculate(
                            profile
                        )
            );

        Assert.Contains(
            "no Forever primary spell stat profile",
            exception.Message,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void ContributorAppearsInStatContributionBreakdown()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Mage,
                level:
                    30
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Intellect,
            27.3m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.HitRating,
            10m
        );

        var result =
            CreateSpellCalculator()
                .Calculate(
                    profile
                );

        Assert.Contains(
            result.StatContributions,
            contribution =>
                contribution.ContributorKey ==
                    "forever-spell-stat-conversions" &&
                contribution.StatKey ==
                    ForeverCombatStatKeys.SpellHitChancePercent &&
                contribution.Amount ==
                    1m
        );

        Assert.Contains(
            result.StatContributions,
            contribution =>
                contribution.ContributorKey ==
                    "forever-spell-stat-conversions" &&
                contribution.StatKey ==
                    ForeverCombatStatKeys.SpellCriticalChancePercent &&
                contribution.Amount ==
                    1m
        );
    }

    [Theory]
    [InlineData(ForeverCharacterClassKeys.Paladin, 31.9, 59.9)]
    [InlineData(ForeverCharacterClassKeys.Hunter, 32.9, 60.6)]
    [InlineData(ForeverCharacterClassKeys.Priest, 26.9, 59.5)]
    [InlineData(ForeverCharacterClassKeys.Shaman, 28.2, 59.2)]
    [InlineData(ForeverCharacterClassKeys.Mage, 27.3, 59.5)]
    [InlineData(ForeverCharacterClassKeys.Warlock, 28.2, 60.6)]
    [InlineData(ForeverCharacterClassKeys.Druid, 28.4, 59.9)]
    public void SpellCritMilestonesMatchCurrentClientData(
        string classKey,
        double expectedAt30,
        double expectedAt60)
    {
        Assert.Equal(
            (decimal)expectedAt30,
            ForeverPrimarySpellStatProfiles
                .GetIntellectPerSpellCriticalPercent(
                    classKey,
                    30
                )
        );

        Assert.Equal(
            (decimal)expectedAt60,
            ForeverPrimarySpellStatProfiles
                .GetIntellectPerSpellCriticalPercent(
                    classKey,
                    60
                )
        );
    }

    private static BaseStatsCharacterSimulationRuntimeCalculator
        CreateSpellCalculator()
    {
        return new BaseStatsCharacterSimulationRuntimeCalculator(
            maximumHealthResolver:
                _ =>
                    5000m,

            statContributors:
                [
                    new ForeverSpellStatConversionContributor()
                ]
        );
    }

    private static CharacterProfile CreateProfile(
        string classKey,
        int level)
    {
        return new CharacterProfile
        {
            Name =
                "Forever Spell Stat Character",

            RulesetKey =
                "wow-forever",

            ClassKey =
                classKey,

            Level =
                level
        };
    }
}
