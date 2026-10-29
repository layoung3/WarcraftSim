using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Forever 70532 Improved Channeling. Incoming damage has a 20%/rank
/// chance not to interrupt Arcane Missiles and a 14%/rank chance
/// not to push back Arcane Blast / Arcane Wrath. Uses the shared
/// cast and channel engine, never a separate spell scheduler.
/// </summary>
public sealed class MageImprovedChannelingProcessor(
    AbilityExecutor executor,int rank) : ICombatEventProcessor
{
    public int EligibleHits {get;private set;}
    public int ResistedHits {get;private set;}
    public int ChannelsCancelled {get;private set;}
    public decimal CastPushbackSeconds {get;private set;}

    public void Process(SimulationContext context,CombatEvent ev)
    {
        if(ev.Type!=CombatEventType.Damage || ev.TargetActorKey!="mage" ||
            ev.SourceActorKey=="mage" || ev.Amount is not >0m)
            return;
        var mage=context.GetActor("mage");
        if(mage?.CurrentCastExecutionId is not Guid id)return;
        var cast=context.GetAbilityExecution(id);
        if(cast is null || cast.IsCancelled)return;
        var missile=cast.AbilityKey=="arcane-missiles" && cast.IsChanneling;
        var blast=cast.AbilityKey is ("arcane-blast" or "arcane-wrath") &&
            !cast.IsChanneling && cast.Timing.CastTimeSeconds>0m;
        if(!missile && !blast)return;
        EligibleHits++;
        var chance=rank*(missile?20m:14m);
        if(chance>0m && (decimal)context.Random.NextDouble()*100m<chance)
        {
            ResistedHits++;
            return;
        }
        if(missile)
        {
            if(executor.TryCancelCurrentCast(context,"mage"))ChannelsCancelled++;
        }
        else
            CastPushbackSeconds+=executor.TryPushbackCurrentCast(
                context,"mage",.5m,1m);
    }
}
