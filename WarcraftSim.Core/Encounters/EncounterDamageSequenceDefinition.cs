namespace WarcraftSim.Core.Encounters;

public sealed class EncounterDamageSequenceDefinition
{
    public int HitCount { get; set; } = 1;

    public decimal HitIntervalSeconds { get; set; }

    public string TargetMode { get; set; } =
        EncounterDamageSequenceTargetModes.SameSelection;
}
