using WarcraftSim.Core.Abilities;

namespace WarcraftSim.Core.Simulation.Engine;

/// <summary>
/// Development-only configurable combat roll resolver.
/// Real rulesets will provide their own resolver using level, stats,
/// attack type, target defenses, and ruleset-specific combat tables.
/// </summary>
public sealed class SimpleCombatRollResolver : ICombatRollResolver
{
    private readonly decimal _hitChancePercent;
    private readonly decimal _criticalChancePercent;
    private readonly decimal _criticalMultiplier;

    public SimpleCombatRollResolver(
        decimal hitChancePercent = 100m,
        decimal criticalChancePercent = 0m,
        decimal criticalMultiplier = 2m)
    {
        _hitChancePercent = Math.Clamp(hitChancePercent, 0m, 100m);
        _criticalChancePercent = Math.Clamp(criticalChancePercent, 0m, 100m);
        _criticalMultiplier = Math.Max(0m, criticalMultiplier);
    }

    public CombatRollResult Resolve(
        SimulationContext context,
        SimulationActorState source,
        SimulationActorState target,
        AbilityDefinition ability,
        AbilityEffectDefinition effect)
    {
        if (
            string.Equals(
                effect.ResolutionType,
                CombatResolutionTypes.AlwaysHits,
                StringComparison.OrdinalIgnoreCase) ||
            !effect.CanMiss)
        {
            return ResolveCritical(context, effect);
        }

        var hitRoll = (decimal)context.Random.NextDouble() * 100m;

        if (hitRoll >= _hitChancePercent)
        {
            return CombatRollResult.Miss();
        }

        return ResolveCritical(context, effect);
    }

    private CombatRollResult ResolveCritical(
        SimulationContext context,
        AbilityEffectDefinition effect)
    {
        if (!effect.CanCrit)
        {
            return CombatRollResult.Hit();
        }

        var critRoll = (decimal)context.Random.NextDouble() * 100m;

        if (critRoll < _criticalChancePercent)
        {
            return CombatRollResult.Critical(_criticalMultiplier);
        }

        return CombatRollResult.Hit();
    }
}
