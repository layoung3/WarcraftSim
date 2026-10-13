// Consolidated tests for Mechanics Foundation.
// Original class names are retained for xUnit test filtering.

// ==================== AbsorbShieldFoundationTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Encounters;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Validation;

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
}

// ==================== AuraManagerTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;

public sealed class AuraManagerTests
{
    [Fact]
    public void Stack_FirstApplicationStartsAtOneStack()
    {
        var (context, target, manager) =
            CreateTestState();

        var aura =
            manager.ApplyAura(
                context,
                target,
                CreateAuraDefinition(
                    AuraStackingMode.Stack,
                    maxStacks:
                        5),
                sourceActorKey:
                    "source",
                abilityKey:
                    "test-ability",
                effectKey:
                    "test-effect",
                scheduleExpiration:
                    false
            );

        Assert.Equal(
            1,
            aura.Stacks
        );

        Assert.Single(
            target.ActiveAuras
        );
    }

    [Fact]
    public void Stack_ReapplicationsIncreaseByOneAndRespectMaxStacks()
    {
        var (context, target, manager) =
            CreateTestState();

        var definition =
            CreateAuraDefinition(
                AuraStackingMode.Stack,
                maxStacks:
                    3
            );

        var first =
            manager.ApplyAura(
                context,
                target,
                definition,
                "source",
                null,
                null,
                scheduleExpiration:
                    false
            );

        var second =
            manager.ApplyAura(
                context,
                target,
                definition,
                "source",
                null,
                null,
                scheduleExpiration:
                    false
            );

        Assert.Same(
            first,
            second
        );

        Assert.Equal(
            2,
            second.Stacks
        );

        manager.ApplyAura(
            context,
            target,
            definition,
            "source",
            null,
            null,
            scheduleExpiration:
                false
        );

        var capped =
            manager.ApplyAura(
                context,
                target,
                definition,
                "source",
                null,
                null,
                scheduleExpiration:
                    false
            );

        Assert.Equal(
            3,
            capped.Stacks
        );

        Assert.Single(
            target.ActiveAuras
        );
    }

    [Fact]
    public void Refresh_ReapplicationKeepsStackCountAndRefreshesDuration()
    {
        var (context, target, manager) =
            CreateTestState();

        var definition =
            CreateAuraDefinition(
                AuraStackingMode.Refresh,
                durationSeconds:
                    5m
            );

        var first =
            manager.ApplyAura(
                context,
                target,
                definition,
                "source",
                null,
                null,
                scheduleExpiration:
                    false
            );

        var originalInstanceId =
            first.InstanceId;

        AdvanceTo(
            context,
            3m
        );

        var refreshed =
            manager.ApplyAura(
                context,
                target,
                definition,
                "source",
                null,
                null,
                scheduleExpiration:
                    false
            );

        Assert.Same(
            first,
            refreshed
        );

        Assert.Equal(
            1,
            refreshed.Stacks
        );

        Assert.NotEqual(
            originalInstanceId,
            refreshed.InstanceId
        );

        Assert.Equal(
            8m,
            refreshed.ExpiresAtSeconds
        );
    }

    [Fact]
    public void Replace_RemovesExistingAuraAndResetsStacks()
    {
        var (context, target, manager) =
            CreateTestState();

        var stackingDefinition =
            CreateAuraDefinition(
                AuraStackingMode.Stack,
                maxStacks:
                    5
            );

        manager.ApplyAura(
            context,
            target,
            stackingDefinition,
            "source",
            null,
            null,
            scheduleExpiration:
                false
        );

        var stacked =
            manager.ApplyAura(
                context,
                target,
                stackingDefinition,
                "source",
                null,
                null,
                scheduleExpiration:
                    false
            );

        Assert.Equal(
            2,
            stacked.Stacks
        );

        var oldInstanceId =
            stacked.InstanceId;

        var replacementDefinition =
            CreateAuraDefinition(
                AuraStackingMode.Replace,
                maxStacks:
                    5
            );

        var replacement =
            manager.ApplyAura(
                context,
                target,
                replacementDefinition,
                "source",
                null,
                null,
                scheduleExpiration:
                    false
            );

        Assert.Single(
            target.ActiveAuras
        );

        Assert.Same(
            replacement,
            target.ActiveAuras[0]
        );

        Assert.Equal(
            1,
            replacement.Stacks
        );

        Assert.NotEqual(
            oldInstanceId,
            replacement.InstanceId
        );
    }

