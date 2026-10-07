using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Encounters;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Validation;

namespace WarcraftSim.Tests;

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
