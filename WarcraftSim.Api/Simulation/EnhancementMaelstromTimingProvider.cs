using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Forever Maelstrom Weapon: each current stack reduces the next normal
/// Lightning Bolt cast time by 4% per talent rank (max five stacks).
/// The engine snapshots this timing at cast start; unrelated spells retain
/// all ordinary aura/casting adjustments.
/// </summary>
public sealed class EnhancementMaelstromTimingProvider(
    Func<int> currentStacks, int talentRank) : IAbilityTimingProvider
{
    public AbilityTimingSnapshot Resolve(SimulationContext context,
        SimulationActorState source, AbilityDefinition ability)
    {
        var normal=BaseAbilityTimingProvider.Instance.Resolve(context,source,ability);
        if (ability.Key != "lightning-bolt" || talentRank <= 0) return normal;
        var reduction=Math.Clamp(.04m * talentRank *
            Math.Clamp(currentStacks(),0,5),0m,1m);
        return new AbilityTimingSnapshot
        {
            CastTimeSeconds=normal.CastTimeSeconds * (1m-reduction),
            GlobalCooldownSeconds=normal.GlobalCooldownSeconds,
            ChannelDurationSeconds=normal.ChannelDurationSeconds,
            ChannelTickIntervalSeconds=normal.ChannelTickIntervalSeconds
        };
    }
}
