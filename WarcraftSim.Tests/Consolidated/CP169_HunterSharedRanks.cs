using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Hunter;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp169HunterCrossSpecSpellRanks
{
    [Fact]
    public void SurvivalAndBeastMasteryUseSameForeverLevelSixtyShotRanks()
    {
        var s=ForeverSurvivalHunterSpellbook.Create(
            new ForeverSurvivalHunterTalents(),new ForeverSurvivalHunterTuning());
        var b=ForeverBeastMasteryHunterSpellbook.Hunter(
            new ForeverBeastMasteryHunterTalents(),new ForeverBeastMasteryHunterTuning());
        foreach(var spellbook in new[]{s,b})
        {
            var arcane=spellbook.Single(x=>x.Key=="arcane-shot");
            Assert.Equal(190m,arcane.ResourceCosts.Single().Amount);
            Assert.Equal(217m,arcane.Effects.Single().MinimumValue);
            var serpent=spellbook.Single(x=>x.Key=="serpent-sting");
            Assert.Equal(250m,serpent.ResourceCosts.Single().Amount);
            var dot=serpent.Effects.Single(x=>x.AuraKey=="serpent-sting");
            Assert.Equal(111m,dot.MinimumValue);
            Assert.Equal(555m,dot.MinimumValue*5m);
            Assert.Equal(15m,dot.DurationSeconds);
            var multi=spellbook.Single(x=>x.Key=="multi-shot");
            Assert.Equal("hunter-aimed-multi",multi.SharedCooldownGroupKey);
            Assert.Equal(6m,multi.SharedCooldownSeconds);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void BeastMasterySerpentApplicationRespectsHitAndNeverTicksAfterMiss(int hit)
    {
        var req=new BeastMasteryHunterSimRequest {
            PetEnabled=false,UseSummonHawk=false,
            UseSerpentSting=true,UseAimedShot=false,UseMultiShot=false,
            UseArcaneShot=false,UseRapidFire=false,UseBestialWrath=false,
            UseIntimidation=false,UseMendPet=false,
            DurationSeconds=17m,TargetMaximumHealth=1000000m,
            CaptureTimeline=true
        };
        req.Tuning.HunterHitPercent=hit;
        var run=new BeastMasteryHunterSimRunner().Run(req);
        var ticks=run.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="serpent-sting" && e.IsPeriodic &&
            e.Amount>0m).ToArray();
        if(hit==0)
        {
            Assert.Empty(ticks);
            Assert.DoesNotContain(run.Run.Timeline,e=>
                e.Type==CombatEventType.AuraApplied &&
                e.AbilityKey=="serpent-sting" && e.TargetActorKey=="target");
        }
        else
        {
            Assert.NotEmpty(ticks);
            Assert.Contains(run.Run.Timeline,e=>
                e.Type==CombatEventType.AuraApplied &&
                e.AbilityKey=="serpent-sting" && e.TargetActorKey=="target");
        }
        Assert.InRange(Math.Abs(run.DamageDone-run.DamageByAbility.Values.Sum()),0m,.000001m);
    }

    [Fact]
    public void MeleeFirstSurvivalStillExecutesCurrentRangedShotsWhenExplicitlyConfigured()
    {
        var req=new SurvivalHunterSimRequest {
            CombatMode="ranged",AdditionalTargets=0,
            UseFireTraps=false,UseFrostTraps=false,
            UseRapidFire=false,UseVolley=false,
            UseAimedShot=false,UseMultiShot=false,
            DurationSeconds=18m,CaptureTimeline=true
        };
        req.Tuning.HitChancePercent=100m;
        var run=new SurvivalHunterSimRunner().Run(req);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="arcane-shot" && e.Amount>0m);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="serpent-sting" && e.IsPeriodic && e.Amount>0m);
        Assert.InRange(Math.Abs(run.DamageDone-run.DamageByAbility.Values.Sum()),0m,.000001m);
    }
}
