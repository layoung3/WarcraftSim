namespace WarcraftSim.Core.Rotations;

public sealed class RotationConditionDefinition
{
    public string ConditionType { get; set; } = "";

    public string ComparisonOperator { get; set; } =
        RotationComparisonOperators.GreaterThanOrEqual;

    // Resource key or aura key when the condition type needs one.
    public string? Key { get; set; }

    // Numeric value for health/resource/time comparisons.
    // Aura active/missing conditions do not use this value.
    public decimal? Value { get; set; }
}
