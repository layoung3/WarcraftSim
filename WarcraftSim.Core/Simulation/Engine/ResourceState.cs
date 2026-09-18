namespace WarcraftSim.Core.Simulation.Engine;

public sealed class ResourceState
{
    public string ResourceKey { get; set; } = "";

    public decimal Maximum { get; set; }

    public decimal Current { get; private set; }

    public decimal RegenerationPerSecond { get; set; }

    public decimal LastUpdatedAtSeconds { get; private set; }

    public ResourceState()
    {
    }

    public ResourceState(
        string resourceKey,
        decimal maximum,
        decimal startingValue,
        decimal regenerationPerSecond = 0m)
    {
        ResourceKey = resourceKey;

        Maximum =
            Math.Max(
                0m,
                maximum
            );

        Current =
            Math.Clamp(
                startingValue,
                0m,
                Maximum
            );

        RegenerationPerSecond =
            Math.Max(
                0m,
                regenerationPerSecond
            );
    }

    public decimal AdvanceTo(
        decimal currentTimeSeconds)
    {
        if (currentTimeSeconds <=
            LastUpdatedAtSeconds)
        {
            return 0m;
        }

        var elapsedSeconds =
            currentTimeSeconds -
            LastUpdatedAtSeconds;

        LastUpdatedAtSeconds =
            currentTimeSeconds;

        if (
            elapsedSeconds <= 0m ||
            RegenerationPerSecond <= 0m ||
            Current >= Maximum
        )
        {
            return 0m;
        }

        var previousValue =
            Current;

        Current =
            Math.Min(
                Maximum,
                Current +
                RegenerationPerSecond *
                elapsedSeconds
            );

        return
            Current -
            previousValue;
    }

    public bool CanSpend(
        decimal amount)
    {
        return
            Current >=
            Math.Max(
                0m,
                amount
            );
    }

    public bool Spend(
        decimal amount)
    {
        if (amount < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount)
            );
        }

        if (!CanSpend(amount))
        {
            return false;
        }

        Current -=
            amount;

        return true;
    }

    public decimal Gain(
        decimal amount)
    {
        if (amount < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount)
            );
        }

        var previousValue =
            Current;

        Current =
            Math.Min(
                Maximum,
                Current + amount
            );

        return
            Current -
            previousValue;
    }

    public void SetCurrent(
        decimal value)
    {
        Current =
            Math.Clamp(
                value,
                0m,
                Maximum
            );
    }

    public decimal? GetTimeWhenAvailable(
        decimal amount,
        decimal currentTimeSeconds)
    {
        AdvanceTo(
            currentTimeSeconds
        );

        var requiredAmount =
            Math.Max(
                0m,
                amount
            );

        if (requiredAmount >
            Maximum)
        {
            return null;
        }

        if (Current >=
            requiredAmount)
        {
            return
                currentTimeSeconds;
        }

        if (RegenerationPerSecond <=
            0m)
        {
            return null;
        }

        var missingAmount =
            requiredAmount -
            Current;

        return
            currentTimeSeconds +
            missingAmount /
            RegenerationPerSecond;
    }
}
