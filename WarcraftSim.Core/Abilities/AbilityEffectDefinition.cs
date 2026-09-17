namespace WarcraftSim.Core.Abilities;

public sealed class AbilityEffectDefinition
{
    public string Key { get; set; } = "";

    public string EffectType { get; set; } = "";

    public string TargetType { get; set; } = AbilityTargetTypes.Enemy;

    public string? SchoolKey { get; set; }

    public decimal MinimumValue { get; set; }

    public decimal MaximumValue { get; set; }

    public string? ScalingStatKey { get; set; }

    public decimal ScalingCoefficient { get; set; }

    public decimal? DurationSeconds { get; set; }

    public decimal? TickIntervalSeconds { get; set; }

    public int MaxTargets { get; set; } = 1;

    public string? CustomMechanicKey { get; set; }

    public List<string> Tags { get; set; } = [];
}