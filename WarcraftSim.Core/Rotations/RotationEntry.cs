namespace WarcraftSim.Core.Rotations;

public sealed class RotationEntry
{
    public string ActionType { get; set; } =
        RotationActionTypes.Ability;

    public string AbilityKey { get; set; } = "";

    public string NextSwingReplacementKey { get; set; } = "";

    public string AutoAttackKey { get; set; } = "";

    public int Priority { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool InterruptCurrentCast { get; set; }
    public RotationTargetDefinition Target { get; set; } = new();
    public List<RotationConditionDefinition> Conditions { get; set; } = [];
}
