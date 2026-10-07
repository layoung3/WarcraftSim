namespace WarcraftSim.Core.Simulation.Engine;

public sealed class AbilityExecutionState
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string SourceActorKey { get; set; } = "";

    public string TargetActorKey { get; set; } = "";

    public string AbilityKey { get; set; } = "";

    public bool IsCancelled { get; private set; }

    public decimal? CancelledAtSeconds { get; private set; }

    public bool IsChanneling { get; private set; }

    public decimal? ChannelStartedAtSeconds { get; private set; }

    public decimal? ChannelEndsAtSeconds { get; private set; }

    public decimal? ChannelCompletedAtSeconds { get; private set; }

    public Dictionary<string, CombatRollResult> EffectResults { get; } =
        new(StringComparer.OrdinalIgnoreCase);

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
