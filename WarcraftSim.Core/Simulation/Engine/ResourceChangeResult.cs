namespace WarcraftSim.Core.Simulation.Engine;

public sealed class ResourceChangeResult
{
    public decimal PreviousValue { get; init; }

    public decimal CurrentValue { get; init; }

    public decimal Delta =>
        CurrentValue -
        PreviousValue;
}
