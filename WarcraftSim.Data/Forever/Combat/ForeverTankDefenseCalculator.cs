using WarcraftSim.Core.Stats;

namespace WarcraftSim.Data.Forever.Combat;

/// <summary>Ruleset conversion independent of a specific tank class or raid runner.
/// Takes already aggregated equipment, enchant, set, buff and primary stat contributions.
/// Defense-derived combat table adjustments are intentionally NOT added here: the
/// ForeverDefenseCombatRollAdjustmentProvider applies them once during a roll.
/// </summary>
public static class ForeverTankDefenseCalculator
{
    public static ForeverTankDefenseSnapshot Calculate(
        StatCollection effectiveStats, bool shieldEquipped,
        decimal baseDodgePercent, decimal baseParryPercent, decimal baseBlockPercent,
        decimal shieldIntrinsicBlockValue = 0m, decimal additionalParryPercent = 0m,
        decimal additionalBlockPercent = 0m, decimal additionalDefenseSkill = 0m,
        decimal provisionalDodgePercentPerAgility = 0m)
    {
        ArgumentNullException.ThrowIfNull(effectiveStats);
        return new ForeverTankDefenseSnapshot(
            DodgePercent: Math.Clamp(baseDodgePercent +
                effectiveStats.Get(ForeverCombatStatKeys.DodgeChancePercent) +
                effectiveStats.Get(ForeverCombatStatKeys.Agility) * provisionalDodgePercentPerAgility, 0m, 100m),
            ParryPercent: Math.Clamp(baseParryPercent +
                effectiveStats.Get(ForeverCombatStatKeys.ParryChancePercent) + additionalParryPercent, 0m, 100m),
            BlockPercent: shieldEquipped ? Math.Clamp(baseBlockPercent +
                effectiveStats.Get(ForeverCombatStatKeys.BlockChancePercent) + additionalBlockPercent, 0m, 100m) : 0m,
            BlockValue: shieldEquipped ? Math.Max(0m,
                shieldIntrinsicBlockValue + effectiveStats.Get(ForeverCombatStatKeys.BlockValue)) : 0m,
            BonusDefenseSkill: effectiveStats.Get(ForeverCombatStatKeys.DefenseSkill) + additionalDefenseSkill);
    }
}

public sealed record ForeverTankDefenseSnapshot(
    decimal DodgePercent, decimal ParryPercent, decimal BlockPercent,
    decimal BlockValue, decimal BonusDefenseSkill);
