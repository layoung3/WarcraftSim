namespace WarcraftSim.Core.Simulation.Building;

public sealed class SimulationResourceBuildDefinition
{
    public string ResourceKey { get; set; } = "";

    public decimal Maximum { get; set; }

    public decimal StartingValue { get; set; }

    public decimal RegenerationPerSecond { get; set; }
}
