using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Characters.Buffs;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Core.Characters.Talents;
using WarcraftSim.Core.Simulation.Building;
using WarcraftSim.Data.Forever.Characters;
using WarcraftSim.Data.Forever.Combat;
using WarcraftSim.Data.Forever.Simulation;

namespace WarcraftSim.Tests;

public sealed class ForeverPrimaryPhysicalStatConversionTests
{
    [Theory]
    [InlineData(ForeverCharacterClassKeys.Warrior, 2)]
    [InlineData(ForeverCharacterClassKeys.Paladin, 2)]
    [InlineData(ForeverCharacterClassKeys.Hunter, 1)]
    [InlineData(ForeverCharacterClassKeys.Rogue, 1)]
    [InlineData(ForeverCharacterClassKeys.Priest, 1)]
    [InlineData(ForeverCharacterClassKeys.Shaman, 2)]
    [InlineData(ForeverCharacterClassKeys.Mage, 1)]
    [InlineData(ForeverCharacterClassKeys.Warlock, 1)]
    [InlineData(ForeverCharacterClassKeys.Druid, 2)]
    public void StrengthAttackPowerMatchesCurrentForeverClassData(
        string classKey,
        int expectedAttackPowerPerStrength)
    {
        var profile =
            CreateProfile(
                classKey,
                level:
                    30
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Strength,
            10m
        );

        var result =
            CreatePrimaryCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            10m *
            expectedAttackPowerPerStrength,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.AttackPower
            )
        );
    }

    [Fact]
    public void HunterAgilityProvidesMeleeAndRangedAttackPowerArmorAndCrit()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Hunter,
                level:
                    30
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Agility,
            48m
        );

        var result =
            CreatePrimaryCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            48m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.AttackPower
            )
        );

        Assert.Equal(
            96m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.RangedAttackPower
            )
        );

        Assert.Equal(
            96m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.Armor
            )
        );

        Assert.Equal(
            2m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.AttackCriticalChancePercent
            )
        );
    }

    [Fact]
    public void RogueLevelSixtyUsesClientEmbeddedAgilityCritConversion()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Rogue,
                level:
                    60
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Agility,
            58m
        );

        var result =
            CreatePrimaryCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            58m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.AttackPower
            )
        );

        Assert.Equal(
            116m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.RangedAttackPower
            )
        );

        Assert.Equal(
            2m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.AttackCriticalChancePercent
            )
        );
    }

    [Fact]
    public void WarriorAgilityProvidesRangedAttackPowerButNotMeleeAttackPower()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Warrior,
                level:
                    30
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Agility,
            20.8m
        );

        var result =
            CreatePrimaryCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            0m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.AttackPower
            )
        );

        Assert.Equal(
            41.6m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.RangedAttackPower
            )
        );

        Assert.Equal(
            2m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.AttackCriticalChancePercent
            )
        );
    }

    [Fact]
    public void DruidAgilityDoesNotApplyCatFormAttackPowerOutsideFormRuntime()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Druid,
                level:
                    30
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Agility,
            22.4m
        );

        var result =
            CreatePrimaryCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            0m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.AttackPower
            )
        );

        Assert.Equal(
            44.8m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.Armor
            )
        );

        Assert.Equal(
            2m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.AttackCriticalChancePercent
            )
        );
    }

    [Fact]
    public void PaladinAgilityAddsArmorAndCritButNoAttackPower()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Paladin,
                level:
                    60
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Strength,
            50m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Agility,
            39.6m
        );

        var result =
            CreatePrimaryCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            100m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.AttackPower
            )
        );

        Assert.Equal(
            0m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.RangedAttackPower
            )
        );

        Assert.Equal(
            79.2m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.Armor
            )
        );

        Assert.Equal(
            2m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.AttackCriticalChancePercent
            )
        );
    }

    [Fact]
    public void StandardForeverPipelineCombinesPrimaryStatsAndRatings()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Rogue,
                level:
                    30
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Agility,
            13m
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
                ForeverCombatStatKeys.AttackCriticalChancePercent
            )
        );
    }

    [Fact]
    public void PrimaryConversionRunsAfterEquipmentTalentsAndStaticBuffs()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Hunter,
                level:
                    30
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Strength,
            10m
        );

        profile.Equipment.Items.Add(
            new CharacterEquipmentItem
            {
                SlotKey =
                    "head",

                ItemKey =
                    "agility-helm",

                Name =
                    "Agility Helm",

                Stats =
                {
                    Values =
                    {
                        [ForeverCombatStatKeys.Agility] =
                            12m
                    }
                }
            }
        );

        profile.Talents.Selections.Add(
            new CharacterTalentSelection
            {
                TalentKey =
                    "agility-talent",

                Name =
                    "Agility Talent",

                Rank =
                    1,

                Stats =
                {
                    Values =
                    {
                        [ForeverCombatStatKeys.Agility] =
                            12m
                    }
                }
            }
        );

        profile.Buffs.Selections.Add(
            new CharacterBuffSelection
            {
                BuffKey =
                    "agility-buff",

                Name =
                    "Agility Buff",

                Stats =
                {
                    Values =
                    {
                        [ForeverCombatStatKeys.Agility] =
                            24m
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
            48m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.Agility
            )
        );

        Assert.Equal(
            58m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.AttackPower
            )
        );

        Assert.Equal(
            96m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.RangedAttackPower
            )
        );

        Assert.Equal(
            2m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.AttackCriticalChancePercent
            )
        );
    }

    [Fact]
    public void ExistingDirectAttackPowerAndArmorArePreservedAndAddedTo()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Hunter,
                level:
                    30
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Strength,
            10m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Agility,
            24m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.AttackPower,
            50m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Armor,
            100m
        );

        var result =
            CreatePrimaryCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            84m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.AttackPower
            )
        );

        Assert.Equal(
            148m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.Armor
            )
        );
    }

    [Fact]
    public void UnsupportedAgilityCritLevelFailsInsteadOfInterpolating()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Rogue,
                level:
                    45
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Agility,
            20m
        );

        var exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    CreatePrimaryCalculator()
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
    public void UnsupportedClassFailsInsteadOfGuessingPrimaryConversions()
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
                    CreatePrimaryCalculator()
                        .Calculate(
                            profile
                        )
            );

        Assert.Contains(
            "no Forever primary physical stat profile",
            exception.Message,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void ContributorAppearsInStatContributionBreakdown()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Warrior,
                level:
                    30
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Strength,
            20m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Agility,
            10.4m
        );

        var result =
            CreatePrimaryCalculator()
                .Calculate(
                    profile
                );

        Assert.Contains(
            result.StatContributions,
            contribution =>
                contribution.ContributorKey ==
                    "forever-primary-physical-stat-conversions" &&
                contribution.StatKey ==
                    ForeverCombatStatKeys.AttackPower &&
                contribution.Amount ==
                    40m
        );

        Assert.Contains(
            result.StatContributions,
            contribution =>
                contribution.ContributorKey ==
                    "forever-primary-physical-stat-conversions" &&
                contribution.StatKey ==
                    ForeverCombatStatKeys.AttackCriticalChancePercent &&
                contribution.Amount ==
                    1m
        );
    }

    [Theory]
    [InlineData(ForeverCharacterClassKeys.Warrior, 10.4, 20.0)]
    [InlineData(ForeverCharacterClassKeys.Paladin, 10.7, 19.8)]
    [InlineData(ForeverCharacterClassKeys.Hunter, 24.0, 52.9)]
    [InlineData(ForeverCharacterClassKeys.Rogue, 13.0, 29.0)]
    [InlineData(ForeverCharacterClassKeys.Priest, 14.0, 20.0)]
    [InlineData(ForeverCharacterClassKeys.Shaman, 11.5, 19.7)]
    [InlineData(ForeverCharacterClassKeys.Mage, 14.5, 19.5)]
    [InlineData(ForeverCharacterClassKeys.Warlock, 12.0, 20.0)]
    [InlineData(ForeverCharacterClassKeys.Druid, 11.2, 20.0)]
    public void CritMilestonesMatchCurrentClientData(
        string classKey,
        double expectedAt30,
        double expectedAt60)
    {
        Assert.Equal(
            (decimal)expectedAt30,
            ForeverPrimaryPhysicalStatProfiles
                .GetAgilityPerCriticalPercent(
                    classKey,
                    30
                )
        );

        Assert.Equal(
            (decimal)expectedAt60,
            ForeverPrimaryPhysicalStatProfiles
                .GetAgilityPerCriticalPercent(
                    classKey,
                    60
                )
        );
    }

    private static BaseStatsCharacterSimulationRuntimeCalculator
        CreatePrimaryCalculator()
    {
        return new BaseStatsCharacterSimulationRuntimeCalculator(
            maximumHealthResolver:
                _ =>
                    5000m,

            statContributors:
                [
                    new ForeverPrimaryPhysicalStatContributor()
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
                "Forever Primary Stat Character",

            RulesetKey =
                "wow-forever",

            ClassKey =
                classKey,

            Level =
                level
        };
    }
}
