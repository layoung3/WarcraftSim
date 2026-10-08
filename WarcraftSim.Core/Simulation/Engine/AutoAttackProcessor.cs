using WarcraftSim.Core.Abilities;

namespace WarcraftSim.Core.Simulation.Engine;

/// <summary>
/// Drives independently-timed background weapon swings. Auto-attacks do not
/// consume player input, do not start the GCD, and do not require cast readiness.
/// </summary>
public sealed class AutoAttackProcessor :
    ICombatEventProcessor
{
    private readonly AbilityExecutor
        _abilityExecutor;

    public AutoAttackProcessor(
        AbilityExecutor abilityExecutor)
    {
        _abilityExecutor =
            abilityExecutor ??
            throw new ArgumentNullException(
                nameof(abilityExecutor)
            );
    }

    public bool Start(
        SimulationContext context,
        string sourceActorKey,
        string targetActorKey,
        AutoAttackDefinition definition,
        decimal? firstSwingDelaySeconds = null)
    {
        ArgumentNullException.ThrowIfNull(
            context
        );

        ValidateDefinition(
            definition
        );

        var source =
            context.GetActor(
                sourceActorKey
            );

        var target =
            context.GetActor(
                targetActorKey
            );

        if (
            source is null ||
            target is null ||
            !source.IsAlive ||
            !target.IsAlive)
        {
            return false;
        }

        var firstDelay =
            firstSwingDelaySeconds ??
            definition.SwingIntervalSeconds;

        if (firstDelay < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(firstSwingDelaySeconds),
                firstSwingDelaySeconds,
                "The first auto-attack swing delay cannot be negative."
            );
        }

        if (!source.AutoAttacks.TryGetValue(
                definition.Key,
                out var state))
        {
            state =
                new AutoAttackState(
                    definition
                );

            source.AutoAttacks.Add(
                definition.Key,
                state
            );
        }
        else if (!ReferenceEquals(
                     state.Definition,
                     definition))
        {
            throw new InvalidOperationException(
                $"Auto-attack key '{definition.Key}' is already registered with a different definition on actor '{source.Key}'."
            );
        }

        var firstSwingAt =
            context.CurrentTimeSeconds +
            firstDelay;

        state.Start(
            target.Key,
            firstSwingAt
        );

        context.EmitEvent(
            new CombatEvent
            {
                TimeSeconds =
                    context.CurrentTimeSeconds,

                Type =
                    CombatEventType.AutoAttackStarted,

                SourceActorKey =
                    source.Key,

                TargetActorKey =
                    target.Key,

                AbilityKey =
                    definition.Key,

                AutoAttackInstanceId =
                    state.InstanceId,

                Description =
                    $"{source.Name} started {definition.Name} against {target.Name}."
            }
        );

        ScheduleSwing(
            context,
            source,
            state,
            firstSwingAt
        );

        return true;
    }

    public bool Stop(
        SimulationContext context,
        string sourceActorKey,
        string autoAttackKey)
    {
        ArgumentNullException.ThrowIfNull(
            context
        );

        var source =
            context.GetActor(
                sourceActorKey
            );

        if (
            source is null ||
            !source.AutoAttacks.TryGetValue(
                autoAttackKey,
                out var state) ||
            !state.IsActive)
        {
            return false;
        }

        var stoppedInstanceId =
            state.InstanceId;

        var targetActorKey =
            state.TargetActorKey;

        state.Stop();

        context.EmitEvent(
            new CombatEvent
            {
                TimeSeconds =
                    context.CurrentTimeSeconds,

                Type =
                    CombatEventType.AutoAttackStopped,

                SourceActorKey =
                    source.Key,

                TargetActorKey =
                    targetActorKey,

                AbilityKey =
                    state.Definition.Key,

                AutoAttackInstanceId =
                    stoppedInstanceId,

                Description =
                    $"{source.Name} stopped {state.Definition.Name}."
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
                CombatEventType.AutoAttackSwing ||
            string.IsNullOrWhiteSpace(
                combatEvent.SourceActorKey) ||
            string.IsNullOrWhiteSpace(
                combatEvent.AbilityKey) ||
            !combatEvent.AutoAttackInstanceId.HasValue)
        {
            return;
        }

        var source =
            context.GetActor(
                combatEvent.SourceActorKey
            );

        if (
            source is null ||
            !source.AutoAttacks.TryGetValue(
                combatEvent.AbilityKey,
                out var state) ||
            !state.IsActive ||
            state.InstanceId !=
                combatEvent.AutoAttackInstanceId.Value)
        {
            return;
        }

        var target =
            context.GetActor(
                state.TargetActorKey
            );

        if (
            !source.IsAlive ||
            target is null ||
            !target.IsAlive)
        {
            Stop(
                context,
                source.Key,
                state.Definition.Key
            );

            return;
        }

        state.NextSwingAtSeconds =
            null;

        _abilityExecutor.ExecuteBackgroundDirectDamage(
            context,
            source,
            target,
            state.Definition.Key,
            state.Definition.Name,
            state.Definition.DamageEffect
        );

        if (!state.IsActive)
        {
            return;
        }

        if (
            !source.IsAlive ||
            !target.IsAlive)
        {
            Stop(
                context,
                source.Key,
                state.Definition.Key
            );

            return;
        }

        var nextSwingAt =
            context.CurrentTimeSeconds +
            state.Definition.SwingIntervalSeconds;

        state.NextSwingAtSeconds =
            nextSwingAt;

        ScheduleSwing(
            context,
            source,
            state,
            nextSwingAt
        );
    }

    private static void ScheduleSwing(
        SimulationContext context,
        SimulationActorState source,
        AutoAttackState state,
        decimal timeSeconds)
    {
        context.ScheduleEvent(
            new CombatEvent
            {
                TimeSeconds =
                    timeSeconds,

                Type =
                    CombatEventType.AutoAttackSwing,

                SourceActorKey =
                    source.Key,

                TargetActorKey =
                    state.TargetActorKey,

                AbilityKey =
                    state.Definition.Key,

                AutoAttackInstanceId =
                    state.InstanceId,

                IsInternal =
                    true,

                Description =
                    $"Internal swing driver for {state.Definition.Name}."
            }
        );
    }

    private static void ValidateDefinition(
        AutoAttackDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(
            definition
        );

        if (string.IsNullOrWhiteSpace(
                definition.Key))
        {
            throw new ArgumentException(
                "Auto-attacks require a key.",
                nameof(definition)
            );
        }

        if (definition.SwingIntervalSeconds <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(definition),
                definition.SwingIntervalSeconds,
                "Auto-attack swing intervals must be greater than zero."
            );
        }

        if (
            definition.DamageEffect is null ||
            !string.Equals(
                definition.DamageEffect.EffectType,
                AbilityEffectTypes.DirectDamage,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Auto-attacks require one direct-damage effect.",
                nameof(definition)
            );
        }
    }
}
