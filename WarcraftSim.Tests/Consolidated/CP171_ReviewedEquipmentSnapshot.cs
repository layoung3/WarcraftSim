using System.Text.Json;
using System.Text.Json.Nodes;
using WarcraftSim.Api.Simulation;

namespace WarcraftSim.Tests.Consolidated;

// Deliberately synthetic, test-only receipts. Never install these rows or their
// hashes in the shared catalog or use them as evidence about real game items.
internal static class SyntheticEquipmentRestrictionFixture
{
    public const int Id=900001;
    public const string Build="1.60.1.70707";
    public static readonly string SparseDigest=new('a',64);
    public static readonly string ItemDigest=new('b',64);
    public static readonly string ValuesDigest=new('c',64);
    public static ForeverEquipmentClassRestrictionEvidence Evidence(int id=Id,long mask=1024L,
        List<string>? classes=null)=>new() {
        ItemId=id,SourceKind="client-item-sparse",RawMask=mask,Status="restricted",
        DataConfidence="provisional",SourceAllowedClasses=classes??["druid"],
        SourceBuild=Build,SourceUrl="https://example.test/synthetic/ItemSparse.csv",SourceSha256=SparseDigest
    };
    public static ForeverEquipmentItem Item(bool weapon=false)=>new() {
        Id=Id,Name="Synthetic restriction fixture (TEST ONLY)",RequiredLevel=60,
        AllowedSlots=weapon?["main-hand"]:["chest"],AllowedClasses=["druid"],
        ItemClass=weapon?"weapon":"armor",ItemSubclass=weapon?"staff":"leather",
        Stats=new() {["intellect"]=25m,["spell-power"]=25m},
        Weapon=weapon?new() {Type="staff",Hand="two-hand",MinimumDamage=138m,MaximumDamage=207m,SpeedSeconds=3.1m}:null,
        SourceUrl="https://example.test/synthetic/item/900001",SourceBuild=Build,
        SourceStatus="forever-client-db2-reviewed",VerificationStatus="client-db2-reviewed",
        ClassRestrictionEvidence=Evidence()
    };
    public static ForeverEquipmentReviewApproval Approval()=>new() {
        ClientBuild=Build,Status="reviewed",Reviewer="synthetic-test-only",
        SourceUrl="https://example.test/synthetic/review",ResolvedValuesSha256=ValuesDigest,
        SourceTableSha256=new() {["Item"]=ItemDigest,["ItemSparse"]=SparseDigest}
    };
    public static ForeverEquipmentImportManifest Manifest(params ForeverEquipmentItem[] items)=>new() {
        CatalogVersion="synthetic-restriction-import-v2",ContentVersion="synthetic-test-only",
        ClientBuild=Build,SourceName="synthetic-test-only",SourceUrl="https://example.test/synthetic/source",
        ImportedAt=DateTimeOffset.Parse("2026-11-09T00:00:00Z"),Items=items.ToList(),
        TableSha256=new() {["Item"]=ItemDigest,["ItemSparse"]=SparseDigest},
        ReviewedValuesSha256=ValuesDigest,ReviewApproval=Approval()
    };
    public static string Json(ForeverEquipmentImportManifest manifest)=>
        JsonSerializer.Serialize(manifest,new JsonSerializerOptions(JsonSerializerDefaults.Web));
    public static ForeverEquipmentCatalogDocument Baseline()
    {
        var item=Item(weapon:true);item.ClassRestrictionEvidence=null;
        return new() {CatalogVersion="synthetic-legacy-baseline",Items=[item]};
    }
}

