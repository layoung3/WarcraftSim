using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rulesets;

namespace WarcraftSim.Core.Simulation.Engine;

public sealed class RulesetCombatRollResolver :
    ICombatRollResolver
{
    private readonly CombatRulesetDefinition _ruleset;

    private readonly ICombatRollContextAdjustmentProvider?
        _contextAdjustmentProvider;

    public RulesetCombatRollResolver(
        CombatRulesetDefinition ruleset,
        ICombatRollContextAdjustmentProvider? contextAdjustmentProvider = null)
    {
        _ruleset = ruleset;

        _contextAdjustmentProvider =
            contextAdjustmentProvider;
    }

    public CombatRollResult Resolve(
        SimulationContext context,
        SimulationActorState source,
        SimulationActorState target,
        AbilityDefinition ability,
        AbilityEffectDefinition effect)
    {
        return Resolve(
            context,
            source,
            target,
            ability,
            effect,
            CombatEffectDeliveryType.Direct
        );
    }

    public CombatRollResult Resolve(
        SimulationContext context,
        SimulationActorState source,
        SimulationActorState target,
        AbilityDefinition ability,
        AbilityEffectDefinition effect,
        CombatEffectDeliveryType deliveryType)
    {
        if (string.Equals(
                effect.ResolutionType,
                CombatResolutionTypes.AlwaysHits,
                StringComparison.OrdinalIgnoreCase))
        {
            if (
                effect.CanGlance ||
                effect.CanCrush)
            {
                throw new InvalidOperationException(
                    $"Effect '{effect.Key}' requires a single-roll attack table for glancing/crushing outcomes."
                );
            }

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

        var adjustment =
            _contextAdjustmentProvider?.GetAdjustment(
                context,
                source,
                target,
                ability,
                effect,
                rule,
                deliveryType
            ) ??
            CombatRollContextAdjustment.None;

        if (rule.UseSingleRollTable)
        {
            return ResolveSingleRollTable(
                context,
                source,
                target,
                effect,
                rule,
                adjustment
            );
        }

        if (
            effect.CanGlance ||
            effect.CanCrush)
        {
            throw new InvalidOperationException(
                $"Effect '{effect.Key}' enables glancing/crushing outcomes, but resolution type '{effect.ResolutionType}' is not configured for a single-roll attack table."
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
                        rule,
                        adjustment
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
                        rule,
                        adjustment
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
                        rule,
                        adjustment
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
                        rule,
                        adjustment
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
            rule,
            adjustment
        );
    }

    private static CombatRollResult ResolveSingleRollTable(
        SimulationContext context,
        SimulationActorState source,
        SimulationActorState target,
        AbilityEffectDefinition effect,
        CombatRollRuleDefinition rule,
        CombatRollContextAdjustment adjustment)
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
                            rule,
                            adjustment
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
                            rule,
                            adjustment
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
                            rule,
                            adjustment
                        ),

                    Result =
                        CombatRollResult.Parry()
                }
            );
        }

        if (effect.CanGlance)
        {
            entries.Add(
                new CombatRollTableEntry
                {
                    ChancePercent =
                        CalculateGlancingChancePercent(
                            source,
                            target,
                            rule,
                            adjustment
                        ),

                    // Multiplier is finalized only if this table entry wins,
                    // so a non-glancing roll does not consume extra RNG.
                    Result =
                        CombatRollResult.Glancing(
                            1m
                        )
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
                            rule,
                            adjustment
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
                            rule,
                            adjustment
                        ),

                    Result =
                        CombatRollResult.Critical(
                            rule.CriticalMultiplier
                        )
                }
            );
        }

        if (effect.CanCrush)
        {
            entries.Add(
                new CombatRollTableEntry
                {
                    ChancePercent =
                        CalculateCrushingChancePercent(
                            source,
                            target,
                            rule,
                            adjustment
                        ),

                    Result =
                        CombatRollResult.Crushing(
                            rule.CrushingDamageMultiplier
                        )
                }
            );
        }

        var result =
            OrderedCombatRollTable.Resolve(
                (decimal)context.Random.NextDouble() *
                100m,
                entries
            );

        if (result.IsGlancing)
        {
            return CombatRollResult.Glancing(
                CalculateGlancingDamageMultiplier(
                    context,
                    rule
                )
            );
        }

        return result;
    }

    private CombatRollResult ResolveCritical(
        SimulationContext context,
        SimulationActorState source,
        SimulationActorState target,
        AbilityEffectDefinition effect,
        CombatRollRuleDefinition? knownRule = null,
        CombatRollContextAdjustment? knownAdjustment = null)
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

        var adjustment =
            knownAdjustment ??
            CombatRollContextAdjustment.None;

        var critChance =
            CalculateCriticalChancePercent(
                source,
                target,
                rule,
                adjustment
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
        CombatRollRuleDefinition rule,
        CombatRollContextAdjustment adjustment)
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

        hitChance +=
            adjustment.HitChancePercentDelta;

        return Math.Clamp(
            hitChance,
            0m,
            100m
        );
    }

    private static decimal CalculateDodgeChancePercent(
        SimulationActorState source,
        SimulationActorState target,
        CombatRollRuleDefinition rule,
        CombatRollContextAdjustment adjustment)
    {
        return CalculateAvoidanceChancePercent(
            source,
            target,
            rule.BaseDodgeChancePercent,
            rule.TargetDodgeChanceStatKey,
            rule.SourceDodgeReductionStatKey,
            adjustment.DodgeChancePercentDelta
        );
    }

    private static decimal CalculateParryChancePercent(
        SimulationActorState source,
        SimulationActorState target,
        CombatRollRuleDefinition rule,
        CombatRollContextAdjustment adjustment)
    {
        return CalculateAvoidanceChancePercent(
            source,
            target,
            rule.BaseParryChancePercent,
            rule.TargetParryChanceStatKey,
            rule.SourceParryReductionStatKey,
            adjustment.ParryChancePercentDelta
        );
    }

    private static decimal CalculateAvoidanceChancePercent(
        SimulationActorState source,
        SimulationActorState target,
        decimal baseChancePercent,
        string? targetChanceStatKey,
        string? sourceReductionStatKey,
        decimal contextualDelta)
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

        chance +=
            contextualDelta;

        return Math.Clamp(
            chance,
            0m,
            100m
        );
    }

    private static decimal CalculateGlancingChancePercent(
        SimulationActorState source,
        SimulationActorState target,
        CombatRollRuleDefinition rule,
        CombatRollContextAdjustment adjustment)
    {
        if (adjustment.GlancingChancePercentOverride.HasValue)
        {
            return Math.Clamp(
                adjustment.GlancingChancePercentOverride.Value,
                0m,
                100m
            );
        }

        return CalculateSpecialOutcomeChancePercent(
            source,
            target,
            rule.BaseGlancingChancePercent,
            rule.GlancingChanceStatKey,
            rule.TargetGlancingSuppressionStatKey
        );
    }

    private static decimal CalculateCrushingChancePercent(
        SimulationActorState source,
        SimulationActorState target,
        CombatRollRuleDefinition rule,
        CombatRollContextAdjustment adjustment)
    {
        if (adjustment.CrushingChancePercentOverride.HasValue)
        {
            return Math.Clamp(
                adjustment.CrushingChancePercentOverride.Value,
                0m,
                100m
            );
        }

        if (!rule.BaseCrushingChancePercent.HasValue)
        {
            throw new InvalidOperationException(
                $"Ruleset crushing-blow chance is not configured for resolution type '{rule.ResolutionType}'."
            );
        }

        return CalculateSpecialOutcomeChancePercent(
            source,
            target,
            rule.BaseCrushingChancePercent.Value,
            rule.CrushingChanceStatKey,
            rule.TargetCrushingSuppressionStatKey
        );
    }

    private static decimal CalculateSpecialOutcomeChancePercent(
        SimulationActorState source,
        SimulationActorState target,
        decimal baseChancePercent,
        string? sourceChanceStatKey,
        string? targetSuppressionStatKey)
    {
        var chance =
            baseChancePercent;

        if (!string.IsNullOrWhiteSpace(
                sourceChanceStatKey))
        {
            chance +=
                source.Stats.Get(
                    sourceChanceStatKey
                );
        }

        if (!string.IsNullOrWhiteSpace(
                targetSuppressionStatKey))
        {
            chance -=
                target.Stats.Get(
                    targetSuppressionStatKey
                );
        }

        return Math.Clamp(
            chance,
            0m,
            100m
        );
    }

    private static decimal CalculateGlancingDamageMultiplier(
        SimulationContext context,
        CombatRollRuleDefinition rule)
    {
        if (
            !rule.MinimumGlancingDamageMultiplier.HasValue ||
            !rule.MaximumGlancingDamageMultiplier.HasValue)
        {
            throw new InvalidOperationException(
                $"Ruleset glancing damage multipliers are not configured for resolution type '{rule.ResolutionType}'."
            );
        }

        var minimum =
            Math.Max(
                0m,
                rule.MinimumGlancingDamageMultiplier.Value
            );

        var maximum =
            Math.Max(
                minimum,
                rule.MaximumGlancingDamageMultiplier.Value
            );

        if (maximum == minimum)
        {
            return minimum;
        }

        return
            minimum +
            (
                (decimal)context.Random.NextDouble() *
                (maximum - minimum)
            );
    }

    private static decimal CalculateBlockChancePercent(
        SimulationActorState source,
        SimulationActorState target,
        CombatRollRuleDefinition rule,
        CombatRollContextAdjustment adjustment)
    {
        return CalculateAvoidanceChancePercent(
            source,
            target,
            rule.BaseBlockChancePercent,
            rule.TargetBlockChanceStatKey,
            rule.SourceBlockReductionStatKey,
            adjustment.BlockChancePercentDelta
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
        CombatRollRuleDefinition rule,
        CombatRollContextAdjustment adjustment)
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

        critChance +=
            adjustment.CriticalChancePercentDelta;

        return Math.Clamp(
            critChance,
            0m,
            100m
        );
    }
}
