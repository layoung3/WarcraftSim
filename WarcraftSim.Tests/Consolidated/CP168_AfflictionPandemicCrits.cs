using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp168AfflictionPandemicCrits
{
    private static WarlockSimRequest Setup(string spell,int rank)
    {
        var req=WarlockSimRequest.CreatePreset("affliction");
        req.EnablePet=false;
        req.PrePullSacrificedDemon=null;
        req.UseLifeTap=false;
        req.DurationSeconds=spell=="bane-of-doom"?61m:10m;
        req.TargetMaximumHealth=1000000m;
        req.BaseSpellHitPercent=100m;
        req.SpellCritPercent=100m;
        req.Talents.Ranks["pandemic"]=rank;
        req.CustomPriority=[spell];
        req.CaptureTimeline=true;
        return req;
    }

    [Theory]
    [InlineData("corruption")]
    [InlineData("bane-of-agony")]
    [InlineData("bane-of-doom")]
    [InlineData("siphon-life")]
    [InlineData("drain-life")]
    [InlineData("drain-soul")]
    [InlineData("wrack")]
    public void EachSupportedPeriodicOrChannelSpellReceivesExactRankedCritBonus(string spell)
    {
        decimal FirstCrit(int rank)
        {
            var run=new WarlockSimRunner().Run(Setup(spell,rank));
            return run.Run.Timeline.First(e=>e.Type==CombatEventType.Damage &&
                e.AbilityKey==spell && (e.IsPeriodic || e.IsChannelTick) &&
                e.ResultKey=="critical" && e.Amount>0m).Amount!.Value;
        }
        var baseDamage=FirstCrit(0);
        Assert.Equal(baseDamage*1.665m/1.5m,FirstCrit(1));
        Assert.Equal(baseDamage*1.835m/1.5m,FirstCrit(2));
        Assert.Equal(baseDamage*2m/1.5m,FirstCrit(3));
    }

    [Fact]
    public void InvalidFourthPandemicRankIsRejected()
    {
        Assert.Throws<ArgumentException>(()=>
            new WarlockSimRunner().Run(Setup("corruption",4)));
    }
}
