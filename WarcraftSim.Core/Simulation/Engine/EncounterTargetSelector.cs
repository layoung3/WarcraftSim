using WarcraftSim.Core.Encounters;

namespace WarcraftSim.Core.Simulation.Engine;

public static class EncounterTargetSelector
{
    public static IReadOnlyList<SimulationActorState> Resolve(
        SimulationContext context,
        EncounterDamagePatternDefinition pattern,
        IReadOnlyCollection<string>? excludedActorKeys = null,
        bool forceUniqueTargets = false)
    {
        var selection =
            pattern.TargetSelection ??
            new EncounterTargetSelectionDefinition
            {
                Mode = EncounterTargetSelectionModes.FixedActor,
                ActorKey = pattern.TargetActorKey
            };

        var excluded =
            excludedActorKeys is null
                ? null
                : new HashSet<string>(
                    excludedActorKeys,
                    StringComparer.OrdinalIgnoreCase
                );

        var source =
            string.IsNullOrWhiteSpace(
                pattern.SourceActorKey)
                ? null
                : context.GetActor(
                    pattern.SourceActorKey
                );

        var candidates =
            SimulationActorSemanticSelector
                .ResolveMatching(
                    context,
                    source,
                    selection.Relationship,
                    selection.TeamKey,
                    selection.AllowedRoles,
                    selection.ExcludedRoles,
                    selection.IncludeSourceActor
                )
                .Where(
                    actor =>
                        excluded is null ||
                        !excluded.Contains(
                            actor.Key
                        )
                )
                .ToList();

        return selection.Mode switch
        {
            EncounterTargetSelectionModes.FixedActor =>
                ResolveFixed(
                    candidates,
                    selection.ActorKey ?? pattern.TargetActorKey
                ),

            EncounterTargetSelectionModes.AllMatchingActors =>
                candidates,

            EncounterTargetSelectionModes.RandomMatchingActors =>
                ResolveRandom(
                    candidates,
                    Math.Max(0, selection.Count),
                    selection.AllowDuplicateTargets && !forceUniqueTargets,
                    context.EncounterRandom
                ),

            EncounterTargetSelectionModes.HighestThreatActor =>
                ResolveHighestThreat(
                    source,
                    candidates
                ),

            _ =>
                throw new InvalidOperationException(
                    $"Unknown encounter target selection mode '{selection.Mode}'."
                )
        };
    }

    private static IReadOnlyList<SimulationActorState>
        ResolveHighestThreat(
            SimulationActorState? source,
            IReadOnlyList<SimulationActorState> candidates)
    {
        if (source is null)
        {
            throw new InvalidOperationException(
                "Highest-threat encounter targeting requires a source actor."
            );
        }

        var actorKey =
            source.ThreatTable
                .GetHighestThreatActorKey(
                    candidates.Select(
                        candidate =>
                            candidate.Key
                    )
                );

        if (string.IsNullOrWhiteSpace(
                actorKey))
        {
            return [];
        }

        var actor =
            candidates.FirstOrDefault(
                candidate =>
                    string.Equals(
                        candidate.Key,
                        actorKey,
                        StringComparison.OrdinalIgnoreCase
                    )
            );

        return actor is null
            ? []
            : [actor];
    }

    private static IReadOnlyList<SimulationActorState> ResolveFixed(
        IReadOnlyList<SimulationActorState> candidates,
        string? actorKey)
    {
        if (string.IsNullOrWhiteSpace(actorKey))
        {
            return [];
        }

        var actor =
            candidates.FirstOrDefault(
                candidate =>
                    string.Equals(
                        candidate.Key,
                        actorKey,
                        StringComparison.OrdinalIgnoreCase
                    )
            );

        return actor is null ? [] : [actor];
    }

    private static IReadOnlyList<SimulationActorState> ResolveRandom(
        IReadOnlyList<SimulationActorState> candidates,
        int count,
        bool allowDuplicateTargets,
        Random random)
    {
        if (count <= 0 || candidates.Count == 0)
        {
            return [];
        }

        if (allowDuplicateTargets)
        {
            var result = new List<SimulationActorState>(count);

            for (var i = 0; i < count; i++)
            {
                result.Add(
                    candidates[random.Next(candidates.Count)]
                );
            }

            return result;
        }

        var pool = candidates.ToList();
        var uniqueCount = Math.Min(count, pool.Count);
        var uniqueResult = new List<SimulationActorState>(uniqueCount);

        for (var i = 0; i < uniqueCount; i++)
        {
            var selectedIndex = random.Next(pool.Count);
            uniqueResult.Add(pool[selectedIndex]);
            pool.RemoveAt(selectedIndex);
        }

        return uniqueResult;
    }
}
