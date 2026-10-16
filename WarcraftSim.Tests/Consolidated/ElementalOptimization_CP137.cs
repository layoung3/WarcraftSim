using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Data.Forever.Shaman;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests;

public sealed class ElementalOptimizationCP137Tests
{
    [Theory]
    [InlineData("raid-sustain",31,7,13)]
    [InlineData("deep-elemental",41,10,0)]
    [InlineData("cleave-hybrid",38,13,0)]
    public void BuildPresetsHaveLegalFiftyOnePointTotals(string name,int e,int h,int r)
    {
        var t=ElementalPreRaidProfileCatalog.Talents(name);
        Assert.Equal(e,t.ElementalPointsSpent);
        Assert.Equal(h,t.EnhancementPointsSpent);
        Assert.Equal(r,t.RestorationPointsSpent);
        t.Validate();
    }

    [Theory]
    [InlineData("throughput")]
    [InlineData("sustain")]
    public void TwoGearProfilesHaveAllSixteenSlots(string gear)
    {
        var profile=ElementalPreRaidProfileCatalog.Character(gear);
        Assert.Equal(16,profile.Equipment.Items.Count);
        Assert.Equal(16,profile.Equipment.Items.Select(x=>x.SlotKey).Distinct().Count());
        Assert.Contains(profile.Equipment.Items,x=>x.SlotKey=="main-hand"&&x.Stats.Get("spell-power")>0m);
    }

    [Fact]
    public void DefaultsUseOctober16ClientSpellManaCosts()
    {
        var t=new ForeverElementalShamanTuning();
        Assert.Equal(220m,t.LightningBoltMana);
        Assert.Equal(315m,t.ChainLightningMana);
        Assert.Equal(196m,t.LightningBoltDamage);
    }

    [Fact]
    public void MasteryIsUsedOnlyWhenTheTalentExists()
    {
        var request=ElementalPreRaidProfileCatalog.Setup("throughput","raid-sustain");
        request.CaptureTimeline=true;
        var yes=new ElementalShamanSimRunner().Run(request);
        Assert.Equal(1,yes.ElementalMasteryCasts);
        Assert.Contains(yes.Run.Timeline,e=>e.AbilityKey=="elemental-mastery"&&
            e.Type==CombatEventType.AbilityCastCompleted);
        request.UseElementalMastery=false;
        var no=new ElementalShamanSimRunner().Run(request);
        Assert.Equal(0,no.ElementalMasteryCasts);
    }

    [Fact]
    public void SearingTotemHasRealDamageAndDoesNotSpamEveryGlobal()
    {
        var request=ElementalPreRaidProfileCatalog.Setup("throughput","raid-sustain");
        request.CaptureTimeline=true;
        var result=new ElementalShamanSimRunner().Run(request);
        Assert.True(result.DamageByAbility.GetValueOrDefault("searing-totem")>0m);
        var casts=result.Run.Timeline.Count(e=>e.AbilityKey=="searing-totem"&&
            e.Type==CombatEventType.AbilityCastCompleted);
        Assert.InRange(casts,1,4);
    }

    [Fact]
    public void CallOfFlameAddsNineSecondsToFlameShockAtThreeRanks()
    {
        var t=ElementalPreRaidProfileCatalog.Talents("raid-sustain");
        var spell=ForeverElementalShamanSpellbook.Create(t,new(),100m)
            .Single(a=>a.Key=="flame-shock");
        Assert.Equal(21m,spell.Effects.Single(e=>e.Key=="flame-shock-tick").DurationSeconds);
    }

    [Fact]
    public void FlameShockNeverOptionRemovesItFromAutoPriority()
    {
        var request=ElementalPreRaidProfileCatalog.Setup("throughput","raid-sustain");
        request.FlameShockPolicy="never";
        var rotation=ElementalAdaptiveRotation.Build(request,400m);
        Assert.DoesNotContain(rotation.Entries,e=>e.AbilityKey=="flame-shock");
    }

    [Fact]
    public void CustomPriorityOverridesAndRejectsUnknownAbilities()
    {
        var request=new ElementalShamanSimRequest{CustomPriority=["lightning-bolt"]};
        var rotation=ElementalAdaptiveRotation.Build(request,400m);
        Assert.Single(rotation.Entries);
        request.CustomPriority=["not-a-real-elemental-ability"];
        Assert.Throws<ArgumentException>(()=>ElementalAdaptiveRotation.Build(request,400m));
    }

    [Fact]
    public void ThreeTargetRotationAddsChainLightningAndReportsCleaveDamage()
    {
        var request=ElementalPreRaidProfileCatalog.Setup("throughput","cleave-hybrid");
        request.CaptureTimeline=false;
        request.TargetCount=3;
        var result=new ElementalShamanSimRunner().Run(request);
        Assert.Contains("chain-lightning",result.ResolvedRotation);
        Assert.True(result.Run.Summary.ActorSummaries.ContainsKey("target-2"));
        Assert.True(result.Run.Summary.ActorSummaries.ContainsKey("target-3"));
        Assert.True(result.DamageDone>=result.Run.Summary.ActorSummaries["target"].DamageTaken);
    }

    [Fact]
    public void ReportReconcilesEveryDamageAbilityWithUpdatedRules()
    {
        var request=ElementalPreRaidProfileCatalog.Setup("sustain","raid-sustain");
        var result=new ElementalShamanSimRunner().Run(request);
        Assert.InRange(Math.Abs(result.DamageDone-result.DamageByAbility.Values.Sum()),0m,.000001m);
    }
}
