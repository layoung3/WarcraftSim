using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>Shared October 16 Resourcefulness: critical abilities grant 30 seconds
/// of 50% additional Spirit regeneration during the mana five-second rule.</summary>
public sealed class HunterResourcefulnessProcessor(
    SimulationActorState hunter, AuraManager auras, int rank,
    decimal spiritRegenPerSecond) : ICombatEventProcessor
{
    public const string AuraKey = "hunter-resourcefulness";
    private const string TickKey = "hunter-resourcefulness-regen";
    private decimal _lastManaSpent = decimal.MinValue;
    private bool _tickPending;
    public int Procs { get; private set; }
    public decimal ManaRestored { get; private set; }

    public void Process(SimulationContext context, CombatEvent ev)
    {
        if (rank <= 0 || ev.SourceActorKey != hunter.Key) return;
        var now = context.CurrentTimeSeconds;
        if (ev.Type == CombatEventType.ResourceChanged &&
            ev.ResourceKey == "mana" && ev.Amount < 0m)
            _lastManaSpent = now;

        // Both melee and ranged abilities qualify; auto aliases, traps, pet
        // attacks and periodic damage are not owner abilities. Volley channel
        // pulses retain the existing Survival !IsPeriodic eligibility.
        if (ev.Type == CombatEventType.Damage && ev.TargetActorKey is not null &&
            ev.Amount > 0m && ev.IsCritical &&
            !ev.IsPeriodic &&
            ev.AbilityKey is not null &&
            hunter.Abilities.TryGetValue(ev.AbilityKey, out var ability) &&
            string.Equals(ability.Definition.ClassKey, "hunter", StringComparison.OrdinalIgnoreCase) &&
            !ability.Definition.IsPassive &&
            ev.AbilityKey is not (null or "melee-auto" or "offhand-auto" or
                "auto-shot" or "ranged-auto" or "explosive-trap" or
                "immolation-trap" or "frost-trap" or "freezing-trap" or
                "lacerating-strikes") &&
            (decimal)context.Random.NextDouble() * 100m < 50m * rank)
        {
            Procs++;
            auras.ApplyAura(context, hunter, new AuraDefinition {
                Key = AuraKey, Name = "Resourcefulness", DurationSeconds = 30m,
                StackingMode = AuraStackingMode.Refresh
            }, hunter.Key, "resourcefulness", "resourcefulness-buff");
            if (!_tickPending) ScheduleTick(context);
        }

        if (ev.Type != CombatEventType.RotationDecision || !ev.IsInternal ||
            ev.EffectKey != TickKey) return;
        _tickPending = false;
        if (!hunter.ActiveAuras.Any(a => a.Definition.Key == AuraKey &&
            a.SourceActorKey == hunter.Key && a.IsActiveAt(now))) return;
        if (now < _lastManaSpent + 5m &&
            hunter.Resources.TryGetValue("mana", out var mana))
        {
            mana.AdvanceTo(now);
            var before = mana.Current;
            mana.Gain(spiritRegenPerSecond * .5m);
            var actual = mana.Current - before;
            ManaRestored += actual;
            if (actual > 0m) context.EmitEvent(new CombatEvent {
                TimeSeconds = now, Type = CombatEventType.ResourceChanged,
                SourceActorKey = hunter.Key, TargetActorKey = hunter.Key,
                AbilityKey = "resourcefulness", ResourceKey = "mana",
                Amount = actual, ResourceTotalAfter = mana.Current,
                Description = "Resourcefulness restored Mana during casting."
            });
        }
        ScheduleTick(context);
    }

    private void ScheduleTick(SimulationContext context)
    {
        var next = context.CurrentTimeSeconds + 1m;
        if (next >= context.Options.DurationSeconds) return;
        _tickPending = true;
        context.ScheduleEvent(new CombatEvent {
            TimeSeconds = next, Type = CombatEventType.RotationDecision,
            SourceActorKey = hunter.Key, EffectKey = TickKey, IsInternal = true
        });
    }
}
