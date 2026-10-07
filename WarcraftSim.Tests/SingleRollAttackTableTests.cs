using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests;

public sealed class SingleRollAttackTableTests
{
    [Fact]
    public void OrderedTable_ResolvesOutcomesInDeclaredOrder()
    {
        var entries =
            new[]
            {
                Entry(
                    10m,
                    CombatRollResult.Miss()
                ),

                Entry(
                    20m,
                    CombatRollResult.Dodge()
                ),

                Entry(
                    30m,
                    CombatRollResult.Parry()
                )
            };

        Assert.Equal(
            CombatResultTypes.Miss,
            OrderedCombatRollTable.Resolve(
                9.999m,
                entries
            ).ResultKey
        );

        Assert.Equal(
            CombatResultTypes.Dodge,
            OrderedCombatRollTable.Resolve(
                10m,
                entries
            ).ResultKey
        );

        Assert.Equal(
            CombatResultTypes.Parry,
            OrderedCombatRollTable.Resolve(
                59.999m,
                entries
            ).ResultKey
        );

        Assert.Equal(
            CombatResultTypes.Hit,
            OrderedCombatRollTable.Resolve(
                60m,
                entries
            ).ResultKey
        );
    }

    [Fact]
    public void OrderedTable_CriticalChanceOccupiesDirectTableSpace()
    {
        var entries =
            new[]
            {
                Entry(
                    20m,
                    CombatRollResult.Miss()
                ),

                Entry(
                    50m,
                    CombatRollResult.Critical(
                        2m
                    )
                )
            };

        Assert.Equal(
            CombatResultTypes.Miss,
            OrderedCombatRollTable.Resolve(
                19.999m,
                entries
            ).ResultKey
        );

        Assert.Equal(
            CombatResultTypes.Critical,
            OrderedCombatRollTable.Resolve(
                20m,
                entries
            ).ResultKey
        );

        Assert.Equal(
            CombatResultTypes.Critical,
            OrderedCombatRollTable.Resolve(
                69.999m,
                entries
            ).ResultKey
        );

        Assert.Equal(
            CombatResultTypes.Hit,
            OrderedCombatRollTable.Resolve(
                70m,
                entries
            ).ResultKey
        );
    }

    [Fact]
    public void OrderedTable_EarlierOutcomesCanPushLaterOutcomesOffTable()
    {
        var entries =
            new[]
            {
                Entry(
                    100m,
                    CombatRollResult.Block(
                        50m
                    )
                ),

                Entry(
                    100m,
                    CombatRollResult.Critical(
                        2m
                    )
                )
            };

        var result =
            OrderedCombatRollTable.Resolve(
                99.999m,
                entries
            );

        Assert.Equal(
            CombatResultTypes.Block,
            result.ResultKey
        );

        Assert.False(
            result.IsCritical
        );
    }

