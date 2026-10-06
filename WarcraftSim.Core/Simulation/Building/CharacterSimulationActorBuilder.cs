namespace WarcraftSim.Core.Simulation.Building;

public sealed class CharacterSimulationActorBuilder
{
    private readonly CharacterSimulationClassCatalog
        _classCatalog;

    private readonly ICharacterSimulationRuntimeCalculator?
        _runtimeCalculator;

    public CharacterSimulationActorBuilder(
        CharacterSimulationClassCatalog classCatalog)
        : this(
            classCatalog,
            runtimeCalculator:
                null
        )
    {
    }

    public CharacterSimulationActorBuilder(
        CharacterSimulationClassCatalog classCatalog,
        ICharacterSimulationRuntimeCalculator? runtimeCalculator)
    {
        ArgumentNullException.ThrowIfNull(
            classCatalog
        );

        _classCatalog =
            classCatalog;

        _runtimeCalculator =
            runtimeCalculator;
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

        var mappingOptions =
            ResolveMappingOptions(
                request
            );

        var buildDefinition =
            CharacterProfileSimulationMapper
                .ToBuildDefinition(
                    request.Profile,
                    mappingOptions,
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

    private CharacterSimulationMappingOptions ResolveMappingOptions(
        CharacterSimulationBuildRequest request)
    {
        if (_runtimeCalculator is null)
        {
            return request.Options;
        }

        var calculation =
            _runtimeCalculator.Calculate(
                request.Profile
            );

        ArgumentNullException.ThrowIfNull(
            calculation
        );

        if (calculation.MaximumHealth <= 0m)
        {
            throw new InvalidOperationException(
                "Character runtime calculation must produce maximum health greater than zero."
            );
        }

        if (
            calculation.StartingHealth.HasValue &&
            (
                calculation.StartingHealth.Value < 0m ||
                calculation.StartingHealth.Value >
                    calculation.MaximumHealth
            )
        )
        {
            throw new InvalidOperationException(
                "Character runtime calculation starting health must be between zero and maximum health."
            );
        }

        return new CharacterSimulationMappingOptions
        {
            ActorKey =
                request.Options.ActorKey,

            TeamKey =
                request.Options.TeamKey,

            AssignedRole =
                request.Options.AssignedRole,

            MaximumHealth =
                calculation.MaximumHealth,

            StartingHealth =
                calculation.StartingHealth,

            InitialActionDelaySeconds =
                request.Options.InitialActionDelaySeconds,

            InputDelaySeconds =
                request.Options.InputDelaySeconds,

            EffectiveStats =
                calculation.EffectiveStats
        };
    }
}
