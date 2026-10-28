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
    // Optional external talent buff that increases the Spirit regeneration
    // rate itself, rather than the fraction retained while casting.
    private readonly Func<decimal>? _spiritRegenMultiplier;
    private decimal _lastSpendAt = decimal.MinValue;

    public FiveSecondManaRegenerationProcessor(string actorKey, decimal spiritRegenPerSecond,
        decimal mp5, decimal castingSpiritFraction,
        Func<decimal>? additionalCastingSpiritFraction = null,
        Func<decimal>? spiritRegenMultiplier = null)
    {
        if (string.IsNullOrWhiteSpace(actorKey) || spiritRegenPerSecond < 0m || mp5 < 0m ||
            castingSpiritFraction is < 0m or > 1m)
            throw new ArgumentException("Invalid five-second rule configuration.");
        _actorKey = actorKey;
        _spiritPerSecond = spiritRegenPerSecond;
        _mp5PerSecond = mp5 / 5m;
        _castingSpiritFraction = castingSpiritFraction;
        _additionalCastingSpiritFraction = additionalCastingSpiritFraction;
        _spiritRegenMultiplier=spiritRegenMultiplier;
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
        // Innervate affects spirit-based Mana regeneration, not flat MP5.
        // Its aura overrides the five-second rule and raises that portion by 400%.
        var innervate = context.GetActor(_actorKey)?.ActiveAuras.Any(a =>
            a.Definition.Key == "druid-innervate" &&
            a.IsActiveAt(context.CurrentTimeSeconds)) == true;
        mana.RegenerationPerSecond = _mp5PerSecond +
            _spiritPerSecond * (innervate ? 5m : fraction) *
            Math.Max(0m,_spiritRegenMultiplier?.Invoke() ?? 1m);
    }

    public void Process(SimulationContext context, CombatEvent e)
    {
        if (e.Type is CombatEventType.AuraApplied or CombatEventType.AuraRemoved &&
            string.Equals(e.TargetActorKey, _actorKey, StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(e.AbilityKey, "innervate", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(e.AbilityKey, "soul-harvest", StringComparison.OrdinalIgnoreCase)))
        {
            RefreshRate(context);
            return;
        }
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
