using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Core.Simulation.Building;

public sealed class CharacterSimulationRosterBuildResult
{
    public required string Key { get; init; }

    public required string Name { get; init; }

    public required IReadOnlyList<CharacterSimulationRosterMemberBuildResult>
        Members
    {
        get;
        init;
    }

    public IReadOnlyList<SimulationActorState> Actors =>
        Members
            .Select(
                member =>
                    member.Actor
            )
            .ToArray();

    public void AddActorsTo(
        SimulationContext context)
    {
        ArgumentNullException.ThrowIfNull(
            context
        );

        foreach (
            var member in
            Members)
        {
            if (context.GetActor(
                    member.Actor.Key) is not null)
            {
                throw new InvalidOperationException(
                    $"Simulation context already contains actor key '{member.Actor.Key}'."
                );
            }
        }

        foreach (
            var member in
            Members)
        {
            context.AddActor(
                member.Actor
            );
        }
    }
}
