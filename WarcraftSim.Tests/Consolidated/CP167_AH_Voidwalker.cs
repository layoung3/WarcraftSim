using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167AhVoidwalker
{
    private static WarlockSimRequest Setup(int rank)
    {
        var r=WarlockSimRequest.CreatePreset("demonology");
        r.PetType="voidwalker";
        r.UseLifeTap=false;
        r.Talents.Ranks["improved-voidwalker"]=rank;
        r.TargetMaximumHealth=1000000m;
        r.DurationSeconds=11m;
        r.CaptureTimeline=true;
        r.DemonTuning.VoidwalkerTormentThreat=500m;
        r.DemonTuning.VoidwalkerSufferingThreat=350m;
        return r;
    }

    [Theory]
    [InlineData(0,500)]
    [InlineData(1,550)]
    [InlineData(2,600)]
    [InlineData(3,650)]
    public void ImprovedVoidwalkerActuallyIncreasesTormentThreat(int rank,int expected)
    {
        var result=new WarlockSimRunner().Run(Setup(rank));
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.ThreatChanged &&
            e.AbilityKey=="demon-torment" && e.Amount==(decimal)expected);
    }

    [Fact]
    public void ImprovedVoidwalkerRejectsOldExtraRanks()
    {
        var r=Setup(4);
        Assert.Throws<ArgumentException>(()=>new WarlockSimRunner().Run(r));
    }

    [Fact]
    public void ImprovedVoidwalkerDoesNotCreatePetDamageMultiplier()
    {
        var a=new WarlockSimRunner().Run(Setup(0));
        var b=new WarlockSimRunner().Run(Setup(3));
        Assert.Equal(a.PetDamage,b.PetDamage);
        Assert.True(b.DemonThreat>a.DemonThreat);
    }
}
