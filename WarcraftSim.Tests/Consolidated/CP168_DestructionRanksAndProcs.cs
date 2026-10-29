using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Warlock;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp168DestructionRankAndProcOutcomes
{
    private static WarlockSimRequest Setup(int fireAndBrimstone,int seed)
    {
        var r=WarlockSimRequest.CreatePreset("destruction");
        r.Seed=seed;
        r.Talents.Ranks["fire-and-brimstone"]=fireAndBrimstone;
        r.EnablePet=false;
        r.PrePullSacrificedDemon=null;
        r.UseLifeTap=false;
        r.TargetMaximumHealth=1000000m;
        r.DurationSeconds=5m;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.CustomPriority=["immolate","conflagrate"];
        r.CaptureTimeline=true;
        return r;
    }

    [Fact]
    public void FireAndBrimstoneRanksIncreaseActualConflagrateCriticalFrequency()
    {
        var counts=new int[4];
        for(var seed=1;seed<=120;seed++)
            for(var rank=0;rank<=3;rank++)
            {
                var run=new WarlockSimRunner().Run(Setup(rank,seed));
                var hit=Assert.Single(run.Run.Timeline,e=>e.AbilityKey=="conflagrate" &&
                    e.Type==CombatEventType.Damage && e.Amount>0m);
                if(hit.IsCritical)counts[rank]++;
            }
        Assert.True(counts[1]>=counts[0],$"r0={counts[0]}, r1={counts[1]}");
        Assert.True(counts[2]>=counts[1],$"r1={counts[1]}, r2={counts[2]}");
        Assert.True(counts[3]>counts[2],$"r2={counts[2]}, r3={counts[3]}");
    }

    [Theory]
    [InlineData(0,255)]
    [InlineData(1,247.35)]
    [InlineData(2,237.15)]
    [InlineData(3,229.5)]
    public void CataclysmRanksReduceRealConflagrateManaCost(int rank,double expected)
    {
        var t=ForeverWarlockTalentBuild.Default("destruction");
        t.Ranks["cataclysm"]=rank;
        var spell=Assert.Single(ForeverWarlockDpsSpellbook.Create("destruction",
            t,new ForeverWarlockTuning(),0m,0m),s=>s.Key=="conflagrate");
        Assert.Equal((decimal)expected,spell.ResourceCosts.Single().Amount);
    }

    [Theory]
    [InlineData(0,3.0)]
    [InlineData(1,2.9)]
    [InlineData(3,2.7)]
    [InlineData(5,2.5)]
    public void BaneRanksReduceActualShadowBoltCastTime(int rank,double expected)
    {
        var t=ForeverWarlockTalentBuild.Default("destruction");
        t.Ranks["bane"]=rank;
        var spell=Assert.Single(ForeverWarlockDpsSpellbook.Create("destruction",
            t,new ForeverWarlockTuning(),0m,0m),s=>s.Key=="shadow-bolt");
        Assert.Equal((decimal)expected,spell.CastTimeSeconds);
    }

    [Fact]
    public void AftermathModifiesImmolateImpactButNotPeriodicDamage()
    {
        WarlockSimRequest Configure(int rank)
        {
            var r=Setup(0,168128);
            r.Talents.Ranks["aftermath"]=rank;
            r.DurationSeconds=7m;
            r.CustomPriority=["immolate"];
            return r;
        }
        var original=new WarlockSimRunner().Run(Configure(0));
        var improved=new WarlockSimRunner().Run(Configure(5));
        decimal Hit(WarlockSimResponse r)=>r.Run.Timeline.First(e=>
            e.Type==CombatEventType.Damage && e.AbilityKey=="immolate" &&
            !e.IsPeriodic && e.Amount>0m).Amount!.Value;
        decimal Tick(WarlockSimResponse r)=>r.Run.Timeline.First(e=>
            e.Type==CombatEventType.Damage && e.AbilityKey=="immolate" &&
            e.IsPeriodic && e.Amount>0m).Amount!.Value;
        Assert.Equal(Hit(original)*1.5m,Hit(improved));
        Assert.Equal(Tick(original),Tick(improved));
    }
}
