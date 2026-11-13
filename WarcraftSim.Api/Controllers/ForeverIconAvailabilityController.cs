using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WarcraftSim.Api.Controllers;

/// <summary>
/// Lists only locally cached icons with matching pinned-source hash receipts. The browser
/// never probes nonexistent CDN or local icon assets. No runtime network calls,
/// icon guessing or automatic game-data promotion.
/// </summary>
[ApiController]
[Route("api/forever/icons")]
public sealed class ForeverIconAvailabilityController(IWebHostEnvironment environment) : ControllerBase
{
    private const string TalentSource = "1cc5ae1e9abdbf941b7326d2d90421822dce31ba";
    private const string ItemSource = "c969b8378b63ada9aecfd35db14b373a8aaa66ea";

    [HttpGet("available")]
    public IActionResult Available()
    {
        var root=environment.WebRootPath;
        var directory=string.IsNullOrWhiteSpace(root)?null:Path.Combine(root,"forever-icons");
        if(directory is null || !Directory.Exists(directory))
            return Ok(new{cached=0,files=Array.Empty<string>()});
        try
        {
            var names=Directory.EnumerateFiles(directory,"*.jpg",SearchOption.TopDirectoryOnly)
                .Where(HasVerifiedReceipt)
                .Select(path=>Path.GetFileNameWithoutExtension(path))
                .OrderBy(x=>x,StringComparer.Ordinal)
                .ToArray();
            return Ok(new{cached=names.Length,files=names});
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return Ok(new{cached=0,files=Array.Empty<string>()});
        }
    }

    private static bool HasVerifiedReceipt(string path)
    {
        var key = Path.GetFileNameWithoutExtension(path);
        if (!Regex.IsMatch(key, @"^[a-z0-9_]+$", RegexOptions.CultureInvariant))
            return false;
        try
        {
            using var document = JsonDocument.Parse(ReadBounded(path + ".receipt.json", 16_384),
                new JsonDocumentOptions { MaxDepth = 16 });
            var receipt = document.RootElement;
            if (Text(receipt, "schema") != "warcraftsim-acquisition-receipt-v1" ||
                Text(receipt, "validation") != "jpeg-structure-v1" ||
                !receipt.TryGetProperty("provenance", out var provenance) ||
                Text(provenance, "kind") != "icon" || Text(provenance, "iconKey") != key)
                return false;
            var source = Text(provenance, "sourceVersion");
            var expectedUrl = source switch
            {
                TalentSource => $"https://raw.githubusercontent.com/gunba/wow-forever-sim/{TalentSource}/assets/img/wowhead/icons/large/{key}.jpg",
                ItemSource => $"https://raw.githubusercontent.com/Napalmsteak/WoW-Classic-Item-Caches/{ItemSource}/icons/{key}.jpg",
                _ => null
            };
            if (expectedUrl is null || Text(provenance, "url") != expectedUrl)
                return false;
            var data = ReadBounded(path, source == ItemSource ? 100_000 : 160_000);
            // The acquisition writer validated JPEG structure before issuing this
            // immutable hash receipt. Never bless legacy files from a filename.
            return data.Length >= 100 && data[0] == 0xff && data[1] == 0xd8 &&
                data[^2] == 0xff && data[^1] == 0xd9 &&
                receipt.TryGetProperty("bytes", out var bytes) && bytes.TryGetInt32(out var length) &&
                length == data.Length &&
                Text(receipt, "sha256") == Convert.ToHexStringLower(SHA256.HashData(data));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                                           JsonException or InvalidOperationException or FormatException)
        {
            // The existing UI renders its neutral fallback for unavailable keys.
            // Keep the original bytes/receipt for review; do not repair at runtime.
            return false;
        }
    }

    private static string? Text(JsonElement value, string field) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(field, out var property) &&
        property.ValueKind == JsonValueKind.String ? property.GetString() : null;

    private static byte[] ReadBounded(string path, int limit)
    {
        using var stream = System.IO.File.OpenRead(path);
        var bytes = new byte[limit + 1];
        var count = stream.ReadAtLeast(bytes, bytes.Length, throwOnEndOfStream: false);
        if (count == 0 || count > limit)
            throw new IOException("Invalid cached icon or receipt size.");
        Array.Resize(ref bytes, count);
        return bytes;
    }
}
