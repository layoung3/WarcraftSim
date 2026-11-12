using WarcraftSim.Core.Abilities;

namespace WarcraftSim.Core.Simulation.Engine;

/// <summary>Optional, run-local ability pricing. Synchronization is at current simulation
/// time only; forecasting must not advance live state to a predicted timestamp.</summary>
public interface IAbilityResourceCostPolicy
{
    void Synchronize(SimulationContext context, SimulationActorState actor, AbilityDefinition ability);
    decimal? GetNextAffordableTime(SimulationContext context, SimulationActorState actor, AbilityDefinition ability);
}
