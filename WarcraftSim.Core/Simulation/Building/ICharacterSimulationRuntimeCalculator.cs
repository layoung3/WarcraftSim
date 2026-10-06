using WarcraftSim.Core.Characters;

namespace WarcraftSim.Core.Simulation.Building;

public interface ICharacterSimulationRuntimeCalculator
{
    CharacterSimulationRuntimeCalculationResult Calculate(
        CharacterProfile profile);
}
