namespace WarcraftSim.Core.Rotations;

public sealed class RotationEntry
{
    public string AbilityKey { get; set; } = "";
    public int Priority { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool InterruptCurrentCast { get; set; }
    public RotationTargetDefinition Target { get; set; } = new();
    public List<RotationConditionDefinition> Conditions { get; set; } = [];
}
