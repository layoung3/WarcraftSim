namespace WarcraftSim.Core.Simulation.Engine;

/// <summary>
/// Source-owned damage threat for solo DPS runners that do not have specialized
/// combat threat processors. Damage must be resolved and attributed before
/// threat is generated; absorbed damage and unsuccessful hits are not threat.
/// 
/// Classes retain control of their source-verified modifier and eligibility
/// rules through delegates. Do not install alongside another damage-threat
/// manager for the same source.
/// </summary>
public sealed class SoloDamageThreatResolver : IThreatGenerationResolver
{
    private readonly string _sourceActorKey;
    private readonly Func<CombatEvent, decimal> _multiplier;
    private readonly Func<CombatEvent, bool> _eligible;

    public SoloDamageThreatResolver(string sourceActorKey,
        Func<CombatEvent, decimal>? multiplier = null,
        Func<CombatEvent, bool>? eligible = null)
    {
        if (string.IsNullOrWhiteSpace(sourceActorKey))
            throw new ArgumentException("Threat source actor key is required.", nameof(sourceActorKey));
        _sourceActorKey = sourceActorKey;
        _multiplier = multiplier ?? (_ => 1m);
        _eligible = eligible ?? (_ => true);
    }

    public IReadOnlyList<ThreatGenerationContribution> Resolve(
        SimulationContext context, CombatEvent combatEvent)
    {
        if (combatEvent.Type != CombatEventType.Damage ||
            !string.Equals(combatEvent.SourceActorKey, _sourceActorKey,
                StringComparison.OrdinalIgnoreCase) ||
            combatEvent.TargetActorKey is null ||
            combatEvent.Amount is not > 0m ||
            !_eligible(combatEvent))
            return [];

        var source = context.GetActor(_sourceActorKey);
        var target = context.GetActor(combatEvent.TargetActorKey);
        if (source is null || target is null ||
            string.Equals(source.TeamKey, target.TeamKey, StringComparison.OrdinalIgnoreCase))
            return [];

        var multiplier = _multiplier(combatEvent);
        if (multiplier < 0m)
            throw new InvalidOperationException("Damage threat multiplier cannot be negative.");
        var amount = combatEvent.Amount.Value * multiplier;
        if (amount <= 0m) return [];

        return [new ThreatGenerationContribution
        {
            ThreatOwnerActorKey = target.Key,
            ThreatSourceActorKey = source.Key,
            Amount = amount
        }];
    }
}
