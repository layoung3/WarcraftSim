namespace WarcraftSim.Core.Simulation.Engine;

public sealed class SimulationRunResult
{
    public SimulationRunSummary Summary { get; set; } = new();

    public List<CombatEvent> Timeline { get; set; } = [];

    // Includes internal scheduler events; useful for diagnosing runaway policies.
    public long ProcessedEventCount { get; set; }
}