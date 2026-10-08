using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Data.Forever.Abilities.Warrior;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Tests;

public sealed class ForeverCleaveTests
{
    [Theory]
    [InlineData(1, 845, 20, 5)]
    [InlineData(2, 7369, 30, 10)]
    [InlineData(3, 11608, 40, 18)]
    [InlineData(4, 11609, 50, 32)]
    [InlineData(5, 20569, 60, 50)]
    public void RankTable_UsesForeverClientValues(
        int rank,
        int spellId,
        int requiredLevel,
        decimal bonusWeaponDamage)
    {
        var definition =
            ForeverCleaveRanks.GetByRank(rank);

        Assert.Equal(rank, definition.Rank);
        Assert.Equal(spellId, definition.SpellId);
        Assert.Equal(requiredLevel, definition.RequiredLevel);
        Assert.Equal(bonusWeaponDamage, definition.BonusWeaponDamage);
        Assert.Equal(20m, definition.RageCost);
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
            ForeverCleaveRanks.GetHighestAvailable(characterLevel);

        Assert.Equal(expectedRank, rank.Rank);
    }

    [Fact]
    public void LevelThirtyFactory_CreatesRankTwoMainHandTwoTargetSpecial()
    {
        var cleave =
            ForeverCleaveFactory.CreateForLevel(
                characterLevel: 30,
                minimumWeaponDamage: 100m,
                maximumWeaponDamage: 120m,
                weaponSpeedSeconds: 2.8m,
                attackSkillStatKey: "sword-skill"
            );

        Assert.Equal(ForeverCleaveFactory.AbilityKey, cleave.Key);
        Assert.Equal("Cleave (Rank 2)", cleave.Name);
        Assert.Equal(WeaponHandKeys.MainHand, cleave.WeaponHandKey);
        Assert.Equal(1, cleave.MaximumAdditionalTargets);

        var effect = cleave.DamageEffect;

        Assert.Equal(WeaponHandKeys.MainHand, effect.WeaponHandKey);
        Assert.Equal(110m, effect.MinimumValue);
        Assert.Equal(130m, effect.MaximumValue);
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

        var cost = Assert.Single(cleave.ResourceCosts);

        Assert.Equal(ForeverCleaveFactory.RageResourceKey, cost.ResourceKey);
        Assert.Equal(20m, cost.Amount);
        Assert.False(cost.IsPercentOfMaximum);
        Assert.Empty(cleave.ResourceRefunds);
    }

    [Fact]
    public void RageCostReduction_ChangesOnlyTheConfiguredCost()
    {
        var cleave =
            ForeverCleaveFactory.CreateForLevel(
                characterLevel: 30,
                minimumWeaponDamage: 100m,
                maximumWeaponDamage: 100m,
                weaponSpeedSeconds: 2.8m,
                attackSkillStatKey: "sword-skill",
                rageCostReduction: 3m
            );

        var cost = Assert.Single(cleave.ResourceCosts);

        Assert.Equal(17m, cost.Amount);
        Assert.Equal(110m, cleave.DamageEffect.MinimumValue);
        Assert.Equal(1, cleave.MaximumAdditionalTargets);
    }

    [Fact]
    public void UnsupportedFactoryInputs_FailExplicitly()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverCleaveFactory.CreateForLevel(
                19,
                100m,
                100m,
                2.8m,
                "sword-skill"
            )
        );

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverCleaveFactory.CreateForLevel(
                61,
                100m,
                100m,
                2.8m,
                "sword-skill"
            )
        );

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverCleaveFactory.CreateForLevel(
                30,
                100m,
                100m,
                0m,
                "sword-skill"
            )
        );

        Assert.Throws<ArgumentException>(() =>
            ForeverCleaveFactory.CreateForLevel(
                30,
                100m,
                100m,
                2.8m,
                ""
            )
        );

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverCleaveFactory.CreateForLevel(
                30,
                100m,
                100m,
                2.8m,
                "sword-skill",
                -1m
            )
        );
    }
}
