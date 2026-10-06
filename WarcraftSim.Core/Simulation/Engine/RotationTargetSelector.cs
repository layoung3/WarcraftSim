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
                SimulationActorSemanticSelector.Matches(
                    source,
                    source,
                    SimulationActorRelationshipTypes.Any,
                    targetDefinition.TeamKey,
                    targetDefinition.AllowedRoles,
                    targetDefinition.ExcludedRoles,
                    includeSourceActor:
                        true
                )
                    ? source
                    : null,

            RotationTargetSelectionModes.Fixed =>
                ResolveFixed(
                    context,
                    source,
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
                    source,
                    targetDefinition
                ) ??
                ResolveLowestHealthAlly(
                    context,
                    source,
                    targetDefinition
                ),

            RotationTargetSelectionModes.FirstMatchingActor =>
                ResolveFirstMatchingActor(
                    context,
                    source,
                    targetDefinition
                ),

            RotationTargetSelectionModes.LowestHealthMatchingActor =>
                ResolveLowestHealthMatchingActor(
                    context,
                    source,
                    targetDefinition
                ),

            _ =>
                ResolveDefault(
                    context,
                    source,
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
                targetDefinition.Mode ==
                    RotationTargetSelectionModes.LowestHealthAlly ||
                targetDefinition.Mode ==
                    RotationTargetSelectionModes.FixedThenLowestHealthAlly
            )
            {
                if (SimulationActorSemanticSelector.Matches(
                        candidate,
                        source,
                        SimulationActorRelationshipTypes.Ally,
                        targetDefinition.TeamKey,
                        targetDefinition.AllowedRoles,
                        targetDefinition.ExcludedRoles,
                        targetDefinition.IncludeSelf
                    ))
                {
                    return true;
                }
            }

            if (
                targetDefinition.Mode ==
                    RotationTargetSelectionModes.FirstMatchingActor ||
                targetDefinition.Mode ==
                    RotationTargetSelectionModes.LowestHealthMatchingActor
            )
            {
                if (SimulationActorSemanticSelector.Matches(
                        candidate,
                        source,
                        targetDefinition.Relationship,
                        targetDefinition.TeamKey,
                        targetDefinition.AllowedRoles,
                        targetDefinition.ExcludedRoles,
                        targetDefinition.IncludeSelf
                    ))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static SimulationActorState?
        ResolveDefault(
            SimulationContext context,
            SimulationActorState source,
            string defaultTargetKey,
            RotationTargetDefinition targetDefinition)
    {
        var actor =
            context.GetActor(
                defaultTargetKey
            );

        return
            actor is not null &&
            SimulationActorSemanticSelector.Matches(
                actor,
                source,
                targetDefinition.Relationship,
                targetDefinition.TeamKey,
                targetDefinition.AllowedRoles,
                targetDefinition.ExcludedRoles,
                targetDefinition.IncludeSelf
            )
                ? actor
                : null;
    }

    private static SimulationActorState?
        ResolveFixed(
            SimulationContext context,
            SimulationActorState source,
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
            actor is not null &&
            SimulationActorSemanticSelector.Matches(
                actor,
                source,
                targetDefinition.Relationship,
                targetDefinition.TeamKey,
                targetDefinition.AllowedRoles,
                targetDefinition.ExcludedRoles,
                targetDefinition.IncludeSelf
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
        return ResolveLowestHealthMatching(
            context,
            source,
            targetDefinition,
            SimulationActorRelationshipTypes.Ally
        );
    }

    private static SimulationActorState?
        ResolveFirstMatchingActor(
            SimulationContext context,
            SimulationActorState source,
            RotationTargetDefinition targetDefinition)
    {
        return SimulationActorSemanticSelector
            .ResolveMatching(
                context,
                source,
                targetDefinition.Relationship,
                targetDefinition.TeamKey,
                targetDefinition.AllowedRoles,
                targetDefinition.ExcludedRoles,
                targetDefinition.IncludeSelf
            )
            .FirstOrDefault();
    }

    private static SimulationActorState?
        ResolveLowestHealthMatchingActor(
            SimulationContext context,
            SimulationActorState source,
            RotationTargetDefinition targetDefinition)
    {
        return ResolveLowestHealthMatching(
            context,
            source,
            targetDefinition,
            targetDefinition.Relationship
        );
    }

    private static SimulationActorState?
        ResolveLowestHealthMatching(
            SimulationContext context,
            SimulationActorState source,
            RotationTargetDefinition targetDefinition,
            string relationship)
    {
        return SimulationActorSemanticSelector
            .ResolveMatching(
                context,
                source,
                relationship,
                targetDefinition.TeamKey,
                targetDefinition.AllowedRoles,
                targetDefinition.ExcludedRoles,
                targetDefinition.IncludeSelf
            )
            .OrderBy(
                actor =>
                    GetHealthPercent(
                        actor
                    )
            )
            .ThenBy(
                actor =>
                    actor.CurrentHealth
            )
            .ThenBy(
                actor =>
                    actor.Key,
                StringComparer.OrdinalIgnoreCase
            )
            .FirstOrDefault();
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
