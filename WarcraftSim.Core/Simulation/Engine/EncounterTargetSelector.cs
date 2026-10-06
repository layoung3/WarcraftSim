using WarcraftSim.Core.Encounters;

namespace WarcraftSim.Core.Simulation.Engine;

public static class EncounterTargetSelector
{
    public static IReadOnlyList<SimulationActorState> Resolve(
        SimulationContext context,
        EncounterDamagePatternDefinition pattern)
    {
        var selection =
            pattern.TargetSelection ??
            new EncounterTargetSelectionDefinition
            {
                Mode =
                    EncounterTargetSelectionModes.FixedActor,

                ActorKey =
                    pattern.TargetActorKey
            };

        var candidates =
            context.Actors.Values
                .Where(actor =>
                    MatchesFilters(
                        actor,
                        selection,
                        pattern.SourceActorKey
                    )
                )
                .OrderBy(actor =>
                    actor.Key,
                    StringComparer.OrdinalIgnoreCase
                )
                .ToList();

        return selection.Mode switch
        {
            EncounterTargetSelectionModes.FixedActor =>
                ResolveFixed(
                    candidates,
                    selection.ActorKey ??
                        pattern.TargetActorKey
                ),

            EncounterTargetSelectionModes.AllMatchingActors =>
                candidates,

            EncounterTargetSelectionModes.RandomMatchingActors =>
                ResolveRandom(
                    candidates,
                    Math.Max(
                        0,
                        selection.Count
                    ),
                    selection.AllowDuplicateTargets,
                    context.EncounterRandom
                ),

            _ =>
                throw new InvalidOperationException(
                    $"Unknown encounter target selection mode '{selection.Mode}'."
                )
        };
    }

    private static bool MatchesFilters(
        SimulationActorState actor,
        EncounterTargetSelectionDefinition selection,
        string? sourceActorKey)
    {
        if (!actor.IsAlive)
        {
            return false;
        }

        if (
            !selection.IncludeSourceActor &&
            !string.IsNullOrWhiteSpace(
                sourceActorKey) &&
            string.Equals(
                actor.Key,
                sourceActorKey,
                StringComparison.OrdinalIgnoreCase)
        )
        {
            return false;
        }

        if (
            !string.IsNullOrWhiteSpace(
                selection.TeamKey) &&
            !string.Equals(
                actor.TeamKey,
                selection.TeamKey,
                StringComparison.OrdinalIgnoreCase)
        )
        {
            return false;
        }

        if (selection.AllowedRoles.Count > 0)
        {
            if (
                !actor.AssignedRole.HasValue ||
                !selection.AllowedRoles.Contains(
                    actor.AssignedRole.Value)
            )
            {
                return false;
            }
        }

        if (
            actor.AssignedRole.HasValue &&
            selection.ExcludedRoles.Contains(
                actor.AssignedRole.Value)
        )
        {
            return false;
        }

        return true;
    }

    private static IReadOnlyList<SimulationActorState> ResolveFixed(
        IReadOnlyList<SimulationActorState> candidates,
        string? actorKey)
    {
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

    private static IReadOnlyList<SimulationActorState> ResolveRandom(
        IReadOnlyList<SimulationActorState> candidates,
        int count,
        bool allowDuplicateTargets,
        Random random)
    {
        if (
            count <= 0 ||
            candidates.Count == 0
        )
        {
            return [];
        }

        if (allowDuplicateTargets)
        {
            var result =
                new List<SimulationActorState>(
                    count
                );

            for (
                var i = 0;
                i < count;
                i++)
            {
                result.Add(
                    candidates[
                        random.Next(
                            candidates.Count
                        )
                    ]
                );
            }

            return result;
        }

        var pool =
            candidates.ToList();

        var uniqueCount =
            Math.Min(
                count,
                pool.Count
            );

        var uniqueResult =
            new List<SimulationActorState>(
                uniqueCount
            );

        for (
            var i = 0;
            i < uniqueCount;
            i++)
        {
            var selectedIndex =
                random.Next(
                    pool.Count
                );

            uniqueResult.Add(
                pool[
                    selectedIndex
                ]
            );

            pool.RemoveAt(
                selectedIndex
            );
        }

        return uniqueResult;
    }
}
