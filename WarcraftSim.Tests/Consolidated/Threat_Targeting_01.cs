// Consolidated tests for Threat Targeting.
// Original class names are retained for xUnit test filtering.

// ==================== ForcedTargetFoundationTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Encounters;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

public sealed class ForcedTargetFoundationTests
{
    [Fact]
    public void ApplyForcedTarget_SetsRuntimeStateAndEmitsAppliedEvent()
    {
        var context =
            CreateContext(
                durationSeconds:
                    5m,

                captureTimeline:
                    true
            );

        var boss =
            AddActor(
                context,
                "boss",
                "enemy"
            );

        var tank =
            AddActor(
                context,
                "tank",
                "raid"
            );

        var manager =
            new ForcedTargetManager();

        var result =
            new SimulationEngine(
                [
                    manager
                ]
            )
            .Run(
                context,
                startedContext =>
                {
                    manager.ApplyForcedTarget(
                        startedContext,
                        boss,
                        tank,
                        durationSeconds:
                            3m,

                        abilityKey:
                            "test-taunt"
                    );
                }
            );

        Assert.Null(
            boss.ForcedTarget
        );

        var applied =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.ForcedTargetApplied
            );

        Assert.Equal(
            tank.Key,
            applied.SourceActorKey
        );

        Assert.Equal(
            boss.Key,
            applied.TargetActorKey
        );

        Assert.Equal(
            tank.Key,
            applied.ForcedTargetActorKey
        );

        Assert.Equal(
            "test-taunt",
            applied.AbilityKey
        );

