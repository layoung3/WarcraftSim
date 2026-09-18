namespace WarcraftSim.Core.Simulation.Engine;

public sealed class AbilityExecutionState
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string SourceActorKey { get; set; } = "";

    public string TargetActorKey { get; set; } = "";

    public string AbilityKey { get; set; } = "";

    public Dictionary<string, CombatRollResult> EffectResults { get; } =
        new(StringComparer.OrdinalIgnoreCase);
}
