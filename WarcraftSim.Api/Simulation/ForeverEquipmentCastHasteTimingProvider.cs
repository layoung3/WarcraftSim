using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Preserves the class timing provider (Nightfall, Missile Barrage, etc) and
/// snapshots CURRENT equipment cast haste for newly started hardcasts only.
/// Classic-era cast-speed effects do not alter in-flight casts, spell GCDs,
/// channel tick frequency or DoT/HoT ticks. Forever hotfix exceptions pending.
/// </summary>
public sealed class ForeverEquipmentCastHasteTimingProvider(
    IAbilityTimingProvider inner) : IAbilityTimingProvider
{
    public AbilityTimingSnapshot Resolve(SimulationContext context,
        SimulationActorState source,AbilityDefinition ability)
    {
        var timing=inner.Resolve(context,source,ability);
        if(timing.CastTimeSeconds<=0m)return timing;
        var haste=source.Stats.Get("haste-percent")+
            source.Stats.Get("cast-haste-percent");
        if(haste<=0m)return timing;
        return new AbilityTimingSnapshot {
            CastTimeSeconds=timing.CastTimeSeconds/(1m+haste/100m),
            GlobalCooldownSeconds=timing.GlobalCooldownSeconds,
            ChannelDurationSeconds=timing.ChannelDurationSeconds,
            ChannelTickIntervalSeconds=timing.ChannelTickIntervalSeconds
        };
    }
}
