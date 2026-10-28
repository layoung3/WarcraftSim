using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Warlock;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167AxCurseAndBaneOwnership
{
    private static WarlockSimRequest Setup(params string[] priority)
    {
        var r=WarlockSimRequest.CreatePreset("demonology");
        r.EnablePet=true;
        r.PetType="succubus";
        r.PrePullSacrificedDemon=null;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.PetMeleeHitPercent=100m;
        r.TargetArmor=4000m;
        r.TargetMaximumHealth=1000000m;
        r.UseLifeTap=false;
        r.DurationSeconds=12m;
        r.CaptureTimeline=true;
        r.CustomPriority=priority.ToList();
        return r;
    }

    [Fact]
    public void CurrentRankFourRecklessnessReducesActualTargetArmorButNotBelowZero()
    {
        var book=ForeverWarlockDpsSpellbook.Create("demonology",
            ForeverWarlockTalentBuild.Default("demonology"),
            new ForeverWarlockTuning(),0m,0m);
        var spell=Assert.Single(book,x=>x.Key=="curse-of-recklessness");
        Assert.Equal(115m,Assert.Single(spell.ResourceCosts).Amount);
        Assert.Equal(120m,Assert.Single(spell.Effects).DurationSeconds);
        var req=Setup("curse-of-recklessness","shadow-bolt");
        var with=new WarlockSimRunner().Run(req);
        Assert.Equal(3495m,with.Run.CustomMetrics["warlock-target-armor-at-end"]);
        req.TargetArmor=300m;
        var floor=new WarlockSimRunner().Run(req);
        Assert.Equal(0m,floor.Run.CustomMetrics["warlock-target-armor-at-end"]);
    }

    [Fact]
    public void RecklessnessImprovesActiveDemonPhysicalAttackMitigation()
    {
        var without=new WarlockSimRunner().Run(Setup("shadow-bolt"));
        var with=new WarlockSimRunner().Run(Setup("curse-of-recklessness","shadow-bolt"));
        decimal Ratio(WarlockSimResponse r)
        {
            var e=r.Run.Timeline.First(x=>x.Type==CombatEventType.Damage &&
                x.AbilityKey=="pet-melee" && x.Amount is >0m &&
                x.RawAmount is >0m);
            return e.Amount!.Value/e.RawAmount!.Value;
        }
        Assert.True(Ratio(with)>Ratio(without));
    }

    [Fact]
    public void CurseOfElementsAndRecklessnessReplaceSameOwnerWithoutRemovingBane()
    {
        var r=Setup("curse-of-the-elements","curse-of-recklessness","shadow-bolt");
        r.DurationSeconds=6m;
        var switched=new WarlockSimRunner().Run(r);
        Assert.True(switched.Run.CustomMetrics["warlock-curse-replacements"]>0m);
        Assert.Contains(switched.Run.Timeline,e=>e.Type==CombatEventType.AuraRemoved &&
            e.AbilityKey is ("curse-of-the-elements" or "curse-of-recklessness"));

        var bane=Setup("curse-of-recklessness","bane-of-agony","shadow-bolt");
        bane.DurationSeconds=6m;
        var together=new WarlockSimRunner().Run(bane);
        Assert.Equal(3495m,together.Run.CustomMetrics["warlock-target-armor-at-end"]);
        Assert.Contains(together.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="bane-of-agony");
    }

    [Fact]
    public void MissedRecklessnessNeverReducesArmor()
    {
        var r=Setup("curse-of-recklessness","shadow-bolt");
        r.BaseSpellHitPercent=0m;
        var run=new WarlockSimRunner().Run(r);
        Assert.Equal(4000m,run.Run.CustomMetrics["warlock-target-armor-at-end"]);
        Assert.DoesNotContain(run.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="curse-of-recklessness");
    }
}
