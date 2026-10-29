using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp168AfflictionSuppressionThreat
{
    [Theory]
    [InlineData(0,100)]
    [InlineData(1,96)]
    [InlineData(3,88)]
    [InlineData(5,80)]
    public void OwnerThreatFallsByFourPercentPerRankWithoutChangingPetThreat(int rank,int expected)
    {
        var context=new SimulationContext(new SimulationRunOptions{DurationSeconds=10m});
        var warlock=new SimulationActorState{Key="warlock",TeamKey="raid"};
        var pet=new SimulationActorState{Key="warlock-pet",TeamKey="raid"};
        var boss=new SimulationActorState{Key="target",TeamKey="enemy"};
        context.AddActor(warlock);
        context.AddActor(pet);
        context.AddActor(boss);
        warlock.Stats.Set("warlock-suppression-threat-reduction",rank*.04m);
        var resolver=new WarlockDemonThreat();
        var ownerEvent=new CombatEvent{
            Type=CombatEventType.Damage,SourceActorKey="warlock",
            TargetActorKey="target",AbilityKey="shadow-bolt",Amount=100m
        };
        var petEvent=new CombatEvent{
            Type=CombatEventType.Damage,SourceActorKey="warlock-pet",
            TargetActorKey="target",AbilityKey="pet-melee",Amount=100m
        };
        Assert.Equal((decimal)expected,Assert.Single(resolver.Resolve(context,ownerEvent)).Amount);
        Assert.Equal(100m,Assert.Single(resolver.Resolve(context,petEvent)).Amount);
    }

    [Fact]
    public void FullSimulatorSuppressionReducesThreatButNotGuaranteedHitDamage()
    {
        WarlockSimRequest Setup(int rank)
        {
            var req=WarlockSimRequest.CreatePreset("affliction");
            req.Talents.Ranks["suppression"]=rank;
            req.EnablePet=false;
            req.PrePullSacrificedDemon=null;
            req.UseLifeTap=false;
            req.DurationSeconds=8m;
            req.TargetMaximumHealth=1000000m;
            req.BaseSpellHitPercent=100m;
            req.SpellCritPercent=0m;
            req.CaptureTimeline=true;
            req.CustomPriority=["shadow-bolt"];
            return req;
        }
        var ordinary=new WarlockSimRunner().Run(Setup(0));
        var protectedRun=new WarlockSimRunner().Run(Setup(5));
        decimal Threat(WarlockSimResponse result)=>result.Run.Timeline.Where(e=>
            e.Type==CombatEventType.ThreatChanged && e.SourceActorKey=="warlock" &&
            e.TargetActorKey=="target").Sum(e=>e.Amount??0m);
        Assert.Equal(ordinary.DamageDone,protectedRun.DamageDone);
        Assert.True(Threat(ordinary)>0m);
        Assert.Equal(Threat(ordinary)*.8m,Threat(protectedRun));
    }
}
