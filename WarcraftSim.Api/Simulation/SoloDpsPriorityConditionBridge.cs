using WarcraftSim.Core.Rotations;

namespace WarcraftSim.Api.Simulation;

/// <summary>Additional AND predicates for a typed user's custom priority.
/// These are attached to the existing Core RotationProfile and evaluated by
/// RotationConditionEvaluator, never by frontend JavaScript.</summary>
public sealed class SoloDpsPriorityConditionRule
{
    public string AbilityKey { get; set; } = "";
    public List<RotationConditionDefinition> Conditions { get; set; } = [];
}

public static class SoloDpsPriorityConditionBridge
{
    private static readonly HashSet<string> Numerical = new(StringComparer.OrdinalIgnoreCase){
        RotationConditionTypes.CurrentTimeSeconds,
        RotationConditionTypes.EncounterTimeRemainingSeconds,
        RotationConditionTypes.SourceAbilityCooldownRemainingSeconds,
        RotationConditionTypes.SourceResourceDeficit,
        RotationConditionTypes.SourceAuraRemainingSeconds,
        RotationConditionTypes.TargetAuraRemainingSeconds,
        RotationConditionTypes.TargetAuraStackCount,
        RotationConditionTypes.ActiveEnemyCount,
        RotationConditionTypes.SourceHealthPercent,
        RotationConditionTypes.TargetHealthPercent,
        RotationConditionTypes.SourceStatValue,
        RotationConditionTypes.SourceResourceCurrent,
        RotationConditionTypes.SourceResourcePercent
    };
    private static readonly HashSet<string> WithKey = new(StringComparer.OrdinalIgnoreCase){
        RotationConditionTypes.SourceResourceCurrent,
        RotationConditionTypes.SourceResourcePercent,
        RotationConditionTypes.SourceStatValue,
        RotationConditionTypes.SourceAbilityOnCooldown,
        RotationConditionTypes.SourceAbilityReady,
        RotationConditionTypes.SourceAbilityCooldownRemainingSeconds,
        RotationConditionTypes.SourceResourceDeficit,
        RotationConditionTypes.SourceAuraRemainingSeconds,
        RotationConditionTypes.TargetAuraRemainingSeconds,
        RotationConditionTypes.TargetAuraStackCount,
        RotationConditionTypes.SourceAuraActive,
        RotationConditionTypes.SourceAuraMissing,
        RotationConditionTypes.TargetAuraActive,
        RotationConditionTypes.TargetAuraMissing,
        RotationConditionTypes.EncounterPhaseActive,
        RotationConditionTypes.EncounterPhaseInactive
    };
    private static readonly HashSet<string> Allowed = new(
        Numerical.Concat(WithKey),StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> Comparisons = new(StringComparer.OrdinalIgnoreCase){
        RotationComparisonOperators.Equal,RotationComparisonOperators.NotEqual,
        RotationComparisonOperators.GreaterThan,RotationComparisonOperators.GreaterThanOrEqual,
        RotationComparisonOperators.LessThan,RotationComparisonOperators.LessThanOrEqual
    };

    public static void Apply(RotationProfile profile,
        IReadOnlyList<SoloDpsPriorityConditionRule>? rules,
        IEnumerable<string> enabledCustomPriority)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if(rules is null)throw new ArgumentException("Custom priority conditions cannot be null.");
        if(rules.Count>25)throw new ArgumentException("At most 25 conditional action rules.");
        var available=new HashSet<string>(enabledCustomPriority,StringComparer.OrdinalIgnoreCase);
        var visited=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var rule in rules)
        {
            if(rule is null || string.IsNullOrWhiteSpace(rule.AbilityKey) ||
               !visited.Add(rule.AbilityKey) || !available.Contains(rule.AbilityKey))
                throw new ArgumentException("A conditional priority must reference exactly one enabled custom action.");
            if(rule.Conditions is null || rule.Conditions.Count>8)
                throw new ArgumentException("A custom action supports at most eight AND conditions.");
            foreach(var condition in rule.Conditions)
            {
                if(condition is null || !Allowed.Contains(condition.ConditionType))
                    throw new ArgumentException("Unsupported rotation condition type.");
                var numerical=Numerical.Contains(condition.ConditionType);
                if(numerical && (!condition.Value.HasValue ||
                    !Comparisons.Contains(condition.ComparisonOperator)) ||
                    WithKey.Contains(condition.ConditionType) &&
                    string.IsNullOrWhiteSpace(condition.Key) ||
                    numerical && condition.ConditionType is
                        RotationConditionTypes.SourceResourcePercent or
                        RotationConditionTypes.SourceHealthPercent or
                        RotationConditionTypes.TargetHealthPercent &&
                    condition.Value is <0m or >100m)
                    throw new ArgumentException("Condition is missing an executable key/value or has an invalid percentage.");
            }
            var matched=profile.Entries.Where(x=>string.Equals(x.AbilityKey,rule.AbilityKey,
                StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.NextSwingReplacementKey,rule.AbilityKey,
                StringComparison.OrdinalIgnoreCase)).ToArray();
            if(matched.Length==0)
                throw new ArgumentException($"Unavailable custom priority action '{rule.AbilityKey}'.");
            foreach(var entry in matched)
                entry.Conditions.AddRange(rule.Conditions.Select(c=>new RotationConditionDefinition {
                    ConditionType=c.ConditionType,ComparisonOperator=c.ComparisonOperator,
                    Key=c.Key,Value=c.Value,RequireSourceOwnership=c.RequireSourceOwnership
                }));
        }
    }
}
