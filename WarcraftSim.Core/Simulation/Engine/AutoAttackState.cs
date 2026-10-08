namespace WarcraftSim.Core.Simulation.Engine;

public sealed class AutoAttackState
{
    public AutoAttackDefinition Definition { get; }

    public Guid InstanceId { get; private set; }

    public string TargetActorKey { get; private set; } = "";

    public bool IsActive { get; private set; }

    public decimal? NextSwingAtSeconds { get; internal set; }

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
    }

    internal void Stop()
    {
        IsActive =
            false;

        NextSwingAtSeconds =
            null;
    }
}
