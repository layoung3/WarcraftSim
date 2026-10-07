using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Encounters;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Validation;

namespace WarcraftSim.Tests;

public sealed class AbsorbShieldFoundationTests
{
    [Fact]
    public void AbsorbEffect_AppliesShieldAndEmitsEvent()
    {
        var source =
            CreateActor(
                "priest",
                "raid"
            );

        var target =
            CreateActor(
                "tank",
                "raid"
            );

        source.AddAbility(
            CreateAbsorbAbility(
                "shield",
                amount:
                    500m,
                durationSeconds:
                    10m
            )
        );

        var result =
            RunAbility(
                source,
                target,
                durationSeconds:
                    1m
            );

        var absorb =
            Assert.Single(
                target.ActiveAbsorbs
            );

        Assert.Equal(
            "shield-absorb",
            absorb.AbsorbKey
        );

        Assert.Equal(
            500m,
            absorb.RemainingAmount
        );

        var applied =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.AbsorbApplied
            );

        Assert.Equal(
            500m,
            applied.Amount
        );
    }

    [Fact]
    public void AbilityDamage_ConsumesAbsorbBeforeHealth()
    {
        var attacker =
            CreateActor(
                "attacker",
                "enemy"
            );

        var target =
            CreateActor(
                "target",
                "raid"
            );

        var absorbManager =
            new AbsorbManager();

        absorbManager.ApplyAbsorb(
            CreateStandaloneContext(
                target.Key
            ),
            target,
            "shield",
            "Shield",
            500m,
            10m,
            AbsorbStackingMode.Refresh,
            1,
            target.Key,
            "shield",
            "shield-effect",
            scheduleExpiration:
                false
        );

        attacker.AddAbility(
            CreateDamageAbility(
                "strike",
                300m
            )
        );

        var result =
            RunAbility(
                attacker,
                target,
                durationSeconds:
                    1m
            );

        Assert.Equal(
            target.MaximumHealth,
            target.CurrentHealth
        );

        var absorb =
            Assert.Single(
                target.ActiveAbsorbs
            );

        Assert.Equal(
            200m,
            absorb.RemainingAmount
        );

        var damage =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.Damage
            );

        Assert.Equal(
            300m,
            damage.AbsorbedAmount
        );

        Assert.Equal(
            0m,
            damage.Amount
        );
    }

    [Fact]
    public void AbilityDamage_OverflowDamagesHealthAndDepletesShield()
    {
        var attacker =
            CreateActor(
                "attacker",
                "enemy"
            );

        var target =
            CreateActor(
                "target",
                "raid"
            );

        var context =
            CreateStandaloneContext(
                target.Key
            );

        new AbsorbManager()
            .ApplyAbsorb(
                context,
                target,
                "shield",
                "Shield",
                200m,
                10m,
                AbsorbStackingMode.Refresh,
                1,
                target.Key,
                "shield",
                "shield-effect",
                scheduleExpiration:
                    false
            );

        attacker.AddAbility(
            CreateDamageAbility(
                "strike",
                500m
            )
        );

        var result =
            RunAbility(
                attacker,
                target,
                durationSeconds:
                    1m
            );

        Assert.Equal(
            4700m,
            target.CurrentHealth
        );

        Assert.Empty(
            target.ActiveAbsorbs
        );

        var damage =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.Damage
            );

        Assert.Equal(
            200m,
            damage.AbsorbedAmount
        );

        Assert.Equal(
            300m,
            damage.Amount
        );

        Assert.Single(
            result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                    CombatEventType.AbsorbRemoved
        );
    }

    [Fact]
    public void AbsorbEffect_ExpiresUnusedShield()
    {
        var source =
            CreateActor(
                "priest",
                "raid"
            );

        var target =
            CreateActor(
                "tank",
                "raid"
            );

        source.AddAbility(
            CreateAbsorbAbility(
                "short-shield",
                amount:
                    500m,
                durationSeconds:
                    2m
            )
        );

        var result =
            RunAbility(
                source,
                target,
                durationSeconds:
                    4m
            );

        Assert.Empty(
            target.ActiveAbsorbs
        );

        Assert.Single(
            result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                    CombatEventType.AbsorbRemoved
        );

        Assert.DoesNotContain(
            result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                    CombatEventType.AbsorbExpiration
        );
    }

    [Fact]
    public void AbsorbEffect_RefreshReplacesRemainingCapacity()
    {
        var context =
            CreateStandaloneContext(
                "target"
            );

        var target =
            CreateActor(
                "target",
                "raid"
            );

        context.AddActor(
            target
        );

        var manager =
            new AbsorbManager();

        manager.ApplyAbsorb(
            context,
            target,
            "shield",
            "Shield",
            500m,
            10m,
            AbsorbStackingMode.Refresh,
            1,
            "source",
            "shield",
            "shield-effect",
            scheduleExpiration:
                false
        );

        manager.ApplyDamage(
            context,
            target,
            300m
        );

        manager.ApplyAbsorb(
            context,
            target,
            "shield",
            "Shield",
            400m,
            10m,
            AbsorbStackingMode.Refresh,
            1,
            "source",
            "shield",
            "shield-effect",
            scheduleExpiration:
                false
        );

        var absorb =
            Assert.Single(
                target.ActiveAbsorbs
            );

        Assert.Equal(
            400m,
            absorb.RemainingAmount
        );

        Assert.Equal(
            400m,
            absorb.MaximumAmount
        );
    }

    [Fact]
    public void AbsorbEffect_StackAddsCapacityOnlyUntilMaxStacks()
    {
        var context =
            CreateStandaloneContext(
                "target"
            );

        var target =
            CreateActor(
                "target",
                "raid"
            );

        context.AddActor(
            target
        );

        var manager =
            new AbsorbManager();

        for (
            var index = 0;
            index < 3;
            index++)
        {
            manager.ApplyAbsorb(
                context,
                target,
                "stacking-shield",
                "Stacking Shield",
                100m,
                10m,
                AbsorbStackingMode.Stack,
                2,
                "source",
                "stacking-shield",
                "stacking-effect",
                scheduleExpiration:
                    false
            );
        }

        var absorb =
            Assert.Single(
                target.ActiveAbsorbs
            );

        Assert.Equal(
            2,
            absorb.Stacks
        );

        Assert.Equal(
            200m,
            absorb.RemainingAmount
        );

        Assert.Equal(
            200m,
            absorb.MaximumAmount
        );
    }

    [Fact]
    public void ScriptedEncounterDamage_RespectsAbsorbShield()
    {
        var encounter =
            new EncounterProfile
            {
                Name =
                    "Absorb Encounter",

                RulesetKey =
                    "development",

                DurationSeconds =
                    2m,

                DamagePatterns =
                [
                    new EncounterDamagePatternDefinition
                    {
                        Key =
                            "hit",

                        Name =
                            "Hit",

                        StartTimeSeconds =
                            1m,

                        EndTimeSeconds =
                            1m,

                        IntervalSeconds =
                            1m,

                        SourceActorKey =
                            "boss",

                        TargetSelection =
                            new EncounterTargetSelectionDefinition
                            {
                                Mode =
                                    EncounterTargetSelectionModes.FixedActor,

                                ActorKey =
                                    "tank"
                            },

                        Amount =
                            400m,

                        SchoolKey =
                            "physical"
                    }
                ]
            };

        var context =
            new SimulationContext(
                new SimulationRunOptions
                {
                    Seed =
                        12345,

                    DurationSeconds =
                        2m,

                    PrimaryActorKey =
                        "tank",

                    CaptureTimeline =
                        true
                },
                encounter
            );

        var boss =
            CreateActor(
                "boss",
                "enemy"
            );

        var tank =
            CreateActor(
                "tank",
                "raid"
            );

        context.AddActor(
            boss
        );

        context.AddActor(
            tank
        );

        var absorbManager =
            new AbsorbManager();

        absorbManager.ApplyAbsorb(
            context,
            tank,
            "shield",
            "Shield",
            250m,
            5m,
            AbsorbStackingMode.Refresh,
            1,
            tank.Key,
            "shield",
            "shield-effect",
            scheduleExpiration:
                true
        );

        var mitigationResolver =
            new RulesetDamageMitigationResolver(
                new CombatRulesetDefinition
                {
                    RulesetKey =
                        "absorb-encounter",

                    Version =
                        "1"
                }
            );

        var result =
            new SimulationEngine(
                [
                    new EncounterTimelineProcessor(),

                    new ScriptedEncounterDamageProcessor(
                        mitigationResolver,
                        absorbManager
                    ),

                    absorbManager
                ]
            )
            .Run(
                context
            );

        Assert.Equal(
            4850m,
            tank.CurrentHealth
        );

        var damage =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.Damage
            );

        Assert.Equal(
            250m,
            damage.AbsorbedAmount
        );

        Assert.Equal(
            150m,
            damage.Amount
        );
    }

    [Fact]
    public void AbsorbConsumption_UpdatesCombatSummary()
    {
        var shieldSource =
            CreateActor(
                "priest",
                "raid"
            );

        var target =
            CreateActor(
                "tank",
                "raid"
            );

        var attacker =
            CreateActor(
                "boss",
                "enemy"
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
                        shieldSource.Key,

                    CaptureTimeline =
                        true
                }
            );

        context.AddActor(
            shieldSource
        );

        context.AddActor(
            target
        );

        context.AddActor(
            attacker
        );

        var absorbManager =
            new AbsorbManager();

        absorbManager.ApplyAbsorb(
            context,
            target,
            "shield",
            "Shield",
            200m,
            5m,
            AbsorbStackingMode.Refresh,
            1,
            shieldSource.Key,
            "shield-spell",
            "shield-effect",
            scheduleExpiration:
                false
        );

        attacker.AddAbility(
            CreateDamageAbility(
                "strike",
                150m
            )
        );

        var executor =
            CreateExecutor(
                absorbManager:
                    absorbManager
            );

        var result =
            new SimulationEngine(
                [
                    executor
                ]
            )
            .Run(
                context,
                startedContext =>
                {
                    var useResult =
                        executor.TryStartAbility(
                            startedContext,
                            attacker.Key,
                            target.Key,
                            "strike"
                        );

                    Assert.True(
                        useResult.Success
                    );
                }
            );

        Assert.Equal(
            150m,
            result.Summary.AbsorptionDone
        );

        Assert.Equal(
            150m,
            result.Summary.ActorSummaries[
                shieldSource.Key
            ].AbsorptionDone
        );

        Assert.Equal(
            150m,
            result.Summary.ActorSummaries[
                target.Key
            ].AbsorptionReceived
        );

        Assert.Equal(
            150m,
            result.Summary.AbsorptionDoneByAbility[
                "shield-spell"
            ]
        );
    }

    [Fact]
    public void Validator_RejectsInvalidAbsorbDefinition()
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
                    "invalid-absorb",

                Name =
                    "Invalid Absorb",

                IsOffGlobalCooldown =
                    true,

                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key =
                            "invalid-absorb-effect",

                        EffectType =
                            AbilityEffectTypes.Absorb,

                        TargetType =
                            AbilityTargetTypes.Self,

                        DurationSeconds =
                            0m,

                        MaxStacks =
                            0,

                        MinimumValue =
                            -1m,

                        MaximumValue =
                            -1m
                    }
                ]
            }
        );

        var context =
            CreateStandaloneContext(
                source.Key
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
                    "requires AbsorbKey",
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
                    "requires non-negative effect values",
                    StringComparison.OrdinalIgnoreCase
                )
        );
    }

    private static AbilityDefinition CreateAbsorbAbility(
        string key,
        decimal amount,
        decimal durationSeconds)
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
                        AbilityEffectTypes.Absorb,

                    TargetType =
                        AbilityTargetTypes.Friendly,

                    ResolutionType =
                        CombatResolutionTypes.Healing,

                    CanMiss =
                        false,

                    CanCrit =
                        false,

                    AbsorbKey =
                        $"{key}-absorb",

                    AbsorbStackingMode =
                        AbsorbStackingMode.Refresh,

                    DurationSeconds =
                        durationSeconds,

                    MinimumValue =
                        amount,

                    MaximumValue =
                        amount,

                    MaxStacks =
                        1
                }
            ]
        };
    }

    private static AbilityDefinition CreateDamageAbility(
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
                        CombatResolutionTypes.AlwaysHits,

                    MitigationType =
                        DamageMitigationTypes.None,

                    CanMiss =
                        false,

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

    private static SimulationRunResult RunAbility(
        SimulationActorState source,
        SimulationActorState target,
        IReadOnlyList<SimulationActorState>? additionalActors = null,
        decimal durationSeconds = 1m)
    {
        var context =
            new SimulationContext(
                new SimulationRunOptions
                {
                    Seed =
                        12345,

                    DurationSeconds =
                        durationSeconds,

                    PrimaryActorKey =
                        source.Key,

                    CaptureTimeline =
                        true
                }
            );

        context.AddActor(
            source
        );

        if (!string.Equals(
                source.Key,
                target.Key,
                StringComparison.OrdinalIgnoreCase))
        {
            context.AddActor(
                target
            );
        }

        foreach (
            var actor in
            additionalActors ??
            [])
        {
            context.AddActor(
                actor
            );
        }

        var executor =
            CreateExecutor();

        return new SimulationEngine(
            [
                executor
            ])
            .Run(
                context,
                startedContext =>
                {
                    var useResult =
                        executor.TryStartAbility(
                            startedContext,
                            source.Key,
                            target.Key,
                            source.Abilities.Values
                                .Single()
                                .Definition.Key
                        );

                    Assert.True(
                        useResult.Success
                    );
                }
            );
    }

    private static AbilityExecutor CreateExecutor(
        AbsorbManager? absorbManager = null)
    {
        return new AbilityExecutor(
            new AuraManager(),
            new SimpleCombatRollResolver(),
            new RulesetDamageMitigationResolver(
                new CombatRulesetDefinition
                {
                    RulesetKey =
                        "absorb-tests",

                    Version =
                        "1"
                }
            ),
            absorbManager:
                absorbManager
        );
    }

    private static SimulationContext CreateStandaloneContext(
        string primaryActorKey)
    {
        return new SimulationContext(
            new SimulationRunOptions
            {
                Seed =
                    12345,

                DurationSeconds =
                    10m,

                PrimaryActorKey =
                    primaryActorKey,

                CaptureTimeline =
                    true
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

        return actor;
    }
}
