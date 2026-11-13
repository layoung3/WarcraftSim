using System.Text.Json;
using System.Text.Json.Nodes;
using WarcraftSim.Api.Simulation;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175EquipmentIconProvenance
{
    [Theory]
    [InlineData(11287, "inv_staff_02")]
    [InlineData(14152, "inv_chest_cloth_38")]
    [InlineData(19682, "inv_chest_cloth_07")]
    [InlineData(19683, "inv_pants_cloth_14")]
    [InlineData(19684, "inv_boots_cloth_02")]
    public void ExactHistoricalIdentitiesHaveDisplayOnlyArtwork(int id, string key)
    {
        var item = ForeverEquipmentCatalog.FindItem(id);
        Assert.Equal(key, item.IconKey);
        Assert.Equal(ForeverEquipmentIconCatalog.VerificationStatus, item.IconSourceStatus);
        Assert.Null(item.ClassRestrictionEvidence);
    }

    [Theory]
    [InlineData("elixir-of-ferocity", 250350, "inv_potion_161")]
    [InlineData("elixir-of-greater-intellect", 9179, "inv_potion_10")]
    [InlineData("elixir-of-the-grizzly", 250351, "inv_potion_160")]
    [InlineData("elixir-of-the-owl", 250337, "inv_potion_164")]
    public void ExactNewModifierIdentitiesJoinWithoutGuessing(string key, int id, string icon)
    {
        var entry = ForeverExternalModifierCatalog.Find(key);
        Assert.Equal(id, entry.ItemId);
        Assert.Equal(icon, entry.IconKey);
        Assert.Equal(ForeverExternalModifierIconCatalog.VerificationStatus, entry.IconSourceStatus);
        Assert.Null((entry with { ItemId = id + 1 }).IconKey);
    }

    private static JsonObject Manifest() => new()
    {
        ["schema"] = "warcraftsim-forever-equipment-icons-v1",
        ["sourceRepository"] = "gunba/wow-forever-sim",
        ["sourceCommit"] = ForeverEquipmentIconCatalog.SourceCommit,
        ["sourceSha256"] = "e342a7ce2dbc9ebc3e3beb2b0c23ed379b665f549ff64c97f4adec2ed61eb8de",
        ["sourceStatus"] = ForeverEquipmentIconCatalog.VerificationStatus,
        ["candidates"] = new JsonArray(new JsonObject
        {
            ["itemId"] = 11287, ["itemName"] = "Lesser Magic Wand", ["iconKey"] = "inv_staff_02",
            ["sourceStatus"] = ForeverEquipmentIconCatalog.VerificationStatus
        })
    };

    private static ForeverEquipmentCatalogDocument Catalog() => new()
    {
        Items = [new ForeverEquipmentItem
        {
            Id = 11287, Name = "Lesser Magic Wand", RequiredLevel = 5,
            Stats = new() { ["spell-power"] = 123m }, AllowedClasses = ["mage"],
            VerificationStatus = "unverified-control", DataConfidence = "provisional"
        }]
    };

    [Theory]
    [InlineData("schema")]
    [InlineData("sourceRepository")]
    [InlineData("sourceCommit")]
    [InlineData("sourceSha256")]
    [InlineData("sourceStatus")]
    public void WrongSourceIdentityFailsBeforeMutation(string property)
    {
        var manifest = Manifest();
        manifest[property] = "wrong";
        var catalog = Catalog();
        var before = JsonSerializer.Serialize(catalog);
        Assert.Throws<InvalidOperationException>(() =>
            ForeverEquipmentIconCatalog.ApplyReviewedManifest(catalog, manifest.ToJsonString()));
        Assert.Equal(before, JsonSerializer.Serialize(catalog));
    }

    [Theory]
    [InlineData("unknown-id")]
    [InlineData("name-case")]
    [InlineData("unsafe-key")]
    [InlineData("wrong-status")]
    [InlineData("duplicate")]
    [InlineData("existing-conflict")]
    public void InvalidRowsAndConflictsAreAtomic(string mode)
    {
        var manifest = Manifest();
        var rows = manifest["candidates"]!.AsArray();
        var row = rows[0]!.AsObject();
        var catalog = Catalog();
        switch (mode)
        {
            case "unknown-id": row["itemId"] = 1; break;
            case "name-case": row["itemName"] = "lesser magic wand"; break;
            case "unsafe-key": row["iconKey"] = "../unsafe"; break;
            case "wrong-status": row["sourceStatus"] = "verified"; break;
            case "duplicate": rows.Add(row.DeepClone()); break;
            case "existing-conflict": catalog.Items[0].IconKey = "other_icon"; break;
        }
        var before = JsonSerializer.Serialize(catalog);
        Assert.Throws<InvalidOperationException>(() =>
            ForeverEquipmentIconCatalog.ApplyReviewedManifest(catalog, manifest.ToJsonString()));
        Assert.Equal(before, JsonSerializer.Serialize(catalog));
    }

    [Fact]
    public void ArtNeverPromotesNumbersEligibilityOrAnExistingSource()
    {
        var catalog = Catalog();
        var item = catalog.Items[0];
        var before = JsonSerializer.Serialize(item);
        ForeverEquipmentIconCatalog.ApplyReviewedManifest(catalog, Manifest().ToJsonString());
        Assert.Equal("inv_staff_02", item.IconKey);
        Assert.Equal(ForeverEquipmentIconCatalog.VerificationStatus, item.IconSourceStatus);
        item.IconKey = null;
        item.IconSourceStatus = "";
        Assert.Equal(before, JsonSerializer.Serialize(item));
        item.IconKey = "inv_staff_02";
        item.IconSourceStatus = "original-source";
        ForeverEquipmentIconCatalog.ApplyReviewedManifest(catalog, Manifest().ToJsonString());
        Assert.Equal("original-source", item.IconSourceStatus);
    }
}
