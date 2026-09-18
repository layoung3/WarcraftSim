using WarcraftSim.Core.GameData;

namespace WarcraftSim.Core.Auras;

public sealed class AuraDefinition
{
    public string Key { get; set; } = "";

    public string Name { get; set; } = "";

    public decimal DurationSeconds { get; set; }

    public AuraStackingMode StackingMode { get; set; } =
        AuraStackingMode.Refresh;

    public int MaxStacks { get; set; } = 1;

    public List<string> Tags { get; set; } = [];

    public GameDataRecordMetadata Metadata { get; set; } = new();
}