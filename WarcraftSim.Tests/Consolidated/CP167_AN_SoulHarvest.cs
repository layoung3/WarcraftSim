using System;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167AnSoulHarvest
{
    private static WarlockSimRequest Setup(int rank,bool eligible=true)
    {
        var r=WarlockSimRequest.CreatePreset("affliction");
        r.EnablePet=false;
        r.PrePullSacrificedDemon=null;
        r.UseLifeTap=false;
        r.CustomPriority=["drain-soul"];
        r.Talents.Ranks["soul-harvest"]=rank;
        r.TargetMaximumHealth=40m;
        r.TargetYieldsSoulShard=eligible;
        r.Tuning.ManaRegenerationPerSecond=10m;
        r.DurationSeconds=13m;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.CaptureTimeline=true;
        return r;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void EligibleDrainSoulKillTriggersRankSensitiveSpiritRegeneration(int rank)
    {
        var original=new WarlockSimRunner().Run(Setup(0));
        var enhanced=new WarlockSimRunner().Run(Setup(rank));
        Assert.Equal(1m,enhanced.Run.CustomMetrics["warlock-soul-harvest-procs"]);
        Assert.Contains(enhanced.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="soul-harvest" && e.TargetActorKey=="warlock");
        Assert.True(enhanced.EndingMana>original.EndingMana);
    }

    [Fact]
    public void SoulHarvestCannotTriggerFromTrivialTargets()
    {
        var r=new WarlockSimRunner().Run(Setup(2,false));
        Assert.Equal(0m,r.Run.CustomMetrics["warlock-soul-harvest-procs"]);
        Assert.DoesNotContain(r.Run.Timeline,e=>e.AbilityKey=="soul-harvest");
    }

    [Fact]
    public void SoulHarvestRejectsUnlearnedThirdRank()
    {
        Assert.Throws<ArgumentException>(()=>new WarlockSimRunner().Run(Setup(3)));
    }
}
