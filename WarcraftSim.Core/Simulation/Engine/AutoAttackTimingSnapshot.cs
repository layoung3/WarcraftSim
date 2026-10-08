namespace WarcraftSim.Core.Simulation.Engine;

/// <summary>
/// Resolved timing for one auto-attack scheduling decision.
/// Keeping this separate from the immutable definition allows rulesets and
/// temporary actor state to alter swing cadence without rewriting weapon data.
/// </summary>
public sealed class AutoAttackTimingSnapshot
{
    public decimal SwingIntervalSeconds { get; init; }

    public static AutoAttackTimingSnapshot FromDefinition(
        AutoAttackDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(
            definition
        );

        return new AutoAttackTimingSnapshot
        {
            SwingIntervalSeconds =
                definition.SwingIntervalSeconds
        };
    }
}
