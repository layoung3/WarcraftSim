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
        // This is an additive, per-run timing adjustment: the existing actor
        // timing engine already applies InputDelaySeconds after casts/GCDs.
        // Do not delay boss event scheduling or auto attacks, and do not change
        // direct unit-test runs when no ambient latency has been configured.
        if (SimulationInputTimingScope.AdditionalDelaySeconds is { } addedDelay &&
            addedDelay > 0m)
        {
            foreach (var actor in context.Actors.Values)
            {
                if (string.Equals(actor.TeamKey, "enemy", StringComparison.OrdinalIgnoreCase))
                    continue;

                actor.ConfigureActionTiming(
                    actor.InitialActionDelaySeconds,
                    actor.InputDelaySeconds + addedDelay);
            }
        }

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

        long processedEvents = 0;
        // Only maintain the event histogram when the caller requests an event
        // budget. It is formatted on failure, not on the normal hot path.
        Dictionary<CombatEventType, long>? eventCounts =
            context.Options.MaximumProcessedEvents > 0 ? [] : null;
        while (
            context.TryGetNextEvent(
                out var combatEvent)
        )
        {
            if (combatEvent is null)
            {
                continue;
            }

            if ((++processedEvents & 255) == 0)
                context.Options.CancellationToken.ThrowIfCancellationRequested();

            if (eventCounts is not null)
            {
                eventCounts.TryGetValue(combatEvent.Type, out var oldCount);
                eventCounts[combatEvent.Type] = oldCount + 1;
            }
            if (context.Options.MaximumProcessedEvents > 0 &&
                processedEvents > context.Options.MaximumProcessedEvents)
            {
                var breakdown = eventCounts is null ? null : string.Join(", ",
                    eventCounts.OrderByDescending(pair => pair.Value)
                        .Take(6).Select(pair => $"{pair.Key}={pair.Value:N0}"));
                throw new SimulationEventLimitExceededException(
                    context.Options.MaximumProcessedEvents, context.CurrentTimeSeconds, breakdown);
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

        context.Options.CancellationToken.ThrowIfCancellationRequested();
        return new SimulationRunResult
        {
            ProcessedEventCount = processedEvents,
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
