// Consolidated tests for Forever Stats Formulas.
// Original class names are retained for xUnit test filtering.

// ==================== ForeverDefenseFormulaTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;

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
    public void NaturalDefenseHasNoBonusAdjustmentEvenAgainstPlusThreeAttacker()
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
            0m,
            adjustment.HitChancePercentDelta
        );

        Assert.Equal(
            0m,
            adjustment.DodgeChancePercentDelta
        );

        Assert.Equal(
            0m,
            adjustment.ParryChancePercentDelta
        );

        Assert.Equal(
            0m,
            adjustment.BlockChancePercentDelta
        );

        Assert.Equal(
            0m,
            adjustment.CriticalChancePercentDelta
        );
    }

    [Fact]
    public void FourHundredFortyDefenseSuppliesFivePointSixPercentCritReduction()
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
            -5.60m,
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
                new ForeverCombatRollContextAdjustmentProvider()
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
            new ForeverCreatureLevelCombatRollAdjustmentProvider()
                .GetAdjustment(
                    CreateContext(),
                    attacker,
                    defender,
                    CreateAbility(),
                    CreateDefenseEffect(),
                    CreateRule()
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
                        100m,
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
            ForeverCombatResolutionTypes.CreatureMeleeAuto
        ] =
            new CombatRollRuleDefinition
            {
                ResolutionType =
                    ForeverCombatResolutionTypes.CreatureMeleeAuto,

                UseSingleRollTable =
                    true,

                BaseHitChancePercent =
                    95m,

                BaseCriticalChancePercent =
                    baseCriticalChancePercent,

                CriticalMultiplier =
                    2m,

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
                ForeverCombatResolutionTypes.CreatureMeleeAuto,

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
}

// ==================== ForeverMeleeAttackDefaultsTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;

public sealed class ForeverMeleeAttackDefaultsTests
{
    [Fact]
    public void SingleWeaponMeleeSpecialMatchesFivePercentEqualLevelHitCap()
    {
        var rule =
            GetRule(
                ForeverCombatResolutionTypes.PlayerMeleeSpecial
            );

        Assert.Equal(
            95m,
            rule.BaseHitChancePercent
        );

        Assert.Equal(
            0.8m,
            rule.HitPenaltyPerHigherTargetLevelPercent
        );

        Assert.Equal(
            ForeverCombatStatKeys.HitChancePercent,
            rule.HitChanceStatKey
        );

        var missChance =
            100m -
            rule.BaseHitChancePercent;

        Assert.Equal(
            5m,
            missChance
        );
    }

    [Fact]
    public void LevelSixtyMeleeSpecialAgainstRaidBossDerivesEightPercentHitCap()
    {
        var rule =
            GetRule(
                ForeverCombatResolutionTypes.PlayerMeleeSpecial
            );

        var source =
            CreateActor(
                "player",
                level:
                    60
            );

        var target =
            CreateActor(
                "boss",
                level:
                    63
            );

        source.Stats.Set(
            "weapon-skill",
            300m
        );

        var effect =
            new AbilityEffectDefinition
            {
                Key =
                    "special-effect",

                ResolutionType =
                    ForeverCombatResolutionTypes.PlayerMeleeSpecial,

                AttackSkillStatKey =
                    "weapon-skill"
            };

        var skillAdjustment =
            new ForeverWeaponSkillCombatRollAdjustmentProvider()
                .GetAdjustment(
                    CreateContext(),
                    source,
                    target,
                    CreateAbility(),
                    effect,
                    rule
                );

        var effectiveHit =
            rule.BaseHitChancePercent -
            (
                (target.Level - source.Level) *
                rule.HitPenaltyPerHigherTargetLevelPercent
            ) +
            skillAdjustment.HitChancePercentDelta;

        Assert.Equal(
            92m,
            effectiveHit
        );

        Assert.Equal(
            8m,
            100m -
            effectiveHit
        );
    }

    [Fact]
    public void DualWieldAutoMatchesTwentyFourAndTwentySevenPercentHitCaps()
    {
        var rule =
            GetRule(
                ForeverCombatResolutionTypes.PlayerDualWieldMeleeAuto
            );

        Assert.Equal(
            76m,
            rule.BaseHitChancePercent
        );

        Assert.Equal(
            24m,
            100m -
            rule.BaseHitChancePercent
        );

        var source =
            CreateActor(
                "player",
                level:
                    60
            );

        var target =
            CreateActor(
                "boss",
                level:
                    63
            );

        source.Stats.Set(
            "weapon-skill",
            300m
        );

        var effect =
            new AbilityEffectDefinition
            {
                Key =
                    "dual-auto",

                ResolutionType =
                    ForeverCombatResolutionTypes.PlayerDualWieldMeleeAuto,

                AttackSkillStatKey =
                    "weapon-skill"
            };

        var skillAdjustment =
            new ForeverWeaponSkillCombatRollAdjustmentProvider()
                .GetAdjustment(
                    CreateContext(),
                    source,
                    target,
                    CreateAbility(),
                    effect,
                    rule
                );

        var effectiveHit =
            rule.BaseHitChancePercent -
            (
                (target.Level - source.Level) *
                rule.HitPenaltyPerHigherTargetLevelPercent
            ) +
            skillAdjustment.HitChancePercentDelta;

        Assert.Equal(
            73m,
            effectiveHit
        );

        Assert.Equal(
            27m,
            100m -
            effectiveHit
        );

        Assert.Equal(
            19m,
            ForeverPhysicalCombatRulesetFactory
                .EqualLevelSingleWeaponBaseHitChancePercent -
            ForeverPhysicalCombatRulesetFactory
                .EqualLevelDualWieldBaseHitChancePercent
        );
    }

