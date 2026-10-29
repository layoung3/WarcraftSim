using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Warlock;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167AcWarlockDoomCoil
{
    [Fact]
    public void CurrentBaneOfDoomAndDeathCoilHaveVerifiedLevel60Ranks()
    {
        var talents=ForeverWarlockTalentBuild.Default("affliction");
        // Check base tooltip ranks independently of Shadow Mastery scaling.
        talents.Ranks["shadow-mastery"]=0;
        var spellbook=ForeverWarlockDpsSpellbook.Create("affliction",
            talents,new ForeverWarlockTuning(),0m,0m);
        var coil=Assert.Single(spellbook,x=>x.Key=="death-coil");
        Assert.Equal(600m,coil.ResourceCosts.Single().Amount);
        Assert.Equal(120m,coil.CooldownSeconds);
        Assert.Equal(454m,Assert.Single(coil.Effects,x=>x.Key=="death-coil-impact").MinimumValue);
        Assert.Equal(3m,Assert.Single(coil.Effects,x=>x.Key=="death-coil-horror").DurationSeconds);
        var doom=Assert.Single(spellbook,x=>x.Key=="bane-of-doom");
        Assert.Equal(300m,doom.ResourceCosts.Single().Amount);
        Assert.Equal(60m,doom.CooldownSeconds);
        var delayed=Assert.Single(doom.Effects,x=>x.Key=="bane-of-doom-tick");
        Assert.Equal(60m,delayed.DurationSeconds);
        Assert.Equal(60m,delayed.TickIntervalSeconds);
        Assert.Equal("bane-of-doom-application",delayed.DependsOnEffectKey);
    }

    private static WarlockSimRequest Request(params string[] keys)
    {
        var req=WarlockSimRequest.CreatePreset("affliction");
        req.TargetMaximumHealth=1000000m;
        req.EnablePet=false;
        req.PrePullSacrificedDemon=null;
        req.UseLifeTap=false;
        req.BaseSpellHitPercent=100m;
        req.SpellCritPercent=0m;
        req.DurationSeconds=66m;
        req.CaptureTimeline=true;
        req.CustomPriority=keys.ToList();
        return req;
    }

    [Fact]
    public void DelayedDoomTicksAtSixtySecondsAndReportsDamage()
    {
        var run=new WarlockSimRunner().Run(Request("bane-of-doom"));
        var ticks=run.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="bane-of-doom" && e.IsPeriodic && e.Amount is >0m).ToList();
        Assert.Single(ticks);
        Assert.Equal(60m,ticks[0].TimeSeconds);
        Assert.InRange(Math.Abs(run.DamageDone-run.DamageByAbility.Values.Sum()),0m,.000001m);
    }

    [Fact]
    public void DeathCoilHealsFromLandedShadowDamageAndAppliesFearAura()
    {
        var req=Request("death-coil");
        req.DurationSeconds=7m;
        var run=new WarlockSimRunner().Run(req);
        var hit=Assert.Single(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="death-coil" && e.Amount is >0m);
        var heal=Assert.Single(run.Run.Timeline,e=>e.Type==CombatEventType.Healing &&
            e.AbilityKey=="death-coil");
        Assert.Equal(hit.Amount,heal.RawAmount);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="death-coil" && e.EffectKey=="death-coil-horror");
    }

    [Fact]
    public void MissedDeathCoilCannotHealOrApplyHorror()
    {
        var req=Request("death-coil");
        req.DurationSeconds=7m;
        req.BaseSpellHitPercent=0m;
        var run=new WarlockSimRunner().Run(req);
        Assert.DoesNotContain(run.Run.Timeline,e=>e.Type==CombatEventType.Healing &&
            e.AbilityKey=="death-coil");
        Assert.DoesNotContain(run.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="death-coil" && e.EffectKey=="death-coil-horror");
    }

    [Fact]
    public void ReplacingBaneOfDoomWithAgonyRemovesDoomAuraAndCancelsPulse()
    {
        var req=Request("bane-of-doom","bane-of-agony");
        var run=new WarlockSimRunner().Run(req);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.AuraRemoved &&
            e.AbilityKey=="bane-of-doom");
        Assert.DoesNotContain(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="bane-of-doom" && e.IsPeriodic);
        Assert.InRange(Math.Abs(run.DamageDone-run.DamageByAbility.Values.Sum()),0m,.000001m);
    }
}
