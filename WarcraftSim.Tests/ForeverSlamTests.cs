using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Data.Forever.Abilities.Warrior;
using WarcraftSim.Data.Forever.Characters;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Tests;

public sealed class ForeverSlamTests
{
    [Theory]
    [InlineData(1, 1240193, 20, 16)]
    [InlineData(2, 1464, 30, 32)]
    [InlineData(3, 8820, 38, 43)]
    [InlineData(4, 11604, 46, 68)]
    [InlineData(5, 11605, 54, 87)]
    public void RankTable_UsesForeverClientValues(
        int rank,
        int spellId,
        int requiredLevel,
        decimal bonusWeaponDamage)
    {
        var definition =
            ForeverSlamRanks.GetByRank(rank);

        Assert.Equal(rank, definition.Rank);
        Assert.Equal(spellId, definition.SpellId);
        Assert.Equal(requiredLevel, definition.RequiredLevel);
        Assert.Equal(bonusWeaponDamage, definition.BonusWeaponDamage);
        Assert.Equal(15m, definition.RageCost);
        Assert.Equal(1.5m, definition.CastTimeSeconds);
        Assert.Equal(18m, definition.CooldownSeconds);
        Assert.Equal(1.5m, definition.GlobalCooldownSeconds);
    }

    [Theory]
    [InlineData(20, 1)]
    [InlineData(29, 1)]
    [InlineData(30, 2)]
    [InlineData(60, 5)]
    public void HighestAvailableRank_UsesCharacterLevel(
        int characterLevel,
        int expectedRank)
    {
        var rank =
            ForeverSlamRanks.GetHighestAvailable(characterLevel);

        Assert.Equal(expectedRank, rank.Rank);
    }

    [Fact]
    public void LevelThirtyFactory_CreatesRankTwoForegroundWeaponSpecial()
    {
        var slam =
            ForeverSlamFactory.CreateForLevel(
                characterLevel: 30,
                minimumWeaponDamage: 100m,
                maximumWeaponDamage: 120m,
                weaponSpeedSeconds: 2.8m,
                attackSkillStatKey: "sword-skill"
            );

        Assert.Equal(ForeverSlamFactory.AbilityKey, slam.Key);
        Assert.Equal("Slam (Rank 2)", slam.Name);
        Assert.Equal(ForeverCharacterClassKeys.Warrior, slam.ClassKey);
        Assert.Equal(30, slam.RequiredLevel);
        Assert.Equal(1.5m, slam.CastTimeSeconds);
        Assert.Equal(18m, slam.CooldownSeconds);
        Assert.Equal(1.5m, slam.GlobalCooldownSeconds);
        Assert.False(slam.IsOffGlobalCooldown);

        var delayedHand =
            Assert.Single(slam.DelayedAutoAttackWeaponHandKeys);

        Assert.Equal(WeaponHandKeys.MainHand, delayedHand);

        var cost =
            Assert.Single(slam.ResourceCosts);

        Assert.Equal(ForeverSlamFactory.RageResourceKey, cost.ResourceKey);
        Assert.Equal(15m, cost.Amount);
        Assert.False(cost.IsPercentOfMaximum);

        var effect =
            Assert.Single(slam.Effects);

        Assert.Equal(WeaponHandKeys.MainHand, effect.WeaponHandKey);
        Assert.Equal(132m, effect.MinimumValue);
        Assert.Equal(152m, effect.MaximumValue);
        Assert.Equal(ForeverCombatStatKeys.AttackPower, effect.ScalingStatKey);
        Assert.Equal(0.2m, effect.ScalingCoefficient);
        Assert.Equal(
            ForeverCombatResolutionTypes.PlayerMeleeSpecial,
            effect.ResolutionType
        );
        Assert.Equal("sword-skill", effect.AttackSkillStatKey);
        Assert.True(effect.CanMiss);
        Assert.True(effect.CanBeDodged);
        Assert.True(effect.CanBeParried);
        Assert.True(effect.CanBeBlocked);
        Assert.True(effect.CanCrit);
        Assert.False(effect.CanGlance);
        Assert.False(effect.CanCrush);
        Assert.Equal(DamageMitigationTypes.Armor, effect.MitigationType);
    }

    [Fact]
    public void UnsupportedFactoryInputs_FailExplicitly()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverSlamFactory.CreateForLevel(
                19,
                100m,
                100m,
                2.8m,
                "sword-skill"
            )
        );

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverSlamFactory.CreateForLevel(
                61,
                100m,
                100m,
                2.8m,
                "sword-skill"
            )
        );

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverSlamFactory.CreateForLevel(
                30,
                100m,
                100m,
                0m,
                "sword-skill"
            )
        );

        Assert.Throws<ArgumentException>(() =>
            ForeverSlamFactory.CreateForLevel(
                30,
                100m,
                100m,
                2.8m,
                ""
            )
        );
    }
}
