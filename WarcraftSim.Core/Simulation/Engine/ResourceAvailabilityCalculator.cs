using WarcraftSim.Core.Abilities;

namespace WarcraftSim.Core.Simulation.Engine;

public static class ResourceAvailabilityCalculator
{
    public static decimal? GetNextAffordableTime(
        SimulationActorState actor,
        AbilityDefinition ability,
        decimal currentTimeSeconds)
    {
        ArgumentNullException.ThrowIfNull(
            ability
        );

        return GetNextAffordableTime(
            actor,
            ability.ResourceCosts,
            currentTimeSeconds
        );
    }

    public static decimal? GetNextAffordableTime(
        SimulationActorState actor,
        IEnumerable<AbilityResourceCost> resourceCosts,
        decimal currentTimeSeconds)
    {
        ArgumentNullException.ThrowIfNull(
            actor
        );

        ArgumentNullException.ThrowIfNull(
            resourceCosts
        );

        actor.RefreshResources(
            currentTimeSeconds
        );

        var readyAt =
            currentTimeSeconds;

        foreach (
            var resourceCost in
            resourceCosts)
        {
            if (!actor.Resources.TryGetValue(
                    resourceCost.ResourceKey,
                    out var resource))
            {
                return null;
            }

            var requiredAmount =
                GetResourceCost(
                    resourceCost,
                    resource
                );

            var resourceReadyAt =
                resource.GetTimeWhenAvailable(
                    requiredAmount,
                    currentTimeSeconds
                );

            if (!resourceReadyAt.HasValue)
            {
                return null;
            }

            readyAt =
                Math.Max(
                    readyAt,
                    resourceReadyAt.Value
                );
        }

        return readyAt;
    }

    private static decimal GetResourceCost(
        AbilityResourceCost resourceCost,
        ResourceState resource)
    {
        if (!resourceCost.IsPercentOfMaximum)
        {
            return
                Math.Max(
                    0m,
                    resourceCost.Amount
                );
        }

        return
            Math.Max(
                0m,
                resource.Maximum *
                resourceCost.Amount /
                100m
            );
    }
}
