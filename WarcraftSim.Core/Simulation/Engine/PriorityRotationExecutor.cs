using WarcraftSim.Core.Rotations;

namespace WarcraftSim.Core.Simulation.Engine;

public sealed class PriorityRotationExecutor : ICombatEventProcessor
{
    private readonly RotationProfile _rotation;
    private readonly string _actorKey;
    private readonly string _defaultTargetKey;
    private readonly AbilityExecutor _abilityExecutor;
    private readonly bool _reactToTargetStateChanges;

    public PriorityRotationExecutor(
        RotationProfile rotation,
        string actorKey,
        string targetKey,
        AbilityExecutor abilityExecutor,
        bool reactToTargetStateChanges = false)
    {
        _rotation = rotation;
        _actorKey = actorKey;
        _defaultTargetKey = targetKey;
        _abilityExecutor = abilityExecutor;
        _reactToTargetStateChanges = reactToTargetStateChanges;
    }

    public void Process(
        SimulationContext context,
        CombatEvent combatEvent)
    {
        switch (combatEvent.Type)
        {
            case CombatEventType.SimulationStarted:
            {
                var actor = context.GetActor(_actorKey);

                if (actor is null)
                {
                    return;
                }

                ScheduleDecision(
                    context,
                    Math.Max(
                        context.CurrentTimeSeconds,
                        actor.InputReadyAtSeconds
                    )
                );

                break;
            }

            case CombatEventType.AbilityCastCompleted:
                if (IsOurActor(
                        combatEvent.SourceActorKey))
                {
                    context.GetActor(_actorKey)?
                        .CompleteCurrentCast(
                            combatEvent.AbilityExecutionId
                        );

                    ScheduleDecision(
                        context,
                        context.CurrentTimeSeconds
                    );
                }
                else if (ShouldReactToTargetEvent(
                             context,
                             combatEvent))
                {
                    ScheduleDecision(
                        context,
                        context.CurrentTimeSeconds
                    );
                }

                break;

            case CombatEventType.AbilityEffectImpact:
            case CombatEventType.PeriodicTick:
            case CombatEventType.Damage:
            case CombatEventType.ActorDied:
                if (ShouldReactToTargetEvent(
                        context,
                        combatEvent))
                {
                    ScheduleDecision(
                        context,
                        context.CurrentTimeSeconds
                    );
                }

                break;

            case CombatEventType.RotationDecision:
                if (IsOurActor(
                        combatEvent.SourceActorKey))
                {
                    ExecuteDecision(
                        context
                    );
                }

                break;
        }
    }

    private void ExecuteDecision(
        SimulationContext context)
    {
        var actor =
            context.GetActor(
                _actorKey
            );

        if (
            actor is null ||
            !actor.IsAlive
        )
        {
            return;
        }

        actor.RefreshResources(
            context.CurrentTimeSeconds
        );

        if (actor.IsCasting(
                context.CurrentTimeSeconds))
        {
            TryExecuteInterruptingEntry(
                context,
                actor
            );

            return;
        }

        if (!actor.IsInputReady(
                context.CurrentTimeSeconds))
        {
            context.EndResourceStarvation(
                actor.Key,
                context.CurrentTimeSeconds
            );

            ScheduleDecision(
                context,
                actor.InputReadyAtSeconds
            );

            return;
        }

        TryExecuteNormalEntry(
            context,
            actor
        );
    }

    private bool TryExecuteInterruptingEntry(
        SimulationContext context,
        SimulationActorState actor)
    {
        foreach (
            var entry in
            _rotation.Entries
                .Where(entry =>
                    entry.IsEnabled &&
                    entry.InterruptCurrentCast)
                .OrderBy(entry =>
                    entry.Priority))
        {
            if (!actor.Abilities.TryGetValue(
                    entry.AbilityKey,
                    out var abilityState))
            {
                continue;
            }

            var target =
                RotationTargetSelector.Resolve(
                    context,
                    actor,
                    entry,
                    _defaultTargetKey
                );

            if (
                target is null ||
                !target.IsAlive
            )
            {
                continue;
            }

            if (!RotationConditionEvaluator
                    .AreSatisfied(
                        context,
                        actor,
                        target,
                        entry.Conditions
                    ))
            {
                continue;
            }

            if (!CanUseAfterCancellingCurrentCast(
                    context,
                    actor,
                    abilityState))
            {
                continue;
            }

            var cancelledAbilityKey =
                actor.CurrentCastAbilityKey;

            var cancelledExecutionId =
                actor.CancelCurrentCast(
                    context.CurrentTimeSeconds
                );

            if (!cancelledExecutionId.HasValue)
            {
                continue;
            }

            context.CancelAbilityExecution(
                cancelledExecutionId.Value,
                context.CurrentTimeSeconds
            );

            context.RecordEvent(
                new CombatEvent
                {
                    TimeSeconds =
                        context.CurrentTimeSeconds,

                    Type =
                        CombatEventType.AbilityCastCancelled,

                    SourceActorKey =
                        actor.Key,

                    TargetActorKey =
                        target.Key,

                    AbilityKey =
                        cancelledAbilityKey,

                    AbilityExecutionId =
                        cancelledExecutionId,

                    Description =
                        $"{actor.Name} cancelled {cancelledAbilityKey}."
                }
            );

            var result =
                _abilityExecutor.TryStartAbility(
                    context,
                    _actorKey,
                    target.Key,
                    entry.AbilityKey
                );

            if (!result.Success)
            {
                return false;
            }

            context.EndResourceStarvation(
                actor.Key,
                context.CurrentTimeSeconds
            );

            RegisterStartedAbility(
                context,
                actor,
                abilityState
            );

            return true;
        }

        return false;
    }

