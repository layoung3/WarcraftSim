using System.Linq;
using WarcraftSim.Api.Simulation;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175F1BReviewedAccessories
{
    [Fact]
    public void ReviewedAccessoryBatchIsInTheActualRuntimeCatalog()
    {
        var items=ForeverEquipmentCatalog.GetCatalog().Items;
        int[] added={21712,23046,23047,23057,23058,23067,21583,21710,23053,288927,288928,288929,288931};
        foreach(var id in added)
        {
            var item=Assert.Single(items,x=>x.Id==id);
            Assert.Equal("published-client-tooltip",item.VerificationStatus);
            Assert.StartsWith("1.60.1.",item.SourceBuild);
            Assert.Equal("reviewed-published-forever-tooltip",item.SourceStatus);
            Assert.Equal("https://bindonforever.com/item/"+id,item.SourceUrl);
            Assert.NotEmpty(item.Stats);
            Assert.True(item.AllowedSlots.Count>0);
        }
        Assert.Equal(15m,items.Single(x=>x.Id==23057).Stats["spell-power"]);
        Assert.Equal(64m,items.Single(x=>x.Id==23041).Stats["attack-power"]);
        Assert.Equal(30m,items.Single(x=>x.Id==23046).Stats["spell-power"]);
        var hunterRing=items.Single(x=>x.Id==23067);
        Assert.Equal(new[]{"hunter"},hunterRing.AllowedClasses);
        Assert.True(hunterRing.UniqueEquipped);
        Assert.Equal("cryptstalker-armor",hunterRing.SetKey);
        Assert.Equal(5m,items.Single(x=>x.Id==288927).Stats["attack-power"]);
        Assert.Equal(0.2m,items.Single(x=>x.Id==288927).Stats["attack-critical-chance-percent"]);
        Assert.Equal(6m,items.Single(x=>x.Id==288931).Stats["spell-power"]);
        Assert.Equal(20m,items.Single(x=>x.Id==288928).Stats["armor"]);
        Assert.Equal(26m,items.Single(x=>x.Id==21710).Stats["agility"]);
        Assert.Equal(8m,items.Single(x=>x.Id==21583).Stats["mana-per-5"]);
        Assert.Equal(2m,items.Single(x=>x.Id==23053).Stats["attack-critical-chance-percent"]);
        Assert.All(new[]{21583,21710,23053,288927,288928,288929,288931},id=>
            Assert.Equal("1.60.1.70707",items.Single(x=>x.Id==id).SourceBuild));
        Assert.Contains(items.Single(x=>x.Id==23041).Effects,e=>e.ExecutionStatus=="modeled");
        Assert.DoesNotContain(items,x=>x.Id==23040);
    }

    [Fact]
    public void PublishedUnsupportedOnUseEffectsNeverClaimExecution()
    {
        var items=ForeverEquipmentCatalog.GetCatalog().Items;
        foreach(var id in new[]{23046,23047})
        {
            var item=items.Single(x=>x.Id==id);
            Assert.Contains(item.Effects,e=>e.Kind=="unsupported"&&e.ExecutionStatus=="not-modeled");
            Assert.DoesNotContain(item.Effects,e=>e.ExecutionStatus=="modeled");
        }
    }
}
