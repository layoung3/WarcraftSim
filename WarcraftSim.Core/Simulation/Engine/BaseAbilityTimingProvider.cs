using WarcraftSim.Core.Abilities;

namespace WarcraftSim.Core.Simulation.Engine;

/// <summary>
/// Default timing provider that preserves the timing stored on the ability.
/// This keeps existing simulations unchanged unless a ruleset explicitly
/// supplies a timing provider.
/// </summary>
public sealed class BaseAbilityTimingProvider :
    IAbilityTimingProvider
{
    public static BaseAbilityTimingProvider Instance { get; } =
        new();

    private BaseAbilityTimingProvider()
    {
    }

    public AbilityTimingSnapshot Resolve(
        SimulationContext context,
        SimulationActorState source,
        AbilityDefinition ability)
    {
        ArgumentNullException.ThrowIfNull(
            context
        );

        ArgumentNullException.ThrowIfNull(
            source
        );

        return AbilityTimingSnapshot.FromAbility(
            ability
        );
    }
}
