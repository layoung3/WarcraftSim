namespace WarcraftSim.Core.Simulation.Engine;

public sealed class SimulationEngine
{
    public SimulationRunResult Run(
        SimulationContext context)
    {
        context.ScheduleEvent(
            new CombatEvent
            {
                TimeSeconds = 0m,
                Type = CombatEventType.SimulationStarted,
                Description = "Simulation started."
            }
        );

        context.ScheduleEvent(
            new CombatEvent
            {
                TimeSeconds =
                    context.Options.DurationSeconds,

                Type = CombatEventType.SimulationEnded,

                Description = "Simulation ended."
            }
        );

        while (
            context.TryGetNextEvent(
                out var combatEvent)
        )
        {
            if (combatEvent is null)
            {
                continue;
            }

            context.RecordEvent(combatEvent);

            if (
                combatEvent.Type ==
                CombatEventType.SimulationEnded
            )
            {
                break;
            }
        }

        return new SimulationRunResult
        {
            Summary = context.Summary,

            Timeline = context.Timeline
        };
    }
}