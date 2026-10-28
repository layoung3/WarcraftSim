using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Warlock;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167ZWarlockNightfall
{
    [Theory]
    [InlineData(0,0)]
    [InlineData(1,2)]
    [InlineData(2,4)]
    public void NightfallTalentRankHasCurrentClientProcChance(int rank,int percent)
    {
        Assert.Equal(percent,WarlockNightfallProcessor.ProcChancePercent(rank));
        var build=ForeverWarlockTalentBuild.Default("affliction");
        build.Ranks["nightfall"]=rank;
        build.Validate(60,"affliction");
    }

    [Fact]
    public void NightfallRankThreeOrWithoutFifteenEarlierAfflictionPointsIsRejected()
    {
        var t=ForeverWarlockTalentBuild.Default("affliction");
        t.Ranks["nightfall"]=3;
        Assert.Throws<ArgumentException>(()=>t.Validate(60,"affliction"));
        t.Ranks["nightfall"]=2;
        t.AfflictionPointsSpent=15;
        t.DemonologyPointsSpent=0;
        Assert.Throws<ArgumentException>(()=>t.Validate(60,"affliction"));
    }

    [Fact]
    public void CorruptionDamageCanGrantInstantNextShadowBoltOnSeededLongEncounter()
    {
        var req=WarlockSimRequest.CreatePreset("affliction");
        req.Talents.Ranks["nightfall"]=2;
        req.DurationSeconds=620m;
        req.TargetMaximumHealth=10000000m;
        req.CaptureTimeline=true;
        req.EnablePet=false;
        req.PrePullSacrificedDemon=null;
        req.UseLifeTap=true;
        req.AssumeExternalHealingForLifeTap=true;
        req.BaseSpellHitPercent=100m;
        req.SpellCritPercent=0m;
        req.CustomPriority=["corruption","shadow-bolt"];
        var run=new WarlockSimRunner().Run(req);
        Assert.True(run.Run.CustomMetrics["warlock-nightfall-procs"]>0m);
        Assert.True(run.Run.CustomMetrics["warlock-nightfall-consumed"]>0m);
        var instants=run.Run.Timeline.Where(e=>e.Type==CombatEventType.AbilityCastStarted &&
            e.AbilityKey=="shadow-bolt").Select(start=>new {
                Start=start,
                Completed=run.Run.Timeline.FirstOrDefault(e=>
                    e.Type==CombatEventType.AbilityCastCompleted &&
                    e.AbilityExecutionId==start.AbilityExecutionId)
            }).Where(x=>x.Completed is not null &&
                x.Completed.TimeSeconds==x.Start.TimeSeconds).ToList();
        Assert.NotEmpty(instants);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="nightfall" && e.TargetActorKey=="warlock");
        Assert.InRange(Math.Abs(run.DamageDone-run.DamageByAbility.Values.Sum()),
            0m,.000001m);
    }

    [Fact]
    public void NoNightfallTalentCannotGrantShadowTrance()
    {
        var req=WarlockSimRequest.CreatePreset("affliction");
        req.DurationSeconds=55m;
        req.EnablePet=false;
        req.UseLifeTap=true;
        req.CustomPriority=["corruption","shadow-bolt"];
        var run=new WarlockSimRunner().Run(req);
        Assert.Equal(0m,run.Run.CustomMetrics["warlock-nightfall-procs"]);
        Assert.DoesNotContain(run.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="nightfall");
    }
}
