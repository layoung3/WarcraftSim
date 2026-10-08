namespace WarcraftSim.Core.Simulation.Engine;

/// <summary>
/// Per-ability observability captured from dispatched combat events.
/// This intentionally records counts and actual resolved amounts rather than
/// re-deriving outcomes later from the optional timeline.
/// </summary>
public sealed class AbilityCombatSummary
{
    public string AbilityKey { get; set; } = "";

    public int CastStartedCount { get; set; }

    public int CastCompletedCount { get; set; }

    public int CastCancelledCount { get; set; }

    public int ChannelStartedCount { get; set; }

    public int ChannelCompletedCount { get; set; }

    public int ChannelCancelledCount { get; set; }

    public int DamageOccurrenceCount { get; set; }

    public int HealingOccurrenceCount { get; set; }

    public int AbsorbConsumptionCount { get; set; }

    public int CriticalDamageCount { get; set; }

    public int CriticalHealingCount { get; set; }

    public int DirectOccurrenceCount { get; set; }

    public int PeriodicOccurrenceCount { get; set; }

    public int ChannelTickOccurrenceCount { get; set; }

    public decimal DamageDone { get; set; }

    public decimal HealingDone { get; set; }

    public decimal OverhealingDone { get; set; }

    public decimal AbsorptionDone { get; set; }

    public Dictionary<string, int> ResultCounts { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}
