using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Core.Encounters;

public sealed class EncounterDamagePatternDefinition
{
    public string Key { get; set; } = "";

    public string Name { get; set; } = "Encounter Damage Pattern";

    public decimal StartTimeSeconds { get; set; }

    public decimal? EndTimeSeconds { get; set; }

    public decimal IntervalSeconds { get; set; }

    public string TargetActorKey { get; set; } = "";

    public EncounterTargetSelectionDefinition? TargetSelection { get; set; }

    public EncounterDamageSequenceDefinition? Sequence { get; set; }

    public string? SourceActorKey { get; set; }

    public decimal Amount { get; set; }

    public string? SchoolKey { get; set; }

    public string MitigationType { get; set; } =
        DamageMitigationTypes.None;

    public List<string> Tags { get; set; } = [];
}
