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

    public string ThreatOperation { get; set; } =
        ThreatManipulationOperationTypes.Add;

    // Optional threat operation performed by a taunt before the
    // forced-target override is applied. Null/blank means that the
    // taunt changes targeting only and leaves the threat table unchanged.
    public string? TauntThreatOperation { get; set; }

    public bool CanMiss { get; set; } = true;

    // These flags are explicit rather than inferred from resolution type.
    // Ruleset data decides which attacks can be dodged or parried.
    public bool CanBeDodged { get; set; }

    public bool CanBeParried { get; set; }

    // Block is a landed result that partially reduces damage rather than
    // fully avoiding the attack. Ruleset data decides which attacks can block.
    public bool CanBeBlocked { get; set; }

    public bool CanCrit { get; set; } = true;

    public decimal MinimumValue { get; set; }

    public decimal MaximumValue { get; set; }

    public string? ScalingStatKey { get; set; }

    public decimal ScalingCoefficient { get; set; }

    public decimal? DurationSeconds { get; set; }

    public decimal? TickIntervalSeconds { get; set; }

    // Periodic auras use a half-open lifetime by default. A ruleset or
    // ability can explicitly opt in to a regular tick that lands exactly
    // on the aura expiration boundary.
    public bool IncludeExpirationBoundaryTick { get; set; }

    public decimal TravelTimeSeconds { get; set; }

    public bool ApplyOnChannelTick { get; set; }

    public int MaxTargets { get; set; } = 1;

    public string? AuraKey { get; set; }

    public string? ResourceKey { get; set; }

    public string? AbsorbKey { get; set; }

    public AbsorbStackingMode AbsorbStackingMode { get; set; } =
        AbsorbStackingMode.Refresh;

    public string ResourceChangeOperation { get; set; } =
        ResourceChangeOperationTypes.Gain;

    public bool ResourceAmountIsPercentOfMaximum { get; set; }

    // When removing an aura, restrict removal to instances originally
    // applied by the actor using this ability.
    public bool RemoveAuraOnlyFromSource { get; set; }

    public AuraStackingMode AuraStackingMode { get; set; } =
        AuraStackingMode.Refresh;

    public int MaxStacks { get; set; } = 1;

    public string? DependsOnEffectKey { get; set; }

    public string? DependencyCondition { get; set; }

    public string? CustomMechanicKey { get; set; }

    public List<string> Tags { get; set; } = [];
}
