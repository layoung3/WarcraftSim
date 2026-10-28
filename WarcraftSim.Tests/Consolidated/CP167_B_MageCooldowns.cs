using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Mage;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167BMageCooldowns
{
    [Fact]
    public void MageCooldownsAreRegisteredOnlyWithTalents()
    {
        var frost=ForeverMageTalentBuild.Default("frost");
        frost.Validate("frost");
        var spells=ForeverMageDpsSpellbook.Create(frost,new ForeverMageTuning(),200m);
        Assert.Equal(600m,Assert.Single(spells, s=>s.Key=="cold-snap").CooldownSeconds);
        Assert.DoesNotContain(spells,s=>s.Key=="combustion");

        var fire=ForeverMageTalentBuild.Default("fire");
        fire.Validate("fire");
        spells=ForeverMageDpsSpellbook.Create(fire,new ForeverMageTuning(),200m);
        Assert.Equal(180m,Assert.Single(spells,s=>s.Key=="combustion").CooldownSeconds);
        Assert.DoesNotContain(spells,s=>s.Key=="cold-snap");
    }

    [Fact]
    public void FrostColdSnapResetsExistingCooldownAndReportsCast()
    {
        var req=MageSimRequest.CreatePreset("frost");
        req.DurationSeconds=24m;
        req.BaseSpellHitPercent=100m;
        req.SpellCritPercent=0m;
        req.UseEvocation=false;
        req.UseMajorManaPotion=false;
        req.CombatItems.ManaRubyCharges=0;
        req.CaptureTimeline=true;
        req.CustomPriority=["cone-of-cold","cold-snap","frostbolt"];
        var result=new MageSimRunner().Run(req);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey=="cold-snap");
        Assert.True(result.Run.Timeline.Count(e=>e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey=="cone-of-cold")>=2);
    }

    [Fact]
    public void CombustionConsumesAfterThreeDirectFireCriticalStrikes()
    {
        var req=MageSimRequest.CreatePreset("fire");
        req.DurationSeconds=20m;
        req.BaseSpellHitPercent=100m;
        req.SpellCritPercent=100m;
        req.UseEvocation=false;
        req.UseMajorManaPotion=false;
        req.CombatItems.ManaRubyCharges=0;
        req.CaptureTimeline=true;
        req.CustomPriority=["combustion","scorch"];
        var result=new MageSimRunner().Run(req);
        Assert.Single(result.Run.Timeline.Where(e=>e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey=="combustion"));
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.AuraRemoved &&
            e.AbilityKey=="combustion");
        Assert.InRange(Math.Abs(result.DamageDone-result.DamageByAbility.Values.Sum()),0m,.000001m);
    }
}
