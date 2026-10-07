using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Data.Forever.Combat;

/// <summary>
/// Applies the current Forever haste model to cast time. The beta client
/// exposes haste rating and spell/ability cast times, while current evidence
/// does not establish a universal haste reduction for global cooldowns or a
/// verified channel/swing timing model. Those timings therefore remain at
/// their ability-defined values for now.
/// </summary>
public sealed class ForeverAbilityTimingProvider :
    IAbilityTimingProvider
{
    // The current beta exposes haste rating but not the server-side timing
    // formula. The standard Classic-style haste divisor remains provisional.
    public const bool CastHasteApplicationVerified =
        false;

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

        ArgumentNullException.ThrowIfNull(
            ability
        );

        var baseTiming =
            AbilityTimingSnapshot.FromAbility(
                ability
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

        var castTimeSeconds =
            baseTiming.CastTimeSeconds <= 0m
                ? 0m
                : baseTiming.CastTimeSeconds /
                  hasteMultiplier;

        return new AbilityTimingSnapshot
        {
            CastTimeSeconds =
                castTimeSeconds,

            // Forever currently exposes per-ability GCD values. Haste does
            // not globally rewrite them; explicit effects can modify GCDs
            // separately when those mechanics are modeled.
            GlobalCooldownSeconds =
                baseTiming.GlobalCooldownSeconds,

            // Channel haste behavior remains intentionally unchanged until
            // measured/verified for Forever.
            ChannelDurationSeconds =
                baseTiming.ChannelDurationSeconds,

            ChannelTickIntervalSeconds =
                baseTiming.ChannelTickIntervalSeconds
        };
    }
}
