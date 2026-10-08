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

    // Blizzard's current Forever notes state that a basic-attack critical
    // strike generates 100% additional Rage.
    public const decimal CriticalRageMultiplier = 2m;

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
            OneHandedMainHandRagePerSecond
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
            TwoHandedMainHandRagePerSecond
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
            OneHandedMainHandRagePerSecond
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
            OffHandRagePerSecond
        );

        return definition;
    }

    private static void AddNormalizedRage(
        AutoAttackDefinition definition,
        decimal ragePerSecond)
    {
        definition.ResourceGenerations.Add(
            new AutoAttackResourceGenerationDefinition
            {
                ResourceKey = RageResourceKey,
                AmountPerLandedSwing =
                    definition.SwingIntervalSeconds *
                    ragePerSecond,
                CriticalMultiplier =
                    CriticalRageMultiplier
            }
        );
    }
}
