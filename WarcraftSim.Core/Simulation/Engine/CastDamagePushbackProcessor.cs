using WarcraftSim.Core.Abilities;

namespace WarcraftSim.Core.Simulation.Engine;

/// <summary>
/// Pushback from actual damage taken during a hard cast. Resist chance is
/// configured by the caller (e.g. Nature's Focus: 14% per rank) and rolled
/// using the simulation seed. Damage events, not encounter AI, drive it.
/// </summary>
public sealed class CastDamagePushbackProcessor : ICombatEventProcessor
{
    private readonly AbilityExecutor _executor;
    private readonly string _casterActorKey;
    private readonly HashSet<string> _schoolKeys;
    private readonly decimal _resistChancePercent;
    private readonly decimal _secondsPerHit;
    private readonly decimal _maximumTotalSeconds;

    public int DamageEventsDuringCast { get; private set; }
    public int ResistedHits { get; private set; }
    public int PushedBackHits { get; private set; }
    public decimal TotalPushbackSeconds { get; private set; }

    public CastDamagePushbackProcessor(AbilityExecutor executor, string casterActorKey,
        decimal resistChancePercent, IEnumerable<string> eligibleSchools,
        decimal secondsPerHit = 0.5m, decimal maximumTotalSeconds = 1m)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        if (string.IsNullOrWhiteSpace(casterActorKey))
            throw new ArgumentException("Caster key required.", nameof(casterActorKey));
        if (resistChancePercent is < 0m or > 100m ||
            secondsPerHit < 0m || maximumTotalSeconds < 0m)
            throw new ArgumentOutOfRangeException(nameof(resistChancePercent));
        _casterActorKey = casterActorKey;
        _resistChancePercent = resistChancePercent;
        _secondsPerHit = secondsPerHit;
        _maximumTotalSeconds = maximumTotalSeconds;
        _schoolKeys = new HashSet<string>(eligibleSchools ??
            throw new ArgumentNullException(nameof(eligibleSchools)),
            StringComparer.OrdinalIgnoreCase);
    }

    public void Process(SimulationContext context, CombatEvent combatEvent)
    {
        if (combatEvent.Type != CombatEventType.Damage ||
            combatEvent.Amount.GetValueOrDefault() <= 0m ||
            !string.Equals(combatEvent.TargetActorKey, _casterActorKey,
                StringComparison.OrdinalIgnoreCase))
            return;
        var caster = context.GetActor(_casterActorKey);
        if (caster?.CurrentCastExecutionId is not Guid id ||
            !caster.IsCasting(context.CurrentTimeSeconds))
            return;
        var execution = context.GetAbilityExecution(id);
        if (execution is null || execution.IsCancelled ||
            execution.Timing.CastTimeSeconds <= 0m ||
            execution.CastCompletionDueAtSeconds <= context.CurrentTimeSeconds ||
            !caster.Abilities.TryGetValue(execution.AbilityKey, out var ability) ||
            !ability.Definition.Effects.Any(effect =>
                _schoolKeys.Contains(effect.SchoolKey ?? "")))
            return;
        DamageEventsDuringCast++;
        if ((decimal)context.Random.NextDouble() * 100m < _resistChancePercent)
        {
            ResistedHits++;
            return;
        }
        var delayed = _executor.TryPushbackCurrentCast(context, caster.Key,
            _secondsPerHit, _maximumTotalSeconds);
        if (delayed <= 0m) return;
        PushedBackHits++;
        TotalPushbackSeconds += delayed;
    }
}
