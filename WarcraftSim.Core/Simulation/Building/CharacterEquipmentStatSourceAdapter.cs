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

            AddModifierSources(
                sources,
                item,
                normalizedSlotKey,
                normalizedItemKey
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

    private static void AddModifierSources(
        ICollection<CharacterSimulationStatBonusSource> sources,
        CharacterEquipmentItem item,
        string normalizedSlotKey,
        string normalizedItemKey)
    {
        ArgumentNullException.ThrowIfNull(
            item.Modifiers
        );

        var placementKeys =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase
            );

        foreach (
            var modifier in
            item.Modifiers)
        {
            ValidateModifier(
                item,
                modifier
            );

            var normalizedPlacementKey =
                modifier.PlacementKey.Trim();

            if (!placementKeys.Add(
                    normalizedPlacementKey))
            {
                throw new InvalidOperationException(
                    $"Equipped item '{normalizedItemKey}' in slot '{normalizedSlotKey}' contains duplicate modifier placement key '{normalizedPlacementKey}'."
                );
            }

            var normalizedModifierKey =
                modifier.ModifierKey.Trim();

            var normalizedModifierType =
                modifier.ModifierType.Trim();

            var sourceName =
                string.IsNullOrWhiteSpace(
                    modifier.Name)
                    ? normalizedModifierKey
                    : modifier.Name.Trim();

            sources.Add(
                new CharacterSimulationStatBonusSource(
                    key:
                        $"equipment:{normalizedSlotKey}:{normalizedItemKey}:{normalizedModifierType}:{normalizedPlacementKey}:{normalizedModifierKey}",

                    sourceType:
                        normalizedModifierType,

                    name:
                        sourceName,

                    statBonuses:
                        modifier.Stats.Values
                )
            );
        }
    }

    private static void ValidateModifier(
        CharacterEquipmentItem item,
        CharacterEquipmentModifier? modifier)
    {
        if (modifier is null)
        {
            throw new InvalidOperationException(
                $"Equipped item '{item.ItemKey}' cannot contain null modifiers."
            );
        }

        if (string.IsNullOrWhiteSpace(
                modifier.PlacementKey))
        {
            throw new InvalidOperationException(
                $"Equipment modifier on item '{item.ItemKey}' requires a placement key."
            );
        }

        if (string.IsNullOrWhiteSpace(
                modifier.ModifierKey))
        {
            throw new InvalidOperationException(
                $"Equipment modifier at placement '{modifier.PlacementKey}' on item '{item.ItemKey}' requires a modifier key."
            );
        }

        if (string.IsNullOrWhiteSpace(
                modifier.ModifierType))
        {
            throw new InvalidOperationException(
                $"Equipment modifier '{modifier.ModifierKey}' on item '{item.ItemKey}' requires a modifier type."
            );
        }

        ArgumentNullException.ThrowIfNull(
            modifier.Stats
        );

        ArgumentNullException.ThrowIfNull(
            modifier.Stats.Values
        );
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
