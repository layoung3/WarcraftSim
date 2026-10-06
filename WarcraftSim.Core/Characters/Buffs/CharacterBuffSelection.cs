using WarcraftSim.Core.Stats;

namespace WarcraftSim.Core.Characters.Buffs;

public sealed class CharacterBuffSelection
{
    public string BuffKey { get; set; } = "";

    public string Name { get; set; } = "";

    public bool IsEnabled { get; set; } = true;

    public StatCollection Stats { get; set; } =
        new();
}
