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
        if (priest.Abilities.ContainsKey("power-infusion") &&
            !priest.ActiveAuras.Any(a=>a.Definition.Key=="priest-power-infusion" &&
                a.IsActiveAt(ctx.CurrentTimeSeconds)))
            executor.TryStartAbility(ctx,priest.Key,priest.Key,"power-infusion");
        // Apply the free-next-spell buff after the GCD power buff so it is
        // reserved by a healing cast rather than accidentally by Power Infusion.
        if (priest.Abilities.ContainsKey("inner-focus") &&
            !priest.ActiveAuras.Any(a=>a.Definition.Key=="priest-inner-focus" &&
                a.IsActiveAt(ctx.CurrentTimeSeconds)))
            executor.TryStartAbility(ctx,priest.Key,priest.Key,"inner-focus");
    }
}
