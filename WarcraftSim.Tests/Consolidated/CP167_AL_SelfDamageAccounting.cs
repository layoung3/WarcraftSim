using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167AlSelfDamageAccounting
{
    [Fact]
    public void HellfireCasterTicksCountOnlyAsTakenDamageNotAsOutgoingDps()
    {
        var req=WarlockSimRequest.CreatePreset("destruction");
        req.EnablePet=false;
        req.PrePullSacrificedDemon=null;
        req.UseLifeTap=false;
        req.DurationSeconds=5m;
        req.BaseSpellHitPercent=100m;
        req.SpellCritPercent=0m;
        req.CustomPriority=["hellfire"];
        req.CaptureTimeline=true;
        var run=new WarlockSimRunner().Run(req);
        var outgoing=run.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="hellfire" && e.TargetActorKey=="target").Sum(e=>e.Amount??0m);
        var self=run.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="hellfire" && e.TargetActorKey=="warlock").Sum(e=>e.Amount??0m);
        Assert.True(outgoing>0m);
        Assert.True(self>0m);
        Assert.Equal(outgoing,run.DamageDone);
        Assert.Equal(outgoing,run.DamageByAbility["hellfire"]);
        Assert.Equal(outgoing,run.Run.Summary.ActorSummaries["warlock"].DamageDone);
        Assert.Equal(self,run.Run.Summary.ActorSummaries["warlock"].DamageTaken);
        Assert.Equal(req.WarlockMaximumHealth-self,run.EndingHealth);
    }
}
