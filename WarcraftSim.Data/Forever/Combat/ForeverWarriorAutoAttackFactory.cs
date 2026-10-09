using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Data.Forever.Combat;

/// <summary>
/// Creates Warrior basic attacks with the normalized Rage generation currently
/// observed in the Forever beta. The baseline coefficients are isolated here
/// because they are server behavior rather than generic weapon rules.
/// </summary>
public static class ForeverWarriorAutoAttackFactory
{
    public const string RageResourceKey = "rage";

    // Current beta combat-log observations. These are intentionally kept in
    // Forever data rather than the generic simulation engine.
    public const decimal OneHandedMainHandRagePerSecond = 3.46m;
    public const decimal TwoHandedMainHandRagePerSecond = 4.50m;
    public const decimal OffHandRagePerSecond = 1.73m;

    // Current logs show that the "100% additional Rage" from a basic-attack
    // critical is another normalized base share. For a one-hander this is the
    // full 3.46x-speed share. For a two-hander it is still 3.46x-speed, rather
    // than doubling the separate 4.50x-speed two-handed swing amount.
    public const decimal OneHandedCriticalBonusRagePerSecond = 3.46m;
    public const decimal TwoHandedCriticalBonusRagePerSecond = 3.46m;

    // Off-hand critical normalization has not yet been independently logged.
    // Keep the doubled half-rate as an isolated provisional value.
    public const decimal OffHandCriticalBonusRagePerSecond = 1.73m;

    public static AutoAttackDefinition CreateOneHandedMainHand(
        string key,
        string name,
        decimal minimumWeaponDamage,
        decimal maximumWeaponDamage,
        decimal weaponSpeedSeconds,
        string attackSkillStatKey)
    {
        var definition =
            ForeverAutoAttackFactory.CreatePlayerMelee(
                key,
                name,
                minimumWeaponDamage,
                maximumWeaponDamage,
                weaponSpeedSeconds,
                attackSkillStatKey
            );

        AddNormalizedRage(
            definition,
            OneHandedMainHandRagePerSecond,
            OneHandedCriticalBonusRagePerSecond
        );

        return definition;
    }

    public static AutoAttackDefinition CreateTwoHandedMainHand(
        string key,
        string name,
        decimal minimumWeaponDamage,
        decimal maximumWeaponDamage,
        decimal weaponSpeedSeconds,
        string attackSkillStatKey)
    {
        var definition =
            ForeverAutoAttackFactory.CreatePlayerMelee(
                key,
                name,
                minimumWeaponDamage,
                maximumWeaponDamage,
                weaponSpeedSeconds,
                attackSkillStatKey
            );

        AddNormalizedRage(
            definition,
            TwoHandedMainHandRagePerSecond,
            TwoHandedCriticalBonusRagePerSecond
        );

        return definition;
    }

    public static AutoAttackDefinition CreateDualWieldMainHand(
        string key,
        string name,
        decimal minimumWeaponDamage,
        decimal maximumWeaponDamage,
        decimal weaponSpeedSeconds,
        string attackSkillStatKey)
    {
        var definition =
            ForeverAutoAttackFactory.CreatePlayerDualWieldMainHand(
                key,
                name,
                minimumWeaponDamage,
                maximumWeaponDamage,
                weaponSpeedSeconds,
                attackSkillStatKey
            );

        AddNormalizedRage(
            definition,
            OneHandedMainHandRagePerSecond,
            OneHandedCriticalBonusRagePerSecond
        );

        return definition;
    }

    public static AutoAttackDefinition CreateDualWieldOffHand(
        string key,
        string name,
        decimal minimumWeaponDamage,
        decimal maximumWeaponDamage,
        decimal weaponSpeedSeconds,
        string attackSkillStatKey)
    {
        var definition =
            ForeverAutoAttackFactory.CreatePlayerDualWieldOffHand(
                key,
                name,
                minimumWeaponDamage,
                maximumWeaponDamage,
                weaponSpeedSeconds,
                attackSkillStatKey
            );

        AddNormalizedRage(
            definition,
            OffHandRagePerSecond,
            OffHandCriticalBonusRagePerSecond
        );

        return definition;
    }

    private static void AddNormalizedRage(
        AutoAttackDefinition definition,
        decimal ragePerSecond,
        decimal criticalBonusRagePerSecond)
    {
        definition.ResourceGenerations.Add(
            new AutoAttackResourceGenerationDefinition
            {
                ResourceKey = RageResourceKey,
                AmountPerLandedSwing =
                    definition.SwingIntervalSeconds *
                    ragePerSecond,
                CriticalMultiplier = 1m,
                CriticalBonusAmountPerLandedSwing =
                    definition.SwingIntervalSeconds *
                    criticalBonusRagePerSecond
            }
        );
    }
}
