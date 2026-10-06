using WarcraftSim.Core.Simulation.Validation;

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
        SimulationDefinitionValidator.Validate(
            context,
            _eventProcessors
        );

        var simulationStartedEvent =
            new CombatEvent
            {
                TimeSeconds = 0m,
                Type =
                    CombatEventType.SimulationStarted,
                Description =
                    "Simulation started."
            };

        DispatchEvent(
            context,
            simulationStartedEvent
        );

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

            DispatchEvent(
                context,
                combatEvent
            );

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

    private void DispatchEvent(
        SimulationContext context,
        CombatEvent combatEvent)
    {
        context.RecordDispatchedEvent(
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
    }
}
