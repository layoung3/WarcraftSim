using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Warlock;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167AjAftermath
{
    private static WarlockSimRequest Setup(int rank)
    {
        var r=WarlockSimRequest.CreatePreset("destruction");
        r.EnablePet=false;
        r.PrePullSacrificedDemon=null;
        r.UseLifeTap=false;
        r.SpellCritPercent=0m;
        r.BaseSpellHitPercent=100m;
        r.TargetMaximumHealth=1000000m;
        r.DurationSeconds=11m;
        r.CustomPriority=["immolate","shadow-bolt"];
        r.Talents.Ranks["aftermath"]=rank;
        return r;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void AftermathBoostsOnlyImmolateInitialImpact(int rank)
    {
        var zero=new WarlockSimRunner().Run(Setup(0));
        var mod=new WarlockSimRunner().Run(Setup(rank));
        decimal Damage(WarlockSimResponse r,bool periodic)=>
            r.Run.Timeline.First(e=>e.Type==CombatEventType.Damage &&
                e.AbilityKey=="immolate" && e.IsPeriodic==periodic &&
                e.Amount is >0m).Amount!.Value;
        Assert.Equal(Damage(zero,false)*(1m+.10m*rank),Damage(mod,false));
        Assert.Equal(Damage(zero,true),Damage(mod,true));
    }

    [Fact]
    public void AftermathRejectsRankSix()
    {
        var r=Setup(6);
        Assert.Throws<ArgumentException>(()=>new WarlockSimRunner().Run(r));
    }
}
