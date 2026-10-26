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

        // Every cost component of the same resource is due at the same
        // completion time. Check their SUM, not their individual readiness:
        // otherwise two affordable 60-Mana components can be advertised
        // as affordable with only 100 Mana available.
        var totals=ResolveCombinedCosts(actor,resourceCosts);
        if(totals is null)return null;

        var readyAt=currentTimeSeconds;
        foreach(var (resourceKey,requiredAmount) in totals)
        {
            var resource=actor.Resources[resourceKey];
            var resourceReadyAt=resource.GetTimeWhenAvailable(
                requiredAmount,currentTimeSeconds);
            if(!resourceReadyAt.HasValue)return null;
            readyAt=Math.Max(readyAt,resourceReadyAt.Value);
        }
        return readyAt;
    }


    /// <summary>
    /// Returns the combined up-front cost for each resource. The same totals
    /// must be used by cast eligibility, cast completion and readiness
    /// prediction to prevent partial spending or premature wake-ups.
    /// An unknown resource makes the ability unavailable.
    /// </summary>
    public static Dictionary<string,decimal>? ResolveCombinedCosts(
        SimulationActorState actor,IEnumerable<AbilityResourceCost> resourceCosts)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(resourceCosts);
        var totals=new Dictionary<string,decimal>(StringComparer.OrdinalIgnoreCase);
        foreach(var cost in resourceCosts)
        {
            ArgumentNullException.ThrowIfNull(cost);
            if(!actor.Resources.TryGetValue(cost.ResourceKey,out var resource))
                return null;
            if(cost.Amount<0m)
                throw new ArgumentOutOfRangeException(nameof(resourceCosts),
                    "Ability resource cost cannot be negative.");
            var amount=GetResourceCost(cost,resource);
            totals[cost.ResourceKey]=totals.GetValueOrDefault(cost.ResourceKey)+amount;
        }
        return totals;
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
