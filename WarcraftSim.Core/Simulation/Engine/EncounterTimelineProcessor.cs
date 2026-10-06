using WarcraftSim.Core.Encounters;

namespace WarcraftSim.Core.Simulation.Engine;

public sealed class EncounterTimelineProcessor : ICombatEventProcessor
{
    public void Process(
        SimulationContext context,
        CombatEvent combatEvent)
    {
        if (context.Encounter is null)
        {
            return;
        }

        switch (combatEvent.Type)
        {
            case CombatEventType.SimulationStarted:
                SchedulePhases(context, context.Encounter);
                ScheduleOneOffDamage(context, context.Encounter);
                ScheduleDamagePatterns(context, context.Encounter);
                break;

            case CombatEventType.EncounterDamagePatternOccurrence:
                ProcessDamagePatternOccurrence(
                    context,
                    context.Encounter,
                    combatEvent
                );
                break;

            case CombatEventType.EncounterDamageSequenceHit:
                ProcessDamageSequenceHit(
                    context,
                    context.Encounter,
                    combatEvent
                );
                break;
        }
    }

    private static void SchedulePhases(
        SimulationContext context,
        EncounterProfile encounter)
    {
        foreach (var phase in encounter.Phases)
        {
            var startTime = Math.Max(0m, phase.StartTimeSeconds);

            if (startTime <= context.Options.DurationSeconds)
            {
                context.ScheduleEvent(
                    new CombatEvent
                    {
                        TimeSeconds = startTime,
                        Type = CombatEventType.EncounterPhaseStarted,
                        EncounterPhaseKey = phase.Key,
                        Description = $"Encounter phase '{phase.Name}' started."
                    }
                );
            }

            if (
                phase.EndTimeSeconds.HasValue &&
                phase.EndTimeSeconds.Value >= 0m &&
                phase.EndTimeSeconds.Value <= context.Options.DurationSeconds
            )
            {
                context.ScheduleEvent(
                    new CombatEvent
                    {
                        TimeSeconds = phase.EndTimeSeconds.Value,
                        Type = CombatEventType.EncounterPhaseEnded,
                        EncounterPhaseKey = phase.Key,
                        Description = $"Encounter phase '{phase.Name}' ended."
                    }
                );
            }
        }
    }

    private static void ScheduleOneOffDamage(
        SimulationContext context,
        EncounterProfile encounter)
    {
        foreach (var damageEvent in encounter.DamageEvents)
        {
            if (
                damageEvent.TimeSeconds < 0m ||
                damageEvent.TimeSeconds > context.Options.DurationSeconds
            )
            {
                continue;
            }

            ScheduleScriptedDamage(
                context,
                damageEvent.TimeSeconds,
                damageEvent.SourceActorKey,
                damageEvent.TargetActorKey,
                damageEvent.Key,
                damageEvent.Name,
                damageEvent.Amount,
                damageEvent.SchoolKey,
                damageEvent.MitigationType
            );
        }
    }

    private static void ScheduleDamagePatterns(
        SimulationContext context,
        EncounterProfile encounter)
    {
        foreach (var pattern in encounter.DamagePatterns)
        {
            ValidatePattern(pattern);

            var firstTime = Math.Max(0m, pattern.StartTimeSeconds);
            var lastTime = Math.Min(
                context.Options.DurationSeconds,
                pattern.EndTimeSeconds ?? context.Options.DurationSeconds
            );

            if (firstTime > lastTime)
            {
                continue;
            }

            var occurrence = 1;

            for (
                var time = firstTime;
                time <= lastTime;
                time += pattern.IntervalSeconds)
            {
                context.ScheduleEvent(
                    new CombatEvent
                    {
                        TimeSeconds = time,
                        Type = CombatEventType.EncounterDamagePatternOccurrence,
                        SourceActorKey = pattern.SourceActorKey,
                        EncounterEventKey = pattern.Key,
                        IsInternal = true,
                        Description = $"{pattern.Name} #{occurrence}"
                    }
                );

                occurrence++;
            }
        }
    }

