using WarcraftSim.Core.Encounters;

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

        SchedulePhases(
            context,
            context.Encounter
        );

        ScheduleOneOffDamage(
            context,
            context.Encounter
        );

        ScheduleDamagePatterns(
            context,
            context.Encounter
        );
    }

    private static void SchedulePhases(
        SimulationContext context,
        EncounterProfile encounter)
    {
        foreach (
            var phase in
            encounter.Phases)
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
    }

    private static void ScheduleOneOffDamage(
        SimulationContext context,
        EncounterProfile encounter)
    {
        foreach (
            var damageEvent in
            encounter.DamageEvents)
        {
            if (
                damageEvent.TimeSeconds < 0m ||
                damageEvent.TimeSeconds >
                    context.Options.DurationSeconds
            )
            {
                continue;
            }

            ScheduleScriptedDamage(
                context,
                timeSeconds:
                    damageEvent.TimeSeconds,
                sourceActorKey:
                    damageEvent.SourceActorKey,
                targetActorKey:
                    damageEvent.TargetActorKey,
                encounterEventKey:
                    damageEvent.Key,
                name:
                    damageEvent.Name,
                amount:
                    damageEvent.Amount,
                schoolKey:
                    damageEvent.SchoolKey,
                mitigationType:
                    damageEvent.MitigationType
            );
        }
    }

    private static void ScheduleDamagePatterns(
        SimulationContext context,
        EncounterProfile encounter)
    {
        foreach (
            var pattern in
            encounter.DamagePatterns)
        {
            if (pattern.IntervalSeconds <= 0m)
            {
                throw new InvalidOperationException(
                    $"Encounter damage pattern '{pattern.Key}' must have an interval greater than zero."
                );
            }

            var firstTime =
                Math.Max(
                    0m,
                    pattern.StartTimeSeconds
                );

            var lastTime =
                Math.Min(
                    context.Options.DurationSeconds,
                    pattern.EndTimeSeconds ??
                        context.Options.DurationSeconds
                );

            if (firstTime > lastTime)
            {
                continue;
            }

            var occurrence =
                1;

            for (
                var time =
                    firstTime;
                time <= lastTime;
                time +=
                    pattern.IntervalSeconds)
            {
                ScheduleScriptedDamage(
                    context,
                    timeSeconds:
                        time,
                    sourceActorKey:
                        pattern.SourceActorKey,
                    targetActorKey:
                        pattern.TargetActorKey,
                    encounterEventKey:
                        pattern.Key,
                    name:
                        $"{pattern.Name} #{occurrence}",
                    amount:
                        pattern.Amount,
                    schoolKey:
                        pattern.SchoolKey,
                    mitigationType:
                        pattern.MitigationType
                );

                occurrence++;
            }
        }
    }

    private static void ScheduleScriptedDamage(
        SimulationContext context,
        decimal timeSeconds,
        string? sourceActorKey,
        string targetActorKey,
        string encounterEventKey,
        string name,
        decimal amount,
        string? schoolKey,
        string mitigationType)
    {
        context.ScheduleEvent(
            new CombatEvent
            {
                TimeSeconds =
                    timeSeconds,

                Type =
                    CombatEventType.ScriptedDamage,

                SourceActorKey =
                    sourceActorKey,

                TargetActorKey =
                    targetActorKey,

                EncounterEventKey =
                    encounterEventKey,

                SchoolKey =
                    schoolKey,

                MitigationType =
                    mitigationType,

                RawAmount =
                    Math.Max(
                        0m,
                        amount
                    ),

                Amount =
                    Math.Max(
                        0m,
                        amount
                    ),

                IsInternal =
                    true,

                Description =
                    name
            }
        );
    }
}
