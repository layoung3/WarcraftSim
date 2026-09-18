using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Core.Encounters;

public sealed class EncounterDamageEventDefinition
{
    public string Key { get; set; } = "";

    public string Name { get; set; } = "Encounter Damage";

    public decimal TimeSeconds { get; set; }

    public string TargetActorKey { get; set; } = "";

    public string? SourceActorKey { get; set; }

    // Raw pre-mitigation damage.
    public decimal Amount { get; set; }

    public string? SchoolKey { get; set; }

    public string MitigationType { get; set; } =
        DamageMitigationTypes.None;

    public List<string> Tags { get; set; } = [];
}
