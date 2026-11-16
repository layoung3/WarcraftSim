using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using System.Security.Cryptography;
using System.Text.Json;
using WarcraftSim.Api.Controllers;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175IconCacheReceipts : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "warcraftsim-icon-tests-" + Guid.NewGuid().ToString("N"));
    private const string TalentSource = "1cc5ae1e9abdbf941b7326d2d90421822dce31ba";
    private const string ItemSource = "c969b8378b63ada9aecfd35db14b373a8aaa66ea";

    public Cp175IconCacheReceipts() => Directory.CreateDirectory(Path.Combine(root, "forever-icons"));

    [Fact]
    public void MissingCacheAndUnreceiptedLegacyFilesUseNeutralFallback()
    {
        Assert.Empty(Available());
        Write("inv_legacy", receipt: false);
        Assert.Empty(Available());
        Assert.True(File.Exists(ImagePath("inv_legacy")));
    }

    [Theory]
    [InlineData(TalentSource)]
    [InlineData(ItemSource)]
    public void PinnedTalentModifierAndItemWritersAreAccepted(string source)
    {
        Write("inv_test", source: source);
        Assert.Equal(new[] { "inv_test" }, Available());
    }

    [Fact]
    public void CorruptionIsNotAdvertisedAndOriginalReceiptIsPreserved()
    {
        Write("inv_test");
        var before = File.ReadAllBytes(ImagePath("inv_test") + ".receipt.json");
        File.WriteAllBytes(ImagePath("inv_test"), [0xff, 0xd8, 0xff, 0xd9]);
        Assert.Empty(Available());
        Assert.Equal(before, File.ReadAllBytes(ImagePath("inv_test") + ".receipt.json"));
    }

    [Theory]
    [InlineData("unknown-version", null, "inv_test")]
    [InlineData(TalentSource, "https://unapproved.example/icon.jpg", "inv_test")]
    [InlineData(TalentSource, null, "other_key")]
    public void SourceVersionUrlAndIconIdentityMustMatch(string source, string? url, string receiptKey)
    {
        Write("inv_test", source: source, url: url, receiptKey: receiptKey);
        Assert.Empty(Available());
    }

    [Fact]
    public void InvalidAndOversizedReceiptsDoNotBreakAvailability()
    {
        Write("inv_good");
        Write("inv_bad");
        File.WriteAllText(ImagePath("inv_bad") + ".receipt.json", "not-json");
        Write("inv_large");
        File.WriteAllText(ImagePath("inv_large") + ".receipt.json", new string('x', 16_385));
        Assert.Equal(new[] { "inv_good" }, Available());
    }

    [Fact]
    public void OversizedImageAndMissingValidationAreNotAdvertised()
    {
        Write("inv_large");
        File.WriteAllBytes(ImagePath("inv_large"), new byte[160_001]);
        Write("inv_unvalidated", validation: "unknown");
        Assert.Empty(Available());
    }

    private string[] Available()
    {
        var controller = new ForeverIconAvailabilityController(new TestEnvironment { WebRootPath = root });
        var result = Assert.IsType<OkObjectResult>(controller.Available());
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(result.Value));
        return document.RootElement.GetProperty("files").EnumerateArray().Select(x => x.GetString()!).ToArray();
    }

    private string ImagePath(string key) => Path.Combine(root, "forever-icons", key + ".jpg");

    private void Write(string key, bool receipt = true, string source = TalentSource,
        string? url = null, string? receiptKey = null, string validation = "jpeg-structure-v1")
    {
        // The controller consumes the writer's hash/structure attestation. No
        // real artwork or HTTP access is used by these local receipt contracts.
        var data = new byte[120];
        data[0] = data[^2] = 0xff;
        data[1] = 0xd8;
        data[^1] = 0xd9;
        File.WriteAllBytes(ImagePath(key), data);
        if (!receipt) return;
        url ??= source == ItemSource
            ? $"https://raw.githubusercontent.com/Napalmsteak/WoW-Classic-Item-Caches/{source}/icons/{key}.jpg"
            : $"https://raw.githubusercontent.com/gunba/wow-forever-sim/{source}/assets/img/wowhead/icons/large/{key}.jpg";
        File.WriteAllText(ImagePath(key) + ".receipt.json", JsonSerializer.Serialize(new
        {
            schema = "warcraftsim-acquisition-receipt-v1", validation,
            bytes = data.Length, sha256 = Convert.ToHexStringLower(SHA256.HashData(data)),
            provenance = new { kind = "icon", iconKey = receiptKey ?? key, sourceVersion = source, url }
        }));
    }

    public void Dispose() => Directory.Delete(root, recursive: true);

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "WarcraftSim.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "";
        public string EnvironmentName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
