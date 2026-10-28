using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167AqDpsProcsAndChannelPushback
{
    private static MageSimRequest Frost(int rank,int seed,decimal duration=3.2m)
    {
        var r=MageSimRequest.CreatePreset("frost");
        r.Talents.Ranks["fingers-of-frost"]=rank;
        r.Seed=seed;
        r.DurationSeconds=duration;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.UseEvocation=false;
        r.UseMajorManaPotion=false;
        r.CombatItems.ManaRubyCharges=0;
        r.CustomPriority=["frostbolt"];
        return r;
    }

    [Fact]
    public void ForeverFingersOfFrostHasSameProcChanceAtBothRanksButOneOrTwoCharges()
    {
        var trials=0;
        for(var seed=1;seed<=200;seed++)
        {
            var first=new MageSimRunner().Run(Frost(1,seed));
            var second=new MageSimRunner().Run(Frost(2,seed));
            Assert.Equal(first.FingersOfFrostProcs,second.FingersOfFrostProcs);
            if(first.FingersOfFrostProcs==0)continue;
            trials++;
            Assert.Equal(1m,first.Run.CustomMetrics["mage-fingers-charges-granted"]);
            Assert.Equal(2m,second.Run.CustomMetrics["mage-fingers-charges-granted"]);
            Assert.Equal(1m,first.Run.CustomMetrics["mage-fingers-charges-end"]);
            Assert.Equal(2m,second.Run.CustomMetrics["mage-fingers-charges-end"]);
            break;
        }
        Assert.Equal(1,trials);
    }

    [Fact]
    public void FingersChargesExpireAfterFifteenSecondsWithoutAnEligibleRefresh()
    {
        int? procSeed=null;
        for(var seed=1;seed<=200;seed++)
        {
            var request=Frost(1,seed);
            request.StartingManaPercent=8m; // permits one Frostbolt, not a second
            if(new MageSimRunner().Run(request).FingersOfFrostProcs>0)
            { procSeed=seed;break; }
        }
        Assert.NotNull(procSeed);
        var early=Frost(1,procSeed!.Value,3.2m);
        early.StartingManaPercent=8m;
        var late=Frost(1,procSeed.Value,19m);
        late.StartingManaPercent=8m;
        Assert.Equal(1m,new MageSimRunner().Run(early).Run.CustomMetrics["mage-fingers-charges-end"]);
        Assert.Equal(0m,new MageSimRunner().Run(late).Run.CustomMetrics["mage-fingers-charges-end"]);
    }

    private static WarlockSimRequest Warlock(string spec,string channel,int rank,int seed)
    {
        var r=WarlockSimRequest.CreatePreset(spec);
        r.Seed=seed;
        r.EnablePet=false;
        r.PrePullSacrificedDemon=null;
        r.UseLifeTap=false;
        r.CaptureTimeline=true;
        r.TargetMaximumHealth=1000000m;
        r.DurationSeconds=9m;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.CustomPriority=[channel];
        r.IncomingDamageAtSeconds=2m;
        r.IncomingDamageAmount=40m;
        if(spec=="affliction")r.Talents.Ranks["fel-concentration"]=rank;
        else r.Talents.Ranks["intensity"]=rank;
        return r;
    }

    [Theory]
    [InlineData("affliction","drain-life")]
    [InlineData("affliction","drain-soul")]
    [InlineData("affliction","wrack")]
    [InlineData("destruction","rain-of-fire")]
    [InlineData("destruction","hellfire")]
    public void TalentProtectsActualChannelsFromDamageDrivenCancellation(
        string spec,string channel)
    {
        var baseline=new WarlockSimRunner().Run(Warlock(spec,channel,0,19));
        Assert.True(baseline.Run.CustomMetrics["warlock-channel-cancellations"]>0m);
        int successfulProtection=0;
        for(var seed=1;seed<=25;seed++)
        {
            var talented=new WarlockSimRunner().Run(Warlock(spec,channel,3,seed));
            if(talented.Run.CustomMetrics["warlock-cast-interrupt-resisted"]>0m)
            {
                Assert.Equal(0m,talented.Run.CustomMetrics["warlock-channel-cancellations"]);
                successfulProtection++;
            }
        }
        Assert.True(successfulProtection>0);
    }

    [Fact]
    public void IntensityAlsoProtectsHardCastsWithoutGrantingInvulnerability()
    {
        var baseline=new WarlockSimRunner().Run(Warlock("destruction","shadow-bolt",0,19));
        Assert.True(baseline.Run.CustomMetrics["warlock-hardcast-pushback-seconds"]>0m);
        int resisted=0;
        for(var seed=1;seed<=30;seed++)
        {
            var result=new WarlockSimRunner().Run(Warlock("destruction","shadow-bolt",3,seed));
            if(result.Run.CustomMetrics["warlock-cast-interrupt-resisted"]>0m)
            {
                resisted++;
                Assert.Equal(0m,result.Run.CustomMetrics["warlock-hardcast-pushback-seconds"]);
            }
        }
        Assert.True(resisted>0);
    }
}
