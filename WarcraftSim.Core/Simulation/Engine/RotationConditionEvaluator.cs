using WarcraftSim.Core.Encounters;
using WarcraftSim.Core.Rotations;

namespace WarcraftSim.Core.Simulation.Engine;

public static class RotationConditionEvaluator
{
    public static bool AreSatisfied(
        SimulationContext context,
        SimulationActorState source,
        SimulationActorState target,
        IReadOnlyList<RotationConditionDefinition> conditions)
    {
        foreach (var condition in conditions)
        {
            if (!IsSatisfied(
                    context,
                    source,
                    target,
                    condition))
            {
                return false;
            }
        }

        return true;
    }

    public static decimal? GetNextKnownEvaluationTime(
        SimulationContext context,
        SimulationActorState source,
        SimulationActorState target,
        IReadOnlyList<RotationConditionDefinition> conditions)
    {
        decimal? nextTime = null;

        foreach (var condition in conditions)
        {
            var candidate =
                GetNextKnownEvaluationTime(
                    context,
                    source,
                    target,
                    condition
                );

            if (
                candidate.HasValue &&
                candidate.Value >
                context.CurrentTimeSeconds
            )
            {
                nextTime =
                    !nextTime.HasValue
                        ? candidate.Value
                        : Math.Min(
                            nextTime.Value,
                            candidate.Value
                        );
            }
        }

        return nextTime;
    }

    private const decimal FutureEvaluationEpsilonSeconds =
        0.000001m;

    private static decimal?
        GetNextKnownEvaluationTime(
            SimulationContext context,
            SimulationActorState source,
            SimulationActorState target,
            RotationConditionDefinition condition)
    {
        if (
            string.Equals(
                condition.ConditionType,
                RotationConditionTypes.CurrentTimeSeconds,
                StringComparison.OrdinalIgnoreCase) &&
            condition.Value.HasValue &&
            (
                condition.ComparisonOperator ==
                    RotationComparisonOperators.GreaterThan ||
                condition.ComparisonOperator ==
                    RotationComparisonOperators.GreaterThanOrEqual
            ) &&
            condition.Value.Value >
                context.CurrentTimeSeconds
        )
        {
            return
                condition.ComparisonOperator ==
                    RotationComparisonOperators.GreaterThan
                        ? condition.Value.Value +
                          FutureEvaluationEpsilonSeconds
                        : condition.Value.Value;
        }

        if (
            string.Equals(
                condition.ConditionType,
                RotationConditionTypes.SourceResourceCurrent,
                StringComparison.OrdinalIgnoreCase)
        )
        {
            return GetNextResourceEvaluationTime(
                context,
                source,
                condition,
                usePercent:
                    false
            );
        }

        if (
            string.Equals(
                condition.ConditionType,
                RotationConditionTypes.SourceResourcePercent,
                StringComparison.OrdinalIgnoreCase)
        )
        {
            return GetNextResourceEvaluationTime(
                context,
                source,
                condition,
                usePercent:
                    true
            );
        }

        if (
            string.Equals(
                condition.ConditionType,
                RotationConditionTypes.SourceAuraActive,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                condition.ConditionType,
                RotationConditionTypes.SourceAuraMissing,
                StringComparison.OrdinalIgnoreCase)
        )
        {
            return GetNextAuraEvaluationTime(
                context,
                source,
                condition.Key
            );
        }

        if (
            string.Equals(
                condition.ConditionType,
                RotationConditionTypes.TargetAuraActive,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                condition.ConditionType,
                RotationConditionTypes.TargetAuraMissing,
                StringComparison.OrdinalIgnoreCase)
        )
        {
            return GetNextAuraEvaluationTime(
                context,
                target,
                condition.Key
            );
        }

        if (
            string.Equals(
                condition.ConditionType,
                RotationConditionTypes.EncounterPhaseActive,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                condition.ConditionType,
                RotationConditionTypes.EncounterPhaseInactive,
                StringComparison.OrdinalIgnoreCase)
        )
        {
            var phase =
                FindPhase(
                    context,
                    condition.Key
                );

            if (phase is null)
            {
                return null;
            }

            var candidates =
                new List<decimal>();

            if (
                phase.StartTimeSeconds >
                context.CurrentTimeSeconds
            )
            {
                candidates.Add(
                    phase.StartTimeSeconds
                );
            }

            if (
                phase.EndTimeSeconds.HasValue &&
                phase.EndTimeSeconds.Value >
                context.CurrentTimeSeconds
            )
            {
                candidates.Add(
                    phase.EndTimeSeconds.Value
                );
            }

            if (candidates.Count > 0)
            {
                return
                    candidates.Min();
            }
        }

        return null;
    }


