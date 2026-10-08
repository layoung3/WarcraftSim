namespace WarcraftSim.Core.Simulation.Engine;

/// <summary>
/// Default auto-attack timing provider. It preserves the interval stored on
/// the auto-attack definition so existing simulations remain unchanged unless
/// a ruleset explicitly supplies another provider.
/// </summary>
public sealed class BaseAutoAttackTimingProvider :
    IAutoAttackTimingProvider
{
    public static BaseAutoAttackTimingProvider Instance { get; } =
        new();

    private BaseAutoAttackTimingProvider()
    {
    }

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

        return AutoAttackTimingSnapshot.FromDefinition(
            definition
        );
    }
}
