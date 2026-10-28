using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Tracks a source-owned Shadowburn finisher window for eligible target deaths.
/// Soul Shard refunds require an opt-in non-trivial target; training bosses
/// never produce free shards from damage alone.
/// </summary>
public sealed class WarlockShadowburnShardProcessor(bool eligibleTarget) : ICombatEventProcessor
{
    private readonly Dictionary<string, decimal> _expires = new(StringComparer.OrdinalIgnoreCase);
    public int ShardsRestored { get; private set; }

    public void Process(SimulationContext context, CombatEvent ev)
    {
        if (!eligibleTarget) return;
        if (ev.Type == CombatEventType.Damage && ev.AbilityKey == "shadowburn" &&
            ev.SourceActorKey == "warlock" && ev.TargetActorKey is { } key &&
            ev.ResultKey is ("hit" or "critical") && ev.Amount is > 0m)
            _expires[key] = context.CurrentTimeSeconds + 8m;

        if (ev.Type != CombatEventType.ActorDied || ev.TargetActorKey is not { } dead ||
            !_expires.Remove(dead, out var expiration) ||
            context.CurrentTimeSeconds > expiration) return;
        var owner = context.GetActor("warlock");
        if (owner is null || !owner.Resources.TryGetValue("soul-shard", out var shard)) return;
        shard.AdvanceTo(context.CurrentTimeSeconds);
        var gained = shard.Gain(1m);
        if (gained <= 0m) return;
        ShardsRestored++;
        context.EmitEvent(new CombatEvent {
            TimeSeconds = context.CurrentTimeSeconds,
            Type = CombatEventType.ResourceChanged,
            SourceActorKey = owner.Key, TargetActorKey = owner.Key,
            AbilityKey = "shadowburn", EffectKey = "shadowburn-kill-shard",
            ResourceKey = "soul-shard", ResourceTotalAfter = shard.Current,
            Amount = gained,
            Description = "Eligible Shadowburn kill refunded a Soul Shard."
        });
    }
}
