using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rulesets;

namespace WarcraftSim.Core.Simulation.Engine;

public sealed class RulesetCombatRollResolver :
    ICombatRollResolver
{
    private readonly CombatRulesetDefinition _ruleset;

    public RulesetCombatRollResolver(
        CombatRulesetDefinition ruleset)
    {
        _ruleset = ruleset;
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
            return ResolveCritical(
                context,
                source,
                target,
                effect
            );
        }

        var rule =
            _ruleset.GetRollRule(
                effect.ResolutionType
            );

        if (rule is null)
        {
            throw new InvalidOperationException(
                $"No combat roll rule exists for resolution type '{effect.ResolutionType}' " +
                $"in ruleset '{_ruleset.RulesetKey}'."
            );
        }

        var hitChance =
            CalculateHitChancePercent(
                source,
                target,
                rule
            );

        var hitRoll =
            (decimal)context.Random.NextDouble() *
            100m;

        if (hitRoll >= hitChance)
        {
            return CombatRollResult.Miss();
        }

        return ResolveCritical(
            context,
            source,
            target,
            effect,
            rule
        );
    }

    private CombatRollResult ResolveCritical(
        SimulationContext context,
        SimulationActorState source,
        SimulationActorState target,
        AbilityEffectDefinition effect,
        CombatRollRuleDefinition? knownRule = null)
    {
        if (!effect.CanCrit)
        {
            return CombatRollResult.Hit();
        }

        var rule =
            knownRule ??
            _ruleset.GetRollRule(
                effect.ResolutionType
            );

        if (rule is null)
        {
            // An AlwaysHits effect that can crit may still need a rule
            // describing its crit chance and multiplier.
            if (string.Equals(
                    effect.ResolutionType,
                    CombatResolutionTypes.AlwaysHits,
                    StringComparison.OrdinalIgnoreCase))
            {
                return CombatRollResult.Hit();
            }

            throw new InvalidOperationException(
                $"No combat roll rule exists for resolution type '{effect.ResolutionType}' " +
                $"in ruleset '{_ruleset.RulesetKey}'."
            );
        }

        var critChance =
            CalculateCriticalChancePercent(
                source,
                target,
                rule
            );

        var critRoll =
            (decimal)context.Random.NextDouble() *
            100m;

        if (critRoll < critChance)
        {
            return CombatRollResult.Critical(
                rule.CriticalMultiplier
            );
        }

        return CombatRollResult.Hit();
    }

    private static decimal CalculateHitChancePercent(
        SimulationActorState source,
        SimulationActorState target,
        CombatRollRuleDefinition rule)
    {
        var hitChance =
            rule.BaseHitChancePercent;

        if (!string.IsNullOrWhiteSpace(
                rule.HitChanceStatKey))
        {
            hitChance +=
                source.Stats.Get(
                    rule.HitChanceStatKey
                );
        }

        if (!string.IsNullOrWhiteSpace(
                rule.TargetAvoidanceStatKey))
        {
            hitChance -=
                target.Stats.Get(
                    rule.TargetAvoidanceStatKey
                );
        }

        var higherTargetLevels =
            Math.Max(
                0,
                target.Level - source.Level
            );

        hitChance -=
            higherTargetLevels *
            rule.HitPenaltyPerHigherTargetLevelPercent;

        return Math.Clamp(
            hitChance,
            0m,
            100m
        );
    }

    private static decimal CalculateCriticalChancePercent(
        SimulationActorState source,
        SimulationActorState target,
        CombatRollRuleDefinition rule)
    {
        var critChance =
            rule.BaseCriticalChancePercent;

        if (!string.IsNullOrWhiteSpace(
                rule.CriticalChanceStatKey))
        {
            critChance +=
                source.Stats.Get(
                    rule.CriticalChanceStatKey
                );
        }

        if (!string.IsNullOrWhiteSpace(
                rule.TargetCriticalSuppressionStatKey))
        {
            critChance -=
                target.Stats.Get(
                    rule.TargetCriticalSuppressionStatKey
                );
        }

        return Math.Clamp(
            critChance,
            0m,
            100m
        );
    }
}
