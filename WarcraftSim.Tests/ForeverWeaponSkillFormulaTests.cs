using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Tests;

public sealed class ForeverWeaponSkillFormulaTests
{
    [Fact]
    public void EqualLevelMaxSkillHasNoHitCritAdjustmentAndTenPercentGlancing()
    {
        var source =
            CreateActor(
                "attacker",
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

        var adjustment =
            CreateProvider()
                .GetAdjustment(
                    CreateContext(),
                    source,
                    target,
                    CreateAbility(),
                    CreateSkillEffect(),
                    CreateRule()
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
            adjustment.CriticalChancePercentDelta
        );

        Assert.Equal(
            10m,
            adjustment.GlancingChancePercentOverride
        );
    }

    [Fact]
    public void PlusThreeTargetProducesCurrentForeverSkillAdjustments()
    {
        var source =
            CreateActor(
                "attacker",
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

        var adjustment =
            CreateProvider()
                .GetAdjustment(
                    CreateContext(),
                    source,
                    target,
                    CreateAbility(),
                    CreateSkillEffect(),
                    CreateRule()
                );

        Assert.Equal(
            -0.60m,
            adjustment.HitChancePercentDelta
        );

        Assert.Equal(
            0.60m,
            adjustment.DodgeChancePercentDelta
        );

        Assert.Equal(
            0.60m,
            adjustment.ParryChancePercentDelta
        );

        Assert.Equal(
            -0.30m,
            adjustment.CriticalChancePercentDelta
        );

        Assert.Equal(
            40m,
            adjustment.GlancingChancePercentOverride
        );
    }

    [Fact]
    public void SkillAboveNaturalCapImprovesHitCritButNotGlancingChance()
    {
        var source =
            CreateActor(
                "attacker",
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
            305m
        );

        var adjustment =
            CreateProvider()
                .GetAdjustment(
                    CreateContext(),
                    source,
                    target,
                    CreateAbility(),
                    CreateSkillEffect(),
                    CreateRule()
                );

        Assert.Equal(
            -0.40m,
            adjustment.HitChancePercentDelta
        );

        Assert.Equal(
            0.40m,
            adjustment.DodgeChancePercentDelta
        );

        Assert.Equal(
            0.40m,
            adjustment.ParryChancePercentDelta
        );

        Assert.Equal(
            -0.20m,
            adjustment.CriticalChancePercentDelta
        );

        Assert.Equal(
            40m,
            adjustment.GlancingChancePercentOverride
        );
    }

    [Fact]
    public void UnderSkilledWeaponRaisesGlancingChanceAgainstEqualLevelTarget()
    {
        var source =
            CreateActor(
                "attacker",
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
            290m
        );

        var adjustment =
            CreateProvider()
                .GetAdjustment(
                    CreateContext(),
                    source,
                    target,
                    CreateAbility(),
                    CreateSkillEffect(),
                    CreateRule()
                );

        Assert.Equal(
            -0.40m,
            adjustment.HitChancePercentDelta
        );

        Assert.Equal(
            -0.20m,
            adjustment.CriticalChancePercentDelta
        );

        Assert.Equal(
            30m,
            adjustment.GlancingChancePercentOverride
        );
    }

    [Fact]
    public void EffectWithoutAttackSkillKeyGetsNoForeverAdjustment()
    {
        var source =
            CreateActor(
                "attacker",
                level:
                    60
            );

        var target =
            CreateActor(
                "target",
                level:
                    63
            );

        var adjustment =
            CreateProvider()
                .GetAdjustment(
                    CreateContext(),
                    source,
                    target,
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
    public void MissingConfiguredAttackSkillFailsInsteadOfSilentlyGuessing()
    {
        var exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    CreateProvider()
                        .GetAdjustment(
                            CreateContext(),
                            CreateActor(
                                "attacker",
                                level:
                                    60
                            ),
                            CreateActor(
                                "target",
                                level:
                                    63
                            ),
                            CreateAbility(),
                            CreateSkillEffect(),
                            CreateRule()
                        )
            );

        Assert.Contains(
            "missing attack skill stat",
            exception.Message,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void RulesetResolverUsesContextProviderForGlancingOverride()
    {
        var source =
            CreateActor(
                "attacker",
                level:
                    60
            );

        var target =
            CreateActor(
                "target",
                level:
                    60
            );

        var effect =
            CreateSkillEffect();

        effect.CanMiss =
            false;

        effect.CanGlance =
            true;

        effect.CanCrit =
            false;

        var ability =
            CreateAbility();

        ability.Effects.Add(
            effect
        );

        var ruleset =
            CreateRuleset();

        var resolver =
            new RulesetCombatRollResolver(
                ruleset,
                new FixedAdjustmentProvider(
                    new CombatRollContextAdjustment
                    {
                        GlancingChancePercentOverride =
                            100m
                    }
                )
            );

        var result =
            resolver.Resolve(
                CreateContext(),
                source,
                target,
                ability,
                effect
            );

        Assert.Equal(
            CombatResultTypes.Glancing,
            result.ResultKey
        );
    }

    [Fact]
    public void RulesetResolverUsesContextProviderForCriticalAdjustment()
    {
        var source =
            CreateActor(
                "attacker",
                level:
                    60
            );

        var target =
            CreateActor(
                "target",
                level:
                    60
            );

        var effect =
            CreateSkillEffect();

        effect.CanMiss =
            false;

        effect.CanGlance =
            false;

        effect.CanCrit =
            true;

        var resolver =
            new RulesetCombatRollResolver(
                CreateRuleset(),
                new FixedAdjustmentProvider(
                    new CombatRollContextAdjustment
                    {
                        CriticalChancePercentDelta =
                            100m
                    }
                )
            );

        var result =
            resolver.Resolve(
                CreateContext(),
                source,
                target,
                CreateAbility(),
                effect
            );

        Assert.Equal(
            CombatResultTypes.Critical,
            result.ResultKey
        );
    }

    [Fact]
    public void RulesetResolverUsesContextProviderForHitAdjustment()
    {
        var source =
            CreateActor(
                "attacker",
                level:
                    60
            );

        var target =
            CreateActor(
                "target",
                level:
                    60
            );

        var effect =
            CreateSkillEffect();

        effect.CanMiss =
            true;

        effect.CanGlance =
            false;

        effect.CanCrit =
            false;

        var resolver =
            new RulesetCombatRollResolver(
                CreateRuleset(),
                new FixedAdjustmentProvider(
                    new CombatRollContextAdjustment
                    {
                        HitChancePercentDelta =
                            -100m
                    }
                )
            );

        var result =
            resolver.Resolve(
                CreateContext(),
                source,
                target,
                CreateAbility(),
                effect
            );

        Assert.Equal(
            CombatResultTypes.Miss,
            result.ResultKey
        );
    }

    private static ForeverWeaponSkillCombatRollAdjustmentProvider
        CreateProvider()
    {
        return new ForeverWeaponSkillCombatRollAdjustmentProvider();
    }

    private static CombatRulesetDefinition CreateRuleset()
    {
        var ruleset =
            new CombatRulesetDefinition
            {
                RulesetKey =
                    "forever-weapon-skill-tests",

                Version =
                    "1"
            };

        ruleset.RollRules[
            CombatResolutionTypes.Melee
        ] =
            CreateRule();

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
                100m,

            BaseCriticalChancePercent =
                0m,

            BaseGlancingChancePercent =
                0m,

            MinimumGlancingDamageMultiplier =
                1m,

            MaximumGlancingDamageMultiplier =
                1m
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

    private static AbilityEffectDefinition CreateSkillEffect()
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

            AttackSkillStatKey =
                "weapon-skill",

            CanMiss =
                true,

            CanBeDodged =
                true,

            CanBeParried =
                true,

            CanGlance =
                true,

            CanCrit =
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
                    key == "attacker"
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
