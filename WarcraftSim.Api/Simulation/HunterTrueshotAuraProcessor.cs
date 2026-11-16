using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Hunter;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Make trained rank-5 Trueshot Aura a live owner RAP contribution, rather
/// than a hardcoded precombat bonus which persists if its aura is removed.
/// Reuses the existing Hunter owner/pet stat inheritance for each transition.
/// Party/raid distribution and geometry remain outside Solo DPS.
/// </summary>
public sealed class HunterTrueshotAuraProcessor(
    SimulationActorState owner, SimulationActorState? pet,
    ForeverBeastMasteryHunterTuning petTuning,
    decimal rangedAttackPower,bool rangedPrimary=true) : ICombatEventProcessor
{
    private bool _active;
    public int Applications {get;private set;}
    public int Removals {get;private set;}

    public void Process(SimulationContext context, CombatEvent ev)
    {
        if(ev.SourceActorKey!=owner.Key || ev.TargetActorKey!=owner.Key ||
           ev.AbilityKey!="trueshot-aura")return;
        var applying=ev.Type==CombatEventType.AuraApplied;
        var removing=ev.Type==CombatEventType.AuraRemoved;
        if(!applying && !removing || applying && _active ||
           removing && !_active)return;
        _active=applying;
        if(applying)Applications++;
        else Removals++;
        ForeverEquipmentHunterDynamicStats.Apply(context,owner,pet,petTuning,
            new Dictionary<string,decimal>(StringComparer.OrdinalIgnoreCase){
                ["ranged-attack-power"]=applying?rangedAttackPower:-rangedAttackPower
            },rangedPrimary:rangedPrimary);
    }
}
