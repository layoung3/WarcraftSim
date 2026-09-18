using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Core.Abilities;

public sealed class AbilityEffectDefinition
{
    public string Key { get; set; } = "";

    public string EffectType { get; set; } = "";

    public string TargetType { get; set; } =
        AbilityTargetTypes.Enemy;

    public string? SchoolKey { get; set; }

    public string ResolutionType { get; set; } =
        CombatResolutionTypes.Spell;

    public string MitigationType { get; set; } =
        DamageMitigationTypes.None;

    public bool CanMiss { get; set; } = true;

    public bool CanCrit { get; set; } = true;

    public decimal MinimumValue { get; set; }

    public decimal MaximumValue { get; set; }

    public string? ScalingStatKey { get; set; }

    public decimal ScalingCoefficient { get; set; }

    public decimal? DurationSeconds { get; set; }

    public decimal? TickIntervalSeconds { get; set; }

    public decimal TravelTimeSeconds { get; set; }

    public int MaxTargets { get; set; } = 1;

    public string? AuraKey { get; set; }

    public AuraStackingMode AuraStackingMode { get; set; } =
        AuraStackingMode.Refresh;

    public int MaxStacks { get; set; } = 1;

    public string? DependsOnEffectKey { get; set; }

    public string? DependencyCondition { get; set; }

    public string? CustomMechanicKey { get; set; }

    public List<string> Tags { get; set; } = [];
}
