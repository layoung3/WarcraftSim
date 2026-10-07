using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Tests;

public sealed class ForeverDefenseFormulaTests
{
    [Fact]
    public void EqualLevelNaturalDefenseHasNoContextAdjustment()
    {
        var attacker =
            CreateActor(
                "attacker",
                level:
                    60
            );

        var defender =
            CreateActor(
                "defender",
                level:
                    60
            );

        defender.Stats.Set(
            "defense",
            300m
        );

        var adjustment =
            CreateDefenseProvider()
                .GetAdjustment(
                    CreateContext(),
                    attacker,
                    defender,
                    CreateAbility(),
                    CreateDefenseEffect(),
                    CreateRule()
                );

        Assert.Same(
            CombatRollContextAdjustment.None,
            CombatRollContextAdjustment.Combine(
                [
                    adjustment
                ]
            )
        );
    }

    [Fact]
    public void TwentyFiveBonusDefenseAddsOnePercentToDefensiveOutcomes()
    {
        var attacker =
            CreateActor(
                "attacker",
                level:
                    60
            );

        var defender =
            CreateActor(
                "defender",
                level:
                    60
            );

        defender.Stats.Set(
            "defense",
            325m
        );

        var adjustment =
            CreateAdjustment(
                attacker,
                defender
            );

        Assert.Equal(
            -1m,
            adjustment.HitChancePercentDelta
        );

        Assert.Equal(
            1m,
            adjustment.DodgeChancePercentDelta
        );

        Assert.Equal(
            1m,
            adjustment.ParryChancePercentDelta
        );

        Assert.Equal(
            1m,
            adjustment.BlockChancePercentDelta
        );

        Assert.Equal(
            -1m,
            adjustment.CriticalChancePercentDelta
        );
    }

    [Fact]
    public void PlusThreeAttackerReducesBaselineDefenseOutcomesByPointSixPercent()
    {
        var attacker =
            CreateActor(
                "boss",
                level:
                    63
            );

        var defender =
            CreateActor(
                "tank",
                level:
                    60
            );

        defender.Stats.Set(
            "defense",
            300m
        );

        var adjustment =
            CreateAdjustment(
                attacker,
                defender
            );

        Assert.Equal(
            0.60m,
            adjustment.HitChancePercentDelta
        );

        Assert.Equal(
            -0.60m,
            adjustment.DodgeChancePercentDelta
        );

        Assert.Equal(
            -0.60m,
            adjustment.ParryChancePercentDelta
        );

        Assert.Equal(
            -0.60m,
            adjustment.BlockChancePercentDelta
        );

        Assert.Equal(
            0.60m,
            adjustment.CriticalChancePercentDelta
        );
    }

    [Fact]
    public void FourHundredFortyDefenseCancelsFivePercentBossCriticalChance()
    {
        var attacker =
            CreateActor(
                "boss",
                level:
                    63
            );

        var defender =
            CreateActor(
                "tank",
                level:
                    60
            );

        defender.Stats.Set(
            "defense",
            440m
        );

        var adjustment =
            CreateAdjustment(
                attacker,
                defender
            );

        Assert.Equal(
            -5m,
            adjustment.CriticalChancePercentDelta
        );

        var effect =
            CreateDefenseEffect();

        effect.CanMiss =
            false;

        effect.CanBeDodged =
            false;

        effect.CanBeParried =
            false;

        effect.CanBeBlocked =
            false;

        effect.CanCrit =
            true;

        effect.CanCrush =
            false;

        var ruleset =
            CreateRuleset(
                baseCriticalChancePercent:
                    5m
            );

        var resolver =
            new RulesetCombatRollResolver(
                ruleset,
                CreateDefenseProvider()
            );

        var result =
            resolver.Resolve(
                CreateContext(),
                attacker,
                defender,
                CreateAbility(),
                effect
            );

        Assert.False(
            result.IsCritical
        );

        Assert.Equal(
            CombatResultTypes.Hit,
            result.ResultKey
        );
    }

    [Fact]
    public void DefenseDoesNotDirectlyOverrideCrushingChance()
    {
        var attacker =
            CreateActor(
                "boss",
                level:
                    63
            );

        var defender =
            CreateActor(
                "tank",
                level:
                    60
            );

        defender.Stats.Set(
            "defense",
            440m
        );

        var adjustment =
            CreateAdjustment(
                attacker,
                defender
            );

        Assert.Null(
            adjustment.CrushingChancePercentOverride
        );
    }

    [Fact]
    public void ClassicOneHundredTwoPointFourSheetCoveragePushesBossCrushOffTable()
    {
        var attacker =
            CreateActor(
                "boss",
                level:
                    63
            );

        var defender =
            CreateActor(
                "tank",
                level:
                    60
            );

        defender.Stats.Set(
            "defense",
            300m
        );

        var adjustment =
            CreateAdjustment(
                attacker,
                defender
            );

        // Character-sheet values versus an equal-level attacker:
        // 5.0 miss + 25.0 dodge + 25.0 parry + 47.4 block = 102.4.
        // A +3 attacker reduces each of the four outcomes by 0.6,
        // leaving exactly 100% actual table coverage.
        var miss =
            5m -
            adjustment.HitChancePercentDelta;

        var dodge =
            25m +
            adjustment.DodgeChancePercentDelta;

        var parry =
            25m +
            adjustment.ParryChancePercentDelta;

        var block =
            47.4m +
            adjustment.BlockChancePercentDelta;

        Assert.Equal(
            100m,
            miss +
            dodge +
            parry +
            block
        );

        var result =
            OrderedCombatRollTable.Resolve(
                99.999m,
                [
                    Entry(
                        miss,
                        CombatRollResult.Miss()
                    ),

                    Entry(
                        dodge,
                        CombatRollResult.Dodge()
                    ),

                    Entry(
                        parry,
                        CombatRollResult.Parry()
                    ),

                    Entry(
                        block,
                        CombatRollResult.Block(
                            1m
                        )
                    ),

                    Entry(
                        5m,
                        CombatRollResult.Critical(
                            2m
                        )
                    ),

                    Entry(
                        15m,
                        CombatRollResult.Crushing(
                            1.5m
                        )
                    )
                ]
            );

        Assert.Equal(
            CombatResultTypes.Block,
            result.ResultKey
        );

        Assert.False(
            result.IsCrushing
        );
    }

