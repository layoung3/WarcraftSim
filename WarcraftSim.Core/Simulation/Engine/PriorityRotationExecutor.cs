using WarcraftSim.Core.Rotations;

namespace WarcraftSim.Core.Simulation.Engine;

public sealed class PriorityRotationExecutor :
    ICombatEventProcessor
{
    private readonly RotationProfile _rotation;

    private readonly string _actorKey;

    private readonly string _targetKey;

    private readonly AbilityExecutor _abilityExecutor;

    public PriorityRotationExecutor(
        RotationProfile rotation,
        string actorKey,
        string targetKey,
        AbilityExecutor abilityExecutor)
    {
        _rotation = rotation;
        _actorKey = actorKey;
        _targetKey = targetKey;
        _abilityExecutor = abilityExecutor;
    }

    public void Process(
        SimulationContext context,
        CombatEvent combatEvent)
    {
        switch (combatEvent.Type)
        {
            case CombatEventType.SimulationStarted:
                ScheduleDecision(
                    context,
                    context.CurrentTimeSeconds
                );
                break;

            case CombatEventType.AbilityCastCompleted:
                if (IsOurActor(
                        combatEvent.SourceActorKey))
                {
                    // A completed cast is itself a meaningful decision point.
                    // Try the priority list immediately. If the GCD/cooldown
                    // still prevents an action, ExecuteDecision will schedule
                    // the next future readiness point.
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
            context.GetActor(_actorKey);

        var target =
            context.GetActor(_targetKey);

        if (
            actor is null ||
            target is null ||
            !actor.IsAlive ||
            !target.IsAlive)
        {
            return;
        }

        var entries =
            _rotation.Entries
                .Where(entry =>
                    entry.IsEnabled)
                .OrderBy(entry =>
                    entry.Priority);

        foreach (var entry in entries)
        {
            if (!actor.Abilities.ContainsKey(
                    entry.AbilityKey))
            {
                continue;
            }

            var result =
                _abilityExecutor.TryStartAbility(
                    context,
                    _actorKey,
                    _targetKey,
                    entry.AbilityKey
                );

            if (result.Success)
            {
                return;
            }
        }

        ScheduleNextDecision(
            context
        );
    }

    private void ScheduleNextDecision(
        SimulationContext context)
    {
        var actor =
            context.GetActor(_actorKey);

        if (
            actor is null ||
            !actor.IsAlive)
        {
            return;
        }

        var candidates =
            new List<decimal>();

        if (
            actor.CastReadyAtSeconds >
            context.CurrentTimeSeconds)
        {
            candidates.Add(
                actor.CastReadyAtSeconds
            );
        }

        if (
            actor.GlobalCooldownReadyAtSeconds >
            context.CurrentTimeSeconds)
        {
            candidates.Add(
                actor.GlobalCooldownReadyAtSeconds
            );
        }

        foreach (
            var entry in
            _rotation.Entries
                .Where(entry =>
                    entry.IsEnabled))
        {
            if (!actor.Abilities.TryGetValue(
                    entry.AbilityKey,
                    out var abilityState))
            {
                continue;
            }

            var nextReadyTime =
                abilityState.GetNextReadyTime(
                    context.CurrentTimeSeconds
                );

            if (
                nextReadyTime >
                context.CurrentTimeSeconds)
            {
                candidates.Add(
                    nextReadyTime
                );
            }
        }

        // If nothing has a known future readiness point, stop making
        // decisions. Future systems such as resource regeneration can
        // add their own meaningful wake-up time here later.
        if (candidates.Count == 0)
        {
            return;
        }

        var nextDecisionTime =
            candidates.Min();

        ScheduleDecision(
            context,
            nextDecisionTime
        );
    }

    private void ScheduleDecision(
        SimulationContext context,
        decimal timeSeconds)
    {
        if (
            timeSeconds >
            context.Options.DurationSeconds)
        {
            return;
        }

        context.ScheduleEvent(
            new CombatEvent
            {
                TimeSeconds = timeSeconds,

                Type = CombatEventType.RotationDecision,

                SourceActorKey = _actorKey,

                TargetActorKey = _targetKey,

                IsInternal = true,

                Description = "Rotation decision."
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
