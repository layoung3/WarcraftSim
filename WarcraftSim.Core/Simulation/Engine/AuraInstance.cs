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

    // Optional snapshotted, *pre-mitigation* rolling periodic damage pool.
    // Null preserves ordinary periodic effects' live/stat-scaled behavior.
    public decimal? RollingDamageRemaining { get; set; }

    public int RollingTicksRemaining { get; set; }

    public bool IsActiveAt(decimal timeSeconds)
    {
        return timeSeconds >= AppliedAtSeconds &&
               timeSeconds < ExpiresAtSeconds;
    }

    public bool CanProcessPeriodicTickAt(
        decimal timeSeconds)
    {
        if (
            timeSeconds <
                AppliedAtSeconds ||
            timeSeconds >
                ExpiresAtSeconds)
        {
            return false;
        }

        if (timeSeconds < ExpiresAtSeconds)
        {
            return true;
        }

        return
            Definition.IsPeriodic &&
            Definition.IncludeExpirationBoundaryTick;
    }
}