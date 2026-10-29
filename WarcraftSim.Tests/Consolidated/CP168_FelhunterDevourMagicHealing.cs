using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp168FelhunterDevourMagicHealing
{
    private static WarlockSimRequest Setup(int rank,bool aura)
    {
        var req=WarlockSimRequest.CreatePreset("demonology");
        req.PetType="felhunter";
        req.PrePullSacrificedDemon=null;
        req.EnablePet=true;
        req.Talents.Ranks["improved-felhunter"]=rank;
        req.DemonTargetHasMagicAura=aura;
        req.DemonIncomingDamageAtSeconds=.75m;
        req.DemonIncomingDamageAmount=1100m;
        req.UseLifeTap=false;
        req.DurationSeconds=12m;
        req.TargetMaximumHealth=1000000m;
        req.CaptureTimeline=true;
        return req;
    }

    [Theory]
    [InlineData(0,618)]
    [InlineData(1,679.8)]
    [InlineData(2,741.6)]
    [InlineData(3,803.4)]
    public void RealDispelHealsFelhunterBySourceBackedRankValue(int rank,double heal)
    {
        var result=new WarlockSimRunner().Run(Setup(rank,true));
        var restored=Assert.Single(result.Run.Timeline,e=>
            e.Type==CombatEventType.Healing &&
            e.AbilityKey=="demon-devour-magic");
        Assert.Equal((decimal)heal,restored.RawAmount);
        Assert.Equal((decimal)heal,restored.Amount);
        Assert.Equal(0m,restored.OverhealingAmount);
        Assert.Contains(result.Run.Timeline,e=>
            e.Type==CombatEventType.AuraRemoved &&
            e.AbilityKey=="encounter-magic-buff");
        Assert.Contains(result.Run.Timeline,e=>
            e.SourceActorKey=="warlock-pet" &&
            e.AbilityKey=="demon-devour-magic" &&
            e.Type==CombatEventType.ResourceChanged &&
            e.ResourceKey=="mana" && e.Amount==-215m);
    }

    [Fact]
    public void NoActualEnemyMagicAuraMeansNoDevourHealingOrManaSpent()
    {
        var result=new WarlockSimRunner().Run(Setup(3,false));
        Assert.DoesNotContain(result.Run.Timeline,e=>
            e.Type==CombatEventType.Healing &&
            e.AbilityKey=="demon-devour-magic");
        Assert.DoesNotContain(result.Run.Timeline,e=>
            e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey=="demon-devour-magic");
    }
}
