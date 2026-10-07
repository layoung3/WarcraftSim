using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Characters.Buffs;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Core.Characters.Talents;
using WarcraftSim.Core.Simulation.Building;
using WarcraftSim.Data.Forever.Characters;
using WarcraftSim.Data.Forever.Combat;
using WarcraftSim.Data.Forever.Simulation;

namespace WarcraftSim.Tests;

public sealed class ForeverPhysicalStatConversionTests
{
    [Fact]
    public void VerifiedRatingConstantsMatchCurrentForeverClientValues()
    {
        Assert.Equal(
            10m,
            ForeverPhysicalStatConversions.HitRatingPerPercent
        );

        Assert.Equal(
            14m,
            ForeverPhysicalStatConversions.CriticalStrikeRatingPerPercent
        );

        Assert.Equal(
            12m,
            ForeverPhysicalStatConversions.DodgeRatingPerPercent
        );

        Assert.Equal(
            15m,
            ForeverPhysicalStatConversions.ParryRatingPerPercent
        );

        Assert.Equal(
            5m,
            ForeverPhysicalStatConversions.BlockRatingPerPercent
        );

        Assert.Equal(
            20m,
            ForeverPhysicalStatConversions.StrengthPerBlockValue
        );
    }

    [Fact]
    public void ContributorConvertsAllPhysicalRatingsToPercentStats()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Warrior
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.HitRating,
            20m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.CriticalStrikeRating,
            28m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.DodgeRating,
            24m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.ParryRating,
            30m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.BlockRating,
            10m
        );

        var result =
            CreateCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            2m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.HitChancePercent
            )
        );

        Assert.Equal(
            2m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.AttackCriticalChancePercent
            )
        );

        Assert.Equal(
            2m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.DodgeChancePercent
            )
        );

        Assert.Equal(
            2m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.ParryChancePercent
            )
        );

        Assert.Equal(
            2m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.BlockChancePercent
            )
        );
    }

    [Fact]
    public void ContributorPreservesRawRatingsForBreakdownAndFutureUse()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Rogue
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.HitRating,
            25m
        );

        var result =
            CreateCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            25m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.HitRating
            )
        );

        Assert.Equal(
            2.5m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.HitChancePercent
            )
        );
    }

    [Fact]
    public void ContributorAddsRatingConversionsToExistingDirectPercentBonuses()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Rogue
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.HitChancePercent,
            1.5m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.HitRating,
            20m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.AttackCriticalChancePercent,
            3m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.CriticalStrikeRating,
            14m
        );

        var result =
            CreateCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            3.5m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.HitChancePercent
            )
        );

        Assert.Equal(
            4m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.AttackCriticalChancePercent
            )
        );
    }

    [Theory]
    [InlineData(ForeverCharacterClassKeys.Warrior)]
    [InlineData(ForeverCharacterClassKeys.Paladin)]
    [InlineData(ForeverCharacterClassKeys.Shaman)]
    public void ShieldClassesGainBlockValueFromStrength(
        string classKey)
    {
        var profile =
            CreateProfile(
                classKey
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Strength,
            60m
        );

        var result =
            CreateCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            3m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.BlockValue
            )
        );
    }

    [Fact]
    public void NonShieldClassDoesNotGainBlockValueFromStrength()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Rogue
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Strength,
            60m
        );

        var result =
            CreateCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            0m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.BlockValue
            )
        );
    }

    [Fact]
    public void StrengthBlockValueAddsToExistingShieldOrGearBlockValue()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Paladin
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Strength,
            100m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.BlockValue,
            42m
        );

        var result =
            CreateCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            47m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.BlockValue
            )
        );
    }

    [Fact]
    public void DerivedConversionsRunAfterEquipmentTalentsAndStaticBuffs()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Warrior
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Strength,
            20m
        );

        profile.Equipment.Items.Add(
            new CharacterEquipmentItem
            {
                SlotKey =
                    "head",

                ItemKey =
                    "rating-helm",

                Name =
                    "Rating Helm",

                Stats =
                {
                    Values =
                    {
                        [ForeverCombatStatKeys.HitRating] =
                            10m,

                        [ForeverCombatStatKeys.Strength] =
                            20m
                    }
                }
            }
        );

        profile.Talents.Selections.Add(
            new CharacterTalentSelection
            {
                TalentKey =
                    "rating-talent",

                Name =
                    "Rating Talent",

                Rank =
                    1,

                Stats =
                {
                    Values =
                    {
                        [ForeverCombatStatKeys.HitRating] =
                            10m
                    }
                }
            }
        );

        profile.Buffs.Selections.Add(
            new CharacterBuffSelection
            {
                BuffKey =
                    "strength-buff",

                Name =
                    "Strength Buff",

                Stats =
                {
                    Values =
                    {
                        [ForeverCombatStatKeys.Strength] =
                            20m,

                        [ForeverCombatStatKeys.CriticalStrikeRating] =
                            14m
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
            20m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.HitRating
            )
        );

        Assert.Equal(
            2m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.HitChancePercent
            )
        );

        Assert.Equal(
            1m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.AttackCriticalChancePercent
            )
        );

        Assert.Equal(
            60m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.Strength
            )
        );

        Assert.Equal(
            3m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.BlockValue
            )
        );
    }

    [Fact]
    public void DerivedContributorAppearsInStatContributionBreakdown()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Warrior
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.HitRating,
            10m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Strength,
            20m
        );

        var result =
            CreateCalculator()
                .Calculate(
                    profile
                );

        var hitContribution =
            Assert.Single(
                result.StatContributions,
                contribution =>
                    contribution.ContributorKey ==
                        "forever-physical-stat-conversions" &&
                    contribution.StatKey ==
                        ForeverCombatStatKeys.HitChancePercent
            );

        Assert.Equal(
            1m,
            hitContribution.Amount
        );

        var blockValueContribution =
            Assert.Single(
                result.StatContributions,
                contribution =>
                    contribution.ContributorKey ==
                        "forever-physical-stat-conversions" &&
                    contribution.StatKey ==
                        ForeverCombatStatKeys.BlockValue
            );

        Assert.Equal(
            1m,
            blockValueContribution.Amount
        );
    }

    [Fact]
    public void RatingConversionSupportsFractionalPercentWithoutRounding()
    {
        Assert.Equal(
            1.5m,
            ForeverPhysicalStatConversions.RatingToPercent(
                15m,
                ForeverPhysicalStatConversions.HitRatingPerPercent
            )
        );

        Assert.Equal(
            0.5m,
            ForeverPhysicalStatConversions.RatingToPercent(
                7m,
                ForeverPhysicalStatConversions.CriticalStrikeRatingPerPercent
            )
        );
    }

    [Fact]
    public void RatingConversionRejectsInvalidDivisor()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                ForeverPhysicalStatConversions.RatingToPercent(
                    10m,
                    0m
                )
        );
    }

    private static BaseStatsCharacterSimulationRuntimeCalculator
        CreateCalculator()
    {
        return new BaseStatsCharacterSimulationRuntimeCalculator(
            maximumHealthResolver:
                _ =>
                    5000m,

            statContributors:
                [
                    new ForeverPhysicalStatConversionContributor()
                ]
        );
    }

    private static CharacterProfile CreateProfile(
        string classKey)
    {
        return new CharacterProfile
        {
            Name =
                "Forever Conversion Character",

            RulesetKey =
                "wow-forever",

            ClassKey =
                classKey,

            Level =
                60
        };
    }
}
