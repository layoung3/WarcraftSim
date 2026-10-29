using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Applies current Hunter aspect stat changes when the ordinary executor lands
/// an Aspect aura. Replaces competing source-owned aspects without stacking.
/// No pet AP inheritance is asserted here: Hawk grants hunter ranged AP only.
/// </summary>
public sealed class HunterAspectProcessor(
    SimulationActorState hunter, AuraManager auras,
    bool primaryAttackPowerIsRanged=true) : ICombatEventProcessor
{
    private bool _hawkActive;
    private bool _beastActive;
    private string HawkStatKey => primaryAttackPowerIsRanged ?
        ForeverCombatStatKeys.AttackPower : "hunter-ranged-attack-power";
    private string BeastStatKey => primaryAttackPowerIsRanged ?
        "hunter-melee-attack-power" : ForeverCombatStatKeys.AttackPower;
    public int HawkApplications { get; private set; }

    public void Process(SimulationContext context, CombatEvent ev)
    {
        if (ev.TargetActorKey != hunter.Key) return;
        if (ev.Type == CombatEventType.AuraApplied &&
            ev.AbilityKey is ("aspect-of-the-hawk" or "aspect-of-the-beast"))
        {
            foreach (var existing in hunter.ActiveAuras
                .Where(a => a.Definition.Key.StartsWith("hunter-aspect-", StringComparison.OrdinalIgnoreCase) &&
                    a.Definition.Key != (ev.AbilityKey=="aspect-of-the-hawk" ?
                        "hunter-aspect-hawk" : "hunter-aspect-beast")).ToList())
                auras.RemoveAura(context, hunter, existing, "another Hunter aspect activated");

            if(ev.AbilityKey=="aspect-of-the-hawk")
            {
                if(_hawkActive)return;
                _hawkActive=true;HawkApplications++;
                hunter.Stats.Set(HawkStatKey,hunter.Stats.Get(HawkStatKey)+120m);
            }
            else
            {
                if(_beastActive)return;
                _beastActive=true;
                hunter.Stats.Set(BeastStatKey,hunter.Stats.Get(BeastStatKey)+110m);
            }
        }
        else if (ev.Type == CombatEventType.AuraRemoved &&
            ev.AbilityKey == "aspect-of-the-hawk" && _hawkActive)
        {
            _hawkActive = false;
            hunter.Stats.Set(HawkStatKey,hunter.Stats.Get(HawkStatKey)-120m);
        }
        else if (ev.Type==CombatEventType.AuraRemoved &&
            ev.AbilityKey=="aspect-of-the-beast" && _beastActive)
        {
            _beastActive=false;
            hunter.Stats.Set(BeastStatKey,hunter.Stats.Get(BeastStatKey)-110m);
        }
    }
}
