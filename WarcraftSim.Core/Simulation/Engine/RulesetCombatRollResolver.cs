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
        if (string.Equals(
                effect.ResolutionType,
                CombatResolutionTypes.AlwaysHits,
                StringComparison.OrdinalIgnoreCase))
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

        if (rule.UseSingleRollTable)
        {
            return ResolveSingleRollTable(
                context,
                source,
                target,
                effect,
                rule
            );
        }

        if (
            effect.CanMiss ||
            effect.CanBeDodged ||
            effect.CanBeParried ||
            effect.CanBeBlocked)
        {
            var tableRoll =
                (decimal)context.Random.NextDouble() *
                100m;

            var cumulativeChance =
                0m;

            if (effect.CanMiss)
            {
                var missChance =
                    100m -
                    CalculateHitChancePercent(
                        source,
                        target,
                        rule
                    );

                cumulativeChance +=
                    missChance;

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
                    CalculateDodgeChancePercent(
                        source,
                        target,
                        rule
                    );

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
                    CalculateParryChancePercent(
                        source,
                        target,
                        rule
                    );

                if (tableRoll <
                    Math.Min(
                        100m,
                        cumulativeChance
                    ))
                {
                    return CombatRollResult.Parry();
                }
            }

            if (effect.CanBeBlocked)
            {
                cumulativeChance +=
                    CalculateBlockChancePercent(
                        source,
                        target,
                        rule
                    );

                if (tableRoll <
                    Math.Min(
                        100m,
                        cumulativeChance
                    ))
                {
                    return CombatRollResult.Block(
                        CalculateBlockValue(
                            target,
                            rule
                        )
                    );
                }
            }
        }

        return ResolveCritical(
            context,
            source,
            target,
            effect,
            rule
        );
    }

    private static CombatRollResult ResolveSingleRollTable(
        SimulationContext context,
        SimulationActorState source,
        SimulationActorState target,
        AbilityEffectDefinition effect,
        CombatRollRuleDefinition rule)
    {
        var entries =
            new List<CombatRollTableEntry>();

        if (effect.CanMiss)
        {
            entries.Add(
                new CombatRollTableEntry
                {
                    ChancePercent =
                        100m -
                        CalculateHitChancePercent(
                            source,
                            target,
                            rule
                        ),

                    Result =
                        CombatRollResult.Miss()
                }
            );
        }

        if (effect.CanBeDodged)
        {
            entries.Add(
                new CombatRollTableEntry
                {
                    ChancePercent =
                        CalculateDodgeChancePercent(
                            source,
                            target,
                            rule
                        ),

                    Result =
                        CombatRollResult.Dodge()
                }
            );
        }

        if (effect.CanBeParried)
        {
            entries.Add(
                new CombatRollTableEntry
                {
                    ChancePercent =
                        CalculateParryChancePercent(
                            source,
                            target,
                            rule
                        ),

                    Result =
                        CombatRollResult.Parry()
                }
            );
        }

        if (effect.CanBeBlocked)
        {
            entries.Add(
                new CombatRollTableEntry
                {
                    ChancePercent =
                        CalculateBlockChancePercent(
                            source,
                            target,
                            rule
                        ),

                    Result =
                        CombatRollResult.Block(
                            CalculateBlockValue(
                                target,
                                rule
                            )
                        )
                }
            );
        }

        if (effect.CanCrit)
        {
            entries.Add(
                new CombatRollTableEntry
                {
                    ChancePercent =
                        CalculateCriticalChancePercent(
                            source,
                            target,
                            rule
                        ),

                    Result =
                        CombatRollResult.Critical(
                            rule.CriticalMultiplier
                        )
                }
            );
        }

        return OrderedCombatRollTable.Resolve(
            (decimal)context.Random.NextDouble() *
            100m,
            entries
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

    private static decimal CalculateDodgeChancePercent(
        SimulationActorState source,
        SimulationActorState target,
        CombatRollRuleDefinition rule)
    {
        return CalculateAvoidanceChancePercent(
            source,
            target,
            rule.BaseDodgeChancePercent,
            rule.TargetDodgeChanceStatKey,
            rule.SourceDodgeReductionStatKey
        );
    }

    private static decimal CalculateParryChancePercent(
        SimulationActorState source,
        SimulationActorState target,
        CombatRollRuleDefinition rule)
    {
        return CalculateAvoidanceChancePercent(
            source,
            target,
            rule.BaseParryChancePercent,
            rule.TargetParryChanceStatKey,
            rule.SourceParryReductionStatKey
        );
    }

    private static decimal CalculateAvoidanceChancePercent(
        SimulationActorState source,
        SimulationActorState target,
        decimal baseChancePercent,
        string? targetChanceStatKey,
        string? sourceReductionStatKey)
    {
        var chance =
            baseChancePercent;

        if (!string.IsNullOrWhiteSpace(
                targetChanceStatKey))
        {
            chance +=
                target.Stats.Get(
                    targetChanceStatKey
                );
        }

        if (!string.IsNullOrWhiteSpace(
                sourceReductionStatKey))
        {
            chance -=
                source.Stats.Get(
                    sourceReductionStatKey
                );
        }

        return Math.Clamp(
            chance,
            0m,
            100m
        );
    }

    private static decimal CalculateBlockChancePercent(
        SimulationActorState source,
        SimulationActorState target,
        CombatRollRuleDefinition rule)
    {
        return CalculateAvoidanceChancePercent(
            source,
            target,
            rule.BaseBlockChancePercent,
            rule.TargetBlockChanceStatKey,
            rule.SourceBlockReductionStatKey
        );
    }

    private static decimal CalculateBlockValue(
        SimulationActorState target,
        CombatRollRuleDefinition rule)
    {
        var blockValue =
            rule.BaseBlockValue;

        if (!string.IsNullOrWhiteSpace(
                rule.TargetBlockValueStatKey))
        {
            blockValue +=
                target.Stats.Get(
                    rule.TargetBlockValueStatKey
                );
        }

        return Math.Max(
            0m,
            blockValue
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
