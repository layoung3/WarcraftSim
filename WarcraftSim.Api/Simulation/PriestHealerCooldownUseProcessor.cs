using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Healing;

namespace WarcraftSim.Api.Simulation;

/// <summary>Opt-in on-use discipline cooldowns, ahead of normal healer decisions.</summary>
public sealed class PriestHealerCooldownUseProcessor(SimulationActorState priest,
    AbilityExecutor executor) : ICombatEventProcessor
{
    public void Process(SimulationContext ctx, CombatEvent ev)
    {
        if (ev.Type != CombatEventType.RotationDecision || !ev.IsInternal ||
            ev.EffectKey != HealerPriorityProcessor.PulseEffectKey ||
            ev.SourceActorKey != priest.Key) return;
        if (priest.Abilities.ContainsKey("inner-focus") &&
            !priest.ActiveAuras.Any(a=>a.Definition.Key=="priest-inner-focus" &&
                a.IsActiveAt(ctx.CurrentTimeSeconds)))
            executor.TryStartAbility(ctx,priest.Key,priest.Key,"inner-focus");
        if (priest.Abilities.ContainsKey("power-infusion") &&
            !priest.ActiveAuras.Any(a=>a.Definition.Key=="priest-power-infusion" &&
                a.IsActiveAt(ctx.CurrentTimeSeconds)))
            executor.TryStartAbility(ctx,priest.Key,priest.Key,"power-infusion");
    }
}
