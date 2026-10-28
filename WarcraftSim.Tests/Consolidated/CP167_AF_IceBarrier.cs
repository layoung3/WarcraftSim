using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Mage;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167AfIceBarrier
{
    private static MageSimRequest Frost(bool withBarrier=true)
    {
        var r=MageSimRequest.CreatePreset("frost");
        r.DurationSeconds=15m;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.UseEvocation=false;
        r.UseMajorManaPotion=false;
        r.CombatItems.ManaRubyCharges=0;
        r.CaptureTimeline=true;
        if(withBarrier) r.Talents.Ranks["ice-barrier"]=1;
        r.CustomPriority=["ice-barrier","frostbolt"];
        return r;
    }

    [Fact]
    public void IceBarrierIsLearnedOnlyWithTalentAndUsesCurrentRankFour()
    {
        var t=ForeverMageTalentBuild.Default("frost");
        Assert.DoesNotContain(ForeverMageDpsSpellbook.Create(t,new ForeverMageTuning(),0m),
            s=>s.Key=="ice-barrier");
        t.Ranks["ice-barrier"]=1;
        t.Validate("frost");
        var b=Assert.Single(ForeverMageDpsSpellbook.Create(t,new ForeverMageTuning(),0m),
            s=>s.Key=="ice-barrier");
        Assert.Equal(480m,Assert.Single(b.ResourceCosts).Amount);
        Assert.Equal(30m,b.CooldownSeconds);
        Assert.Equal(811m,Assert.Single(b.Effects).MinimumValue);
        Assert.Equal(60m,Assert.Single(b.Effects).DurationSeconds);
        Assert.Equal("mage-ice-barrier",Assert.Single(b.Effects).AbsorbKey);
    }

    [Fact]
    public void BarrierAppliesRealAbsorbRatherThanOnlyDescriptiveAura()
    {
        var run=new MageSimRunner().Run(Frost());
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.AbsorbApplied &&
            e.AbilityKey=="ice-barrier" && e.Amount==811m &&
            e.TargetActorKey=="mage");
        Assert.InRange(Math.Abs(run.DamageDone-run.DamageByAbility.Values.Sum()),0m,.000001m);
    }

    [Fact]
    public void BarrierCannotBeCastWithoutTalent()
    {
        Assert.Throws<ArgumentException>(()=>new MageSimRunner().Run(Frost(false)));
    }

    [Fact]
    public void ColdSnapResetsIceBarrierCooldown()
    {
        var request=Frost();
        request.DurationSeconds=12m;
        request.CustomPriority=["cone-of-cold","ice-barrier","cold-snap","frostbolt"];
        var run=new MageSimRunner().Run(request);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey=="cold-snap");
        Assert.True(run.Run.Timeline.Count(e=>e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey=="ice-barrier")>=2);
    }

    [Fact]
    public void BarrierRequiresColdSnapPrerequisite()
    {
        var t=ForeverMageTalentBuild.Default("frost");
        t.Ranks["ice-barrier"]=1;
        t.Ranks.Remove("cold-snap");
        Assert.Throws<ArgumentException>(()=>t.Validate("frost"));
    }
}
