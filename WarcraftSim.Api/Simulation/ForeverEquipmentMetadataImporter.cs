using System.Net.Http;
using System.Text.Json;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Read-only provider adapter for AHledger's documented public API
/// (https://api.ahledger.com/v1/items/{id}?game=forever).
/// That endpoint currently documents metadata, NOT combat stat vectors.
/// Its output is retained as a review candidate and can never be silently
/// promoted into a damage-bearing catalog entry.
/// </summary>
public interface IForeverEquipmentMetadataSource
{
    string ProviderKey { get; }
    Task<ForeverRawEquipmentRecord> ReadAsync(int itemId,CancellationToken token=default);
}

public sealed class AhledgerForeverItemMetadataSource(HttpClient client) :
    IForeverEquipmentMetadataSource
{
    public string ProviderKey=>"ahledger-forever-items-v1";

    public async Task<ForeverRawEquipmentRecord> ReadAsync(
        int itemId,CancellationToken token=default)
    {
        if(itemId<=0)throw new ArgumentOutOfRangeException(nameof(itemId));
        var endpoint=$"https://api.ahledger.com/v1/items/{itemId}?game=forever";
        using var response=await client.GetAsync(endpoint,token);
        response.EnsureSuccessStatusCode();
        var json=await response.Content.ReadAsStringAsync(token);
        return ForeverEquipmentMetadataImporter.ParseAhledger(
            json,itemId,endpoint);
    }
}

public sealed class ForeverRawEquipmentRecord
{
    public int Id { get; set; }
    public string Name { get; set; }="";
    public string Quality { get; set; }="";
    public string Provider { get; set; }="";
    public string SourceUrl { get; set; }="";
    public string SourceBuild { get; set; }="";
    public string VerificationStatus { get; set; }="metadata-only";
    public DateTimeOffset ImportedAt { get; set; }
    // Raw records are not normalized catalog items and never supply DPS.
    public JsonElement Raw { get; set; }
}

public static class ForeverEquipmentMetadataImporter
{
    public static ForeverRawEquipmentRecord ParseAhledger(
        string json,int requestedId,string sourceUrl,string sourceBuild="")
    {
        if(requestedId<=0 || string.IsNullOrWhiteSpace(json) ||
           !Uri.TryCreate(sourceUrl,UriKind.Absolute,out var uri) ||
           uri.Scheme!=Uri.UriSchemeHttps ||
           !string.Equals(uri.Host,"api.ahledger.com",StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Valid AHledger HTTPS source and item ID required.");
        using var document=JsonDocument.Parse(json);
        var root=document.RootElement;
        var item=root.TryGetProperty("item",out var nested) &&
            nested.ValueKind==JsonValueKind.Object?nested:root;
        if(item.ValueKind!=JsonValueKind.Object ||
           !item.TryGetProperty("id",out var id) ||
           !id.TryGetInt32(out var actualId) || actualId!=requestedId ||
           !item.TryGetProperty("name",out var name) ||
           string.IsNullOrWhiteSpace(name.GetString()))
            throw new InvalidOperationException(
                "Item metadata lacks a matching stable game ID or name.");
        var quality=item.TryGetProperty("quality",out var q)&&
            q.ValueKind==JsonValueKind.String?q.GetString()??"":"";
        return new ForeverRawEquipmentRecord{
            Id=actualId,Name=name.GetString()!,Quality=quality,
            Provider="ahledger-forever-items-v1",
            SourceUrl=sourceUrl,SourceBuild=sourceBuild,
            VerificationStatus="metadata-only",
            ImportedAt=DateTimeOffset.UtcNow,
            Raw=root.Clone()
        };
    }

    /// <summary>Intentionally fail closed. The metadata API does not contain
    /// verified equip-stat/weapon effects. Promotion requires a separate
    /// curated client/source snapshot and review, never fabricated defaults.</summary>
    public static ForeverEquipmentItem PromoteToSimulationItem(ForeverRawEquipmentRecord source) =>
        throw new InvalidOperationException(
            $"Item {source.Id} ({source.Name}) is metadata-only. A verified " +
            "Forever client stat/slot/effect snapshot is required before simulation.");
}
