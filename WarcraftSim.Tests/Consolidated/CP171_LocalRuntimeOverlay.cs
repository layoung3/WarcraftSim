using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp171LocalApprovedManifestOverlay
{
    private static string Manifest()
    {
        var manifest = new ForeverEquipmentImportManifest {
            CatalogVersion = "synthetic-forever-cp171-70707-overlay-test",
            ContentVersion = "synthetic-local-approval-test",
            ClientBuild = "1.60.1.70707",
            SourceName = "synthetic-reviewed-client-join-test",
            SourceUrl = "https://example.test/source",
            ImportedAt = DateTimeOffset.Parse("2026-10-30T16:00:00Z"),
            Items = [
                new ForeverEquipmentItem {
                    Id = 300001, Name = "Synthetic Reviewed Ring (TEST ONLY)",
                    RequiredLevel = 60,AllowedSlots = ["finger1","finger2"],
                    AllowedClasses = ["mage"], Quality = "rare",
                    ItemClass = "armor",ItemSubclass = "miscellaneous",
                    SourceStatus = "forever-client-db2-reviewed",
                    VerificationStatus = "client-db2-reviewed",
                    SourceBuild = "1.60.1.70707",
                    SourceUrl = "https://example.test/reviewed/item/300001",
                    Stats = new Dictionary<string,decimal> { ["spell-power"] = 17m }
                }
            ]
        };
        return JsonSerializer.Serialize(manifest,new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    [Fact]
    public void StartupLocalOverlayRequiresExactHashAndCanExpandCurrentClassNeutralCatalog()
    {
        var path = Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".json");
        try
        {
            var raw=Encoding.UTF8.GetBytes(Manifest());
            File.WriteAllBytes(path,raw);
            var sha=Convert.ToHexString(SHA256.HashData(raw));
            var baseline=ForeverEquipmentCatalog.GetCatalog();
            var expanded=ForeverEquipmentCatalog.LoadApprovedLocalManifest(baseline,path,sha);
            Assert.Equal(baseline.Items.Count+1,expanded.Items.Count);
            Assert.Equal("synthetic-forever-cp171-70707-overlay-test",expanded.CatalogVersion);
            Assert.Equal(17m,expanded.Items.Find(x=>x.Id==300001)!.Stats["spell-power"]);
            Assert.DoesNotContain(baseline.Items,x=>x.Id==300001);
            Assert.Throws<InvalidOperationException>(()=>
                ForeverEquipmentCatalog.LoadApprovedLocalManifest(baseline,path,new string('0',64)));
            Assert.Throws<InvalidOperationException>(()=>
                ForeverEquipmentCatalog.LoadApprovedLocalManifest(baseline,"relative.json",sha));
            Assert.Throws<InvalidOperationException>(()=>
                ForeverEquipmentCatalog.LoadApprovedLocalManifest(baseline,path,""));
        }
        finally { if(File.Exists(path)) File.Delete(path); }
    }
}