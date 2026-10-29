using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Client 70532: an eligible non-trivial enemy killed by Drain Soul yields
/// one Soul Shard. Nonlethal ticks may also yield a shard; their server-side
/// probability is explicitly configured rather than invented.
/// </summary>
public sealed class WarlockDrainSoulShardProcessor(
    bool eligibleTarget, decimal nonLethalChancePercent) : ICombatEventProcessor
{
    private readonly Dictionary<string, decimal> _killingTicks = new(
        StringComparer.OrdinalIgnoreCase);
    public int Generated { get; private set; }

    public void Process(SimulationContext context, CombatEvent ev)
    {
        if (!eligibleTarget) return;
        if (ev.Type == CombatEventType.Damage && ev.AbilityKey == "drain-soul" &&
            ev.SourceActorKey == "warlock" && ev.TargetActorKey is { } key &&
            ev.Amount is >0m && ev.ResultKey is ("hit" or "critical") &&
            ev.IsChannelTick)
        {
            var target=context.GetActor(key);
            if(target?.IsAlive == false)
                _killingTicks[key]=context.CurrentTimeSeconds;
            else if(nonLethalChancePercent>0m &&
                (decimal)context.Random.NextDouble()*100m<nonLethalChancePercent)
                Grant(context,"drain-soul-tick-shard");
        }
        if(ev.Type==CombatEventType.ActorDied && ev.TargetActorKey is { } dead &&
            _killingTicks.Remove(dead,out var time) &&
            time==context.CurrentTimeSeconds)
            Grant(context,"drain-soul-kill-shard");
    }

    private void Grant(SimulationContext context,string effect)
    {
        var actor=context.GetActor("warlock");
        if(actor is null || !actor.Resources.TryGetValue("soul-shard",out var shards))
            return;
        shards.AdvanceTo(context.CurrentTimeSeconds);
        var amount=shards.Gain(1m);
        if(amount<=0m) return;
        Generated++;
        context.EmitEvent(new CombatEvent {
            TimeSeconds=context.CurrentTimeSeconds,
            Type=CombatEventType.ResourceChanged,
            SourceActorKey=actor.Key,TargetActorKey=actor.Key,
            AbilityKey="drain-soul",EffectKey=effect,
            ResourceKey="soul-shard",Amount=amount,
            ResourceTotalAfter=shards.Current,
            Description="Eligible Drain Soul generated a Soul Shard."
        });
    }
}
