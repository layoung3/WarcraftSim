// Consolidated tests for Forever Spells Timing.
// Original class names are retained for xUnit test filtering.

// ==================== ForeverAbilityTimingTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Characters;
using WarcraftSim.Data.Forever.Combat;
using WarcraftSim.Data.Forever.Simulation;

public sealed class ForeverAbilityTimingTests
{
    [Fact]
    public void CastHasteApplicationIsExplicitlyMarkedProvisional()
    {
        Assert.False(
            ForeverAbilityTimingProvider
                .CastHasteApplicationVerified
        );
    }

    [Fact]
    public void TwentyPercentHasteReducesThreeSecondCastToTwoPointFiveSeconds()
    {
        var source =
            CreateActor(
                "source"
            );

        source.Stats.Set(
            ForeverCombatStatKeys.HastePercent,
            20m
        );

        var timing =
            new ForeverAbilityTimingProvider()
                .Resolve(
                    CreateContext(),
                    source,
                    CreateAbility(
                        castTimeSeconds:
                            3m
                    )
                );

        Assert.Equal(
            2.5m,
            timing.CastTimeSeconds
        );
    }

    [Fact]
    public void ZeroHastePreservesAbilityTiming()
    {
        var timing =
            new ForeverAbilityTimingProvider()
                .Resolve(
                    CreateContext(),
                    CreateActor(
                        "source"
                    ),
                    CreateAbility(
                        castTimeSeconds:
                            2.5m,

                        globalCooldownSeconds:
                            1.25m
                    )
                );

        Assert.Equal(
            2.5m,
            timing.CastTimeSeconds
        );

        Assert.Equal(
            1.25m,
            timing.GlobalCooldownSeconds
        );
    }

    [Fact]
    public void HasteDoesNotGloballyReduceAbilityGlobalCooldown()
    {
        var source =
            CreateActor(
                "source"
            );

        source.Stats.Set(
            ForeverCombatStatKeys.HastePercent,
            50m
        );

        var timing =
            new ForeverAbilityTimingProvider()
                .Resolve(
                    CreateContext(),
                    source,
                    CreateAbility(
                        castTimeSeconds:
                            3m,

                        globalCooldownSeconds:
                            1.5m
                    )
                );

        Assert.Equal(
            2m,
            timing.CastTimeSeconds
        );

        Assert.Equal(
            1.5m,
            timing.GlobalCooldownSeconds
        );
    }

    [Fact]
    public void HasteLeavesChannelTimingUnchangedUntilForeverBehaviorIsVerified()
    {
        var source =
            CreateActor(
                "source"
            );

        source.Stats.Set(
            ForeverCombatStatKeys.HastePercent,
            25m
        );

        var ability =
            CreateAbility(
                castTimeSeconds:
                    0m
            );

        ability.ChannelDurationSeconds =
            6m;

        ability.ChannelTickIntervalSeconds =
            1m;

        var timing =
            new ForeverAbilityTimingProvider()
                .Resolve(
                    CreateContext(),
                    source,
                    ability
                );

        Assert.Equal(
            6m,
            timing.ChannelDurationSeconds
        );

        Assert.Equal(
            1m,
            timing.ChannelTickIntervalSeconds
        );
    }

    [Fact]
    public void InstantCastRemainsInstantUnderHaste()
    {
        var source =
            CreateActor(
                "source"
            );

        source.Stats.Set(
            ForeverCombatStatKeys.HastePercent,
            100m
        );

        var timing =
            new ForeverAbilityTimingProvider()
                .Resolve(
                    CreateContext(),
                    source,
                    CreateAbility(
                        castTimeSeconds:
                            0m
                    )
                );

        Assert.Equal(
            0m,
            timing.CastTimeSeconds
        );
    }

