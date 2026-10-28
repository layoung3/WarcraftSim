using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167AtHeatingUpLandedDamage
{
    private static MageSimRequest Fire(decimal hit,decimal crit,bool pyro=false)
    {
        var r=MageSimRequest.CreatePreset("fire");
        r.DurationSeconds=pyro?9m:2m;
        r.BaseSpellHitPercent=hit;
        r.SpellCritPercent=crit;
        r.UseEvocation=false;
        r.UseMajorManaPotion=false;
        r.CombatItems.ManaRubyCharges=0;
        r.UseHeatingUp=true;
        r.CaptureTimeline=true;
        r.CustomPriority=pyro?["fire-blast","pyroblast"]:["fire-blast"];
        return r;
    }

    [Theory]
    [InlineData(0,0,0)]
    [InlineData(100,0,1)]
    [InlineData(100,100,2)]
    public void FireBlastGeneratesZeroOneOrTwoStacksFromResolvedDamage(
        int hit,int crit,int expected)
    {
        var result=new MageSimRunner().Run(Fire(hit,crit));
        Assert.Equal((decimal)expected,result.Run.CustomMetrics["mage-heating-up-charges-granted"]);
        Assert.Equal((decimal)expected,result.Run.CustomMetrics["mage-heating-up-charges-at-end"]);
        if(hit==0)
            Assert.DoesNotContain(result.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
                e.AbilityKey=="fire-blast" && e.Amount is >0m);
    }

    [Fact]
    public void MissedFireBlastDoesNotReduceNextPyroblastCastTime()
    {
        var miss=new MageSimRunner().Run(Fire(0m,0m,true));
        var hit=new MageSimRunner().Run(Fire(100m,0m,true));
        decimal PyroCompleted(MageSimResponse r)=>r.Run.Timeline.First(e=>
            e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey=="pyroblast").TimeSeconds;
        // 6s base Pyro versus 4.5s after one landed Fire Blast charge.
        Assert.Equal(7.5m,PyroCompleted(miss));
        Assert.Equal(6m,PyroCompleted(hit));
        Assert.True(PyroCompleted(hit)<PyroCompleted(miss));
    }
}