        Assert.DoesNotContain(
            result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                    CombatEventType.ForcedTargetExpiration
        );
    }

    [Fact]
    public void HighestThreatTargeting_ActiveForcedTargetOverridesThreatLeader()
    {
        var context =
            CreateContext();

        var boss =
            AddActor(
                context,
                "boss",
                "enemy"
            );

        var tank =
            AddActor(
                context,
                "tank",
                "raid"
            );

        var damage =
            AddActor(
                context,
                "damage",
                "raid"
            );

        boss.ThreatTable.AddThreat(
            tank.Key,
            100m
        );

        boss.ThreatTable.AddThreat(
            damage.Key,
            500m
        );

        new ForcedTargetManager()
            .ApplyForcedTarget(
                context,
                boss,
                tank,
                durationSeconds:
                    5m
            );

        var selected =
            Assert.Single(
                EncounterTargetSelector.Resolve(
                    context,
                    CreateThreatPattern()
                )
            );

        Assert.Same(
            tank,
            selected
        );
    }

    [Fact]
    public void HighestThreatTargeting_DeadForcedTargetFallsBackToThreatLeader()
    {
        var context =
            CreateContext();

        var boss =
            AddActor(
                context,
                "boss",
                "enemy"
            );

        var tank =
            AddActor(
                context,
                "tank",
                "raid"
            );

        var damage =
            AddActor(
                context,
                "damage",
                "raid"
            );

        boss.ThreatTable.AddThreat(
            tank.Key,
            100m
        );

        boss.ThreatTable.AddThreat(
            damage.Key,
            500m
        );

        new ForcedTargetManager()
            .ApplyForcedTarget(
                context,
                boss,
                tank,
                durationSeconds:
                    5m
            );

        tank.TakeDamage(
            tank.MaximumHealth
        );

        var selected =
            Assert.Single(
                EncounterTargetSelector.Resolve(
                    context,
                    CreateThreatPattern()
                )
            );

        Assert.Same(
            damage,
            selected
        );
    }

    [Fact]
    public void ForcedTargetExpiration_ReturnsTargetingToThreatLeader()
    {
        var encounter =
            new EncounterProfile
            {
                Name =
                    "Forced Target Expiration Test",

                RulesetKey =
                    "development",

                DurationSeconds =
                    4m,

                DamagePatterns =
                [
                    new EncounterDamagePatternDefinition
                    {
                        Key =
                            "melee",

                        Name =
                            "Melee",

                        StartTimeSeconds =
                            1m,

                        EndTimeSeconds =
                            3m,

                        IntervalSeconds =
                            2m,

                        SourceActorKey =
                            "boss",

                        TargetSelection =
                            new EncounterTargetSelectionDefinition
                            {
                                Mode =
                                    EncounterTargetSelectionModes.HighestThreatActor,

                                Relationship =
                                    SimulationActorRelationshipTypes.Enemy
                            },

                        Amount =
                            100m,

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
                        4m,

                    PrimaryActorKey =
                        "boss",

                    CaptureTimeline =
                        true
                },
                encounter
            );

        var boss =
            AddActor(
                context,
                "boss",
                "enemy"
            );

        var tank =
            AddActor(
                context,
                "tank",
                "raid"
            );

        var damage =
            AddActor(
                context,
                "damage",
                "raid"
            );

        boss.ThreatTable.AddThreat(
            tank.Key,
            100m
        );

        boss.ThreatTable.AddThreat(
            damage.Key,
            500m
        );

        var forcedTargetManager =
            new ForcedTargetManager();

        var mitigationResolver =
            new RulesetDamageMitigationResolver(
                new CombatRulesetDefinition
                {
                    RulesetKey =
                        "forced-target-tests",

                    Version =
                        "1"
                }
            );

        var result =
            new SimulationEngine(
                [
                    new EncounterTimelineProcessor(),
                    new ScriptedEncounterDamageProcessor(
                        mitigationResolver
                    ),
                    forcedTargetManager
                ]
            )
            .Run(
                context,
                startedContext =>
                {
                    forcedTargetManager.ApplyForcedTarget(
                        startedContext,
                        boss,
                        tank,
                        durationSeconds:
                            2m
                    );
                }
            );

        var damageEvents =
            result.Timeline
                .Where(
                    combatEvent =>
                        combatEvent.Type ==
                            CombatEventType.Damage &&
                        string.Equals(
                            combatEvent.AbilityKey,
                            "encounter:melee",
                            StringComparison.OrdinalIgnoreCase
                        )
                )
                .OrderBy(
                    combatEvent =>
                        combatEvent.TimeSeconds
                )
                .ToArray();

        Assert.Equal(
            2,
            damageEvents.Length
        );

        Assert.Equal(
            1m,
            damageEvents[0].TimeSeconds
        );

        Assert.Equal(
            tank.Key,
            damageEvents[0].TargetActorKey
        );

        Assert.Equal(
            3m,
            damageEvents[1].TimeSeconds
        );

        Assert.Equal(
            damage.Key,
            damageEvents[1].TargetActorKey
        );

        Assert.Single(
            result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                    CombatEventType.ForcedTargetRemoved
        );
    }

    [Fact]
    public void ReapplyingForcedTarget_IgnoresStaleExpirationEvent()
    {
        var context =
            CreateContext(
                durationSeconds:
                    2m
            );

        var boss =
            AddActor(
                context,
                "boss",
                "enemy"
            );

        var firstTank =
            AddActor(
                context,
                "tank-a",
                "raid"
            );

        var secondTank =
            AddActor(
                context,
                "tank-b",
                "raid"
            );

        var manager =
            new ForcedTargetManager();

        var reapplyDriver =
            new ReapplyForcedTargetDriver(
                manager,
                boss,
                secondTank
            );

        new SimulationEngine(
            [
                manager,
                reapplyDriver
            ])
            .Run(
                context,
                startedContext =>
                {
                    manager.ApplyForcedTarget(
                        startedContext,
                        boss,
                        firstTank,
                        durationSeconds:
                            1m
                    );
                }
            );

        Assert.NotNull(
            boss.ForcedTarget
        );

        Assert.Equal(
            secondTank.Key,
            boss.ForcedTarget!.ForcedTargetActorKey
        );

        Assert.True(
            boss.ForcedTarget.ExpiresAtSeconds >
            context.Options.DurationSeconds
        );
    }

    [Fact]
    public void RemoveForcedTarget_ClearsOverrideAndEmitsRemovedEvent()
    {
        var context =
            CreateContext(
                durationSeconds:
                    2m,

                captureTimeline:
                    true
            );

        var boss =
            AddActor(
                context,
                "boss",
                "enemy"
            );

        var tank =
            AddActor(
                context,
                "tank",
                "raid"
            );

        var manager =
            new ForcedTargetManager();

        var result =
            new SimulationEngine(
                [
                    manager
                ]
            )
            .Run(
                context,
                startedContext =>
                {
                    manager.ApplyForcedTarget(
                        startedContext,
                        boss,
                        tank,
                        durationSeconds:
                            5m
                    );

                    Assert.True(
                        manager.RemoveForcedTarget(
                            startedContext,
                            boss,
                            "cancelled"
                        )
                    );
                }
            );

        Assert.Null(
            boss.ForcedTarget
        );

        var removed =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.ForcedTargetRemoved
            );

        Assert.Equal(
            tank.Key,
            removed.ForcedTargetActorKey
        );

        Assert.DoesNotContain(
            result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                    CombatEventType.ForcedTargetExpiration
        );
    }

    [Fact]
    public void ApplyForcedTarget_RejectsNonPositiveDuration()
    {
        var context =
            CreateContext();

        var boss =
            AddActor(
                context,
                "boss",
                "enemy"
            );

        var tank =
            AddActor(
                context,
                "tank",
                "raid"
            );

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new ForcedTargetManager()
                    .ApplyForcedTarget(
                        context,
                        boss,
                        tank,
                        durationSeconds:
                            0m
                    )
        );
    }

    private static EncounterDamagePatternDefinition CreateThreatPattern()
    {
        return new EncounterDamagePatternDefinition
        {
            Key =
                "threat-target",

            Name =
                "Threat Target",

            SourceActorKey =
                "boss",

            TargetSelection =
                new EncounterTargetSelectionDefinition
                {
                    Mode =
                        EncounterTargetSelectionModes.HighestThreatActor,

                    Relationship =
                        SimulationActorRelationshipTypes.Enemy
                }
        };
    }

    private static SimulationContext CreateContext(
        decimal durationSeconds = 10m,
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
                    "boss",

                CaptureTimeline =
                    captureTimeline
            }
        );
    }

    private static SimulationActorState AddActor(
        SimulationContext context,
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

        context.AddActor(
            actor
        );

        return actor;
    }

    private sealed class ReapplyForcedTargetDriver :
        ICombatEventProcessor
    {
        private readonly ForcedTargetManager
            _manager;

        private readonly SimulationActorState
            _targetOwner;

        private readonly SimulationActorState
            _forcedTarget;

        public ReapplyForcedTargetDriver(
            ForcedTargetManager manager,
            SimulationActorState targetOwner,
            SimulationActorState forcedTarget)
        {
            _manager =
                manager;

            _targetOwner =
                targetOwner;

            _forcedTarget =
                forcedTarget;
        }

        public void Process(
            SimulationContext context,
            CombatEvent combatEvent)
        {
            if (
                combatEvent.Type ==
                    CombatEventType.SimulationStarted
            )
            {
                context.ScheduleEvent(
                    new CombatEvent
                    {
                        TimeSeconds =
                            0.5m,

                        Type =
                            CombatEventType.RotationDecision,

                        SourceActorKey =
                            _targetOwner.Key,

                        IsInternal =
                            true,

                        Description =
                            "Reapply forced target."
                    }
                );

                return;
            }

            if (
                combatEvent.Type ==
                    CombatEventType.RotationDecision &&
                combatEvent.TimeSeconds ==
                    0.5m
            )
            {
                _manager.ApplyForcedTarget(
                    context,
                    _targetOwner,
                    _forcedTarget,
                    durationSeconds:
                        5m
                );
            }
        }
    }
}
}

// ==================== RulesetThreatGenerationResolverTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation.Engine;

