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

        var baseline = AbilityTimingSnapshot.FromAbility(ability);
        if (baseline.CastTimeSeconds <= 0m)
            return baseline;
        decimal castMultiplier = 1m;
        foreach (var aura in source.ActiveAuras)
        {
            if (!aura.IsActiveAt(context.CurrentTimeSeconds)) continue;
            if (aura.Definition.CastTimeMultiplier < 1m)
                throw new InvalidOperationException("Cast-time aura multiplier must be at least 1.");
            castMultiplier *= aura.Definition.CastTimeMultiplier;
        }
        if (castMultiplier == 1m) return baseline;
        return new AbilityTimingSnapshot {
            CastTimeSeconds = baseline.CastTimeSeconds * castMultiplier,
            GlobalCooldownSeconds = baseline.GlobalCooldownSeconds,
            ChannelDurationSeconds = baseline.ChannelDurationSeconds,
            ChannelTickIntervalSeconds = baseline.ChannelTickIntervalSeconds
        };
    }
}
