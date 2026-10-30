using System;
using System.Linq;
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp171ReviewedEquipmentSnapshot
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private static string Manifest(string items, string metadata = "[]", string overrides = "[]") =>
        $$"""
        {
          "catalogVersion": "forever-1.60.1.70532-cp171-review-1",
          "contentVersion": "cp171-review-1",
          "clientBuild": "1.60.1.70532",
          "sourceName": "reviewed-forever-client-tooltips",
          "sourceUrl": "https://theforeverera.com/en/items/",
          "importedAt": "2026-10-30T13:56:00Z",
          "items": {{items}},
          "metadataOnly": {{metadata}},
          "overrides": {{overrides}}
        }
        """;

    private const string ReviewedItem = """
        [{
          "id": 19102, "name": "Crackling Staff",
          "allowedSlots": ["main-hand"], "requiredLevel": 60,
          "allowedClasses": ["mage","warlock","priest","druid"],
          "stats": {"intellect":25, "spell-power":25},
          "weapon": {"type":"staff","hand":"two-hand",
                     "minimumDamage":138,"maximumDamage":207,"speedSeconds":3.1},
          "sourceUrl":"https://theforeverera.com/en/items/19102/",
          "sourceStatus":"forever-client-verified-tooltip",
          "sourceBuild":"1.60.1.70532","verificationStatus":"client-tooltip",
          "effects": []
        }]
        """;
    private const string Override = """
        [{"itemId":19102,"reason":"Reviewed existing item against the pinned Forever client tooltip.",
          "sourceUrl":"https://theforeverera.com/en/items/19102/"}]
        """;

    [Fact]
    public void PinnedImportIsDeterministicAndDoesNotMutateBaseline()
    {
        var baseline = ForeverEquipmentCatalog.GetCatalog();
        var originalCount = baseline.Items.Count;
        var payload = Manifest(ReviewedItem, overrides: Override);
        var first = ForeverEquipmentSnapshotPipeline.Import(payload, baseline);
        var second = ForeverEquipmentSnapshotPipeline.Import(payload, baseline);
        Assert.Equal(ForeverEquipmentSnapshotPipeline.CanonicalCatalogJson(first.Catalog),
            ForeverEquipmentSnapshotPipeline.CanonicalCatalogJson(second.Catalog));
        Assert.Equal(1, first.Report.ReviewedOverrides);
        Assert.Equal(0, first.Report.NewVerified);
        Assert.Equal(originalCount, first.Catalog.Items.Count);
        Assert.Equal(originalCount, baseline.Items.Count);
        Assert.Equal(25m, first.Catalog.Items.Single(x => x.Id == 19102).Stats["spell-power"]);
    }

    [Fact]
    public void MetadataOnlyIsKeptOutOfCombatCatalog()
    {
        const string metadata = """
          [{"id":299999,"name":"Unverified Item","sourceUrl":"https://api.ahledger.com/v1/items/299999?game=forever",
            "verificationStatus":"metadata-only"}]
          """;
        var result = ForeverEquipmentSnapshotPipeline.Import(
            Manifest(ReviewedItem, metadata, Override), ForeverEquipmentCatalog.GetCatalog());
        Assert.Equal(1, result.Report.MetadataOnly);
        Assert.DoesNotContain(result.Catalog.Items, x => x.Id == 299999);
        Assert.Single(result.MetadataOnly);
    }

    [Fact]
    public void DuplicateAndUnapprovedReplacementFailClosed()
    {
        var baseDoc = ForeverEquipmentCatalog.GetCatalog();
        Assert.Throws<InvalidOperationException>(() =>
            ForeverEquipmentSnapshotPipeline.Import(Manifest(ReviewedItem), baseDoc));
        var duplicate = ReviewedItem.Replace("}]", "}, " + ReviewedItem[1..^1]);
        Assert.ThrowsAny<Exception>(() =>
            ForeverEquipmentSnapshotPipeline.Import(Manifest(duplicate, overrides: Override), baseDoc));
    }

    [Fact]
    public void MissingWeaponAndUnsupportedStatsAreRejected()
    {
        var baseDoc = ForeverEquipmentCatalog.GetCatalog();
        Assert.Throws<InvalidOperationException>(() =>
            ForeverEquipmentSnapshotPipeline.Import(
                Manifest(ReviewedItem.Replace("\"spell-power\":25", "\"invented-power\":25"), overrides:Override),
                baseDoc));
        Assert.Throws<InvalidOperationException>(() =>
            ForeverEquipmentSnapshotPipeline.Import(
                Manifest(ReviewedItem.Replace("\"speedSeconds\":3.1", "\"speedSeconds\":0"), overrides:Override),
                baseDoc));
    }

    [Fact]
    public void DeterministicDiffIdentifiesReviewedStatChanges()
    {
        var baseline = ForeverEquipmentCatalog.GetCatalog();
        var reviewed = ForeverEquipmentSnapshotPipeline.Import(
            Manifest(ReviewedItem, overrides:Override),baseline);
        Assert.Empty(ForeverEquipmentSnapshotPipeline.Compare(reviewed.Catalog,reviewed.Catalog));
        var changed = ForeverEquipmentSnapshotPipeline.Compare(baseline,reviewed.Catalog);
        Assert.Contains(changed, x => x.ItemId==19102 &&
            x.Kind=="changed" && x.ChangedFields.Contains("otherMetadata"));
    }
}
