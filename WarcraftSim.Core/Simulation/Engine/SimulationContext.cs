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

    private readonly Dictionary<string, decimal>
        _resourceStarvationStartedAt =
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

        var actorSummary =
            new ActorCombatSummary
            {
                ActorKey =
                    actor.Key,

                Name =
                    actor.Name
            };

        foreach (
            var resource in
            actor.Resources.Values)
        {
            actorSummary.StartingResources[
                resource.ResourceKey
            ] = resource.Current;
        }

        Summary.ActorSummaries[
            actor.Key
        ] = actorSummary;
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

    public void BeginResourceStarvation(
        string actorKey,
        decimal currentTimeSeconds)
    {
        if (_resourceStarvationStartedAt.ContainsKey(
                actorKey))
        {
            return;
        }

        _resourceStarvationStartedAt[
            actorKey
        ] = currentTimeSeconds;

        if (Summary.ActorSummaries.TryGetValue(
                actorKey,
                out var actorSummary))
        {
            actorSummary.FirstResourceStarvedAtSeconds ??=
                currentTimeSeconds;
        }
    }

    public void EndResourceStarvation(
        string actorKey,
        decimal currentTimeSeconds)
    {
        if (!_resourceStarvationStartedAt.TryGetValue(
                actorKey,
                out var startedAt))
        {
            return;
        }

        _resourceStarvationStartedAt.Remove(
            actorKey
        );

        if (Summary.ActorSummaries.TryGetValue(
                actorKey,
                out var actorSummary))
        {
            actorSummary.ResourceStarvedSeconds +=
                Math.Max(
                    0m,
                    currentTimeSeconds -
                    startedAt
                );
        }
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
        if (
            combatEvent.Type ==
            CombatEventType.SimulationEnded
        )
        {
            FinalizeActorRuntimeMetrics(
                combatEvent.TimeSeconds
            );
        }

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

    private void FinalizeActorRuntimeMetrics(
        decimal currentTimeSeconds)
    {
        foreach (
            var actorKey in
            _resourceStarvationStartedAt.Keys.ToList())
        {
            EndResourceStarvation(
                actorKey,
                currentTimeSeconds
            );
        }

        foreach (
            var actor in
            Actors.Values)
        {
            actor.RefreshResources(
                currentTimeSeconds
            );

            if (!Summary.ActorSummaries.TryGetValue(
                    actor.Key,
                    out var actorSummary))
            {
                continue;
            }

            actorSummary.EndingResources.Clear();

            foreach (
                var resource in
                actor.Resources.Values)
            {
                actorSummary.EndingResources[
                    resource.ResourceKey
                ] = resource.Current;
            }
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
