using WarcraftSim.Core.Stats;

namespace WarcraftSim.Core.Characters.Equipment;

public sealed class CharacterEquipmentItem
{
    public string SlotKey { get; set; } = "";

    public string ItemKey { get; set; } = "";

    // Optional server-owned catalog selection. When set, client-supplied Stats
    // and Modifiers are never accepted as item data.
    public int? ItemId { get; set; }

    public int? EnchantSpellId { get; set; }

    public string Name { get; set; } = "";

    public StatCollection Stats { get; set; } =
        new();

    public List<CharacterEquipmentModifier> Modifiers { get; set; } =
        [];
}