    [Fact]
    public void PlayerAutoAndSpecialProfilesUseDifferentClassicOutcomeShapes()
    {
        var auto =
            ForeverMeleeAttackProfiles.PlayerAuto;

        Assert.True(
            auto.CanGlance
        );

        Assert.False(
            auto.CanCrush
        );

        Assert.True(
            auto.CanBeDodged
        );

        Assert.True(
            auto.CanBeParried
        );

        Assert.True(
            auto.CanBeBlocked
        );

        var special =
            ForeverMeleeAttackProfiles.PlayerSpecial;

        Assert.False(
            special.CanGlance
        );

        Assert.False(
            special.CanCrush
        );

        Assert.True(
            special.CanBeDodged
        );

        Assert.True(
            special.CanBeParried
        );

        Assert.True(
            special.CanBeBlocked
        );
    }

    [Fact]
    public void CreatureAutoCanCritAndCrushButCannotGlance()
    {
        var profile =
            ForeverMeleeAttackProfiles.CreatureAuto;

        Assert.False(
            profile.CanGlance
        );

        Assert.True(
            profile.CanCrit
        );

        Assert.True(
            profile.CanCrush
        );

        Assert.True(
            profile.UsesTargetDefenseSkill
        );

        var rule =
            GetRule(
                profile.ResolutionType
            );

        Assert.Equal(
            5m,
            rule.BaseCriticalChancePercent
        );

        Assert.Null(
            rule.BaseCrushingChancePercent
        );

        Assert.Equal(
            1.5m,
            rule.CrushingDamageMultiplier
        );

        Assert.False(
            profile.CrushingChanceModelVerified
        );
    }

