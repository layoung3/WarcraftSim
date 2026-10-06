using WarcraftSim.Core.Stats;

namespace WarcraftSim.Core.Characters.Equipment;

public sealed class CharacterEquipmentModifier
{
    public string PlacementKey { get; set; } = "";

    public string ModifierKey { get; set; } = "";

    public string ModifierType { get; set; } = "";

    public string Name { get; set; } = "";

    public StatCollection Stats { get; set; } =
        new();
}
