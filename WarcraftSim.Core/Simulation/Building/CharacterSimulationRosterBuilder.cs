namespace WarcraftSim.Core.Simulation.Building;

public sealed class CharacterSimulationRosterBuilder
{
    private readonly CharacterSimulationActorBuilder
        _actorBuilder;

    public CharacterSimulationRosterBuilder(
        CharacterSimulationActorBuilder actorBuilder)
    {
        ArgumentNullException.ThrowIfNull(
            actorBuilder
        );

        _actorBuilder =
            actorBuilder;
    }

    public CharacterSimulationRosterBuildResult Build(
        CharacterSimulationRosterBuildRequest request)
    {
        ArgumentNullException.ThrowIfNull(
            request
        );

        if (string.IsNullOrWhiteSpace(
                request.Key))
        {
            throw new ArgumentException(
                "Simulation roster requires a key.",
                nameof(request)
            );
        }

        if (string.IsNullOrWhiteSpace(
                request.Name))
        {
            throw new ArgumentException(
                "Simulation roster requires a name.",
                nameof(request)
            );
        }

        ArgumentNullException.ThrowIfNull(
            request.Members
        );

        if (request.Members.Count == 0)
        {
            throw new ArgumentException(
                "Simulation roster requires at least one member.",
                nameof(request)
            );
        }

        ValidateActorKeys(
            request.Members
        );

        var members =
            new List<CharacterSimulationRosterMemberBuildResult>(
                request.Members.Count
            );

        foreach (
            var memberRequest in
            request.Members)
        {
            ArgumentNullException.ThrowIfNull(
                memberRequest
            );

            var character =
                _actorBuilder.Build(
                    memberRequest
                );

            members.Add(
                new CharacterSimulationRosterMemberBuildResult
                {
                    Profile =
                        memberRequest.Profile,

                    Character =
                        character
                }
            );
        }

        return new CharacterSimulationRosterBuildResult
        {
            Key =
                request.Key.Trim(),

            Name =
                request.Name.Trim(),

            Members =
                members
        };
    }

    private static void ValidateActorKeys(
        IReadOnlyList<CharacterSimulationBuildRequest> members)
    {
        var actorKeys =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase
            );

        foreach (
            var member in
            members)
        {
            ArgumentNullException.ThrowIfNull(
                member
            );

            ArgumentNullException.ThrowIfNull(
                member.Profile
            );

            ArgumentNullException.ThrowIfNull(
                member.Options
            );

            var actorKey =
                string.IsNullOrWhiteSpace(
                    member.Options.ActorKey)
                    ? member.Profile.Id.ToString("N")
                    : member.Options.ActorKey;

            if (!actorKeys.Add(
                    actorKey))
            {
                throw new InvalidOperationException(
                    $"Simulation roster contains duplicate actor key '{actorKey}'."
                );
            }
        }
    }
}
