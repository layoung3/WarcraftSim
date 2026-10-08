using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Data.Forever.Abilities.Warrior;

/// <summary>
/// Creates the client-verified portion of Heroic Strike as a one-shot
/// main-hand swing replacement. Exact server-side bonus threat and
/// miss/dodge/parry refund behavior remain intentionally unconfigured until
/// verified for Forever.
/// </summary>
public static class ForeverHeroicStrikeFactory
{
    public const string AbilityKey =
        "heroic-strike";

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

        if (string.IsNullOrWhiteSpace(
                attackSkillStatKey))
        {
            throw new ArgumentException(
                "Heroic Strike requires a melee attack-skill stat key.",
                nameof(attackSkillStatKey)
            );
        }

        if (rageCostReduction < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rageCostReduction),
                rageCostReduction,
                "Heroic Strike Rage-cost reduction cannot be negative."
            );
        }

        var rank =
            ForeverHeroicStrikeRanks.GetHighestAvailable(
                characterLevel
            );

        var lowerWeaponDamage =
            Math.Max(
                0m,
                Math.Min(
                    minimumWeaponDamage,
                    maximumWeaponDamage
                )
            );

        var upperWeaponDamage =
            Math.Max(
                0m,
                Math.Max(
                    minimumWeaponDamage,
                    maximumWeaponDamage
                )
            );

        var effect =
            new AbilityEffectDefinition
            {
                Key =
                    "damage",

                EffectType =
                    AbilityEffectTypes.DirectDamage,

                TargetType =
                    AbilityTargetTypes.Enemy,

                WeaponHandKey =
                    WeaponHandKeys.MainHand,

                MinimumValue =
                    lowerWeaponDamage +
                    rank.BonusWeaponDamage,

                MaximumValue =
                    upperWeaponDamage +
                    rank.BonusWeaponDamage,

                ScalingStatKey =
                    ForeverCombatStatKeys.AttackPower,

                ScalingCoefficient =
                    weaponSpeedSeconds /
                    ForeverAutoAttackFactory.AttackPowerPerWeaponDamagePerSecond,

                MitigationType =
                    DamageMitigationTypes.Armor
            };

        ForeverMeleeAttackProfiles.Apply(
            effect,
            ForeverMeleeAttackProfiles.PlayerSpecial,
            attackSkillStatKey
        );

        var rageCost =
            Math.Max(
                0m,
                rank.RageCost -
                rageCostReduction
            );

        return new NextSwingReplacementDefinition
        {
            Key =
                AbilityKey,

            Name =
                $"Heroic Strike (Rank {rank.Rank})",

            WeaponHandKey =
                WeaponHandKeys.MainHand,

            ResourceCosts =
            [
                new AbilityResourceCost
                {
                    ResourceKey =
                        RageResourceKey,

                    Amount =
                        rageCost
                }
            ],

            DamageEffect =
                effect
        };
    }
}
