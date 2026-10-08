using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Data.Forever.Combat;

/// <summary>
/// Applies the actor's resolved Forever haste percentage to background weapon
/// swing cadence. Weapon damage/AP scaling continues to use base weapon speed;
/// haste changes frequency, not the AP coefficient of an individual hit.
/// </summary>
public sealed class ForeverAutoAttackTimingProvider :
    IAutoAttackTimingProvider
{
    public AutoAttackTimingSnapshot Resolve(
        SimulationContext context,
        SimulationActorState source,
        AutoAttackDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(
            context
        );

        ArgumentNullException.ThrowIfNull(
            source
        );

        ArgumentNullException.ThrowIfNull(
            definition
        );

        var hastePercent =
            Math.Max(
                0m,
                source.Stats.Get(
                    ForeverCombatStatKeys.HastePercent
                )
            );

        var hasteMultiplier =
            1m +
            hastePercent / 100m;

        return new AutoAttackTimingSnapshot
        {
            SwingIntervalSeconds =
                definition.SwingIntervalSeconds /
                hasteMultiplier
        };
    }
}
