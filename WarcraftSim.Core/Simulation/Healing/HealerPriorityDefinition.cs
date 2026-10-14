namespace WarcraftSim.Core.Simulation.Healing;

/// <summary>
/// Generic class-neutral health-priority decision rules. The spell must be
/// present on the healer actor; healing amounts and mana costs live on its
/// AbilityDefinition, not in the policy.
/// </summary>
public sealed class HealerPriorityDefinition
{
    public string HealerActorKey { get; set; } = "healer";
    public string EligibleTeamKey { get; set; } = "raid";
    public decimal DecisionIntervalSeconds { get; set; } = 0.25m;
    public List<HealerPriorityRule> Rules { get; set; } = [];
}

public sealed class HealerPriorityRule
{
    public string AbilityKey { get; set; } = "";
    public int Priority { get; set; }
    public decimal CastAtOrBelowHealthPercent { get; set; } = 80m;
    public decimal MinimumMissingHealth { get; set; } = 1m;
    // Optional guard against wasting HoTs, Prayer of Mending, or shields.
    public string? SkipWhenTargetHasAuraKey { get; set; }
    public string? SkipWhenTargetHasAbsorbKey { get; set; }
    // Limits group spells to scenarios with enough injured targets.
    public int MinimumInjuredTeamMembers { get; set; } = 1;
}
