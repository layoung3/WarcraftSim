using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>Rolling raw-health-damage window, inspired by the windowed MSD
/// measure in SimulationCraft. Threshold is user-defined, not a healing guarantee.
/// Per-iteration state only; no timeline allocation in nonrepresentative runs.</summary>
public sealed class WarriorTankSpikeProcessor : ICombatEventProcessor
{
    private readonly decimal _window, _warningPercent;
    private readonly Queue<(decimal Time, decimal Damage)> _hits = new();
    private decimal _rolling, _max, _maxPercent;
    private int _warnings;
    public decimal MaximumDamage => _max;
    public decimal MaximumHealthPercent => _maxPercent;
    public int ThresholdCrossings => _warnings;

    public WarriorTankSpikeProcessor(decimal windowSeconds, decimal warningPercent)
    {
        _window = windowSeconds;
        _warningPercent = warningPercent;
    }

    public void Process(SimulationContext context, CombatEvent ev)
    {
        if (ev.Type != CombatEventType.Damage || ev.TargetActorKey != "warrior" ||
            ev.SourceActorKey != "target") return;
        var damage = Math.Max(0m, ev.Amount ?? 0m);
        var at = context.CurrentTimeSeconds;
        _hits.Enqueue((at, damage));
        _rolling += damage;
        while (_hits.Count > 0 && at - _hits.Peek().Time > _window)
            _rolling -= _hits.Dequeue().Damage;
        var health = context.GetActor("warrior")?.MaximumHealth ?? 1m;
        var percent = _rolling * 100m / Math.Max(1m, health);
        if (_rolling > _max) _max = _rolling;
        if (percent > _maxPercent) _maxPercent = percent;
        if (percent >= _warningPercent) _warnings++;
    }
}
