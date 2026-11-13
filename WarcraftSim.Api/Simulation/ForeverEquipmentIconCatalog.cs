using System.Text.Json;
using System.Text.RegularExpressions;

namespace WarcraftSim.Api.Simulation;

/// <summary>Additional exact-identity artwork only; no numeric or eligibility import.</summary>
public static class ForeverEquipmentIconCatalog
{
    public const string SourceCommit = ForeverExternalModifierIconCatalog.SourceCommit;
    public const string VerificationStatus = ForeverExternalModifierIconCatalog.VerificationStatus;
    private const string SourceSha256 = "e342a7ce2dbc9ebc3e3beb2b0c23ed379b665f549ff64c97f4adec2ed61eb8de";

    public static void ApplyEmbedded(ForeverEquipmentCatalogDocument catalog)
    {
        var assembly = typeof(ForeverEquipmentIconCatalog).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(x =>
            x.EndsWith("forever-equipment-icons-cp175-provisional.json", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource) ??
            throw new InvalidOperationException("Pinned additional equipment artwork is missing.");
        using var reader = new StreamReader(stream);
        ApplyReviewedManifest(catalog, reader.ReadToEnd());
    }

    public static void ApplyReviewedManifest(ForeverEquipmentCatalogDocument catalog, string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.GetProperty("schema").GetString() != "warcraftsim-forever-equipment-icons-v1" ||
            root.GetProperty("sourceRepository").GetString() != "gunba/wow-forever-sim" ||
            root.GetProperty("sourceCommit").GetString() != SourceCommit ||
            root.GetProperty("sourceSha256").GetString() != SourceSha256 ||
            root.GetProperty("sourceStatus").GetString() != VerificationStatus)
            throw new InvalidOperationException("Additional equipment artwork source identity is invalid.");
        var rows = root.GetProperty("candidates").EnumerateArray().ToArray();
        var seen = new HashSet<int>();
        var updates = new List<(ForeverEquipmentItem Item, string Key)>();
        foreach (var row in rows)
        {
            var id = row.GetProperty("itemId").GetInt32();
            var name = row.GetProperty("itemName").GetString();
            var key = row.GetProperty("iconKey").GetString();
            if (!seen.Add(id) || string.IsNullOrWhiteSpace(key) || !Regex.IsMatch(key, @"^[a-z0-9_]+$") ||
                row.GetProperty("sourceStatus").GetString() != VerificationStatus)
                throw new InvalidOperationException("Additional equipment artwork row is invalid.");
            var item = catalog.Items.SingleOrDefault(x => x.Id == id && x.Name == name);
            if (item is null)
                throw new InvalidOperationException("Additional equipment artwork requires an exact registered identity.");
            if (item.IconKey is not null && item.IconKey != key)
                throw new InvalidOperationException("Additional equipment artwork conflicts with an existing source.");
            updates.Add((item, key));
        }
        // Validate every row before mutating any catalog item. Never overwrite
        // an existing matching mapping's original source status.
        foreach (var (item, key) in updates.Where(x => x.Item.IconKey is null))
        {
            item.IconKey = key;
            item.IconSourceStatus = VerificationStatus;
        }
    }
}
