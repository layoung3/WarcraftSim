using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Current Forever Decimation ranks: 45/90% permanent Soul Fire cooldown reduction;
/// a landed direct Shadow Bolt/Searing Pain on an enemy already below 35% health
/// refreshes a 10-second source aura. During the window Soul Fire casts 20/40%
/// faster and does not consume a shard. Cast-start timing and the shard cost
/// stay reserved through completion, even if the aura expires mid-cast.
/// </summary>
public sealed class WarlockDecimationProcessor : ICombatEventProcessor, IAbilityTimingProvider
{
    private readonly SimulationActorState _owner;
    private readonly AuraManager _auras;
    private readonly int _rank;
    private Guid? _pendingSoulFire;
    private bool _pendingShardFree;
    private decimal _activeUntil;
    public int Triggers { get; private set; }
    public int ShardFreeCasts { get; private set; }
    public int Expirations { get; private set; }

    public WarlockDecimationProcessor(SimulationActorState owner, AuraManager auras, int rank)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _auras = auras ?? throw new ArgumentNullException(nameof(auras));
        if (rank is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(rank));
        _rank = rank;
    }

    private bool ActiveAt(decimal time) => _rank > 0 && time < _activeUntil;

    private void RefreshShardCost(decimal time)
    {
        if (!_owner.Abilities.TryGetValue("soul-fire", out var state)) return;
        var cost = state.Definition.ResourceCosts.FirstOrDefault(c => c.ResourceKey == "soul-shard");
        if (cost is null) return;
        // A cast already begun with a free shard retains that payment snapshot.
        if (_pendingSoulFire.HasValue) return;
        cost.Amount = ActiveAt(time) ? 0m : 1m;
    }

    public AbilityTimingSnapshot Resolve(SimulationContext context,
        SimulationActorState source, AbilityDefinition ability)
    {
        var castTime = ability.CastTimeSeconds;
        if (source.Key == _owner.Key && ability.Key == "soul-fire" &&
            ActiveAt(context.CurrentTimeSeconds))
            castTime *= 1m - .20m * _rank;
        return new AbilityTimingSnapshot {
            CastTimeSeconds = castTime,
            GlobalCooldownSeconds = ability.GlobalCooldownSeconds,
            ChannelDurationSeconds = ability.ChannelDurationSeconds,
            ChannelTickIntervalSeconds = ability.ChannelTickIntervalSeconds
        };
    }

    public void Process(SimulationContext context, CombatEvent ev)
    {
        if (_rank == 0 || ev.Type == CombatEventType.SimulationEnded) return;
        var now = context.CurrentTimeSeconds;
        if (ev.Type == CombatEventType.AuraRemoved &&
            ev.AbilityKey == "decimation" && ev.TargetActorKey == _owner.Key)
        {
            // A stale removal from an aura refreshed on the same timestamp must
            // not erase the newer window.
            if (!ActiveAt(now)) {
                Expirations++;
                RefreshShardCost(now);
            }
        }

        if (ev.SourceActorKey != _owner.Key) return;

        if (ev.Type == CombatEventType.Damage &&
            ev.AbilityKey is ("shadow-bolt" or "searing-pain") &&
            !ev.IsPeriodic && !ev.IsChannelTick &&
            ev.TargetActorKey is { } key &&
            ev.Amount is > 0m && ev.ResultKey is ("hit" or "critical"))
        {
            var target = context.GetActor(key);
            if (target is null || target.TeamKey == _owner.TeamKey ||
                target.MaximumHealth <= 0m) return;
            // Damage events are emitted after health is applied. Check the
            // health BEFORE this hit, not the health it just reduced.
            var priorHealth = Math.Min(target.MaximumHealth,
                target.CurrentHealth + ev.Amount.Value);
            if (priorHealth * 100m >= target.MaximumHealth * 35m) return;
            _activeUntil = now + 10m;
            Triggers++;
            _auras.ApplyAura(context, _owner, new AuraDefinition {
                Key = "warlock-decimation", Name = "Decimation",
                DurationSeconds = 10m
            }, _owner.Key, "decimation", "decimation-buff");
            RefreshShardCost(now);
            return;
        }

        if (ev.Type == CombatEventType.AbilityCastStarted &&
            ev.AbilityKey == "soul-fire" && ev.AbilityExecutionId is { } started)
        {
            _pendingSoulFire = started;
            _pendingShardFree = ActiveAt(now);
        }
        else if (ev.AbilityKey == "soul-fire" &&
            _pendingSoulFire.HasValue && ev.AbilityExecutionId == _pendingSoulFire &&
            ev.Type is (CombatEventType.AbilityCastCompleted or CombatEventType.AbilityCastCancelled))
        {
            if (ev.Type == CombatEventType.AbilityCastCompleted && _pendingShardFree)
                ShardFreeCasts++;
            _pendingSoulFire = null;
            _pendingShardFree = false;
            RefreshShardCost(now);
        }
    }
}
