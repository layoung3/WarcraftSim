using WarcraftSim.Core.Stats;

namespace WarcraftSim.Core.Simulation.Building;

public sealed class CharacterSimulationRuntimeCalculationResult
{
    public decimal MaximumHealth { get; init; }

    public decimal? StartingHealth { get; init; }

    public StatCollection EffectiveStats { get; init; } =
        new();
}
