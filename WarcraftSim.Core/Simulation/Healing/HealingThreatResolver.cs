using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Core.Simulation.Healing;

/// <summary>
/// Class-independent healing threat contribution. Distributes a configurable
/// fraction of effective healing among living enemies. Forever's exact
/// healing-threat coefficient and encounter aggro rules require log validation.
/// </summary>
public sealed class HealingThreatResolver : IThreatGenerationResolver
{
    private readonly decimal _coefficient;
    public HealingThreatResolver(decimal coefficient = 0.5m)
    {
        if (coefficient < 0m) throw new ArgumentOutOfRangeException(nameof(coefficient));
        _coefficient = coefficient;
    }
    public IReadOnlyList<ThreatGenerationContribution> Resolve(SimulationContext context, CombatEvent e)
    {
        if (e.Type != CombatEventType.Healing || e.Amount.GetValueOrDefault() <= 0m ||
            string.IsNullOrWhiteSpace(e.SourceActorKey)) return [];
        var healer = context.GetActor(e.SourceActorKey);
        if (healer is null) return [];
        var enemies = context.Actors.Values.Where(a => a.IsAlive &&
            !string.Equals(a.TeamKey, healer.TeamKey, StringComparison.OrdinalIgnoreCase)).ToList();
        if (enemies.Count == 0) return [];
        var each = e.Amount!.Value * _coefficient / enemies.Count;
        return enemies.Select(enemy => new ThreatGenerationContribution
        {
            ThreatOwnerActorKey = enemy.Key, ThreatSourceActorKey = healer.Key, Amount = each
        }).ToList();
    }
}
