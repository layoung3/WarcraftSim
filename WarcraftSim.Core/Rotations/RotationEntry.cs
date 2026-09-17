namespace WarcraftSim.Core.Rotations;

public sealed class RotationEntry
{
    public string AbilityKey { get; set; } = "";

    public int Priority { get; set; }

    public bool IsEnabled { get; set; } = true;
}