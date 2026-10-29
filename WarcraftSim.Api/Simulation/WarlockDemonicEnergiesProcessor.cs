using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Forever 70532 Demonic Energies: an ACTIVE summoned demon receives
/// 8/15% of owner spell damage as effective healing (rank 1/2), and
/// 50/100% of owner Life Tap Mana gain (capped to the pet resource).
/// Runs on already-resolved combat events; pet attacks never double-trigger.
/// </summary>
public sealed class WarlockDemonicEnergiesProcessor(
    int rank,Func<WarlockDemonCompanion?> activeCompanion):ICombatEventProcessor
{
    public decimal PetHealing {get;private set;}
    public decimal PetManaGained {get;private set;}

    public void Process(SimulationContext context,CombatEvent ev)
    {
        if(rank==0 || ev.SourceActorKey!="warlock")return;
        var companion=activeCompanion();
        if(companion?.IsActive!=true)return;
        var pet=companion.Actor;
        if(ev.Type==CombatEventType.Damage && ev.Amount is >0m &&
            ev.TargetActorKey is { } targetKey &&
            context.GetActor(targetKey) is {TeamKey:"enemy"} &&
            ev.SchoolKey is ("arcane" or "fire" or "frost" or "holy" or "nature" or "shadow"))
        {
            var raw=ev.Amount.Value*(rank==1?.08m:.15m);
            var actual=pet.Heal(raw);
            PetHealing+=actual;
            context.EmitEvent(new CombatEvent{
                TimeSeconds=context.CurrentTimeSeconds,Type=CombatEventType.Healing,
                SourceActorKey="warlock",TargetActorKey=pet.Key,
                AbilityKey="demonic-energies",EffectKey="demonic-energies-pet-heal",
                RawAmount=raw,Amount=actual,OverhealingAmount=raw-actual,
                Description="Owner spell damage restored active demon health."
            });
        }
        else if(ev.Type==CombatEventType.ResourceChanged &&
            ev.AbilityKey=="life-tap" && ev.ResourceKey=="mana" &&
            ev.Amount is >0m && pet.Resources.TryGetValue("mana",out var petMana))
        {
            petMana.AdvanceTo(context.CurrentTimeSeconds);
            var gain=petMana.Gain(ev.Amount.Value*(rank==1?.5m:1m));
            if(gain<=0m)return;
            PetManaGained+=gain;
            context.EmitEvent(new CombatEvent {
                TimeSeconds=context.CurrentTimeSeconds,
                Type=CombatEventType.ResourceChanged,
                SourceActorKey=pet.Key,TargetActorKey=pet.Key,
                AbilityKey="demonic-energies",EffectKey="demonic-energies-mana",
                ResourceKey="mana",Amount=gain,ResourceTotalAfter=petMana.Current,
                Description="Demonic Energies transferred owner's Life Tap Mana gain."
            });
        }
    }
}
