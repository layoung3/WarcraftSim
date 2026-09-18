namespace WarcraftSim.Core.Rotations;

public sealed class RotationTargetDefinition
{
    public string Mode { get; set; } = RotationTargetSelectionModes.Default;
    public string? ActorKey { get; set; }
    public bool IncludeSelf { get; set; } = true;
}
