using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Links the existing Searing Totem's target-side periodic aura to a
/// source-owned Fire totem lifetime for Fire Nova eligibility. This is a
/// stationary solo proxy: totem health/radius and other Fire totem conflicts
/// still require a full slot-lifetime subsystem.
/// </summary>
public sealed class ShamanFireTotemProcessor(
    SimulationActorState owner,AuraManager auras) : ICombatEventProcessor
{
    public int Placements {get;private set;}

    public void Process(SimulationContext context,CombatEvent ev)
    {
        if(ev.SourceActorKey!=owner.Key || ev.AbilityKey!="searing-totem")return;
        if(ev.Type==CombatEventType.AuraApplied &&
            ev.TargetActorKey!=owner.Key)
        {
            Placements++;
            auras.ApplyAura(context,owner,new AuraDefinition {
                Key="shaman-fire-totem-active",Name="Active Searing Fire Totem",
                DurationSeconds=55m
            },owner.Key,"shaman-fire-totem-state","fire-totem-slot");
        }
        else if(ev.Type==CombatEventType.AuraRemoved &&
            ev.TargetActorKey!=owner.Key)
        {
            // This event is tied to the original periodic aura's owner.
            // A refreshed totem remains active until its new aura expires.
            var target=context.GetActor(ev.TargetActorKey ?? "");
            if(target?.ActiveAuras.Any(x=>x.Definition.Key=="searing-totem" &&
                x.SourceActorKey==owner.Key &&
                x.IsActiveAt(context.CurrentTimeSeconds))!=true)
                auras.RemoveAuras(context,owner,"shaman-fire-totem-active",
                    owner.Key,"Searing Totem expired");
        }
    }
}