    private static void ValidatePattern(
        EncounterDamagePatternDefinition pattern)
    {
        if (pattern.IntervalSeconds <= 0m)
        {
            throw new InvalidOperationException(
                $"Encounter damage pattern '{pattern.Key}' must have an interval greater than zero."
            );
        }

        if (pattern.Sequence is not null && pattern.Sequence.HitCount <= 0)
        {
            throw new InvalidOperationException(
                $"Encounter damage pattern '{pattern.Key}' must have a sequence hit count greater than zero."
            );
        }

        if (
            pattern.Sequence is not null &&
            pattern.Sequence.HitIntervalSeconds < 0m
        )
        {
            throw new InvalidOperationException(
                $"Encounter damage pattern '{pattern.Key}' cannot have a negative sequence hit interval."
            );
        }

        if (
            pattern.Sequence is not null &&
            !string.Equals(
                pattern.Sequence.SelectionHistoryMode,
                EncounterSequenceSelectionHistoryModes.AllowRepeats,
                StringComparison.OrdinalIgnoreCase
            ) &&
            !string.Equals(
                pattern.Sequence.SelectionHistoryMode,
                EncounterSequenceSelectionHistoryModes.UniqueAcrossSequence,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            throw new InvalidOperationException(
                $"Unknown encounter sequence selection history mode '{pattern.Sequence.SelectionHistoryMode}'."
            );
        }
    }

    private static void ProcessDamagePatternOccurrence(
        SimulationContext context,
        EncounterProfile encounter,
        CombatEvent occurrenceEvent)
    {
        var pattern = FindPattern(encounter, occurrenceEvent.EncounterEventKey);

        if (pattern is null)
        {
            return;
        }

        var sequence = pattern.Sequence ?? new EncounterDamageSequenceDefinition();

        switch (sequence.TargetMode)
        {
            case EncounterDamageSequenceTargetModes.SameSelection:
                ScheduleSameSelectionSequence(
                    context,
                    pattern,
                    occurrenceEvent,
                    sequence
                );
                break;

            case EncounterDamageSequenceTargetModes.SequentialSelection:
                ScheduleSequentialSelectionSequence(
                    context,
                    pattern,
                    occurrenceEvent,
                    sequence
                );
                break;

            case EncounterDamageSequenceTargetModes.ReselectEachHit:
                ScheduleFirstReselectHit(context, pattern, occurrenceEvent);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown encounter damage sequence target mode '{sequence.TargetMode}'."
                );
        }
    }

    private static void ScheduleSameSelectionSequence(
        SimulationContext context,
        EncounterDamagePatternDefinition pattern,
        CombatEvent occurrenceEvent,
        EncounterDamageSequenceDefinition sequence)
    {
        var targets = EncounterTargetSelector.Resolve(context, pattern);

        for (var hitIndex = 0; hitIndex < sequence.HitCount; hitIndex++)
        {
            var hitTime = GetHitTime(
                context.CurrentTimeSeconds,
                sequence,
                hitIndex
            );

            if (hitTime > context.Options.DurationSeconds)
            {
                break;
            }

            foreach (var target in targets)
            {
                ScheduleLockedSequenceHit(
                    context,
                    pattern,
                    occurrenceEvent,
                    target.Key,
                    hitTime,
                    hitIndex + 1
                );
            }
        }
    }

    private static void ScheduleSequentialSelectionSequence(
        SimulationContext context,
        EncounterDamagePatternDefinition pattern,
        CombatEvent occurrenceEvent,
        EncounterDamageSequenceDefinition sequence)
    {
        var targets = EncounterTargetSelector.Resolve(context, pattern);

        if (targets.Count == 0)
        {
            return;
        }

        var hitsToSchedule = Math.Min(sequence.HitCount, targets.Count);

        for (var hitIndex = 0; hitIndex < hitsToSchedule; hitIndex++)
        {
            var hitTime = GetHitTime(
                context.CurrentTimeSeconds,
                sequence,
                hitIndex
            );

            if (hitTime > context.Options.DurationSeconds)
            {
                break;
            }

            ScheduleLockedSequenceHit(
                context,
                pattern,
                occurrenceEvent,
                targets[hitIndex].Key,
                hitTime,
                hitIndex + 1
            );
        }
    }

    private static void ScheduleFirstReselectHit(
        SimulationContext context,
        EncounterDamagePatternDefinition pattern,
        CombatEvent occurrenceEvent)
    {
        context.ScheduleEvent(
            new CombatEvent
            {
                TimeSeconds = context.CurrentTimeSeconds,
                Type = CombatEventType.EncounterDamageSequenceHit,
                SourceActorKey = pattern.SourceActorKey,
                EncounterEventKey = pattern.Key,
                EncounterSequenceHitNumber = 1,
                EncounterSequenceSelectedActorKeys = [],
                IsInternal = true,
                Description = occurrenceEvent.Description ?? pattern.Name
            }
        );
    }

    private static void ScheduleLockedSequenceHit(
        SimulationContext context,
        EncounterDamagePatternDefinition pattern,
        CombatEvent occurrenceEvent,
        string targetActorKey,
        decimal hitTime,
        int hitNumber)
    {
        context.ScheduleEvent(
            new CombatEvent
            {
                TimeSeconds = hitTime,
                Type = CombatEventType.EncounterDamageSequenceHit,
                SourceActorKey = pattern.SourceActorKey,
                TargetActorKey = targetActorKey,
                EncounterEventKey = pattern.Key,
                EncounterSequenceHitNumber = hitNumber,
                IsInternal = true,
                Description = occurrenceEvent.Description ?? pattern.Name
            }
        );
    }

