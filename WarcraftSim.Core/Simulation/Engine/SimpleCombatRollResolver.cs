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
    private readonly decimal _dodgeChancePercent;
    private readonly decimal _parryChancePercent;
    private readonly decimal _criticalChancePercent;
    private readonly decimal _criticalMultiplier;

    public SimpleCombatRollResolver(
        decimal hitChancePercent = 100m,
        decimal dodgeChancePercent = 0m,
        decimal parryChancePercent = 0m,
        decimal criticalChancePercent = 0m,
        decimal criticalMultiplier = 2m)
    {
        _hitChancePercent = Math.Clamp(hitChancePercent, 0m, 100m);
        _dodgeChancePercent = Math.Clamp(dodgeChancePercent, 0m, 100m);
        _parryChancePercent = Math.Clamp(parryChancePercent, 0m, 100m);
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
        if (string.Equals(
                effect.ResolutionType,
                CombatResolutionTypes.AlwaysHits,
                StringComparison.OrdinalIgnoreCase))
        {
            return ResolveCritical(
                context,
                effect
            );
        }

        if (
            effect.CanMiss ||
            effect.CanBeDodged ||
            effect.CanBeParried)
        {
            var tableRoll =
                (decimal)context.Random.NextDouble() *
                100m;

            var cumulativeChance =
                0m;

            if (effect.CanMiss)
            {
                cumulativeChance +=
                    100m -
                    _hitChancePercent;

                if (tableRoll <
                    Math.Min(
                        100m,
                        cumulativeChance
                    ))
                {
                    return CombatRollResult.Miss();
                }
            }

            if (effect.CanBeDodged)
            {
                cumulativeChance +=
                    _dodgeChancePercent;

                if (tableRoll <
                    Math.Min(
                        100m,
                        cumulativeChance
                    ))
                {
                    return CombatRollResult.Dodge();
                }
            }

            if (effect.CanBeParried)
            {
                cumulativeChance +=
                    _parryChancePercent;

                if (tableRoll <
                    Math.Min(
                        100m,
                        cumulativeChance
                    ))
                {
                    return CombatRollResult.Parry();
                }
            }
        }

        return ResolveCritical(
            context,
            effect
        );
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
