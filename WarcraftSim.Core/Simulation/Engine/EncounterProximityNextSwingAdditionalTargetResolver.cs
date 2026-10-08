namespace WarcraftSim.Core.Simulation.Engine;

/// <summary>
/// Resolves secondary targets for queued weapon attacks from encounter
/// proximity links. The encounter model deliberately stores only the semantic
/// "in cleave range" relationship today; exact distances can replace this
/// resolver later without changing the queued-attack model.
/// </summary>
public sealed class EncounterProximityNextSwingAdditionalTargetResolver :
    INextSwingAdditionalTargetResolver
{
    public static EncounterProximityNextSwingAdditionalTargetResolver Instance { get; } =
        new();

    private EncounterProximityNextSwingAdditionalTargetResolver()
    {
    }

    public IReadOnlyList<SimulationActorState> Resolve(
        SimulationContext context,
        SimulationActorState source,
        SimulationActorState primaryTarget,
        NextSwingReplacementDefinition replacement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(primaryTarget);
        ArgumentNullException.ThrowIfNull(replacement);

        if (
            replacement.MaximumAdditionalTargets <= 0 ||
            context.Encounter is null ||
            context.Encounter.TargetProximityLinks.Count == 0)
        {
            return [];
        }

        var linkedActorKeys =
            context.Encounter.TargetProximityLinks
                .Where(link => link.InCleaveRange)
                .Select(link => ResolveOtherActorKey(link.TargetAKey, link.TargetBKey, primaryTarget.Key))
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Select(key => key!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase);

        var resolved =
            new List<SimulationActorState>();

        foreach (var actorKey in linkedActorKeys)
        {
            var actor = context.GetActor(actorKey);

            if (
                actor is null ||
                !actor.IsAlive ||
                string.Equals(actor.Key, primaryTarget.Key, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actor.TeamKey, source.TeamKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            resolved.Add(actor);

            if (resolved.Count >= replacement.MaximumAdditionalTargets)
            {
                break;
            }
        }

        return resolved;
    }

    private static string? ResolveOtherActorKey(
        string targetAKey,
        string targetBKey,
        string primaryTargetKey)
    {
        if (string.Equals(targetAKey, primaryTargetKey, StringComparison.OrdinalIgnoreCase))
        {
            return targetBKey;
        }

        if (string.Equals(targetBKey, primaryTargetKey, StringComparison.OrdinalIgnoreCase))
        {
            return targetAKey;
        }

        return null;
    }
}
