namespace WarcraftSim.Core.Simulation.Building;

public sealed class CharacterSimulationActorBuilder
{
    private readonly CharacterSimulationClassCatalog
        _classCatalog;

    public CharacterSimulationActorBuilder(
        CharacterSimulationClassCatalog classCatalog)
    {
        ArgumentNullException.ThrowIfNull(
            classCatalog
        );

        _classCatalog =
            classCatalog;
    }

    public CharacterSimulationBuildResult Build(
        CharacterSimulationBuildRequest request)
    {
        ArgumentNullException.ThrowIfNull(
            request
        );

        ArgumentNullException.ThrowIfNull(
            request.Profile
        );

        ArgumentNullException.ThrowIfNull(
            request.Options
        );

        var classDefinition =
            _classCatalog.Resolve(
                request.Profile
            );

        var buildDefinition =
            CharacterProfileSimulationMapper
                .ToBuildDefinition(
                    request.Profile,
                    request.Options,
                    classDefinition
                );

        var actor =
            SimulationActorFactory.Create(
                buildDefinition
            );

        return new CharacterSimulationBuildResult
        {
            ClassDefinition =
                classDefinition,

            BuildDefinition =
                buildDefinition,

            Actor =
                actor
        };
    }
}
