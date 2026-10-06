using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Core.Simulation.Validation;

public interface ISimulationDefinitionValidationParticipant
{
    void CollectValidationErrors(
        SimulationContext context,
        ICollection<string> errors);
}