public sealed class RulesetThreatGenerationResolverTests
{
    [Fact]
    public void DamageRule_GeneratesThreatOnEventTarget()
    {
        var context =
            CreateContext();

        var source =
            AddActor(
                context,
                "player",
                "raid"
            );

        var target =
            AddActor(
                context,
                "boss",
                "enemy"
            );

        var resolver =
            CreateResolver(
                damageRule:
                    new ThreatGenerationRuleDefinition
                    {
                        BaseMultiplier =
                            2m
                    }
            );

        var contribution =
            Assert.Single(
                resolver.Resolve(
                    context,
                    new CombatEvent
                    {
                        Type =
                            CombatEventType.Damage,

                        SourceActorKey =
                            source.Key,

                        TargetActorKey =
                            target.Key,

                        Amount =
                            100m
                    }
                )
            );

        Assert.Equal(
            target.Key,
            contribution.ThreatOwnerActorKey
        );

        Assert.Equal(
            source.Key,
            contribution.ThreatSourceActorKey
        );

        Assert.Equal(
            200m,
            contribution.Amount
        );
    }

    [Fact]
    public void DamageRule_CanUseSourceThreatMultiplierStat()
    {
        var context =
            CreateContext();

        var source =
            AddActor(
                context,
                "player",
                "raid"
            );

        var target =
            AddActor(
                context,
                "boss",
                "enemy"
            );

        source.Stats.Set(
            "threat-multiplier",
            1.5m
        );

        var resolver =
            CreateResolver(
                damageRule:
                    new ThreatGenerationRuleDefinition
                    {
                        BaseMultiplier =
                            1m,

                        SourceMultiplierStatKey =
                            "threat-multiplier"
                    }
            );

        var contribution =
            Assert.Single(
                resolver.Resolve(
                    context,
                    new CombatEvent
                    {
                        Type =
                            CombatEventType.Damage,

                        SourceActorKey =
                            source.Key,

                        TargetActorKey =
                            target.Key,

                        Amount =
                            100m
                    }
                )
            );

        Assert.Equal(
            150m,
            contribution.Amount
        );
    }

    [Fact]
    public void DamageRule_CanApplyAbilitySpecificMultiplier()
    {
        var context =
            CreateContext();

        var source =
            AddActor(
                context,
                "player",
                "raid"
            );

        var target =
            AddActor(
                context,
                "boss",
                "enemy"
            );

        var resolver =
            CreateResolver(
                damageRule:
                    new ThreatGenerationRuleDefinition
                    {
                        BaseMultiplier =
                            1m,

                        AbilityMultipliers =
                            new Dictionary<string, decimal>(
                                StringComparer.OrdinalIgnoreCase
                            )
                            {
                                ["high-threat-strike"] =
                                    3m
                            }
                    }
            );

        var contribution =
            Assert.Single(
                resolver.Resolve(
                    context,
                    new CombatEvent
                    {
                        Type =
                            CombatEventType.Damage,

                        SourceActorKey =
                            source.Key,

                        TargetActorKey =
                            target.Key,

                        AbilityKey =
                            "HIGH-THREAT-STRIKE",

                        Amount =
                            50m
                    }
                )
            );

        Assert.Equal(
            150m,
            contribution.Amount
        );
    }

    [Fact]
    public void DamageRule_CanUseRawAmountInsteadOfEffectiveAmount()
    {
        var context =
            CreateContext();

        var source =
            AddActor(
                context,
                "player",
                "raid"
            );

        var target =
            AddActor(
                context,
                "boss",
                "enemy"
            );

        var resolver =
            CreateResolver(
                damageRule:
                    new ThreatGenerationRuleDefinition
                    {
                        BaseMultiplier =
                            1m,

                        AmountBasis =
                            ThreatAmountBasisTypes.RawAmount
                    }
            );

        var contribution =
            Assert.Single(
                resolver.Resolve(
                    context,
                    new CombatEvent
                    {
                        Type =
                            CombatEventType.Damage,

                        SourceActorKey =
                            source.Key,

                        TargetActorKey =
                            target.Key,

                        RawAmount =
                            200m,

                        Amount =
                            125m
                    }
                )
            );

        Assert.Equal(
            200m,
            contribution.Amount
        );
    }

    [Fact]
    public void HealingRule_CanGenerateFullThreatOnEveryLivingEnemy()
    {
        var context =
            CreateContext();

        var healer =
            AddActor(
                context,
                "healer",
                "raid"
            );

        var ally =
            AddActor(
                context,
                "ally",
                "raid"
            );

        AddActor(
            context,
            "boss-a",
            "enemy"
        );

        AddActor(
            context,
            "boss-b",
            "enemy"
        );

        var resolver =
            CreateResolver(
                healingRule:
                    new ThreatGenerationRuleDefinition
                    {
                        BaseMultiplier =
                            0.5m,

                        ThreatOwnerSelectionMode =
                            ThreatOwnerSelectionModes.AllEnemiesOfSource,

                        ThreatOwnerDistributionMode =
                            ThreatOwnerDistributionModes.FullAmountPerOwner
                    }
            );

        var contributions =
            resolver.Resolve(
                context,
                new CombatEvent
                {
                    Type =
                        CombatEventType.Healing,

                    SourceActorKey =
                        healer.Key,

                    TargetActorKey =
                        ally.Key,

                    Amount =
                        100m
                }
            );

        Assert.Equal(
            2,
            contributions.Count
        );

        Assert.All(
            contributions,
            contribution =>
                Assert.Equal(
                    50m,
                    contribution.Amount
                )
        );

        Assert.Equal(
            new[]
            {
                "boss-a",
                "boss-b"
            },
            contributions
                .Select(
                    contribution =>
                        contribution.ThreatOwnerActorKey
                )
                .ToArray()
        );
    }

