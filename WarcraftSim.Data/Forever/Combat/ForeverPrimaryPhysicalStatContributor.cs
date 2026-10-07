using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation.Building;
using WarcraftSim.Core.Stats;

namespace WarcraftSim.Data.Forever.Combat;

/// <summary>
/// Derives the class-dependent physical stats exposed by the current
/// Forever client: Strength/Agility attack power, Agility ranged attack
/// power, Agility armor, and Agility critical chance.
///
/// Druid Cat Form's +1 attack power per Agility is deliberately not applied
/// here because it is form-specific runtime state, not a permanent character
/// stat conversion.
/// </summary>
public sealed class ForeverPrimaryPhysicalStatContributor :
    ICharacterSimulationStatContributor
{
    public string Key =>
        "forever-primary-physical-stat-conversions";

    public int Order =>
        CharacterSimulationStatContributorOrders.Derived;

    public void Contribute(
        CharacterProfile profile,
        StatCollection effectiveStats)
    {
        ArgumentNullException.ThrowIfNull(
            profile
        );

        ArgumentNullException.ThrowIfNull(
            effectiveStats
        );

        var classProfile =
            ForeverPrimaryPhysicalStatProfiles.GetRequired(
                profile.ClassKey
            );

        var strength =
            effectiveStats.Get(
                ForeverCombatStatKeys.Strength
            );

        var agility =
            effectiveStats.Get(
                ForeverCombatStatKeys.Agility
            );

        var attackPower =
            (
                strength *
                classProfile.AttackPowerPerStrength
            ) +
            (
                agility *
                classProfile.AttackPowerPerAgility
            );

        if (attackPower != 0m)
        {
            effectiveStats.Add(
                ForeverCombatStatKeys.AttackPower,
                attackPower
            );
        }

        var rangedAttackPower =
            agility *
            classProfile.RangedAttackPowerPerAgility;

        if (rangedAttackPower != 0m)
        {
            effectiveStats.Add(
                ForeverCombatStatKeys.RangedAttackPower,
                rangedAttackPower
            );
        }

        var agilityArmor =
            agility *
            ForeverPrimaryPhysicalStatProfiles.ArmorPerAgility;

        if (agilityArmor != 0m)
        {
            effectiveStats.Add(
                ForeverCombatStatKeys.Armor,
                agilityArmor
            );
        }

        if (agility == 0m)
        {
            return;
        }

        var agilityPerCriticalPercent =
            ForeverPrimaryPhysicalStatProfiles
                .GetAgilityPerCriticalPercent(
                    profile.ClassKey,
                    profile.Level
                );

        effectiveStats.Add(
            ForeverCombatStatKeys.AttackCriticalChancePercent,
            agility /
            agilityPerCriticalPercent
        );
    }
}
