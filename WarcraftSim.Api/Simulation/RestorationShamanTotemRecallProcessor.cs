using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Healing;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Optional deterministic in-combat Recall. Reuses the normal AbilityExecutor
/// to respect the current GCD and the same ShamanTotemLifecycleProcessor for
/// removed slots/refunds. Fires once, never cycles calls/recalled totems itself.
/// </summary>
public sealed class RestorationShamanTotemRecallProcessor(
    SimulationActorState owner, AbilityExecutor executor,
    ShamanTotemLifecycleProcessor slots, decimal atSeconds) : ICombatEventProcessor
{
    private bool _used;
    public void Process(SimulationContext context, CombatEvent ev)
    {
        if (_used || ev.Type != CombatEventType.RotationDecision ||
            !ev.IsInternal || ev.EffectKey != HealerPriorityProcessor.PulseEffectKey ||
            ev.SourceActorKey != owner.Key ||
            context.CurrentTimeSeconds < atSeconds || slots.ActiveSlots.Count == 0)
            return;
        if(executor.TryStartAbility(context,owner.Key,owner.Key,"totemic-recall").Success)
            _used=true;
    }
}
