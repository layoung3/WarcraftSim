using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Data.Forever.Characters;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Data.Forever.Abilities.Warrior;

/// <summary>
/// Creates Forever Whirlwind. The current beta makes dual-wield Whirlwind
/// strike with both weapons baseline. Each weapon effect independently rolls
/// against up to four active enemy targets. Exact 8-yard geometry is not yet
/// represented by the actor model; encounter composition controls which enemy
/// actors are active candidates for the area effect.
/// </summary>
public static class ForeverWhirlwindFactory
{
    public const string AbilityKey =
        "whirlwind";

    public const string RageResourceKey =
        "rage";

    public const int SpellId =
        1680;

    public const int RequiredLevel =
        36;

    public const decimal BaseRageCost =
        25m;

    public const decimal CooldownSeconds =
        10m;

    public const decimal GlobalCooldownSeconds =
        1.5m;

    public const int MaximumTargets =
        4;

    public static AbilityDefinition CreateSingleWeapon(
        decimal minimumMainHandWeaponDamage,
        decimal maximumMainHandWeaponDamage,
        decimal normalizedMainHandWeaponSpeedSeconds,
        string mainHandAttackSkillStatKey,
        decimal rageCostReduction = 0m)
    {
        ValidateWeaponInputs(
            normalizedMainHandWeaponSpeedSeconds,
            mainHandAttackSkillStatKey,
            nameof(normalizedMainHandWeaponSpeedSeconds),
            nameof(mainHandAttackSkillStatKey)
        );

        ValidateRageCostReduction(rageCostReduction);

        return CreateCore(
            minimumMainHandWeaponDamage,
            maximumMainHandWeaponDamage,
            normalizedMainHandWeaponSpeedSeconds,
            mainHandAttackSkillStatKey,
            offHand: null,
            rageCostReduction: rageCostReduction
        );
    }

    public static AbilityDefinition CreateDualWield(
        decimal minimumMainHandWeaponDamage,
        decimal maximumMainHandWeaponDamage,
        decimal normalizedMainHandWeaponSpeedSeconds,
        string mainHandAttackSkillStatKey,
        decimal minimumOffHandWeaponDamage,
        decimal maximumOffHandWeaponDamage,
        decimal normalizedOffHandWeaponSpeedSeconds,
        string offHandAttackSkillStatKey,
        decimal rageCostReduction = 0m)
    {
        ValidateWeaponInputs(
            normalizedMainHandWeaponSpeedSeconds,
            mainHandAttackSkillStatKey,
            nameof(normalizedMainHandWeaponSpeedSeconds),
            nameof(mainHandAttackSkillStatKey)
        );

        ValidateWeaponInputs(
            normalizedOffHandWeaponSpeedSeconds,
            offHandAttackSkillStatKey,
            nameof(normalizedOffHandWeaponSpeedSeconds),
            nameof(offHandAttackSkillStatKey)
        );

        ValidateRageCostReduction(rageCostReduction);

        return CreateCore(
            minimumMainHandWeaponDamage,
            maximumMainHandWeaponDamage,
            normalizedMainHandWeaponSpeedSeconds,
            mainHandAttackSkillStatKey,
            new OffHandDefinition(
                minimumOffHandWeaponDamage,
                maximumOffHandWeaponDamage,
                normalizedOffHandWeaponSpeedSeconds,
                offHandAttackSkillStatKey
            ),
            rageCostReduction
        );
    }

