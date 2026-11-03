using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// November 2 Forever notes: Soul Shards can proc on eligible Drain Soul
/// DAMAGE, not only a killing blow. The reduced chance has not been published;
/// keep the explicit encounter-provided base probability. The previously
/// announced progressive chance increases after each unsuccessful tick until
/// guaranteed, without introducing a kill-only trigger.
/// </summary>
public sealed class WarlockDrainSoulShardProcessor(
    bool eligibleTarget, decimal nonLethalChancePercent) : ICombatEventProcessor
{
    private readonly Dictionary<string,int> _failedTicksByTarget =
        new(StringComparer.OrdinalIgnoreCase);
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
        // The published beta describes a progressive chance until guaranteed.
        // When the actual reduced baseline is unknown, a configured fixture
        // supplies the initial chance while failure-streak progression executes.
        var failures=_failedTicksByTarget.GetValueOrDefault(key);
        var chance=Math.Min(100m,nonLethalChancePercent*(failures+1));
        if((decimal)context.Random.NextDouble()*100m < chance)
        {
            _failedTicksByTarget.Remove(key);
            Grant(context);
        }
        else _failedTicksByTarget[key]=failures+1;
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
