using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Tests;

public sealed class ForeverSpellCombatResolutionTests
{
    [Fact]
    public void SpellRuleUsesSpellFacingHitAndCriticalStats()
    {
        var rule =
            GetSpellRule();

        Assert.Equal(
            96m,
            rule.BaseHitChancePercent
        );

        Assert.Equal(
            ForeverCombatStatKeys.SpellHitChancePercent,
            rule.HitChanceStatKey
        );

        Assert.Equal(
            ForeverCombatStatKeys.SpellCriticalChancePercent,
            rule.CriticalChanceStatKey
        );

        Assert.False(
            rule.UseSingleRollTable
        );

        Assert.Equal(
            1.5m,
            rule.CriticalMultiplier
        );
    }

    [Fact]
    public void DirectSpellProfileAllowsMissAndCritButNotPhysicalAvoidanceOutcomes()
    {
        var effect =
            new AbilityEffectDefinition
            {
                Key =
                    "spell-effect"
            };

        ForeverSpellAttackProfiles.Apply(
            effect,
            ForeverSpellAttackProfiles.PlayerDirectSpell
        );

        Assert.Equal(
            ForeverCombatResolutionTypes.PlayerSpell,
            effect.ResolutionType
        );

        Assert.True(
            effect.CanMiss
        );

        Assert.True(
            effect.CanCrit
        );

        Assert.False(
            effect.CanBeDodged
        );

        Assert.False(
            effect.CanBeParried
        );

        Assert.False(
            effect.CanBeBlocked
        );

        Assert.False(
            effect.CanGlance
        );

        Assert.False(
            effect.CanCrush
        );

        Assert.Null(
            effect.AttackSkillStatKey
        );

        Assert.Null(
            effect.TargetDefenseSkillStatKey
        );
    }

    [Theory]
    [InlineData(30, 30, 0.0)]
    [InlineData(30, 31, -1.0)]
    [InlineData(30, 32, -2.0)]
    [InlineData(30, 33, -13.0)]
    [InlineData(30, 34, -24.0)]
    public void HigherTargetLevelsApplyProvisionalClassicSpellHitProgression(
        int sourceLevel,
        int targetLevel,
        double expectedHitDelta)
    {
        var adjustment =
            new ForeverSpellLevelCombatRollAdjustmentProvider()
                .GetAdjustment(
                    CreateContext(),
                    CreateActor(
                        "player",
                        sourceLevel
                    ),
                    CreateActor(
                        "target",
                        targetLevel
                    ),
                    CreateAbility(),
                    CreateSpellEffect(),
                    GetSpellRule()
                );

        Assert.Equal(
            (decimal)expectedHitDelta,
            adjustment.HitChancePercentDelta
        );
    }

    [Fact]
    public void PlusThreeTargetProducesSeventeenPercentBaseSpellMissChance()
    {
        var source =
            CreateActor(
                "player",
                level:
                    30
            );

        var target =
            CreateActor(
                "boss",
                level:
                    33
            );

        var rule =
            GetSpellRule();

        var adjustment =
            new ForeverSpellLevelCombatRollAdjustmentProvider()
                .GetAdjustment(
                    CreateContext(),
                    source,
                    target,
                    CreateAbility(),
                    CreateSpellEffect(),
                    rule
                );

        var effectiveHit =
            rule.BaseHitChancePercent +
            adjustment.HitChancePercentDelta;

        Assert.Equal(
            83m,
            effectiveHit
        );

        Assert.Equal(
            17m,
            100m - effectiveHit
        );
    }

    [Fact]
    public void SpellHitStatCanCapPlusThreeTargetAtOneHundredPercentHit()
    {
        var source =
            CreateActor(
                "player",
                level:
                    30
            );

        var target =
            CreateActor(
                "boss",
                level:
                    33
            );

        source.Stats.Set(
            ForeverCombatStatKeys.SpellHitChancePercent,
            17m
        );

        var resolver =
            new RulesetCombatRollResolver(
                ForeverCombatRulesetFactory.Create(),
                new ForeverCombatRollContextAdjustmentProvider()
            );

        var result =
            resolver.Resolve(
                CreateContext(),
                source,
                target,
                CreateAbility(),
                CreateSpellEffect(
                    canCrit:
                        false
                )
            );

        Assert.True(
            result.Landed
        );

        Assert.Equal(
            CombatResultTypes.Hit,
            result.ResultKey
        );
    }

