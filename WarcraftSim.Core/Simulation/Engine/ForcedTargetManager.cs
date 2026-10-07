namespace WarcraftSim.Core.Simulation.Engine;

public sealed class ForcedTargetManager :
    ICombatEventProcessor
{
    public ForcedTargetState ApplyForcedTarget(
        SimulationContext context,
        SimulationActorState targetOwner,
        SimulationActorState forcedTarget,
        decimal durationSeconds,
        string? sourceActorKey = null,
        string? abilityKey = null,
        string? effectKey = null)
    {
        ArgumentNullException.ThrowIfNull(
            context
        );

        ArgumentNullException.ThrowIfNull(
            targetOwner
        );

        ArgumentNullException.ThrowIfNull(
            forcedTarget
        );

        if (durationSeconds <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(durationSeconds),
                durationSeconds,
                "Forced-target duration must be greater than zero."
            );
        }

        if (!forcedTarget.IsAlive)
        {
            throw new InvalidOperationException(
                $"Cannot force targeting to dead actor '{forcedTarget.Key}'."
            );
        }

        var effectSourceActorKey =
            string.IsNullOrWhiteSpace(
                sourceActorKey)
                ? forcedTarget.Key
                : sourceActorKey;

        if (context.GetActor(
                effectSourceActorKey) is null)
        {
            throw new InvalidOperationException(
                $"Forced-target source actor '{effectSourceActorKey}' does not exist in the simulation."
            );
        }

        if (targetOwner.ForcedTarget is not null)
        {
            RemoveForcedTarget(
                context,
                targetOwner,
                "replaced"
            );
        }

        var now =
            context.CurrentTimeSeconds;

        var state =
            new ForcedTargetState
            {
                InstanceId =
                    Guid.NewGuid(),

                TargetOwnerActorKey =
                    targetOwner.Key,

                ForcedTargetActorKey =
                    forcedTarget.Key,

                SourceActorKey =
                    effectSourceActorKey,

                AbilityKey =
                    abilityKey,

                EffectKey =
                    effectKey,

                AppliedAtSeconds =
                    now,

                ExpiresAtSeconds =
                    now +
                    durationSeconds
            };

        targetOwner.ForcedTarget =
            state;

        context.EmitEvent(
            new CombatEvent
            {
                TimeSeconds =
                    now,

                Type =
                    CombatEventType.ForcedTargetApplied,

                SourceActorKey =
                    effectSourceActorKey,

                TargetActorKey =
                    targetOwner.Key,

                ForcedTargetActorKey =
                    forcedTarget.Key,

                ForcedTargetInstanceId =
                    state.InstanceId,

                AbilityKey =
                    abilityKey,

                EffectKey =
                    effectKey,

                Description =
                    $"{targetOwner.Name} is forced to target {forcedTarget.Name} until {state.ExpiresAtSeconds}."
            }
        );

        ScheduleExpiration(
            context,
            state
        );

        return state;
    }

    public bool RemoveForcedTarget(
        SimulationContext context,
        SimulationActorState targetOwner,
        string reason = "removed")
    {
        ArgumentNullException.ThrowIfNull(
            context
        );

        ArgumentNullException.ThrowIfNull(
            targetOwner
        );

        var state =
            targetOwner.ForcedTarget;

        if (state is null)
        {
            return false;
        }

        targetOwner.ForcedTarget =
            null;

        context.EmitEvent(
            new CombatEvent
            {
                TimeSeconds =
                    context.CurrentTimeSeconds,

                Type =
                    CombatEventType.ForcedTargetRemoved,

                SourceActorKey =
                    state.SourceActorKey,

                TargetActorKey =
                    targetOwner.Key,

                ForcedTargetActorKey =
                    state.ForcedTargetActorKey,

                ForcedTargetInstanceId =
                    state.InstanceId,

                AbilityKey =
                    state.AbilityKey,

                EffectKey =
                    state.EffectKey,

                Description =
                    $"Forced target {state.ForcedTargetActorKey} {reason} on {targetOwner.Name}."
            }
        );

        return true;
    }

    public void Process(
        SimulationContext context,
        CombatEvent combatEvent)
    {
        if (
            combatEvent.Type !=
                CombatEventType.ForcedTargetExpiration ||
            !combatEvent.ForcedTargetInstanceId.HasValue ||
            string.IsNullOrWhiteSpace(
                combatEvent.TargetActorKey)
        )
        {
            return;
        }

        var targetOwner =
            context.GetActor(
                combatEvent.TargetActorKey
            );

        if (targetOwner is null)
        {
            return;
        }

        var state =
            targetOwner.ForcedTarget;

        // A missing or different instance means this expiration belongs to
        // an effect that was removed or replaced after it was scheduled.
        if (
            state is null ||
            state.InstanceId !=
                combatEvent.ForcedTargetInstanceId.Value
        )
        {
            return;
        }

        if (context.CurrentTimeSeconds <
            state.ExpiresAtSeconds)
        {
            return;
        }

        RemoveForcedTarget(
            context,
            targetOwner,
            "expired"
        );
    }

    private static void ScheduleExpiration(
        SimulationContext context,
        ForcedTargetState state)
    {
        if (state.ExpiresAtSeconds <=
            context.CurrentTimeSeconds)
        {
            return;
        }

        context.ScheduleEvent(
            new CombatEvent
            {
                TimeSeconds =
                    state.ExpiresAtSeconds,

                Type =
                    CombatEventType.ForcedTargetExpiration,

                SourceActorKey =
                    state.SourceActorKey,

                TargetActorKey =
                    state.TargetOwnerActorKey,

                ForcedTargetActorKey =
                    state.ForcedTargetActorKey,

                ForcedTargetInstanceId =
                    state.InstanceId,

                AbilityKey =
                    state.AbilityKey,

                EffectKey =
                    state.EffectKey,

                IsInternal =
                    true,

                Description =
                    $"Forced-target expiration check for {state.TargetOwnerActorKey}."
            }
        );
    }
}
