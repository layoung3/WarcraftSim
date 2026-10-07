namespace WarcraftSim.Core.Simulation.Engine;

public sealed class ThreatManipulationResult
{
    public decimal PreviousThreat { get; init; }

    public decimal CurrentThreat { get; init; }

    public decimal Delta =>
        CurrentThreat -
        PreviousThreat;
}
