using WarcraftSim.Core.Abilities;

namespace WarcraftSim.Core.Simulation.Engine;

/// <summary>
/// The resolved timing values for one ability execution. Keeping these values
/// together allows ruleset-specific timing adjustments to be snapshotted when
/// the cast starts instead of being recalculated partway through an action.
/// </summary>
public sealed class AbilityTimingSnapshot
{
    public decimal CastTimeSeconds { get; init; }

    public decimal GlobalCooldownSeconds { get; init; }

    public decimal ChannelDurationSeconds { get; init; }

    public decimal ChannelTickIntervalSeconds { get; init; }

    public static AbilityTimingSnapshot FromAbility(
        AbilityDefinition ability)
    {
        ArgumentNullException.ThrowIfNull(
            ability
        );

        return new AbilityTimingSnapshot
        {
            CastTimeSeconds =
                Math.Max(
                    0m,
                    ability.CastTimeSeconds
                ),

            GlobalCooldownSeconds =
                Math.Max(
                    0m,
                    ability.GlobalCooldownSeconds
                ),

            ChannelDurationSeconds =
                Math.Max(
                    0m,
                    ability.ChannelDurationSeconds
                ),

            ChannelTickIntervalSeconds =
                Math.Max(
                    0m,
                    ability.ChannelTickIntervalSeconds
                )
        };
    }
}