    [Fact]
    public void OrderedTable_RejectsRollOutsidePercentageRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                OrderedCombatRollTable.Resolve(
                    -0.001m,
                    []
                )
        );

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                OrderedCombatRollTable.Resolve(
                    100m,
                    []
                )
        );
    }

    [Fact]
    public void RulesetResolver_SingleRollTableKeepsBlockAheadOfCritical()
    {
        var source =
            CreateActor(
                "attacker",
                "raid"
            );

        var target =
            CreateActor(
                "target",
                "enemy"
            );

        var ruleset =
            CreateRuleset(
                useSingleRollTable:
                    true,
                blockChancePercent:
                    100m,
                criticalChancePercent:
                    100m
            );

        var result =
            new RulesetCombatRollResolver(
                ruleset
            )
            .Resolve(
                CreateContext(),
                source,
                target,
                CreateAbility(),
                CreateEffect(
                    canBeBlocked:
                        true,
                    canCrit:
                        true
                )
            );

        Assert.Equal(
            CombatResultTypes.Block,
            result.ResultKey
        );

        Assert.False(
            result.IsCritical
        );
    }

    [Fact]
    public void RulesetResolver_SingleRollTableStillAllowsCriticalWithoutEarlierOutcome()
    {
        var ruleset =
            CreateRuleset(
                useSingleRollTable:
                    true,
                criticalChancePercent:
                    100m
            );

        var result =
            new RulesetCombatRollResolver(
                ruleset
            )
            .Resolve(
                CreateContext(),
                CreateActor(
                    "attacker",
                    "raid"
                ),
                CreateActor(
                    "target",
                    "enemy"
                ),
                CreateAbility(),
                CreateEffect(
                    canCrit:
                        true
                )
            );

        Assert.True(
            result.IsCritical
        );

        Assert.Equal(
            CombatResultTypes.Critical,
            result.ResultKey
        );
    }

    [Fact]
    public void RulesetResolver_DefaultStagedModeRemainsAvailable()
    {
        var ruleset =
            CreateRuleset(
                useSingleRollTable:
                    false,
                criticalChancePercent:
                    100m
            );

        var result =
            new RulesetCombatRollResolver(
                ruleset
            )
            .Resolve(
                CreateContext(),
                CreateActor(
                    "attacker",
                    "raid"
                ),
                CreateActor(
                    "target",
                    "enemy"
                ),
                CreateAbility(),
                CreateEffect(
                    canCrit:
                        true
                )
            );

        Assert.True(
            result.IsCritical
        );

        Assert.Equal(
            CombatResultTypes.Critical,
            result.ResultKey
        );
    }

    [Fact]
    public void SimpleResolver_CanUseSameSingleRollTableFoundation()
    {
        var result =
            new SimpleCombatRollResolver(
                hitChancePercent:
                    100m,
                blockChancePercent:
                    100m,
                blockValue:
                    25m,
                criticalChancePercent:
                    100m,
                useSingleRollTable:
                    true
            )
            .Resolve(
                CreateContext(),
                CreateActor(
                    "attacker",
                    "raid"
                ),
                CreateActor(
                    "target",
                    "enemy"
                ),
                CreateAbility(),
                CreateEffect(
                    canBeBlocked:
                        true,
                    canCrit:
                        true
                )
            );

        Assert.Equal(
            CombatResultTypes.Block,
            result.ResultKey
        );

        Assert.Equal(
            25m,
            result.BlockValue
        );

        Assert.False(
            result.IsCritical
        );
    }

    private static CombatRollTableEntry Entry(
        decimal chancePercent,
        CombatRollResult result)
    {
        return new CombatRollTableEntry
        {
            ChancePercent =
                chancePercent,

            Result =
                result
        };
    }

    private static CombatRulesetDefinition CreateRuleset(
        bool useSingleRollTable,
        decimal blockChancePercent = 0m,
        decimal criticalChancePercent = 0m)
    {
        var ruleset =
            new CombatRulesetDefinition
            {
                RulesetKey =
                    "single-roll-table-tests",

                Version =
                    "1"
            };

        ruleset.RollRules[
            CombatResolutionTypes.Melee
        ] =
            new CombatRollRuleDefinition
            {
                ResolutionType =
                    CombatResolutionTypes.Melee,

                UseSingleRollTable =
                    useSingleRollTable,

                BaseHitChancePercent =
                    100m,

                BaseBlockChancePercent =
                    blockChancePercent,

                BaseBlockValue =
                    50m,

                BaseCriticalChancePercent =
                    criticalChancePercent,

                CriticalMultiplier =
                    2m
            };

        return ruleset;
    }

    private static AbilityDefinition CreateAbility()
    {
        return new AbilityDefinition
        {
            Key =
                "attack",

            Name =
                "Attack"
        };
    }

    private static AbilityEffectDefinition CreateEffect(
        bool canBeBlocked = false,
        bool canCrit = false)
    {
        return new AbilityEffectDefinition
        {
            Key =
                "attack-effect",

            EffectType =
                AbilityEffectTypes.DirectDamage,

            TargetType =
                AbilityTargetTypes.Enemy,

            ResolutionType =
                CombatResolutionTypes.Melee,

            CanMiss =
                false,

            CanBeDodged =
                false,

            CanBeParried =
                false,

            CanBeBlocked =
                canBeBlocked,

            CanCrit =
                canCrit
        };
    }

    private static SimulationContext CreateContext()
    {
        return new SimulationContext(
            new SimulationRunOptions
            {
                Seed =
                    12345,

                DurationSeconds =
                    1m,

                PrimaryActorKey =
                    "attacker"
            }
        );
    }

    private static SimulationActorState CreateActor(
        string key,
        string teamKey)
    {
        var actor =
            new SimulationActorState
            {
                Key =
                    key,

                Name =
                    key,

                TeamKey =
                    teamKey,

                Level =
                    60
            };

        actor.InitializeHealth(
            maximumHealth:
                5000m
        );

        return actor;
    }
}
