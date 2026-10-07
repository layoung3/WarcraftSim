using WarcraftSim.Core.Abilities;

namespace WarcraftSim.Core.Simulation.Engine;

public sealed class AbsorbInstance
{
    public Guid InstanceId { get; set; } =
        Guid.NewGuid();

    public string AbsorbKey { get; set; } = "";

    public string Name { get; set; } = "";

    public string SourceActorKey { get; set; } = "";

    public string TargetActorKey { get; set; } = "";

    public string? AbilityKey { get; set; }

    public string? EffectKey { get; set; }

    public decimal AppliedAtSeconds { get; set; }

    public decimal ExpiresAtSeconds { get; set; }

    public decimal MaximumAmount { get; set; }

    public decimal RemainingAmount { get; set; }

    public int Stacks { get; set; } = 1;

    public AbsorbStackingMode StackingMode { get; set; } =
        AbsorbStackingMode.Refresh;

    public int MaxStacks { get; set; } = 1;

    public bool IsActiveAt(
        decimal timeSeconds)
    {
        return
            timeSeconds >=
                AppliedAtSeconds &&
            timeSeconds <
                ExpiresAtSeconds &&
            RemainingAmount > 0m;
    }
}
