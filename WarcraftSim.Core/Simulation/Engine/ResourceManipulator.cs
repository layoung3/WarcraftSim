using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Core.Simulation.Engine;

public static class ResourceManipulator
{
    public static ResourceChangeResult Apply(
        ResourceState resource,
        string operation,
        decimal amount,
        bool amountIsPercentOfMaximum = false)
    {
        ArgumentNullException.ThrowIfNull(
            resource
        );

        if (amount < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                amount,
                "Resource change amount cannot be negative."
            );
        }

        var resolvedAmount =
            amountIsPercentOfMaximum
                ? resource.Maximum *
                  amount /
                  100m
                : amount;

        var previousValue =
            resource.Current;

        if (string.Equals(
                operation,
                ResourceChangeOperationTypes.Gain,
                StringComparison.OrdinalIgnoreCase))
        {
            resource.Gain(
                resolvedAmount
            );
        }
        else if (string.Equals(
                     operation,
                     ResourceChangeOperationTypes.Spend,
                     StringComparison.OrdinalIgnoreCase))
        {
            resource.SetCurrent(
                previousValue -
                resolvedAmount
            );
        }
        else if (string.Equals(
                     operation,
                     ResourceChangeOperationTypes.Set,
                     StringComparison.OrdinalIgnoreCase))
        {
            resource.SetCurrent(
                resolvedAmount
            );
        }
        else
        {
            throw new InvalidOperationException(
                $"Unknown resource change operation '{operation}'."
            );
        }

        return new ResourceChangeResult
        {
            PreviousValue =
                previousValue,

            CurrentValue =
                resource.Current
        };
    }
}
