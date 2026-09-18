namespace WarcraftSim.Core.Simulation.Batch;

public sealed class SimulationBatchOptions
{
    public int Iterations { get; set; } = 1000;

    public int BaseSeed { get; set; } = 12345;

    public SimulationType SimulationType { get; set; } =
        SimulationType.Dps;
}