    [Fact]
    public void SpellCriticalChanceStatProducesOnePointFiveMultiplierCritical()
    {
        var source =
            CreateActor(
                "player",
                level:
                    30
            );

        var target =
            CreateActor(
                "target",
                level:
                    30
            );

        source.Stats.Set(
            ForeverCombatStatKeys.SpellCriticalChancePercent,
            100m
        );

        var effect =
            CreateSpellEffect();

        effect.CanMiss =
            false;

        var resolver =
            new RulesetCombatRollResolver(
                ForeverCombatRulesetFactory.Create(),
                new ForeverCombatRollContextAdjustmentProvider()
            );

        var result =
            resolver.Resolve(
                CreateContext(),
                source,
                target,
                CreateAbility(),
                effect
            );

        Assert.True(
            result.Landed
        );

        Assert.True(
            result.IsCritical
        );

        Assert.Equal(
            1.5m,
            result.AmountMultiplier
        );
    }

    [Fact]
    public void NonSpellResolutionDoesNotReceiveSpellLevelPenalty()
    {
        var effect =
            new AbilityEffectDefinition
            {
                Key =
                    "physical-effect",

                ResolutionType =
                    ForeverCombatResolutionTypes.PlayerMeleeSpecial
            };

        var adjustment =
            new ForeverSpellLevelCombatRollAdjustmentProvider()
                .GetAdjustment(
                    CreateContext(),
                    CreateActor(
                        "player",
                        level:
                            30
                    ),
                    CreateActor(
                        "target",
                        level:
                            33
                    ),
                    CreateAbility(),
                    effect,
                    ForeverPhysicalCombatRulesetFactory
                        .Create()
                        .GetRollRule(
                            ForeverCombatResolutionTypes.PlayerMeleeSpecial
                        )!
                );

        Assert.Same(
            CombatRollContextAdjustment.None,
            adjustment
        );
    }

    [Fact]
    public void CombinedForeverRulesetContainsPhysicalAndSpellRules()
    {
        var ruleset =
            ForeverCombatRulesetFactory.Create();

        Assert.NotNull(
            ruleset.GetRollRule(
                ForeverCombatResolutionTypes.PlayerMeleeAuto
            )
        );

        Assert.NotNull(
            ruleset.GetRollRule(
                ForeverCombatResolutionTypes.PlayerMeleeSpecial
            )
        );

        Assert.NotNull(
            ruleset.GetRollRule(
                ForeverCombatResolutionTypes.PlayerSpell
            )
        );

        Assert.Equal(
            "wow-forever",
            ruleset.RulesetKey
        );
    }

    [Fact]
    public void ProvisionalSpellAssumptionsRemainExplicitlyMarkedUnverified()
    {
        var profile =
            ForeverSpellAttackProfiles.PlayerDirectSpell;

        Assert.False(
            profile.LevelMissProgressionVerified
        );

        Assert.False(
            profile.CriticalMultiplierVerified
        );
    }

    private static CombatRollRuleDefinition GetSpellRule()
    {
        return ForeverSpellCombatRulesetFactory
            .Create()
            .GetRollRule(
                ForeverCombatResolutionTypes.PlayerSpell
            )!;
    }

    private static AbilityDefinition CreateAbility()
    {
        return new AbilityDefinition
        {
            Key =
                "spell",

            Name =
                "Spell"
        };
    }

    private static AbilityEffectDefinition CreateSpellEffect(
        bool canCrit = true)
    {
        var effect =
            new AbilityEffectDefinition
            {
                Key =
                    "spell-effect",

                EffectType =
                    AbilityEffectTypes.DirectDamage,

                TargetType =
                    AbilityTargetTypes.Enemy
            };

        ForeverSpellAttackProfiles.Apply(
            effect,
            ForeverSpellAttackProfiles.PlayerDirectSpell
        );

        effect.CanCrit =
            canCrit;

        return effect;
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
                    key == "player"
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
}
