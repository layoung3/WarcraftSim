namespace WarcraftSim.Core.Simulation.Engine;

/// <summary>
/// Describes how an effect occurrence is delivered. This is intentionally
/// separate from the effect's damage/healing type so rulesets can distinguish
/// direct effects, aura-driven periodic ticks, and channel ticks.
/// </summary>
public enum CombatEffectDeliveryType
{
    Direct = 0,
    Periodic = 1,
    ChannelTick = 2
}
