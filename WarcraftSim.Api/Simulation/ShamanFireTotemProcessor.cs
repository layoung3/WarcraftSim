using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Links a source-owned Fire totem lifetime to Fire Nova eligibility. Legacy
/// callers retain their accepted 55-second Searing marker event contract.
/// Class-wide callers derive the lifetime from live Searing, Magma or
/// Flametongue auras and use the slot and movement processors alongside this.
/// </summary>
public sealed class ShamanFireTotemProcessor(
    SimulationActorState owner,AuraManager auras,bool classWide=false) : ICombatEventProcessor
{
    public int Placements {get;private set;}

    public void Process(SimulationContext context,CombatEvent ev)
    {
        if(ev.SourceActorKey!=owner.Key || (classWide
            ? ev.EffectKey is not ("searing-totem-tick" or "magma-totem-tick" or "flametongue-totem-aura")
            : ev.EffectKey!="searing-totem-tick"))return;
        if(ev.Type==CombatEventType.AuraApplied &&
            (ev.TargetActorKey!=owner.Key || ev.EffectKey=="flametongue-totem-aura"))
        {
            var aura=context.GetActor(ev.TargetActorKey ?? "")?.ActiveAuras.LastOrDefault(x=>
                x.Definition.Key is "searing-totem" or "magma-totem" or "shaman-flametongue-totem" &&
                x.SourceActorKey==owner.Key && x.IsActiveAt(context.CurrentTimeSeconds));
            if(classWide&&aura is null)return;
            Placements++;
            auras.ApplyAura(context,owner,new AuraDefinition {
                Key="shaman-fire-totem-active",Name="Active Fire Totem",
                DurationSeconds=classWide ? aura!.ExpiresAtSeconds-context.CurrentTimeSeconds : 55m
            },owner.Key,"shaman-fire-totem-state","fire-totem-slot");
        }
        else if(ev.Type==CombatEventType.AuraRemoved &&
            (ev.TargetActorKey!=owner.Key || ev.EffectKey=="flametongue-totem-aura"))
        {
            // This event is tied to the original periodic aura's owner.
            // A refreshed totem remains active until its new aura expires.
            var candidates=classWide ? context.Actors.Values.AsEnumerable() :
                context.Actors.Values.Where(x=>x.Key==ev.TargetActorKey);
            if(!candidates.Any(target=>target.ActiveAuras.Any(x=>
                x.Definition.Key is "searing-totem" or "magma-totem" or "shaman-flametongue-totem" &&
                x.SourceActorKey==owner.Key &&
                x.IsActiveAt(context.CurrentTimeSeconds))))
                auras.RemoveAuras(context,owner,"shaman-fire-totem-active",
                    owner.Key,"Searing Totem expired");
        }
    }
}