    [Fact]
    public void HealingRule_CanSplitThreatEvenlyAcrossEnemies()
    {
        var context =
            CreateContext();

        var healer =
            AddActor(
                context,
                "healer",
                "raid"
            );

        var ally =
            AddActor(
                context,
                "ally",
                "raid"
            );

        AddActor(
            context,
            "boss-a",
            "enemy"
        );

        AddActor(
            context,
            "boss-b",
            "enemy"
        );

        var resolver =
            CreateResolver(
                healingRule:
                    new ThreatGenerationRuleDefinition
                    {
                        BaseMultiplier =
                            1m,

                        ThreatOwnerSelectionMode =
                            ThreatOwnerSelectionModes.AllEnemiesOfSource,

                        ThreatOwnerDistributionMode =
                            ThreatOwnerDistributionModes.SplitEvenly
                    }
            );

        var contributions =
            resolver.Resolve(
                context,
                new CombatEvent
                {
                    Type =
                        CombatEventType.Healing,

                    SourceActorKey =
                        healer.Key,

                    TargetActorKey =
                        ally.Key,

                    Amount =
                        100m
                }
            );

        Assert.Equal(
            2,
            contributions.Count
        );

        Assert.All(
            contributions,
            contribution =>
                Assert.Equal(
                    50m,
                    contribution.Amount
                )
        );
    }

    [Fact]
    public void UnconfiguredEventType_GeneratesNoThreat()
    {
        var context =
            CreateContext();

        var source =
            AddActor(
                context,
                "player",
                "raid"
            );

        var target =
            AddActor(
                context,
                "boss",
                "enemy"
            );

        var resolver =
            CreateResolver(
                damageRule:
                    null,

                healingRule:
                    null
            );

        var contributions =
            resolver.Resolve(
                context,
                new CombatEvent
                {
                    Type =
                        CombatEventType.Damage,

                    SourceActorKey =
                        source.Key,

                    TargetActorKey =
                        target.Key,

                    Amount =
                        100m
                }
            );

        Assert.Empty(
            contributions
        );
    }

    [Fact]
    public void RulesetResolver_IntegratesWithThreatManager()
    {
        var context =
            CreateContext(
                captureTimeline:
                    true
            );

        var player =
            AddActor(
                context,
                "player",
                "raid"
            );

        var boss =
            AddActor(
                context,
                "boss",
                "enemy"
            );

        var resolver =
            CreateResolver(
                damageRule:
                    new ThreatGenerationRuleDefinition
                    {
                        BaseMultiplier =
                            1.25m
                    }
            );

        var engine =
            new SimulationEngine(
                [
                    new CurrentTimeEventDriver(
                        new CombatEvent
                        {
                            Type =
                                CombatEventType.Damage,

                            SourceActorKey =
                                player.Key,

                            TargetActorKey =
                                boss.Key,

                            AbilityKey =
                                "strike",

                            Amount =
                                80m
                        }
                    ),

                    new ThreatManager(
                        resolver
                    )
                ]
            );

        var result =
            engine.Run(
                context
            );

        Assert.Equal(
            100m,
            boss.ThreatTable.GetThreat(
                player.Key
            )
        );

        var threatEvent =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.ThreatChanged
            );

        Assert.Equal(
            100m,
            threatEvent.Amount
        );
    }

    private static RulesetThreatGenerationResolver CreateResolver(
        ThreatGenerationRuleDefinition? damageRule = null,
        ThreatGenerationRuleDefinition? healingRule = null)
    {
        var ruleset =
            new CombatRulesetDefinition
            {
                RulesetKey =
                    "threat-test",

                Version =
                    "1"
            };

        if (damageRule is not null)
        {
            ruleset.ThreatGenerationRules[
                ThreatGenerationEventTypes.Damage
            ] = damageRule;
        }

        if (healingRule is not null)
        {
            ruleset.ThreatGenerationRules[
                ThreatGenerationEventTypes.Healing
            ] = healingRule;
        }

        return new RulesetThreatGenerationResolver(
            ruleset
        );
    }

    private static SimulationContext CreateContext(
        bool captureTimeline = false)
    {
        return new SimulationContext(
            new SimulationRunOptions
            {
                Seed =
                    12345,

                DurationSeconds =
                    10m,

                PrimaryActorKey =
                    "player",

                CaptureTimeline =
                    captureTimeline
            }
        );
    }

    private static SimulationActorState AddActor(
        SimulationContext context,
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
                1000m
        );

        context.AddActor(
            actor
        );

        return actor;
    }

    private sealed class CurrentTimeEventDriver :
        ICombatEventProcessor
    {
        private readonly CombatEvent
            _event;

        public CurrentTimeEventDriver(
            CombatEvent combatEvent)
        {
            _event =
                combatEvent;
        }

        public void Process(
            SimulationContext context,
            CombatEvent combatEvent)
        {
            if (
                combatEvent.Type !=
                    CombatEventType.SimulationStarted
            )
            {
                return;
            }

            _event.TimeSeconds =
                context.CurrentTimeSeconds;

            context.EmitEvent(
                _event
            );
        }
    }
}
}

// ==================== TauntEffectFoundationTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Encounters;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Validation;

public sealed class TauntEffectFoundationTests
{
    [Fact]
    public void TauntEffect_ForcesEnemyToCasterWithoutChangingThreatByDefault()
    {
        var tank =
            CreateActor(
                "tank",
                "raid"
            );

        var damage =
            CreateActor(
                "damage",
                "raid"
            );

        var boss =
            CreateActor(
                "boss",
                "enemy"
            );

        boss.ThreatTable.AddThreat(
            tank.Key,
            100m
        );

        boss.ThreatTable.AddThreat(
            damage.Key,
            500m
        );

        tank.AddAbility(
            CreateTauntAbility(
                "basic-taunt",
                durationSeconds:
                    3m
            )
        );

        var run =
            RunAbility(
                tank,
                boss,
                [
                    damage
                ],
                durationSeconds:
                    1m
            );

        Assert.NotNull(
            boss.ForcedTarget
        );

        Assert.Equal(
            tank.Key,
            boss.ForcedTarget!.ForcedTargetActorKey
        );

        Assert.Equal(
            100m,
            boss.ThreatTable.GetThreat(
                tank.Key
            )
        );

        var selected =
            Assert.Single(
                EncounterTargetSelector.Resolve(
                    run.Context,
                    CreateThreatPattern()
                )
            );

        Assert.Same(
            tank,
            selected
        );

        Assert.Single(
            run.Result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                    CombatEventType.ForcedTargetApplied
        );

        Assert.DoesNotContain(
            run.Result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                    CombatEventType.ThreatChanged
        );
    }

