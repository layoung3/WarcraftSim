using WarcraftSim.Core.Auras;

namespace WarcraftSim.Core.Simulation.Engine;

public sealed class AuraInstance
{
    public Guid InstanceId { get; set; } = Guid.NewGuid();

    public AuraDefinition Definition { get; set; } = new();

    public string SourceActorKey { get; set; } = "";

    public string TargetActorKey { get; set; } = "";

    public string? AbilityKey { get; set; }

    public string? EffectKey { get; set; }

    public decimal AppliedAtSeconds { get; set; }

    public decimal ExpiresAtSeconds { get; set; }

    public int Stacks { get; set; } = 1;

    public bool IsActiveAt(decimal timeSeconds)
    {
        return timeSeconds >= AppliedAtSeconds &&
               timeSeconds < ExpiresAtSeconds;
    }
}