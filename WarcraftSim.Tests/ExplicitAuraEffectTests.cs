using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Validation;

namespace WarcraftSim.Tests;

public sealed class ExplicitAuraEffectTests
{
    [Fact]
    public void ApplyAuraEffect_AppliesConfiguredAuraAndEmitsEvent()
    {
        var source =
            CreateActor(
                "source",
                "raid"
            );

        var target =
            CreateActor(
                "target",
                "enemy"
            );

        source.AddAbility(
            CreateApplyAuraAbility(
                "mark",
                auraKey:
                    "marked",
                durationSeconds:
                    5m
            )
        );

        var run =
            RunAbility(
                source,
                target,
                durationSeconds:
                    1m
            );

        var aura =
            Assert.Single(
                target.ActiveAuras
            );

        Assert.Equal(
            "marked",
            aura.Definition.Key
        );

        Assert.Equal(
            5m,
            aura.ExpiresAtSeconds
        );

        Assert.Equal(
            source.Key,
            aura.SourceActorKey
        );

        var applied =
            Assert.Single(
                run.Result.Timeline,
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.AuraApplied
            );

        Assert.Equal(
            source.Key,
            applied.SourceActorKey
        );

        Assert.Equal(
            target.Key,
            applied.TargetActorKey
        );

        Assert.Equal(
            "mark",
            applied.AbilityKey
        );
    }

    [Fact]
    public void ApplyAuraEffect_ExpiresThroughAuraManager()
    {
        var source =
            CreateActor(
                "source",
                "raid"
            );

        var target =
            CreateActor(
                "target",
                "enemy"
            );

        source.AddAbility(
            CreateApplyAuraAbility(
                "short-mark",
                auraKey:
                    "short-marked",
                durationSeconds:
                    2m
            )
        );

        var run =
            RunAbility(
                source,
                target,
                durationSeconds:
                    4m
            );

        Assert.Empty(
            target.ActiveAuras
        );

        Assert.Single(
            run.Result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                    CombatEventType.AuraApplied
        );

        Assert.Single(
            run.Result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                    CombatEventType.AuraRemoved
        );

        Assert.DoesNotContain(
            run.Result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                    CombatEventType.AuraExpiration
        );
    }

    [Fact]
    public void ApplyAuraEffect_CanApplyToMultipleTargets()
    {
        var source =
            CreateActor(
                "source",
                "raid"
            );

        var boss =
            CreateActor(
                "boss",
                "enemy"
            );

        var addA =
            CreateActor(
                "a-add",
                "enemy"
            );

        var addZ =
            CreateActor(
                "z-add",
                "enemy"
            );

        source.AddAbility(
            CreateApplyAuraAbility(
                "multi-mark",
                auraKey:
                    "marked",
                durationSeconds:
                    5m,
                maxTargets:
                    2
            )
        );

        RunAbility(
            source,
            boss,
            [
                addZ,
                addA
            ],
            durationSeconds:
                1m
        );

        Assert.Single(
            boss.ActiveAuras
        );

        Assert.Single(
            addA.ActiveAuras
        );

        Assert.Empty(
            addZ.ActiveAuras
        );
    }

    [Fact]
    public void RemoveAuraEffect_RemovesMatchingAuraFromAnySource()
    {
        var source =
            CreateActor(
                "dispel-source",
                "raid"
            );

        var otherSource =
            CreateActor(
                "other-source",
                "raid"
            );

        var target =
            CreateActor(
                "target",
                "raid"
            );

        source.AddAbility(
            CreateRemoveAuraAbility(
                "cleanse",
                auraKey:
                    "poison"
            )
        );

        var run =
            RunAbility(
                source,
                target,
                [
                    otherSource
                ],
                durationSeconds:
                    1m,
                beforeAbility:
                    (context, auraManager) =>
                    {
                        auraManager.ApplyAura(
                            context,
                            target,
                            CreateAura(
                                "poison"
                            ),
                            otherSource.Key,
                            abilityKey:
                                "poison-source",
                            effectKey:
                                "poison-effect",
                            scheduleExpiration:
                                false
                        );
                    }
            );

        Assert.Empty(
            target.ActiveAuras
        );

        Assert.Single(
            run.Result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                    CombatEventType.AuraRemoved
        );
    }

