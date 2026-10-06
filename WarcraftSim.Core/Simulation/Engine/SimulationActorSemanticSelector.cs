using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Core.Simulation.Engine;

public static class SimulationActorSemanticSelector
{
    public static IReadOnlyList<SimulationActorState> ResolveMatching(
        SimulationContext context,
        SimulationActorState? source,
        string relationship,
        string? teamKey = null,
        IReadOnlyCollection<SimulationType>? allowedRoles = null,
        IReadOnlyCollection<SimulationType>? excludedRoles = null,
        bool includeSourceActor = false)
    {
        ArgumentNullException.ThrowIfNull(
            context
        );

        ValidateRelationship(
            relationship
        );

        return context.Actors.Values
            .Where(
                actor =>
                    Matches(
                        actor,
                        source,
                        relationship,
                        teamKey,
                        allowedRoles,
                        excludedRoles,
                        includeSourceActor
                    )
            )
            .OrderBy(
                actor =>
                    actor.Key,
                StringComparer.OrdinalIgnoreCase
            )
            .ToList();
    }

    public static bool Matches(
        SimulationActorState actor,
        SimulationActorState? source,
        string relationship,
        string? teamKey = null,
        IReadOnlyCollection<SimulationType>? allowedRoles = null,
        IReadOnlyCollection<SimulationType>? excludedRoles = null,
        bool includeSourceActor = false)
    {
        ArgumentNullException.ThrowIfNull(
            actor
        );

        ValidateRelationship(
            relationship
        );

        if (!actor.IsAlive)
        {
            return false;
        }

        var isSource =
            source is not null &&
            string.Equals(
                actor.Key,
                source.Key,
                StringComparison.OrdinalIgnoreCase
            );

        if (
            isSource &&
            !includeSourceActor
        )
        {
            return false;
        }

        if (
            !string.IsNullOrWhiteSpace(
                teamKey) &&
            !string.Equals(
                actor.TeamKey,
                teamKey,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return false;
        }

        if (!MatchesRelationship(
                actor,
                source,
                relationship
            ))
        {
            return false;
        }

        if (
            allowedRoles is { Count: > 0 } &&
            (
                !actor.AssignedRole.HasValue ||
                !allowedRoles.Contains(
                    actor.AssignedRole.Value
                )
            )
        )
        {
            return false;
        }

        if (
            actor.AssignedRole.HasValue &&
            excludedRoles is { Count: > 0 } &&
            excludedRoles.Contains(
                actor.AssignedRole.Value
            )
        )
        {
            return false;
        }

        return true;
    }

    private static bool MatchesRelationship(
        SimulationActorState actor,
        SimulationActorState? source,
        string relationship)
    {
        if (string.Equals(
                relationship,
                SimulationActorRelationshipTypes.Any,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (source is null)
        {
            return false;
        }

        var isAlly =
            IsAlly(
                source,
                actor
            );

        if (string.Equals(
                relationship,
                SimulationActorRelationshipTypes.Ally,
                StringComparison.OrdinalIgnoreCase))
        {
            return isAlly;
        }

        return !isAlly;
    }

    private static bool IsAlly(
        SimulationActorState source,
        SimulationActorState candidate)
    {
        if (
            string.IsNullOrWhiteSpace(
                source.TeamKey) ||
            string.IsNullOrWhiteSpace(
                candidate.TeamKey)
        )
        {
            return string.Equals(
                source.Key,
                candidate.Key,
                StringComparison.OrdinalIgnoreCase
            );
        }

        return string.Equals(
            source.TeamKey,
            candidate.TeamKey,
            StringComparison.OrdinalIgnoreCase
        );
    }

    private static void ValidateRelationship(
        string relationship)
    {
        if (
            string.Equals(
                relationship,
                SimulationActorRelationshipTypes.Any,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                relationship,
                SimulationActorRelationshipTypes.Ally,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                relationship,
                SimulationActorRelationshipTypes.Enemy,
                StringComparison.OrdinalIgnoreCase)
        )
        {
            return;
        }

        throw new InvalidOperationException(
            $"Unknown simulation actor relationship '{relationship}'."
        );
    }
}