    [Fact]
    public void NegativeHasteStatDoesNotSpeedCast()
    {
        var source =
            CreateActor(
                "source"
            );

        source.Stats.Set(
            ForeverCombatStatKeys.HastePercent,
            -25m
        );

        var timing =
            new ForeverAbilityTimingProvider()
                .Resolve(
                    CreateContext(),
                    source,
                    CreateAbility(
                        castTimeSeconds:
                            3m
                    )
                );

        Assert.Equal(
            3m,
            timing.CastTimeSeconds
        );
    }

    [Fact]
    public void AbilityExecutorUsesResolvedForeverCastAndActionTiming()
    {
        var source =
            CreateActor(
                "source"
            );

        var target =
            CreateActor(
                "target"
            );

        source.ConfigureActionTiming(
            initialActionDelaySeconds:
                0m,

            inputDelaySeconds:
                0.2m
        );

        source.Stats.Set(
            ForeverCombatStatKeys.HastePercent,
            20m
        );

        source.AddAbility(
            CreateAbility(
                castTimeSeconds:
                    3m,

                globalCooldownSeconds:
                    1.5m
            )
        );

        var context =
            CreateContext();

        context.AddActor(
            source
        );

        context.AddActor(
            target
        );

        var executor =
            CreateAbilityExecutor();

        var result =
            executor.TryStartAbility(
                context,
                source.Key,
                target.Key,
                "test-cast"
            );

        Assert.True(
            result.Success
        );

        Assert.Equal(
            2.5m,
            source.CastReadyAtSeconds
        );

        Assert.Equal(
            1.5m,
            source.GlobalCooldownReadyAtSeconds
        );

        Assert.Equal(
            2.7m,
            source.InputReadyAtSeconds
        );
    }

    [Fact]
    public void AbilityExecutionSnapshotsResolvedTimingAtCastStart()
    {
        var source =
            CreateActor(
                "source"
            );

        var target =
            CreateActor(
                "target"
            );

        source.Stats.Set(
            ForeverCombatStatKeys.HastePercent,
            20m
        );

        source.AddAbility(
            CreateAbility(
                castTimeSeconds:
                    3m
            )
        );

        var context =
            CreateContext();

        context.AddActor(
            source
        );

        context.AddActor(
            target
        );

        var executor =
            CreateAbilityExecutor();

        var result =
            executor.TryStartAbility(
                context,
                source.Key,
                target.Key,
                "test-cast"
            );

        Assert.True(
            result.Success
        );

        var execution =
            context.GetAbilityExecution(
                source.CurrentCastExecutionId!.Value
            );

        Assert.NotNull(
            execution
        );

        source.Stats.Set(
            ForeverCombatStatKeys.HastePercent,
            0m
        );

        Assert.Equal(
            2.5m,
            execution!.Timing.CastTimeSeconds
        );
    }

    [Fact]
    public void HasteRatingPipelineFeedsForeverCastTimingProvider()
    {
        var profile =
            new CharacterProfile
            {
                Name =
                    "Mage",

                ClassKey =
                    ForeverCharacterClassKeys.Mage,

                Level =
                    30
            };

        profile.BaseStats.Set(
            ForeverCombatStatKeys.HasteRating,
            200m
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

        var source =
            CreateActor(
                "source"
            );

        source.Stats =
            result.EffectiveStats;

        var timing =
            new ForeverAbilityTimingProvider()
                .Resolve(
                    CreateContext(),
                    source,
                    CreateAbility(
                        castTimeSeconds:
                            3m
                    )
                );

        Assert.Equal(
            20m,
            source.Stats.Get(
                ForeverCombatStatKeys.HastePercent
            )
        );

        Assert.Equal(
            2.5m,
            timing.CastTimeSeconds
        );
    }

    private static AbilityDefinition CreateAbility(
        decimal castTimeSeconds,
        decimal globalCooldownSeconds = 1.5m)
    {
        return new AbilityDefinition
        {
            Key =
                "test-cast",

            Name =
                "Test Cast",

            CastTimeSeconds =
                castTimeSeconds,

            GlobalCooldownSeconds =
                globalCooldownSeconds
        };
    }

    private static SimulationActorState CreateActor(
        string key)
    {
        var actor =
            new SimulationActorState
            {
                Key =
                    key,

                Name =
                    key,

                TeamKey =
                    key == "target"
                        ? "enemy"
                        : "raid",

                Level =
                    30
            };

        actor.InitializeHealth(
            1000m
        );

        actor.ConfigureActionTiming(
            initialActionDelaySeconds:
                0m,

            inputDelaySeconds:
                0m
        );

        return actor;
    }

    private static SimulationContext CreateContext()
    {
        return new SimulationContext(
            new SimulationRunOptions
            {
                Seed =
                    12345,

                DurationSeconds =
                    10m,

                PrimaryActorKey =
                    "source",

                CaptureTimeline =
                    true
            }
        );
    }

    private static AbilityExecutor CreateAbilityExecutor()
    {
        return new AbilityExecutor(
            new AuraManager(),
            new SimpleCombatRollResolver(),
            new RulesetDamageMitigationResolver(
                new CombatRulesetDefinition
                {
                    RulesetKey =
                        "forever-ability-timing-tests",

                    Version =
                        "1"
                }
            ),
            abilityTimingProvider:
                new ForeverAbilityTimingProvider()
        );
    }
}
}

// ==================== ForeverSpellCombatResolutionTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;

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
}

// ==================== ForeverSpellCriticalDeliveryTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;

public sealed class ForeverSpellCriticalDeliveryTests
{
    [Fact]
    public void NonPeriodicSpellCriticalBonus_AppliesToDirectSpell()
    {
        var result = Resolve(
            CombatEffectDeliveryType.Direct,
            nonPeriodicCriticalChancePercent: 100m
        );

        Assert.True(result.IsCritical);
    }

    [Fact]
    public void NonPeriodicSpellCriticalBonus_AppliesToChannelTick()
    {
        var result = Resolve(
            CombatEffectDeliveryType.ChannelTick,
            nonPeriodicCriticalChancePercent: 100m
        );

        Assert.True(result.IsCritical);
    }

    [Fact]
    public void NonPeriodicSpellCriticalBonus_DoesNotApplyToPeriodicTick()
    {
        var result = Resolve(
            CombatEffectDeliveryType.Periodic,
            nonPeriodicCriticalChancePercent: 100m
        );

        Assert.False(result.IsCritical);
        Assert.Equal(
            CombatResultTypes.Hit,
            result.ResultKey
        );
    }

    [Fact]
    public void PeriodicSpellCriticalBonus_AppliesToPeriodicTick()
    {
        var result = Resolve(
            CombatEffectDeliveryType.Periodic,
            periodicCriticalChancePercent: 100m
        );

        Assert.True(result.IsCritical);
    }

    [Theory]
    [InlineData(CombatEffectDeliveryType.Direct)]
    [InlineData(CombatEffectDeliveryType.ChannelTick)]
    public void PeriodicSpellCriticalBonus_DoesNotApplyToNonPeriodicDelivery(
        CombatEffectDeliveryType deliveryType)
    {
        var result = Resolve(
            deliveryType,
            periodicCriticalChancePercent: 100m
        );

        Assert.False(result.IsCritical);
        Assert.Equal(
            CombatResultTypes.Hit,
            result.ResultKey
        );
    }

    [Theory]
    [InlineData(CombatEffectDeliveryType.Direct)]
    [InlineData(CombatEffectDeliveryType.Periodic)]
    [InlineData(CombatEffectDeliveryType.ChannelTick)]
    public void GeneralSpellCriticalChance_StillAppliesToEveryDeliveryType(
        CombatEffectDeliveryType deliveryType)
    {
        var result = Resolve(
            deliveryType,
            generalCriticalChancePercent: 100m
        );

        Assert.True(result.IsCritical);
    }

