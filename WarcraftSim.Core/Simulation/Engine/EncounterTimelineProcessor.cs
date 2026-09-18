namespace WarcraftSim.Core.Simulation.Engine;

public sealed class EncounterTimelineProcessor :
    ICombatEventProcessor
{
    public void Process(
        SimulationContext context,
        CombatEvent combatEvent)
    {
        if (
            combatEvent.Type !=
            CombatEventType.SimulationStarted ||
            context.Encounter is null
        )
        {
            return;
        }

        foreach (
            var phase in
            context.Encounter.Phases)
        {
            var startTime =
                Math.Max(
                    0m,
                    phase.StartTimeSeconds
                );

            if (
                startTime <=
                context.Options.DurationSeconds
            )
            {
                context.ScheduleEvent(
                    new CombatEvent
                    {
                        TimeSeconds =
                            startTime,

                        Type =
                            CombatEventType.EncounterPhaseStarted,

                        EncounterPhaseKey =
                            phase.Key,

                        Description =
                            $"Encounter phase '{phase.Name}' started."
                    }
                );
            }

            if (
                phase.EndTimeSeconds.HasValue &&
                phase.EndTimeSeconds.Value >= 0m &&
                phase.EndTimeSeconds.Value <=
                    context.Options.DurationSeconds
            )
            {
                context.ScheduleEvent(
                    new CombatEvent
                    {
                        TimeSeconds =
                            phase.EndTimeSeconds.Value,

                        Type =
                            CombatEventType.EncounterPhaseEnded,

                        EncounterPhaseKey =
                            phase.Key,

                        Description =
                            $"Encounter phase '{phase.Name}' ended."
                    }
                );
            }
        }

        foreach (
            var damageEvent in
            context.Encounter.DamageEvents)
        {
            if (
                damageEvent.TimeSeconds < 0m ||
                damageEvent.TimeSeconds >
                    context.Options.DurationSeconds
            )
            {
                continue;
            }

            context.ScheduleEvent(
                new CombatEvent
                {
                    TimeSeconds =
                        damageEvent.TimeSeconds,

                    Type =
                        CombatEventType.ScriptedDamage,

                    SourceActorKey =
                        damageEvent.SourceActorKey,

                    TargetActorKey =
                        damageEvent.TargetActorKey,

                    EncounterEventKey =
                        damageEvent.Key,

                    SchoolKey =
                        damageEvent.SchoolKey,

                    RawAmount =
                        Math.Max(
                            0m,
                            damageEvent.Amount
                        ),

                    Amount =
                        Math.Max(
                            0m,
                            damageEvent.Amount
                        ),

                    IsInternal =
                        true,

                    Description =
                        damageEvent.Name
                }
            );
        }
    }
}
