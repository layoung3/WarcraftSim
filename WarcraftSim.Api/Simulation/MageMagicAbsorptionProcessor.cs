using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Current 70532 Magic Absorption: rank 1/2 grants 1%/2% total Mana on a
/// fully resisted magical spell, limited to one activation per second.
/// The simulator never fabricates full-resist odds.
/// </summary>
public sealed class MageMagicAbsorptionProcessor(int rank) : ICombatEventProcessor
{
    private decimal _nextEligibleAt;
    public int Refunds { get; private set; }

    public void Process(SimulationContext context,CombatEvent ev)
    {
        if(rank<=0 || ev.Type!=CombatEventType.Damage ||
            ev.TargetActorKey!="mage" ||
            ev.SchoolKey is not ("arcane" or "fire" or "frost" or "holy" or "nature" or "shadow") ||
            ev.ResultKey!="resist" ||
            ev.Amount.GetValueOrDefault()!=0m ||
            context.CurrentTimeSeconds<_nextEligibleAt)
            return;
        var mage=context.GetActor("mage");
        if(mage is null || !mage.Resources.TryGetValue("mana",out var mana))
            return;
        _nextEligibleAt=context.CurrentTimeSeconds+1m;
        mana.AdvanceTo(context.CurrentTimeSeconds);
        var gained=mana.Gain(mana.Maximum*.01m*rank);
        if(gained<=0m)return;
        Refunds++;
        context.EmitEvent(new CombatEvent {
            TimeSeconds=context.CurrentTimeSeconds,
            Type=CombatEventType.ResourceChanged,
            SourceActorKey="mage",TargetActorKey="mage",
            AbilityKey="magic-absorption",EffectKey="full-resist-refund",
            ResourceKey="mana",Amount=gained,
            ResourceTotalAfter=mana.Current,
            Description="Fully resisted magical attack restored Mana."
        });
    }
}
