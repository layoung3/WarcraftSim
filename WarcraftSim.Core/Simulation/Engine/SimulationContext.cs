namespace WarcraftSim.Core.Simulation.Engine;

public sealed class SimulationContext
{
    private readonly PriorityQueue<
        CombatEvent,
        (decimal TimeSeconds, long Sequence)
    > _eventQueue = new();

    private long _nextSequence;

    public SimulationRunOptions Options { get; }

    public Random Random { get; }

    public decimal CurrentTimeSeconds { get; private set; }

    public Dictionary<string, SimulationActorState> Actors { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public List<CombatEvent> Timeline { get; } = [];

    public SimulationRunSummary Summary { get; }

    public SimulationContext(SimulationRunOptions options)
    {
        Options = options;

        Random = new Random(options.Seed);

        Summary = new SimulationRunSummary
        {
            Seed = options.Seed,
            DurationSeconds = options.DurationSeconds
        };
    }

    public void AddActor(SimulationActorState actor)
    {
        Actors[actor.Key] = actor;
    }

    public SimulationActorState? GetActor(string actorKey)
    {
        return Actors.TryGetValue(
            actorKey,
            out var actor
        )
            ? actor
            : null;
    }

    public void ScheduleEvent(CombatEvent combatEvent)
    {
        if (combatEvent.TimeSeconds < CurrentTimeSeconds)
        {
            throw new InvalidOperationException(
                "Cannot schedule an event in the past."
            );
        }

        if (combatEvent.TimeSeconds > Options.DurationSeconds)
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

    public bool TryGetNextEvent(out CombatEvent? combatEvent)
    {
        if (!_eventQueue.TryDequeue(
                out combatEvent,
                out _))
        {
            return false;
        }

        CurrentTimeSeconds =
            combatEvent!.TimeSeconds;

        return true;
    }

    public void RecordEvent(CombatEvent combatEvent)
    {
        UpdateSummary(combatEvent);

        if (Options.CaptureTimeline)
        {
            Timeline.Add(combatEvent);
        }
    }

    private void UpdateSummary(CombatEvent combatEvent)
    {
        var amount = combatEvent.Amount ?? 0m;

        var abilityKey =
            string.IsNullOrWhiteSpace(combatEvent.AbilityKey)
                ? "unknown"
                : combatEvent.AbilityKey;

        if (combatEvent.Type == CombatEventType.Damage)
        {
            if (string.Equals(
                    combatEvent.SourceActorKey,
                    Options.PrimaryActorKey,
                    StringComparison.OrdinalIgnoreCase))
            {
                Summary.DamageDone += amount;

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
                Summary.DamageTaken += amount;

                AddBreakdownValue(
                    Summary.DamageTakenByAbility,
                    abilityKey,
                    amount
                );
            }
        }

        if (combatEvent.Type == CombatEventType.Healing)
        {
            if (string.Equals(
                    combatEvent.SourceActorKey,
                    Options.PrimaryActorKey,
                    StringComparison.OrdinalIgnoreCase))
            {
                Summary.HealingDone += amount;

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
                Summary.HealingReceived += amount;

                AddBreakdownValue(
                    Summary.HealingReceivedByAbility,
                    abilityKey,
                    amount
                );
            }
        }

        if (
            combatEvent.Type == CombatEventType.ActorDied &&
            string.Equals(
                combatEvent.TargetActorKey,
                Options.PrimaryActorKey,
                StringComparison.OrdinalIgnoreCase)
        )
        {
            Summary.PrimaryActorDied = true;

            Summary.PrimaryActorDeathTimeSeconds =
                combatEvent.TimeSeconds;
        }
    }

    private static void AddBreakdownValue(
    Dictionary<string, decimal> breakdown,
    string key,
    decimal amount)
    {
        if (breakdown.TryGetValue(key, out var currentValue))
        {
            breakdown[key] = currentValue + amount;
            return;
        }

        breakdown[key] = amount;
    }
}