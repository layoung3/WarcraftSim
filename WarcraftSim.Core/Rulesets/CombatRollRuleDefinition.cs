namespace WarcraftSim.Core.Rulesets;

public sealed class CombatRollRuleDefinition
{
    public string ResolutionType { get; set; } = "";

    public decimal BaseHitChancePercent { get; set; } = 100m;

    public string? HitChanceStatKey { get; set; }

    public string? TargetAvoidanceStatKey { get; set; }

    public decimal HitPenaltyPerHigherTargetLevelPercent { get; set; }

    public decimal BaseCriticalChancePercent { get; set; }

    public string? CriticalChanceStatKey { get; set; }

    public string? TargetCriticalSuppressionStatKey { get; set; }

    public decimal CriticalMultiplier { get; set; } = 2m;
}
