using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Damage-driven cast/channel interruption for Forever Warlock DPS.
/// Fel Concentration protects Drain Life/Mana/Soul and Wrack; Intensity
/// protects Destruction casts/channels. 70532 ranks 23/47/70%.
/// No encounter attacks are fabricated: only actual incoming Damage events
/// can interrupt a live cast or cancel its scheduled channel ticks.
/// </summary>
public sealed class WarlockIncomingCastInterruptionProcessor(
    AbilityExecutor executor,int felConcentrationRank,int intensityRank)
    : ICombatEventProcessor
{
    public int EligibleHits { get; private set; }
    public int ResistedHits { get; private set; }
    public int CancelledChannels { get; private set; }
    public decimal HardcastPushbackSeconds { get; private set; }

    public void Process(SimulationContext context,CombatEvent ev)
    {
        if(ev.Type!=CombatEventType.Damage ||
           ev.TargetActorKey!="warlock" ||
           ev.SourceActorKey=="warlock" ||
           ev.Amount is not >0m)
            return;
        var caster=context.GetActor("warlock");
        if(caster?.CurrentCastExecutionId is not Guid id)return;
        var current=context.GetAbilityExecution(id);
        if(current is null || current.IsCancelled ||
           !caster.Abilities.TryGetValue(current.AbilityKey,out var active))
            return;
        var key=active.Definition.Key;
        var drain = key is "drain-life" or "drain-soul" or "drain-mana" or "wrack";
        var destruction = key is "shadow-bolt" or "immolate" or "searing-pain" or
            "soul-fire" or "shadowburn" or "incinerate" or "conflagrate" or
            "rain-of-fire" or "hellfire";
        if(!drain && !destruction)return;
        if(!current.IsChanneling && current.Timing.CastTimeSeconds<=0m)return;

        EligibleHits++;
        var rank=drain?felConcentrationRank:intensityRank;
        var chance=rank switch {1=>23m,2=>47m,3=>70m,_=>0m};
        if(chance>0m && (decimal)context.Random.NextDouble()*100m<chance)
        {
            ResistedHits++;
            return;
        }
        if(current.IsChanneling)
        {
            if(executor.TryCancelCurrentCast(context,caster.Key))
                CancelledChannels++;
        }
        else
        {
            HardcastPushbackSeconds+=executor.TryPushbackCurrentCast(
                context,caster.Key,.5m,1m);
        }
    }
}
