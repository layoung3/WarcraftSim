using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Characters.Equipment;

namespace WarcraftSim.Core.Simulation.Building;

public sealed class CharacterEquipmentStatSourceAdapter
{
    public const string EquipmentSourceType =
        "equipment";

    public IReadOnlyList<CharacterSimulationStatBonusSource>
        CreateSources(
            CharacterProfile profile)
    {
        ArgumentNullException.ThrowIfNull(
            profile
        );

        ArgumentNullException.ThrowIfNull(
            profile.Equipment
        );

        ArgumentNullException.ThrowIfNull(
            profile.Equipment.Items
        );

        var slotKeys =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase
            );

        var sources =
            new List<CharacterSimulationStatBonusSource>();

        foreach (
            var item in
            profile.Equipment.Items)
        {
            ValidateItem(
                item
            );

            var normalizedSlotKey =
                item.SlotKey.Trim();

            var normalizedItemKey =
                item.ItemKey.Trim();

            if (!slotKeys.Add(
                    normalizedSlotKey))
            {
                throw new InvalidOperationException(
                    $"Character equipment contains duplicate slot key '{normalizedSlotKey}'."
                );
            }

            var sourceKey =
                $"equipment:{normalizedSlotKey}:{normalizedItemKey}";

            var sourceName =
                string.IsNullOrWhiteSpace(
                    item.Name)
                    ? normalizedItemKey
                    : item.Name.Trim();

            sources.Add(
                new CharacterSimulationStatBonusSource(
                    key:
                        sourceKey,

                    sourceType:
                        EquipmentSourceType,

                    name:
                        sourceName,

                    statBonuses:
                        item.Stats.Values
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

    private static void ValidateItem(
        CharacterEquipmentItem? item)
    {
        if (item is null)
        {
            throw new InvalidOperationException(
                "Character equipment cannot contain null items."
            );
        }

        if (string.IsNullOrWhiteSpace(
                item.SlotKey))
        {
            throw new InvalidOperationException(
                "Equipped items require a slot key."
            );
        }

        if (string.IsNullOrWhiteSpace(
                item.ItemKey))
        {
            throw new InvalidOperationException(
                $"Equipped item in slot '{item.SlotKey}' requires an item key."
            );
        }

        ArgumentNullException.ThrowIfNull(
            item.Stats
        );

        ArgumentNullException.ThrowIfNull(
            item.Stats.Values
        );
    }
}
