namespace WarcraftSim.Core.Simulation.Engine;

public sealed class ForcedTargetState
{
    public Guid InstanceId { get; init; } =
        Guid.NewGuid();

    // Actor whose normal target selection is being overridden.
    public string TargetOwnerActorKey { get; init; } = "";

    // Actor that the owner is forced to target.
    public string ForcedTargetActorKey { get; init; } = "";

    // Actor responsible for applying the forced-target effect.
    public string SourceActorKey { get; init; } = "";

    public string? AbilityKey { get; init; }

    public string? EffectKey { get; init; }

    public decimal AppliedAtSeconds { get; init; }

    public decimal ExpiresAtSeconds { get; init; }

    public bool IsActiveAt(
        decimal currentTimeSeconds)
    {
        return
            currentTimeSeconds >=
                AppliedAtSeconds &&
            currentTimeSeconds <
                ExpiresAtSeconds;
    }
}
