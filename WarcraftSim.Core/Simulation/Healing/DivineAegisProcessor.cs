using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Core.Simulation.Healing;

/// <summary>Critical-heal absorb proc, reusable as an opt-in talent adapter.</summary>
public sealed class DivineAegisProcessor : ICombatEventProcessor
{
    private readonly string _healerKey;
    private readonly decimal _healingFraction;
    private readonly AbsorbManager _absorbs;
    public DivineAegisProcessor(string healerKey, decimal healingFraction, AbsorbManager absorbs)
    {
        if (string.IsNullOrWhiteSpace(healerKey) || healingFraction is < 0m or > 1m)
            throw new ArgumentException("Invalid Divine Aegis configuration.");
        _healerKey = healerKey;
        _healingFraction = healingFraction;
        _absorbs = absorbs;
    }
    public void Process(SimulationContext context, CombatEvent e)
    {
        // Forever's October 26 Discipline tooltip explicitly limits this
        // absorb to NON-PERIODIC critical heals. Channel ticks are distinct
        // from aura-driven periodic effects and remain eligible.
        if (e.Type != CombatEventType.Healing || !e.IsCritical ||
            e.EffectDeliveryType == CombatEffectDeliveryType.Periodic ||
            !string.Equals(e.SourceActorKey, _healerKey, StringComparison.OrdinalIgnoreCase) ||
            e.RawAmount.GetValueOrDefault() <= 0m ||
            string.IsNullOrWhiteSpace(e.TargetActorKey)) return;
        var target = context.GetActor(e.TargetActorKey);
        if (target is not { IsAlive: true }) return;
        _absorbs.ApplyAbsorb(context, target, "divine-aegis", "Divine Aegis",
            e.RawAmount!.Value * _healingFraction, 12m, AbsorbStackingMode.Refresh,
            1, _healerKey, "divine-aegis", "divine-aegis-absorb");
    }
}
