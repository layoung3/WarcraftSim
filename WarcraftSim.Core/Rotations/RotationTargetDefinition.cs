using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Core.Rotations;

public sealed class RotationTargetDefinition
{
    public string Mode { get; set; } =
        RotationTargetSelectionModes.Default;

    // Used by Fixed and FixedThenLowestHealthAlly.
    public string? ActorKey { get; set; }

    // Applies to semantic and ally-selection modes.
    public bool IncludeSelf { get; set; } = true;

    // Optional exact team filter.
    public string? TeamKey { get; set; }

    // Semantic relationship to the actor using the rotation.
    public string Relationship { get; set; } =
        SimulationActorRelationshipTypes.Any;

    // Empty means any assigned role is allowed.
    public List<SimulationType> AllowedRoles { get; set; } = [];

    // Applied after AllowedRoles.
    public List<SimulationType> ExcludedRoles { get; set; } = [];
}
