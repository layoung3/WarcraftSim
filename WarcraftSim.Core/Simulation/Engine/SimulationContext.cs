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

    // Only configured for timed single-character simulations. Raid simulations
    // omit scripts and always use actual boss health to determine phases.
    private readonly Dictionary<string, ScriptedTargetHealthTimeline> _scriptedTargetHealth =
        new(StringComparer.OrdinalIgnoreCase);

    // Multiple combat processors can request the same ordinary rotation
    // wake-up. Keep only one pending decision per actor/target/time; named
    // internal effect ticks (e.g. Anger Management) must remain distinct.
    // Entries are removed at dequeue, so a later state mutation at the same
    // timestamp may schedule another legitimate reevaluation.
    private readonly HashSet<(decimal Time, string Source, string Target)>
        _pendingOrdinaryRotationDecisions = [];

    private static bool TryOrdinaryRotationKey(
        CombatEvent combatEvent,
        out (decimal Time, string Source, string Target) key)
    {
        if (combatEvent.Type == CombatEventType.RotationDecision &&
            combatEvent.EffectKey is null &&
            !string.IsNullOrWhiteSpace(combatEvent.SourceActorKey))
        {
            key = (combatEvent.TimeSeconds,
                combatEvent.SourceActorKey,
                combatEvent.TargetActorKey ?? string.Empty);
            return true;
        }
        key = default;
        return false;
    }

    private long _nextSequence;

    public SimulationRunOptions Options { get; }

    public EncounterProfile? Encounter { get; }

    public Random Random { get; }

    // Separate deterministic RNG stream for encounter targeting.
    // This keeps random boss target selection from changing the
    // combat-roll RNG sequence.
    public Random EncounterRandom { get; }

    public decimal CurrentTimeSeconds { get; private set; }

    // Effects resolve against their ORIGINAL cast execution. A next cast may
    // already be active at the same timestamp. This per-simulation scope
    // allows proc/crit handlers to read the correct cast ownership.
    public Guid? CurrentResolvingAbilityExecutionId { get; internal set; }

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

        EncounterRandom =
            new Random(
                options.Seed ^ 0x005A17C3
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
        ArgumentNullException.ThrowIfNull(
            actor
        );

        if (string.IsNullOrWhiteSpace(
                actor.Key))
        {
            throw new ArgumentException(
                "Simulation actors require a key.",
                nameof(actor)
            );
        }

        if (Actors.ContainsKey(
                actor.Key))
        {
            throw new InvalidOperationException(
                $"Duplicate simulation actor key '{actor.Key}'."
            );
        }

        Actors.Add(
            actor.Key,
            actor
        );

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

    public void SetScriptedTargetHealth(string actorKey, ScriptedTargetHealthTimeline timeline)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        if (string.IsNullOrWhiteSpace(actorKey) || GetActor(actorKey) is null)
            throw new ArgumentException("Scripted health requires an existing actor key.", nameof(actorKey));
        if (timeline.Checkpoints.Any(point => point.TimeSeconds >= Options.DurationSeconds))
            throw new ArgumentException("All scripted health checkpoints must occur before simulation end.", nameof(timeline));
        _scriptedTargetHealth[actorKey] = timeline;
    }

    public decimal GetHealthPercentForConditions(SimulationActorState actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (_scriptedTargetHealth.TryGetValue(actor.Key, out var timeline))
            return timeline.GetHealthPercentAt(CurrentTimeSeconds);
        return actor.MaximumHealth > 0m ?
            actor.CurrentHealth * 100m / actor.MaximumHealth : 0m;
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
        CombatRollResult result,
        string? targetActorKey = null)
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
        if(!string.IsNullOrWhiteSpace(targetActorKey))
            execution.EffectResultsByTarget[
                (effectKey.ToLowerInvariant(),targetActorKey.ToLowerInvariant())]=result;
    }

    public bool TryGetAbilityEffectResult(
        Guid executionId,
        string effectKey,
        out CombatRollResult? result,
        string? targetActorKey = null)
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

        // Once a dependency has any target-specific results, never fall
        // back to another target's last result. Legacy single-target callers
        // that recorded no target still use the previous effect-wide slot.
        if(!string.IsNullOrWhiteSpace(targetActorKey) &&
           execution.EffectResultsByTarget.Count>0)
        {
            if(!execution.EffectResultsByTarget.TryGetValue(
                (effectKey.ToLowerInvariant(),targetActorKey.ToLowerInvariant()),
                out var targetResult))
            {
                if(execution.EffectResultsByTarget.Keys.Any(key =>
                    key.Effect==effectKey.ToLowerInvariant()))
                {
                    // Self-targeted reactive effects (dodge/block/glance
                    // rewards, Bloodthirst's self buff) depend on the
                    // single enemy-targeted impact. Only multi-target
                    // impacts require an exact per-target match.
                    var source=GetActor(execution.SourceActorKey);
                    if(source is null ||
                       !source.Abilities.TryGetValue(execution.AbilityKey,
                           out var abilityState) ||
                       abilityState.Definition.Effects.FirstOrDefault(e=>
                           string.Equals(e.Key,effectKey,
                               StringComparison.OrdinalIgnoreCase))
                           ?.MaxTargets!=1)
                        return false;
                }
            }
            else
            {
                result=targetResult;
                return true;
            }
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

    public void EmitEvent(
        CombatEvent combatEvent)
    {
        ArgumentNullException.ThrowIfNull(
            combatEvent
        );

        if (
            combatEvent.TimeSeconds !=
            CurrentTimeSeconds
        )
        {
            throw new InvalidOperationException(
                "Emitted gameplay events must occur at the current simulation time."
            );
        }

        // Resource changes are emitted after mutating the resource. Capture the
        // balance now, not on later event dispatch (multiple changes may queue
        // at the same timestamp). Generic single-resource actors need no extra
        // annotations; multi-resource actors can specify ResourceKey explicitly.
        if (combatEvent.Type == CombatEventType.ResourceChanged &&
            combatEvent.ResourceTotalAfter is null)
        {
            var actor = combatEvent.TargetActorKey is { } targetKey
                ? GetActor(targetKey) : null;
            if (actor is null || actor.Resources.Count == 0)
                actor = combatEvent.SourceActorKey is { } sourceKey
                    ? GetActor(sourceKey) : null;
            if (actor is not null)
            {
                var resource = combatEvent.ResourceKey is { } key &&
                    actor.Resources.TryGetValue(key, out var named)
                    ? named : actor.Resources.Count == 1
                        ? actor.Resources.Values.Single() : null;
                if (resource is not null)
                {
                    combatEvent.ResourceKey ??= resource.ResourceKey;
                    combatEvent.ResourceTotalAfter = resource.Current;
                }
            }
        }
        ScheduleEvent(
            combatEvent
        );
    }

    public void ScheduleEvent(
        CombatEvent combatEvent)
    {
        ArgumentNullException.ThrowIfNull(
            combatEvent
        );

        if (
            combatEvent.TimeSeconds <
            CurrentTimeSeconds
        )
        {
            throw new InvalidOperationException(
                "Cannot schedule an event in the past."
            );
        }

        // Simulation gameplay uses a half-open time window:
        // [0, DurationSeconds). SimulationEnded is the sole event allowed
        // at the exact ending timestamp.
        if (
            combatEvent.Type !=
                CombatEventType.SimulationEnded &&
            combatEvent.TimeSeconds >=
                Options.DurationSeconds
        )
        {
            return;
        }

        if (
            combatEvent.Type ==
                CombatEventType.SimulationEnded &&
            combatEvent.TimeSeconds >
                Options.DurationSeconds
        )
        {
            return;
        }

        if (TryOrdinaryRotationKey(combatEvent, out var pendingKey) &&
            !_pendingOrdinaryRotationDecisions.Add(pendingKey))
            return;

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
        // Re-queue outdated completion timestamps without dispatching them.
        // Unlike processing the completion and bailing out afterward, this
        // keeps the event timeline, rotation and cast summaries correct.
        while (true)
        {
            if (!_eventQueue.TryDequeue(out combatEvent, out _))
                return false;
            // Duplicated or superseded completion events must not count as
            // extra casts in the timeline or actor summaries.
            if(combatEvent.Type==CombatEventType.AbilityCastCompleted &&
               combatEvent.AbilityExecutionId is Guid completedId &&
               GetAbilityExecution(completedId)?.CastCompletionProcessed==true)
                continue;
            if (combatEvent.Type == CombatEventType.AbilityCastCompleted &&
                combatEvent.AbilityExecutionId.HasValue &&
                GetAbilityExecution(combatEvent.AbilityExecutionId.Value) is
                    { IsCancelled: false } execution &&
                execution.Timing.CastTimeSeconds > 0m &&
                combatEvent.TimeSeconds < execution.CastCompletionDueAtSeconds)
            {
                combatEvent.TimeSeconds = execution.CastCompletionDueAtSeconds;
                ScheduleEvent(combatEvent);
                continue;
            }
            break;
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

        // Release the queued-decision key BEFORE dispatch. If an earlier
        // event changes state later in the same timestamp, the rotation
        // executor may legitimately schedule another decision at that time.
        if (TryOrdinaryRotationKey(combatEvent, out var pendingKey))
            _pendingOrdinaryRotationDecisions.Remove(pendingKey);

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

    internal void RecordDispatchedEvent(
        CombatEvent combatEvent)
    {
        ArgumentNullException.ThrowIfNull(
            combatEvent
        );

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

        var blocked =
            combatEvent.BlockedAmount ?? 0m;

        var abilityKey =
            string.IsNullOrWhiteSpace(
                combatEvent.AbilityKey)
                ? "unknown"
                : combatEvent.AbilityKey;

        // Self-inflicted damage remains DamageTaken, never outgoing
        // enemy DPS, including Hellfire caster ticks.
        if (combatEvent.Type != CombatEventType.Damage ||
            !string.Equals(combatEvent.SourceActorKey,combatEvent.TargetActorKey,
                StringComparison.OrdinalIgnoreCase))
            UpdateAbilityCombatSummary(combatEvent);

        if (
            combatEvent.Type ==
            CombatEventType.Damage
        )
        {
            if (
                !string.IsNullOrWhiteSpace(
                    combatEvent.SourceActorKey) &&
                !string.Equals(combatEvent.SourceActorKey,combatEvent.TargetActorKey,
                    StringComparison.OrdinalIgnoreCase) &&
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

                targetSummary.BlockedDamageReceived +=
                    blocked;

                AddBreakdownValue(
                    targetSummary.DamageTakenByAbility,
                    abilityKey,
                    amount
                );

                AddBreakdownValue(
                    targetSummary.BlockedDamageReceivedByAbility,
                    abilityKey,
                    blocked
                );
            }

            if (string.Equals(
                    combatEvent.SourceActorKey,
                    Options.PrimaryActorKey,
                    StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(combatEvent.SourceActorKey,combatEvent.TargetActorKey,
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

                Summary.BlockedDamageReceived +=
                    blocked;

                AddBreakdownValue(
                    Summary.DamageTakenByAbility,
                    abilityKey,
                    amount
                );

                AddBreakdownValue(
                    Summary.BlockedDamageReceivedByAbility,
                    abilityKey,
                    blocked
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
            CombatEventType.AbsorbConsumed
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
                sourceSummary.AbsorptionDone +=
                    amount;

                AddBreakdownValue(
                    sourceSummary.AbsorptionDoneByAbility,
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
                targetSummary.AbsorptionReceived +=
                    amount;

                AddBreakdownValue(
                    targetSummary.AbsorptionReceivedByAbility,
                    abilityKey,
                    amount
                );
            }

            if (string.Equals(
                    combatEvent.SourceActorKey,
                    Options.PrimaryActorKey,
                    StringComparison.OrdinalIgnoreCase))
            {
                Summary.AbsorptionDone +=
                    amount;

                AddBreakdownValue(
                    Summary.AbsorptionDoneByAbility,
                    abilityKey,
                    amount
                );
            }

            if (string.Equals(
                    combatEvent.TargetActorKey,
                    Options.PrimaryActorKey,
                    StringComparison.OrdinalIgnoreCase))
            {
                Summary.AbsorptionReceived +=
                    amount;

                AddBreakdownValue(
                    Summary.AbsorptionReceivedByAbility,
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

    private void UpdateAbilityCombatSummary(
        CombatEvent combatEvent)
    {
        if (string.IsNullOrWhiteSpace(
                combatEvent.AbilityKey))
        {
            return;
        }

        // Only events with an actual per-ability metric should create a
        // summary row. Lifecycle/driver events such as AutoAttackStarted,
        // AutoAttackSwing, and AutoAttackStopped still carry an AbilityKey,
        // but creating an entry for them would leave an empty ability row.
        // Their resolved Damage event is what should be reported instead.
        if (!ContributesToAbilityCombatSummary(
                combatEvent.Type))
        {
            return;
        }

        // Channel completion has an internal driver event and a separate
        // emitted completion event. Only the emitted event is a completed
        // channel for reporting purposes.
        if (
            combatEvent.Type ==
                CombatEventType.AbilityChannelCompleted &&
            combatEvent.IsInternal)
        {
            return;
        }

        // A cancelled cast can leave its already-queued completion event in
        // the event queue. TryGetNextEvent converts that stale completion into
        // an internal AbilityCastCancelled driver event. The real cancellation
        // was already emitted separately, so the internal driver must not be
        // counted again for observability.
        if (
            combatEvent.Type ==
                CombatEventType.AbilityCastCancelled &&
            combatEvent.IsInternal)
        {
            return;
        }

        if (
            !string.IsNullOrWhiteSpace(
                combatEvent.SourceActorKey) &&
            Summary.ActorSummaries.TryGetValue(
                combatEvent.SourceActorKey,
                out var actorSummary))
        {
            UpdateAbilityCombatSummary(
                actorSummary.Abilities,
                combatEvent
            );
        }

        if (string.Equals(
                combatEvent.SourceActorKey,
                Options.PrimaryActorKey,
                StringComparison.OrdinalIgnoreCase))
        {
            UpdateAbilityCombatSummary(
                Summary.Abilities,
                combatEvent
            );
        }
    }

    private static bool ContributesToAbilityCombatSummary(
        CombatEventType eventType)
    {
        return eventType is
            CombatEventType.AbilityCastStarted or
            CombatEventType.AbilityCastCompleted or
            CombatEventType.AbilityCastCancelled or
            CombatEventType.AbilityChannelStarted or
            CombatEventType.AbilityChannelCompleted or
            CombatEventType.AbilityChannelCancelled or
            CombatEventType.Damage or
            CombatEventType.Healing or
            CombatEventType.AbsorbConsumed;
    }

    private static void UpdateAbilityCombatSummary(
        Dictionary<string, AbilityCombatSummary> abilities,
        CombatEvent combatEvent)
    {
        var abilityKey =
            combatEvent.AbilityKey!;

        if (!abilities.TryGetValue(
                abilityKey,
                out var summary))
        {
            summary =
                new AbilityCombatSummary
                {
                    AbilityKey =
                        abilityKey
                };

            abilities[
                abilityKey
            ] = summary;
        }

        switch (combatEvent.Type)
        {
            case CombatEventType.AbilityCastStarted:
                summary.CastStartedCount++;
                break;

            case CombatEventType.AbilityCastCompleted:
                summary.CastCompletedCount++;
                break;

            case CombatEventType.AbilityCastCancelled:
                summary.CastCancelledCount++;
                break;

            case CombatEventType.AbilityChannelStarted:
                summary.ChannelStartedCount++;
                break;

            case CombatEventType.AbilityChannelCompleted:
                summary.ChannelCompletedCount++;
                break;

            case CombatEventType.AbilityChannelCancelled:
                summary.ChannelCancelledCount++;
                break;

            case CombatEventType.Damage:
                summary.DamageOccurrenceCount++;
                summary.DamageDone +=
                    combatEvent.Amount ?? 0m;

                if (combatEvent.IsCritical)
                {
                    summary.CriticalDamageCount++;
                }

                RecordAbilityOutcome(
                    summary,
                    combatEvent
                );
                break;

            case CombatEventType.Healing:
                summary.HealingOccurrenceCount++;
                summary.HealingDone +=
                    combatEvent.Amount ?? 0m;
                summary.OverhealingDone +=
                    combatEvent.OverhealingAmount ?? 0m;

                if (combatEvent.IsCritical)
                {
                    summary.CriticalHealingCount++;
                }

                RecordAbilityOutcome(
                    summary,
                    combatEvent
                );
                break;

            case CombatEventType.AbsorbConsumed:
                summary.AbsorbConsumptionCount++;
                summary.AbsorptionDone +=
                    combatEvent.Amount ?? 0m;
                break;
        }
    }

    private static void RecordAbilityOutcome(
        AbilityCombatSummary summary,
        CombatEvent combatEvent)
    {
        if (!string.IsNullOrWhiteSpace(
                combatEvent.ResultKey))
        {
            AddBreakdownCount(
                summary.ResultCounts,
                combatEvent.ResultKey
            );
        }

        switch (combatEvent.EffectDeliveryType)
        {
            case CombatEffectDeliveryType.Periodic:
                summary.PeriodicOccurrenceCount++;
                break;

            case CombatEffectDeliveryType.ChannelTick:
                summary.ChannelTickOccurrenceCount++;
                break;

            case CombatEffectDeliveryType.Direct:
            default:
                summary.DirectOccurrenceCount++;
                break;
        }
    }

    private static void AddBreakdownCount(
        Dictionary<string, int> breakdown,
        string key)
    {
        if (breakdown.TryGetValue(
                key,
                out var currentValue))
        {
            breakdown[key] =
                currentValue + 1;

            return;
        }

        breakdown[key] = 1;
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
