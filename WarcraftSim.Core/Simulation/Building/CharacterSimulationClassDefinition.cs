using WarcraftSim.Core.Abilities;

namespace WarcraftSim.Core.Simulation.Building;

public sealed class CharacterSimulationClassDefinition
{
    public string RulesetKey { get; set; } = "";

    public string ClassKey { get; set; } = "";

    // Null means the definition is class-wide and can be used by any
    // specialization of the class.
    public string? SpecializationKey { get; set; }

    public List<SimulationResourceBuildDefinition> Resources { get; set; } = [];

    public List<AbilityDefinition> Abilities { get; set; } = [];
}
