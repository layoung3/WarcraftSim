namespace WarcraftSim.Core.GameData;

public sealed class ExternalDataReference
{
    public string ProviderKey { get; set; } = "";

    public string EntityType { get; set; } = "";

    public string ExternalId { get; set; } = "";

    public string? Namespace { get; set; }

    public string? Version { get; set; }
}