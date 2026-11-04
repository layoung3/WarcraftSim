using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Warlock;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Finite five-second Drain Mana channel. Runs on existing executor channel
/// tick events, after cast and interruption checks. An unconfigured target
/// has no Mana; transfers are capped by its real remaining resource.
/// </summary>
public sealed class WarlockDrainManaProcessor(int improvedDrainsRank) : ICombatEventProcessor
{
    private readonly decimal _perTick =
        ForeverWarlockDrainMana.ModifiedPerTick(4, improvedDrainsRank);

    public decimal Transferred { get; private set; }
    public int SuccessfulTicks { get; private set; }

    public void Process(SimulationContext context, CombatEvent ev)
    {
        if(ev.Type!=CombatEventType.AbilityChannelTick ||
           ev.AbilityKey!="drain-mana" || ev.SourceActorKey!="warlock" ||
           ev.TargetActorKey is not { } key || ev.AbilityExecutionId is not { } id)
            return;
        var execution=context.GetAbilityExecution(id);
        if(execution is null || execution.IsCancelled || !execution.IsChanneling ||
           execution.SourceActorKey!="warlock" || execution.TargetActorKey!=key)
            return;
        var owner=context.GetActor("warlock");
        var target=context.GetActor(key);
        if(owner is null || !owner.IsAlive || target is null || !target.IsAlive ||
           target.TeamKey==owner.TeamKey ||
           !owner.Resources.TryGetValue("mana",out var casterMana) ||
           !target.Resources.TryGetValue("mana",out var enemyMana))
            return;
        casterMana.AdvanceTo(context.CurrentTimeSeconds);
        enemyMana.AdvanceTo(context.CurrentTimeSeconds);
        var drained=Math.Min(_perTick,enemyMana.Current);
        if(drained<=0m || !enemyMana.Spend(drained))return;
        Transferred+=drained;
        SuccessfulTicks++;
        context.EmitEvent(new CombatEvent {
            TimeSeconds=context.CurrentTimeSeconds,Type=CombatEventType.ResourceChanged,
            SourceActorKey=owner.Key,TargetActorKey=target.Key,
            AbilityExecutionId=ev.AbilityExecutionId,AbilityKey="drain-mana",
            EffectKey="drain-mana-target",ResourceKey="mana",Amount=-drained,
            ResourceTotalAfter=enemyMana.Current,
            Description="Drain Mana removed finite Mana from its target."
        });
        var restored=casterMana.Gain(drained);
        if(restored>0m)
            context.EmitEvent(new CombatEvent {
                TimeSeconds=context.CurrentTimeSeconds,Type=CombatEventType.ResourceChanged,
                SourceActorKey=owner.Key,TargetActorKey=owner.Key,
                AbilityExecutionId=ev.AbilityExecutionId,AbilityKey="drain-mana",
                EffectKey="drain-mana-caster",ResourceKey="mana",Amount=restored,
                ResourceTotalAfter=casterMana.Current,
                Description="Drain Mana transferred available target Mana to caster."
            });
    }
}
