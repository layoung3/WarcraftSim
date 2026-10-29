using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp168AfflictionImprovedDrains
{
    private static WarlockSimRequest Setup(int rank,string spell)
    {
        var req=WarlockSimRequest.CreatePreset("affliction");
        req.EnablePet=false;
        req.PrePullSacrificedDemon=null;
        req.UseLifeTap=false;
        req.DurationSeconds=9m;
        req.TargetMaximumHealth=1000000m;
        req.BaseSpellHitPercent=100m;
        req.SpellCritPercent=0m;
        req.Talents.Ranks["improved-drains"]=rank;
        req.CustomPriority=[spell];
        req.CaptureTimeline=true;
        return req;
    }

    [Theory]
    [InlineData("drain-life",1,1.07)]
    [InlineData("drain-life",2,1.13)]
    [InlineData("drain-life",3,1.20)]
    [InlineData("drain-soul",1,1.07)]
    [InlineData("drain-soul",2,1.13)]
    [InlineData("drain-soul",3,1.20)]
    [InlineData("wrack",1,1.07)]
    [InlineData("wrack",2,1.13)]
    [InlineData("wrack",3,1.20)]
    public void EachRankModifiesActualChannelTick(string spell,int rank,double expected)
    {
        decimal FirstTick(int chosenRank)
        {
            var run=new WarlockSimRunner().Run(Setup(chosenRank,spell));
            return run.Run.Timeline.First(e=>e.Type==CombatEventType.Damage &&
                e.AbilityKey==spell && e.IsChannelTick && e.Amount>0m).Amount!.Value;
        }
        Assert.Equal(FirstTick(0)*(decimal)expected,FirstTick(rank));
    }

    [Fact]
    public void InvalidFourthRankIsRejectedByLevelSixtyTalentValidation()
    {
        var request=Setup(4,"drain-life");
        Assert.Throws<ArgumentException>(()=>new WarlockSimRunner().Run(request));
    }
}
