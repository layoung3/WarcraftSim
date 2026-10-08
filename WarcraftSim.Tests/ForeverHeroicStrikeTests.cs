using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Data.Forever.Abilities.Warrior;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Tests;

public sealed class ForeverHeroicStrikeTests
{
    [Theory]
    [InlineData(1, 78, 1, 11)]
    [InlineData(2, 284, 8, 21)]
    [InlineData(3, 285, 16, 32)]
    [InlineData(4, 1608, 24, 44)]
    [InlineData(5, 11564, 32, 58)]
    [InlineData(6, 11565, 40, 80)]
    [InlineData(7, 11566, 48, 111)]
    [InlineData(8, 11567, 56, 138)]
    [InlineData(9, 25286, 60, 157)]
    public void RankTable_UsesForeverClientValues(
        int rank,
        int spellId,
        int requiredLevel,
        decimal bonusWeaponDamage)
    {
        var definition =
            ForeverHeroicStrikeRanks.GetByRank(rank);

        Assert.Equal(rank, definition.Rank);
        Assert.Equal(spellId, definition.SpellId);
        Assert.Equal(requiredLevel, definition.RequiredLevel);
        Assert.Equal(
            bonusWeaponDamage,
            definition.BonusWeaponDamage
        );
        Assert.Equal(15m, definition.RageCost);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(8, 2)]
    [InlineData(30, 4)]
    [InlineData(60, 9)]
    public void HighestAvailableRank_UsesCharacterLevel(
        int characterLevel,
        int expectedRank)
    {
        var rank =
            ForeverHeroicStrikeRanks.GetHighestAvailable(
                characterLevel
            );

        Assert.Equal(expectedRank, rank.Rank);
    }

    [Fact]
    public void LevelThirtyFactory_CreatesRankFourMainHandSpecial()
    {
        var heroicStrike =
            ForeverHeroicStrikeFactory.CreateForLevel(
                characterLevel: 30,
                minimumWeaponDamage: 100m,
                maximumWeaponDamage: 120m,
                weaponSpeedSeconds: 2.8m,
                attackSkillStatKey: "sword-skill"
            );

        Assert.Equal(
            ForeverHeroicStrikeFactory.AbilityKey,
            heroicStrike.Key
        );
        Assert.Equal(
            "Heroic Strike (Rank 4)",
            heroicStrike.Name
        );
        Assert.Equal(
            WeaponHandKeys.MainHand,
            heroicStrike.WeaponHandKey
        );

        var effect =
            heroicStrike.DamageEffect;

        Assert.Equal(
            WeaponHandKeys.MainHand,
            effect.WeaponHandKey
        );
        Assert.Equal(144m, effect.MinimumValue);
        Assert.Equal(164m, effect.MaximumValue);
        Assert.Equal(
            ForeverCombatStatKeys.AttackPower,
            effect.ScalingStatKey
        );
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
        Assert.Equal(
            DamageMitigationTypes.Armor,
            effect.MitigationType
        );

        var cost =
            Assert.Single(heroicStrike.ResourceCosts);

        Assert.Equal(
            ForeverHeroicStrikeFactory.RageResourceKey,
            cost.ResourceKey
        );
        Assert.Equal(15m, cost.Amount);
        Assert.False(cost.IsPercentOfMaximum);

        // Refund behavior remains server-side/unverified and must be opted in
        // separately once confirmed rather than inherited from Classic.
        Assert.Empty(heroicStrike.ResourceRefunds);
    }

    [Fact]
    public void RageCostReduction_ChangesOnlyTheConfiguredCost()
    {
        var heroicStrike =
            ForeverHeroicStrikeFactory.CreateForLevel(
                characterLevel: 30,
                minimumWeaponDamage: 100m,
                maximumWeaponDamage: 100m,
                weaponSpeedSeconds: 2.8m,
                attackSkillStatKey: "sword-skill",
                rageCostReduction: 3m
            );

        var cost =
            Assert.Single(heroicStrike.ResourceCosts);

        Assert.Equal(12m, cost.Amount);
        Assert.Equal(144m, heroicStrike.DamageEffect.MinimumValue);
        Assert.Equal(144m, heroicStrike.DamageEffect.MaximumValue);
    }

    [Fact]
    public void UnsupportedFactoryInputs_FailExplicitly()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverHeroicStrikeFactory.CreateForLevel(
                0,
                100m,
                100m,
                2.8m,
                "sword-skill"
            )
        );

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverHeroicStrikeFactory.CreateForLevel(
                61,
                100m,
                100m,
                2.8m,
                "sword-skill"
            )
        );

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverHeroicStrikeFactory.CreateForLevel(
                30,
                100m,
                100m,
                0m,
                "sword-skill"
            )
        );

        Assert.Throws<ArgumentException>(() =>
            ForeverHeroicStrikeFactory.CreateForLevel(
                30,
                100m,
                100m,
                2.8m,
                ""
            )
        );

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverHeroicStrikeFactory.CreateForLevel(
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
