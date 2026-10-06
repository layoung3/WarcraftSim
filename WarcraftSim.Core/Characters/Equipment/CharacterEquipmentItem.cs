using WarcraftSim.Core.Stats;

namespace WarcraftSim.Core.Characters.Equipment;

public sealed class CharacterEquipmentItem
{
    public string SlotKey { get; set; } = "";

    public string ItemKey { get; set; } = "";

    public string Name { get; set; } = "";

    public StatCollection Stats { get; set; } =
        new();
}
