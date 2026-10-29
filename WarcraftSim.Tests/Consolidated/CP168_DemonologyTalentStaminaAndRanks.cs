using System;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Data.Forever.Warlock;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp168DemonologyTalentStaminaAndRanks
{
    private static WarlockSimRequest Request(int embraceRank)
    {
        var r=WarlockSimRequest.CreatePreset("demonology");
        r.Talents.Ranks["demonic-embrace"]=embraceRank;
        r.Character.BaseStats.Values["stamina"]=100m;
        r.PrePullSacrificedDemon=null;
        r.EnablePet=false;
        r.UseLifeTap=false;
        r.DurationSeconds=4m;
        r.CustomPriority=["shadow-bolt"];
        return r;
    }

    [Theory]
    [InlineData(0,0)]
    [InlineData(1,30)]
    [InlineData(3,90)]
    [InlineData(5,150)]
    public void DemonicEmbraceRanksIncreaseActualMaximumHealthWithoutSpiritPenalty(
        int rank,int expectedHealth)
    {
        var request=Request(rank);
        var run=new WarlockSimRunner().Run(request);
        Assert.Equal(request.WarlockMaximumHealth+expectedHealth,run.EndingHealth);
    }

    [Fact]
    public void NewDemonologyTalentRankCapsAndTierGatesAreEnforced()
    {
        var t=ForeverWarlockTalentBuild.Default("demonology");
        t.Ranks["demonic-aegis"]=2;
        t.Ranks["demonic-embrace"]=5;
        t.Validate(60,"demonology");
        t.Ranks["demonic-aegis"]=3;
        Assert.Throws<ArgumentException>(()=>t.Validate(60,"demonology"));
        t.Ranks["demonic-aegis"]=2;
        t.Ranks["demonic-embrace"]=6;
        Assert.Throws<ArgumentException>(()=>t.Validate(60,"demonology"));
        t.Ranks["demonic-embrace"]=0;
        t.Ranks["master-demonologist"]=5;
        t.DemonologyPointsSpent=20;
        t.AfflictionPointsSpent=10;
        Assert.Throws<ArgumentException>(()=>t.Validate(60,"demonology"));
    }
}
