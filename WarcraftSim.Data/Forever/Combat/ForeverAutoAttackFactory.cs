using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Data.Forever.Combat;

/// <summary>
/// Creates Forever player melee auto-attacks from client-visible weapon
/// damage/speed data and the character-sheet attack-power conversion.
/// </summary>
public static class ForeverAutoAttackFactory
{
    // Forever's current character sheet states that 14 Attack Power adds
    // 1 weapon DPS. A weapon swing therefore gains AP / 14 * weapon speed.
    public const decimal AttackPowerPerWeaponDamagePerSecond =
        14m;

    // Forever currently retains the Classic-style baseline where an off-hand
    // weapon deals half of its normal weapon/AP damage before off-hand damage
    // bonuses are applied.
    public const decimal BaseOffHandDamageMultiplier =
        0.5m;

    public static AutoAttackDefinition CreatePlayerMelee(
        string key,
        string name,
        decimal minimumWeaponDamage,
        decimal maximumWeaponDamage,
        decimal weaponSpeedSeconds,
        string attackSkillStatKey,
        bool usesDualWieldHitTable = false)
    {
        return CreatePlayerMeleeCore(
            key,
            name,
            minimumWeaponDamage,
            maximumWeaponDamage,
            weaponSpeedSeconds,
            attackSkillStatKey,
            usesDualWieldHitTable,
            WeaponHandKeys.MainHand,
            damageMultiplier: 1m,
            damageMultiplierStatKey: null
        );
    }

    public static AutoAttackDefinition CreatePlayerDualWieldMainHand(
        string key,
        string name,
        decimal minimumWeaponDamage,
        decimal maximumWeaponDamage,
        decimal weaponSpeedSeconds,
        string attackSkillStatKey)
    {
        return CreatePlayerMeleeCore(
            key,
            name,
            minimumWeaponDamage,
            maximumWeaponDamage,
            weaponSpeedSeconds,
            attackSkillStatKey,
            usesDualWieldHitTable: true,
            weaponHandKey:
                WeaponHandKeys.MainHand,
            damageMultiplier: 1m,
            damageMultiplierStatKey: null
        );
    }

    public static AutoAttackDefinition CreatePlayerDualWieldOffHand(
        string key,
        string name,
        decimal minimumWeaponDamage,
        decimal maximumWeaponDamage,
        decimal weaponSpeedSeconds,
        string attackSkillStatKey)
    {
        return CreatePlayerMeleeCore(
            key,
            name,
            minimumWeaponDamage,
            maximumWeaponDamage,
            weaponSpeedSeconds,
            attackSkillStatKey,
            usesDualWieldHitTable: true,
            weaponHandKey:
                WeaponHandKeys.OffHand,
            damageMultiplier:
                BaseOffHandDamageMultiplier,
            damageMultiplierStatKey:
                ForeverCombatStatKeys.OffHandDamagePercent
        );
    }

    private static AutoAttackDefinition CreatePlayerMeleeCore(
        string key,
        string name,
        decimal minimumWeaponDamage,
        decimal maximumWeaponDamage,
        decimal weaponSpeedSeconds,
        string attackSkillStatKey,
        bool usesDualWieldHitTable,
        string weaponHandKey,
        decimal damageMultiplier,
        string? damageMultiplierStatKey)
    {
        if (string.IsNullOrWhiteSpace(
                key))
        {
            throw new ArgumentException(
                "Forever auto-attacks require a key.",
                nameof(key)
            );
        }

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
                "Forever player auto-attacks require an attack-skill stat key.",
                nameof(attackSkillStatKey)
            );
        }

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
                    weaponHandKey,

                MinimumValue =
                    Math.Max(
                        0m,
                        Math.Min(
                            minimumWeaponDamage,
                            maximumWeaponDamage
                        )
                    ),

                MaximumValue =
                    Math.Max(
                        0m,
                        Math.Max(
                            minimumWeaponDamage,
                            maximumWeaponDamage
                        )
                    ),

                ScalingStatKey =
                    ForeverCombatStatKeys.AttackPower,

                ScalingCoefficient =
                    weaponSpeedSeconds /
                    AttackPowerPerWeaponDamagePerSecond,

                MitigationType =
                    DamageMitigationTypes.Armor
            };

        ForeverMeleeAttackProfiles.Apply(
            effect,
            usesDualWieldHitTable
                ? ForeverMeleeAttackProfiles.PlayerDualWieldAuto
                : ForeverMeleeAttackProfiles.PlayerAuto,
            attackSkillStatKey
        );

        return new AutoAttackDefinition
        {
            Key =
                key,

            Name =
                string.IsNullOrWhiteSpace(
                    name)
                    ? key
                    : name,

            SwingIntervalSeconds =
                weaponSpeedSeconds,

            WeaponHandKey =
                weaponHandKey,

            DamageMultiplier =
                damageMultiplier,

            DamageMultiplierStatKey =
                damageMultiplierStatKey,

            DamageEffect =
                effect
        };
    }
}
