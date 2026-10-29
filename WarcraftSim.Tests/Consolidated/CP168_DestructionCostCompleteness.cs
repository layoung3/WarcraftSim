using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Data.Forever.Warlock;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp168DestructionCostCompleteness
{
    [Theory]
    [InlineData("shadowburn",365)]
    [InlineData("rain-of-fire",1185)]
    [InlineData("hellfire",1300)]
    [InlineData("soul-fire",335)]
    [InlineData("conflagrate",255)]
    [InlineData("searing-pain",168)]
    public void CataclysmReducesAllEligibleDestructionAbilityManaCosts(
        string key,double baseline)
    {
        var t=ForeverWarlockTalentBuild.Default("destruction");
        t.Ranks["cataclysm"]=0;
        var original=Assert.Single(ForeverWarlockDpsSpellbook.Create(
            "destruction",t,new ForeverWarlockTuning(),0m,0m),a=>a.Key==key);
        Assert.Equal((decimal)baseline,original.ResourceCosts.Single(c=>
            c.ResourceKey=="mana").Amount);
        t.Ranks["cataclysm"]=3;
        var improved=Assert.Single(ForeverWarlockDpsSpellbook.Create(
            "destruction",t,new ForeverWarlockTuning(),0m,0m),a=>a.Key==key);
        Assert.Equal((decimal)baseline*.9m,improved.ResourceCosts.Single(c=>
            c.ResourceKey=="mana").Amount);
    }

    [Fact]
    public void CataclysmDoesNotReduceAfflictionBanesOrCurses()
    {
        var t=ForeverWarlockTalentBuild.Default("destruction");
        var spells=ForeverWarlockDpsSpellbook.Create("destruction",t,
            new ForeverWarlockTuning(),0m,0m);
        Assert.Equal(215m,spells.Single(a=>a.Key=="bane-of-agony")
            .ResourceCosts.Single().Amount);
        Assert.Equal(200m,spells.Single(a=>a.Key=="curse-of-the-elements")
            .ResourceCosts.Single().Amount);
    }
}
