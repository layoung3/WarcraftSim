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
    private readonly decimal _blockChancePercent;
    private readonly decimal _blockValue;
    private readonly decimal _criticalChancePercent;
    private readonly decimal _criticalMultiplier;
    private readonly bool _useSingleRollTable;

    public SimpleCombatRollResolver(
        decimal hitChancePercent = 100m,
        decimal dodgeChancePercent = 0m,
        decimal parryChancePercent = 0m,
        decimal blockChancePercent = 0m,
        decimal blockValue = 0m,
        decimal criticalChancePercent = 0m,
        decimal criticalMultiplier = 2m,
        bool useSingleRollTable = false)
    {
        _hitChancePercent = Math.Clamp(hitChancePercent, 0m, 100m);
        _dodgeChancePercent = Math.Clamp(dodgeChancePercent, 0m, 100m);
        _parryChancePercent = Math.Clamp(parryChancePercent, 0m, 100m);
        _blockChancePercent = Math.Clamp(blockChancePercent, 0m, 100m);
        _blockValue = Math.Max(0m, blockValue);
        _criticalChancePercent = Math.Clamp(criticalChancePercent, 0m, 100m);
        _criticalMultiplier = Math.Max(0m, criticalMultiplier);
        _useSingleRollTable = useSingleRollTable;
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

        if (_useSingleRollTable)
        {
            return ResolveSingleRollTable(
                context,
                effect
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

            if (effect.CanBeBlocked)
            {
                cumulativeChance +=
                    _blockChancePercent;

                if (tableRoll <
                    Math.Min(
                        100m,
                        cumulativeChance
                    ))
                {
                    return CombatRollResult.Block(
                        _blockValue
                    );
                }
            }
        }

        return ResolveCritical(
            context,
            effect
        );
    }

    private CombatRollResult ResolveSingleRollTable(
        SimulationContext context,
        AbilityEffectDefinition effect)
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
                        _hitChancePercent,

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
                        _dodgeChancePercent,

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
                        _parryChancePercent,

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
                        _blockChancePercent,

                    Result =
                        CombatRollResult.Block(
                            _blockValue
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
                        _criticalChancePercent,

                    Result =
                        CombatRollResult.Critical(
                            _criticalMultiplier
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
