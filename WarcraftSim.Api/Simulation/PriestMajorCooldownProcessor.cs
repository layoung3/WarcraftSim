using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Source-owned Inner Focus reservation and cost accounting for Priest casts.
/// A cancelled cast keeps the charged buff; a completed eligible cast consumes it.
/// The normal executor remains authoritative for spell costs and effects.
/// </summary>
public sealed class PriestMajorCooldownProcessor : ICombatEventProcessor
{
    private readonly SimulationActorState _priest;
    private readonly AuraManager _auras;
    private readonly Dictionary<string, decimal> _manaCosts;
    private Guid? _reserved;
    private string? _reservedKey;
    private bool _focused;
    public int InnerFocusConsumed { get; private set; }

    public PriestMajorCooldownProcessor(SimulationActorState priest, AuraManager auras)
    {
        _priest = priest ?? throw new ArgumentNullException(nameof(priest));
        _auras = auras ?? throw new ArgumentNullException(nameof(auras));
        _manaCosts = priest.Abilities.Values
            .Where(x => x.Definition.ResourceCosts.Any(c => c.ResourceKey == "mana"))
            .ToDictionary(x => x.Definition.Key,
                x => x.Definition.ResourceCosts.First(c => c.ResourceKey == "mana").Amount,
                StringComparer.OrdinalIgnoreCase);
    }

    public bool IsInnerFocusEmpowering(AbilityDefinition ability) =>
        _focused && _reservedKey == ability.Key && ability.Effects.Any(x =>
            x.EffectType is AbilityEffectTypes.DirectDamage or AbilityEffectTypes.DirectHealing);

    public bool PowerInfusionActive(SimulationContext ctx) =>
        _priest.ActiveAuras.Any(a => a.Definition.Key == "priest-power-infusion" &&
            a.IsActiveAt(ctx.CurrentTimeSeconds));

    private void SetCosts(bool focused)
    {
        foreach (var (key, mana) in _manaCosts)
        {
            if (_priest.Abilities.TryGetValue(key, out var ability))
            {
                var cost = ability.Definition.ResourceCosts
                    .FirstOrDefault(x => x.ResourceKey == "mana");
                if (cost is not null) cost.Amount = focused ? 0m : mana;
            }
        }
    }

    public void Process(SimulationContext ctx, CombatEvent ev)
    {
        if (ev.SourceActorKey != _priest.Key) return;
        if (ev.Type == CombatEventType.AuraApplied && ev.AbilityKey == "inner-focus")
        {
            _focused = true;
            SetCosts(true);
        }
        if (ev.Type == CombatEventType.AbilityCastStarted && _focused &&
            _reserved is null && ev.AbilityExecutionId is Guid id &&
            ev.AbilityKey is { } key && _manaCosts.ContainsKey(key))
        {
            _reserved = id;
            _reservedKey = key;
        }
        if (ev.Type == CombatEventType.AbilityCastCancelled &&
            ev.AbilityExecutionId == _reserved)
        {
            _reserved = null;
            _reservedKey = null;
        }
        if (ev.Type == CombatEventType.AbilityCastCompleted &&
            ev.AbilityExecutionId == _reserved)
        {
            _reserved = null;
            _reservedKey = null;
            _focused = false;
            InnerFocusConsumed++;
            // The executor has resolved and paid the completed cast before
            // this event reaches downstream processors.
            _auras.RemoveAuras(ctx, _priest, "priest-inner-focus", _priest.Key,
                "eligible spell completed");
            SetCosts(false);
        }
        if (ev.Type == CombatEventType.AuraRemoved && ev.AbilityKey == "inner-focus" &&
            _reserved is null)
        {
            _focused = false;
            SetCosts(false);
        }
    }
}
