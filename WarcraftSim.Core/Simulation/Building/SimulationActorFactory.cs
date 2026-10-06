using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Core.Simulation.Building;

public static class SimulationActorFactory
{
    public static SimulationActorState Create(
        SimulationActorBuildDefinition build)
    {
        ArgumentNullException.ThrowIfNull(
            build
        );

        Validate(
            build
        );

        var actor =
            new SimulationActorState
            {
                Key =
                    build.Key,

                Name =
                    build.Name,

                TeamKey =
                    build.TeamKey,

                AssignedRole =
                    build.AssignedRole,

                Level =
                    build.Level
            };

        actor.InitializeHealth(
            build.MaximumHealth,
            build.StartingHealth
        );

        actor.ConfigureActionTiming(
            build.InitialActionDelaySeconds,
            build.InputDelaySeconds
        );

        foreach (
            var stat in
            build.Stats)
        {
            actor.Stats.Set(
                stat.Key,
                stat.Value
            );
        }

        foreach (
            var resource in
            build.Resources)
        {
            actor.AddResource(
                new ResourceState(
                    resourceKey:
                        resource.ResourceKey,

                    maximum:
                        resource.Maximum,

                    startingValue:
                        resource.StartingValue,

                    regenerationPerSecond:
                        resource.RegenerationPerSecond
                )
            );
        }

        foreach (
            var ability in
            build.Abilities)
        {
            actor.AddAbility(
                ability
            );
        }

        return actor;
    }

    private static void Validate(
        SimulationActorBuildDefinition build)
    {
        if (string.IsNullOrWhiteSpace(
                build.Key))
        {
            throw new ArgumentException(
                "Simulation actor build requires a key.",
                nameof(build)
            );
        }

        if (string.IsNullOrWhiteSpace(
                build.Name))
        {
            throw new ArgumentException(
                "Simulation actor build requires a name.",
                nameof(build)
            );
        }

        if (build.Level <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(build),
                "Simulation actor level must be greater than zero."
            );
        }

        if (build.MaximumHealth <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(build),
                "Simulation actor maximum health must be greater than zero."
            );
        }

        if (
            build.StartingHealth.HasValue &&
            (
                build.StartingHealth.Value < 0m ||
                build.StartingHealth.Value >
                    build.MaximumHealth
            )
        )
        {
            throw new ArgumentOutOfRangeException(
                nameof(build),
                "Starting health must be between zero and maximum health."
            );
        }

        ValidateResources(
            build
        );

        ValidateAbilities(
            build
        );
    }

    private static void ValidateResources(
        SimulationActorBuildDefinition build)
    {
        var keys =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase
            );

        foreach (
            var resource in
            build.Resources)
        {
            if (string.IsNullOrWhiteSpace(
                    resource.ResourceKey))
            {
                throw new ArgumentException(
                    "Simulation resources require a key.",
                    nameof(build)
                );
            }

            if (!keys.Add(
                    resource.ResourceKey))
            {
                throw new ArgumentException(
                    $"Duplicate simulation resource key '{resource.ResourceKey}'.",
                    nameof(build)
                );
            }

            if (resource.Maximum < 0m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(build),
                    $"Resource '{resource.ResourceKey}' cannot have a negative maximum."
                );
            }

            if (
                resource.StartingValue < 0m ||
                resource.StartingValue >
                    resource.Maximum
            )
            {
                throw new ArgumentOutOfRangeException(
                    nameof(build),
                    $"Resource '{resource.ResourceKey}' starting value must be between zero and its maximum."
                );
            }

            if (
                resource.RegenerationPerSecond <
                0m
            )
            {
                throw new ArgumentOutOfRangeException(
                    nameof(build),
                    $"Resource '{resource.ResourceKey}' cannot have negative regeneration."
                );
            }
        }
    }

    private static void ValidateAbilities(
        SimulationActorBuildDefinition build)
    {
        var keys =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase
            );

        foreach (
            var ability in
            build.Abilities)
        {
            if (ability is null)
            {
                throw new ArgumentException(
                    "Simulation actor abilities cannot contain null entries.",
                    nameof(build)
                );
            }

            if (string.IsNullOrWhiteSpace(
                    ability.Key))
            {
                throw new ArgumentException(
                    "Simulation abilities require a key.",
                    nameof(build)
                );
            }

            if (!keys.Add(
                    ability.Key))
            {
                throw new ArgumentException(
                    $"Duplicate simulation ability key '{ability.Key}'.",
                    nameof(build)
                );
            }
        }
    }
}
