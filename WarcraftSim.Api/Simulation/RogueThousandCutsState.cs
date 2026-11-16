using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>Historical/provisional shared ten-second lifetime and live flat Energy prices.
/// Trigger and consumption selection remain the Rogue observer's existing local policy.</summary>
internal sealed class RogueThousandCutsState(SimulationActorState owner, AuraManager auras)
{
    private readonly AuraDefinition _definition = new() { Key = "rogue-thousand-cuts", Name = "Thousand Cuts",
        DurationSeconds = 10m, StackingMode = AuraStackingMode.Stack, MaxStacks = 5 };
    private readonly Dictionary<AbilityDefinition, (AbilityResourceCost Component, decimal Base)> _costs = Bind(owner);
    private AuraInstance? _aura;
    public int Stacks => _aura?.Stacks ?? 0;

    private static Dictionary<AbilityDefinition, (AbilityResourceCost, decimal)> Bind(SimulationActorState actor)
    {
        var result = new Dictionary<AbilityDefinition, (AbilityResourceCost, decimal)>();
        foreach (var (key, baseline) in new[] { ("backstab", 60m), ("hemorrhage", 35m) })
        {
            if (!actor.Abilities.TryGetValue(key, out var state)) continue;
            // Bind the intended spellbook component once. Do not rewrite unrelated,
            // percentage, duplicate or replaced components merely because they use Energy.
            var matches = state.Definition.ResourceCosts.Where(c => c.ResourceKey == "energy" &&
                !c.IsPercentOfMaximum && c.Amount == baseline).ToArray();
            if (matches.Length == 1) result.Add(state.Definition, (matches[0], baseline));
        }
        return result;
    }

    private bool Bound(AbilityDefinition ability, out (AbilityResourceCost Component, decimal Base) cost)
    {
        if (_costs.TryGetValue(ability, out cost) && owner.Abilities.TryGetValue(ability.Key, out var current) &&
            ReferenceEquals(current.Definition, ability) && ability.ResourceCosts.Contains(cost.Component) &&
            cost.Component.ResourceKey == "energy" && !cost.Component.IsPercentOfMaximum) return true;
        return false;
    }

    public void Synchronize(SimulationContext context)
    {
        if (_aura is not null && (!_aura.IsActiveAt(context.CurrentTimeSeconds) || !owner.ActiveAuras.Contains(_aura)))
        {
            auras.RemoveAura(context, owner, _aura, "expired");
            _aura = null;
        }
        foreach (var ability in _costs.Keys)
            if (Bound(ability, out var cost)) cost.Component.Amount = Math.Max(0m, cost.Base - 3m * Stacks);
    }

    public void Gain(SimulationContext context)
    {
        Synchronize(context); // AuraManager's matching list includes expired objects.
        _aura = auras.ApplyAura(context, owner, _definition, owner.Key,
            "thousand-cuts", "thousand-cuts-benefit");
        Synchronize(context);
    }

    public void Consume(SimulationContext context)
    {
        if (_aura is not null) auras.RemoveAura(context, owner, _aura, "consumed");
        _aura = null;
        Synchronize(context);
    }

    public decimal? GetNextAffordableTime(SimulationContext context, SimulationActorState actor, AbilityDefinition ability)
    {
        if (!ReferenceEquals(actor, owner))
            return ResourceAvailabilityCalculator.GetNextAffordableTime(actor, ability, context.CurrentTimeSeconds);
        Synchronize(context);
        var ready = ResourceAvailabilityCalculator.GetNextAffordableTime(actor, ability, context.CurrentTimeSeconds);
        if (_aura is null || !ready.HasValue || ready.Value < _aura.ExpiresAtSeconds || !Bound(ability, out var cost))
            return ready;
        // Quote restored costs from NOW, not from the future candidate. Both calculator
        // overloads refresh resources; passing the candidate would advance live balances.
        var restored = ability.ResourceCosts.Select(c => new AbilityResourceCost {
            ResourceKey = c.ResourceKey, IsPercentOfMaximum = c.IsPercentOfMaximum,
            Amount = ReferenceEquals(c, cost.Component) ? cost.Base : c.Amount }).ToArray();
        return ResourceAvailabilityCalculator.GetNextAffordableTime(actor, restored, context.CurrentTimeSeconds);
    }
}
