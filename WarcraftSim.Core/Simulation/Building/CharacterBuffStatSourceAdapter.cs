using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Characters.Buffs;

namespace WarcraftSim.Core.Simulation.Building;

public sealed class CharacterBuffStatSourceAdapter
{
    public const string BuffSourceType =
        "buff";

    public IReadOnlyList<CharacterSimulationStatBonusSource>
        CreateSources(
            CharacterProfile profile)
    {
        ArgumentNullException.ThrowIfNull(
            profile
        );

        ArgumentNullException.ThrowIfNull(
            profile.Buffs
        );

        ArgumentNullException.ThrowIfNull(
            profile.Buffs.Selections
        );

        var buffKeys =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase
            );

        var sources =
            new List<CharacterSimulationStatBonusSource>();

        foreach (
            var selection in
            profile.Buffs.Selections)
        {
            ValidateSelection(
                selection
            );

            var normalizedBuffKey =
                selection.BuffKey.Trim();

            if (!buffKeys.Add(
                    normalizedBuffKey))
            {
                throw new InvalidOperationException(
                    $"Character buffs contain duplicate buff key '{normalizedBuffKey}'."
                );
            }

            var sourceName =
                string.IsNullOrWhiteSpace(
                    selection.Name)
                    ? normalizedBuffKey
                    : selection.Name.Trim();

            sources.Add(
                new CharacterSimulationStatBonusSource(
                    key:
                        $"buff:{normalizedBuffKey}",

                    sourceType:
                        BuffSourceType,

                    name:
                        sourceName,

                    statBonuses:
                        selection.Stats.Values,

                    isActive:
                        selection.IsEnabled
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
        CharacterBuffSelection? selection)
    {
        if (selection is null)
        {
            throw new InvalidOperationException(
                "Character buff loadout cannot contain null selections."
            );
        }

        if (string.IsNullOrWhiteSpace(
                selection.BuffKey))
        {
            throw new InvalidOperationException(
                "Selected buffs require a buff key."
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
