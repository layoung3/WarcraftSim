using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Core.Simulation.Batch;

public sealed class SimulationBatchResult
{
    public SimulationBatchSummary Summary { get; set; } = new();

    public int RepresentativeSeed { get; set; }

    public SimulationRunResult RepresentativeRun { get; set; } = new();
}
