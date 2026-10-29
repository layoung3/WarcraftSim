using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp168AfflictionSoulSiphonCurse
{
    private static WarlockSimRequest Setup(int rank,string curse)
    {
        var request=WarlockSimRequest.CreatePreset("affliction");
        request.EnablePet=false;
        request.PrePullSacrificedDemon=null;
        request.UseLifeTap=false;
        request.TargetMaximumHealth=1000000m;
        request.DurationSeconds=11m;
        request.BaseSpellHitPercent=100m;
        request.SpellCritPercent=0m;
        request.Talents.Ranks["soul-siphon"]=rank;
        request.CustomPriority=[curse,"drain-life"];
        request.CaptureTimeline=true;
        return request;
    }

    [Theory]
    [InlineData("curse-of-the-elements")]
    [InlineData("curse-of-recklessness")]
    public void CurrentOwnedCurseCountsAsOneSoulSiphonEffect(string curse)
    {
        var baseline=new WarlockSimRunner().Run(Setup(0,curse));
        var talented=new WarlockSimRunner().Run(Setup(3,curse));
        decimal Tick(WarlockSimResponse run)=>run.Run.Timeline.First(e=>
            e.Type==CombatEventType.Damage && e.AbilityKey=="drain-life" &&
            e.IsChannelTick && e.Amount is >0m).Amount!.Value;
        Assert.Contains(talented.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey==curse);
        Assert.Equal(Tick(baseline)*1.12m,Tick(talented));
        Assert.InRange(Math.Abs(talented.DamageDone-talented.DamageByAbility.Values.Sum()),
            0m,.000001m);
    }
}
