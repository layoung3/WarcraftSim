using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167AaMageHeatingUp
{
    [Fact]
    public void PresenceOfMindPyroblastDoesNotConsumeExistingHeatingUpStack()
    {
        var req=MageSimRequest.CreatePreset("fire");
        req.DurationSeconds=8m;
        req.BaseSpellHitPercent=100m;
        req.SpellCritPercent=0m;
        req.UseEvocation=false;
        req.UseMajorManaPotion=false;
        req.CombatItems.ManaRubyCharges=0;
        req.Talents.FirePointsSpent=30;
        req.Talents.ArcanePointsSpent=21;
        req.Talents.Ranks.Remove("combustion");
        req.Talents.Ranks["presence-of-mind"]=1;
        req.CustomPriority=["fire-blast","presence-of-mind","pyroblast"];
        var result=new MageSimRunner().Run(req);
        var casts=result.Run.Timeline.Where(e=>e.AbilityKey=="pyroblast" &&
            e.Type==CombatEventType.AbilityCastStarted).ToList();
        var completes=result.Run.Timeline.Where(e=>e.AbilityKey=="pyroblast" &&
            e.Type==CombatEventType.AbilityCastCompleted).ToList();
        Assert.True(casts.Count>=2);
        Assert.True(completes.Count>=2);
        Assert.Equal(0m,completes[0].TimeSeconds-casts[0].TimeSeconds);
        Assert.Equal(4.5m,completes[1].TimeSeconds-casts[1].TimeSeconds);
        Assert.Equal(1,result.HeatingUpPyroblasts);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.AuraRemoved &&
            e.AbilityKey=="presence-of-mind");
    }
}
