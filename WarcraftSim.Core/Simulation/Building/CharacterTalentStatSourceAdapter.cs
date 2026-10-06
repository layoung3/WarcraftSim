using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Characters.Talents;

namespace WarcraftSim.Core.Simulation.Building;

public sealed class CharacterTalentStatSourceAdapter
{
    public const string TalentSourceType =
        "talent";

    public IReadOnlyList<CharacterSimulationStatBonusSource>
        CreateSources(
            CharacterProfile profile)
    {
        ArgumentNullException.ThrowIfNull(
            profile
        );

        ArgumentNullException.ThrowIfNull(
            profile.Talents
        );

        ArgumentNullException.ThrowIfNull(
            profile.Talents.Selections
        );

        var talentKeys =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase
            );

        var sources =
            new List<CharacterSimulationStatBonusSource>();

        foreach (
            var selection in
            profile.Talents.Selections)
        {
            ValidateSelection(
                selection
            );

            var normalizedTalentKey =
                selection.TalentKey.Trim();

            if (!talentKeys.Add(
                    normalizedTalentKey))
            {
                throw new InvalidOperationException(
                    $"Character talents contain duplicate talent key '{normalizedTalentKey}'."
                );
            }

            var sourceName =
                string.IsNullOrWhiteSpace(
                    selection.Name)
                    ? normalizedTalentKey
                    : selection.Name.Trim();

            sources.Add(
                new CharacterSimulationStatBonusSource(
                    key:
                        $"talent:{normalizedTalentKey}",

                    sourceType:
                        TalentSourceType,

                    name:
                        sourceName,

                    statBonuses:
                        selection.Stats.Values
                )
            );
        }

        return sources
            .OrderBy(
                source =>
                    source.Key,
                StringComparer.OrdinalIgnoreCase
            )
            .ToList();
    }

    private static void ValidateSelection(
        CharacterTalentSelection? selection)
    {
        if (selection is null)
        {
            throw new InvalidOperationException(
                "Character talent loadout cannot contain null selections."
            );
        }

        if (string.IsNullOrWhiteSpace(
                selection.TalentKey))
        {
            throw new InvalidOperationException(
                "Selected talents require a talent key."
            );
        }

        if (selection.Rank < 1)
        {
            throw new InvalidOperationException(
                $"Selected talent '{selection.TalentKey}' requires a rank of at least 1."
            );
        }

        ArgumentNullException.ThrowIfNull(
            selection.Stats
        );

        ArgumentNullException.ThrowIfNull(
            selection.Stats.Values
        );
    }
}
