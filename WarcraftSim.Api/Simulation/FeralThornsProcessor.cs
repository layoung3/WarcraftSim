using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>Reactive Nature damage after a landed physical hit on the Druid.
/// No Thorns contribution is generated for an avoided incoming attack.</summary>
public sealed class FeralThornsProcessor(string druidKey, bool enabled, decimal damage)
    : ICombatEventProcessor
{
    public int Hits { get; private set; }
    public void Process(SimulationContext ctx, CombatEvent ev)
    {
        if (!enabled || damage <= 0m || ev.Type != CombatEventType.Damage ||
            ev.TargetActorKey != druidKey || string.IsNullOrWhiteSpace(ev.SourceActorKey) ||
            ev.SourceActorKey == druidKey ||
            ev.Amount is not > 0m || ev.SchoolKey != "physical") return;
        var druid = ctx.GetActor(druidKey);
        if (druid is null || !druid.ActiveAuras.Any(a =>
            a.Definition.Key == "druid-thorns" && a.IsActiveAt(ctx.CurrentTimeSeconds))) return;
        var enemy = ctx.GetActor(ev.SourceActorKey!);
        if (enemy is null || !enemy.IsAlive) return;
        var dealt = enemy.TakeDamage(damage);
        if (dealt <= 0m) return;
        Hits++;
        ctx.EmitEvent(new CombatEvent {
            Type = CombatEventType.Damage, TimeSeconds = ctx.CurrentTimeSeconds,
            SourceActorKey = druidKey, TargetActorKey = enemy.Key,
            AbilityKey = "thorns", EffectKey = "thorns-reactive-nature",
            SchoolKey = "nature", Amount = dealt, RawAmount = damage,
            Description = "Reactive Thorns damage after a landed physical attack."
        });
    }
}
