namespace WarcraftSim.Core.Rotations;

public static class RotationTargetSelectionModes
{
    public const string Default = "default";
    public const string Self = "self";
    public const string Fixed = "fixed";
    public const string LowestHealthAlly = "lowest-health-ally";
    public const string FixedThenLowestHealthAlly = "fixed-then-lowest-health-ally";
}
