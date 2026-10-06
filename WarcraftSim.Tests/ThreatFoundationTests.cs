using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests;

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
