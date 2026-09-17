namespace WarcraftSim.Core.Simulation.Engine;

public sealed class SimulationEngine
{
    private readonly List<ICombatEventProcessor>
        _eventProcessors;

    public SimulationEngine(
        IEnumerable<ICombatEventProcessor>? eventProcessors = null)
    {
        _eventProcessors =
            eventProcessors?.ToList() ?? [];
    }

    public SimulationRunResult Run(
        SimulationContext context,
        Action<SimulationContext>? onSimulationStarted = null)
    {
        var simulationStartedEvent =
            new CombatEvent
            {
                TimeSeconds = 0m,
                Type =
                    CombatEventType.SimulationStarted,
                Description =
                    "Simulation started."
            };

        context.RecordEvent(
            simulationStartedEvent
        );

        foreach (
            var processor in
            _eventProcessors)
        {
            processor.Process(
                context,
                simulationStartedEvent
            );
        }

        onSimulationStarted?.Invoke(
            context
        );

        context.ScheduleEvent(
            new CombatEvent
            {
                TimeSeconds =
                    context.Options.DurationSeconds,

                Type =
                    CombatEventType.SimulationEnded,

                Description =
                    "Simulation ended."
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

            context.RecordEvent(
                combatEvent
            );

            foreach (
                var processor in
                _eventProcessors)
            {
                processor.Process(
                    context,
                    combatEvent
                );
            }

            if (
                combatEvent.Type ==
                CombatEventType.SimulationEnded)
            {
                break;
            }
        }

        return new SimulationRunResult
        {
            Summary =
                context.Summary,

            Timeline =
                context.Timeline
        };
    }
}