namespace WarcraftSim.Core.Rulesets;

public sealed class DamageMitigationRuleDefinition
{
    public string MitigationType { get; set; } = "";

    public string FormulaType { get; set; } = "";

    public string? DefenseStatKey { get; set; }

    // Example: "{school}-resistance" -> "fire-resistance".
    public string? DefenseStatKeyFormat { get; set; }

    // Used by RationalLevelScaled:
    // reduction = defense / (defense + BaseConstant + PerAttackerLevelConstant * attackerLevel)
    public decimal BaseConstant { get; set; }

    public decimal PerAttackerLevelConstant { get; set; }

    // Used by ResistanceAverage:
    // fractionOfCap = defense / (DefensePerAttackerLevel * attackerLevel)
    public decimal DefensePerAttackerLevel { get; set; } = 5m;

    public decimal MaximumReductionPercent { get; set; } = 75m;
}
