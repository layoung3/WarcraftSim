namespace WarcraftSim.Core.Simulation.Engine;

public sealed class AutoAttackState
{
    public AutoAttackDefinition Definition { get; }

    public Guid InstanceId { get; private set; }

    public string TargetActorKey { get; private set; } = "";

    public bool IsActive { get; private set; }

    public decimal? NextSwingAtSeconds { get; internal set; }

    public NextSwingReplacementDefinition? QueuedNextSwingReplacement
    {
        get;
        private set;
    }

    public AutoAttackState(
        AutoAttackDefinition definition)
    {
        Definition =
            definition ??
            throw new ArgumentNullException(
                nameof(definition)
            );
    }

    internal void Start(
        string targetActorKey,
        decimal nextSwingAtSeconds)
    {
        InstanceId =
            Guid.NewGuid();

        TargetActorKey =
            targetActorKey;

        IsActive =
            true;

        NextSwingAtSeconds =
            nextSwingAtSeconds;

        // A replacement belongs to one concrete swing-stream lifetime.
        // Restarting/retargeting creates a new lifetime and invalidates it.
        QueuedNextSwingReplacement =
            null;
    }

    internal void Stop()
    {
        IsActive =
            false;

        NextSwingAtSeconds =
            null;

        QueuedNextSwingReplacement =
            null;
    }

    internal void QueueNextSwingReplacement(
        NextSwingReplacementDefinition replacement)
    {
        QueuedNextSwingReplacement =
            replacement ??
            throw new ArgumentNullException(
                nameof(replacement)
            );
    }

    internal bool CancelNextSwingReplacement()
    {
        if (QueuedNextSwingReplacement is null)
        {
            return false;
        }

        QueuedNextSwingReplacement =
            null;

        return true;
    }

    internal NextSwingReplacementDefinition? ConsumeNextSwingReplacement()
    {
        var replacement =
            QueuedNextSwingReplacement;

        QueuedNextSwingReplacement =
            null;

        return replacement;
    }
}
