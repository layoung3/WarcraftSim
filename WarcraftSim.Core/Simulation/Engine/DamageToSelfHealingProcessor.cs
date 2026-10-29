namespace WarcraftSim.Core.Simulation.Engine;

/// <summary>
/// Converts eligible, actually resolved enemy damage into caster healing.
/// Misses, absorbed-to-zero damage, friendly damage and other actors' procs
/// cannot grant phantom healing. No independent hit/critical roll is performed.
/// </summary>
public sealed class DamageToSelfHealingProcessor : ICombatEventProcessor
{
    private readonly string _owner;
    private readonly IReadOnlyDictionary<string, decimal> _transferRates;

    public DamageToSelfHealingProcessor(string owner,
        IReadOnlyDictionary<string, decimal> transferRates)
    {
        if (string.IsNullOrWhiteSpace(owner))
            throw new ArgumentException("Damage-transfer owner is required.", nameof(owner));
        ArgumentNullException.ThrowIfNull(transferRates);
        if (transferRates.Any(x => string.IsNullOrWhiteSpace(x.Key) ||
            x.Value < 0m || x.Value > 1m))
            throw new ArgumentException("Damage transfer fractions must be between zero and one.");
        _owner = owner;
        _transferRates = new Dictionary<string, decimal>(transferRates,
            StringComparer.OrdinalIgnoreCase);
    }

    public void Process(SimulationContext context, CombatEvent ev)
    {
        if (ev.Type != CombatEventType.Damage || ev.Amount is not > 0m ||
            !string.Equals(ev.SourceActorKey, _owner, StringComparison.OrdinalIgnoreCase) ||
            ev.AbilityKey is null || ev.TargetActorKey is null ||
            !_transferRates.TryGetValue(ev.AbilityKey, out var fraction) ||
            fraction <= 0m) return;

        var owner = context.GetActor(_owner);
        var victim = context.GetActor(ev.TargetActorKey);
        if (owner is not { IsAlive: true } || victim is null ||
            owner.TeamKey == victim.TeamKey) return;

        var raw = HealingReceivedModifiers.Apply(owner, context.CurrentTimeSeconds,
            ev.Amount.Value * fraction);
        var effective = owner.Heal(raw);
        context.EmitEvent(new CombatEvent
        {
            TimeSeconds = context.CurrentTimeSeconds,
            Type = CombatEventType.Healing,
            SourceActorKey = owner.Key, TargetActorKey = owner.Key,
            AbilityKey = ev.AbilityKey,
            AbilityExecutionId = ev.AbilityExecutionId,
            EffectKey = $"{ev.EffectKey}-life-transfer",
            SchoolKey = ev.SchoolKey,
            EffectDeliveryType = ev.EffectDeliveryType,
            RawAmount = raw, Amount = effective,
            OverhealingAmount = raw - effective,
            ResultKey = CombatResultTypes.Hit,
            Description = "Resolved damage transferred as health."
        });
    }
}
