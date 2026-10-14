using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Core.Simulation.Healing;

/// <summary>
/// Litany-style Mana refund when consecutive successfully completed healing
/// spells have different names. Refund is configurable until rank coefficients
/// are verified. An ability with multiple healing effects refunds only once.
/// </summary>
public sealed class AlternatingHealManaRefundProcessor : ICombatEventProcessor
{
    private readonly string _healerKey;
    private readonly decimal _refundFraction;
    private string? _lastHealingSpell;
    public AlternatingHealManaRefundProcessor(string healerKey, decimal refundFraction)
    {
        if (string.IsNullOrWhiteSpace(healerKey) || refundFraction is < 0m or > 1m)
            throw new ArgumentException("Invalid alternating-heal refund rule.");
        _healerKey = healerKey;
        _refundFraction = refundFraction;
    }

    public void Process(SimulationContext context, CombatEvent e)
    {
        if (e.Type != CombatEventType.ResourceChanged || e.Amount.GetValueOrDefault() >= 0m ||
            !string.Equals(e.SourceActorKey, _healerKey, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(e.AbilityKey)) return;
        var healer = context.GetActor(_healerKey);
        if (healer is null || !healer.Resources.TryGetValue("mana", out var mana) ||
            !healer.Abilities.TryGetValue(e.AbilityKey, out var ability)) return;
        var definition = ability.Definition;
        if (definition.Key != "prayer-of-mending" &&
            !definition.Effects.Any(effect =>
                effect.EffectType == "direct-healing" ||
                effect.EffectType == "periodic-healing")) return;
        var prior = _lastHealingSpell;
        _lastHealingSpell = definition.Key;
        if (prior is null || prior == definition.Key || _refundFraction <= 0m) return;
        var paidCost = -e.Amount!.Value;
        var refund = mana.Gain(paidCost * _refundFraction);
        if (refund > 0m)
            context.EmitEvent(new CombatEvent
            {
                TimeSeconds = context.CurrentTimeSeconds,
                Type = CombatEventType.ResourceChanged,
                SourceActorKey = _healerKey, TargetActorKey = _healerKey,
                AbilityKey = "litany-of-light", ResourceKey = "mana",
                Amount = refund, ResourceTotalAfter = mana.Current,
                Description = $"Litany of Light returned {refund:0.##} mana."
            });
    }
}
