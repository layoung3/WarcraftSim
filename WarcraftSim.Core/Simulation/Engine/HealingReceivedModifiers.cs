namespace WarcraftSim.Core.Simulation.Engine;

/// <summary>Resolves active healing-received auras at the instant a heal lands.
/// Applies to both direct and periodic heals; callers record post-modifier raw
/// healing to avoid counting healing reduction as overhealing.</summary>
public static class HealingReceivedModifiers
{
    public static decimal Apply(SimulationActorState target, decimal timeSeconds, decimal amount)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (amount <= 0m) return 0m;
        foreach (var aura in target.ActiveAuras)
        {
            if (!aura.IsActiveAt(timeSeconds)) continue;
            var factor = aura.Definition.HealingReceivedMultiplier;
            if (factor is < 0m or > 1m)
                throw new InvalidOperationException("Healing-received aura multiplier must be in [0, 1].");
            amount *= factor;
        }
        return amount;
    }
}
