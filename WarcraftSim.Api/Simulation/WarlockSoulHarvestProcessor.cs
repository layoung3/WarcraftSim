using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Current Forever Soul Harvest (two ranks): killing a non-trivial enemy
/// with a landed Drain Soul tick boosts Spirit Mana regeneration by 50/100%
/// and allows 50/100% of ordinary Spirit regeneration within the five-second
/// rule for ten seconds. Uses the existing five-second Mana processor.
/// </summary>
public sealed class WarlockSoulHarvestProcessor(
    AuraManager auras,int talentRank,bool eligibleTarget):ICombatEventProcessor
{
    private readonly Dictionary<string,decimal> _kills=new(
        StringComparer.OrdinalIgnoreCase);
    public int Triggers { get; private set; }

    public void Process(SimulationContext context,CombatEvent ev)
    {
        if(talentRank<=0 || !eligibleTarget)return;
        if(ev.Type==CombatEventType.Damage && ev.AbilityKey=="drain-soul" &&
            ev.SourceActorKey=="warlock" && ev.TargetActorKey is { } key &&
            ev.IsChannelTick && ev.Amount is >0m &&
            context.GetActor(key)?.IsAlive==false)
            _kills[key]=context.CurrentTimeSeconds;
        if(ev.Type!=CombatEventType.ActorDied || ev.TargetActorKey is not { } dead ||
            !_kills.Remove(dead,out var timestamp) ||
            timestamp!=context.CurrentTimeSeconds)
            return;
        var owner=context.GetActor("warlock");
        if(owner is null)return;
        auras.ApplyAura(context,owner,new AuraDefinition {
            Key="warlock-soul-harvest",Name="Soul Harvest",
            DurationSeconds=10m
        },owner.Key,"soul-harvest","soul-harvest-regen");
        Triggers++;
    }
}
