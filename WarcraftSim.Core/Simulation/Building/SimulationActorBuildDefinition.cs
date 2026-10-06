using WarcraftSim.Core.Abilities;

namespace WarcraftSim.Core.Simulation.Building;

public sealed class SimulationActorBuildDefinition
{
    public string Key { get; set; } = "";

    public string Name { get; set; } = "";

    public string TeamKey { get; set; } = "";

    public SimulationType? AssignedRole { get; set; }

    public int Level { get; set; } = 1;

    public decimal MaximumHealth { get; set; }

    public decimal? StartingHealth { get; set; }

    public decimal InitialActionDelaySeconds { get; set; }

    public decimal InputDelaySeconds { get; set; }

    public Dictionary<string, decimal> Stats { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public List<SimulationResourceBuildDefinition> Resources { get; set; } = [];

    public List<AbilityDefinition> Abilities { get; set; } = [];
}
