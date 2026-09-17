using WarcraftSim.Core.GameData;

namespace WarcraftSim.Core.Resources;

public sealed class ResourceDefinition
{
    public string Key { get; set; } = "";

    public string Name { get; set; } = "";

    public decimal DefaultMaximum { get; set; }

    public ResourceStartMode StartMode { get; set; } = ResourceStartMode.Full;

    public decimal FixedStartingValue { get; set; }

    public decimal BaseRegenerationPerSecond { get; set; }

    public List<string> Tags { get; set; } = [];

    public GameDataRecordMetadata Metadata { get; set; } = new();
}