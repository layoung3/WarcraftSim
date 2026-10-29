using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp168AfflictionShadowMasteryCoverage
{
    private static WarlockSimRequest Setup(string spell,int rank)
    {
        var request=WarlockSimRequest.CreatePreset("affliction");
        request.EnablePet=false;
        request.PrePullSacrificedDemon=null;
        request.UseLifeTap=false;
        request.TargetMaximumHealth=1000000m;
        request.DurationSeconds=8m;
        request.BaseSpellHitPercent=100m;
        request.SpellCritPercent=0m;
        request.Talents.Ranks["shadow-mastery"]=rank;
        request.CustomPriority=[spell];
        request.CaptureTimeline=true;
        return request;
    }

    [Theory]
    [InlineData("drain-soul")]
    [InlineData("death-coil")]
    public void RankFiveShadowMasteryIncreasesEligibleDamageByTenPercent(string spell)
    {
        decimal FirstHit(int rank)
        {
            var run=new WarlockSimRunner().Run(Setup(spell,rank));
            return run.Run.Timeline.First(e=>e.Type==CombatEventType.Damage &&
                e.AbilityKey==spell && e.Amount>0m).Amount!.Value;
        }
        Assert.Equal(FirstHit(0)*1.10m,FirstHit(5));
    }

    [Fact]
    public void ShadowMasteryIncreasesEffectiveDeathCoilHealingWithLandedDamage()
    {
        var baseRun=new WarlockSimRunner().Run(Setup("death-coil",0));
        var enhanced=new WarlockSimRunner().Run(Setup("death-coil",5));
        decimal Healing(WarlockSimResponse run)=>
            run.Run.Timeline.First(e=>e.Type==CombatEventType.Healing &&
                e.AbilityKey=="death-coil").RawAmount!.Value;
        Assert.Equal(Healing(baseRun)*1.10m,Healing(enhanced));
    }
}
