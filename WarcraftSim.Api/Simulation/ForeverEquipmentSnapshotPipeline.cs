using System.Text.Json;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Deterministic, offline ingestion of independently reviewed Forever item snapshots.
/// External metadata is never promoted into combat statistics by this pipeline.
/// The output uses CP170's ForeverEquipmentCatalogDocument and shared resolver.
/// </summary>
public static class ForeverEquipmentSnapshotPipeline
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private static readonly HashSet<string> SupportedStats = new(StringComparer.OrdinalIgnoreCase)
    {
        "strength", "agility", "stamina", "intellect", "spirit", "armor",
        "attack-power", "melee-attack-power", "ranged-attack-power",
        "spell-power", "healing-power", "hit-rating", "critical-strike-rating",
        "haste-rating", "expertise-rating", "defense", "defense-rating",
        "hit-chance-percent", "attack-critical-chance-percent",
        "spell-critical-chance-percent", "spell-hit-chance-percent",
        "expertise-percent", "mana", "health", "mana-per-5"
    };

    /// <summary>Import a fully reviewed, pinned-source manifest. Caller supplies
    /// the baseline. Neither the network nor current clock is read.</summary>
    public static ForeverEquipmentImportResult Import(
        string manifestJson, ForeverEquipmentCatalogDocument baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        if (string.IsNullOrWhiteSpace(manifestJson))
            throw new ArgumentException("A versioned import manifest is required.", nameof(manifestJson));

        var manifest = JsonSerializer.Deserialize<ForeverEquipmentImportManifest>(
            manifestJson, Json) ?? throw new InvalidOperationException("Manifest is empty.");
        if (string.IsNullOrWhiteSpace(manifest.CatalogVersion) ||
            string.IsNullOrWhiteSpace(manifest.ClientBuild) ||
            string.IsNullOrWhiteSpace(manifest.ContentVersion) ||
            string.IsNullOrWhiteSpace(manifest.SourceName) ||
            !IsHttps(manifest.SourceUrl) ||
            manifest.ImportedAt == default)
            throw new InvalidOperationException(
                "Import requires catalog/content versions, build, source, HTTPS URL and explicit timestamp.");
        if (string.Equals(manifest.CatalogVersion, baseline.CatalogVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("An imported catalog must have a distinct version.");
        if (manifest.Items is null || manifest.MetadataOnly is null ||
            manifest.Overrides is null)
            throw new InvalidOperationException("Import lists must not be null.");

        var ids = new HashSet<int>();
        foreach (var item in manifest.Items)
        {
            if (item is null || !ids.Add(item.Id))
                throw new InvalidOperationException("Duplicate or null reviewed item.");
            ValidateItem(item, manifest.ClientBuild);
        }

        foreach (var metadata in manifest.MetadataOnly)
        {
            if (metadata is null || metadata.Id <= 0 ||
                !ids.Add(metadata.Id) || string.IsNullOrWhiteSpace(metadata.Name) ||
                !IsHttps(metadata.SourceUrl) ||
                metadata.VerificationStatus != "metadata-only")
                throw new InvalidOperationException("Invalid or duplicate metadata-only candidate.");
        }

        var declarations = manifest.Overrides.ToDictionary(x => x.ItemId);
        if (declarations.Count != manifest.Overrides.Count ||
            declarations.Values.Any(x => x.ItemId <= 0 ||
                string.IsNullOrWhiteSpace(x.Reason) || !IsHttps(x.SourceUrl)))
            throw new InvalidOperationException("Conflicting or unsubstantiated override declaration.");

        // Deep copy; never mutate the cached CP170 baseline or caller's manifest.
        var document = JsonSerializer.Deserialize<ForeverEquipmentCatalogDocument>(
            JsonSerializer.Serialize(baseline, Json), Json)!;
        var old = document.Items.ToDictionary(x => x.Id);
        var replacements = 0;
        foreach (var item in manifest.Items.OrderBy(x => x.Id))
        {
            if (old.ContainsKey(item.Id))
            {
                if (!declarations.ContainsKey(item.Id))
                    throw new InvalidOperationException(
                        $"Item {item.Id} already exists; a reviewed override is required.");
                document.Items.RemoveAll(x => x.Id == item.Id);
                replacements++;
            }
            else if (declarations.ContainsKey(item.Id))
                throw new InvalidOperationException($"Override {item.Id} has no baseline record.");

            // Stable key ordering is needed for byte-for-byte reproducible output.
            item.Stats = item.Stats.OrderBy(x => x.Key, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
            document.Items.Add(item);
        }
        if (declarations.Keys.Except(manifest.Items.Select(x => x.Id)).Any())
            throw new InvalidOperationException("Every override must match a reviewed input item.");

        document.Items = document.Items.OrderBy(x => x.Id).ToList();
        document.CatalogVersion = manifest.CatalogVersion;
        document.Scope = $"Reviewed Forever build {manifest.ClientBuild}; " +
            $"{manifest.Items.Count} reviewed rows, {manifest.MetadataOnly.Count} metadata-only candidates " +
            $"excluded from combat; original CP170 entries retained.";
        return new ForeverEquipmentImportResult
        {
            Catalog = document,
            MetadataOnly = manifest.MetadataOnly.OrderBy(x => x.Id).ToList(),
            Report = new ForeverEquipmentImportReport
            {
                CatalogVersion = manifest.CatalogVersion,
                ContentVersion = manifest.ContentVersion,
                ClientBuild = manifest.ClientBuild,
                SourceName = manifest.SourceName,
                SourceUrl = manifest.SourceUrl,
                ImportedAt = manifest.ImportedAt,
                NewVerified = manifest.Items.Count - replacements,
                ReviewedOverrides = replacements,
                MetadataOnly = manifest.MetadataOnly.Count,
                TotalCatalog = document.Items.Count
            }
        };
    }

    public static string CanonicalCatalogJson(ForeverEquipmentCatalogDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var clone = JsonSerializer.Deserialize<ForeverEquipmentCatalogDocument>(
            JsonSerializer.Serialize(document, Json), Json)!;
        clone.Items = clone.Items.OrderBy(x => x.Id).ToList();
        foreach (var item in clone.Items)
            item.Stats = item.Stats.OrderBy(x => x.Key, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        return JsonSerializer.Serialize(clone, Json) + "\n";
    }

    public static IReadOnlyList<ForeverEquipmentCatalogChange> Compare(
        ForeverEquipmentCatalogDocument before, ForeverEquipmentCatalogDocument after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        var a = before.Items.ToDictionary(x => x.Id);
        var b = after.Items.ToDictionary(x => x.Id);
        var result = new List<ForeverEquipmentCatalogChange>();
        foreach (var id in a.Keys.Union(b.Keys).OrderBy(x => x))
        {
            if (!a.TryGetValue(id, out var previous))
                result.Add(new(id, "added", ["item"]));
            else if (!b.TryGetValue(id, out var current))
                result.Add(new(id, "removed", ["item"]));
            else
            {
                var changes = new List<string>();
                if (!JsonEqual(previous.Stats, current.Stats)) changes.Add("stats");
                if (!JsonEqual(previous.Weapon, current.Weapon)) changes.Add("weapon");
                if (!JsonEqual(previous.Effects, current.Effects)) changes.Add("effects");
                if (previous.SourceBuild != current.SourceBuild) changes.Add("sourceBuild");
                if (previous.VerificationStatus != current.VerificationStatus) changes.Add("verificationStatus");
                if (!JsonEqual(previous.AllowedSlots, current.AllowedSlots)) changes.Add("allowedSlots");
                if (!JsonEqual(previous.AllowedClasses, current.AllowedClasses)) changes.Add("allowedClasses");
                if (previous.RequiredLevel != current.RequiredLevel) changes.Add("requiredLevel");
                if (previous.Name != current.Name) changes.Add("name");
                if (changes.Count == 0 &&
                    JsonSerializer.Serialize(previous, Json) != JsonSerializer.Serialize(current, Json))
                    changes.Add("otherMetadata");
                if (changes.Count > 0) result.Add(new(id, "changed", changes));
            }
        }
        return result;
    }

    private static bool JsonEqual<T>(T a, T b) =>
        JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(b, Json);

    private static bool IsHttps(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps && !string.IsNullOrWhiteSpace(uri.Host);

    private static void ValidateItem(ForeverEquipmentItem item, string build)
    {
        if (item.Id <= 0 || string.IsNullOrWhiteSpace(item.Name) ||
            !IsHttps(item.SourceUrl) || item.SourceBuild != build ||
            item.VerificationStatus is not ("client-tooltip" or "client-db2-reviewed") ||
            item.SourceStatus != "forever-client-verified-tooltip" &&
            item.SourceStatus != "forever-client-db2-reviewed" ||
            item.RequiredLevel is < 0 or > 60 ||
            item.AllowedSlots is null || item.AllowedSlots.Count == 0 ||
            item.AllowedSlots.Any(x => !ForeverSharedEquipmentResolver.Slots.Contains(x)) ||
            item.Stats is null || item.Stats.Any(x =>
                !SupportedStats.Contains(x.Key) || x.Value < 0m) ||
            item.Effects is null)
            throw new InvalidOperationException(
                $"Item {item.Id} has incomplete or unsupported verified combat data.");
        if (item.Weapon is { } w &&
            (w.MinimumDamage < 0m || w.MaximumDamage < w.MinimumDamage ||
             w.SpeedSeconds <= 0m || w.Hand is not ("one-hand" or "two-hand" or "ranged") ||
             string.IsNullOrWhiteSpace(w.Type)))
            throw new InvalidOperationException($"Item {item.Id} has invalid weapon data.");
        if (item.AllowedSlots.Any(x => x == "main-hand" || x == "ranged") &&
            item.Weapon is null)
            throw new InvalidOperationException($"Item {item.Id} lacks required weapon data.");
        foreach (var effect in item.Effects)
            if (effect is null || effect.Kind is not
                ("static" or "equip" or "on-use" or "proc" or "set-bonus" or "unsupported") ||
                effect.SpellId is <= 0 ||
                effect.ExecutionStatus is not ("modeled" or "not-modeled"))
                throw new InvalidOperationException($"Item {item.Id} has invalid effect linkage.");
    }
}

public sealed class ForeverEquipmentImportManifest
{
    public string CatalogVersion { get; set; } = "";
    public string ContentVersion { get; set; } = "";
    public string ClientBuild { get; set; } = "";
    public string SourceName { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public DateTimeOffset ImportedAt { get; set; }
    public List<ForeverEquipmentItem> Items { get; set; } = [];
    public List<ForeverEquipmentMetadataCandidate> MetadataOnly { get; set; } = [];
    public List<ForeverEquipmentOverride> Overrides { get; set; } = [];
}
public sealed class ForeverEquipmentMetadataCandidate
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public string VerificationStatus { get; set; } = "metadata-only";
}
public sealed class ForeverEquipmentOverride
{
    public int ItemId { get; set; }
    public string Reason { get; set; } = "";
    public string SourceUrl { get; set; } = "";
}
public sealed class ForeverEquipmentImportResult
{
    public ForeverEquipmentCatalogDocument Catalog { get; set; } = new();
    public List<ForeverEquipmentMetadataCandidate> MetadataOnly { get; set; } = [];
    public ForeverEquipmentImportReport Report { get; set; } = new();
}
public sealed class ForeverEquipmentImportReport
{
    public string CatalogVersion { get; set; } = "";
    public string ContentVersion { get; set; } = "";
    public string ClientBuild { get; set; } = "";
    public string SourceName { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public DateTimeOffset ImportedAt { get; set; }
    public int NewVerified { get; set; }
    public int ReviewedOverrides { get; set; }
    public int MetadataOnly { get; set; }
    public int TotalCatalog { get; set; }
}
public sealed record ForeverEquipmentCatalogChange(
    int ItemId, string Kind, IReadOnlyList<string> ChangedFields);

public sealed class ForeverEquipmentEffectReference
{
    public string Kind { get; set; } = "unsupported";
    public int? SpellId { get; set; }
    public string ExecutionStatus { get; set; } = "not-modeled";
    public string Notes { get; set; } = "";
}