    [Fact]
    public void RemoveAuraEffect_SourceOnlyPreservesOtherSourceAura()
    {
        var source =
            CreateActor(
                "source",
                "raid"
            );

        var otherSource =
            CreateActor(
                "other-source",
                "raid"
            );

        var target =
            CreateActor(
                "target",
                "raid"
            );

        source.AddAbility(
            CreateRemoveAuraAbility(
                "remove-own-mark",
                auraKey:
                    "mark",
                removeOnlyFromSource:
                    true
            )
        );

        RunAbility(
            source,
            target,
            [
                otherSource
            ],
            durationSeconds:
                1m,
            beforeAbility:
                (context, auraManager) =>
                {
                    auraManager.ApplyAura(
                        context,
                        target,
                        CreateAura(
                            "mark"
                        ),
                        source.Key,
                        abilityKey:
                            "source-mark",
                        effectKey:
                            "source-mark-effect",
                        scheduleExpiration:
                            false
                    );

                    auraManager.ApplyAura(
                        context,
                        target,
                        CreateAura(
                            "mark"
                        ),
                        otherSource.Key,
                        abilityKey:
                            "other-mark",
                        effectKey:
                            "other-mark-effect",
                        scheduleExpiration:
                            false
                    );
                }
        );

        var remaining =
            Assert.Single(
                target.ActiveAuras
            );

        Assert.Equal(
            otherSource.Key,
            remaining.SourceActorKey
        );
    }

    [Fact]
    public void ApplyAuraEffect_MissDoesNotApplyAura()
    {
        var source =
            CreateActor(
                "source",
                "raid"
            );

        var target =
            CreateActor(
                "target",
                "enemy"
            );

        source.AddAbility(
            CreateApplyAuraAbility(
                "missed-mark",
                auraKey:
                    "marked",
                durationSeconds:
                    5m
            )
        );

        var run =
            RunAbility(
                source,
                target,
                durationSeconds:
                    1m,
                combatRollResolver:
                    new SimpleCombatRollResolver(
                        hitChancePercent:
                            0m
                    )
            );

        Assert.Empty(
            target.ActiveAuras
        );

        Assert.DoesNotContain(
            run.Result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                    CombatEventType.AuraApplied
        );
    }

