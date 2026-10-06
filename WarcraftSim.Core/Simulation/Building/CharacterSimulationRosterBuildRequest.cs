namespace WarcraftSim.Core.Simulation.Building;

public sealed class CharacterSimulationRosterBuildRequest
{
    public string Key { get; init; } = "raid";

    public string Name { get; init; } = "Raid Roster";

    public List<CharacterSimulationBuildRequest> Members { get; init; } =
        [];
}