    private bool TryExecuteNormalEntry(
        SimulationContext context,
        SimulationActorState actor)
    {
        foreach (
            var entry in
            _rotation.Entries
                .Where(entry =>
                    entry.IsEnabled)
                .OrderBy(entry =>
                    entry.Priority))
        {
            if (!actor.Abilities.TryGetValue(
                    entry.AbilityKey,
                    out var abilityState))
            {
                continue;
            }

            var target =
                RotationTargetSelector.Resolve(
                    context,
                    actor,
                    entry,
                    _defaultTargetKey
                );

            if (
                target is null ||
                !target.IsAlive
            )
            {
                continue;
            }

            if (!RotationConditionEvaluator
                    .AreSatisfied(
                        context,
                        actor,
                        target,
                        entry.Conditions
                    ))
            {
                continue;
            }

            var result =
                _abilityExecutor.TryStartAbility(
                    context,
                    _actorKey,
                    target.Key,
                    entry.AbilityKey
                );

            if (!result.Success)
            {
                continue;
            }

            context.EndResourceStarvation(
                actor.Key,
                context.CurrentTimeSeconds
            );

            RegisterStartedAbility(
                context,
                actor,
                abilityState
            );

            return true;
        }

        ScheduleNextDecision(
            context
        );

        return false;
    }

    private void RegisterStartedAbility(
        SimulationContext context,
        SimulationActorState actor,
        AbilityState abilityState)
    {
        var ability =
            abilityState.Definition;

        var executionId =
            context.GetLatestAbilityExecutionId(
                actor.Key
            );

        if (
            executionId.HasValue &&
            ability.CastTimeSeconds > 0m
        )
        {
            actor.TrackCurrentCast(
                executionId.Value,
                ability.Key,
                ability.CastTimeSeconds
            );
        }

        actor.RegisterActionStarted(
            context.CurrentTimeSeconds,
            ability.CastTimeSeconds,
            ability.IsOffGlobalCooldown
                ? 0m
                : ability.GlobalCooldownSeconds
        );
    }

    private static bool CanUseAfterCancellingCurrentCast(
        SimulationContext context,
        SimulationActorState actor,
        AbilityState abilityState)
    {
        var ability =
            abilityState.Definition;

        if (!abilityState.IsReady(
                context.CurrentTimeSeconds))
        {
            return false;
        }

        if (
            !ability.IsOffGlobalCooldown &&
            !actor.IsGlobalCooldownReady(
                context.CurrentTimeSeconds)
        )
        {
            return false;
        }

        var resourceReadyAt =
            ResourceAvailabilityCalculator
                .GetNextAffordableTime(
                    actor,
                    ability,
                    context.CurrentTimeSeconds
                );

        return
            resourceReadyAt.HasValue &&
            resourceReadyAt.Value <=
                context.CurrentTimeSeconds;
    }

