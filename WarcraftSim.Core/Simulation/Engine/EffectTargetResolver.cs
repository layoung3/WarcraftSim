using WarcraftSim.Core.Abilities;

namespace WarcraftSim.Core.Simulation.Engine;

public static class EffectTargetResolver
{
    public static IReadOnlyList<SimulationActorState> Resolve(
        SimulationContext context,
        SimulationActorState source,
        SimulationActorState primaryTarget,
        AbilityEffectDefinition effect)
    {
        ArgumentNullException.ThrowIfNull(
            context
        );

        ArgumentNullException.ThrowIfNull(
            source
        );

        ArgumentNullException.ThrowIfNull(
            primaryTarget
        );

        ArgumentNullException.ThrowIfNull(
            effect
        );

        var maxTargets =
            Math.Max(
                1,
                effect.MaxTargets
            );

        if (string.Equals(
                effect.TargetType,
                AbilityTargetTypes.Self,
                StringComparison.OrdinalIgnoreCase))
        {
            return source.IsAlive
                ? [source]
                : [];
        }

        Func<SimulationActorState, bool> isEligible =
            string.Equals(
                effect.TargetType,
                AbilityTargetTypes.Friendly,
                StringComparison.OrdinalIgnoreCase)
                ? actor =>
                    actor.IsAlive &&
                    IsSameTeam(
                        source,
                        actor
                    )
                : string.Equals(
                    effect.TargetType,
                    AbilityTargetTypes.Enemy,
                    StringComparison.OrdinalIgnoreCase)
                    ? actor =>
                        actor.IsAlive &&
                        !IsSameTeam(
                            source,
                            actor
                        )
                    : _ => false;

        var resolved =
            new List<SimulationActorState>(
                maxTargets
            );

        if (isEligible(
                primaryTarget))
        {
            resolved.Add(
                primaryTarget
            );
        }

        foreach (
            var candidate in
            context.Actors.Values
                .Where(isEligible)
                .Where(candidate =>
                    !string.Equals(
                        candidate.Key,
                        primaryTarget.Key,
                        StringComparison.OrdinalIgnoreCase
                    ))
                .OrderBy(
                    candidate =>
                        candidate.Key,
                    StringComparer.OrdinalIgnoreCase
                ))
        {
            if (resolved.Count >= maxTargets)
            {
                break;
            }

            resolved.Add(
                candidate
            );
        }

        return resolved;
    }

    private static bool IsSameTeam(
        SimulationActorState source,
        SimulationActorState candidate)
    {
        return string.Equals(
            source.TeamKey,
            candidate.TeamKey,
            StringComparison.OrdinalIgnoreCase
        );
    }
}
