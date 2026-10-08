using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Data.Forever.Characters;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Data.Forever.Abilities.Warrior;

/// <summary>
/// Creates Forever Slam from client-visible rank data. Baseline Slam has a
/// cast time and interrupts the main-hand swing timer; talent-driven Improved
/// Slam behavior can remove or alter that interaction later without changing
/// Slam's base definition.
/// </summary>
public static class ForeverSlamFactory
{
    public const string AbilityKey =
        "slam";

    public const string RageResourceKey =
        "rage";

    public static AbilityDefinition CreateForLevel(
        int characterLevel,
        decimal minimumWeaponDamage,
        decimal maximumWeaponDamage,
        decimal weaponSpeedSeconds,
        string attackSkillStatKey)
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
                "Slam requires a melee attack-skill stat key.",
                nameof(attackSkillStatKey)
            );
        }

        var rank =
            ForeverSlamRanks.GetHighestAvailable(characterLevel);

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

        return new AbilityDefinition
        {
            Key = AbilityKey,
            Name = $"Slam (Rank {rank.Rank})",
            ClassKey = ForeverCharacterClassKeys.Warrior,
            RequiredLevel = rank.RequiredLevel,
            CooldownSeconds = rank.CooldownSeconds,
            GlobalCooldownSeconds = rank.GlobalCooldownSeconds,
            CastTimeSeconds = rank.CastTimeSeconds,
            ResourceCosts =
            [
                new AbilityResourceCost
                {
                    ResourceKey = RageResourceKey,
                    Amount = rank.RageCost
                }
            ],
            DelayedAutoAttackWeaponHandKeys =
            [
                WeaponHandKeys.MainHand
            ],
            Effects =
            [
                effect
            ]
        };
    }
}