    [Fact]
    public void PlusThreeCreatureLevelAdvantageAddsPointSixToHitAndCrit()
    {
        var attacker =
            CreateActor(
                "boss",
                level:
                    63
            );

        var target =
            CreateActor(
                "tank",
                level:
                    60
            );

        var effect =
            new AbilityEffectDefinition
            {
                Key =
                    "boss-auto",

                ResolutionType =
                    ForeverCombatResolutionTypes.CreatureMeleeAuto
            };

        var adjustment =
            new ForeverCreatureLevelCombatRollAdjustmentProvider()
                .GetAdjustment(
                    CreateContext(),
                    attacker,
                    target,
                    CreateAbility(),
                    effect,
                    GetRule(
                        ForeverCombatResolutionTypes.CreatureMeleeAuto
                    )
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
    public void FourFortyDefenseAndPlusThreeCreatureProduceZeroCritChance()
    {
        var attacker =
            CreateActor(
                "boss",
                level:
                    63
            );

        var target =
            CreateActor(
                "tank",
                level:
                    60
            );

        target.Stats.Set(
            ForeverCombatStatKeys.DefenseSkill,
            440m
        );

        var effect =
            new AbilityEffectDefinition
            {
                Key =
                    "boss-auto",

                ResolutionType =
                    ForeverCombatResolutionTypes.CreatureMeleeAuto,

                TargetDefenseSkillStatKey =
                    ForeverCombatStatKeys.DefenseSkill
            };

        var rule =
            GetRule(
                ForeverCombatResolutionTypes.CreatureMeleeAuto
            );

        var adjustment =
            new ForeverCombatRollContextAdjustmentProvider()
                .GetAdjustment(
                    CreateContext(),
                    attacker,
                    target,
                    CreateAbility(),
                    effect,
                    rule
                );

        Assert.Equal(
            -5m,
            adjustment.CriticalChancePercentDelta
        );

        Assert.Equal(
            0m,
            Math.Max(
                0m,
                rule.BaseCriticalChancePercent +
                adjustment.CriticalChancePercentDelta
            )
        );
    }

    [Fact]
    public void ProfileApplicationConfiguresRuntimeEffectWithoutHardcodingWeaponType()
    {
        var effect =
            new AbilityEffectDefinition
            {
                Key =
                    "swing"
            };

        ForeverMeleeAttackProfiles.Apply(
            effect,
            ForeverMeleeAttackProfiles.PlayerDualWieldAuto,
            attackSkillStatKey:
                "one-handed-swords-skill"
        );

        Assert.Equal(
            ForeverCombatResolutionTypes.PlayerDualWieldMeleeAuto,
            effect.ResolutionType
        );

        Assert.Equal(
            "one-handed-swords-skill",
            effect.AttackSkillStatKey
        );

        Assert.True(
            effect.CanGlance
        );

        Assert.False(
            effect.CanCrush
        );

        var creatureEffect =
            new AbilityEffectDefinition
            {
                Key =
                    "boss-swing"
            };

        ForeverMeleeAttackProfiles.Apply(
            creatureEffect,
            ForeverMeleeAttackProfiles.CreatureAuto
        );

        Assert.Equal(
            ForeverCombatStatKeys.DefenseSkill,
            creatureEffect.TargetDefenseSkillStatKey
        );

        Assert.True(
            creatureEffect.CanCrush
        );
    }

    [Fact]
    public void PlayerProfileRequiresExplicitWeaponSkillStatKey()
    {
        var effect =
            new AbilityEffectDefinition
            {
                Key =
                    "swing"
            };

        Assert.Throws<ArgumentException>(
            () =>
                ForeverMeleeAttackProfiles.Apply(
                    effect,
                    ForeverMeleeAttackProfiles.PlayerAuto
                )
        );
    }

    [Fact]
    public void StandardForeverAutoRuleLeavesGlancingDamagePenaltyUnconfigured()
    {
        var rule =
            GetRule(
                ForeverCombatResolutionTypes.PlayerMeleeAuto
            );

        Assert.Null(
            rule.MinimumGlancingDamageMultiplier
        );

        Assert.Null(
            rule.MaximumGlancingDamageMultiplier
        );

        Assert.False(
            ForeverMeleeAttackProfiles
                .PlayerAuto
                .GlancingDamageModelVerified
        );
    }

    [Fact]
    public void RulesetResolverFailsLoudlyWhenUnverifiedGlancingDamageIsSelected()
    {
        var source =
            CreateActor(
                "player",
                level:
                    60
            );

        var target =
            CreateActor(
                "target",
                level:
                    60
            );

        source.Stats.Set(
            "weapon-skill",
            300m
        );

        var effect =
            new AbilityEffectDefinition
            {
                Key =
                    "auto-effect",

                EffectType =
                    AbilityEffectTypes.DirectDamage,

                TargetType =
                    AbilityTargetTypes.Enemy
            };

        ForeverMeleeAttackProfiles.Apply(
            effect,
            ForeverMeleeAttackProfiles.PlayerAuto,
            "weapon-skill"
        );

        effect.CanMiss =
            false;

        effect.CanBeDodged =
            false;

        effect.CanBeParried =
            false;

        effect.CanBeBlocked =
            false;

        effect.CanCrit =
            false;

        var resolver =
            new RulesetCombatRollResolver(
                ForeverPhysicalCombatRulesetFactory.Create(),
                new FixedAdjustmentProvider(
                    new CombatRollContextAdjustment
                    {
                        GlancingChancePercentOverride =
                            100m
                    }
                )
            );

        var exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    resolver.Resolve(
                        CreateContext(),
                        source,
                        target,
                        CreateAbility(),
                        effect
                    )
            );

        Assert.Contains(
            "glancing damage multipliers are not configured",
            exception.Message,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void RulesetResolverFailsLoudlyWhenUnverifiedCrushingChanceIsEnabled()
    {
        var source =
            CreateActor(
                "boss",
                level:
                    63
            );

        var target =
            CreateActor(
                "tank",
                level:
                    60
            );

        target.Stats.Set(
            ForeverCombatStatKeys.DefenseSkill,
            300m
        );

        var effect =
            new AbilityEffectDefinition
            {
                Key =
                    "boss-auto",

                EffectType =
                    AbilityEffectTypes.DirectDamage,

                TargetType =
                    AbilityTargetTypes.Enemy
            };

        ForeverMeleeAttackProfiles.Apply(
            effect,
            ForeverMeleeAttackProfiles.CreatureAuto
        );

        effect.CanMiss =
            false;

        effect.CanBeDodged =
            false;

        effect.CanBeParried =
            false;

        effect.CanBeBlocked =
            false;

        effect.CanCrit =
            false;

        var resolver =
            new RulesetCombatRollResolver(
                ForeverPhysicalCombatRulesetFactory.Create(),
                new ForeverCombatRollContextAdjustmentProvider()
            );

        var exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    resolver.Resolve(
                        CreateContext(),
                        source,
                        target,
                        CreateAbility(),
                        effect
                    )
            );

        Assert.Contains(
            "crushing-blow chance is not configured",
            exception.Message,
            StringComparison.OrdinalIgnoreCase
        );
    }

    private static CombatRollRuleDefinition GetRule(
        string resolutionType)
    {
        return ForeverPhysicalCombatRulesetFactory
            .Create()
            .GetRollRule(
                resolutionType
            )!;
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
                    "player"
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
                    key == "player" ||
                    key == "tank"
                        ? "raid"
                        : "enemy",

                Level =
                    level
            };

        actor.InitializeHealth(
            maximumHealth:
                5000m
        );

        return actor;
    }

    private sealed class FixedAdjustmentProvider :
        ICombatRollContextAdjustmentProvider
    {
        private readonly CombatRollContextAdjustment
            _adjustment;

        public FixedAdjustmentProvider(
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
}

// ==================== ForeverPhysicalStatConversionTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Characters.Buffs;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Core.Characters.Talents;
using WarcraftSim.Core.Simulation.Building;
using WarcraftSim.Data.Forever.Characters;
using WarcraftSim.Data.Forever.Combat;
using WarcraftSim.Data.Forever.Simulation;

public sealed class ForeverPhysicalStatConversionTests
{
    [Fact]
    public void VerifiedRatingConstantsMatchCurrentForeverClientValues()
    {
        Assert.Equal(
            10m,
            ForeverPhysicalStatConversions.HitRatingPerPercent
        );

        Assert.Equal(
            14m,
            ForeverPhysicalStatConversions.CriticalStrikeRatingPerPercent
        );

        Assert.Equal(
            12m,
            ForeverPhysicalStatConversions.DodgeRatingPerPercent
        );

        Assert.Equal(
            15m,
            ForeverPhysicalStatConversions.ParryRatingPerPercent
        );

        Assert.Equal(
            5m,
            ForeverPhysicalStatConversions.BlockRatingPerPercent
        );

        Assert.Equal(
            20m,
            ForeverPhysicalStatConversions.StrengthPerBlockValue
        );
    }

    [Fact]
    public void ContributorConvertsAllPhysicalRatingsToPercentStats()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Warrior
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.HitRating,
            20m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.CriticalStrikeRating,
            28m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.DodgeRating,
            24m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.ParryRating,
            30m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.BlockRating,
            10m
        );

        var result =
            CreateCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            2m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.HitChancePercent
            )
        );

