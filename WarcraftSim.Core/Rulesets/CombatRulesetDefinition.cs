namespace WarcraftSim.Core.Rulesets;

public sealed class CombatRulesetDefinition
{
    public string RulesetKey { get; set; } = "";

    public string Version { get; set; } = "";

    public Dictionary<string, CombatRollRuleDefinition> RollRules { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, DamageMitigationRuleDefinition> MitigationRules { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public CombatRollRuleDefinition? GetRollRule(
        string resolutionType)
    {
        return RollRules.TryGetValue(
            resolutionType,
            out var rule)
                ? rule
                : null;
    }

    public DamageMitigationRuleDefinition? GetMitigationRule(
        string mitigationType)
    {
        return MitigationRules.TryGetValue(
            mitigationType,
            out var rule)
                ? rule
                : null;
    }
}
