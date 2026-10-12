namespace WarcraftSim.Core.Simulation;

public static class ResourceChangeOperationTypes
{
    public const string Gain =
        "gain";

    public const string Spend =
        "spend";

    public const string Set =
        "set";

    // Reduce current resource to at most the configured amount; never grants it.
    public const string Cap = "cap";
}
