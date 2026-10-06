using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Core.Encounters;

public sealed class EncounterTargetSelectionDefinition
{
    public string Mode { get; set; } =
        EncounterTargetSelectionModes.FixedActor;

    // Used by fixed-actor targeting.
    public string? ActorKey { get; set; }

    // Optional team filter, for example "raid" or "enemy".
    public string? TeamKey { get; set; }

    // Empty means any assigned role is allowed.
    public List<SimulationType> AllowedRoles { get; set; } = [];

    // Applied after AllowedRoles. Useful for mechanics such as
    // "any raid member except tanks".
    public List<SimulationType> ExcludedRoles { get; set; } = [];

    // Source is excluded by default so a boss does not accidentally
    // select itself when team filters are omitted.
    public bool IncludeSourceActor { get; set; }

    // Used by random-matching-actors.
    public int Count { get; set; } = 1;

    // false = choose distinct actors when possible.
    // true = the same actor may be selected more than once during
    // one occurrence of the encounter pattern.
    public bool AllowDuplicateTargets { get; set; }
}
