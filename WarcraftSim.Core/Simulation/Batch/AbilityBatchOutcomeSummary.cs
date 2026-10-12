namespace WarcraftSim.Core.Simulation.Batch;

/// <summary>
/// Per-iteration averages derived from resolved events across every seed, not
/// the representative fight. Periodic ticks are distinguished from direct attacks.
/// </summary>
public sealed class AbilityBatchOutcomeSummary
{
    public decimal AverageDamageOccurrences { get; set; }
    public decimal AverageCriticalDamageOccurrences { get; set; }
    public decimal AverageDirectOccurrences { get; set; }
    public decimal AveragePeriodicOccurrences { get; set; }
    public Dictionary<string, decimal> AverageResultCounts { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}