    [Fact]
    public void TauntEffect_ExpirationReturnsTargetingToThreatLeader()
    {
        var tank =
            CreateActor(
                "tank",
                "raid"
            );

        var damage =
            CreateActor(
                "damage",
                "raid"
            );

        var boss =
            CreateActor(
                "boss",
                "enemy"
            );

        boss.ThreatTable.AddThreat(
            tank.Key,
            100m
        );

        boss.ThreatTable.AddThreat(
            damage.Key,
            500m
        );

        tank.AddAbility(
            CreateTauntAbility(
                "short-taunt",
                durationSeconds:
                    2m
            )
        );

        var run =
            RunAbility(
                tank,
                boss,
                [
                    damage
                ],
                durationSeconds:
                    4m
            );

        Assert.Null(
            boss.ForcedTarget
        );

        var selected =
            Assert.Single(
                EncounterTargetSelector.Resolve(
                    run.Context,
                    CreateThreatPattern()
                )
            );

        Assert.Same(
            damage,
            selected
        );

        Assert.Single(
            run.Result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                    CombatEventType.ForcedTargetRemoved
        );
    }

    [Fact]
    public void TauntEffect_CanMatchCurrentHighestThreat()
    {
        var tank =
            CreateActor(
                "tank",
                "raid"
            );

        var damage =
            CreateActor(
                "damage",
                "raid"
            );

        var boss =
            CreateActor(
                "boss",
                "enemy"
            );

        boss.ThreatTable.AddThreat(
            tank.Key,
            100m
        );

        boss.ThreatTable.AddThreat(
            damage.Key,
            400m
        );

        tank.AddAbility(
            CreateTauntAbility(
                "match-taunt",
                durationSeconds:
                    3m,

                tauntThreatOperation:
                    ThreatManipulationOperationTypes.MatchHighest
            )
        );

        var run =
            RunAbility(
                tank,
                boss,
                [
                    damage
                ],
                durationSeconds:
                    1m
            );

        Assert.Equal(
            400m,
            boss.ThreatTable.GetThreat(
                tank.Key
            )
        );

        Assert.Equal(
            tank.Key,
            boss.ForcedTarget?.ForcedTargetActorKey
        );

        var threatChanged =
            Assert.Single(
                run.Result.Timeline,
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.ThreatChanged
            );

        Assert.Equal(
            300m,
            threatChanged.Amount
        );
    }

    [Fact]
    public void TauntEffect_CanMatchHighestPlusConfiguredAmount()
    {
        var tank =
            CreateActor(
                "tank",
                "raid"
            );

        var damage =
            CreateActor(
                "damage",
                "raid"
            );

        var boss =
            CreateActor(
                "boss",
                "enemy"
            );

        boss.ThreatTable.AddThreat(
            tank.Key,
            100m
        );

        boss.ThreatTable.AddThreat(
            damage.Key,
            400m
        );

        tank.AddAbility(
            CreateTauntAbility(
                "overtake-taunt",
                durationSeconds:
                    3m,

                tauntThreatOperation:
                    ThreatManipulationOperationTypes.MatchHighestPlus,

                value:
                    25m
            )
        );

        RunAbility(
            tank,
            boss,
            [
                damage
            ],
            durationSeconds:
                1m
        );

        Assert.Equal(
            425m,
            boss.ThreatTable.GetThreat(
                tank.Key
            )
        );

        Assert.Equal(
            tank.Key,
            boss.ThreatTable.GetHighestThreatActorKey()
        );
    }

    [Fact]
    public void TauntEffect_MissDoesNotForceTargetOrManipulateThreat()
    {
        var tank =
            CreateActor(
                "tank",
                "raid"
            );

        var damage =
            CreateActor(
                "damage",
                "raid"
            );

        var boss =
            CreateActor(
                "boss",
                "enemy"
            );

        boss.ThreatTable.AddThreat(
            tank.Key,
            100m
        );

        boss.ThreatTable.AddThreat(
            damage.Key,
            400m
        );

        tank.AddAbility(
            CreateTauntAbility(
                "missed-taunt",
                durationSeconds:
                    3m,

                tauntThreatOperation:
                    ThreatManipulationOperationTypes.MatchHighest
            )
        );

        var run =
            RunAbility(
                tank,
                boss,
                [
                    damage
                ],
                durationSeconds:
                    1m,

                combatRollResolver:
                    new SimpleCombatRollResolver(
                        hitChancePercent:
                            0m
                    )
            );

        Assert.Null(
            boss.ForcedTarget
        );

        Assert.Equal(
            100m,
            boss.ThreatTable.GetThreat(
                tank.Key
            )
        );

        Assert.DoesNotContain(
            run.Result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                    CombatEventType.ForcedTargetApplied
        );

        Assert.DoesNotContain(
            run.Result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                    CombatEventType.ThreatChanged
        );
    }

    [Fact]
    public void TauntEffect_CanForceMultipleEnemyTargets()
    {
        var tank =
            CreateActor(
                "tank",
                "raid"
            );

        var bossA =
            CreateActor(
                "boss-a",
                "enemy"
            );

        var bossB =
            CreateActor(
                "boss-b",
                "enemy"
            );

        tank.AddAbility(
            CreateTauntAbility(
                "area-taunt",
                durationSeconds:
                    3m,

                maxTargets:
                    2
            )
        );

        var run =
            RunAbility(
                tank,
                bossA,
                [
                    bossB
                ],
                durationSeconds:
                    1m
            );

        Assert.Equal(
            tank.Key,
            bossA.ForcedTarget?.ForcedTargetActorKey
        );

        Assert.Equal(
            tank.Key,
            bossB.ForcedTarget?.ForcedTargetActorKey
        );

        Assert.Equal(
            2,
            run.Result.Timeline.Count(
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.ForcedTargetApplied
            )
        );
    }

