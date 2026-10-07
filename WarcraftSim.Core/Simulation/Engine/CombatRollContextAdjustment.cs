namespace WarcraftSim.Core.Simulation.Engine;

/// <summary>
/// Contextual changes to a combat roll after the base ruleset/static stats
/// are considered. This keeps game-specific formulas out of the generic
/// simulation engine.
/// </summary>
public sealed class CombatRollContextAdjustment
{
    public static CombatRollContextAdjustment None { get; } =
        new();

    public decimal HitChancePercentDelta { get; init; }

    public decimal DodgeChancePercentDelta { get; init; }

    public decimal ParryChancePercentDelta { get; init; }

    public decimal BlockChancePercentDelta { get; init; }

    public decimal CriticalChancePercentDelta { get; init; }

    public decimal? GlancingChancePercentOverride { get; init; }

    public decimal? CrushingChancePercentOverride { get; init; }

    public static CombatRollContextAdjustment Combine(
        IEnumerable<CombatRollContextAdjustment> adjustments)
    {
        ArgumentNullException.ThrowIfNull(
            adjustments
        );

        decimal hit =
            0m;

        decimal dodge =
            0m;

        decimal parry =
            0m;

        decimal block =
            0m;

        decimal critical =
            0m;

        decimal? glancingOverride =
            null;

        decimal? crushingOverride =
            null;

        foreach (
            var adjustment in
            adjustments)
        {
            ArgumentNullException.ThrowIfNull(
                adjustment
            );

            hit +=
                adjustment.HitChancePercentDelta;

            dodge +=
                adjustment.DodgeChancePercentDelta;

            parry +=
                adjustment.ParryChancePercentDelta;

            block +=
                adjustment.BlockChancePercentDelta;

            critical +=
                adjustment.CriticalChancePercentDelta;

            if (
                adjustment.GlancingChancePercentOverride
                    .HasValue)
            {
                glancingOverride =
                    adjustment.GlancingChancePercentOverride;
            }

            if (
                adjustment.CrushingChancePercentOverride
                    .HasValue)
            {
                crushingOverride =
                    adjustment.CrushingChancePercentOverride;
            }
        }

        if (
            hit == 0m &&
            dodge == 0m &&
            parry == 0m &&
            block == 0m &&
            critical == 0m &&
            !glancingOverride.HasValue &&
            !crushingOverride.HasValue)
        {
            return None;
        }

        return new CombatRollContextAdjustment
        {
            HitChancePercentDelta =
                hit,

            DodgeChancePercentDelta =
                dodge,

            ParryChancePercentDelta =
                parry,

            BlockChancePercentDelta =
                block,

            CriticalChancePercentDelta =
                critical,

            GlancingChancePercentOverride =
                glancingOverride,

            CrushingChancePercentOverride =
                crushingOverride
        };
    }
}
