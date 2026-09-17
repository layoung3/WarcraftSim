namespace WarcraftSim.Core.Simulation.Engine;

public sealed class SimulationRunResult
{
    public SimulationRunSummary Summary { get; set; } = new();

    public List<CombatEvent> Timeline { get; set; } = [];
}