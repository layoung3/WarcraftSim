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
        var target = entry.Target ?? new RotationTargetDefinition();

        return target.Mode switch
        {
            RotationTargetSelectionModes.Self => source,
            RotationTargetSelectionModes.Fixed =>
                ResolveFixed(context, target.ActorKey),
            RotationTargetSelectionModes.LowestHealthAlly =>
                ResolveLowestHealthAlly(context, source, target.IncludeSelf),
            RotationTargetSelectionModes.FixedThenLowestHealthAlly =>
                ResolveFixed(context, target.ActorKey) ??
                ResolveLowestHealthAlly(context, source, target.IncludeSelf),
            _ => context.GetActor(defaultTargetKey)
        };
    }

    public static bool WatchesActor(
        SimulationContext context,
        SimulationActorState source,
        IReadOnlyList<RotationEntry> entries,
        string defaultTargetKey,
        string actorKey)
    {
        if (string.Equals(defaultTargetKey, actorKey, StringComparison.OrdinalIgnoreCase))
            return true;

        foreach (var entry in entries.Where(x => x.IsEnabled))
        {
            var target = entry.Target ?? new RotationTargetDefinition();

            if ((target.Mode == RotationTargetSelectionModes.Fixed ||
                 target.Mode == RotationTargetSelectionModes.FixedThenLowestHealthAlly) &&
                string.Equals(target.ActorKey, actorKey, StringComparison.OrdinalIgnoreCase))
                return true;

            if (target.Mode == RotationTargetSelectionModes.LowestHealthAlly ||
                target.Mode == RotationTargetSelectionModes.FixedThenLowestHealthAlly)
            {
                var actor = context.GetActor(actorKey);

                if (actor is not null &&
                    IsAlly(source, actor) &&
                    (target.IncludeSelf ||
                     !string.Equals(source.Key, actor.Key, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
        }

        return false;
    }

    private static SimulationActorState? ResolveFixed(
        SimulationContext context,
        string? actorKey)
    {
        if (string.IsNullOrWhiteSpace(actorKey))
            return null;

        var actor = context.GetActor(actorKey);
        return actor is { IsAlive: true } ? actor : null;
    }

    private static SimulationActorState? ResolveLowestHealthAlly(
        SimulationContext context,
        SimulationActorState source,
        bool includeSelf)
    {
        return context.Actors.Values
            .Where(actor => actor.IsAlive)
            .Where(actor => IsAlly(source, actor))
            .Where(actor => includeSelf ||
                !string.Equals(actor.Key, source.Key, StringComparison.OrdinalIgnoreCase))
            .OrderBy(GetHealthPercent)
            .ThenBy(actor => actor.CurrentHealth)
            .ThenBy(actor => actor.Key, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static bool IsAlly(
        SimulationActorState source,
        SimulationActorState candidate)
    {
        if (string.IsNullOrWhiteSpace(source.TeamKey) ||
            string.IsNullOrWhiteSpace(candidate.TeamKey))
        {
            return string.Equals(
                source.Key,
                candidate.Key,
                StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(
            source.TeamKey,
            candidate.TeamKey,
            StringComparison.OrdinalIgnoreCase);
    }

    private static decimal GetHealthPercent(SimulationActorState actor)
    {
        if (actor.MaximumHealth <= 0m)
            return 0m;

        return actor.CurrentHealth / actor.MaximumHealth * 100m;
    }
}
