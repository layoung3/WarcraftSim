using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Consumes ONLY the caster's Immolate after a landed Conflagrate. The engine's
/// aura removal also cancels future Immolate ticks; it is not merely a UI flag.
/// Shadow and Flame retention is currently 20% per rank (5/5 = guaranteed).
/// </summary>
public sealed class WarlockConflagrateConsumptionProcessor : ICombatEventProcessor
{
    private readonly AuraManager _auras;
    private readonly decimal _retentionChance;

    public WarlockConflagrateConsumptionProcessor(AuraManager auras, int shadowAndFlameRank)
    {
        _auras = auras ?? throw new ArgumentNullException(nameof(auras));
        if (shadowAndFlameRank is < 0 or > 5)
            throw new ArgumentOutOfRangeException(nameof(shadowAndFlameRank));
        _retentionChance = shadowAndFlameRank * .20m;
    }

    public void Process(SimulationContext context, CombatEvent combatEvent)
    {
        if (combatEvent.Type != CombatEventType.Damage ||
            !string.Equals(combatEvent.AbilityKey, "conflagrate", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(combatEvent.SourceActorKey, "warlock", StringComparison.OrdinalIgnoreCase) ||
            combatEvent.ResultKey is not ("hit" or "critical") ||
            string.IsNullOrWhiteSpace(combatEvent.TargetActorKey))
            return;

        var target = context.GetActor(combatEvent.TargetActorKey);
        if (target is null || _retentionChance >= 1m) return;

        // Rank 0 consumes without advancing RNG; 1-4 use the sim's seeded RNG.
        if (_retentionChance > 0m &&
            (decimal)context.Random.NextDouble() < _retentionChance)
            return;

        _auras.RemoveAuras(context, target, "immolate", "warlock",
            "consumed by Conflagrate");
    }
}
