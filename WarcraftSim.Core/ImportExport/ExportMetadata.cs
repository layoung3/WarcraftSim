namespace WarcraftSim.Core.ImportExport;

public sealed class ExportMetadata
{
    public string Format { get; set; } = "WarcraftSim";

    public int SchemaVersion { get; set; } = 1;

    public string PackageType { get; set; } = "";

    public string RulesetKey { get; set; } = "";

    public string? RulesetVersion { get; set; }

    public string? AppVersion { get; set; }

    public DateTimeOffset ExportedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}