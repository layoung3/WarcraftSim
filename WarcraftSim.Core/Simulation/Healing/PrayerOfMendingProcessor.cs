using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Core.Simulation.Healing;

/// <summary>
/// Priest-specific Prayer of Mending proc. Uses the Core aura, healing event,
/// and actor-health pipeline. Proximity isn't yet available in the preview,
/// so only live friendly actors in the same team are considered for jumps.
/// One active chain per caster, with five total charges.
/// </summary>
public sealed class PrayerOfMendingProcessor : ICombatEventProcessor
{
    private sealed class Chain
    {
        public string TargetKey { get; set; } = "";
        public int Charges { get; set; } = 5;
    }
    private readonly string _priestKey;
    private readonly decimal _healAmount;
    private readonly AuraManager _auras;
    private readonly Dictionary<string, Chain> _chains = new(StringComparer.OrdinalIgnoreCase);

    public PrayerOfMendingProcessor(string priestKey, decimal healAmount, AuraManager auras)
    {
        if (string.IsNullOrWhiteSpace(priestKey) || healAmount <= 0m)
            throw new ArgumentException("Invalid Prayer of Mending configuration.");
        _priestKey = priestKey;
        _healAmount = healAmount;
        _auras = auras;
    }

    public void Process(SimulationContext context, CombatEvent e)
    {
        if (e.Type == CombatEventType.AuraApplied &&
            string.Equals(e.AbilityKey, "prayer-of-mending", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.SourceActorKey, _priestKey, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrEmpty(e.TargetActorKey))
        {
            // Replace the caster's previous chain; prevents double-proccing.
            foreach (var previous in _chains.Values)
            {
                var oldTarget = context.GetActor(previous.TargetKey);
                if (oldTarget is not null)
                    _auras.RemoveAuras(context, oldTarget, "prayer-of-mending", _priestKey,
                        "Prayer of Mending reassigned");
            }
            _chains.Clear();
            _chains[_priestKey] = new Chain { TargetKey = e.TargetActorKey };
            return;
        }

        if ((e.Type != CombatEventType.Damage && e.Type != CombatEventType.Healing) ||
            e.Amount.GetValueOrDefault() <= 0m || string.IsNullOrWhiteSpace(e.TargetActorKey)) return;
        if (e.Type == CombatEventType.Healing &&
            (e.IsPeriodic || e.IsChannelTick ||
             string.Equals(e.AbilityKey, "prayer-of-mending", StringComparison.OrdinalIgnoreCase))) return;
        if (!_chains.TryGetValue(_priestKey, out var chain) ||
            !string.Equals(chain.TargetKey, e.TargetActorKey, StringComparison.OrdinalIgnoreCase)) return;
        var target = context.GetActor(chain.TargetKey);
        var caster = context.GetActor(_priestKey);
        if (target is not { IsAlive: true } || caster is null) return;
        if (!target.ActiveAuras.Any(a => a.IsActiveAt(context.CurrentTimeSeconds) &&
            a.Definition.Key == "prayer-of-mending" && a.SourceActorKey == _priestKey))
        {
            _chains.Clear();
            return;
        }
        _auras.RemoveAuras(context, target, "prayer-of-mending", _priestKey,
            "Prayer of Mending activated");
        var applied = target.Heal(_healAmount);
        context.EmitEvent(new CombatEvent
        {
            TimeSeconds = context.CurrentTimeSeconds, Type = CombatEventType.Healing,
            SourceActorKey = _priestKey, TargetActorKey = target.Key,
            AbilityKey = "prayer-of-mending", EffectKey = "prayer-of-mending-proc",
            SchoolKey = "holy", ResultKey = "hit", RawAmount = _healAmount,
            Amount = applied, OverhealingAmount = _healAmount - applied,
            Description = $"Prayer of Mending healed {target.Name} for {applied:0.##}."
        });
        chain.Charges--;
        if (chain.Charges <= 0) { _chains.Clear(); return; }
        var next = context.Actors.Values
            .Where(a => a.IsAlive && a.Key != target.Key &&
                string.Equals(a.TeamKey, caster.TeamKey, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(a => a.MaximumHealth - a.CurrentHealth)
            .ThenBy(a => a.Key, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
        if (next is null) { _chains.Clear(); return; }
        chain.TargetKey = next.Key;
        _auras.ApplyAura(context, next, new AuraDefinition
        {
            Key = "prayer-of-mending", Name = "Prayer of Mending", DurationSeconds = 30m,
            StackingMode = AuraStackingMode.Refresh
        }, _priestKey, "prayer-of-mending-jump", "prayer-of-mending-aura");
    }
}
