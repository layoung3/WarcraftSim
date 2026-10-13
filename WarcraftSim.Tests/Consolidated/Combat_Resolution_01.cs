// Consolidated tests for Combat Resolution.
// Original class names are retained for xUnit test filtering.

// ==================== CombatAvoidanceFoundationTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

public sealed class CombatAvoidanceFoundationTests
{
    [Fact]
    public void RulesetResolver_DodgeOutcomeWhenEnabled()
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

        var resolver =
            CreateResolver(
                baseDodgeChancePercent:
                    100m
            );

        var result =
            resolver.Resolve(
                CreateContext(),
                source,
                target,
                CreateAbility(),
                CreateEffect(
                    canBeDodged:
                        true
                )
            );

        Assert.False(
            result.Landed
        );

        Assert.Equal(
            CombatResultTypes.Dodge,
            result.ResultKey
        );

        Assert.Equal(
            0m,
            result.AmountMultiplier
        );
    }

    [Fact]
    public void RulesetResolver_ParryOutcomeWhenEnabled()
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

        var resolver =
            CreateResolver(
                baseParryChancePercent:
                    100m
            );

        var result =
            resolver.Resolve(
                CreateContext(),
                source,
                target,
                CreateAbility(),
                CreateEffect(
                    canBeParried:
                        true
                )
            );

        Assert.False(
            result.Landed
        );

        Assert.Equal(
            CombatResultTypes.Parry,
            result.ResultKey
        );
    }

    [Fact]
    public void RulesetResolver_AvoidanceFlagMustBeEnabledOnEffect()
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

        var resolver =
            CreateResolver(
                baseDodgeChancePercent:
                    100m,
                baseParryChancePercent:
                    100m
            );

        var result =
            resolver.Resolve(
                CreateContext(),
                source,
                target,
                CreateAbility(),
                CreateEffect()
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
    public void RulesetResolver_TargetAndSourceStatsModifyDodgeChance()
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

        target.Stats.Set(
            "dodge",
            100m
        );

        source.Stats.Set(
            "expertise",
            100m
        );

        var resolver =
            CreateResolver(
                targetDodgeChanceStatKey:
                    "dodge",
                sourceDodgeReductionStatKey:
                    "expertise"
            );

        var result =
            resolver.Resolve(
                CreateContext(),
                source,
                target,
                CreateAbility(),
                CreateEffect(
                    canBeDodged:
                        true
                )
            );

        Assert.True(
            result.Landed
        );

        source.Stats.Set(
            "expertise",
            0m
        );

        result =
            resolver.Resolve(
                CreateContext(),
                source,
                target,
                CreateAbility(),
                CreateEffect(
                    canBeDodged:
                        true
                )
            );

        Assert.False(
            result.Landed
        );

        Assert.Equal(
            CombatResultTypes.Dodge,
            result.ResultKey
        );
    }

    [Fact]
    public void RulesetResolver_MissPrecedesDodgeInCombatTable()
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

        var resolver =
            CreateResolver(
                baseHitChancePercent:
                    0m,
                baseDodgeChancePercent:
                    100m
            );

        var result =
            resolver.Resolve(
                CreateContext(),
                source,
                target,
                CreateAbility(),
                CreateEffect(
                    canMiss:
                        true,
                    canBeDodged:
                        true
                )
            );

        Assert.Equal(
            CombatResultTypes.Miss,
            result.ResultKey
        );
    }

    [Fact]
    public void RulesetResolver_AvoidancePreventsCriticalResolution()
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

        var resolver =
            CreateResolver(
                baseDodgeChancePercent:
                    100m,
                baseCriticalChancePercent:
                    100m
            );

        var result =
            resolver.Resolve(
                CreateContext(),
                source,
                target,
                CreateAbility(),
                CreateEffect(
                    canBeDodged:
                        true,
                    canCrit:
                        true
                )
            );

        Assert.False(
            result.Landed
        );

        Assert.False(
            result.IsCritical
        );

        Assert.Equal(
            CombatResultTypes.Dodge,
            result.ResultKey
        );
    }

    [Fact]
    public void SimpleResolver_UsesSameOrderedAvoidanceSemantics()
    {
        var resolver =
            new SimpleCombatRollResolver(
                hitChancePercent:
                    100m,
                dodgeChancePercent:
                    0m,
                parryChancePercent:
                    100m,
                criticalChancePercent:
                    100m
            );

        var result =
            resolver.Resolve(
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
                    canBeParried:
                        true,
                    canCrit:
                        true
                )
            );

        Assert.Equal(
            CombatResultTypes.Parry,
            result.ResultKey
        );

        Assert.False(
            result.IsCritical
        );
    }

    [Fact]
    public void DodgedDependencyCondition_CanTriggerFollowupEffect()
    {
        var source =
            CreateActor(
                "attacker",
                "raid",
                resourceKey:
                    "rage",
                maximum:
                    100m,
                current:
                    0m
            );

        var target =
            CreateActor(
                "target",
                "enemy"
            );

        source.AddAbility(
            new AbilityDefinition
            {
                Key =
                    "dodge-reactive",

                Name =
                    "Dodge Reactive",

                IsOffGlobalCooldown =
                    true,

                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key =
                            "attack",

                        EffectType =
                            AbilityEffectTypes.DirectDamage,

                        TargetType =
                            AbilityTargetTypes.Enemy,

                        ResolutionType =
                            CombatResolutionTypes.Melee,

                        MitigationType =
                            DamageMitigationTypes.None,

                        CanMiss =
                            false,

                        CanBeDodged =
                            true,

                        CanCrit =
                            false,

                        MinimumValue =
                            100m,

                        MaximumValue =
                            100m
                    },

                    new AbilityEffectDefinition
                    {
                        Key =
                            "rage-on-dodge",

                        EffectType =
                            AbilityEffectTypes.ResourceChange,

                        TargetType =
                            AbilityTargetTypes.Self,

                        ResolutionType =
                            CombatResolutionTypes.AlwaysHits,

                        CanMiss =
                            false,

                        CanCrit =
                            false,

                        ResourceKey =
                            "rage",

                        ResourceChangeOperation =
                            ResourceChangeOperationTypes.Gain,

                        MinimumValue =
                            10m,

                        MaximumValue =
                            10m,

                        DependsOnEffectKey =
                            "attack",

                        DependencyCondition =
                            EffectDependencyConditions.Dodged
                    }
                ]
            }
        );

        var context =
            new SimulationContext(
                new SimulationRunOptions
                {
                    Seed =
                        12345,

                    DurationSeconds =
                        1m,

                    PrimaryActorKey =
                        source.Key,

                    CaptureTimeline =
                        true
                }
            );

        context.AddActor(
            source
        );

        context.AddActor(
            target
        );

        var ruleset =
            new CombatRulesetDefinition
            {
                RulesetKey =
                    "avoidance-tests",

                Version =
                    "1",

                RollRules =
                {
                    [CombatResolutionTypes.Melee] =
                        new CombatRollRuleDefinition
                        {
                            ResolutionType =
                                CombatResolutionTypes.Melee,

                            BaseHitChancePercent =
                                100m,

                            BaseDodgeChancePercent =
                                100m
                        }
                }
            };

        var executor =
            new AbilityExecutor(
                new AuraManager(),
                new RulesetCombatRollResolver(
                    ruleset
                ),
                new RulesetDamageMitigationResolver(
                    ruleset
                )
            );

        var result =
            new SimulationEngine(
                [
                    executor
                ])
                .Run(
                    context,
                    startedContext =>
                    {
                        Assert.True(
                            executor.TryStartAbility(
                                startedContext,
                                source.Key,
                                target.Key,
                                "dodge-reactive"
                            ).Success
                        );
                    }
                );

        Assert.Equal(
            10m,
            source.Resources["rage"].Current
        );

        var damage =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.Damage
            );

        Assert.Equal(
            CombatResultTypes.Dodge,
            damage.ResultKey
        );

        Assert.Equal(
            0m,
            damage.Amount
        );

        Assert.Contains(
            result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                    CombatEventType.ResourceChanged &&
                combatEvent.Amount ==
                    10m
        );
    }

    private static RulesetCombatRollResolver CreateResolver(
        decimal baseHitChancePercent = 100m,
        decimal baseDodgeChancePercent = 0m,
        decimal baseParryChancePercent = 0m,
        decimal baseCriticalChancePercent = 0m,
        string? targetDodgeChanceStatKey = null,
        string? sourceDodgeReductionStatKey = null)
    {
        var ruleset =
            new CombatRulesetDefinition
            {
                RulesetKey =
                    "avoidance-tests",

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

                BaseHitChancePercent =
                    baseHitChancePercent,

                BaseDodgeChancePercent =
                    baseDodgeChancePercent,

                TargetDodgeChanceStatKey =
                    targetDodgeChanceStatKey,

                SourceDodgeReductionStatKey =
                    sourceDodgeReductionStatKey,

                BaseParryChancePercent =
                    baseParryChancePercent,

                BaseCriticalChancePercent =
                    baseCriticalChancePercent,

                CriticalMultiplier =
                    2m
            };

        return new RulesetCombatRollResolver(
            ruleset
        );
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
        bool canMiss = false,
        bool canBeDodged = false,
        bool canBeParried = false,
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
                canMiss,

            CanBeDodged =
                canBeDodged,

            CanBeParried =
                canBeParried,

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
        string teamKey,
        string? resourceKey = null,
        decimal maximum = 0m,
        decimal current = 0m)
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
                    20
            };

        actor.InitializeHealth(
            maximumHealth:
                5000m
        );

        actor.ConfigureActionTiming(
            initialActionDelaySeconds:
                0m,

            inputDelaySeconds:
                0m
        );

        if (!string.IsNullOrWhiteSpace(
                resourceKey))
        {
            actor.AddResource(
                new ResourceState(
                    resourceKey,
                    maximum,
                    current
                )
            );
        }

        return actor;
    }
}
}