    [Fact]
    public void Validator_RejectsInvalidTauntDefinition()
    {
        var source =
            CreateActor(
                "tank",
                "raid"
            );

        source.AddAbility(
            new AbilityDefinition
            {
                Key =
                    "invalid-taunt",

                Name =
                    "Invalid Taunt",

                IsOffGlobalCooldown =
                    true,

                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key =
                            "invalid-taunt-effect",

                        EffectType =
                            AbilityEffectTypes.Taunt,

                        TargetType =
                            AbilityTargetTypes.Friendly,

                        DurationSeconds =
                            0m,

                        TauntThreatOperation =
                            "invent-threat"
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
                    "requires enemy targeting",
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
                    "unknown taunt threat operation",
                    StringComparison.OrdinalIgnoreCase
                )
        );
    }

    private static AbilityDefinition CreateTauntAbility(
        string key,
        decimal durationSeconds,
        string? tauntThreatOperation = null,
        decimal value = 0m,
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
                        AbilityEffectTypes.Taunt,

                    TargetType =
                        AbilityTargetTypes.Enemy,

                    ResolutionType =
                        CombatResolutionTypes.Spell,

                    CanMiss =
                        true,

                    CanCrit =
                        false,

                    DurationSeconds =
                        durationSeconds,

                    TauntThreatOperation =
                        tauntThreatOperation,

                    MinimumValue =
                        value,

                    MaximumValue =
                        value,

                    MaxTargets =
                        maxTargets
                }
            ]
        };
    }

    private static AbilityRunResult RunAbility(
        SimulationActorState source,
        SimulationActorState primaryTarget,
        IReadOnlyList<SimulationActorState>? additionalActors = null,
        decimal durationSeconds = 1m,
        ICombatRollResolver? combatRollResolver = null)
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

        var executor =
            new AbilityExecutor(
                new AuraManager(),
                combatRollResolver ??
                new SimpleCombatRollResolver(),
                new RulesetDamageMitigationResolver(
                    new CombatRulesetDefinition
                    {
                        RulesetKey =
                            "taunt-effect-tests",

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

    private static EncounterDamagePatternDefinition CreateThreatPattern()
    {
        return new EncounterDamagePatternDefinition
        {
            Key =
                "threat-target",

            Name =
                "Threat Target",

            SourceActorKey =
                "boss",

            TargetSelection =
                new EncounterTargetSelectionDefinition
                {
                    Mode =
                        EncounterTargetSelectionModes.HighestThreatActor,

                    Relationship =
                        SimulationActorRelationshipTypes.Enemy
                }
        };
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

    private sealed record AbilityRunResult(
        SimulationContext Context,
        SimulationRunResult Result);
}
}

// ==================== ThreatFoundationTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Simulation.Engine;

public sealed class ThreatFoundationTests
{
    [Fact]
    public void ThreatTable_AccumulatesThreatPerActor()
    {
        var table =
            new ThreatTableState();

        Assert.Equal(
            100m,
            table.AddThreat(
                "tank",
                100m
            )
        );

        Assert.Equal(
            150m,
            table.AddThreat(
                "TANK",
                50m
            )
        );

        table.AddThreat(
            "damage",
            125m
        );

        Assert.Equal(
            150m,
            table.GetThreat(
                "tank"
            )
        );

        Assert.Equal(
            125m,
            table.GetThreat(
                "damage"
            )
        );

        Assert.Equal(
            "tank",
            table.GetHighestThreatActorKey(),
            ignoreCase:
                true
        );
    }

    [Fact]
    public void ThreatTable_HighestThreatTieBreaksByStableActorKey()
    {
        var table =
            new ThreatTableState();

        table.AddThreat(
            "z-player",
            100m
        );

        table.AddThreat(
            "a-player",
            100m
        );

        Assert.Equal(
            "a-player",
            table.GetHighestThreatActorKey()
        );
    }

    [Fact]
    public void ThreatTable_RejectsNegativeThreat()
    {
        var table =
            new ThreatTableState();

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                table.AddThreat(
                    "tank",
                    -1m
                )
        );
    }

    [Fact]
    public void ThreatManager_AppliesResolverContributionAndEmitsEvent()
    {
        var context =
            CreateContext(
                primaryActorKey:
                    "player"
            );

        var player =
            AddActor(
                context,
                "player",
                "raid"
            );

        var boss =
            AddActor(
                context,
                "boss",
                "enemy"
            );

        var resolver =
            new TestThreatResolver(
                sourceEvent =>
                {
                    if (
                        sourceEvent.Type !=
                            CombatEventType.Damage
                    )
                    {
                        return [];
                    }

                    return
                    [
                        new ThreatGenerationContribution
                        {
                            ThreatOwnerActorKey =
                                "boss",

                            ThreatSourceActorKey =
                                "player",

                            Amount =
                                250m
                        }
                    ];
                }
            );

        var engine =
            new SimulationEngine(
                [
                    new CurrentTimeCombatEventDriver(
                        new CombatEvent
                        {
                            Type =
                                CombatEventType.Damage,

                            SourceActorKey =
                                player.Key,

                            TargetActorKey =
                                boss.Key,

                            AbilityKey =
                                "test-strike",

                            Amount =
                                100m
                        }
                    ),

                    new ThreatManager(
                        resolver
                    )
                ]
            );

        var result =
            engine.Run(
                context
            );

        Assert.Equal(
            250m,
            boss.ThreatTable.GetThreat(
                player.Key
            )
        );

        Assert.Equal(
            player.Key,
            boss.ThreatTable.GetHighestThreatActorKey()
        );

        var threatEvent =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.ThreatChanged
            );

        Assert.Equal(
            player.Key,
            threatEvent.SourceActorKey
        );

        Assert.Equal(
            boss.Key,
            threatEvent.TargetActorKey
        );

        Assert.Equal(
            "test-strike",
            threatEvent.AbilityKey
        );

        Assert.Equal(
            250m,
            threatEvent.Amount
        );
    }

    [Fact]
    public void ThreatManager_ResolverCanGenerateThreatOnMultipleOwners()
    {
        var context =
            CreateContext(
                primaryActorKey:
                    "healer"
            );

        var healer =
            AddActor(
                context,
                "healer",
                "raid"
            );

        var ally =
            AddActor(
                context,
                "ally",
                "raid"
            );

        var bossA =
            AddActor(
                context,
                "boss-a",
                "enemy"
            );

        var bossB =
            AddActor(
                context,
                "boss-b",
                "enemy"
            );

        var resolver =
            new TestThreatResolver(
                sourceEvent =>
                {
                    if (
                        sourceEvent.Type !=
                            CombatEventType.Healing
                    )
                    {
                        return [];
                    }

                    return
                    [
                        new ThreatGenerationContribution
                        {
                            ThreatOwnerActorKey =
                                bossA.Key,

                            ThreatSourceActorKey =
                                healer.Key,

                            Amount =
                                40m
                        },

                        new ThreatGenerationContribution
                        {
                            ThreatOwnerActorKey =
                                bossB.Key,

                            ThreatSourceActorKey =
                                healer.Key,

                            Amount =
                                40m
                        }
                    ];
                }
            );

        var engine =
            new SimulationEngine(
                [
                    new CurrentTimeCombatEventDriver(
                        new CombatEvent
                        {
                            Type =
                                CombatEventType.Healing,

                            SourceActorKey =
                                healer.Key,

                            TargetActorKey =
                                ally.Key,

                            AbilityKey =
                                "test-heal",

                            Amount =
                                100m
                        }
                    ),

                    new ThreatManager(
                        resolver
                    )
                ]
            );

        var result =
            engine.Run(
                context
            );

        Assert.Equal(
            40m,
            bossA.ThreatTable.GetThreat(
                healer.Key
            )
        );

        Assert.Equal(
            40m,
            bossB.ThreatTable.GetThreat(
                healer.Key
            )
        );

        Assert.Equal(
            2,
            result.Timeline.Count(
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.ThreatChanged
            )
        );
    }

    [Fact]
    public void ThreatManager_RejectsUnknownThreatOwner()
    {
        var context =
            CreateContext(
                primaryActorKey:
                    "player"
            );

        var player =
            AddActor(
                context,
                "player",
                "raid"
            );

        var target =
            AddActor(
                context,
                "target",
                "enemy"
            );

        var resolver =
            new TestThreatResolver(
                _ =>
                [
                    new ThreatGenerationContribution
                    {
                        ThreatOwnerActorKey =
                            "missing-boss",

                        ThreatSourceActorKey =
                            player.Key,

                        Amount =
                            100m
                    }
                ]
            );

        var engine =
            new SimulationEngine(
                [
                    new CurrentTimeCombatEventDriver(
                        new CombatEvent
                        {
                            Type =
                                CombatEventType.Damage,

                            SourceActorKey =
                                player.Key,

                            TargetActorKey =
                                target.Key,

                            Amount =
                                10m
                        }
                    ),

                    new ThreatManager(
                        resolver
                    )
                ]
            );

        var exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    engine.Run(
                        context
                    )
            );

        Assert.Contains(
            "Threat owner actor 'missing-boss'",
            exception.Message
        );
    }

    private static SimulationContext CreateContext(
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

    private static SimulationActorState AddActor(
        SimulationContext context,
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
                1000m
        );

        context.AddActor(
            actor
        );

        return actor;
    }

    private sealed class TestThreatResolver :
        IThreatGenerationResolver
    {
        private readonly Func<
            CombatEvent,
            IReadOnlyList<ThreatGenerationContribution>>
            _resolve;

        public TestThreatResolver(
            Func<
                CombatEvent,
                IReadOnlyList<ThreatGenerationContribution>>
                resolve)
        {
            _resolve =
                resolve;
        }

        public IReadOnlyList<ThreatGenerationContribution> Resolve(
            SimulationContext context,
            CombatEvent combatEvent)
        {
            return _resolve(
                combatEvent
            );
        }
    }

    private sealed class CurrentTimeCombatEventDriver :
        ICombatEventProcessor
    {
        private readonly CombatEvent
            _combatEvent;

        public CurrentTimeCombatEventDriver(
            CombatEvent combatEvent)
        {
            _combatEvent =
                combatEvent;
        }

        public void Process(
            SimulationContext context,
            CombatEvent combatEvent)
        {
            if (
                combatEvent.Type !=
                    CombatEventType.SimulationStarted
            )
            {
                return;
            }

            _combatEvent.TimeSeconds =
                context.CurrentTimeSeconds;

            context.EmitEvent(
                _combatEvent
            );
        }
    }
}
}

// ==================== ThreatManipulationEffectTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Validation;

public sealed class ThreatManipulationEffectTests
{
    [Fact]
    public void ThreatTable_SetThreatCanLowerAndClearThreat()
    {
        var table =
            new ThreatTableState();

        table.AddThreat(
            "player",
            500m
        );

        Assert.Equal(
            125m,
            table.SetThreat(
                "player",
                125m
            )
        );

        Assert.Equal(
            125m,
            table.GetThreat(
                "PLAYER"
            )
        );

        table.SetThreat(
            "player",
            0m
        );

        Assert.Equal(
            0m,
            table.GetThreat(
                "player"
            )
        );

        Assert.Null(
            table.GetHighestThreatActorKey()
        );
    }

    [Fact]
    public void ThreatEffect_Add_IncreasesCasterThreatOnTarget()
    {
        var source =
            CreateActor(
                "tank",
                "raid"
            );

        var boss =
            CreateActor(
                "boss",
                "enemy"
            );

        boss.ThreatTable.AddThreat(
            source.Key,
            100m
        );

        source.AddAbility(
            CreateThreatAbility(
                "threat-strike",
                ThreatManipulationOperationTypes.Add,
                value:
                    75m
            )
        );

        var result =
            RunAbility(
                source,
                boss
            );

        Assert.Equal(
            175m,
            boss.ThreatTable.GetThreat(
                source.Key
            )
        );

        var threatEvent =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.ThreatChanged
            );

