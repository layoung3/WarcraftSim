namespace WarcraftSim.Core.Simulation.Engine;

public sealed class SimulationRunOptions
{
    public int Seed { get; set; }

    public decimal DurationSeconds { get; set; } = 180m;

    public string PrimaryActorKey { get; set; } = "player";

    public bool CaptureTimeline { get; set; }

    // Opt-in safety limit; zero leaves the generic engine unrestricted.
    // Single-character preview runners should configure a finite budget.
    public long MaximumProcessedEvents { get; set; }

    public CancellationToken CancellationToken { get; set; }
}