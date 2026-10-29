using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Mage;
using WarcraftSim.Data.Forever.Warlock;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167JAreaSpells
{
    [Fact]
    public void FlamestrikeHasDirectDamageAndHitDependentGroundDamage()
    {
        var talent = ForeverMageTalentBuild.Default("fire");
        talent.Ranks["improved-flamestrike"] = 3;
        var spell = Assert.Single(ForeverMageDpsSpellbook.Create(talent,
            new ForeverMageTuning(),0m), a=>a.Key=="flamestrike");
        Assert.Equal(990m,spell.ResourceCosts.Single().Amount);
        Assert.Equal(3m,spell.CastTimeSeconds);
        var impact=Assert.Single(spell.Effects,e=>e.Key=="flamestrike-impact");
        var ground=Assert.Single(spell.Effects,e=>e.Key=="flamestrike-ground-tick");
        Assert.Equal(417m,impact.MinimumValue);
        Assert.Equal(15m,impact.BonusCriticalChancePercent);
        Assert.Equal(83m,ground.MinimumValue);
        Assert.Equal("flamestrike-impact",ground.DependsOnEffectKey);
        Assert.Equal(9,ground.MaxTargets);
        Assert.Equal(2m,ground.TickIntervalSeconds);
    }

    [Fact]
    public void FlamestrikeDealsPerEnemyDirectAndPeriodicDamageAndAccounts()
    {
        var req=MageSimRequest.CreatePreset("fire");
        req.DurationSeconds=12m;
        req.AdditionalTargets=2;
        req.BaseSpellHitPercent=100m;
        req.SpellCritPercent=0m;
        req.UseEvocation=false;
        req.UseMajorManaPotion=false;
        req.CombatItems.ManaRubyCharges=0;
        req.Talents.Ranks["improved-flamestrike"]=3;
        req.CustomPriority=["flamestrike"];
        var result=new MageSimRunner().Run(req);
        var events=result.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="flamestrike" && e.Amount>0m).ToList();
        foreach(var target in new[] {"target","target-1","target-2"})
        {
            Assert.Contains(events,e=>e.TargetActorKey==target && !e.IsPeriodic);
            Assert.Contains(events,e=>e.TargetActorKey==target && e.IsPeriodic);
        }
        Assert.InRange(Math.Abs(result.DamageDone-result.DamageByAbility.Values.Sum()),
            0m,.000001m);
    }

    [Fact]
    public void RainOfFireHasSixteenHundredYearKnownClientRankAndEightTicks()
    {
        var book=ForeverWarlockDpsSpellbook.Create("destruction",
            ForeverWarlockTalentBuild.Default("destruction"),new ForeverWarlockTuning(),
            0m,0m);
        var rain=Assert.Single(book,a=>a.Key=="rain-of-fire");
        Assert.Equal(1185m,rain.ResourceCosts.Single().Amount);
        Assert.Equal(8m,rain.ChannelDurationSeconds);
        Assert.Equal(1m,rain.ChannelTickIntervalSeconds);
        Assert.Equal(110m,rain.Effects.Single().MinimumValue);
        Assert.Equal(9,rain.Effects.Single().MaxTargets);
    }

    [Fact]
    public void RainOfFireTargetsConfiguredEnemiesAndReportsChannelEvents()
    {
        var req=WarlockSimRequest.CreatePreset("destruction");
        req.DurationSeconds=10m;
        req.AdditionalTargets=2;
        req.CaptureTimeline=true;
        req.EnablePet=false;
        req.UseLifeTap=false;
        req.BaseSpellHitPercent=100m;
        req.SpellCritPercent=0m;
        req.CustomPriority=["rain-of-fire"];
        var result=new WarlockSimRunner().Run(req);
        var pulses=result.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="rain-of-fire" && e.IsChannelTick && e.Amount is >0m).ToList();
        foreach(var target in new[] {"target","target-1","target-2"})
            Assert.Contains(pulses,e=>e.TargetActorKey==target && e.SchoolKey=="fire");
        Assert.InRange(Math.Abs(result.DamageDone-result.DamageByAbility.Values.Sum()),
            0m,.000001m);
    }
}
