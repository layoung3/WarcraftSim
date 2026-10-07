using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests;

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
