namespace WarcraftSim.Core.Simulation.Engine;

public sealed class AbilityExecutionState
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string SourceActorKey { get; set; } = "";

    public string TargetActorKey { get; set; } = "";

    public string AbilityKey { get; set; } = "";

    public AbilityTimingSnapshot Timing { get; set; } =
        new();

    // A cast can be pushed back after its original completion event was queued.
    // The event queue checks this before dispatching a completion, so obsolete
    // timestamps never inflate cast summaries or awaken rotations early.
    public decimal CastCompletionDueAtSeconds { get; private set; }
    public decimal TotalCastPushbackSeconds { get; private set; }

    public void InitializeCastCompletion(decimal dueAtSeconds)
    {
        CastCompletionDueAtSeconds = Math.Max(0m, dueAtSeconds);
    }

    public decimal ApplyCastPushback(decimal seconds, decimal maximumTotalSeconds)
    {
        if (IsCancelled || IsChanneling || Timing.CastTimeSeconds <= 0m)
            return 0m;
        var applied = Math.Min(Math.Max(0m, seconds),
            Math.Max(0m, maximumTotalSeconds - TotalCastPushbackSeconds));
        CastCompletionDueAtSeconds += applied;
        TotalCastPushbackSeconds += applied;
        return applied;
    }

    // Cast completion events are queued and can be rescheduled on pushback.
    // Claim at most one successful completion per execution, even if an
    // external processor accidentally queues a duplicate same-time event.
    public bool CastCompletionProcessed { get; private set; }

    public bool TryClaimCastCompletion()
    {
        if(IsCancelled || CastCompletionProcessed)return false;
        CastCompletionProcessed=true;
        return true;
    }

    public bool IsCancelled { get; private set; }

    public decimal? CancelledAtSeconds { get; private set; }

    public bool IsChanneling { get; private set; }

    public decimal? ChannelStartedAtSeconds { get; private set; }

    public decimal? ChannelEndsAtSeconds { get; private set; }

    public decimal? ChannelCompletedAtSeconds { get; private set; }

    public Dictionary<string, CombatRollResult> EffectResults { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    // Multi-target effects resolve independently. A global last-result slot
    // cannot decide whether each individual target receives a dependent DoT.
    public Dictionary<(string Effect, string Target),CombatRollResult>
        EffectResultsByTarget { get; } = new();

    public Dictionary<string, decimal> AdditionalResourcesConsumed { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public void RecordAdditionalResourceConsumed(
        string resourceKey,
        decimal amount)
    {
        if (string.IsNullOrWhiteSpace(resourceKey))
        {
            throw new ArgumentException(
                "Consumed resources require a resource key.",
                nameof(resourceKey)
            );
        }

        AdditionalResourcesConsumed[resourceKey] =
            Math.Max(0m, amount);
    }

    public decimal GetAdditionalResourceConsumed(
        string resourceKey)
    {
        return AdditionalResourcesConsumed.TryGetValue(
                resourceKey,
                out var amount)
            ? amount
            : 0m;
    }

    public void StartChannel(
        decimal currentTimeSeconds,
        decimal durationSeconds)
    {
        if (durationSeconds <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(durationSeconds),
                durationSeconds,
                "Channel duration must be greater than zero."
            );
        }

        IsChanneling =
            true;

        ChannelStartedAtSeconds =
            currentTimeSeconds;

        ChannelEndsAtSeconds =
            currentTimeSeconds +
            durationSeconds;
    }

    public void CompleteChannel(
        decimal currentTimeSeconds)
    {
        if (!IsChanneling)
        {
            return;
        }

        IsChanneling =
            false;

        ChannelCompletedAtSeconds =
            currentTimeSeconds;
    }

    public void Cancel(
        decimal currentTimeSeconds)
    {
        IsCancelled = true;

        IsChanneling =
            false;

        CancelledAtSeconds =
            currentTimeSeconds;
    }
}
