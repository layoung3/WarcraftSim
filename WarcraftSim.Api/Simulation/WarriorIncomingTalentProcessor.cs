using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Api.Simulation;

/// <summary>Reaction talents using actual incoming boss damage events. The
/// current single-character DPS dummy does not attack, so these effects become
/// observable when the encounter/tank runner supplies incoming damage events.</summary>
public sealed class WarriorIncomingTalentProcessor : ICombatEventProcessor
{
    public const string EnrageBonusKey = "warrior-enrage-physical-bonus";
    private const string EnrageExpiration = "warrior-enrage-expire";
    private const string BloodCrazeTick = "warrior-blood-craze-tick";
    private readonly int _enrageRank, _bloodCrazeRank, _shieldSpecRank;
    private readonly bool _shieldEquipped;
    private decimal _enrageExpiresAt;
    private int _bloodCrazeGeneration;

    public WarriorIncomingTalentProcessor(WarriorSimRequest request)
    {
        _enrageRank = ForeverWarriorTalentCatalog.Rank(request, "enrage");
        _bloodCrazeRank = ForeverWarriorTalentCatalog.Rank(request, "blood-craze");
        _shieldSpecRank = ForeverWarriorTalentCatalog.Rank(request, "shield-specialization");
        _shieldEquipped = !request.IsTwoHanded && !request.DualWield &&
            request.Gear.Any(i => i.Slot == "off-hand" &&
                i.Name.Contains("shield", StringComparison.OrdinalIgnoreCase));
    }

    public void Process(SimulationContext context, CombatEvent ev)
    {
        var warrior = context.GetActor("warrior");
        if (warrior is null) return;
        if (ev.Type == CombatEventType.RotationDecision && ev.IsInternal)
        {
            if (ev.EffectKey == EnrageExpiration && context.CurrentTimeSeconds >= _enrageExpiresAt)
                SetEnrageBonus(warrior, 0m);
            if (ev.EffectKey == BloodCrazeTick &&
                ev.EncounterSequenceHitNumber == _bloodCrazeGeneration && warrior.IsAlive)
                Heal(context, warrior, warrior.MaximumHealth * _bloodCrazeRank / 600m, "blood-craze");
            return;
        }
        if (ev.Type != CombatEventType.Damage || ev.TargetActorKey != warrior.Key ||
            ev.SourceActorKey == warrior.Key) return;
        var amount = ev.Amount ?? 0m;
        if (_shieldEquipped && _shieldSpecRank > 0 &&
            (ev.ResultKey == "block" || (ev.BlockedAmount ?? 0m) > 0m) &&
            context.Random.NextDouble() < 0.2d * _shieldSpecRank &&
            warrior.Resources.TryGetValue("rage", out var rage))
        {
            var gained = rage.Gain(5m);
            if (gained > 0m)
                context.EmitEvent(new CombatEvent
                {
                    TimeSeconds = context.CurrentTimeSeconds, Type = CombatEventType.ResourceChanged,
                    SourceActorKey = warrior.Key, TargetActorKey = warrior.Key,
                    AbilityKey = "shield-specialization", Amount = gained
                });
        }
        if (amount <= 0m) return;
        if (_enrageRank > 0 && context.Random.NextDouble() < 0.30d)
        {
            _enrageExpiresAt = context.CurrentTimeSeconds + 12m;
            SetEnrageBonus(warrior, 2m * _enrageRank);
            context.ScheduleEvent(new CombatEvent
            {
                TimeSeconds = _enrageExpiresAt, Type = CombatEventType.RotationDecision,
                SourceActorKey = warrior.Key, TargetActorKey = ev.SourceActorKey,
                EffectKey = EnrageExpiration, IsInternal = true
            });
        }
        if (_bloodCrazeRank > 0 && (ev.IsCritical || amount > warrior.MaximumHealth * 0.20m))
        {
            var generation = ++_bloodCrazeGeneration;
            for (var i = 1; i <= 6; i++)
                context.ScheduleEvent(new CombatEvent
                {
                    TimeSeconds = context.CurrentTimeSeconds + i,
                    Type = CombatEventType.RotationDecision,
                    SourceActorKey = warrior.Key, TargetActorKey = warrior.Key,
                    EffectKey = BloodCrazeTick, EncounterSequenceHitNumber = generation,
                    IsInternal = true
                });
        }
    }

    private static void SetEnrageBonus(SimulationActorState actor, decimal amount)
    {
        var old = actor.Stats.Get(EnrageBonusKey);
        actor.Stats.Set(EnrageBonusKey, amount);
        actor.Stats.Set("physical-damage-bonus-percent",
            actor.Stats.Get("physical-damage-bonus-percent") - old + amount);
    }

    private static void Heal(SimulationContext context, SimulationActorState warrior,
        decimal amount, string abilityKey)
    {
        var gained = warrior.Heal(amount);
        if (gained <= 0m) return;
        context.EmitEvent(new CombatEvent
        {
            TimeSeconds = context.CurrentTimeSeconds, Type = CombatEventType.Healing,
            SourceActorKey = warrior.Key, TargetActorKey = warrior.Key,
            AbilityKey = abilityKey, Amount = gained
        });
    }
}
