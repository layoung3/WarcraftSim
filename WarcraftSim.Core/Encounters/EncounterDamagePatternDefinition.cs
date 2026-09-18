using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Core.Encounters;

public sealed class EncounterDamagePatternDefinition
{
    public string Key { get; set; } = "";

    public string Name { get; set; } = "Encounter Damage Pattern";

    public decimal StartTimeSeconds { get; set; }

    // Null means repeat until the encounter ends.
    public decimal? EndTimeSeconds { get; set; }

    public decimal IntervalSeconds { get; set; }

    public string TargetActorKey { get; set; } = "";

    public string? SourceActorKey { get; set; }

    // Raw pre-mitigation damage.
    public decimal Amount { get; set; }

    public string? SchoolKey { get; set; }

    public string MitigationType { get; set; } =
        DamageMitigationTypes.None;

    public List<string> Tags { get; set; } = [];
}
