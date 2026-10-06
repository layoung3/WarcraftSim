using WarcraftSim.Core.Stats;

namespace WarcraftSim.Core.Characters.Talents;

public sealed class CharacterTalentSelection
{
    public string TalentKey { get; set; } = "";

    public string Name { get; set; } = "";

    public int Rank { get; set; } = 1;

    public StatCollection Stats { get; set; } =
        new();
}