    private void ScheduleNextDecision(
        SimulationContext context)
    {
        var actor =
            context.GetActor(
                _actorKey
            );

        if (
            actor is null ||
            !actor.IsAlive
        )
        {
            return;
        }

        actor.RefreshResources(
            context.CurrentTimeSeconds
        );

        var candidates =
            new List<decimal>();

        var actionReadyNow =
            actor.InputReadyAtSeconds <=
                context.CurrentTimeSeconds &&
            actor.CastReadyAtSeconds <=
                context.CurrentTimeSeconds &&
            actor.GlobalCooldownReadyAtSeconds <=
                context.CurrentTimeSeconds;

        var resourceBlockedNow =
            false;

        var readyAffordableCandidateExists =
            false;

        if (
            actor.InputReadyAtSeconds >
            context.CurrentTimeSeconds
        )
        {
            candidates.Add(
                actor.InputReadyAtSeconds
            );
        }

        if (
            actor.CastReadyAtSeconds >
            context.CurrentTimeSeconds
        )
        {
            candidates.Add(
                actor.CastReadyAtSeconds
            );
        }

        if (
            actor.GlobalCooldownReadyAtSeconds >
            context.CurrentTimeSeconds
        )
        {
            candidates.Add(
                actor.GlobalCooldownReadyAtSeconds
            );
        }

        foreach (
            var entry in
            _rotation.Entries.Where(entry =>
                entry.IsEnabled))
        {
            if (!actor.Abilities.TryGetValue(
                    entry.AbilityKey,
                    out var abilityState))
            {
                continue;
            }

            var target =
                RotationTargetSelector.Resolve(
                    context,
                    actor,
                    entry,
                    _defaultTargetKey
                );

            if (
                target is null ||
                !target.IsAlive
            )
            {
                continue;
            }

            var nextConditionTime =
                RotationConditionEvaluator
                    .GetNextKnownEvaluationTime(
                        context,
                        entry.Conditions
                    );

            if (
                nextConditionTime.HasValue &&
                nextConditionTime.Value >
                context.CurrentTimeSeconds
            )
            {
                candidates.Add(
                    nextConditionTime.Value
                );
            }

            if (!RotationConditionEvaluator
                    .AreSatisfied(
                        context,
                        actor,
                        target,
                        entry.Conditions
                    ))
            {
                continue;
            }

            var nextAbilityReadyTime =
                abilityState.GetNextReadyTime(
                    context.CurrentTimeSeconds
                );

            if (
                nextAbilityReadyTime >
                context.CurrentTimeSeconds
            )
            {
                candidates.Add(
                    nextAbilityReadyTime
                );
            }

            var nextResourceReadyTime =
                ResourceAvailabilityCalculator
                    .GetNextAffordableTime(
                        actor,
                        abilityState.Definition,
                        context.CurrentTimeSeconds
                    );

            if (
                nextResourceReadyTime.HasValue &&
                nextResourceReadyTime.Value >
                context.CurrentTimeSeconds
            )
            {
                candidates.Add(
                    nextResourceReadyTime.Value
                );
            }

            if (
                actionReadyNow &&
                nextAbilityReadyTime <=
                    context.CurrentTimeSeconds
            )
            {
                if (
                    nextResourceReadyTime.HasValue &&
                    nextResourceReadyTime.Value <=
                        context.CurrentTimeSeconds
                )
                {
                    readyAffordableCandidateExists =
                        true;
                }
                else
                {
                    resourceBlockedNow =
                        true;
                }
            }
        }

        if (
            actionReadyNow &&
            resourceBlockedNow &&
            !readyAffordableCandidateExists
        )
        {
            context.BeginResourceStarvation(
                actor.Key,
                context.CurrentTimeSeconds
            );
        }
        else
        {
            context.EndResourceStarvation(
                actor.Key,
                context.CurrentTimeSeconds
            );
        }

        if (candidates.Count > 0)
        {
            ScheduleDecision(
                context,
                candidates.Min()
            );
        }
    }

    private bool ShouldReactToTargetEvent(
        SimulationContext context,
        CombatEvent combatEvent)
    {
        if (
            !_reactToTargetStateChanges ||
            string.IsNullOrWhiteSpace(
                combatEvent.TargetActorKey)
        )
        {
            return false;
        }

        var actor =
            context.GetActor(
                _actorKey
            );

        if (actor is null)
        {
            return false;
        }

        return RotationTargetSelector.WatchesActor(
            context,
            actor,
            _rotation.Entries,
            _defaultTargetKey,
            combatEvent.TargetActorKey
        );
    }

    private void ScheduleDecision(
        SimulationContext context,
        decimal timeSeconds)
    {
        if (
            timeSeconds >
            context.Options.DurationSeconds
        )
        {
            return;
        }

        context.ScheduleEvent(
            new CombatEvent
            {
                TimeSeconds =
                    timeSeconds,

                Type =
                    CombatEventType.RotationDecision,

                SourceActorKey =
                    _actorKey,

                TargetActorKey =
                    _defaultTargetKey,

                IsInternal =
                    true,

                Description =
                    "Rotation decision."
            }
        );
    }

    private bool IsOurActor(
        string? actorKey)
    {
        return string.Equals(
            actorKey,
            _actorKey,
            StringComparison.OrdinalIgnoreCase
        );
    }
}
