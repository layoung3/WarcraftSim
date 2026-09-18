using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Core.Simulation.Analysis;

public sealed class HealingThroughputResult
{
    public HealingThroughputMetrics Metrics { get; set; } = new();

    public SimulationRunResult Simulation { get; set; } = new();
}