    private static decimal? GetNextResourceEvaluationTime(
        SimulationContext context,
        SimulationActorState source,
        RotationConditionDefinition condition,
        bool usePercent)
    {
        if (
            string.IsNullOrWhiteSpace(
                condition.Key) ||
            !condition.Value.HasValue ||
            !source.Resources.TryGetValue(
                condition.Key,
                out var resource)
        )
        {
            return null;
        }

        resource.AdvanceTo(
            context.CurrentTimeSeconds
        );

        if (
            resource.RegenerationPerSecond <= 0m ||
            resource.Current >=
                resource.Maximum
        )
        {
            return null;
        }

        var currentValue =
            usePercent
                ? resource.Maximum <= 0m
                    ? 0m
                    : resource.Current /
                      resource.Maximum *
                      100m
                : resource.Current;

        var expectedValue =
            condition.Value.Value;

        if (Compare(
                currentValue,
                expectedValue,
                condition.ComparisonOperator))
        {
            return null;
        }

        var requiredResource =
            usePercent
                ? resource.Maximum *
                  expectedValue /
                  100m
                : expectedValue;

        switch (condition.ComparisonOperator)
        {
            case RotationComparisonOperators.GreaterThanOrEqual:
                return resource.GetTimeWhenAvailable(
                    requiredResource,
                    context.CurrentTimeSeconds
                );

            case RotationComparisonOperators.GreaterThan:
            {
                if (
                    requiredResource >=
                    resource.Maximum
                )
                {
                    return null;
                }

                var thresholdTime =
                    resource.GetTimeWhenAvailable(
                        requiredResource,
                        context.CurrentTimeSeconds
                    );

                return thresholdTime.HasValue
                    ? thresholdTime.Value +
                      FutureEvaluationEpsilonSeconds
                    : null;
            }

            case RotationComparisonOperators.Equal:
                if (
                    currentValue >
                        expectedValue ||
                    requiredResource < 0m ||
                    requiredResource >
                        resource.Maximum
                )
                {
                    return null;
                }

                return resource.GetTimeWhenAvailable(
                    requiredResource,
                    context.CurrentTimeSeconds
                );

            case RotationComparisonOperators.NotEqual:
                // If NotEqual is currently false, the resource is exactly
                // on the requested value. Positive regeneration makes the
                // condition true immediately after the current timestamp.
                return context.CurrentTimeSeconds +
                       FutureEvaluationEpsilonSeconds;

            default:
                // Passive regeneration only increases a resource, so a false
                // less-than condition cannot become true without some other
                // gameplay event changing the resource. ResourceChanged event
                // reactions handle those discrete changes.
                return null;
        }
    }

    private static decimal? GetNextAuraEvaluationTime(
        SimulationContext context,
        SimulationActorState actor,
        string? auraKey)
    {
        if (string.IsNullOrWhiteSpace(
                auraKey))
        {
            return null;
        }

        return actor.ActiveAuras
            .Where(
                aura =>
                    string.Equals(
                        aura.Definition.Key,
                        auraKey,
                        StringComparison.OrdinalIgnoreCase
                    ) &&
                    aura.IsActiveAt(
                        context.CurrentTimeSeconds
                    ) &&
                    aura.ExpiresAtSeconds >
                        context.CurrentTimeSeconds
            )
            .Select(
                aura =>
                    (decimal?)aura.ExpiresAtSeconds
            )
            .Min();
    }

