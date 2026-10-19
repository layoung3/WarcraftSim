using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Fixed-duration solo Bear incoming burst score. No fictitious character death
/// and no implicit healer HPS are counted. Threat factor is provisional.
/// </summary>
public sealed class FeralDruidTankMetricsProcessor : ICombatEventProcessor
{
    private readonly string _druid;
    private readonly string _boss;
    private readonly decimal _window;
    private readonly Func<bool> _inBear;
    private readonly decimal _threatMultiplier;
    private readonly Queue<(decimal At, decimal Damage)> _recent = new();
    private decimal _runningWindowDamage;
    public decimal TotalIncoming { get; private set; }
    public decimal MaximumBurst { get; private set; }
    public decimal Threat { get; private set; }

    public FeralDruidTankMetricsProcessor(string druid, string boss,
        decimal windowSeconds, Func<bool> inBear, decimal threatMultiplier)
    {
        if (windowSeconds <= 0m || threatMultiplier <= 0m)
            throw new ArgumentException("Invalid Bear threat or burst configuration.");
        _druid = druid; _boss = boss; _window = windowSeconds;
        _inBear = inBear; _threatMultiplier = threatMultiplier;
    }

    public void Process(SimulationContext ctx, CombatEvent ev)
    {
        if (ev.Type != CombatEventType.Damage || ev.Amount is not > 0m) return;
        if (ev.SourceActorKey == _druid && ev.TargetActorKey != _druid && _inBear())
        {
            var target = ctx.GetActor(ev.TargetActorKey!);
            if (target is not null)
            {
                var gained = ev.Amount.Value * _threatMultiplier;
                target.ThreatTable.AddThreat(_druid, gained);
                Threat += gained;
                ctx.EmitEvent(new CombatEvent {
                    TimeSeconds = ctx.CurrentTimeSeconds, Type = CombatEventType.ThreatChanged,
                    SourceActorKey = _druid, TargetActorKey = target.Key,
                    AbilityKey = ev.AbilityKey, EffectKey = ev.EffectKey, Amount = gained,
                    Description = "Feral Bear threat entered the shared enemy threat table."
                });
            }
        }
        if (ev.SourceActorKey != _boss || ev.TargetActorKey != _druid) return;
        var damage = ev.Amount.Value;
        TotalIncoming += damage;
        _recent.Enqueue((ctx.CurrentTimeSeconds, damage));
        _runningWindowDamage += damage;
        while (_recent.Count > 0 && _recent.Peek().At < ctx.CurrentTimeSeconds - _window)
            _runningWindowDamage -= _recent.Dequeue().Damage;
        MaximumBurst = Math.Max(MaximumBurst, _runningWindowDamage);
    }
}
