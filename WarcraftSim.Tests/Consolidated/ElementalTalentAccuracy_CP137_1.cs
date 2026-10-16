using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Data.Forever.Shaman;
using Xunit;

namespace WarcraftSim.Tests;

public sealed class ElementalTalentAccuracyCP1371Tests
{
    [Fact]
    public void ShamanisticFocusDiscountsFlameShockButNotLightningBolt()
    {
        var hybrid=ElementalPreRaidProfileCatalog.Talents("cleave-hybrid");
        var deep=ElementalPreRaidProfileCatalog.Talents("deep-elemental");
        var v=new ForeverElementalShamanTuning();
        var hybridSpellbook=ForeverElementalShamanSpellbook.Create(hybrid,v,400m);
        var deepSpellbook=ForeverElementalShamanSpellbook.Create(deep,v,400m);
        var focusShock=hybridSpellbook.Single(x=>x.Key=="flame-shock").ResourceCosts.Single().Amount;
        var normalShock=deepSpellbook.Single(x=>x.Key=="flame-shock").ResourceCosts.Single().Amount;
        Assert.Equal(normalShock*.55m,focusShock);
        Assert.Equal(198m,focusShock); // 400 base * 0.90 Convection * 0.55 Focus
        Assert.Equal(deepSpellbook.Single(x=>x.Key=="lightning-bolt").ResourceCosts.Single().Amount,
            hybridSpellbook.Single(x=>x.Key=="lightning-bolt").ResourceCosts.Single().Amount);
    }

    [Theory]
    [InlineData("throughput", "raid-sustain", 4320.8)]
    [InlineData("throughput", "deep-elemental", 4508.0)]
    [InlineData("throughput", "cleave-hybrid", 4508.0)]
    [InlineData("sustain", "raid-sustain", 4991.6)]
    [InlineData("sustain", "deep-elemental", 5217.5)]
    [InlineData("sustain", "cleave-hybrid", 5217.5)]
    public void AncestralKnowledgeIncreasesMaximumManaWithTalentAndEquipment(
        string gear,string build,double expectedMana)
    {
        var request=ElementalPreRaidProfileCatalog.Setup(gear,build);
        var result=new ElementalShamanSimRunner().Run(request);
        Assert.Equal((decimal)expectedMana,result.MaximumMana);
    }

    [Fact]
    public void AncestralKnowledgePreservesStartingManaPercentage()
    {
        var request=ElementalPreRaidProfileCatalog.Setup("throughput","deep-elemental");
        request.StartingManaPercent=40m;
        var result=new ElementalShamanSimRunner().Run(request);
        Assert.Equal(4508m,result.MaximumMana);
        Assert.Equal(4508m*.4m,result.Run.Summary.ActorSummaries["shaman"].StartingResources["mana"]);
    }

    [Fact]
    public void TalentsAffectResourceBudgetWithoutMutatingInputProfile()
    {
        var request=ElementalPreRaidProfileCatalog.Setup("throughput","deep-elemental");
        var originalIntellect=request.Character.BaseStats.Get("intellect");
        new ElementalShamanSimRunner().Run(request);
        Assert.Equal(originalIntellect,request.Character.BaseStats.Get("intellect"));
        Assert.Equal(208m,originalIntellect);
    }
}
