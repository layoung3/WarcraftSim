namespace WarcraftSim.Core.Encounters;

public sealed class TargetProximityLink
{
    public string TargetAKey { get; set; } = "";

    public string TargetBKey { get; set; } = "";

    public bool InCleaveRange { get; set; } = true;
}