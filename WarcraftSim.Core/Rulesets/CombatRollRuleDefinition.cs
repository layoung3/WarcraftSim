namespace WarcraftSim.Core.Rulesets;

public sealed class CombatRollRuleDefinition
{
    public string ResolutionType { get; set; } = "";

    // Classic-style physical attack tables use one shared roll for ordered
    // outcomes. Other resolution types may keep the existing staged model.
    public bool UseSingleRollTable { get; set; }

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

    // Glancing blows are a landed reduced-damage result. Core does not
    // assume any particular game formula for the chance or multiplier.
    public decimal BaseGlancingChancePercent { get; set; }

    public string? GlancingChanceStatKey { get; set; }

    public string? TargetGlancingSuppressionStatKey { get; set; }

    public decimal MinimumGlancingDamageMultiplier { get; set; } = 1m;

    public decimal MaximumGlancingDamageMultiplier { get; set; } = 1m;

    // Block chance participates in the same ordered result table after
    // miss/dodge/parry. Block value is damage removed from a landed block.
    public decimal BaseBlockChancePercent { get; set; }

    public string? TargetBlockChanceStatKey { get; set; }

    public string? SourceBlockReductionStatKey { get; set; }

    public decimal BaseBlockValue { get; set; }

    public string? TargetBlockValueStatKey { get; set; }

    public decimal BaseCriticalChancePercent { get; set; }

    public string? CriticalChanceStatKey { get; set; }

    public string? TargetCriticalSuppressionStatKey { get; set; }

    public decimal CriticalMultiplier { get; set; } = 2m;

    // Crushing blows are a landed increased-damage result that occupies
    // attack-table space after critical strikes.
    public decimal BaseCrushingChancePercent { get; set; }

    public string? CrushingChanceStatKey { get; set; }

    public string? TargetCrushingSuppressionStatKey { get; set; }

    public decimal CrushingDamageMultiplier { get; set; } = 1m;
}
