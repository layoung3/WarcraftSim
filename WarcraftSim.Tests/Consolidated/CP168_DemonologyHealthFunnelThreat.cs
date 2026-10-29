using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp168DemonologyHealthFunnelThreat
{
    [Theory]
    [InlineData(0,50)]
    [InlineData(1,25)]
    [InlineData(2,0)]
    public void TalentRanksReduceActualPetHealingThreat(int rank,int expected)
    {
        var ctx=new SimulationContext(new SimulationRunOptions{DurationSeconds=5m});
        var owner=new SimulationActorState{Key="warlock",TeamKey="raid",MaximumHealth=3000m};
        var demon=new SimulationActorState{Key="warlock-pet",TeamKey="raid",MaximumHealth=2500m};
        var boss=new SimulationActorState{Key="target",TeamKey="enemy",MaximumHealth=10000m};
        foreach(var actor in new[]{owner,demon,boss}){
            actor.InitializeHealth(actor.MaximumHealth);
            ctx.AddActor(actor);
        }
        var healing=new CombatEvent{
            Type=CombatEventType.Healing,SourceActorKey="warlock",
            TargetActorKey="warlock-pet",AbilityKey="health-funnel",Amount=100m
        };
        var contributions=new WarlockDemonThreat(rank).Resolve(ctx,healing);
        Assert.Equal((decimal)expected,Assert.Single(contributions).Amount);
        var other=new CombatEvent{
            Type=CombatEventType.Healing,SourceActorKey="warlock",
            TargetActorKey="warlock-pet",AbilityKey="demonic-energies",Amount=100m
        };
        Assert.Equal(50m,Assert.Single(new WarlockDemonThreat(rank).Resolve(ctx,other)).Amount);
    }

    [Fact]
    public void FullyImprovedHealthFunnelStillHealsPetAndConsumesOwnerHealth()
    {
        var r=WarlockSimRequest.CreatePreset("demonology");
        r.PetType="imp";
        r.PrePullSacrificedDemon=null;
        r.Talents.Ranks["improved-health-funnel"]=2;
        r.EnablePet=true;
        r.DemonIncomingDamageAtSeconds=2m;
        r.DemonIncomingDamageAmount=1100m;
        r.UseHealthFunnel=true;
        r.UseLifeTap=false;
        r.DurationSeconds=16m;
        r.CaptureTimeline=true;
        var run=new WarlockSimRunner().Run(r);
        Assert.True(run.Run.CustomMetrics["warlock-health-funnel-pet-healing"]>0m);
        Assert.True(run.Run.CustomMetrics["warlock-health-funnel-health-spent"]>0m);
        Assert.DoesNotContain(run.Run.Timeline,e=>e.Type==CombatEventType.ThreatChanged &&
            e.AbilityKey=="health-funnel" && e.Amount>0m);
    }
}
