namespace WarcraftSim.Data.Forever.Druid;

/// <summary>
/// Isolated Forever 70450 Heart of the Wild deltas and Thick Hide level/Defense
/// armor. Existing Feral tuning totals already include normal Strength/Stamina/
/// Intellect contributions; only the incremental talent-derived amount is added.
/// Exact starting stat budgets and form armor multipliers remain configurable.
/// </summary>
public static class ForeverDruidDerivedStats
{
    public static (decimal AttackPower, decimal Health, decimal Mana) HeartOfTheWild(
        ForeverFeralDruidTuning tuning, int rank, ForeverDruidForm form,
        decimal maximumHealth, decimal maximumMana)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        if (rank is < 0 or > 5) throw new ArgumentOutOfRangeException(nameof(rank));
        var strengthBonus = form == ForeverDruidForm.Cat
            ? tuning.BaseStrength * .02m * rank * tuning.AttackPowerPerStrength : 0m;
        var staminaBonus = form is ForeverDruidForm.Bear or ForeverDruidForm.DireBear
            ? tuning.BaseStamina * .04m * rank * tuning.HealthPerStamina : 0m;
        var intellectBonus = tuning.BaseIntellect * .02m * rank * tuning.ManaPerIntellect;
        return (tuning.AttackPower + strengthBonus, maximumHealth + staminaBonus,
            maximumMana + intellectBonus);
    }

    /// <summary>Forever Thick Hide (70450): per rank and level, plus Defense above 5*level.
    /// Form multiplier is explicitly configurable; do not pretend it is a verified coefficient.</summary>
    public static decimal ThickHideBonusArmor(int rank, int level, decimal defenseSkill,
        decimal formArmorMultiplier)
    {
        if (rank is < 0 or > 3 || level <= 0 || defenseSkill < 0m || formArmorMultiplier <= 0m)
            throw new ArgumentOutOfRangeException(nameof(rank));
        var defenseAboveBase = Math.Max(0m, defenseSkill - 5m * level);
        // Ranks have tooltip values 0.67 / 1.33 / 2.00; full rank is exactly 2.
        decimal perDefense = rank switch { 1 => .67m, 2 => 1.33m, 3 => 2m, _ => 0m };
        return (level * rank + defenseAboveBase * perDefense) * formArmorMultiplier;
    }
}
