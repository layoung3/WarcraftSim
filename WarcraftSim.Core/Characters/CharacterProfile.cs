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

    public StatCollection BaseStats { get; set; } = new();
}