using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Mage;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167FMageArcaneCooldowns
{
    [Fact]
    public void ArcanePowerRequiresPresenceOfMind()
    {
        var talents=ForeverMageTalentBuild.Default("arcane");
        talents.Ranks.Remove("presence-of-mind");
        Assert.Throws<ArgumentException>(()=>talents.Validate("arcane"));
        talents.Ranks["presence-of-mind"]=1;
        talents.Validate("arcane");
        var book=ForeverMageDpsSpellbook.Create(talents,new ForeverMageTuning(),0m);
        Assert.Equal(180m,Assert.Single(book,a=>a.Key=="presence-of-mind").CooldownSeconds);
        Assert.Equal(180m,Assert.Single(book,a=>a.Key=="arcane-power").CooldownSeconds);
        Assert.Equal(15m,Assert.Single(book,a=>a.Key=="arcane-power")
            .Effects.Single().DurationSeconds);
    }

    private static MageSimRequest Setup(params string[] actions)
    {
        var req=MageSimRequest.CreatePreset("arcane");
        req.DurationSeconds=25m;
        req.CaptureTimeline=true;
        req.BaseSpellHitPercent=100m;
        req.SpellCritPercent=0m;
        req.UseMissileBarrage=false;
        req.UseEvocation=false;
        req.UseMajorManaPotion=false;
        req.CombatItems.ManaRubyCharges=0;
        req.Talents.Ranks["arcane-concentration"]=0;
        req.Talents.Ranks["arcane-meditation"]=0;
        req.Talents.Ranks["arcane-instability"]=0;
        req.Talents.Ranks["arcane-impact"]=0;
        req.CustomPriority=actions.ToList();
        return req;
    }

    [Fact]
    public void PresenceOfMindGivesExactlyOneInstantHardCast()
    {
        var sim=new MageSimRunner().Run(Setup("presence-of-mind","fireball"));
        var started=sim.Run.Timeline.Where(e=>e.Type==CombatEventType.AbilityCastStarted &&
            e.AbilityKey=="fireball").ToList();
        var completed=sim.Run.Timeline.Where(e=>e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey=="fireball").ToList();
        Assert.True(started.Count>=3);
        Assert.True(completed.Count>=2);
        Assert.Equal(started[0].TimeSeconds,completed[0].TimeSeconds);
        Assert.True(completed[1].TimeSeconds>started[1].TimeSeconds);
        Assert.Single(sim.Run.Timeline,e=>e.Type==CombatEventType.AuraRemoved &&
            e.AbilityKey=="presence-of-mind");
    }

    [Fact]
    public void ArcanePowerIncreasesManaCostAndDamageAndExpires()
    {
        var baseline=new MageSimRunner().Run(Setup("fireball"));
        var powered=new MageSimRunner().Run(Setup("arcane-power","fireball"));
        var paidBase=baseline.Run.Timeline.First(e=>e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="fireball" && e.ResourceKey=="mana" && e.Amount<0m);
        var paidPower=powered.Run.Timeline.First(e=>e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="fireball" && e.ResourceKey=="mana" && e.Amount<0m);
        Assert.Equal(paidBase.Amount!.Value*1.3m,paidPower.Amount!.Value);
        var baseHit=baseline.Run.Timeline.First(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="fireball" && e.Amount>0m);
        var powerHit=powered.Run.Timeline.First(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="fireball" && e.Amount>0m);
        Assert.Equal(baseHit.Amount!.Value*1.3m,powerHit.Amount!.Value);
        Assert.Contains(powered.Run.Timeline,e=>e.Type==CombatEventType.AuraRemoved &&
            e.AbilityKey=="arcane-power");
        Assert.InRange(Math.Abs(powered.DamageDone-powered.DamageByAbility.Values.Sum()),0m,.000001m);
    }

    [Fact]
    public void ClearcastingRemainsFreeDuringArcanePower()
    {
        var req=Setup("arcane-power","fireball");
        req.DurationSeconds=80m;
        req.Talents.Ranks["arcane-concentration"]=5;
        req.Talents.Ranks["arcane-meditation"]=3;
        var run=new MageSimRunner().Run(req);
        Assert.True(run.ClearcastingProcs>0);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="fireball" && e.ResourceKey=="mana" && e.Amount==0m);
    }
}
