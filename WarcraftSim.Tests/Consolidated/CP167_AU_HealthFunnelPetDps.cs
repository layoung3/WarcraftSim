using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Warlock;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167AuHealthFunnelPetDps
{
    private static WarlockSimRequest Setup(int rank,bool healing=true,bool secondHit=false)
    {
        var r=WarlockSimRequest.CreatePreset("demonology");
        r.PetType="succubus";
        r.PrePullSacrificedDemon=null;
        r.EnablePet=true;
        r.UseLifeTap=false;
        r.Talents.Ranks["improved-health-funnel"]=rank;
        r.CaptureTimeline=true;
        r.BaseSpellHitPercent=100m;
        r.TargetMaximumHealth=1000000m;
        r.DurationSeconds=secondHit?30m:14m;
        r.CustomPriority=healing?["health-funnel","shadow-bolt"]:["shadow-bolt"];
        r.DemonIncomingDamageAtSeconds=1m;
        r.DemonIncomingDamageAmount=1200m;
        if(secondHit)
        {
            r.DemonSecondIncomingDamageAtSeconds=18m;
            r.DemonSecondIncomingDamageAmount=1200m;
        }
        return r;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void RealFunnelChannelTransfersHealthFromCasterToSameActivePet(int rank)
    {
        var request=Setup(rank);
        var result=new WarlockSimRunner().Run(request);
        var count=result.Run.CustomMetrics["warlock-health-funnel-ticks"];
        Assert.True(count>0m);
        Assert.Equal(count*79m*(1m-.15m*rank),
            result.Run.CustomMetrics["warlock-health-funnel-health-spent"]);
        Assert.True(result.Run.CustomMetrics["warlock-health-funnel-pet-healing"]>0m);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.Healing &&
            e.AbilityKey=="health-funnel" && e.SourceActorKey=="warlock" &&
            e.TargetActorKey=="warlock-pet" &&
            e.RawAmount==153m*(1m+.20m*rank));
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.HealthChanged &&
            e.AbilityKey=="health-funnel" && e.Amount<0m);
        Assert.Equal(request.WarlockMaximumHealth-
            result.Run.CustomMetrics["warlock-health-funnel-health-spent"],
            result.EndingHealth);
    }

    [Fact]
    public void OptInHealthFunnelCanSavePetFromSecondExplicitHitAndPreserveDpsUptime()
    {
        var dry=Setup(0,false,true);
        var protectedPet=Setup(0,true,true);
        var without=new WarlockSimRunner().Run(dry);
        var with=new WarlockSimRunner().Run(protectedPet);
        Assert.True(without.DemonDied);
        Assert.False(with.DemonDied);
        Assert.True(with.DemonUptimeSeconds>without.DemonUptimeSeconds);
        Assert.True(with.PetDamage>without.PetDamage);
        Assert.True(with.Run.CustomMetrics["warlock-health-funnel-pet-healing"]>0m);
    }

    [Fact]
    public void FullHealthUntalentedPetWillNotWasteHealthFunnel()
    {
        var r=Setup(0);
        r.DemonIncomingDamageAtSeconds=null;
        r.DemonIncomingDamageAmount=0m;
        var result=new WarlockSimRunner().Run(r);
        Assert.Equal(0m,result.Run.CustomMetrics["warlock-health-funnel-ticks"]);
        Assert.DoesNotContain(result.Run.Timeline,e=>
            e.Type==CombatEventType.AbilityChannelStarted &&
            e.AbilityKey=="health-funnel");
    }

    [Fact]
    public void CurrentLevelSixtyChannelAndTalentRanksAreRecognized()
    {
        var spells=ForeverWarlockDpsSpellbook.Create("demonology",
            ForeverWarlockTalentBuild.Default("demonology"),new ForeverWarlockTuning(),0m,0m);
        var ability=Assert.Single(spells,x=>x.Key=="health-funnel");
        Assert.Equal(10m,ability.ChannelDurationSeconds);
        Assert.Equal(1m,ability.ChannelTickIntervalSeconds);
        var bad=Setup(3);
        Assert.Throws<ArgumentException>(()=>new WarlockSimRunner().Run(bad));
    }
}