    private static bool IsSatisfied(
        SimulationContext context,
        SimulationActorState source,
        SimulationActorState target,
        RotationConditionDefinition condition)
    {
        switch (condition.ConditionType)
        {
            case RotationConditionTypes.CurrentTimeSeconds:
                return CompareRequiredValue(
                    context.CurrentTimeSeconds,
                    condition
                );

            case RotationConditionTypes.SourceHealthPercent:
                return CompareRequiredValue(
                    GetHealthPercent(source),
                    condition
                );

            case RotationConditionTypes.TargetHealthPercent:
                return CompareRequiredValue(
                    GetHealthPercent(target),
                    condition
                );

            case RotationConditionTypes.SourceResourceCurrent:
                return CompareResource(
                    context,
                    source,
                    condition,
                    usePercent: false
                );

            case RotationConditionTypes.SourceResourcePercent:
                return CompareResource(
                    context,
                    source,
                    condition,
                    usePercent: true
                );

            case RotationConditionTypes.SourceAuraActive:
                return HasActiveAura(
                    context,
                    source,
                    condition.Key
                );

            case RotationConditionTypes.SourceAuraMissing:
                return !HasActiveAura(
                    context,
                    source,
                    condition.Key
                );

            case RotationConditionTypes.TargetAuraActive:
                return HasActiveAura(
                    context,
                    target,
                    condition.Key
                );

            case RotationConditionTypes.TargetAuraMissing:
                return !HasActiveAura(
                    context,
                    target,
                    condition.Key
                );

            case RotationConditionTypes.EncounterPhaseActive:
                return IsEncounterPhaseActive(
                    context,
                    condition.Key
                );

            case RotationConditionTypes.EncounterPhaseInactive:
                return !IsEncounterPhaseActive(
                    context,
                    condition.Key
                );

            default:
                return false;
        }
    }

    private static bool CompareResource(
        SimulationContext context,
        SimulationActorState actor,
        RotationConditionDefinition condition,
        bool usePercent)
    {
        if (
            string.IsNullOrWhiteSpace(
                condition.Key) ||
            !condition.Value.HasValue ||
            !actor.Resources.TryGetValue(
                condition.Key,
                out var resource)
        )
        {
            return false;
        }

        resource.AdvanceTo(
            context.CurrentTimeSeconds
        );

        var value =
            usePercent
                ? resource.Maximum <= 0m
                    ? 0m
                    : resource.Current /
                      resource.Maximum *
                      100m
                : resource.Current;

        return Compare(
            value,
            condition.Value.Value,
            condition.ComparisonOperator
        );
    }

    private static bool CompareRequiredValue(
        decimal actualValue,
        RotationConditionDefinition condition)
    {
        if (!condition.Value.HasValue)
        {
            return false;
        }

        return Compare(
            actualValue,
            condition.Value.Value,
            condition.ComparisonOperator
        );
    }

    private static bool Compare(
        decimal actualValue,
        decimal expectedValue,
        string comparisonOperator)
    {
        return comparisonOperator switch
        {
            RotationComparisonOperators.LessThan =>
                actualValue < expectedValue,

            RotationComparisonOperators.LessThanOrEqual =>
                actualValue <= expectedValue,

            RotationComparisonOperators.Equal =>
                actualValue == expectedValue,

            RotationComparisonOperators.NotEqual =>
                actualValue != expectedValue,

            RotationComparisonOperators.GreaterThanOrEqual =>
                actualValue >= expectedValue,

            RotationComparisonOperators.GreaterThan =>
                actualValue > expectedValue,

            _ => false
        };
    }

    private static decimal GetHealthPercent(
        SimulationActorState actor)
    {
        if (actor.MaximumHealth <= 0m)
        {
            return 0m;
        }

        return
            actor.CurrentHealth /
            actor.MaximumHealth *
            100m;
    }

    private static bool HasActiveAura(
        SimulationContext context,
        SimulationActorState actor,
        string? auraKey)
    {
        if (string.IsNullOrWhiteSpace(
                auraKey))
        {
            return false;
        }

        return actor.ActiveAuras.Any(
            aura =>
                string.Equals(
                    aura.Definition.Key,
                    auraKey,
                    StringComparison.OrdinalIgnoreCase
                ) &&
                aura.IsActiveAt(
                    context.CurrentTimeSeconds
                )
        );
    }

    private static bool IsEncounterPhaseActive(
        SimulationContext context,
        string? phaseKey)
    {
        var phase =
            FindPhase(
                context,
                phaseKey
            );

        return
            phase is not null &&
            phase.IsActiveAt(
                context.CurrentTimeSeconds
            );
    }

    private static EncounterPhaseDefinition?
        FindPhase(
            SimulationContext context,
            string? phaseKey)
    {
        if (
            context.Encounter is null ||
            string.IsNullOrWhiteSpace(
                phaseKey)
        )
        {
            return null;
        }

        return context.Encounter.Phases
            .FirstOrDefault(
                phase =>
                    string.Equals(
                        phase.Key,
                        phaseKey,
                        StringComparison.OrdinalIgnoreCase
                    )
            );
    }
}
