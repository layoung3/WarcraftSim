using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation.Building;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Data.Forever.Priest;

/// <summary>
/// CharacterProfile-backed Priest stats. Reuses the shared base/equipment/
/// enchant/buff/talent stat pipeline. Level-60 values from the Forever beta
/// client are starting points, not verified live-server outcomes. Spirit
/// regeneration and healing spell coefficients remain explicit assumptions.
/// </summary>
public static class ForeverPriestCharacterCalculator
{
    public static ForeverPriestCalculatedStats Calculate(
        CharacterProfile profile, ForeverPriestStatAssumptions? assumptions = null,
        int mentalStrengthRanks = 0, int spiritualGuidanceRanks = 0)
    {
        ArgumentNullException.ThrowIfNull(profile);
        assumptions ??= new ForeverPriestStatAssumptions();
        if (!string.Equals(profile.ClassKey, "priest", StringComparison.OrdinalIgnoreCase) ||
            profile.Level < 1 || profile.Level > 60 ||
            mentalStrengthRanks is < 0 or > 5 || spiritualGuidanceRanks is < 0 or > 5 ||
            assumptions.BaseMana < 0m || assumptions.IntellectPerSpellCritPercent <= 0m ||
            assumptions.ManaPerIntellectAboveTwenty < 0m ||
            assumptions.SpiritManaRegenerationPerSecondPerPoint < 0m ||
            assumptions.CriticalRatingPerPercent <= 0m)
            throw new ArgumentException("Invalid Priest character or conversion assumptions.");

        var calculation = new BaseStatsCharacterSimulationRuntimeCalculator(
            _ => 1m,
            statContributors: CharacterSimulationStatContributorPipeline.CreateStandard())
            .Calculate(profile);
        var stats = calculation.EffectiveStats;
        // October 15 beta: Mental Strength grants Intellect (2% per rank),
        // not a generic multiplier to the final Mana pool. Apply it before
        // calculating derived Mana and spell critical chance; bonus flat Mana
        // from items must not be multiplied by this talent.
        decimal intellect = Math.Max(0m, stats.Get(ForeverCombatStatKeys.Intellect)) *
            (1m + mentalStrengthRanks * .02m);
        stats.Set(ForeverCombatStatKeys.Intellect, intellect);
        decimal spirit = Math.Max(0m, stats.Get("spirit"));
        decimal manaFromIntellect = Math.Min(20m, intellect) +
            Math.Max(0m, intellect - 20m) * assumptions.ManaPerIntellectAboveTwenty;
        decimal maximumMana = assumptions.BaseMana + manaFromIntellect +
            stats.Get("mana");
        decimal healingPower = stats.Get("healing-power") +
            spirit * spiritualGuidanceRanks * .05m;
        decimal crit = Math.Max(0m, intellect / assumptions.IntellectPerSpellCritPercent +
            stats.Get(ForeverCombatStatKeys.SpellCriticalChancePercent) +
            stats.Get(ForeverCombatStatKeys.CriticalStrikeRating) /
                assumptions.CriticalRatingPerPercent +
            stats.Get("healing-crit-chance-percent"));
        decimal spiritRegen = spirit * assumptions.SpiritManaRegenerationPerSecondPerPoint;
        decimal mp5 = Math.Max(0m, stats.Get("mp5") + stats.Get("mana-per-5-seconds"));
        if (maximumMana <= 0m || healingPower < 0m)
            throw new ArgumentException("Derived maximum Mana must be positive and bonus healing cannot be negative.");
        return new ForeverPriestCalculatedStats
        {
            EffectiveStats = new Dictionary<string, decimal>(stats.Values,
                StringComparer.OrdinalIgnoreCase),
            MaximumMana = maximumMana, HealingPower = healingPower,
            HealingCritPercent = crit, SpiritRegenerationPerSecond = spiritRegen,
            ManaPerFiveSeconds = mp5
        };
    }
}

public sealed class ForeverPriestStatAssumptions
{
    public decimal BaseMana { get; set; } = 1376m; // Level-60 client table; provisional at max level.
    public decimal ManaPerIntellectAboveTwenty { get; set; } = 15m;
    public decimal IntellectPerSpellCritPercent { get; set; } = 59.5m;
    public decimal CriticalRatingPerPercent { get; set; } = 14m;
    // Unknown server formula. Override or calibrate against game logs.
    public decimal SpiritManaRegenerationPerSecondPerPoint { get; set; } = .075m;
}

public sealed class ForeverPriestCalculatedStats
{
    public Dictionary<string, decimal> EffectiveStats { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public decimal MaximumMana { get; init; }
    public decimal HealingPower { get; init; }
    public decimal HealingCritPercent { get; init; }
    public decimal SpiritRegenerationPerSecond { get; init; }
    public decimal ManaPerFiveSeconds { get; init; }
}
