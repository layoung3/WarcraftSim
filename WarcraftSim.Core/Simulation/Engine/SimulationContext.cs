using WarcraftSim.Core.Encounters;

namespace WarcraftSim.Core.Simulation.Engine;

public sealed class SimulationContext
{
    private readonly PriorityQueue<
        CombatEvent,
        (decimal TimeSeconds, long Sequence)
    > _eventQueue = new();

    private readonly Dictionary<string, Guid>
        _latestAbilityExecutionByActor =
            new(StringComparer.OrdinalIgnoreCase);

    private long _nextSequence;

    public SimulationRunOptions Options { get; }

    public EncounterProfile? Encounter { get; }

    public Random Random { get; }

    public decimal CurrentTimeSeconds { get; private set; }

    public Dictionary<string, SimulationActorState> Actors { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<Guid, AbilityExecutionState> AbilityExecutions { get; } =
        [];

    public List<CombatEvent> Timeline { get; } = [];

    public SimulationRunSummary Summary { get; }

    public SimulationContext(
        SimulationRunOptions options,
        EncounterProfile? encounter = null)
    {
        Options = options;
        Encounter = encounter;

        Random =
            new Random(
                options.Seed
            );

        Summary =
            new SimulationRunSummary
            {
                Seed =
                    options.Seed,

                DurationSeconds =
                    options.DurationSeconds
            };
    }

    public void AddActor(
        SimulationActorState actor)
    {
        Actors[
            actor.Key
        ] = actor;

        Summary.ActorSummaries[
            actor.Key
        ] =
            new ActorCombatSummary
            {
                ActorKey =
                    actor.Key,

                Name =
                    actor.Name
            };
    }

    public SimulationActorState? GetActor(
        string actorKey)
    {
        return Actors.TryGetValue(
            actorKey,
            out var actor)
                ? actor
                : null;
    }

    public void AddAbilityExecution(
        AbilityExecutionState execution)
    {
        AbilityExecutions[
            execution.Id
        ] = execution;

        if (!string.IsNullOrWhiteSpace(
                execution.SourceActorKey))
        {
            _latestAbilityExecutionByActor[
                execution.SourceActorKey
            ] = execution.Id;
        }
    }

    public Guid? GetLatestAbilityExecutionId(
        string actorKey)
    {
        return _latestAbilityExecutionByActor.TryGetValue(
            actorKey,
            out var executionId)
                ? executionId
                : null;
    }

    public AbilityExecutionState?
        GetAbilityExecution(
            Guid executionId)
    {
        return AbilityExecutions.TryGetValue(
            executionId,
            out var execution)
                ? execution
                : null;
    }

    public void CancelAbilityExecution(
        Guid executionId,
        decimal currentTimeSeconds)
    {
        var execution =
            GetAbilityExecution(
                executionId
            );

        execution?.Cancel(
            currentTimeSeconds
        );
    }

    public void RecordAbilityEffectResult(
        Guid executionId,
        string effectKey,
        CombatRollResult result)
    {
        var execution =
            GetAbilityExecution(
                executionId
            );

        if (execution is null)
        {
            return;
        }

        execution.EffectResults[
            effectKey
        ] = result;
    }

    public bool TryGetAbilityEffectResult(
        Guid executionId,
        string effectKey,
        out CombatRollResult? result)
    {
        result = null;

        var execution =
            GetAbilityExecution(
                executionId
            );

        if (execution is null)
        {
            return false;
        }

        if (!execution.EffectResults.TryGetValue(
                effectKey,
                out var storedResult))
        {
            return false;
        }

        result =
            storedResult;

        return true;
    }

    public void ScheduleEvent(
        CombatEvent combatEvent)
    {
        if (
            combatEvent.TimeSeconds <
            CurrentTimeSeconds
        )
        {
            throw new InvalidOperationException(
                "Cannot schedule an event in the past."
            );
        }

        if (
            combatEvent.TimeSeconds >
            Options.DurationSeconds
        )
        {
            return;
        }

        _eventQueue.Enqueue(
            combatEvent,
            (
                combatEvent.TimeSeconds,
                _nextSequence++
            )
        );
    }

    public bool TryGetNextEvent(
        out CombatEvent? combatEvent)
    {
        if (!_eventQueue.TryDequeue(
                out combatEvent,
                out _))
        {
            return false;
        }

        if (
            combatEvent!.Type ==
                CombatEventType.AbilityCastCompleted &&
            combatEvent.AbilityExecutionId.HasValue &&
            GetAbilityExecution(
                combatEvent.AbilityExecutionId.Value)
                is { IsCancelled: true }
        )
        {
            // Keep the queued event harmless without requiring event-queue
            // deletion. It becomes an internal cancellation event and will
            // not reach AbilityExecutor as a cast completion.
            combatEvent.Type =
                CombatEventType.AbilityCastCancelled;

            combatEvent.IsInternal =
                true;

            combatEvent.Description =
                "Cancelled cast completion ignored.";
        }

        CurrentTimeSeconds =
            combatEvent.TimeSeconds;

        foreach (
            var actor in
            Actors.Values)
        {
            actor.RefreshResources(
                CurrentTimeSeconds
            );
        }

        return true;
    }

    public void RecordEvent(
        CombatEvent combatEvent)
    {
        UpdateSummary(
            combatEvent
        );

        if (
            Options.CaptureTimeline &&
            !combatEvent.IsInternal
        )
        {
            Timeline.Add(
                combatEvent
            );
        }
    }

    private void UpdateSummary(
        CombatEvent combatEvent)
    {
        var amount =
            combatEvent.Amount ?? 0m;

        var overhealing =
            combatEvent.OverhealingAmount ?? 0m;

        var abilityKey =
            string.IsNullOrWhiteSpace(
                combatEvent.AbilityKey)
                ? "unknown"
                : combatEvent.AbilityKey;

        if (
            combatEvent.Type ==
            CombatEventType.Damage
        )
        {
            if (
                !string.IsNullOrWhiteSpace(
                    combatEvent.SourceActorKey) &&
                Summary.ActorSummaries.TryGetValue(
                    combatEvent.SourceActorKey,
                    out var sourceSummary)
            )
            {
                sourceSummary.DamageDone +=
                    amount;

                AddBreakdownValue(
                    sourceSummary.DamageDoneByAbility,
                    abilityKey,
                    amount
                );
            }

            if (
                !string.IsNullOrWhiteSpace(
                    combatEvent.TargetActorKey) &&
                Summary.ActorSummaries.TryGetValue(
                    combatEvent.TargetActorKey,
                    out var targetSummary)
            )
            {
                targetSummary.DamageTaken +=
                    amount;

                AddBreakdownValue(
                    targetSummary.DamageTakenByAbility,
                    abilityKey,
                    amount
                );
            }

            if (string.Equals(
                    combatEvent.SourceActorKey,
                    Options.PrimaryActorKey,
                    StringComparison.OrdinalIgnoreCase))
            {
                Summary.DamageDone +=
                    amount;

                AddBreakdownValue(
                    Summary.DamageDoneByAbility,
                    abilityKey,
                    amount
                );
            }

            if (string.Equals(
                    combatEvent.TargetActorKey,
                    Options.PrimaryActorKey,
                    StringComparison.OrdinalIgnoreCase))
            {
                Summary.DamageTaken +=
                    amount;

                AddBreakdownValue(
                    Summary.DamageTakenByAbility,
                    abilityKey,
                    amount
                );
            }
        }

        if (
            combatEvent.Type ==
            CombatEventType.Healing
        )
        {
            if (
                !string.IsNullOrWhiteSpace(
                    combatEvent.SourceActorKey) &&
                Summary.ActorSummaries.TryGetValue(
                    combatEvent.SourceActorKey,
                    out var sourceSummary)
            )
            {
                sourceSummary.HealingDone +=
                    amount;

                sourceSummary.OverhealingDone +=
                    overhealing;

                AddBreakdownValue(
                    sourceSummary.HealingDoneByAbility,
                    abilityKey,
                    amount
                );
            }

            if (
                !string.IsNullOrWhiteSpace(
                    combatEvent.TargetActorKey) &&
                Summary.ActorSummaries.TryGetValue(
                    combatEvent.TargetActorKey,
                    out var targetSummary)
            )
            {
                targetSummary.HealingReceived +=
                    amount;

                targetSummary.OverhealingReceived +=
                    overhealing;

                AddBreakdownValue(
                    targetSummary.HealingReceivedByAbility,
                    abilityKey,
                    amount
                );
            }

            if (string.Equals(
                    combatEvent.SourceActorKey,
                    Options.PrimaryActorKey,
                    StringComparison.OrdinalIgnoreCase))
            {
                Summary.HealingDone +=
                    amount;

                Summary.OverhealingDone +=
                    overhealing;

                AddBreakdownValue(
                    Summary.HealingDoneByAbility,
                    abilityKey,
                    amount
                );
            }

            if (string.Equals(
                    combatEvent.TargetActorKey,
                    Options.PrimaryActorKey,
                    StringComparison.OrdinalIgnoreCase))
            {
                Summary.HealingReceived +=
                    amount;

                Summary.OverhealingReceived +=
                    overhealing;

                AddBreakdownValue(
                    Summary.HealingReceivedByAbility,
                    abilityKey,
                    amount
                );
            }
        }

        if (
            combatEvent.Type ==
            CombatEventType.ActorDied
        )
        {
            if (
                !string.IsNullOrWhiteSpace(
                    combatEvent.TargetActorKey) &&
                Summary.ActorSummaries.TryGetValue(
                    combatEvent.TargetActorKey,
                    out var targetSummary)
            )
            {
                targetSummary.Died =
                    true;

                targetSummary.DeathTimeSeconds =
                    combatEvent.TimeSeconds;
            }

            if (string.Equals(
                    combatEvent.TargetActorKey,
                    Options.PrimaryActorKey,
                    StringComparison.OrdinalIgnoreCase))
            {
                Summary.PrimaryActorDied =
                    true;

                Summary.PrimaryActorDeathTimeSeconds =
                    combatEvent.TimeSeconds;
            }
        }
    }

    private static void AddBreakdownValue(
        Dictionary<string, decimal> breakdown,
        string key,
        decimal amount)
    {
        if (breakdown.TryGetValue(
                key,
                out var currentValue))
        {
            breakdown[
                key
            ] =
                currentValue +
                amount;

            return;
        }

        breakdown[
            key
        ] = amount;
    }
}