    [Fact]
    public void Independent_CreatesSeparateAuraInstances()
    {
        var (context, target, manager) =
            CreateTestState();

        var definition =
            CreateAuraDefinition(
                AuraStackingMode.Independent
            );

        var first =
            manager.ApplyAura(
                context,
                target,
                definition,
                "source",
                null,
                null,
                scheduleExpiration:
                    false
            );

        var second =
            manager.ApplyAura(
                context,
                target,
                definition,
                "source",
                null,
                null,
                scheduleExpiration:
                    false
            );

        Assert.Equal(
            2,
            target.ActiveAuras.Count
        );

        Assert.NotSame(
            first,
            second
        );

        Assert.NotEqual(
            first.InstanceId,
            second.InstanceId
        );

        Assert.All(
            target.ActiveAuras,
            aura =>
                Assert.Equal(
                    1,
                    aura.Stacks
                )
        );
    }

    [Fact]
    public void Refresh_StaleExpirationDoesNotRemoveCurrentAura()
    {
        var (context, target, manager) =
            CreateTestState();

        var definition =
            CreateAuraDefinition(
                AuraStackingMode.Refresh,
                durationSeconds:
                    5m
            );

        manager.ApplyAura(
            context,
            target,
            definition,
            "source",
            null,
            null,
            scheduleExpiration:
                true
        );

        AdvanceTo(
            context,
            2m
        );

        var refreshed =
            manager.ApplyAura(
                context,
                target,
                definition,
                "source",
                null,
                null,
                scheduleExpiration:
                    true
            );

        var staleExpiration =
            DequeueNextEventOfType(
                context,
                CombatEventType.AuraExpiration
            );

        Assert.Equal(
            5m,
            staleExpiration.TimeSeconds
        );

        manager.Process(
            context,
            staleExpiration
        );

        Assert.Single(
            target.ActiveAuras
        );

        Assert.Same(
            refreshed,
            target.ActiveAuras[0]
        );

        var currentExpiration =
            DequeueNextEventOfType(
                context,
                CombatEventType.AuraExpiration
            );

        Assert.Equal(
            7m,
            currentExpiration.TimeSeconds
        );

        manager.Process(
            context,
            currentExpiration
        );

        Assert.Empty(
            target.ActiveAuras
        );
    }

    private static (
        SimulationContext Context,
        SimulationActorState Target,
        AuraManager Manager)
        CreateTestState()
    {
        var context =
            new SimulationContext(
                new SimulationRunOptions
                {
                    Seed =
                        12345,

                    DurationSeconds =
                        30m,

                    PrimaryActorKey =
                        "target",

                    CaptureTimeline =
                        true
                }
            );

        var target =
            new SimulationActorState
            {
                Key =
                    "target",

                Name =
                    "Aura Target",

                TeamKey =
                    "raid"
            };

        target.InitializeHealth(
            10000m
        );

        context.AddActor(
            target
        );

        return (
            context,
            target,
            new AuraManager()
        );
    }

    private static AuraDefinition CreateAuraDefinition(
        AuraStackingMode stackingMode,
        int maxStacks = 1,
        decimal durationSeconds = 10m)
    {
        return new AuraDefinition
        {
            Key =
                "test-aura",

            Name =
                "Test Aura",

            DurationSeconds =
                durationSeconds,

            StackingMode =
                stackingMode,

            MaxStacks =
                maxStacks
        };
    }

    private static void AdvanceTo(
        SimulationContext context,
        decimal timeSeconds)
    {
        context.ScheduleEvent(
            new CombatEvent
            {
                TimeSeconds =
                    timeSeconds,

                Type =
                    CombatEventType.RotationDecision,

                IsInternal =
                    true,

                Description =
                    "Aura test time advance."
            }
        );

        var timeAdvance =
            DequeueNextEventOfType(
                context,
                CombatEventType.RotationDecision
            );

        Assert.Equal(
            timeSeconds,
            timeAdvance.TimeSeconds
        );

        Assert.Equal(
            timeSeconds,
            context.CurrentTimeSeconds
        );
    }

    private static CombatEvent DequeueNextEventOfType(
        SimulationContext context,
        CombatEventType eventType)
    {
        while (
            context.TryGetNextEvent(
                out var combatEvent)
        )
        {
            Assert.NotNull(
                combatEvent
            );

            if (
                combatEvent!.Type ==
                eventType
            )
            {
                return combatEvent;
            }
        }

        throw new InvalidOperationException(
            $"No queued event of type '{eventType}' was found."
        );
    }
}
}

// ==================== ExplicitAuraEffectTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Validation;

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
}

// ==================== ResourceChangeEffectTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Validation;

public sealed class ResourceChangeEffectTests
{
    [Fact]
    public void ResourceChange_GainAddsResourceAndReportsActualDelta()
    {
        var source = CreateActor("source", "raid");
        var target = CreateActor("target", "raid", "mana", 100m, 80m);

        source.AddAbility(
            CreateResourceAbility(
                "restore-mana",
                "mana",
                ResourceChangeOperationTypes.Gain,
                50m,
                AbilityTargetTypes.Friendly
            )
        );

        var result = RunAbility(source, target);

        Assert.Equal(100m, target.Resources["mana"].Current);

        var resourceEvent =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.ResourceChanged
            );

        Assert.Equal(20m, resourceEvent.Amount);
    }

    [Fact]
    public void ResourceChange_SpendDrainsOnlyAvailableResource()
    {
        var source = CreateActor("source", "raid");
        var target = CreateActor("target", "enemy", "mana", 100m, 30m);

        source.AddAbility(
            CreateResourceAbility(
                "mana-drain",
                "mana",
                ResourceChangeOperationTypes.Spend,
                50m,
                AbilityTargetTypes.Enemy
            )
        );

        var result = RunAbility(source, target);

        Assert.Equal(0m, target.Resources["mana"].Current);

        var resourceEvent =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.ResourceChanged
            );

        Assert.Equal(-30m, resourceEvent.Amount);
    }

    [Fact]
    public void ResourceChange_SetAssignsAbsoluteCurrentValue()
    {
        var source = CreateActor("source", "raid", "rage", 100m, 10m);

        source.AddAbility(
            CreateResourceAbility(
                "set-rage",
                "rage",
                ResourceChangeOperationTypes.Set,
                60m,
                AbilityTargetTypes.Self
            )
        );

        var result = RunAbility(source, source);

        Assert.Equal(60m, source.Resources["rage"].Current);

        var resourceEvent =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.ResourceChanged
            );

        Assert.Equal(50m, resourceEvent.Amount);
    }

    [Fact]
    public void ResourceChange_CanUsePercentOfMaximum()
    {
        var source = CreateActor("source", "raid");
        var target = CreateActor("target", "raid", "mana", 200m, 50m);

        source.AddAbility(
            CreateResourceAbility(
                "percent-restore",
                "mana",
                ResourceChangeOperationTypes.Gain,
                25m,
                AbilityTargetTypes.Friendly,
                amountIsPercentOfMaximum:
                    true
            )
        );

        RunAbility(source, target);

        Assert.Equal(100m, target.Resources["mana"].Current);
    }

    [Fact]
    public void ResourceChange_MultiTargetAffectsEachResolvedTarget()
    {
        var source = CreateActor("source", "raid");
        var targetA = CreateActor("a-target", "raid", "mana", 100m, 20m);
        var targetB = CreateActor("b-target", "raid", "mana", 100m, 30m);

        source.AddAbility(
            CreateResourceAbility(
                "group-restore",
                "mana",
                ResourceChangeOperationTypes.Gain,
                25m,
                AbilityTargetTypes.Friendly,
                maxTargets:
                    2
            )
        );

        var result =
            RunAbility(
                source,
                targetA,
                [targetB]
            );

        Assert.Equal(45m, targetA.Resources["mana"].Current);
        Assert.Equal(55m, targetB.Resources["mana"].Current);

        Assert.Equal(
            2,
            result.Timeline.Count(
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.ResourceChanged
            )
        );
    }

    [Fact]
    public void ResourceChange_MissDoesNotChangeResource()
    {
        var source = CreateActor("source", "raid");
        var target = CreateActor("target", "enemy", "mana", 100m, 75m);

        source.AddAbility(
            CreateResourceAbility(
                "missed-drain",
                "mana",
                ResourceChangeOperationTypes.Spend,
                50m,
                AbilityTargetTypes.Enemy
            )
        );

        var result =
            RunAbility(
                source,
                target,
                combatRollResolver:
                    new SimpleCombatRollResolver(
                        hitChancePercent:
                            0m
                    )
            );

        Assert.Equal(75m, target.Resources["mana"].Current);

        Assert.DoesNotContain(
            result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                    CombatEventType.ResourceChanged
        );
    }

    [Fact]
    public void Validator_RejectsInvalidResourceChangeDefinition()
    {
        var source = CreateActor("source", "raid");

        source.AddAbility(
            new AbilityDefinition
            {
                Key = "invalid-resource",
                Name = "Invalid Resource",
                IsOffGlobalCooldown = true,
                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key = "invalid-resource-effect",
                        EffectType = AbilityEffectTypes.ResourceChange,
                        TargetType = AbilityTargetTypes.Self,
                        ResourceChangeOperation = "invent-resource-operation",
                        MinimumValue = -10m,
                        MaximumValue = -10m
                    }
                ]
            }
        );

        var context = CreateContext(source.Key);
        context.AddActor(source);

        var exception =
            Assert.Throws<SimulationDefinitionValidationException>(
                () =>
                    new SimulationEngine()
                        .Run(context)
            );

        Assert.Contains(
            exception.Errors,
            error =>
                error.Contains(
                    "requires ResourceKey",
                    StringComparison.OrdinalIgnoreCase
                )
        );

        Assert.Contains(
            exception.Errors,
            error =>
                error.Contains(
                    "unknown resource operation",
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

    private static AbilityDefinition CreateResourceAbility(
        string key,
        string resourceKey,
        string operation,
        decimal value,
        string targetType,
        bool amountIsPercentOfMaximum = false,
        int maxTargets = 1)
    {
        return new AbilityDefinition
        {
            Key = key,
            Name = key,
            IsOffGlobalCooldown = true,
            Effects =
            [
                new AbilityEffectDefinition
                {
                    Key = $"{key}-effect",
                    EffectType = AbilityEffectTypes.ResourceChange,
                    TargetType = targetType,
                    ResolutionType =
                        string.Equals(
                            targetType,
                            AbilityTargetTypes.Enemy,
                            StringComparison.OrdinalIgnoreCase)
                            ? CombatResolutionTypes.Spell
                            : CombatResolutionTypes.AlwaysHits,
                    CanMiss =
                        string.Equals(
                            targetType,
                            AbilityTargetTypes.Enemy,
                            StringComparison.OrdinalIgnoreCase),
                    CanCrit = false,
                    ResourceKey = resourceKey,
                    ResourceChangeOperation = operation,
                    ResourceAmountIsPercentOfMaximum = amountIsPercentOfMaximum,
                    MinimumValue = value,
                    MaximumValue = value,
                    MaxTargets = maxTargets
                }
            ]
        };
    }

    private static SimulationRunResult RunAbility(
        SimulationActorState source,
        SimulationActorState primaryTarget,
        IReadOnlyList<SimulationActorState>? additionalActors = null,
        ICombatRollResolver? combatRollResolver = null)
    {
        var context =
            CreateContext(
                source.Key,
                captureTimeline:
                    true
            );

        context.AddActor(source);

        if (!string.Equals(
                source.Key,
                primaryTarget.Key,
                StringComparison.OrdinalIgnoreCase))
        {
            context.AddActor(primaryTarget);
        }

        foreach (var actor in additionalActors ?? [])
        {
            context.AddActor(actor);
        }

        var executor =
            new AbilityExecutor(
                new AuraManager(),
                combatRollResolver ??
                new SimpleCombatRollResolver(),
                new RulesetDamageMitigationResolver(
                    new CombatRulesetDefinition
                    {
                        RulesetKey = "resource-change-tests",
                        Version = "1"
                    }
                )
            );

        return new SimulationEngine(
            [executor]
        )
        .Run(
            context,
            startedContext =>
            {
                var result =
                    executor.TryStartAbility(
                        startedContext,
                        source.Key,
                        primaryTarget.Key,
                        source.Abilities.Values
                            .Single()
                            .Definition.Key
                    );

                Assert.True(result.Success);
            }
        );
    }

    private static SimulationContext CreateContext(
        string primaryActorKey,
        bool captureTimeline = false)
    {
        return new SimulationContext(
            new SimulationRunOptions
            {
                Seed = 12345,
                DurationSeconds = 1m,
                PrimaryActorKey = primaryActorKey,
                CaptureTimeline = captureTimeline
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
                Key = key,
                Name = key,
                TeamKey = teamKey,
                Level = 20
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

        if (!string.IsNullOrWhiteSpace(resourceKey))
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
