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
    // Optional tank damage forecast. Ignored unless an individual rule opts in.
    public string? ForecastTargetActorKey { get; set; }
    public decimal ForecastIncomingDamagePerSecond { get; set; }
    public decimal ForecastSeconds { get; set; } = 0m;
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
    public bool UseIncomingDamageForecast { get; set; }
    public decimal MinimumManaPercent { get; set; }
    public decimal EmergencyHealthPercent { get; set; } = 40m;
    // 100 => disabled. Only filters direct heals, not defensive absorbs.
    public decimal MaximumExpectedOverhealPercent { get; set; } = 100m;
    // Binding Heal should not self-heal a full-health caster.
    public decimal MaximumHealerHealthPercent { get; set; } = 100m;
    // When true, only consider a specific actor (for tank-only shield maintenance).
    public string? RequiredTargetActorKey { get; set; }
    public bool AllowUninjuredTargets { get; set; }
    // Useful for paired self-and-target spells (Binding Heal).
    public bool ExcludeHealerAsTarget { get; set; }

}