public sealed class Cp171ReviewedEquipmentSnapshot
{
    private static ForeverEquipmentImportManifest Replacement()
    {
        var item=SyntheticEquipmentRestrictionFixture.Item(weapon:true);
        var manifest=SyntheticEquipmentRestrictionFixture.Manifest(item);
        manifest.Overrides=[new() {ItemId=item.Id,Reason="Synthetic test-only replacement",
            SourceUrl="https://example.test/synthetic/review"}];
        return manifest;
    }
    private static ForeverEquipmentImportResult Import(ForeverEquipmentImportManifest manifest,
        ForeverEquipmentCatalogDocument? baseline=null)=>ForeverEquipmentSnapshotPipeline.Import(
            SyntheticEquipmentRestrictionFixture.Json(manifest),baseline??SyntheticEquipmentRestrictionFixture.Baseline());

    [Fact]
    public void PinnedImportIsDeterministicAndDoesNotMutateBaseline()
    {
        var baseline=SyntheticEquipmentRestrictionFixture.Baseline();
        var original=ForeverEquipmentSnapshotPipeline.CanonicalCatalogJson(baseline);
        var first=Import(Replacement(),baseline);var second=Import(Replacement(),baseline);
        Assert.Equal(ForeverEquipmentSnapshotPipeline.CanonicalCatalogJson(first.Catalog),
            ForeverEquipmentSnapshotPipeline.CanonicalCatalogJson(second.Catalog));
        Assert.Equal(1,first.Report.ReviewedOverrides);Assert.Equal(0,first.Report.NewVerified);
        Assert.Single(first.Catalog.Items);Assert.Equal(original,ForeverEquipmentSnapshotPipeline.CanonicalCatalogJson(baseline));
        Assert.Equal(25m,first.Catalog.Items.Single().Stats["spell-power"]);
    }

    [Fact]
    public void MetadataOnlyIsKeptOutOfCombatCatalog()
    {
        var manifest=Replacement();
        manifest.MetadataOnly=[new() {Id=900002,Name="Synthetic unknown restriction (TEST ONLY)",
            SourceUrl="https://example.test/synthetic/unknown",ClassRestrictionEvidence=new() {
                ItemId=900002,RawMask=-1L,Status="unknown",DataConfidence="unknown"}}];
        var result=Import(manifest);
        Assert.Equal(1,result.Report.MetadataOnly);Assert.DoesNotContain(result.Catalog.Items,x=>x.Id==900002);
        var pending=Assert.Single(result.MetadataOnly).ClassRestrictionEvidence!;
        Assert.Equal(-1L,pending.RawMask);Assert.Equal("unknown",pending.Status);Assert.Equal("unknown",pending.DataConfidence);
    }

    [Fact]
    public void DuplicateAndUnapprovedReplacementFailClosed()
    {
        var manifest=Replacement();manifest.Overrides.Clear();
        Assert.Throws<InvalidOperationException>(()=>Import(manifest));
        manifest=Replacement();manifest.Items.Add(SyntheticEquipmentRestrictionFixture.Item());
        Assert.Throws<InvalidOperationException>(()=>Import(manifest));
    }

    [Fact]
    public void MissingWeaponAndUnsupportedStatsAreRejected()
    {
        var manifest=Replacement();manifest.Items[0].Stats["invented-power"]=25m;
        Assert.Throws<InvalidOperationException>(()=>Import(manifest));
        manifest=Replacement();manifest.Items[0].Weapon!.SpeedSeconds=0m;
        Assert.Throws<InvalidOperationException>(()=>Import(manifest));
        manifest=Replacement();manifest.Items[0].Weapon=null;
        Assert.Throws<InvalidOperationException>(()=>Import(manifest));
    }

    [Fact]
    public void DeterministicDiffIdentifiesReviewedStatChanges()
    {
        var baseline=SyntheticEquipmentRestrictionFixture.Baseline();var reviewed=Import(Replacement(),baseline);
        Assert.Empty(ForeverEquipmentSnapshotPipeline.Compare(reviewed.Catalog,reviewed.Catalog));
        Assert.Contains(ForeverEquipmentSnapshotPipeline.Compare(baseline,reviewed.Catalog),x=>
            x.ItemId==SyntheticEquipmentRestrictionFixture.Id&&x.Kind=="changed"&&x.ChangedFields.Contains("classRestrictionEvidence"));
    }

