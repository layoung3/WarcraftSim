using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Stats;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Applies current Hunter aspect stat changes when the ordinary executor lands
/// an Aspect aura. Replaces competing source-owned aspects without stacking.
/// No pet AP inheritance is asserted here: Hawk grants hunter ranged AP only.
/// </summary>
public sealed class HunterAspectProcessor(
    SimulationActorState hunter, AuraManager auras) : ICombatEventProcessor
{
    private bool _hawkActive;
    public int HawkApplications { get; private set; }

    public void Process(SimulationContext context, CombatEvent ev)
    {
        if (ev.TargetActorKey != hunter.Key) return;
        if (ev.Type == CombatEventType.AuraApplied &&
            ev.AbilityKey == "aspect-of-the-hawk")
        {
            foreach (var existing in hunter.ActiveAuras
                .Where(a => a.Definition.Key.StartsWith("hunter-aspect-", StringComparison.OrdinalIgnoreCase) &&
                    a.Definition.Key != "hunter-aspect-hawk").ToList())
                auras.RemoveAura(context, hunter, existing, "another Hunter aspect activated");

            if (_hawkActive) return;
            _hawkActive = true;
            HawkApplications++;
            hunter.Stats.Set(ForeverCombatStatKeys.AttackPower,
                hunter.Stats.Get(ForeverCombatStatKeys.AttackPower) + 120m);
        }
        else if (ev.Type == CombatEventType.AuraRemoved &&
            ev.AbilityKey == "aspect-of-the-hawk" && _hawkActive)
        {
            _hawkActive = false;
            hunter.Stats.Set(ForeverCombatStatKeys.AttackPower,
                hunter.Stats.Get(ForeverCombatStatKeys.AttackPower) - 120m);
        }
    }
}