    [Theory]
    [InlineData(CombatEffectDeliveryType.Direct)]
    [InlineData(CombatEffectDeliveryType.Periodic)]
    [InlineData(CombatEffectDeliveryType.ChannelTick)]
    public void CanCritFalse_PreventsCriticalRegardlessOfDeliveryBonuses(
        CombatEffectDeliveryType deliveryType)
    {
        var result = Resolve(
            deliveryType,
            canCrit: false,
            generalCriticalChancePercent: 100m,
            nonPeriodicCriticalChancePercent: 100m,
            periodicCriticalChancePercent: 100m
        );

        Assert.False(result.IsCritical);
        Assert.Equal(
            CombatResultTypes.Hit,
            result.ResultKey
        );
    }

    [Fact]
    public void DeliverySpecificCriticalProvider_IgnoresPhysicalResolution()
    {
        var source = CreateActor(
            "player",
            "raid"
        );

        source.Stats.Set(
            ForeverCombatStatKeys.PeriodicSpellCriticalChancePercent,
            100m
        );

        var effect = new AbilityEffectDefinition
        {
            Key = "physical",
            ResolutionType =
                ForeverCombatResolutionTypes.PlayerMeleeSpecial
        };

        var rule =
            ForeverPhysicalCombatRulesetFactory
                .Create()
                .GetRollRule(
                    ForeverCombatResolutionTypes.PlayerMeleeSpecial
                )!;

        var adjustment =
            new ForeverSpellCriticalDeliveryAdjustmentProvider()
                .GetAdjustment(
                    CreateContext(),
                    source,
                    CreateActor(
                        "target",
                        "enemy"
                    ),
                    CreateAbility(),
                    effect,
                    rule,
                    CombatEffectDeliveryType.Periodic
                );

        Assert.Same(
            CombatRollContextAdjustment.None,
            adjustment
        );
    }

    private static CombatRollResult Resolve(
        CombatEffectDeliveryType deliveryType,
        bool canCrit = true,
        decimal generalCriticalChancePercent = 0m,
        decimal nonPeriodicCriticalChancePercent = 0m,
        decimal periodicCriticalChancePercent = 0m)
    {
        var source = CreateActor(
            "player",
            "raid"
        );

        var target = CreateActor(
            "target",
            "enemy"
        );

        source.Stats.Set(
            ForeverCombatStatKeys.SpellCriticalChancePercent,
            generalCriticalChancePercent
        );

        source.Stats.Set(
            ForeverCombatStatKeys.NonPeriodicSpellCriticalChancePercent,
            nonPeriodicCriticalChancePercent
        );

        source.Stats.Set(
            ForeverCombatStatKeys.PeriodicSpellCriticalChancePercent,
            periodicCriticalChancePercent
        );

        var effect = new AbilityEffectDefinition
        {
            Key = "spell-effect",
            EffectType =
                AbilityEffectTypes.DirectDamage,
            TargetType =
                AbilityTargetTypes.Enemy,
            ResolutionType =
                ForeverCombatResolutionTypes.PlayerSpell,
            CanMiss = false,
            CanCrit = canCrit
        };

        var resolver =
            new RulesetCombatRollResolver(
                ForeverCombatRulesetFactory.Create(),
                new ForeverCombatRollContextAdjustmentProvider()
            );

        return resolver.Resolve(
            CreateContext(),
            source,
            target,
            CreateAbility(),
            effect,
            deliveryType
        );
    }

    private static SimulationContext CreateContext()
    {
        return new SimulationContext(
            new SimulationRunOptions
            {
                Seed = 12345,
                DurationSeconds = 1m,
                PrimaryActorKey = "player"
            }
        );
    }

    private static AbilityDefinition CreateAbility()
    {
        return new AbilityDefinition
        {
            Key = "spell",
            Name = "Spell"
        };
    }

    private static SimulationActorState CreateActor(
        string key,
        string teamKey)
    {
        var actor = new SimulationActorState
        {
            Key = key,
            Name = key,
            TeamKey = teamKey,
            Level = 30
        };

        actor.InitializeHealth(
            maximumHealth: 5000m
        );

        return actor;
    }
}
}
