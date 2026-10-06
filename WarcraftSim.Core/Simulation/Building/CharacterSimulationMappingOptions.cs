using WarcraftSim.Core.Stats;

namespace WarcraftSim.Core.Simulation.Building;

public sealed class CharacterSimulationMappingOptions
{
    // Optional runtime key override. If omitted, the CharacterProfile Id
    // becomes the stable actor key.
    public string? ActorKey { get; set; }

    public string TeamKey { get; set; } = "raid";

    public SimulationType? AssignedRole { get; set; }

    // Health is runtime input for now. A later ruleset character calculator
    // will derive this from the character build/gear/talents/buffs.
    public decimal MaximumHealth { get; set; }

    public decimal? StartingHealth { get; set; }

    public decimal InitialActionDelaySeconds { get; set; }

    public decimal InputDelaySeconds { get; set; }

    // When supplied, this replaces CharacterProfile.BaseStats as the stat
    // source. This is the bridge for already-calculated final stats.
    public StatCollection? EffectiveStats { get; set; }
}
