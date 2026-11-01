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
        int[] added={21712,23041,23046,23047,23057,23058,23067};
        foreach(var id in added)
        {
            var item=Assert.Single(items.Where(x=>x.Id==id));
            Assert.Equal("published-client-tooltip",item.VerificationStatus);
            Assert.StartsWith("1.60.1.",item.SourceBuild);
            Assert.Equal("reviewed-published-forever-tooltip",item.SourceStatus);
            Assert.Equal("https://bindonforever.com/item/"+id,item.SourceUrl);
            Assert.NotEmpty(item.Stats);
            Assert.True(item.AllowedSlots.Count>0);
        }
        Assert.Equal(15m,items.Single(x=>x.Id==23057).Stats["spell-power"]);
        Assert.Equal(48m,items.Single(x=>x.Id==23041).Stats["attack-power"]);
        Assert.Equal(30m,items.Single(x=>x.Id==23046).Stats["spell-power"]);
        var hunterRing=items.Single(x=>x.Id==23067);
        Assert.Equal(new[]{"hunter"},hunterRing.AllowedClasses);
        Assert.True(hunterRing.UniqueEquipped);
        Assert.Equal("cryptstalker-armor",hunterRing.SetKey);
        Assert.DoesNotContain(items,x=>x.Id==23040);
    }

    [Fact]
    public void PublishedUnsupportedOnUseEffectsNeverClaimExecution()
    {
        var items=ForeverEquipmentCatalog.GetCatalog().Items;
        foreach(var id in new[]{23041,23046,23047})
        {
            var item=items.Single(x=>x.Id==id);
            Assert.Contains(item.Effects,e=>e.Kind=="unsupported"&&e.ExecutionStatus=="not-modeled");
            Assert.DoesNotContain(item.Effects,e=>e.ExecutionStatus=="modeled");
        }
    }
}
