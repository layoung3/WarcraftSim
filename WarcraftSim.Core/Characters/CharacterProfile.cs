using WarcraftSim.Core.Characters.Buffs;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Core.Characters.Talents;
using WarcraftSim.Core.Stats;

namespace WarcraftSim.Core.Characters;

public sealed class CharacterProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "New Character";

    public string RulesetKey { get; set; } = "";

    public string ClassKey { get; set; } = "";

    public string? SpecializationKey { get; set; }

    public int Level { get; set; } = 1;

    // Only used for verifiable equipment faction restrictions in CP170.
    // Racial combat abilities remain a later modifier phase.
    public string? Race { get; set; }

    public StatCollection BaseStats { get; set; } = new();

    public CharacterEquipmentLoadout Equipment { get; set; } =
        new();

    public CharacterTalentLoadout Talents { get; set; } =
        new();

    public CharacterBuffLoadout Buffs { get; set; } =
        new();
}