using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Core.Simulation.Healing;

/// <summary>
/// Class-neutral continuous-rate approximation of the five-second rule.
/// Spirit regen is suppressed for five seconds after a completed mana spell;
/// Mp5 is always available; talents can retain some Spirit regen while casting.
/// In the future, discrete 2-second mana ticks can replace this approximation.
/// </summary>
public sealed class FiveSecondManaRegenerationProcessor : ICombatEventProcessor
{
    public const string WakeKey = "five-second-mana-rule-expiry";
    private readonly string _actorKey;
    private readonly decimal _spiritPerSecond;
    private readonly decimal _mp5PerSecond;
    private readonly decimal _castingSpiritFraction;
    // Optional class-specific additive casting regen (e.g. Improved
    // Stormstrike). Null preserves every existing caster's five-second rule.
    private readonly Func<decimal>? _additionalCastingSpiritFraction;
    private decimal _lastSpendAt = decimal.MinValue;

    public FiveSecondManaRegenerationProcessor(string actorKey, decimal spiritRegenPerSecond,
        decimal mp5, decimal castingSpiritFraction,
        Func<decimal>? additionalCastingSpiritFraction = null)
    {
        if (string.IsNullOrWhiteSpace(actorKey) || spiritRegenPerSecond < 0m || mp5 < 0m ||
            castingSpiritFraction is < 0m or > 1m)
            throw new ArgumentException("Invalid five-second rule configuration.");
        _actorKey = actorKey;
        _spiritPerSecond = spiritRegenPerSecond;
        _mp5PerSecond = mp5 / 5m;
        _castingSpiritFraction = castingSpiritFraction;
        _additionalCastingSpiritFraction = additionalCastingSpiritFraction;
    }

    public void Initialize(SimulationContext context)
    {
        var mana = context.GetActor(_actorKey)?.Resources.GetValueOrDefault("mana");
        if (mana is not null) mana.RegenerationPerSecond = _mp5PerSecond + _spiritPerSecond;
    }

    public void RefreshRate(SimulationContext context)
    {
        var mana = context.GetActor(_actorKey)?.Resources.GetValueOrDefault("mana");
        if (mana is null) return;
        mana.AdvanceTo(context.CurrentTimeSeconds);
        var insideFiveSecondRule = context.CurrentTimeSeconds < _lastSpendAt + 5m;
        var fraction = insideFiveSecondRule
            ? Math.Clamp(_castingSpiritFraction +
                (_additionalCastingSpiritFraction?.Invoke() ?? 0m), 0m, 1m)
            : 1m;
        mana.RegenerationPerSecond = _mp5PerSecond + _spiritPerSecond * fraction;
    }

    public void Process(SimulationContext context, CombatEvent e)
    {
        if (!string.Equals(e.SourceActorKey, _actorKey, StringComparison.OrdinalIgnoreCase)) return;
        var mana = context.GetActor(_actorKey)?.Resources.GetValueOrDefault("mana");
        if (mana is null) return;

        if (e.Type == CombatEventType.ResourceChanged &&
            string.Equals(e.ResourceKey, "mana", StringComparison.OrdinalIgnoreCase) &&
            e.Amount < 0m)
        {
            // Refresh BEFORE changing the rate so the preceding interval is
            // accounted for at its old rate. Executor already paid the spell.
            mana.AdvanceTo(context.CurrentTimeSeconds);
            _lastSpendAt = context.CurrentTimeSeconds;
            RefreshRate(context);
            var wakeAt = context.CurrentTimeSeconds + 5m;
            if (wakeAt < context.Options.DurationSeconds)
                context.ScheduleEvent(new CombatEvent
                {
                    TimeSeconds = wakeAt, Type = CombatEventType.RotationDecision,
                    SourceActorKey = _actorKey, EffectKey = WakeKey, IsInternal = true
                });
        }
        else if (e.Type == CombatEventType.RotationDecision && e.IsInternal &&
            e.EffectKey == WakeKey && context.CurrentTimeSeconds >= _lastSpendAt + 5m)
        {
            RefreshRate(context);
        }
    }
}
