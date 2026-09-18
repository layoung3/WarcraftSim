namespace WarcraftSim.Core.Encounters;

public sealed class EncounterDamageEventDefinition
{
    public string Key { get; set; } = "";

    public string Name { get; set; } = "Encounter Damage";

    public decimal TimeSeconds { get; set; }

    public string TargetActorKey { get; set; } = "";

    public string? SourceActorKey { get; set; }

    // For this first scripted-damage checkpoint this is final damage
    // applied to health. Later this can be expanded to raw damage +
    // ruleset mitigation without changing the encounter timeline model.
    public decimal Amount { get; set; }

    public string? SchoolKey { get; set; }

    public List<string> Tags { get; set; } = [];
}
