using WarcraftSim.Core.Rotations;

namespace WarcraftSim.Core.Simulation.Engine;

public static class RotationTargetSelector
{
    public static SimulationActorState? Resolve(
        SimulationContext context,
        SimulationActorState source,
        RotationEntry entry,
        string defaultTargetKey)
    {
        var targetDefinition =
            entry.Target ?? new RotationTargetDefinition();

        return targetDefinition.Mode switch
        {
            RotationTargetSelectionModes.Self =>
                IsAllowedByRole(
                    source,
                    targetDefinition
                )
                    ? source
                    : null,

            RotationTargetSelectionModes.Fixed =>
                ResolveFixed(
                    context,
                    targetDefinition
                ),

            RotationTargetSelectionModes.LowestHealthAlly =>
                ResolveLowestHealthAlly(
                    context,
                    source,
                    targetDefinition
                ),

            RotationTargetSelectionModes.FixedThenLowestHealthAlly =>
                ResolveFixed(
                    context,
                    targetDefinition
                ) ??
                ResolveLowestHealthAlly(
                    context,
                    source,
                    targetDefinition
                ),

            _ =>
                ResolveDefault(
                    context,
                    defaultTargetKey,
                    targetDefinition
                )
        };
    }

    public static bool WatchesActor(
        SimulationContext context,
        SimulationActorState source,
        IReadOnlyList<RotationEntry> entries,
        string defaultTargetKey,
        string actorKey)
    {
        if (
            string.Equals(
                defaultTargetKey,
                actorKey,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return true;
        }

        var candidate =
            context.GetActor(
                actorKey
            );

        if (candidate is null)
        {
            return false;
        }

        foreach (
            var entry in
            entries.Where(entry =>
                entry.IsEnabled))
        {
            var targetDefinition =
                entry.Target ?? new RotationTargetDefinition();

            if (
                (
                    targetDefinition.Mode ==
                        RotationTargetSelectionModes.Fixed ||
                    targetDefinition.Mode ==
                        RotationTargetSelectionModes.FixedThenLowestHealthAlly
                ) &&
                string.Equals(
                    targetDefinition.ActorKey,
                    actorKey,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return true;
            }

            if (
                (
                    targetDefinition.Mode ==
                        RotationTargetSelectionModes.LowestHealthAlly ||
                    targetDefinition.Mode ==
                        RotationTargetSelectionModes.FixedThenLowestHealthAlly
                ) &&
                candidate.IsAlive &&
                IsAlly(
                    source,
                    candidate
                ) &&
                IsAllowedByRole(
                    candidate,
                    targetDefinition
                ) &&
                (
                    targetDefinition.IncludeSelf ||
                    !string.Equals(
                        source.Key,
                        candidate.Key,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
            )
            {
                return true;
            }
        }

        return false;
    }

    private static SimulationActorState?
        ResolveDefault(
            SimulationContext context,
            string defaultTargetKey,
            RotationTargetDefinition targetDefinition)
    {
        var actor =
            context.GetActor(
                defaultTargetKey
            );

        return
            actor is { IsAlive: true } &&
            IsAllowedByRole(
                actor,
                targetDefinition
            )
                ? actor
                : null;
    }

    private static SimulationActorState?
        ResolveFixed(
            SimulationContext context,
            RotationTargetDefinition targetDefinition)
    {
        if (string.IsNullOrWhiteSpace(
                targetDefinition.ActorKey))
        {
            return null;
        }

        var actor =
            context.GetActor(
                targetDefinition.ActorKey
            );

        return
            actor is { IsAlive: true } &&
            IsAllowedByRole(
                actor,
                targetDefinition
            )
                ? actor
                : null;
    }

    private static SimulationActorState?
        ResolveLowestHealthAlly(
            SimulationContext context,
            SimulationActorState source,
            RotationTargetDefinition targetDefinition)
    {
        return context.Actors.Values
            .Where(actor =>
                actor.IsAlive)
            .Where(actor =>
                IsAlly(
                    source,
                    actor
                ))
            .Where(actor =>
                targetDefinition.IncludeSelf ||
                !string.Equals(
                    actor.Key,
                    source.Key,
                    StringComparison.OrdinalIgnoreCase
                ))
            .Where(actor =>
                IsAllowedByRole(
                    actor,
                    targetDefinition
                ))
            .OrderBy(actor =>
                GetHealthPercent(
                    actor
                ))
            .ThenBy(actor =>
                actor.CurrentHealth)
            .ThenBy(actor =>
                actor.Key,
                StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static bool IsAllowedByRole(
        SimulationActorState actor,
        RotationTargetDefinition targetDefinition)
    {
        if (
            targetDefinition.AllowedRoles.Count ==
            0
        )
        {
            return true;
        }

        return
            actor.AssignedRole.HasValue &&
            targetDefinition.AllowedRoles.Contains(
                actor.AssignedRole.Value
            );
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
}
