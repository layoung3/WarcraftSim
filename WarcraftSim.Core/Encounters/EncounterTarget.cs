using WarcraftSim.Core.Stats;

namespace WarcraftSim.Core.Encounters;

public sealed class EncounterTarget
{
    public string Key { get; set; } = "";

    public string Name { get; set; } = "Target";

    public int Level { get; set; } = 1;

    public decimal MaxHealth { get; set; }

    public int Priority { get; set; } = 1;

    public bool IsTankedBySimulatedCharacter { get; set; }

    public decimal SpawnTimeSeconds { get; set; }

    public decimal? DespawnTimeSeconds { get; set; }

    public StatCollection DefensiveStats { get; set; } = new();

    public List<string> Tags { get; set; } = [];
}