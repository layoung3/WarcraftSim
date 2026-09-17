namespace WarcraftSim.Core.Simulation.Engine;

public sealed class SimulationRunOptions
{
    public int Seed { get; set; }

    public decimal DurationSeconds { get; set; } = 180m;

    public string PrimaryActorKey { get; set; } = "player";

    public bool CaptureTimeline { get; set; }
}