        Assert.Equal(
            2m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.AttackCriticalChancePercent
            )
        );

        Assert.Equal(
            2m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.DodgeChancePercent
            )
        );

        Assert.Equal(
            2m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.ParryChancePercent
            )
        );

        Assert.Equal(
            2m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.BlockChancePercent
            )
        );
    }

    [Fact]
    public void ContributorPreservesRawRatingsForBreakdownAndFutureUse()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Rogue
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.HitRating,
            25m
        );

        var result =
            CreateCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            25m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.HitRating
            )
        );

        Assert.Equal(
            2.5m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.HitChancePercent
            )
        );
    }

    [Fact]
    public void ContributorAddsRatingConversionsToExistingDirectPercentBonuses()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Rogue
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.HitChancePercent,
            1.5m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.HitRating,
            20m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.AttackCriticalChancePercent,
            3m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.CriticalStrikeRating,
            14m
        );

        var result =
            CreateCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            3.5m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.HitChancePercent
            )
        );

        Assert.Equal(
            4m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.AttackCriticalChancePercent
            )
        );
    }

    [Theory]
    [InlineData(ForeverCharacterClassKeys.Warrior)]
    [InlineData(ForeverCharacterClassKeys.Paladin)]
    [InlineData(ForeverCharacterClassKeys.Shaman)]
    public void ShieldClassesGainBlockValueFromStrength(
        string classKey)
    {
        var profile =
            CreateProfile(
                classKey
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Strength,
            60m
        );

        var result =
            CreateCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            3m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.BlockValue
            )
        );
    }

    [Fact]
    public void NonShieldClassDoesNotGainBlockValueFromStrength()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Rogue
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Strength,
            60m
        );

        var result =
            CreateCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            0m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.BlockValue
            )
        );
    }

    [Fact]
    public void StrengthBlockValueAddsToExistingShieldOrGearBlockValue()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Paladin
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Strength,
            100m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.BlockValue,
            42m
        );

        var result =
            CreateCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            47m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.BlockValue
            )
        );
    }

    [Fact]
    public void DerivedConversionsRunAfterEquipmentTalentsAndStaticBuffs()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Warrior
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Strength,
            20m
        );

        profile.Equipment.Items.Add(
            new CharacterEquipmentItem
            {
                SlotKey =
                    "head",

                ItemKey =
                    "rating-helm",

                Name =
                    "Rating Helm",

                Stats =
                {
                    Values =
                    {
                        [ForeverCombatStatKeys.HitRating] =
                            10m,

                        [ForeverCombatStatKeys.Strength] =
                            20m
                    }
                }
            }
        );

        profile.Talents.Selections.Add(
            new CharacterTalentSelection
            {
                TalentKey =
                    "rating-talent",

                Name =
                    "Rating Talent",

                Rank =
                    1,

                Stats =
                {
                    Values =
                    {
                        [ForeverCombatStatKeys.HitRating] =
                            10m
                    }
                }
            }
        );

        profile.Buffs.Selections.Add(
            new CharacterBuffSelection
            {
                BuffKey =
                    "strength-buff",

                Name =
                    "Strength Buff",

                Stats =
                {
                    Values =
                    {
                        [ForeverCombatStatKeys.Strength] =
                            20m,

                        [ForeverCombatStatKeys.CriticalStrikeRating] =
                            14m
                    }
                }
            }
        );

        var result =
            ForeverCharacterSimulationRuntimeCalculatorFactory
                .CreateStandard(
                    maximumHealthResolver:
                        _ =>
                            5000m
                )
                .Calculate(
                    profile
                );

        Assert.Equal(
            20m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.HitRating
            )
        );

        Assert.Equal(
            2m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.HitChancePercent
            )
        );

        Assert.Equal(
            1m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.AttackCriticalChancePercent
            )
        );

        Assert.Equal(
            60m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.Strength
            )
        );

        Assert.Equal(
            3m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.BlockValue
            )
        );
    }

    [Fact]
    public void DerivedContributorAppearsInStatContributionBreakdown()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Warrior
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.HitRating,
            10m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Strength,
            20m
        );

        var result =
            CreateCalculator()
                .Calculate(
                    profile
                );

        var hitContribution =
            Assert.Single(
                result.StatContributions,
                contribution =>
                    contribution.ContributorKey ==
                        "forever-physical-stat-conversions" &&
                    contribution.StatKey ==
                        ForeverCombatStatKeys.HitChancePercent
            );

        Assert.Equal(
            1m,
            hitContribution.Amount
        );

        var blockValueContribution =
            Assert.Single(
                result.StatContributions,
                contribution =>
                    contribution.ContributorKey ==
                        "forever-physical-stat-conversions" &&
                    contribution.StatKey ==
                        ForeverCombatStatKeys.BlockValue
            );

        Assert.Equal(
            1m,
            blockValueContribution.Amount
        );
    }

    [Fact]
    public void RatingConversionSupportsFractionalPercentWithoutRounding()
    {
        Assert.Equal(
            1.5m,
            ForeverPhysicalStatConversions.RatingToPercent(
                15m,
                ForeverPhysicalStatConversions.HitRatingPerPercent
            )
        );

        Assert.Equal(
            0.5m,
            ForeverPhysicalStatConversions.RatingToPercent(
                7m,
                ForeverPhysicalStatConversions.CriticalStrikeRatingPerPercent
            )
        );
    }

    [Fact]
    public void RatingConversionRejectsInvalidDivisor()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                ForeverPhysicalStatConversions.RatingToPercent(
                    10m,
                    0m
                )
        );
    }

    private static BaseStatsCharacterSimulationRuntimeCalculator
        CreateCalculator()
    {
        return new BaseStatsCharacterSimulationRuntimeCalculator(
            maximumHealthResolver:
                _ =>
                    5000m,

            statContributors:
                [
                    new ForeverPhysicalStatConversionContributor()
                ]
        );
    }

    private static CharacterProfile CreateProfile(
        string classKey)
    {
        return new CharacterProfile
        {
            Name =
                "Forever Conversion Character",

            RulesetKey =
                "wow-forever",

            ClassKey =
                classKey,

            Level =
                60
        };
    }
}
}

