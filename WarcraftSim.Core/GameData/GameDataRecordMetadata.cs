namespace WarcraftSim.Core.GameData;

public sealed class GameDataRecordMetadata
{
    public List<ExternalDataReference> ExternalReferences { get; set; } = [];

    public string RulesetKey { get; set; } = "";

    public string RulesetVersion { get; set; } = "";

    public DateTimeOffset? LastUpdatedUtc { get; set; }

    public bool HasManualOverrides { get; set; }
}