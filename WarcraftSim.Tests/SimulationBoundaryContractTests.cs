using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests;

public sealed class SimulationBoundaryContractTests
{
    [Fact]
    public void Run_UsesHalfOpenGameplayWindow()
    {
        var context =
            new SimulationContext(
                new SimulationRunOptions
                {
                    Seed =
                        12345,

                    DurationSeconds =
                        1m,

                    PrimaryActorKey =
                        "player",

                    CaptureTimeline =
                        true
                }
            );

        context.AddActor(
            CreateActor(
                "player"
            )
        );

        context.AddActor(
            CreateActor(
                "target"
            )
        );

        var engine =
            new SimulationEngine(
                [
                    new BoundaryEventScheduler()
                ]
            );

        var result =
            engine.Run(
                context
            );

        Assert.Contains(
            result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                    CombatEventType.Damage &&
                combatEvent.TimeSeconds ==
                    0.999m
        );

        Assert.DoesNotContain(
            result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                    CombatEventType.Damage &&
                combatEvent.TimeSeconds ==
                    1m
        );

        var ended =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type ==
                    CombatEventType.SimulationEnded
            );

        Assert.Equal(
            1m,
            ended.TimeSeconds
        );

        Assert.Equal(
            10m,
            result.Summary.DamageDone
        );
    }

    private static SimulationActorState CreateActor(
        string key)
    {
        var actor =
            new SimulationActorState
            {
                Key =
                    key,

                Name =
                    key,

                TeamKey =
                    key == "player"
                        ? "raid"
                        : "enemy"
            };

        actor.InitializeHealth(
            maximumHealth:
                1000m
        );

        return actor;
    }

    private sealed class BoundaryEventScheduler :
        ICombatEventProcessor
    {
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

            context.ScheduleEvent(
                new CombatEvent
                {
                    TimeSeconds =
                        0.999m,

                    Type =
                        CombatEventType.Damage,

                    SourceActorKey =
                        "player",

                    TargetActorKey =
                        "target",

                    Amount =
                        10m,

                    Description =
                        "Damage immediately before the simulation boundary."
                }
            );

            context.ScheduleEvent(
                new CombatEvent
                {
                    TimeSeconds =
                        1m,

                    Type =
                        CombatEventType.Damage,

                    SourceActorKey =
                        "player",

                    TargetActorKey =
                        "target",

                    Amount =
                        100m,

                    Description =
                        "Damage exactly at the simulation boundary."
                }
            );
        }
    }
}
