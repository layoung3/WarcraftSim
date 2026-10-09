using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation.Validation;

namespace WarcraftSim.Core.Simulation.Engine;

public sealed class PriorityRotationExecutor :
    ICombatEventProcessor,
    ISimulationDefinitionValidationParticipant
{
    private readonly RotationProfile _rotation;
    private readonly string _actorKey;
    private readonly string _defaultTargetKey;
    private readonly AbilityExecutor _abilityExecutor;
    private readonly bool _reactToTargetStateChanges;
    private readonly AutoAttackProcessor? _autoAttackProcessor;

    public PriorityRotationExecutor(
        RotationProfile rotation,
        string actorKey,
        string targetKey,
        AbilityExecutor abilityExecutor,
        bool reactToTargetStateChanges = false,
        AutoAttackProcessor? autoAttackProcessor = null)
    {
        _rotation = rotation;
        _actorKey = actorKey;
        _defaultTargetKey = targetKey;
        _abilityExecutor = abilityExecutor;
        _reactToTargetStateChanges = reactToTargetStateChanges;
        _autoAttackProcessor = autoAttackProcessor;
    }

    public void CollectValidationErrors(
        SimulationContext context,
        ICollection<string> errors)
    {
        SimulationDefinitionValidator.CollectRotationErrors(
            context,
            _rotation,
            _actorKey,
            _defaultTargetKey,
            errors
        );

        if (
            _autoAttackProcessor is null &&
            _rotation.Entries.Any(entry =>
                entry.IsEnabled &&
                IsNextSwingEntry(entry)))
        {
            errors.Add(
                $"Rotation '{_rotation.Name}' contains queued next-swing actions but its executor has no auto-attack processor."
            );
        }
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

            case CombatEventType.AutoAttackStarted:
            case CombatEventType.AutoAttackSwing:
                if (IsOurActor(
                        combatEvent.SourceActorKey))
                {
                    ScheduleDecision(
                        context,
                        context.CurrentTimeSeconds
                    );
                }

                break;

            case CombatEventType.ReactiveOpportunityGranted:
                if (IsOurActor(combatEvent.SourceActorKey))
                    ScheduleDecision(context, context.CurrentTimeSeconds);
                break;

            case CombatEventType.AbilityCastCompleted:
            case CombatEventType.AbilityChannelCompleted:
            case CombatEventType.AbilityChannelCancelled:
                if (IsOurActor(
                        combatEvent.SourceActorKey))
                {
                    ScheduleDecision(
                        context,
                        context.CurrentTimeSeconds
                    );
                }
                else if (ShouldReactToStateEvent(
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
            case CombatEventType.Healing:
            case CombatEventType.ResourceChanged:
            case CombatEventType.AuraApplied:
            case CombatEventType.AuraRemoved:
            case CombatEventType.ActorDied:
                if (ShouldReactToStateEvent(
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

        TryExecuteNextSwingEntry(
            context,
            actor
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

    private bool TryExecuteNextSwingEntry(
        SimulationContext context,
        SimulationActorState actor)
    {
        if (_autoAttackProcessor is null)
        {
            return false;
        }

        foreach (
            var entry in
            _rotation.Entries
                .Where(entry =>
                    entry.IsEnabled &&
                    IsNextSwingEntry(entry))
                .OrderBy(entry =>
                    entry.Priority))
        {
            if (!actor.NextSwingReplacements.TryGetValue(
                    entry.NextSwingReplacementKey,
                    out var replacement))
            {
                continue;
            }

            if (!actor.AutoAttacks.TryGetValue(
                    entry.AutoAttackKey,
                    out var autoAttackState) ||
                !autoAttackState.IsActive)
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
                !target.IsAlive ||
                !string.Equals(
                    target.Key,
                    autoAttackState.TargetActorKey,
                    StringComparison.OrdinalIgnoreCase
                ))
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

            if (string.Equals(
                    autoAttackState.QueuedNextSwingReplacement?.Key,
                    replacement.Key,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!_autoAttackProcessor.QueueNextSwingReplacement(
                    context,
                    actor.Key,
                    entry.AutoAttackKey,
                    replacement
                ))
            {
                continue;
            }

            return true;
        }

        return false;
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
                    IsAbilityEntry(entry) &&
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

            if (!_abilityExecutor.TryCancelCurrentCast(
                    context,
                    actor.Key
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
                return false;
            }

            context.EndResourceStarvation(
                actor.Key,
                context.CurrentTimeSeconds
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
                    entry.IsEnabled &&
                    IsAbilityEntry(entry))
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

            return true;
        }

        ScheduleNextDecision(
            context
        );

        return false;
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
                entry.IsEnabled &&
                IsNextSwingEntry(entry)))
        {
            if (!actor.NextSwingReplacements.TryGetValue(
                    entry.NextSwingReplacementKey,
                    out var replacement) ||
                !actor.AutoAttacks.TryGetValue(
                    entry.AutoAttackKey,
                    out var autoAttackState) ||
                !autoAttackState.IsActive ||
                string.Equals(
                    autoAttackState.QueuedNextSwingReplacement?.Key,
                    replacement.Key,
                    StringComparison.OrdinalIgnoreCase))
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
                !target.IsAlive ||
                !string.Equals(
                    target.Key,
                    autoAttackState.TargetActorKey,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var nextConditionTime =
                RotationConditionEvaluator
                    .GetNextKnownEvaluationTime(
                        context,
                        actor,
                        target,
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

            var nextResourceReadyTime =
                ResourceAvailabilityCalculator
                    .GetNextAffordableTime(
                        actor,
                        replacement.ResourceCosts,
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
        }

        foreach (
            var entry in
            _rotation.Entries.Where(entry =>
                entry.IsEnabled &&
                IsAbilityEntry(entry)))
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
                        actor,
                        target,
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

    private bool ShouldReactToStateEvent(
        SimulationContext context,
        CombatEvent combatEvent)
    {
        if (string.IsNullOrWhiteSpace(
                combatEvent.TargetActorKey))
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

        if (
            combatEvent.Type ==
                CombatEventType.ResourceChanged &&
            string.Equals(
                combatEvent.TargetActorKey,
                actor.Key,
                StringComparison.OrdinalIgnoreCase
            ) &&
            _rotation.Entries.Any(entry =>
                entry.IsEnabled &&
                IsNextSwingEntry(entry)))
        {
            return true;
        }

        if (
            string.Equals(
                combatEvent.TargetActorKey,
                actor.Key,
                StringComparison.OrdinalIgnoreCase
            ) &&
            UsesSourceConditionAffectedBy(
                combatEvent.Type
            )
        )
        {
            return true;
        }

        if (!RotationTargetSelector.WatchesActor(
                context,
                actor,
                _rotation.Entries,
                _defaultTargetKey,
                combatEvent.TargetActorKey
            ))
        {
            return false;
        }

        if (_reactToTargetStateChanges)
        {
            return true;
        }

        return UsesTargetConditionAffectedBy(
            combatEvent.Type
        );
    }

    private bool UsesSourceConditionAffectedBy(
        CombatEventType eventType)
    {
        return _rotation.Entries
            .Where(
                entry =>
                    entry.IsEnabled
            )
            .SelectMany(
                entry =>
                    entry.Conditions
            )
            .Any(
                condition =>
                    eventType switch
                    {
                        CombatEventType.AbilityEffectImpact or
                        CombatEventType.PeriodicTick or
                        CombatEventType.Damage or
                        CombatEventType.Healing or
                        CombatEventType.ActorDied =>
                            string.Equals(
                                condition.ConditionType,
                                RotationConditionTypes.SourceHealthPercent,
                                StringComparison.OrdinalIgnoreCase
                            ),

                        CombatEventType.ResourceChanged =>
                            string.Equals(
                                condition.ConditionType,
                                RotationConditionTypes.SourceResourceCurrent,
                                StringComparison.OrdinalIgnoreCase
                            ) ||
                            string.Equals(
                                condition.ConditionType,
                                RotationConditionTypes.SourceResourcePercent,
                                StringComparison.OrdinalIgnoreCase
                            ),

                        CombatEventType.AuraApplied or
                        CombatEventType.AuraRemoved =>
                            string.Equals(
                                condition.ConditionType,
                                RotationConditionTypes.SourceAuraActive,
                                StringComparison.OrdinalIgnoreCase
                            ) ||
                            string.Equals(
                                condition.ConditionType,
                                RotationConditionTypes.SourceAuraMissing,
                                StringComparison.OrdinalIgnoreCase
                            ),

                        _ =>
                            false
                    }
            );
    }

    private bool UsesTargetConditionAffectedBy(
        CombatEventType eventType)
    {
        return _rotation.Entries
            .Where(
                entry =>
                    entry.IsEnabled
            )
            .SelectMany(
                entry =>
                    entry.Conditions
            )
            .Any(
                condition =>
                    eventType switch
                    {
                        CombatEventType.AbilityEffectImpact or
                        CombatEventType.PeriodicTick or
                        CombatEventType.Damage or
                        CombatEventType.Healing or
                        CombatEventType.ActorDied =>
                            string.Equals(
                                condition.ConditionType,
                                RotationConditionTypes.TargetHealthPercent,
                                StringComparison.OrdinalIgnoreCase
                            ),

                        CombatEventType.AuraApplied or
                        CombatEventType.AuraRemoved =>
                            string.Equals(
                                condition.ConditionType,
                                RotationConditionTypes.TargetAuraActive,
                                StringComparison.OrdinalIgnoreCase
                            ) ||
                            string.Equals(
                                condition.ConditionType,
                                RotationConditionTypes.TargetAuraMissing,
                                StringComparison.OrdinalIgnoreCase
                            ),

                        _ =>
                            false
                    }
            );
    }

    private static bool IsAbilityEntry(
        RotationEntry entry)
    {
        return string.Equals(
            entry.ActionType,
            RotationActionTypes.Ability,
            StringComparison.OrdinalIgnoreCase
        );
    }

    private static bool IsNextSwingEntry(
        RotationEntry entry)
    {
        return string.Equals(
            entry.ActionType,
            RotationActionTypes.QueueNextSwingReplacement,
            StringComparison.OrdinalIgnoreCase
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
