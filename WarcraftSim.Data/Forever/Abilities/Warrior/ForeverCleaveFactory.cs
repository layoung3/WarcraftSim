using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Data.Forever.Abilities.Warrior;

/// <summary>
/// Creates Cleave as a one-shot main-hand swing replacement. The replacement
/// uses encounter cleave-range links to resolve at most one secondary enemy.
/// </summary>
public static class ForeverCleaveFactory
{
    public const string AbilityKey =
        "cleave";

    public const string RageResourceKey =
        "rage";

    public static NextSwingReplacementDefinition CreateForLevel(
        int characterLevel,
        decimal minimumWeaponDamage,
        decimal maximumWeaponDamage,
        decimal weaponSpeedSeconds,
        string attackSkillStatKey,
        decimal rageCostReduction = 0m)
    {
        if (weaponSpeedSeconds <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(weaponSpeedSeconds),
                weaponSpeedSeconds,
                "Weapon speed must be greater than zero."
            );
        }

        if (string.IsNullOrWhiteSpace(attackSkillStatKey))
        {
            throw new ArgumentException(
                "Cleave requires a melee attack-skill stat key.",
                nameof(attackSkillStatKey)
            );
        }

        if (rageCostReduction < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rageCostReduction),
                rageCostReduction,
                "Cleave Rage-cost reduction cannot be negative."
            );
        }

        var rank =
            ForeverCleaveRanks.GetHighestAvailable(characterLevel);

        var lowerWeaponDamage =
            Math.Max(
                0m,
                Math.Min(minimumWeaponDamage, maximumWeaponDamage)
            );

        var upperWeaponDamage =
            Math.Max(
                0m,
                Math.Max(minimumWeaponDamage, maximumWeaponDamage)
            );

        var effect =
            new AbilityEffectDefinition
            {
                Key = "damage",
                EffectType = AbilityEffectTypes.DirectDamage,
                TargetType = AbilityTargetTypes.Enemy,
                WeaponHandKey = WeaponHandKeys.MainHand,
                MinimumValue = lowerWeaponDamage + rank.BonusWeaponDamage,
                MaximumValue = upperWeaponDamage + rank.BonusWeaponDamage,
                ScalingStatKey = ForeverCombatStatKeys.AttackPower,
                ScalingCoefficient =
                    weaponSpeedSeconds /
                    ForeverAutoAttackFactory.AttackPowerPerWeaponDamagePerSecond,
                MitigationType = DamageMitigationTypes.Armor
            };

        ForeverMeleeAttackProfiles.Apply(
            effect,
            ForeverMeleeAttackProfiles.PlayerSpecial,
            attackSkillStatKey
        );

        var rageCost =
            Math.Max(
                0m,
                rank.RageCost - rageCostReduction
            );

        return new NextSwingReplacementDefinition
        {
            Key = AbilityKey,
            Name = $"Cleave (Rank {rank.Rank})",
            WeaponHandKey = WeaponHandKeys.MainHand,
            MaximumAdditionalTargets = 1,
            ResourceCosts =
            [
                new AbilityResourceCost
                {
                    ResourceKey = RageResourceKey,
                    Amount = rageCost
                }
            ],
            DamageEffect = effect
        };
    }
}
