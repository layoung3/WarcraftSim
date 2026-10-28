using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Hunter;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167PHunterCrossSpecAspects
{
    [Fact]
    public void BothAspectsUseCurrentLevelSixtyCostAndMutuallyExclusiveAuras()
    {
        var beast=ForeverHunterSharedAbilities.CreateAspectOfTheBeast();
        var hawk=ForeverHunterSharedAbilities.CreateAspectOfTheHawk();
        Assert.Equal(110m,beast.ResourceCosts.Single().Amount);
        Assert.Equal(120m,hawk.ResourceCosts.Single().Amount);
        Assert.Equal("hunter-aspect-beast",beast.Effects.Single().AuraKey);
        Assert.Equal("hunter-aspect-hawk",hawk.Effects.Single().AuraKey);
        var bm=ForeverBeastMasteryHunterSpellbook.Hunter(
            new ForeverBeastMasteryHunterTalents(),new ForeverBeastMasteryHunterTuning());
        var feral=ForeverSurvivalHunterSpellbook.Create(new ForeverSurvivalHunterTalents(),
            new ForeverSurvivalHunterTuning());
        foreach(var book in new[]{bm,feral})
        {
            Assert.Contains(book,a=>a.Key=="aspect-of-the-beast");
            Assert.Contains(book,a=>a.Key=="aspect-of-the-hawk");
            Assert.Contains(book,a=>a.Key=="volley");
        }
    }

    [Fact]
    public void BeastMasteryHawkImprovesOwnerRangedAutoAttack()
    {
        BeastMasteryHunterSimRequest setup(bool hawk) => new() {
            DurationSeconds=14m,CaptureTimeline=true,PetEnabled=false,
            UseAspectOfTheHawk=hawk,UseAspectOfTheBeast=false,
            UseSummonHawk=false,UseRapidFire=false,
            UseAimedShot=false,UseMultiShot=false,UseArcaneShot=false,
            UseSerpentSting=false,UseMendPet=false,UseIntimidation=false
        };
        var old=new BeastMasteryHunterSimRunner().Run(setup(false));
        var buff=new BeastMasteryHunterSimRunner().Run(setup(true));
        Assert.Contains(buff.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="aspect-of-the-hawk");
        var oldAuto=old.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="auto-shot").Sum(e=>e.Amount.GetValueOrDefault());
        var newAuto=buff.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="auto-shot").Sum(e=>e.Amount.GetValueOrDefault());
        Assert.True(newAuto>oldAuto);
    }

    [Fact]
    public void SurvivalBeastBoostsMeleeWhileRetainingMeleeFirstBehavior()
    {
        SurvivalHunterSimRequest setup(bool beast) => new(){
            DurationSeconds=18m,CaptureTimeline=true,CombatMode="melee",
            UseAspectOfTheBeast=beast,
            UseRapidFire=false,UseAimedShot=false,UseMultiShot=false,
            UseFireTraps=false,UseFrostTraps=false,UseWingClip=false,
            ScriptedAvoidanceEverySeconds=null
        };
        var baseRun=new SurvivalHunterSimRunner().Run(setup(false));
        var buffed=new SurvivalHunterSimRunner().Run(setup(true));
        Assert.Contains(buffed.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="aspect-of-the-beast");
        Assert.True(buffed.MeleeDamage>baseRun.MeleeDamage);
        Assert.DoesNotContain(buffed.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="auto-shot" && e.Amount>0m);
    }
}
