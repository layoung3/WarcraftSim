using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Stats;

namespace WarcraftSim.Core.Simulation.Building;

public static class CharacterProfileSimulationMapper
{
    public static SimulationActorBuildDefinition ToBuildDefinition(
        CharacterProfile profile,
        CharacterSimulationMappingOptions options)
    {
        ArgumentNullException.ThrowIfNull(
            profile
        );

        ArgumentNullException.ThrowIfNull(
            options
        );

        if (options.MaximumHealth <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Maximum health must be greater than zero."
            );
        }

        var statSource =
            options.EffectiveStats ??
            profile.BaseStats;

        var build =
            new SimulationActorBuildDefinition
            {
                Key =
                    string.IsNullOrWhiteSpace(
                        options.ActorKey)
                        ? profile.Id.ToString("N")
                        : options.ActorKey,

                Name =
                    profile.Name,

                TeamKey =
                    options.TeamKey,

                AssignedRole =
                    options.AssignedRole,

                Level =
                    profile.Level,

                MaximumHealth =
                    options.MaximumHealth,

                StartingHealth =
                    options.StartingHealth,

                InitialActionDelaySeconds =
                    options.InitialActionDelaySeconds,

                InputDelaySeconds =
                    options.InputDelaySeconds
            };

        CopyStats(
            statSource,
            build
        );

        return build;
    }

    public static SimulationActorBuildDefinition ToBuildDefinition(
        CharacterProfile profile,
        CharacterSimulationMappingOptions options,
        CharacterSimulationClassDefinition classDefinition)
    {
        ArgumentNullException.ThrowIfNull(
            classDefinition
        );

        ValidateClassDefinitionMatchesProfile(
            profile,
            classDefinition
        );

        var build =
            ToBuildDefinition(
                profile,
                options
            );

        CopyResources(
            classDefinition,
            build
        );

        CopyAbilities(
            classDefinition,
            build
        );

        return build;
    }

    public static Simulation.Engine.SimulationActorState ToActor(
        CharacterProfile profile,
        CharacterSimulationMappingOptions options)
    {
        return SimulationActorFactory.Create(
            ToBuildDefinition(
                profile,
                options
            )
        );
    }

    public static Simulation.Engine.SimulationActorState ToActor(
        CharacterProfile profile,
        CharacterSimulationMappingOptions options,
        CharacterSimulationClassDefinition classDefinition)
    {
        return SimulationActorFactory.Create(
            ToBuildDefinition(
                profile,
                options,
                classDefinition
            )
        );
    }

    private static void CopyStats(
        StatCollection source,
        SimulationActorBuildDefinition destination)
    {
        foreach (
            var stat in
            source.Values)
        {
            destination.Stats[
                stat.Key
            ] =
                stat.Value;
        }
    }

    private static void CopyResources(
        CharacterSimulationClassDefinition source,
        SimulationActorBuildDefinition destination)
    {
        foreach (
            var resource in
            source.Resources)
        {
            destination.Resources.Add(
                new SimulationResourceBuildDefinition
                {
                    ResourceKey =
                        resource.ResourceKey,

                    Maximum =
                        resource.Maximum,

                    StartingValue =
                        resource.StartingValue,

                    RegenerationPerSecond =
                        resource.RegenerationPerSecond
                }
            );
        }
    }

    private static void CopyAbilities(
        CharacterSimulationClassDefinition source,
        SimulationActorBuildDefinition destination)
    {
        foreach (
            var ability in
            source.Abilities)
        {
            destination.Abilities.Add(
                ability
            );
        }
    }

    private static void ValidateClassDefinitionMatchesProfile(
        CharacterProfile profile,
        CharacterSimulationClassDefinition classDefinition)
    {
        if (
            !string.Equals(
                profile.RulesetKey,
                classDefinition.RulesetKey,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            throw new InvalidOperationException(
                $"Character ruleset '{profile.RulesetKey}' does not match simulation class definition ruleset '{classDefinition.RulesetKey}'."
            );
        }

        if (
            !string.Equals(
                profile.ClassKey,
                classDefinition.ClassKey,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            throw new InvalidOperationException(
                $"Character class '{profile.ClassKey}' does not match simulation class definition class '{classDefinition.ClassKey}'."
            );
        }

        // A null specialization on the simulation definition means the
        // definition is class-wide and can be used by any specialization.
        if (
            !string.IsNullOrWhiteSpace(
                classDefinition.SpecializationKey) &&
            !string.Equals(
                profile.SpecializationKey,
                classDefinition.SpecializationKey,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            throw new InvalidOperationException(
                $"Character specialization '{profile.SpecializationKey}' does not match simulation class definition specialization '{classDefinition.SpecializationKey}'."
            );
        }
    }
}
