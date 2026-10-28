using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Warlock;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167AwAgonyRankAndRamp
{
    [Fact]
    public void ForeverLevelSixtyAgonyUsesCurrentBaseRankSixTotalsAndMana()
    {
        var t=ForeverWarlockTalentBuild.Default("affliction");
        t.Ranks["shadow-mastery"]=0;
        var spell=Assert.Single(ForeverWarlockDpsSpellbook.Create(
            "affliction",t,new ForeverWarlockTuning(),0m,0m),
            x=>x.Key=="bane-of-agony");
        Assert.Equal(215m,Assert.Single(spell.ResourceCosts).Amount);
        Assert.Equal(46m,Assert.Single(spell.Effects).MinimumValue);
        Assert.Equal(24m,Assert.Single(spell.Effects).DurationSeconds);
        Assert.Equal(2m,Assert.Single(spell.Effects).TickIntervalSeconds);
        Assert.Equal(552m,Enumerable.Range(1,12)
            .Sum(i=>46m*ForeverWarlockAgonyRamp.FactorForTick(i)));
    }

    [Fact]
    public void LandedAgonyHasThreeRealTickStagesWithoutFlatEarlyDamage()
    {
        var r=WarlockSimRequest.CreatePreset("affliction");
        r.Talents.Ranks["shadow-mastery"]=0;
        r.Talents.Ranks["malevolence"]=0;
        r.Character.BaseStats.Values["spell-power"]=0m;
        r.EnablePet=false;
        r.PrePullSacrificedDemon=null;
        r.UseLifeTap=false;
        r.DurationSeconds=24.5m;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.CustomPriority=["bane-of-agony"];
        r.TargetMaximumHealth=1000000m;
        r.CaptureTimeline=true;
        var result=new WarlockSimRunner().Run(r);
        var ticks=result.Run.Timeline.Where(e=>
            e.Type==CombatEventType.Damage && e.AbilityKey=="bane-of-agony" &&
            e.IsPeriodic && e.Amount is >0m).OrderBy(e=>e.TimeSeconds).ToArray();
        Assert.Equal(12,ticks.Length);
        // An inherited hit bonus or owner school buff would scale each
        // stage equally. Exact 70532 stage proportions remain provisional.
        Assert.Equal(ticks[0].Amount,ticks[3].Amount);
        Assert.Equal(ticks[4].Amount,ticks[7].Amount);
        Assert.Equal(ticks[8].Amount,ticks[11].Amount);
        Assert.Equal(ticks[0].Amount!.Value*2m,ticks[4].Amount);
        Assert.Equal(ticks[4].Amount!.Value*1.5m,ticks[8].Amount);
        Assert.InRange(Math.Abs(result.DamageDone-result.DamageByAbility.Values.Sum()),
            0m,.000001m);
    }

    [Theory]
    [InlineData(1,0.5)]
    [InlineData(5,1)]
    [InlineData(9,1.5)]
    public void ClassicRampingProfileIsExplicitlyParameterizedByTickIndex(
        int index,double expected)
    {
        Assert.Equal((decimal)expected,ForeverWarlockAgonyRamp.FactorForTick(index));
    }
}
