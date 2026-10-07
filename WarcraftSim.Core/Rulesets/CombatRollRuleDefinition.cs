namespace WarcraftSim.Core.Rulesets;

public sealed class CombatRollRuleDefinition
{
    public string ResolutionType { get; set; } = "";

    public decimal BaseHitChancePercent { get; set; } = 100m;

    public string? HitChanceStatKey { get; set; }

    public string? TargetAvoidanceStatKey { get; set; }

    public decimal HitPenaltyPerHigherTargetLevelPercent { get; set; }

    // Dodge and parry chances are generic ruleset inputs. No game-specific
    // defaults or formulas are assumed by the engine.
    public decimal BaseDodgeChancePercent { get; set; }

    public string? TargetDodgeChanceStatKey { get; set; }

    public string? SourceDodgeReductionStatKey { get; set; }

    public decimal BaseParryChancePercent { get; set; }

    public string? TargetParryChanceStatKey { get; set; }

    public string? SourceParryReductionStatKey { get; set; }

    public decimal BaseCriticalChancePercent { get; set; }

    public string? CriticalChanceStatKey { get; set; }

    public string? TargetCriticalSuppressionStatKey { get; set; }

    public decimal CriticalMultiplier { get; set; } = 2m;
}
