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
}
