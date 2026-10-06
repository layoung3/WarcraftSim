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
}
