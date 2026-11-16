using System.Text.Json;
using System.Text.RegularExpressions;

namespace WarcraftSim.Api.Simulation;

/// <summary>Display-only historical artwork references. Never imports stats or changes effects.</summary>
public static class ForeverExternalModifierIconCatalog
{
    public const string SourceCommit = "1cc5ae1e9abdbf941b7326d2d90421822dce31ba";
    public const string SourceUrl = "https://github.com/gunba/wow-forever-sim/blob/" + SourceCommit + "/assets/database/db.json";
    public const string VerificationStatus = "historical-reference-not-current-70707-verified";
    private const string SourceSha256 = "e342a7ce2dbc9ebc3e3beb2b0c23ed379b665f549ff64c97f4adec2ed61eb8de";

    public sealed class IconRecord
    {
        public string Key { get; set; } = "";
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public int SpellId { get; set; }
        public int? ItemId { get; set; }
        public string SourceKind { get; set; } = "";
        public int SourceId { get; set; }
        public string SourceName { get; set; } = "";
        public string IconKey { get; set; } = "";
        public string IconSourceStatus { get; set; } = "";
    }

    private sealed class Document
    {
        public string Schema { get; set; } = "";
        public string SourceCommit { get; set; } = "";
        public string SourceSha256 { get; set; } = "";
        public List<IconRecord> Records { get; set; } = [];
    }

    private static readonly Lazy<Document> Data = new(Load);

    private static Document Load()
    {
        var assembly = typeof(ForeverExternalModifierIconCatalog).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(x =>
            x.EndsWith("forever-modifier-icons-cp175-provisional.json", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource) ??
            throw new InvalidOperationException("Pinned modifier artwork reference is missing.");
        var document = JsonSerializer.Deserialize<Document>(stream,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)) ??
            throw new InvalidOperationException("Pinned modifier artwork reference is invalid.");
        if (document.Schema != "warcraftsim-forever-modifier-icons-v1" ||
            document.SourceCommit != SourceCommit || document.SourceSha256 != SourceSha256 ||
            document.Records.Select(x => x.Key).Distinct(StringComparer.Ordinal).Count() != document.Records.Count)
            throw new InvalidOperationException("Modifier artwork reference identity is invalid.");
        return document;
    }

    public static IconRecord? Match(ForeverExternalModifierDefinition entry)
    {
        var kind = entry.ItemId.HasValue ? "item" : "spell";
        var id = entry.ItemId ?? entry.SpellId;
        if (id <= 0) return null;
        var sourceName = Regex.Replace(entry.Name, @" \((?:Well Fed|Forever|Rank [1-9][0-9]*)\)$", "");
        return Data.Value.Records.SingleOrDefault(x =>
            x.Key == entry.Key && x.Name == entry.Name && x.Category == entry.Category &&
            x.ItemId == entry.ItemId && x.SpellId == entry.SpellId &&
            x.SourceKind == kind && x.SourceId == id && x.SourceName == sourceName &&
            x.IconSourceStatus == VerificationStatus && Regex.IsMatch(x.IconKey, @"^[a-z0-9_]+$"));
    }
}
