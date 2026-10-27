using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Data.Forever.Combat;

/// <summary>
/// October 20, 2026 pet-hit developer clarification (forever-bugs #394).
/// These values are inherited BONUS hit percentage points, never final hit
/// chances. The target-level miss tables are applied independently.
/// </summary>
public static class ForeverCompanionHitInheritance
{
    public const decimal HunterPetSpellToMeleeFactor = 17m / 8m;
    public const decimal WarlockDemonMeleeToSpellFactor = 8m / 17m;

    public static decimal HunterMeleeBonus(decimal ownerRangedBonus) =>
        Math.Max(0m, ownerRangedBonus);

    public static decimal HunterSpellBonus(decimal ownerRangedBonus) =>
        HunterMeleeBonus(ownerRangedBonus) * HunterPetSpellToMeleeFactor;

    public static decimal WarlockSpellBonus(decimal ownerSpellBonus) =>
        Math.Max(0m, ownerSpellBonus);

    public static decimal WarlockMeleeBonus(decimal ownerSpellBonus) =>
        WarlockSpellBonus(ownerSpellBonus) * WarlockDemonMeleeToSpellFactor;

    // The shared creature melee table uses 95% equal-level hit and the
    // current creature level adjustment of 0.04% per skill point (5/level).
    // The latter is a provisional Forever server coefficient, not a pet
    // inheritance rule; keep it independently configurable by rulesets.
    public static decimal CreatureMeleeBaseHit(int sourceLevel, int targetLevel) =>
        95m + (sourceLevel - targetLevel) *
        ForeverCreatureLevelCombatRollAdjustmentProvider.SkillPointsPerLevel *
        ForeverCreatureLevelCombatRollAdjustmentProvider.PercentPerSkillPoint;

    // The Stage 3 spell table currently assumes 4% equal-level miss and
    // 5%/6%/17% miss at +1/+2/+3. No invented new cap is applied.
    public static decimal SpellBaseHit(int sourceLevel, int targetLevel)
    {
        var higher = Math.Max(0, targetLevel - sourceLevel);
        var penalty = Math.Min(2, higher) *
            ForeverSpellLevelCombatRollAdjustmentProvider.MissPenaltyPerHigherLevelThroughTwo +
            Math.Max(0, higher - 2) *
            ForeverSpellLevelCombatRollAdjustmentProvider.MissPenaltyPerHigherLevelAfterTwo;
        return 96m - penalty;
    }

    public static decimal FinalHit(decimal baseHit, decimal inheritedBonus) =>
        Math.Clamp(baseHit + inheritedBonus, 0m, 100m);

    public static decimal OwnerSpellHitBonus(SimulationActorState owner) =>
        Math.Max(0m, owner.Stats.Get("spell-hit-percent") +
            owner.Stats.Get(ForeverCombatStatKeys.SpellHitChancePercent) +
            owner.Stats.Get(ForeverCombatStatKeys.HitChancePercent));
}
