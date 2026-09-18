using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Core.Rotations;

public sealed class RotationTargetDefinition
{
    public string Mode { get; set; } =
        RotationTargetSelectionModes.Default;

    // Used by Fixed and FixedThenLowestHealthAlly.
    public string? ActorKey { get; set; }

    // Applies to ally-selection modes.
    public bool IncludeSelf { get; set; } = true;

    // Empty means any assigned role is allowed.
    public List<SimulationType> AllowedRoles { get; set; } = [];
}
