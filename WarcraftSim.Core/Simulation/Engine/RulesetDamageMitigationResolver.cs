using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rulesets;

namespace WarcraftSim.Core.Simulation.Engine;

public sealed class RulesetDamageMitigationResolver :
    IDamageMitigationResolver
{
    private readonly CombatRulesetDefinition _ruleset;

    public RulesetDamageMitigationResolver(
        CombatRulesetDefinition ruleset)
    {
        _ruleset = ruleset;
    }

    public DamageMitigationResult Resolve(
        SimulationContext context,
        SimulationActorState source,
        SimulationActorState target,
        AbilityDefinition ability,
        AbilityEffectDefinition effect,
        decimal rawAmount)
    {
        var safeRawAmount =
            Math.Max(
                0m,
                rawAmount
            );

        if (
            safeRawAmount <= 0m ||
            string.Equals(
                effect.MitigationType,
                DamageMitigationTypes.None,
                StringComparison.OrdinalIgnoreCase)
        )
        {
            return DamageMitigationResult.Unmitigated(
                safeRawAmount
            );
        }

        var rule =
            _ruleset.GetMitigationRule(
                effect.MitigationType
            );

        if (rule is null)
        {
            throw new InvalidOperationException(
                $"No mitigation rule exists for type '{effect.MitigationType}' " +
                $"in ruleset '{_ruleset.RulesetKey}'."
            );
        }

        var defenseStatKey =
            ResolveDefenseStatKey(
                rule,
                effect
            );

        var defenseValue =
            string.IsNullOrWhiteSpace(
                defenseStatKey)
                ? 0m
                : Math.Max(
                    0m,
                    target.Stats.Get(
                        defenseStatKey
                    )
                );

        var reductionPercent =
            CalculateReductionPercent(
                source,
                defenseValue,
                rule
            );

        var finalAmount =
            safeRawAmount *
            (
                1m -
                reductionPercent / 100m
            );

        return new DamageMitigationResult
        {
            RawAmount =
                safeRawAmount,

            FinalAmount =
                Math.Max(
                    0m,
                    finalAmount
                ),

            MitigatedAmount =
                Math.Max(
                    0m,
                    safeRawAmount -
                    finalAmount
                ),

            ReductionPercent =
                reductionPercent,

            DefenseValue =
                defenseValue
        };
    }

    private static string? ResolveDefenseStatKey(
        DamageMitigationRuleDefinition rule,
        AbilityEffectDefinition effect)
    {
        if (!string.IsNullOrWhiteSpace(
                rule.DefenseStatKey))
        {
            return rule.DefenseStatKey;
        }

        if (
            !string.IsNullOrWhiteSpace(
                rule.DefenseStatKeyFormat) &&
            !string.IsNullOrWhiteSpace(
                effect.SchoolKey)
        )
        {
            return rule.DefenseStatKeyFormat.Replace(
                "{school}",
                effect.SchoolKey,
                StringComparison.OrdinalIgnoreCase
            );
        }

        return null;
    }

    private static decimal CalculateReductionPercent(
        SimulationActorState source,
        decimal defenseValue,
        DamageMitigationRuleDefinition rule)
    {
        if (defenseValue <= 0m)
        {
            return 0m;
        }

        decimal reductionPercent;

        switch (rule.FormulaType)
        {
            case DamageMitigationFormulaTypes.RationalLevelScaled:
            {
                var denominator =
                    defenseValue +
                    rule.BaseConstant +
                    (
                        rule.PerAttackerLevelConstant *
                        Math.Max(
                            1,
                            source.Level
                        )
                    );

                reductionPercent =
                    denominator <= 0m
                        ? 0m
                        : defenseValue /
                          denominator *
                          100m;

                break;
            }

            case DamageMitigationFormulaTypes.ResistanceAverage:
            {
                var levelScale =
                    rule.DefensePerAttackerLevel *
                    Math.Max(
                        1,
                        source.Level
                    );

                var fractionOfCap =
                    levelScale <= 0m
                        ? 0m
                        : defenseValue /
                          levelScale;

                reductionPercent =
                    fractionOfCap *
                    rule.MaximumReductionPercent;

                break;
            }

            default:
                throw new InvalidOperationException(
                    $"Unknown mitigation formula '{rule.FormulaType}'."
                );
        }

        return Math.Clamp(
            reductionPercent,
            0m,
            Math.Max(
                0m,
                rule.MaximumReductionPercent
            )
        );
    }
}
