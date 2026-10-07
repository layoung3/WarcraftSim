using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Tests;

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