    [Fact]
    public void Validator_RejectsInvalidExplicitAuraEffects()
    {
        var source =
            CreateActor(
                "source",
                "raid"
            );

        source.AddAbility(
            new AbilityDefinition
            {
                Key =
                    "invalid-auras",

                Name =
                    "Invalid Auras",

                IsOffGlobalCooldown =
                    true,

                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key =
                            "invalid-apply",

                        EffectType =
                            AbilityEffectTypes.ApplyAura,

                        TargetType =
                            AbilityTargetTypes.Self,

                        DurationSeconds =
                            0m,

                        MaxStacks =
                            0
                    },

                    new AbilityEffectDefinition
                    {
                        Key =
                            "invalid-remove",

                        EffectType =
                            AbilityEffectTypes.RemoveAura,

                        TargetType =
                            AbilityTargetTypes.Self
                    }
                ]
            }
        );

        var context =
            CreateContext(
                source.Key,
                durationSeconds:
                    1m
            );

        context.AddActor(
            source
        );

        var exception =
            Assert.Throws<SimulationDefinitionValidationException>(
                () =>
                    new SimulationEngine()
                        .Run(
                            context
                        )
            );

        Assert.Contains(
            exception.Errors,
            error =>
                error.Contains(
                    "apply-aura effect 'invalid-apply' requires AuraKey",
                    StringComparison.OrdinalIgnoreCase
                )
        );

        Assert.Contains(
            exception.Errors,
            error =>
                error.Contains(
                    "DurationSeconds greater than zero",
                    StringComparison.OrdinalIgnoreCase
                )
        );

        Assert.Contains(
            exception.Errors,
            error =>
                error.Contains(
                    "MaxStacks to be at least 1",
                    StringComparison.OrdinalIgnoreCase
                )
        );

        Assert.Contains(
            exception.Errors,
            error =>
                error.Contains(
                    "remove-aura effect 'invalid-remove' requires AuraKey",
                    StringComparison.OrdinalIgnoreCase
                )
        );
    }

    private static AbilityDefinition CreateApplyAuraAbility(
        string key,
        string auraKey,
        decimal durationSeconds,
        int maxTargets = 1)
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
                        AbilityEffectTypes.ApplyAura,

                    TargetType =
                        AbilityTargetTypes.Enemy,

                    ResolutionType =
                        CombatResolutionTypes.Spell,

                    CanMiss =
                        true,

                    CanCrit =
                        false,

                    AuraKey =
                        auraKey,

                    DurationSeconds =
                        durationSeconds,

                    AuraStackingMode =
                        AuraStackingMode.Refresh,

                    MaxStacks =
                        1,

                    MaxTargets =
                        maxTargets
                }
            ]
        };
    }

    private static AbilityDefinition CreateRemoveAuraAbility(
        string key,
        string auraKey,
        bool removeOnlyFromSource = false)
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
                        AbilityEffectTypes.RemoveAura,

                    TargetType =
                        AbilityTargetTypes.Friendly,

                    ResolutionType =
                        CombatResolutionTypes.AlwaysHits,

                    CanMiss =
                        false,

                    CanCrit =
                        false,

                    AuraKey =
                        auraKey,

                    RemoveAuraOnlyFromSource =
                        removeOnlyFromSource
                }
            ]
        };
    }

    private static AuraDefinition CreateAura(
        string key)
    {
        return new AuraDefinition
        {
            Key =
                key,

            Name =
                key,

            DurationSeconds =
                30m,

            StackingMode =
                AuraStackingMode.Refresh,

            MaxStacks =
                1
        };
    }

    private static AbilityRunResult RunAbility(
        SimulationActorState source,
        SimulationActorState primaryTarget,
        IReadOnlyList<SimulationActorState>? additionalActors = null,
        decimal durationSeconds = 1m,
        ICombatRollResolver? combatRollResolver = null,
        Action<SimulationContext, AuraManager>? beforeAbility = null)
    {
        var context =
            CreateContext(
                source.Key,
                durationSeconds,
                captureTimeline:
                    true
            );

        context.AddActor(
            source
        );

        context.AddActor(
            primaryTarget
        );

        foreach (
            var actor in
            additionalActors ??
            [])
        {
            context.AddActor(
                actor
            );
        }

        var auraManager =
            new AuraManager();

        var executor =
            new AbilityExecutor(
                auraManager,
                combatRollResolver ??
                new SimpleCombatRollResolver(),
                new RulesetDamageMitigationResolver(
                    new CombatRulesetDefinition
                    {
                        RulesetKey =
                            "explicit-aura-tests",

                        Version =
                            "1"
                    }
                )
            );

        var result =
            new SimulationEngine(
                [
                    executor,
                    auraManager
                ])
                .Run(
                    context,
                    startedContext =>
                    {
                        beforeAbility?.Invoke(
                            startedContext,
                            auraManager
                        );

                        var useResult =
                            executor.TryStartAbility(
                                startedContext,
                                source.Key,
                                primaryTarget.Key,
                                source.Abilities.Values
                                    .Single()
                                    .Definition.Key
                            );

                        Assert.True(
                            useResult.Success
                        );
                    }
                );

        return new AbilityRunResult(
            context,
            result
        );
    }

    private static SimulationContext CreateContext(
        string primaryActorKey,
        decimal durationSeconds,
        bool captureTimeline = false)
    {
        return new SimulationContext(
            new SimulationRunOptions
            {
                Seed =
                    12345,

                DurationSeconds =
                    durationSeconds,

                PrimaryActorKey =
                    primaryActorKey,

                CaptureTimeline =
                    captureTimeline
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
                    teamKey
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

        return actor;
    }

    private sealed record AbilityRunResult(
        SimulationContext Context,
        SimulationRunResult Result);
}
