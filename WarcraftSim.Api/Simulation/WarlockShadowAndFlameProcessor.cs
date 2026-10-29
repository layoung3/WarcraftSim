using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Forever 70532 Shadow and Flame (ranks 1..5): landed Conflagrate grants
/// +2% per rank Shadow damage for 20s; landed Shadowburn grants the same
/// Fire bonus for 20s and has 20% per rank immediate shard refund.
/// The separate Conflagrate Immolate-retention effect is owned by the
/// existing WarlockConflagrateConsumptionProcessor.
/// </summary>
public sealed class WarlockShadowAndFlameProcessor(
    AuraManager auras,int rank) : ICombatEventProcessor
{
    public const string ShadowAura="warlock-shadow-and-flame-shadow";
    public const string FireAura="warlock-shadow-and-flame-fire";
    public int ShadowBuffs {get;private set;}
    public int FireBuffs {get;private set;}
    public int ImmediateShardsRestored {get;private set;}

    public void Process(SimulationContext context,CombatEvent ev)
    {
        if(rank<=0 || ev.Type!=CombatEventType.Damage ||
            ev.SourceActorKey!="warlock" || ev.TargetActorKey is not { } target ||
            context.GetActor(target) is not {TeamKey:"enemy"} ||
            ev.Amount is not >0m || ev.ResultKey is not ("hit" or "critical") ||
            ev.IsPeriodic || ev.IsChannelTick ||
            ev.AbilityKey is not ("conflagrate" or "shadowburn"))
            return;
        var owner=context.GetActor("warlock");
        if(owner is null)return;
        var conflagrate=ev.AbilityKey=="conflagrate";
        var aura=conflagrate?ShadowAura:FireAura;
        auras.ApplyAura(context,owner,new AuraDefinition {
            Key=aura,Name=conflagrate?"Shadow and Flame (Shadow)":"Shadow and Flame (Fire)",
            DurationSeconds=20m
        },owner.Key,"shadow-and-flame","shadow-and-flame-buff");
        if(conflagrate)ShadowBuffs++;
        else
        {
            FireBuffs++;
            if(owner.Resources.TryGetValue("soul-shard",out var shards) &&
                (decimal)context.Random.NextDouble()*100m < rank*20m)
            {
                shards.AdvanceTo(context.CurrentTimeSeconds);
                var gained=shards.Gain(1m);
                if(gained<=0m)return;
                ImmediateShardsRestored++;
                context.EmitEvent(new CombatEvent {
                    TimeSeconds=context.CurrentTimeSeconds,Type=CombatEventType.ResourceChanged,
                    SourceActorKey=owner.Key,TargetActorKey=owner.Key,
                    AbilityKey="shadow-and-flame",EffectKey="shadowburn-immediate-shard-refund",
                    ResourceKey="soul-shard",Amount=gained,
                    ResourceTotalAfter=shards.Current,
                    Description="Shadow and Flame immediately refunded Shadowburn's Soul Shard."
                });
            }
        }
    }
}
