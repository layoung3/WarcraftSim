using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Validation;

namespace WarcraftSim.Tests;

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
