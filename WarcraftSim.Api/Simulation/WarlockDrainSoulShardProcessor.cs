using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// November 2 Forever notes: Soul Shards can proc on eligible Drain Soul
/// DAMAGE, not only a killing blow. The reduced chance has not been published;
/// keep the explicit encounter-provided probability, without inventing a
/// number or guaranteeing a kill drop.
/// </summary>
public sealed class WarlockDrainSoulShardProcessor(
    bool eligibleTarget, decimal nonLethalChancePercent) : ICombatEventProcessor
{
    public int Generated { get; private set; }

    public void Process(SimulationContext context, CombatEvent ev)
    {
        if (!eligibleTarget || nonLethalChancePercent<=0m ||
            ev.Type!=CombatEventType.Damage || ev.AbilityKey!="drain-soul" ||
            ev.SourceActorKey!="warlock" || ev.TargetActorKey is not { } key ||
            ev.Amount is not >0m || ev.ResultKey is not ("hit" or "critical") ||
            !ev.IsChannelTick)
            return;
        var target=context.GetActor(key);
        if(target is null || target.TeamKey==context.GetActor("warlock")?.TeamKey)
            return;
        if((decimal)context.Random.NextDouble()*100m < nonLethalChancePercent)
            Grant(context);
    }

    private void Grant(SimulationContext context)
    {
        var actor=context.GetActor("warlock");
        if(actor is null || !actor.Resources.TryGetValue("soul-shard",out var shards))
            return;
        shards.AdvanceTo(context.CurrentTimeSeconds);
        var amount=shards.Gain(1m);
        if(amount<=0m)return;
        Generated++;
        context.EmitEvent(new CombatEvent {
            TimeSeconds=context.CurrentTimeSeconds,
            Type=CombatEventType.ResourceChanged,
            SourceActorKey=actor.Key,TargetActorKey=actor.Key,
            AbilityKey="drain-soul",EffectKey="drain-soul-damage-shard",
            ResourceKey="soul-shard",Amount=amount,
            ResourceTotalAfter=shards.Current,
            Description="Eligible damaging Drain Soul tick generated a Soul Shard."
        });
    }
}
