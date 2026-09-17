namespace WarcraftSim.Core.Simulation.Engine;

public sealed class ResourceState
{
    public string ResourceKey { get; set; } = "";

    public decimal Maximum { get; set; }

    public decimal Current { get; private set; }

    public ResourceState()
    {
    }

    public ResourceState(
        string resourceKey,
        decimal maximum,
        decimal startingValue)
    {
        ResourceKey = resourceKey;
        Maximum = Math.Max(0m, maximum);

        Current = Math.Clamp(
            startingValue,
            0m,
            Maximum
        );
    }

    public bool CanSpend(decimal amount)
    {
        return Current >= amount;
    }

    public bool Spend(decimal amount)
    {
        if (amount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        if (!CanSpend(amount))
        {
            return false;
        }

        Current -= amount;

        return true;
    }

    public decimal Gain(decimal amount)
    {
        if (amount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        var previousValue = Current;

        Current = Math.Min(
            Maximum,
            Current + amount
        );

        return Current - previousValue;
    }

    public void SetCurrent(decimal value)
    {
        Current = Math.Clamp(
            value,
            0m,
            Maximum
        );
    }
}