    private static void ProcessDamageSequenceHit(
        SimulationContext context,
        EncounterProfile encounter,
        CombatEvent sequenceHitEvent)
    {
        var pattern = FindPattern(encounter, sequenceHitEvent.EncounterEventKey);

        if (pattern is null)
        {
            return;
        }

        var sequence = pattern.Sequence ?? new EncounterDamageSequenceDefinition();
        var hitNumber = sequenceHitEvent.EncounterSequenceHitNumber ?? 1;

        if (
            string.Equals(
                sequence.TargetMode,
                EncounterDamageSequenceTargetModes.ReselectEachHit,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            ProcessReselectedHit(
                context,
                pattern,
                sequence,
                sequenceHitEvent,
                hitNumber
            );
            return;
        }

        ProcessLockedHit(
            context,
            pattern,
            sequenceHitEvent,
            hitNumber
        );
    }

    private static void ProcessReselectedHit(
        SimulationContext context,
        EncounterDamagePatternDefinition pattern,
        EncounterDamageSequenceDefinition sequence,
        CombatEvent sequenceHitEvent,
        int hitNumber)
    {
        var uniqueAcrossSequence =
            string.Equals(
                sequence.SelectionHistoryMode,
                EncounterSequenceSelectionHistoryModes.UniqueAcrossSequence,
                StringComparison.OrdinalIgnoreCase
            );

        var previousSelections =
            sequenceHitEvent.EncounterSequenceSelectedActorKeys;

        var targets =
            EncounterTargetSelector.Resolve(
                context,
                pattern,
                excludedActorKeys:
                    uniqueAcrossSequence
                        ? previousSelections
                        : null,
                forceUniqueTargets:
                    uniqueAcrossSequence
            );

        foreach (var target in targets)
        {
            ScheduleScriptedDamage(
                context,
                context.CurrentTimeSeconds,
                pattern.SourceActorKey,
                target.Key,
                pattern.Key,
                BuildHitDescription(
                    sequenceHitEvent.Description ?? pattern.Name,
                    sequence,
                    hitNumber
                ),
                pattern.Amount,
                pattern.SchoolKey,
                pattern.MitigationType
            );
        }

        if (hitNumber >= sequence.HitCount)
        {
            return;
        }

        var nextHitTime =
            context.CurrentTimeSeconds + sequence.HitIntervalSeconds;

        if (nextHitTime > context.Options.DurationSeconds)
        {
            return;
        }

        var selectionHistory = previousSelections.ToList();

        if (uniqueAcrossSequence)
        {
            foreach (var target in targets)
            {
                if (
                    !selectionHistory.Contains(
                        target.Key,
                        StringComparer.OrdinalIgnoreCase
                    )
                )
                {
                    selectionHistory.Add(target.Key);
                }
            }
        }

        context.ScheduleEvent(
            new CombatEvent
            {
                TimeSeconds = nextHitTime,
                Type = CombatEventType.EncounterDamageSequenceHit,
                SourceActorKey = pattern.SourceActorKey,
                EncounterEventKey = pattern.Key,
                EncounterSequenceHitNumber = hitNumber + 1,
                EncounterSequenceSelectedActorKeys = selectionHistory,
                IsInternal = true,
                Description = sequenceHitEvent.Description ?? pattern.Name
            }
        );
    }

    private static void ProcessLockedHit(
        SimulationContext context,
        EncounterDamagePatternDefinition pattern,
        CombatEvent sequenceHitEvent,
        int hitNumber)
    {
        if (string.IsNullOrWhiteSpace(sequenceHitEvent.TargetActorKey))
        {
            return;
        }

        var lockedTarget = context.GetActor(sequenceHitEvent.TargetActorKey);

        if (lockedTarget is null || !lockedTarget.IsAlive)
        {
            return;
        }

        var sequence = pattern.Sequence ?? new EncounterDamageSequenceDefinition();

        ScheduleScriptedDamage(
            context,
            context.CurrentTimeSeconds,
            pattern.SourceActorKey,
            lockedTarget.Key,
            pattern.Key,
            BuildHitDescription(
                sequenceHitEvent.Description ?? pattern.Name,
                sequence,
                hitNumber
            ),
            pattern.Amount,
            pattern.SchoolKey,
            pattern.MitigationType
        );
    }

    private static EncounterDamagePatternDefinition? FindPattern(
        EncounterProfile encounter,
        string? patternKey)
    {
        if (string.IsNullOrWhiteSpace(patternKey))
        {
            return null;
        }

        return encounter.DamagePatterns.FirstOrDefault(
            candidate =>
                string.Equals(
                    candidate.Key,
                    patternKey,
                    StringComparison.OrdinalIgnoreCase
                )
        );
    }

    private static decimal GetHitTime(
        decimal occurrenceTime,
        EncounterDamageSequenceDefinition sequence,
        int zeroBasedHitIndex)
    {
        return occurrenceTime +
            (sequence.HitIntervalSeconds * zeroBasedHitIndex);
    }

    private static string BuildHitDescription(
        string baseDescription,
        EncounterDamageSequenceDefinition sequence,
        int hitNumber)
    {
        return sequence.HitCount <= 1
            ? baseDescription
            : $"{baseDescription}, hit {hitNumber}";
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
                TimeSeconds = timeSeconds,
                Type = CombatEventType.ScriptedDamage,
                SourceActorKey = sourceActorKey,
                TargetActorKey = targetActorKey,
                EncounterEventKey = encounterEventKey,
                SchoolKey = schoolKey,
                MitigationType = mitigationType,
                RawAmount = Math.Max(0m, amount),
                Amount = Math.Max(0m, amount),
                IsInternal = true,
                Description = name
            }
        );
    }
}
