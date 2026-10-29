using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Mage;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167XMagePyroblastRank
{
    [Fact]
    public void CurrentRankEightPyroblastIncludesHitDependentPeriodicEffect()
    {
        var book=ForeverMageDpsSpellbook.Create(ForeverMageTalentBuild.Default("fire"),
            new ForeverMageTuning(),0m);
        var pyro=Assert.Single(book,a=>a.Key=="pyroblast");
        Assert.Equal(440m,pyro.ResourceCosts.Single().Amount);
        Assert.Equal(6m,pyro.CastTimeSeconds);
        var impact=Assert.Single(pyro.Effects,e=>e.Key=="pyroblast-impact");
        var dot=Assert.Single(pyro.Effects,e=>e.Key=="pyroblast-dot-tick");
        Assert.Equal(583m,impact.MinimumValue);
        Assert.Equal(53m,dot.MinimumValue);
        Assert.Equal(12m,dot.DurationSeconds);
        Assert.Equal(3m,dot.TickIntervalSeconds);
        Assert.Equal("pyroblast-impact",dot.DependsOnEffectKey);
    }

    private static MageSimRequest Request(decimal hit)
    {
        var req=MageSimRequest.CreatePreset("fire");
        req.DurationSeconds=22m;
        req.BaseSpellHitPercent=hit;
        req.SpellCritPercent=0m;
        req.CaptureTimeline=true;
        req.UseEvocation=false;
        req.UseMajorManaPotion=false;
        req.CombatItems.ManaRubyCharges=0;
        req.CustomPriority=["pyroblast"];
        return req;
    }

    [Fact]
    public void LandedPyroblastDealsReportedDirectAndPeriodicDamage()
    {
        var run=new MageSimRunner().Run(Request(100m));
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="pyroblast" && !e.IsPeriodic && e.Amount>0m);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="pyroblast" && e.IsPeriodic && e.Amount>0m);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="pyroblast" && e.EffectKey=="pyroblast-dot-tick");
        Assert.InRange(Math.Abs(run.DamageDone-run.DamageByAbility.Values.Sum()),0m,.000001m);
    }

    [Fact]
    public void MissedPyroblastCannotApplyOrTickItsDot()
    {
        var run=new MageSimRunner().Run(Request(0m));
        Assert.DoesNotContain(run.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="pyroblast" && e.EffectKey=="pyroblast-dot-tick");
        Assert.DoesNotContain(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="pyroblast" && e.IsPeriodic);
    }
}