// ==================== CombatBlockFoundationTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

public sealed class CombatBlockFoundationTests
{
    [Fact]
    public void RulesetResolver_BlockOutcomeCarriesBlockValue()
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

        target.Stats.Set(
            "block-value",
            75m
        );

        var resolver =
            CreateResolver(
                baseBlockChancePercent:
                    100m,
                baseBlockValue:
                    25m,
                targetBlockValueStatKey:
                    "block-value"
            );

        var result =
            resolver.Resolve(
                CreateContext(),
                source,
                target,
                CreateAbility(),
                CreateEffect(
                    canBeBlocked:
                        true
                )
            );

        Assert.True(
            result.Landed
        );

        Assert.True(
            result.IsBlocked
        );

        Assert.False(
            result.IsCritical
        );

        Assert.Equal(
            CombatResultTypes.Block,
            result.ResultKey
        );

        Assert.Equal(
            100m,
            result.BlockValue
        );
    }

    [Fact]
    public void RulesetResolver_BlockRequiresEffectOptIn()
    {
        var resolver =
            CreateResolver(
                baseBlockChancePercent:
                    100m,
                baseBlockValue:
                    100m
            );

        var result =
            resolver.Resolve(
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
                CreateEffect()
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
    public void RulesetResolver_BlockChanceCanUseTargetAndSourceStats()
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

        target.Stats.Set(
            "block-chance",
            100m
        );

        source.Stats.Set(
            "block-reduction",
            100m
        );

        var resolver =
            CreateResolver(
                targetBlockChanceStatKey:
                    "block-chance",
                sourceBlockReductionStatKey:
                    "block-reduction"
            );

        var result =
            resolver.Resolve(
                CreateContext(),
                source,
                target,
                CreateAbility(),
                CreateEffect(
                    canBeBlocked:
                        true
                )
            );

        Assert.Equal(
            CombatResultTypes.Hit,
            result.ResultKey
        );

        source.Stats.Set(
            "block-reduction",
            0m
        );

        result =
            resolver.Resolve(
                CreateContext(),
                source,
                target,
                CreateAbility(),
                CreateEffect(
                    canBeBlocked:
                        true
                )
            );

        Assert.Equal(
            CombatResultTypes.Block,
            result.ResultKey
        );
    }

    [Fact]
    public void RulesetResolver_BlockPreventsCriticalResolution()
    {
        var resolver =
            CreateResolver(
                baseBlockChancePercent:
                    100m,
                baseBlockValue:
                    100m,
                baseCriticalChancePercent:
                    100m
            );

        var result =
            resolver.Resolve(
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

        Assert.False(
            result.IsCritical
        );
    }

    [Fact]
    public void AbilityDamage_BlockOccursAfterMitigationAndBeforeAbsorb()
    {
        var attacker =
            CreateActor(
                "attacker",
                "enemy"
            );

        var target =
            CreateActor(
                "tank",
                "raid"
            );

        attacker.AddAbility(
            CreateBlockingAttack(
                "strike",
                500m
            )
        );

        var context =
            CreateRunContext(
                attacker.Key
            );

        context.AddActor(
            attacker
        );

        context.AddActor(
            target
        );

        var absorbManager =
            new AbsorbManager();

        absorbManager.ApplyAbsorb(
            context,
            target,
            "shield",
            "Shield",
            50m,
            10m,
            AbsorbStackingMode.Refresh,
            1,
            target.Key,
            "shield",
            "shield-effect",
            scheduleExpiration:
                false
        );

        var executor =
            new AbilityExecutor(
                new AuraManager(),
                new SimpleCombatRollResolver(
                    blockChancePercent:
                        100m,
                    blockValue:
                        100m
                ),
                new FixedMitigationResolver(
                    finalAmount:
                        300m
                ),
                absorbManager:
                    absorbManager
            );

        var result =
            new SimulationEngine(
                [
                    executor
                ])
                .Run(
                    context,
                    startedContext =>
                    {
                        Assert.True(
                            executor.TryStartAbility(
                                startedContext,
                                attacker.Key,
                                target.Key,
                                "strike"
                            ).Success
                        );
                    }
                );

        Assert.Equal(
            4850m,
            target.CurrentHealth
        );

        var damage =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.Damage
            );

        Assert.Equal(
            500m,
            damage.RawAmount
        );

        Assert.Equal(
            200m,
            damage.MitigatedAmount
        );

        Assert.Equal(
            100m,
            damage.BlockedAmount
        );

        Assert.Equal(
            50m,
            damage.AbsorbedAmount
        );

        Assert.Equal(
            150m,
            damage.Amount
        );
    }

    [Fact]
    public void BlockAmountClampsToPostMitigationDamage()
    {
        var attacker =
            CreateActor(
                "attacker",
                "enemy"
            );

        var target =
            CreateActor(
                "tank",
                "raid"
            );

        attacker.AddAbility(
            CreateBlockingAttack(
                "small-hit",
                50m
            )
        );

        var result =
            RunBlockingAttack(
                attacker,
                target,
                blockValue:
                    500m
            );

        Assert.Equal(
            5000m,
            target.CurrentHealth
        );

        var damage =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.Damage
            );

        Assert.Equal(
            50m,
            damage.BlockedAmount
        );

        Assert.Equal(
            0m,
            damage.Amount
        );
    }

    [Fact]
    public void BlockedDependencyCondition_CanTriggerFollowupEffect()
    {
        var source =
            CreateActor(
                "attacker",
                "raid",
                resourceKey:
                    "rage",
                maximum:
                    100m,
                current:
                    0m
            );

        var target =
            CreateActor(
                "tank",
                "enemy"
            );

        source.AddAbility(
            new AbilityDefinition
            {
                Key =
                    "block-reactive",

                Name =
                    "Block Reactive",

                IsOffGlobalCooldown =
                    true,

                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key =
                            "attack",

                        EffectType =
                            AbilityEffectTypes.DirectDamage,

                        TargetType =
                            AbilityTargetTypes.Enemy,

                        ResolutionType =
                            CombatResolutionTypes.Melee,

                        MitigationType =
                            DamageMitigationTypes.None,

                        CanMiss =
                            false,

                        CanBeBlocked =
                            true,

                        CanCrit =
                            false,

                        MinimumValue =
                            100m,

                        MaximumValue =
                            100m
                    },

                    new AbilityEffectDefinition
                    {
                        Key =
                            "rage-on-block",

                        EffectType =
                            AbilityEffectTypes.ResourceChange,

                        TargetType =
                            AbilityTargetTypes.Self,

                        ResolutionType =
                            CombatResolutionTypes.AlwaysHits,

                        CanMiss =
                            false,

                        CanCrit =
                            false,

                        ResourceKey =
                            "rage",

                        ResourceChangeOperation =
                            ResourceChangeOperationTypes.Gain,

                        MinimumValue =
                            10m,

                        MaximumValue =
                            10m,

                        DependsOnEffectKey =
                            "attack",

                        DependencyCondition =
                            EffectDependencyConditions.Blocked
                    }
                ]
            }
        );

        var context =
            CreateRunContext(
                source.Key
            );

        context.AddActor(
            source
        );

        context.AddActor(
            target
        );

        var executor =
            new AbilityExecutor(
                new AuraManager(),
                new SimpleCombatRollResolver(
                    blockChancePercent:
                        100m,
                    blockValue:
                        25m
                ),
                new RulesetDamageMitigationResolver(
                    new CombatRulesetDefinition
                    {
                        RulesetKey =
                            "block-reactive-tests",

                        Version =
                            "1"
                    }
                )
            );

        new SimulationEngine(
            [
                executor
            ])
            .Run(
                context,
                startedContext =>
                {
                    Assert.True(
                        executor.TryStartAbility(
                            startedContext,
                            source.Key,
                            target.Key,
                            "block-reactive"
                        ).Success
                    );
                }
            );

        Assert.Equal(
            10m,
            source.Resources["rage"].Current
        );
    }

    [Fact]
    public void BlockedDamageUpdatesTargetAndPrimarySummary()
    {
        var attacker =
            CreateActor(
                "attacker",
                "enemy"
            );

        var target =
            CreateActor(
                "tank",
                "raid"
            );

        attacker.AddAbility(
            CreateBlockingAttack(
                "strike",
                200m
            )
        );

        var context =
            new SimulationContext(
                new SimulationRunOptions
                {
                    Seed =
                        12345,

                    DurationSeconds =
                        1m,

                    PrimaryActorKey =
                        target.Key,

                    CaptureTimeline =
                        true
                }
            );

        context.AddActor(
            attacker
        );

        context.AddActor(
            target
        );

        var executor =
            new AbilityExecutor(
                new AuraManager(),
                new SimpleCombatRollResolver(
                    blockChancePercent:
                        100m,
                    blockValue:
                        75m
                ),
                new RulesetDamageMitigationResolver(
                    new CombatRulesetDefinition
                    {
                        RulesetKey =
                            "block-summary-tests",

                        Version =
                            "1"
                    }
                )
            );

        var result =
            new SimulationEngine(
                [
                    executor
                ])
                .Run(
                    context,
                    startedContext =>
                    {
                        Assert.True(
                            executor.TryStartAbility(
                                startedContext,
                                attacker.Key,
                                target.Key,
                                "strike"
                            ).Success
                        );
                    }
                );

        Assert.Equal(
            75m,
            result.Summary.BlockedDamageReceived
        );

        Assert.Equal(
            75m,
            result.Summary.ActorSummaries[
                target.Key
            ].BlockedDamageReceived
        );

        Assert.Equal(
            75m,
            result.Summary.BlockedDamageReceivedByAbility[
                "strike"
            ]
        );
    }

    [Fact]
    public void SimpleResolver_UsesOrderedBlockSemantics()
    {
        var resolver =
            new SimpleCombatRollResolver(
                hitChancePercent:
                    100m,
                blockChancePercent:
                    100m,
                blockValue:
                    42m,
                criticalChancePercent:
                    100m
            );

        var result =
            resolver.Resolve(
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
            42m,
            result.BlockValue
        );

        Assert.False(
            result.IsCritical
        );
    }

    private static RulesetCombatRollResolver CreateResolver(
        decimal baseHitChancePercent = 100m,
        decimal baseBlockChancePercent = 0m,
        decimal baseBlockValue = 0m,
        decimal baseCriticalChancePercent = 0m,
        string? targetBlockChanceStatKey = null,
        string? sourceBlockReductionStatKey = null,
        string? targetBlockValueStatKey = null)
    {
        var ruleset =
            new CombatRulesetDefinition
            {
                RulesetKey =
                    "block-tests",

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

                BaseHitChancePercent =
                    baseHitChancePercent,

                BaseBlockChancePercent =
                    baseBlockChancePercent,

                TargetBlockChanceStatKey =
                    targetBlockChanceStatKey,

                SourceBlockReductionStatKey =
                    sourceBlockReductionStatKey,

                BaseBlockValue =
                    baseBlockValue,

                TargetBlockValueStatKey =
                    targetBlockValueStatKey,

                BaseCriticalChancePercent =
                    baseCriticalChancePercent,

                CriticalMultiplier =
                    2m
            };

        return new RulesetCombatRollResolver(
            ruleset
        );
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

            CanBeBlocked =
                canBeBlocked,

            CanCrit =
                canCrit
        };
    }

    private static AbilityDefinition CreateBlockingAttack(
        string key,
        decimal amount)
    {
        return new AbilityDefinition
        {
            Key =
                key,

            Name =
                key,

            IsOffGlobalCooldown =
                true,

            Effects =
            [
                new AbilityEffectDefinition
                {
                    Key =
                        $"{key}-effect",

                    EffectType =
                        AbilityEffectTypes.DirectDamage,

                    TargetType =
                        AbilityTargetTypes.Enemy,

                    ResolutionType =
                        CombatResolutionTypes.Melee,

                    MitigationType =
                        DamageMitigationTypes.None,

                    CanMiss =
                        false,

                    CanBeBlocked =
                        true,

                    CanCrit =
                        false,

                    MinimumValue =
                        amount,

                    MaximumValue =
                        amount
                }
            ]
        };
    }

    private static SimulationRunResult RunBlockingAttack(
        SimulationActorState attacker,
        SimulationActorState target,
        decimal blockValue)
    {
        var context =
            CreateRunContext(
                attacker.Key
            );

        context.AddActor(
            attacker
        );

        context.AddActor(
            target
        );

        var executor =
            new AbilityExecutor(
                new AuraManager(),
                new SimpleCombatRollResolver(
                    blockChancePercent:
                        100m,
                    blockValue:
                        blockValue
                ),
                new RulesetDamageMitigationResolver(
                    new CombatRulesetDefinition
                    {
                        RulesetKey =
                            "block-damage-tests",

                        Version =
                            "1"
                    }
                )
            );

        return new SimulationEngine(
            [
                executor
            ])
            .Run(
                context,
                startedContext =>
                {
                    Assert.True(
                        executor.TryStartAbility(
                            startedContext,
                            attacker.Key,
                            target.Key,
                            attacker.Abilities.Values
                                .Single()
                                .Definition.Key
                        ).Success
                    );
                }
            );
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

    private static SimulationContext CreateRunContext(
        string primaryActorKey)
    {
        return new SimulationContext(
            new SimulationRunOptions
            {
                Seed =
                    12345,

                DurationSeconds =
                    1m,

                PrimaryActorKey =
                    primaryActorKey,

                CaptureTimeline =
                    true
            }
        );
    }

    private static SimulationActorState CreateActor(
        string key,
        string teamKey,
        string? resourceKey = null,
        decimal maximum = 0m,
        decimal current = 0m)
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

        actor.ConfigureActionTiming(
            initialActionDelaySeconds:
                0m,

            inputDelaySeconds:
                0m
        );

        if (!string.IsNullOrWhiteSpace(
                resourceKey))
        {
            actor.AddResource(
                new ResourceState(
                    resourceKey,
                    maximum,
                    current
                )
            );
        }

        return actor;
    }

    private sealed class FixedMitigationResolver :
        IDamageMitigationResolver
    {
        private readonly decimal _finalAmount;

        public FixedMitigationResolver(
            decimal finalAmount)
        {
            _finalAmount =
                finalAmount;
        }

        public DamageMitigationResult Resolve(
            SimulationContext context,
            SimulationActorState source,
            SimulationActorState target,
            AbilityDefinition ability,
            AbilityEffectDefinition effect,
            decimal rawAmount)
        {
            var finalAmount =
                Math.Clamp(
                    _finalAmount,
                    0m,
                    rawAmount
                );

            var mitigated =
                rawAmount -
                finalAmount;

            return new DamageMitigationResult
            {
                RawAmount =
                    rawAmount,

                FinalAmount =
                    finalAmount,

                MitigatedAmount =
                    mitigated,

                ReductionPercent =
                    rawAmount <= 0m
                        ? 0m
                        : mitigated /
                          rawAmount *
                          100m
            };
        }
    }
}
}

// ==================== CombatEffectDeliveryClassificationTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Simulation.Engine;

public sealed class CombatEffectDeliveryClassificationTests
{
    [Theory]
    [InlineData(
        CombatEffectDeliveryType.Direct,
        false,
        false)]
    [InlineData(
        CombatEffectDeliveryType.Periodic,
        true,
        false)]
    [InlineData(
        CombatEffectDeliveryType.ChannelTick,
        false,
        true)]
    public void CombatEvent_DeliveryTypeExposesExpectedClassification(
        CombatEffectDeliveryType deliveryType,
        bool expectedPeriodic,
        bool expectedChannelTick)
    {
        var combatEvent =
            new CombatEvent
            {
                EffectDeliveryType =
                    deliveryType
            };

        Assert.Equal(
            expectedPeriodic,
            combatEvent.IsPeriodic
        );

        Assert.Equal(
            expectedChannelTick,
            combatEvent.IsChannelTick
        );
    }
}
}

// ==================== CombatEventTypeContractTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Simulation.Engine;

public sealed class CombatEventTypeContractTests
{
    [Fact]
    public void NumericValues_RemainStable()
    {
        var expected =
            new Dictionary<CombatEventType, int>
            {
                [CombatEventType.SimulationStarted] =
                    0,

                [CombatEventType.SimulationEnded] =
                    1,

                [CombatEventType.RotationDecision] =
                    2,

                [CombatEventType.AbilityCastStarted] =
                    3,

                [CombatEventType.AbilityCastCompleted] =
                    4,

                [CombatEventType.AbilityEffectImpact] =
                    5,

                [CombatEventType.Damage] =
                    6,

                [CombatEventType.Healing] =
                    7,

                [CombatEventType.ResourceChanged] =
                    8,

                [CombatEventType.AuraApplied] =
                    9,

                [CombatEventType.AuraRemoved] =
                    10,

                [CombatEventType.AuraExpiration] =
                    11,

                [CombatEventType.PeriodicTick] =
                    12,

                [CombatEventType.ActorDied] =
                    13,

                [CombatEventType.EncounterPhaseStarted] =
                    14,

                [CombatEventType.EncounterPhaseEnded] =
                    15,

                [CombatEventType.AbilityCastCancelled] =
                    16,

                [CombatEventType.ScriptedDamage] =
                    17,

                [CombatEventType.EncounterDamagePatternOccurrence] =
                    18,

                [CombatEventType.EncounterDamageSequenceHit] =
                    19,

                [CombatEventType.ThreatChanged] =
                    20,

                [CombatEventType.ForcedTargetApplied] =
                    21,

                [CombatEventType.ForcedTargetRemoved] =
                    22,

                [CombatEventType.ForcedTargetExpiration] =
                    23,

                [CombatEventType.AbsorbApplied] =
                    24,

                [CombatEventType.AbsorbConsumed] =
                    25,

                [CombatEventType.AbsorbRemoved] =
                    26,

                [CombatEventType.AbsorbExpiration] =
                    27,

                [CombatEventType.AbilityChannelStarted] =
                    28,

                [CombatEventType.AbilityChannelTick] =
                    29,

                [CombatEventType.AbilityChannelCompleted] =
                    30,

                [CombatEventType.AbilityChannelCancelled] =
                    31,

                [CombatEventType.AutoAttackStarted] =
                    32,

                [CombatEventType.AutoAttackSwing] =
                    33,

                [CombatEventType.AutoAttackStopped] =
                    34,

                [CombatEventType.HealthChanged] =
                    35,

                // CP105: appended without changing existing serialized IDs.
                [CombatEventType.ReactiveOpportunityGranted] =
                    36
            };

        Assert.Equal(
            expected.Count,
            Enum.GetValues<CombatEventType>().Length
        );

        foreach (
            var expectedValue in
            expected)
        {
            Assert.Equal(
                expectedValue.Value,
                (int)expectedValue.Key
            );
        }
    }
}
}

// ==================== CombatRollDeliveryPropagationTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

public sealed class CombatRollDeliveryPropagationTests
{
    [Fact]
    public void PeriodicTicks_ForwardPeriodicDeliveryToCombatResolver()
    {
        var source = CreateActor("caster", "raid");
        var target = CreateActor("target", "enemy");

        source.AddAbility(
            new AbilityDefinition
            {
                Key = "dot",
                Name = "dot",
                IsOffGlobalCooldown = true,
                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key = "dot-tick",
                        EffectType =
                            AbilityEffectTypes.PeriodicDamage,
                        TargetType =
                            AbilityTargetTypes.Enemy,
                        ResolutionType =
                            CombatResolutionTypes.AlwaysHits,
                        MitigationType =
                            DamageMitigationTypes.None,
                        CanMiss = false,
                        CanCrit = false,
                        DurationSeconds = 2m,
                        TickIntervalSeconds = 1m,
                        MinimumValue = 1m,
                        MaximumValue = 1m
                    }
                ]
            }
        );

        var recorder = new RecordingCombatRollResolver();
        var auraManager = new AuraManager();
        var executor = CreateExecutor(
            auraManager,
            recorder
        );

        RunAbility(
            source,
            target,
            durationSeconds: 2m,
            executor,
            auraManager
        );

        Assert.NotEmpty(recorder.Deliveries);
        Assert.All(
            recorder.Deliveries,
            delivery => Assert.Equal(
                CombatEffectDeliveryType.Periodic,
                delivery
            )
        );
    }

    [Fact]
    public void ChannelTicks_ForwardChannelTickDeliveryToCombatResolver()
    {
        var source = CreateActor("caster", "raid");
        var target = CreateActor("target", "enemy");

        source.AddAbility(
            new AbilityDefinition
            {
                Key = "channel",
                Name = "channel",
                IsOffGlobalCooldown = true,
                ChannelDurationSeconds = 2m,
                ChannelTickIntervalSeconds = 1m,
                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key = "channel-tick",
                        EffectType =
                            AbilityEffectTypes.DirectDamage,
                        TargetType =
                            AbilityTargetTypes.Enemy,
                        ResolutionType =
                            CombatResolutionTypes.AlwaysHits,
                        MitigationType =
                            DamageMitigationTypes.None,
                        CanMiss = false,
                        CanCrit = false,
                        MinimumValue = 1m,
                        MaximumValue = 1m,
                        ApplyOnChannelTick = true
                    }
                ]
            }
        );

        var recorder = new RecordingCombatRollResolver();
        var auraManager = new AuraManager();
        var executor = CreateExecutor(
            auraManager,
            recorder
        );

        RunAbility(
            source,
            target,
            durationSeconds: 3m,
            executor,
            auraManager
        );

        Assert.NotEmpty(recorder.Deliveries);
        Assert.All(
            recorder.Deliveries,
            delivery => Assert.Equal(
                CombatEffectDeliveryType.ChannelTick,
                delivery
            )
        );
    }

    private static void RunAbility(
        SimulationActorState source,
        SimulationActorState target,
        decimal durationSeconds,
        AbilityExecutor executor,
        AuraManager auraManager)
    {
        var context = new SimulationContext(
            new SimulationRunOptions
            {
                Seed = 12345,
                DurationSeconds = durationSeconds,
                PrimaryActorKey = source.Key,
                CaptureTimeline = true
            }
        );

        context.AddActor(source);
        context.AddActor(target);

        new SimulationEngine(
            [
                executor,
                auraManager
            ])
            .Run(
                context,
                startedContext =>
                {
                    Assert.True(
                        executor.TryStartAbility(
                            startedContext,
                            source.Key,
                            target.Key,
                            source.Abilities.Values
                                .Single()
                                .Definition.Key
                        ).Success
                    );
                }
            );
    }

    private static AbilityExecutor CreateExecutor(
        AuraManager auraManager,
        ICombatRollResolver resolver)
    {
        return new AbilityExecutor(
            auraManager,
            resolver,
            new RulesetDamageMitigationResolver(
                new CombatRulesetDefinition
                {
                    RulesetKey = "delivery-tests",
                    Version = "1"
                }
            )
        );
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

        actor.ConfigureActionTiming(
            initialActionDelaySeconds: 0m,
            inputDelaySeconds: 0m
        );

        return actor;
    }

    private sealed class RecordingCombatRollResolver :
        ICombatRollResolver
    {
        public List<CombatEffectDeliveryType> Deliveries { get; } = [];

        public CombatRollResult Resolve(
            SimulationContext context,
            SimulationActorState source,
            SimulationActorState target,
            AbilityDefinition ability,
            AbilityEffectDefinition effect)
        {
            Deliveries.Add(
                CombatEffectDeliveryType.Direct
            );

            return CombatRollResult.Hit();
        }

        public CombatRollResult Resolve(
            SimulationContext context,
            SimulationActorState source,
            SimulationActorState target,
            AbilityDefinition ability,
            AbilityEffectDefinition effect,
            CombatEffectDeliveryType deliveryType)
        {
            Deliveries.Add(deliveryType);

            return CombatRollResult.Hit();
        }
    }
}
}
