using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests;

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
