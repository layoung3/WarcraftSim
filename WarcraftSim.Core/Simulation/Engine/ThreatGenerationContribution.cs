namespace WarcraftSim.Core.Simulation.Engine;

public sealed class ThreatGenerationContribution
{
    public string ThreatOwnerActorKey { get; init; } = "";

    public string ThreatSourceActorKey { get; init; } = "";

    public decimal Amount { get; init; }
}
