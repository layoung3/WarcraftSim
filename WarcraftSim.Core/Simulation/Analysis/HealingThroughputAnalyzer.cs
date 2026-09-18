using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Core.Simulation.Analysis;

public static class HealingThroughputAnalyzer
{
    public static HealingThroughputMetrics Analyze(
        SimulationRunResult result,
        string healerActorKey,
        string resourceKey)
    {
        if (!result.Summary.ActorSummaries.TryGetValue(
                healerActorKey,
                out var actorSummary))
        {
            throw new InvalidOperationException(
                $"Actor '{healerActorKey}' was not found in the simulation summary."
            );
        }

        var duration =
            Math.Max(
                0m,
                result.Summary.DurationSeconds
            );

        var effectiveHealing =
            actorSummary.HealingDone;

        var overhealing =
            actorSummary.OverhealingDone;

        var rawHealing =
            effectiveHealing +
            overhealing;

        var casts =
            result.Timeline
                .Where(combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.AbilityCastCompleted &&
                    string.Equals(
                        combatEvent.SourceActorKey,
                        healerActorKey,
                        StringComparison.OrdinalIgnoreCase
                    ) &&
                    !string.IsNullOrWhiteSpace(
                        combatEvent.AbilityKey)
                )
                .GroupBy(
                    combatEvent =>
                        combatEvent.AbilityKey!,
                    StringComparer.OrdinalIgnoreCase
                )
                .ToDictionary(
                    group =>
                        group.Key,
                    group =>
                        group.Count(),
                    StringComparer.OrdinalIgnoreCase
                );

        actorSummary.StartingResources.TryGetValue(
            resourceKey,
            out var startingResource
        );

        actorSummary.EndingResources.TryGetValue(
            resourceKey,
            out var endingResource
        );

        return new HealingThroughputMetrics
        {
            HealerActorKey =
                healerActorKey,

            WindowSeconds =
                duration,

            EffectiveHealing =
                effectiveHealing,

            Overhealing =
                overhealing,

            RawHealing =
                rawHealing,

            EffectiveHps =
                duration <= 0m
                    ? 0m
                    : effectiveHealing /
                      duration,

            RawHps =
                duration <= 0m
                    ? 0m
                    : rawHealing /
                      duration,

            OverhealingPercent =
                rawHealing <= 0m
                    ? 0m
                    : overhealing /
                      rawHealing *
                      100m,

            ResourceKey =
                resourceKey,

            StartingResource =
                startingResource,

            EndingResource =
                endingResource,

            BecameResourceStarved =
                actorSummary.FirstResourceStarvedAtSeconds
                    .HasValue,

            FirstResourceStarvedAtSeconds =
                actorSummary.FirstResourceStarvedAtSeconds,

            ResourceStarvedSeconds =
                actorSummary.ResourceStarvedSeconds,

            ResourceStarvedPercent =
                duration <= 0m
                    ? 0m
                    : actorSummary.ResourceStarvedSeconds /
                      duration *
                      100m,

            CompletedCastsByAbility =
                casts
        };
    }
}
