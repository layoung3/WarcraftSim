namespace WarcraftSim.Core.Simulation.Engine;

/// <summary>A bounded simulation stopped because processors generated too many
/// events, commonly from same-timestamp wakeup loops.</summary>
public sealed class SimulationEventLimitExceededException : Exception
{
    public SimulationEventLimitExceededException(long limit, decimal elapsedSeconds,
        string? eventBreakdown = null)
        : base($"Simulation exceeded {limit:N0} combat events by {elapsedSeconds:0.###}s. " +
            (string.IsNullOrWhiteSpace(eventBreakdown) ? "" : $"Event breakdown: {eventBreakdown}. ") +
            "An ability or stance policy may be rescheduling events repeatedly; " +
            "reduce iterations to 1 and capture a representative timeline when diagnosing.")
    {
        Limit = limit;
        ElapsedSeconds = elapsedSeconds;
    }

    public long Limit { get; }
    public decimal ElapsedSeconds { get; }
}