    private static AbilityDefinition CreateCore(
        decimal minimumMainHandWeaponDamage,
        decimal maximumMainHandWeaponDamage,
        decimal normalizedMainHandWeaponSpeedSeconds,
        string mainHandAttackSkillStatKey,
        OffHandDefinition? offHand,
        decimal rageCostReduction)
    {
        var effects =
            new List<AbilityEffectDefinition>
            {
                CreateWeaponDamageEffect(
                    "main-hand-damage",
                    WeaponHandKeys.MainHand,
                    minimumMainHandWeaponDamage,
                    maximumMainHandWeaponDamage,
                    normalizedMainHandWeaponSpeedSeconds,
                    mainHandAttackSkillStatKey,
                    damageMultiplier: 1m,
                    damageMultiplierStatKey: null
                )
            };

        if (offHand is not null)
        {
            effects.Add(
                CreateWeaponDamageEffect(
                    "off-hand-damage",
                    WeaponHandKeys.OffHand,
                    offHand.MinimumWeaponDamage,
                    offHand.MaximumWeaponDamage,
                    offHand.NormalizedWeaponSpeedSeconds,
                    offHand.AttackSkillStatKey,
                    damageMultiplier:
                        ForeverAutoAttackFactory.BaseOffHandDamageMultiplier,
                    damageMultiplierStatKey:
                        ForeverCombatStatKeys.OffHandDamagePercent
                )
            );
        }

        return new AbilityDefinition
        {
            Key = AbilityKey,
            Name = "Whirlwind",
            ClassKey = ForeverCharacterClassKeys.Warrior,
            RequiredLevel = RequiredLevel,
            CooldownSeconds = CooldownSeconds,
            GlobalCooldownSeconds = GlobalCooldownSeconds,
            RequiredSourceAuraKeys =
            [
                ForeverWarriorAuraKeys.BerserkerStance
            ],
            ResourceCosts =
            [
                new AbilityResourceCost
                {
                    ResourceKey = RageResourceKey,
                    Amount = Math.Max(0m, BaseRageCost - rageCostReduction)
                }
            ],
            Effects = effects
        };
    }

    private static AbilityEffectDefinition CreateWeaponDamageEffect(
        string key,
        string weaponHandKey,
        decimal minimumWeaponDamage,
        decimal maximumWeaponDamage,
        decimal normalizedWeaponSpeedSeconds,
        string attackSkillStatKey,
        decimal damageMultiplier,
        string? damageMultiplierStatKey)
    {
        var effect =
            new AbilityEffectDefinition
            {
                Key = key,
                EffectType = AbilityEffectTypes.DirectDamage,
                TargetType = AbilityTargetTypes.Enemy,
                WeaponHandKey = weaponHandKey,
                MinimumValue =
                    Math.Max(
                        0m,
                        Math.Min(minimumWeaponDamage, maximumWeaponDamage)
                    ),
                MaximumValue =
                    Math.Max(
                        0m,
                        Math.Max(minimumWeaponDamage, maximumWeaponDamage)
                    ),
                ScalingStatKey = ForeverCombatStatKeys.AttackPower,
                ScalingCoefficient =
                    normalizedWeaponSpeedSeconds /
                    ForeverAutoAttackFactory.AttackPowerPerWeaponDamagePerSecond,
                DamageMultiplier = damageMultiplier,
                DamageMultiplierStatKey = damageMultiplierStatKey,
                MitigationType = DamageMitigationTypes.Armor,
                MaxTargets = MaximumTargets
            };

        ForeverMeleeAttackProfiles.Apply(
            effect,
            ForeverMeleeAttackProfiles.PlayerSpecial,
            attackSkillStatKey
        );

        return effect;
    }

    private static void ValidateWeaponInputs(
        decimal normalizedWeaponSpeedSeconds,
        string attackSkillStatKey,
        string speedParameterName,
        string skillParameterName)
    {
        if (normalizedWeaponSpeedSeconds <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                speedParameterName,
                normalizedWeaponSpeedSeconds,
                "Normalized weapon speed must be greater than zero."
            );
        }

        if (string.IsNullOrWhiteSpace(attackSkillStatKey))
        {
            throw new ArgumentException(
                "Whirlwind requires a melee attack-skill stat key.",
                skillParameterName
            );
        }
    }

    private static void ValidateRageCostReduction(decimal rageCostReduction)
    {
        if (rageCostReduction < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rageCostReduction),
                rageCostReduction,
                "Whirlwind Rage-cost reduction cannot be negative."
            );
        }
    }

    private sealed record OffHandDefinition(
        decimal MinimumWeaponDamage,
        decimal MaximumWeaponDamage,
        decimal NormalizedWeaponSpeedSeconds,
        string AttackSkillStatKey);
}
