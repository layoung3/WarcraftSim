namespace WarcraftSim.Core.Encounters;

public sealed class EncounterProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "Generic Encounter";

    public string RulesetKey { get; set; } = "";

    public decimal DurationSeconds { get; set; } = 180m;

    public List<EncounterTarget> Targets { get; set; } = [];

    public List<TargetProximityLink> TargetProximityLinks { get; set; } = [];

    public List<EncounterPhaseDefinition> Phases { get; set; } = [];

    public string? Notes { get; set; }
}
