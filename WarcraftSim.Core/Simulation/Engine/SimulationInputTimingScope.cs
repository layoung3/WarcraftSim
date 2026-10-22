namespace WarcraftSim.Core.Simulation.Engine;

/// <summary>
/// Per-run, asynchronous call-context input latency. API middleware supplies this
/// setting without changing every specialization request contract. Direct engine
/// runs and unit tests remain deterministic and unchanged unless explicitly scoped.
/// </summary>
public static class SimulationInputTimingScope
{
    private static readonly System.Threading.AsyncLocal<decimal?> AmbientDelaySeconds = new();

    /// <summary>Additional reaction/input delay applied between rotation actions, in seconds.</summary>
    public static decimal? AdditionalDelaySeconds
    {
        get => AmbientDelaySeconds.Value;
        set => AmbientDelaySeconds.Value = value;
    }
}