        Assert.Equal(
            75m,
            threatEvent.Amount
        );
    }

    [Fact]
    public void ThreatEffect_Set_CanResetCasterThreatToZero()
    {
        var source =
            CreateActor(
                "rogue",
                "raid"
            );

        var boss =
            CreateActor(
                "boss",
                "enemy"
            );

        boss.ThreatTable.AddThreat(
            source.Key,
            500m
        );

        source.AddAbility(
            CreateThreatAbility(
                "vanish-threat-reset",
                ThreatManipulationOperationTypes.Set,
                value:
                    0m
            )
        );

        var result =
            RunAbility(
                source,
                boss
            );

        Assert.Equal(
            0m,
            boss.ThreatTable.GetThreat(
                source.Key
            )
        );

        var threatEvent =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.ThreatChanged
            );

        Assert.Equal(
            -500m,
            threatEvent.Amount
        );
    }

    [Fact]
    public void ThreatEffect_MatchHighest_MatchesCurrentLivingThreatLeader()
    {
        var tank =
            CreateActor(
                "tank",
                "raid"
            );

        var damage =
            CreateActor(
                "damage",
                "raid"
            );

        var boss =
            CreateActor(
                "boss",
                "enemy"
            );

        boss.ThreatTable.AddThreat(
            tank.Key,
            100m
        );

        boss.ThreatTable.AddThreat(
            damage.Key,
            400m
        );

        tank.AddAbility(
            CreateThreatAbility(
                "match-threat",
                ThreatManipulationOperationTypes.MatchHighest
            )
        );

        RunAbility(
            tank,
            boss,
            [
                damage
            ]
        );

        Assert.Equal(
            400m,
            boss.ThreatTable.GetThreat(
                tank.Key
            )
        );
    }

    [Fact]
    public void ThreatEffect_MatchHighestPlus_AddsConfiguredAmountAboveLeader()
    {
        var tank =
            CreateActor(
                "tank",
                "raid"
            );

        var damage =
            CreateActor(
                "damage",
                "raid"
            );

        var boss =
            CreateActor(
                "boss",
                "enemy"
            );

        boss.ThreatTable.AddThreat(
            tank.Key,
            100m
        );

        boss.ThreatTable.AddThreat(
            damage.Key,
            400m
        );

        tank.AddAbility(
            CreateThreatAbility(
                "overtake-threat",
                ThreatManipulationOperationTypes.MatchHighestPlus,
                value:
                    25m
            )
        );

        RunAbility(
            tank,
            boss,
            [
                damage
            ]
        );

        Assert.Equal(
            425m,
            boss.ThreatTable.GetThreat(
                tank.Key
            )
        );

        Assert.Equal(
            tank.Key,
            boss.ThreatTable.GetHighestThreatActorKey()
        );
    }

    [Fact]
    public void ThreatEffect_MultiTargetSet_CanClearThreatAcrossEnemies()
    {
        var source =
            CreateActor(
                "rogue",
                "raid"
            );

        var bossA =
            CreateActor(
                "boss-a",
                "enemy"
            );

        var bossB =
            CreateActor(
                "boss-b",
                "enemy"
            );

        bossA.ThreatTable.AddThreat(
            source.Key,
            200m
        );

        bossB.ThreatTable.AddThreat(
            source.Key,
            300m
        );

        source.AddAbility(
            CreateThreatAbility(
                "multi-reset",
                ThreatManipulationOperationTypes.Set,
                value:
                    0m,
                maxTargets:
                    2
            )
        );

        var result =
            RunAbility(
                source,
                bossA,
                [
                    bossB
                ]
            );

        Assert.Equal(
            0m,
            bossA.ThreatTable.GetThreat(
                source.Key
            )
        );

        Assert.Equal(
            0m,
            bossB.ThreatTable.GetThreat(
                source.Key
            )
        );

        Assert.Equal(
            2,
            result.Timeline.Count(
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.ThreatChanged
            )
        );
    }

    [Fact]
    public void Validator_RejectsUnknownThreatManipulationOperation()
    {
        var source =
            CreateActor(
                "tank",
                "raid"
            );

        source.AddAbility(
            CreateThreatAbility(
                "invalid-threat",
                "invent-threat"
            )
        );

        var context =
            CreateContext(
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
                    "unknown threat operation 'invent-threat'",
                    StringComparison.OrdinalIgnoreCase
                )
        );
    }

    private static AbilityDefinition CreateThreatAbility(
        string key,
        string operation,
        decimal value = 0m,
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
                        AbilityEffectTypes.Threat,

                    TargetType =
                        AbilityTargetTypes.Enemy,

                    ThreatOperation =
                        operation,

                    MinimumValue =
                        value,

                    MaximumValue =
                        value,

                    MaxTargets =
                        maxTargets
                }
            ]
        };
    }

    private static SimulationRunResult RunAbility(
        SimulationActorState source,
        SimulationActorState primaryTarget,
        IReadOnlyList<SimulationActorState>? additionalActors = null)
    {
        var context =
            CreateContext(
                source.Key,
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

        var executor =
            new AbilityExecutor(
                new AuraManager(),
                new SimpleCombatRollResolver(),
                new RulesetDamageMitigationResolver(
                    new CombatRulesetDefinition
                    {
                        RulesetKey =
                            "threat-manipulation-tests",

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
                    var result =
                        executor.TryStartAbility(
                            startedContext,
                            source.Key,
                            primaryTarget.Key,
                            source.Abilities.Values
                                .Single()
                                .Definition.Key
                        );

                    Assert.True(
                        result.Success
                    );
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
                Seed =
                    12345,

                DurationSeconds =
                    1m,

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