    [Fact]
    public void MissingConfiguredDefenseSkillFailsInsteadOfGuessing()
    {
        var exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    CreateDefenseProvider()
                        .GetAdjustment(
                            CreateContext(),
                            CreateActor(
                                "boss",
                                level:
                                    63
                            ),
                            CreateActor(
                                "tank",
                                level:
                                    60
                            ),
                            CreateAbility(),
                            CreateDefenseEffect(),
                            CreateRule()
                        )
            );

        Assert.Contains(
            "missing defense skill stat",
            exception.Message,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void EffectWithoutDefenseSkillKeyGetsNoDefenseAdjustment()
    {
        var adjustment =
            CreateDefenseProvider()
                .GetAdjustment(
                    CreateContext(),
                    CreateActor(
                        "boss",
                        level:
                            63
                    ),
                    CreateActor(
                        "tank",
                        level:
                            60
                    ),
                    CreateAbility(),
                    new AbilityEffectDefinition
                    {
                        Key =
                            "spell-effect"
                    },
                    CreateRule()
                );

        Assert.Same(
            CombatRollContextAdjustment.None,
            adjustment
        );
    }

    [Fact]
    public void CompositeProviderAddsIndependentAdjustmentsAndUsesLastOverride()
    {
        var provider =
            new CompositeCombatRollContextAdjustmentProvider(
                [
                    new FixedProvider(
                        new CombatRollContextAdjustment
                        {
                            HitChancePercentDelta =
                                1m,

                            CriticalChancePercentDelta =
                                2m,

                            GlancingChancePercentOverride =
                                20m
                        }
                    ),

                    new FixedProvider(
                        new CombatRollContextAdjustment
                        {
                            HitChancePercentDelta =
                                -0.5m,

                            BlockChancePercentDelta =
                                3m,

                            GlancingChancePercentOverride =
                                40m
                        }
                    )
                ]
            );

        var adjustment =
            provider.GetAdjustment(
                CreateContext(),
                CreateActor(
                    "attacker",
                    60
                ),
                CreateActor(
                    "target",
                    60
                ),
                CreateAbility(),
                new AbilityEffectDefinition
                {
                    Key =
                        "effect"
                },
                CreateRule()
            );

        Assert.Equal(
            0.5m,
            adjustment.HitChancePercentDelta
        );

        Assert.Equal(
            2m,
            adjustment.CriticalChancePercentDelta
        );

        Assert.Equal(
            3m,
            adjustment.BlockChancePercentDelta
        );

        Assert.Equal(
            40m,
            adjustment.GlancingChancePercentOverride
        );
    }

    private static CombatRollContextAdjustment CreateAdjustment(
        SimulationActorState attacker,
        SimulationActorState defender)
    {
        return CreateDefenseProvider()
            .GetAdjustment(
                CreateContext(),
                attacker,
                defender,
                CreateAbility(),
                CreateDefenseEffect(),
                CreateRule()
            );
    }

    private static ForeverDefenseCombatRollAdjustmentProvider
        CreateDefenseProvider()
    {
        return new ForeverDefenseCombatRollAdjustmentProvider();
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
        decimal baseCriticalChancePercent = 0m)
    {
        var ruleset =
            new CombatRulesetDefinition
            {
                RulesetKey =
                    "forever-defense-tests",

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
                    true,

                BaseHitChancePercent =
                    95m,

                BaseCriticalChancePercent =
                    baseCriticalChancePercent,

                CriticalMultiplier =
                    2m,

                BaseCrushingChancePercent =
                    15m,

                CrushingDamageMultiplier =
                    1.5m
            };

        return ruleset;
    }

    private static CombatRollRuleDefinition CreateRule()
    {
        return new CombatRollRuleDefinition
        {
            ResolutionType =
                CombatResolutionTypes.Melee,

            UseSingleRollTable =
                true,

            BaseHitChancePercent =
                95m
        };
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

    private static AbilityEffectDefinition CreateDefenseEffect()
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

            TargetDefenseSkillStatKey =
                "defense",

            CanMiss =
                true,

            CanBeDodged =
                true,

            CanBeParried =
                true,

            CanBeBlocked =
                true,

            CanCrit =
                true,

            CanCrush =
                true
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
        int level)
    {
        var actor =
            new SimulationActorState
            {
                Key =
                    key,

                Name =
                    key,

                TeamKey =
                    key == "boss" ||
                    key == "attacker"
                        ? "enemy"
                        : "raid",

                Level =
                    level
            };

        actor.InitializeHealth(
            maximumHealth:
                5000m
        );

        return actor;
    }

    private sealed class FixedProvider :
        ICombatRollContextAdjustmentProvider
    {
        private readonly CombatRollContextAdjustment
            _adjustment;

        public FixedProvider(
            CombatRollContextAdjustment adjustment)
        {
            _adjustment =
                adjustment;
        }

        public CombatRollContextAdjustment GetAdjustment(
            SimulationContext context,
            SimulationActorState source,
            SimulationActorState target,
            AbilityDefinition ability,
            AbilityEffectDefinition effect,
            CombatRollRuleDefinition rule)
        {
            return _adjustment;
        }
    }
}