// ==================== ForeverPrimaryPhysicalStatConversionTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Characters.Buffs;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Core.Characters.Talents;
using WarcraftSim.Core.Simulation.Building;
using WarcraftSim.Data.Forever.Characters;
using WarcraftSim.Data.Forever.Combat;
using WarcraftSim.Data.Forever.Simulation;

public sealed class ForeverPrimaryPhysicalStatConversionTests
{
    [Theory]
    [InlineData(ForeverCharacterClassKeys.Warrior, 2)]
    [InlineData(ForeverCharacterClassKeys.Paladin, 2)]
    [InlineData(ForeverCharacterClassKeys.Hunter, 1)]
    [InlineData(ForeverCharacterClassKeys.Rogue, 1)]
    [InlineData(ForeverCharacterClassKeys.Priest, 1)]
    [InlineData(ForeverCharacterClassKeys.Shaman, 2)]
    [InlineData(ForeverCharacterClassKeys.Mage, 1)]
    [InlineData(ForeverCharacterClassKeys.Warlock, 1)]
    [InlineData(ForeverCharacterClassKeys.Druid, 2)]
    public void StrengthAttackPowerMatchesCurrentForeverClassData(
        string classKey,
        int expectedAttackPowerPerStrength)
    {
        var profile =
            CreateProfile(
                classKey,
                level:
                    30
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Strength,
            10m
        );

        var result =
            CreatePrimaryCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            10m *
            expectedAttackPowerPerStrength,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.AttackPower
            )
        );
    }

    [Fact]
    public void HunterAgilityProvidesMeleeAndRangedAttackPowerArmorAndCrit()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Hunter,
                level:
                    30
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Agility,
            48m
        );

        var result =
            CreatePrimaryCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            48m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.AttackPower
            )
        );

        Assert.Equal(
            96m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.RangedAttackPower
            )
        );

        Assert.Equal(
            96m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.Armor
            )
        );

        Assert.Equal(
            2m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.AttackCriticalChancePercent
            )
        );
    }

    [Fact]
    public void RogueLevelSixtyUsesClientEmbeddedAgilityCritConversion()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Rogue,
                level:
                    60
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Agility,
            58m
        );

        var result =
            CreatePrimaryCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            58m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.AttackPower
            )
        );

        Assert.Equal(
            116m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.RangedAttackPower
            )
        );

        Assert.Equal(
            2m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.AttackCriticalChancePercent
            )
        );
    }

    [Fact]
    public void WarriorAgilityProvidesRangedAttackPowerButNotMeleeAttackPower()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Warrior,
                level:
                    30
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Agility,
            20.8m
        );

        var result =
            CreatePrimaryCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            0m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.AttackPower
            )
        );

        Assert.Equal(
            41.6m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.RangedAttackPower
            )
        );

        Assert.Equal(
            2m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.AttackCriticalChancePercent
            )
        );
    }

    [Fact]
    public void DruidAgilityDoesNotApplyCatFormAttackPowerOutsideFormRuntime()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Druid,
                level:
                    30
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Agility,
            22.4m
        );

        var result =
            CreatePrimaryCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            0m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.AttackPower
            )
        );

        Assert.Equal(
            44.8m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.Armor
            )
        );

        Assert.Equal(
            2m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.AttackCriticalChancePercent
            )
        );
    }

    [Fact]
    public void PaladinAgilityAddsArmorAndCritButNoAttackPower()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Paladin,
                level:
                    60
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Strength,
            50m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Agility,
            39.6m
        );

        var result =
            CreatePrimaryCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            100m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.AttackPower
            )
        );

        Assert.Equal(
            0m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.RangedAttackPower
            )
        );

        Assert.Equal(
            79.2m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.Armor
            )
        );

        Assert.Equal(
            2m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.AttackCriticalChancePercent
            )
        );
    }

    [Fact]
    public void StandardForeverPipelineCombinesPrimaryStatsAndRatings()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Rogue,
                level:
                    30
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Agility,
            13m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.CriticalStrikeRating,
            14m
        );

        var result =
            ForeverCharacterSimulationRuntimeCalculatorFactory
                .CreateStandard(
                    maximumHealthResolver:
                        _ =>
                            5000m
                )
                .Calculate(
                    profile
                );

        Assert.Equal(
            2m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.AttackCriticalChancePercent
            )
        );
    }

    [Fact]
    public void PrimaryConversionRunsAfterEquipmentTalentsAndStaticBuffs()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Hunter,
                level:
                    30
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Strength,
            10m
        );

        profile.Equipment.Items.Add(
            new CharacterEquipmentItem
            {
                SlotKey =
                    "head",

                ItemKey =
                    "agility-helm",

                Name =
                    "Agility Helm",

                Stats =
                {
                    Values =
                    {
                        [ForeverCombatStatKeys.Agility] =
                            12m
                    }
                }
            }
        );

        profile.Talents.Selections.Add(
            new CharacterTalentSelection
            {
                TalentKey =
                    "agility-talent",

                Name =
                    "Agility Talent",

                Rank =
                    1,

                Stats =
                {
                    Values =
                    {
                        [ForeverCombatStatKeys.Agility] =
                            12m
                    }
                }
            }
        );

        profile.Buffs.Selections.Add(
            new CharacterBuffSelection
            {
                BuffKey =
                    "agility-buff",

                Name =
                    "Agility Buff",

                Stats =
                {
                    Values =
                    {
                        [ForeverCombatStatKeys.Agility] =
                            24m
                    }
                }
            }
        );

        var result =
            ForeverCharacterSimulationRuntimeCalculatorFactory
                .CreateStandard(
                    maximumHealthResolver:
                        _ =>
                            5000m
                )
                .Calculate(
                    profile
                );

        Assert.Equal(
            48m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.Agility
            )
        );

        Assert.Equal(
            58m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.AttackPower
            )
        );

        Assert.Equal(
            96m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.RangedAttackPower
            )
        );

        Assert.Equal(
            2m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.AttackCriticalChancePercent
            )
        );
    }

    [Fact]
    public void ExistingDirectAttackPowerAndArmorArePreservedAndAddedTo()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Hunter,
                level:
                    30
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Strength,
            10m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Agility,
            24m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.AttackPower,
            50m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Armor,
            100m
        );

        var result =
            CreatePrimaryCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            84m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.AttackPower
            )
        );

        Assert.Equal(
            148m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.Armor
            )
        );
    }

    [Fact]
    public void UnsupportedAgilityCritLevelFailsInsteadOfInterpolating()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Rogue,
                level:
                    45
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Agility,
            20m
        );

        var exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    CreatePrimaryCalculator()
                        .Calculate(
                            profile
                        )
            );

        Assert.Contains(
            "not configured yet",
            exception.Message,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void UnsupportedClassFailsInsteadOfGuessingPrimaryConversions()
    {
        var profile =
            CreateProfile(
                "new-forever-class",
                level:
                    30
            );

        var exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    CreatePrimaryCalculator()
                        .Calculate(
                            profile
                        )
            );

        Assert.Contains(
            "no Forever primary physical stat profile",
            exception.Message,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void ContributorAppearsInStatContributionBreakdown()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Warrior,
                level:
                    30
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Strength,
            20m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Agility,
            10.4m
        );

        var result =
            CreatePrimaryCalculator()
                .Calculate(
                    profile
                );

        Assert.Contains(
            result.StatContributions,
            contribution =>
                contribution.ContributorKey ==
                    "forever-primary-physical-stat-conversions" &&
                contribution.StatKey ==
                    ForeverCombatStatKeys.AttackPower &&
                contribution.Amount ==
                    40m
        );

        Assert.Contains(
            result.StatContributions,
            contribution =>
                contribution.ContributorKey ==
                    "forever-primary-physical-stat-conversions" &&
                contribution.StatKey ==
                    ForeverCombatStatKeys.AttackCriticalChancePercent &&
                contribution.Amount ==
                    1m
        );
    }

    [Theory]
    [InlineData(ForeverCharacterClassKeys.Warrior, 10.4, 20.0)]
    [InlineData(ForeverCharacterClassKeys.Paladin, 10.7, 19.8)]
    [InlineData(ForeverCharacterClassKeys.Hunter, 24.0, 52.9)]
    [InlineData(ForeverCharacterClassKeys.Rogue, 13.0, 29.0)]
    [InlineData(ForeverCharacterClassKeys.Priest, 14.0, 20.0)]
    [InlineData(ForeverCharacterClassKeys.Shaman, 11.5, 19.7)]
    [InlineData(ForeverCharacterClassKeys.Mage, 14.5, 19.5)]
    [InlineData(ForeverCharacterClassKeys.Warlock, 12.0, 20.0)]
    [InlineData(ForeverCharacterClassKeys.Druid, 11.2, 20.0)]
    public void CritMilestonesMatchCurrentClientData(
        string classKey,
        double expectedAt30,
        double expectedAt60)
    {
        Assert.Equal(
            (decimal)expectedAt30,
            ForeverPrimaryPhysicalStatProfiles
                .GetAgilityPerCriticalPercent(
                    classKey,
                    30
                )
        );

        Assert.Equal(
            (decimal)expectedAt60,
            ForeverPrimaryPhysicalStatProfiles
                .GetAgilityPerCriticalPercent(
                    classKey,
                    60
                )
        );
    }

    private static BaseStatsCharacterSimulationRuntimeCalculator
        CreatePrimaryCalculator()
    {
        return new BaseStatsCharacterSimulationRuntimeCalculator(
            maximumHealthResolver:
                _ =>
                    5000m,

            statContributors:
                [
                    new ForeverPrimaryPhysicalStatContributor()
                ]
        );
    }

    private static CharacterProfile CreateProfile(
        string classKey,
        int level)
    {
        return new CharacterProfile
        {
            Name =
                "Forever Primary Stat Character",

            RulesetKey =
                "wow-forever",

            ClassKey =
                classKey,

            Level =
                level
        };
    }
}
}

// ==================== ForeverSpellStatConversionTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Characters.Buffs;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Core.Characters.Talents;
using WarcraftSim.Core.Simulation.Building;
using WarcraftSim.Data.Forever.Characters;
using WarcraftSim.Data.Forever.Combat;
using WarcraftSim.Data.Forever.Simulation;

public sealed class ForeverSpellStatConversionTests
{
    [Fact]
    public void HasteRatingConstantMatchesCurrentForeverClientValue()
    {
        Assert.Equal(
            10m,
            ForeverSpellStatConversions.HasteRatingPerPercent
        );
    }

    [Fact]
    public void MageLevelThirtyIntellectProvidesSpellCriticalChance()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Mage,
                level:
                    30
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Intellect,
            27.3m
        );

        var result =
            CreateSpellCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            1m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.SpellCriticalChancePercent
            )
        );
    }

    [Fact]
    public void WarlockLevelSixtyUsesClientEmbeddedIntellectCritConversion()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Warlock,
                level:
                    60
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Intellect,
            121.2m
        );

        var result =
            CreateSpellCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            2m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.SpellCriticalChancePercent
            )
        );
    }

    [Fact]
    public void SharedHitAndCritRatingsProduceSpellFacingPercentStats()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Priest,
                level:
                    30
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.HitRating,
            20m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.CriticalStrikeRating,
            28m
        );

        var result =
            CreateSpellCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            2m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.SpellHitChancePercent
            )
        );

        Assert.Equal(
            2m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.SpellCriticalChancePercent
            )
        );
    }

    [Fact]
    public void HasteRatingProducesHastePercentForTimingConsumers()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Hunter,
                level:
                    30
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.HasteRating,
            25m
        );

        var result =
            CreateSpellCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            2.5m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.HastePercent
            )
        );
    }

    [Fact]
    public void StandardForeverPipelineCombinesIntellectAndCriticalRating()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Mage,
                level:
                    30
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Intellect,
            27.3m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.CriticalStrikeRating,
            14m
        );

        var result =
            ForeverCharacterSimulationRuntimeCalculatorFactory
                .CreateStandard(
                    maximumHealthResolver:
                        _ =>
                            5000m
                )
                .Calculate(
                    profile
                );

        Assert.Equal(
            2m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.SpellCriticalChancePercent
            )
        );

        Assert.Equal(
            1m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.AttackCriticalChancePercent
            )
        );
    }

    [Fact]
    public void StandardForeverPipelineProducesPhysicalAndSpellHitFromSharedRating()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Shaman,
                level:
                    30
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.HitRating,
            30m
        );

        var result =
            ForeverCharacterSimulationRuntimeCalculatorFactory
                .CreateStandard(
                    maximumHealthResolver:
                        _ =>
                            5000m
                )
                .Calculate(
                    profile
                );

        Assert.Equal(
            3m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.HitChancePercent
            )
        );

        Assert.Equal(
            3m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.SpellHitChancePercent
            )
        );
    }

    [Fact]
    public void SpellConversionRunsAfterEquipmentTalentsAndStaticBuffs()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Mage,
                level:
                    30
            );

        profile.Equipment.Items.Add(
            new CharacterEquipmentItem
            {
                SlotKey =
                    "head",

                ItemKey =
                    "intellect-helm",

                Name =
                    "Intellect Helm",

                Stats =
                {
                    Values =
                    {
                        [ForeverCombatStatKeys.Intellect] =
                            9.1m
                    }
                }
            }
        );

        profile.Talents.Selections.Add(
            new CharacterTalentSelection
            {
                TalentKey =
                    "intellect-talent",

                Name =
                    "Intellect Talent",

                Rank =
                    1,

                Stats =
                {
                    Values =
                    {
                        [ForeverCombatStatKeys.Intellect] =
                            9.1m
                    }
                }
            }
        );

        profile.Buffs.Selections.Add(
            new CharacterBuffSelection
            {
                BuffKey =
                    "intellect-buff",

                Name =
                    "Intellect Buff",

                Stats =
                {
                    Values =
                    {
                        [ForeverCombatStatKeys.Intellect] =
                            9.1m
                    }
                }
            }
        );

        var result =
            ForeverCharacterSimulationRuntimeCalculatorFactory
                .CreateStandard(
                    maximumHealthResolver:
                        _ =>
                            5000m
                )
                .Calculate(
                    profile
                );

        Assert.Equal(
            27.3m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.Intellect
            )
        );

        Assert.Equal(
            1m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.SpellCriticalChancePercent
            )
        );
    }

    [Fact]
    public void ExistingDirectSpellPercentBonusesArePreservedAndAddedTo()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Druid,
                level:
                    30
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Intellect,
            28.4m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.HitRating,
            10m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.CriticalStrikeRating,
            14m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.HasteRating,
            10m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.SpellHitChancePercent,
            2m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.SpellCriticalChancePercent,
            3m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.HastePercent,
            4m
        );

        var result =
            CreateSpellCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            3m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.SpellHitChancePercent
            )
        );

        Assert.Equal(
            5m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.SpellCriticalChancePercent
            )
        );

        Assert.Equal(
            5m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.HastePercent
            )
        );
    }

    [Theory]
    [InlineData(ForeverCharacterClassKeys.Warrior)]
    [InlineData(ForeverCharacterClassKeys.Rogue)]
    public void NonManaClassesDoNotGainSpellCritFromIntellect(
        string classKey)
    {
        var profile =
            CreateProfile(
                classKey,
                level:
                    30
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Intellect,
            100m
        );

        var result =
            CreateSpellCalculator()
                .Calculate(
                    profile
                );

        Assert.Equal(
            0m,
            result.EffectiveStats.Get(
                ForeverCombatStatKeys.SpellCriticalChancePercent
            )
        );
    }

    [Fact]
    public void UnsupportedSpellCritLevelFailsInsteadOfInterpolating()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Mage,
                level:
                    45
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Intellect,
            20m
        );

        var exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    CreateSpellCalculator()
                        .Calculate(
                            profile
                        )
            );

        Assert.Contains(
            "not configured yet",
            exception.Message,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void UnsupportedClassFailsInsteadOfGuessingSpellConversions()
    {
        var profile =
            CreateProfile(
                "new-forever-class",
                level:
                    30
            );

        var exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    CreateSpellCalculator()
                        .Calculate(
                            profile
                        )
            );

        Assert.Contains(
            "no Forever primary spell stat profile",
            exception.Message,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void ContributorAppearsInStatContributionBreakdown()
    {
        var profile =
            CreateProfile(
                ForeverCharacterClassKeys.Mage,
                level:
                    30
            );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.Intellect,
            27.3m
        );

        profile.BaseStats.Set(
            ForeverCombatStatKeys.HitRating,
            10m
        );

        var result =
            CreateSpellCalculator()
                .Calculate(
                    profile
                );

        Assert.Contains(
            result.StatContributions,
            contribution =>
                contribution.ContributorKey ==
                    "forever-spell-stat-conversions" &&
                contribution.StatKey ==
                    ForeverCombatStatKeys.SpellHitChancePercent &&
                contribution.Amount ==
                    1m
        );

        Assert.Contains(
            result.StatContributions,
            contribution =>
                contribution.ContributorKey ==
                    "forever-spell-stat-conversions" &&
                contribution.StatKey ==
                    ForeverCombatStatKeys.SpellCriticalChancePercent &&
                contribution.Amount ==
                    1m
        );
    }

    [Theory]
    [InlineData(ForeverCharacterClassKeys.Paladin, 31.9, 59.9)]
    [InlineData(ForeverCharacterClassKeys.Hunter, 32.9, 60.6)]
    [InlineData(ForeverCharacterClassKeys.Priest, 26.9, 59.5)]
    [InlineData(ForeverCharacterClassKeys.Shaman, 28.2, 59.2)]
    [InlineData(ForeverCharacterClassKeys.Mage, 27.3, 59.5)]
    [InlineData(ForeverCharacterClassKeys.Warlock, 28.2, 60.6)]
    [InlineData(ForeverCharacterClassKeys.Druid, 28.4, 59.9)]
    public void SpellCritMilestonesMatchCurrentClientData(
        string classKey,
        double expectedAt30,
        double expectedAt60)
    {
        Assert.Equal(
            (decimal)expectedAt30,
            ForeverPrimarySpellStatProfiles
                .GetIntellectPerSpellCriticalPercent(
                    classKey,
                    30
                )
        );

        Assert.Equal(
            (decimal)expectedAt60,
            ForeverPrimarySpellStatProfiles
                .GetIntellectPerSpellCriticalPercent(
                    classKey,
                    60
                )
        );
    }

    private static BaseStatsCharacterSimulationRuntimeCalculator
        CreateSpellCalculator()
    {
        return new BaseStatsCharacterSimulationRuntimeCalculator(
            maximumHealthResolver:
                _ =>
                    5000m,

            statContributors:
                [
                    new ForeverSpellStatConversionContributor()
                ]
        );
    }

    private static CharacterProfile CreateProfile(
        string classKey,
        int level)
    {
        return new CharacterProfile
        {
            Name =
                "Forever Spell Stat Character",

            RulesetKey =
                "wow-forever",

            ClassKey =
                classKey,

            Level =
                level
        };
    }
}
}