    [Theory]
    [InlineData("missing")][InlineData("unknown")][InlineData("source-mask")]
    [InlineData("class-list")][InlineData("proficiency")][InlineData("sparse-pin")][InlineData("item-pin")]
    [InlineData("approval")][InlineData("values")][InlineData("mixed-build")][InlineData("tooltip-status")]
    [InlineData("row-id")][InlineData("row-build")][InlineData("confidence")]
    public void NewImportsRequireConsistentReviewedRestrictionEvidence(string conflict)
    {
        var manifest=Replacement();var item=manifest.Items[0];var e=item.ClassRestrictionEvidence!;
        switch(conflict)
        {
            case "missing":item.ClassRestrictionEvidence=null;break;
            case "unknown":e.Status="unknown";e.DataConfidence="unknown";break;
            case "source-mask":e.RawMask=0L;break;
            case "class-list":e.SourceAllowedClasses=["mage"];break;
            case "proficiency":item.ItemClass="armor";item.ItemSubclass="plate";item.Weapon=null;item.AllowedSlots=["chest"];break;
            case "sparse-pin":manifest.TableSha256["ItemSparse"]=new('d',64);break;
            case "item-pin":manifest.ReviewApproval!.SourceTableSha256["Item"]=new('d',64);break;
            case "approval":manifest.ReviewApproval=null;break;
            case "values":manifest.ReviewedValuesSha256=new('d',64);break;
            case "mixed-build":manifest.ClientBuild=item.SourceBuild=e.SourceBuild=manifest.ReviewApproval!.ClientBuild="mixed-reviewed";break;
            case "tooltip-status":item.SourceStatus="forever-client-verified-tooltip";item.VerificationStatus="client-tooltip";break;
            case "row-id":e.ItemId++;break;
            case "row-build":e.SourceBuild="1.60.1.70532";break;
            case "confidence":e.DataConfidence="verified";break;
        }
        Assert.Throws<InvalidOperationException>(()=>Import(manifest));
    }

    [Fact]
    public void UnknownReceiptCanBeReplacedByValidEvidenceWithoutChangingEmbeddedLegacy()
    {
        var manifest=Replacement();manifest.Items[0].ClassRestrictionEvidence=null;
        Assert.Throws<InvalidOperationException>(()=>Import(manifest));
        manifest.Items[0].ClassRestrictionEvidence=SyntheticEquipmentRestrictionFixture.Evidence();
        Assert.Equal("restricted",Import(manifest).Catalog.Items.Single().ClassRestrictionEvidence!.Status);
        var legacy=ForeverEquipmentCatalog.FindItem(19102);
        Assert.Null(legacy.ClassRestrictionEvidence);
        var resolved=ForeverSharedEquipmentResolver.Resolve(new() {Items=[new() {ItemId=19102,SlotKey="main-hand"}]},"druid");
        Assert.Equal(25m,resolved.RawStats.Get("spell-power")); // Legacy compatibility, not new prepared eligibility.
    }

    [Fact]
    public void ManifestCannotSelectEmbeddedCompatibilityBypass()
    {
        var manifest=Replacement();manifest.Items[0].ClassRestrictionEvidence=null;
        var json=JsonNode.Parse(SyntheticEquipmentRestrictionFixture.Json(manifest))!;
        json["allowEmbeddedLegacy"]=true;json["isEmbedded"]=true;
        Assert.Throws<InvalidOperationException>(()=>ForeverEquipmentSnapshotPipeline.Import(
            json.ToJsonString(),SyntheticEquipmentRestrictionFixture.Baseline()));
        Assert.Null(typeof(ForeverEquipmentSnapshotPipeline).GetMethod("ImportEmbeddedLegacySnapshot"));
    }
}
