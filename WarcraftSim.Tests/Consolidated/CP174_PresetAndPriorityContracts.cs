using System;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp174PresetAndPriorityContracts
{
    [Fact]
    public void BothSkyborneVariantsRemainDistinctAndFailClosedWithoutBaseline()
    {
        var races=ForeverPlayableRaceCatalog.Races;
        var alliance=Assert.Single(races,x=>x.Key=="high-order-skyborne");
        var horde=Assert.Single(races,x=>x.Key=="windshaper-skyborne");
        Assert.Equal("alliance",alliance.Faction);
        Assert.Equal("horde",horde.Faction);
        Assert.Contains("hunter",alliance.Classes);
        Assert.Contains("hunter",horde.Classes);
        Assert.DoesNotContain("mage",horde.Classes);
        Assert.IsType<BadRequestObjectResult>(
            new ForeverCharacterController().BaseStats("windshaper-skyborne","hunter",60,true));
    }

    [Fact]
    public void MagePriorityPreviewReflectsActualTalentAndRejectsUnlearnedSpell()
    {
        var ctrl=new ForeverSoloRotationController();
        var fire=MageSimRequest.CreatePreset("fire");
        var request=new ForeverLevel60SimulationRequest{ClassKey="mage",Specialization="fire",Mage=fire};
        var success=Assert.IsType<OkObjectResult>(ctrl.Preview(request));
        var json=JsonSerializer.Serialize(success.Value);
        Assert.Contains("fireball",json,StringComparison.OrdinalIgnoreCase);
        fire.CustomPriority=["arcane-power"];
        Assert.IsType<BadRequestObjectResult>(ctrl.Preview(request));
    }

    [Fact]
    public void WarlockCustomPriorityIsAcceptedByActualEnginePreview()
    {
        var ctrl=new ForeverSoloRotationController();
        var warlock=WarlockSimRequest.CreatePreset("affliction");
        warlock.CustomPriority=["corruption","shadow-bolt"];
        var result=Assert.IsType<OkObjectResult>(ctrl.Preview(
            new ForeverLevel60SimulationRequest{ClassKey="warlock",Specialization="affliction",Warlock=warlock}));
        var json=JsonSerializer.Serialize(result.Value);
        Assert.Contains("corruption",json,StringComparison.OrdinalIgnoreCase);
        Assert.Contains("custom",json,StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public void MageCustomPriorityExecutesSelectedAbilityInsteadOfDefaultFiller()
    {
        var request=MageSimRequest.CreatePreset("fire");
        request.Character.Race="human";
        request.UseProvisionalLevel60Baseline=true;
        request.DurationSeconds=30m;
        request.TargetMaximumHealth=1000000m;
        request.Seed=174600;
        request.CustomPriority=["frostbolt"];
        var frost=new MageSimRunner().Run(request,System.Threading.CancellationToken.None);
        Assert.True(frost.DamageDone>0m);
        Assert.Contains(frost.DamageByAbility.Keys,x=>x.Contains("frostbolt",StringComparison.OrdinalIgnoreCase));
        request.CustomPriority=["fireball"];
        var fire=new MageSimRunner().Run(request,System.Threading.CancellationToken.None);
        Assert.True(fire.DamageDone>0m);
        Assert.Contains(fire.DamageByAbility.Keys,x=>x.Contains("fireball",StringComparison.OrdinalIgnoreCase));
        Assert.NotEqual(frost.DamageByAbility,fire.DamageByAbility);
    }

    [Fact]
    public void FireMagePresetWithDominantFrostAllocationUsesExecutableFrostboltDefault()
    {
        var mage=MageSimRequest.CreatePreset("fire");
        mage.Talents.FirePointsSpent=10;
        mage.Talents.FrostPointsSpent=41;
        mage.Talents.ArcanePointsSpent=0;
        mage.Talents.Ranks.Clear();
        mage.UseFrostfireBolt=false;
        mage.RotationPolicy="adaptive";
        var rotation=MageAdaptiveRotation.Build(mage);
        Assert.Contains(rotation.Entries,x=>x.AbilityKey=="frostbolt");
        Assert.DoesNotContain(rotation.Entries,x=>x.AbilityKey=="fireball");
        mage.CustomPriority=["fireball"];
        var custom=MageAdaptiveRotation.Build(mage);
        Assert.Contains(custom.Entries,x=>x.AbilityKey=="fireball");
        Assert.DoesNotContain(custom.Entries,x=>x.AbilityKey=="frostbolt");
    }

    [Fact]
    public void FireMageWithFrostDominantAllocationActuallyCastsFrostbolt()
    {
        var mage=MageSimRequest.CreatePreset("fire");
        mage.Character.Race="human";
        mage.UseProvisionalLevel60Baseline=true;
        mage.DurationSeconds=35m;
        mage.TargetMaximumHealth=1000000m;
        mage.Seed=174602;
        mage.Talents.ArcanePointsSpent=0;
        mage.Talents.FirePointsSpent=10;
        mage.Talents.FrostPointsSpent=41;
        mage.Talents.Ranks.Clear();
        mage.CustomPriority.Clear();
        var sim=new MageSimRunner().Run(mage,System.Threading.CancellationToken.None);
        Assert.Contains(sim.DamageByAbility.Keys,key=>
            key.Contains("frostbolt",StringComparison.OrdinalIgnoreCase));
        Assert.True(sim.Dps>0m);
    }

    [Fact]
    public void AfflictionPresetWithValidDestructionTalentsExecutesIncinerateCustomPriority()
    {
        var warlock=WarlockSimRequest.CreatePreset("affliction");
        warlock.Character.Race="orc";
        warlock.UseProvisionalLevel60Baseline=true;
        warlock.DurationSeconds=40m;
        warlock.Seed=174603;
        warlock.TargetMaximumHealth=1000000m;
        warlock.Talents=WarcraftSim.Data.Forever.Warlock.ForeverWarlockTalentBuild.Default("destruction");
        warlock.UseImmolate=true;
        warlock.UseIncinerate=true;
        warlock.CustomPriority=["immolate","incinerate","shadow-bolt"];
        var custom=new WarlockSimRunner().Run(warlock,System.Threading.CancellationToken.None);
        Assert.True(custom.DamageDone>0m);
        Assert.Contains(custom.DamageByAbility.Keys,key=>
            key.Contains("incinerate",StringComparison.OrdinalIgnoreCase));
        warlock.CustomPriority=["shadow-bolt"];
        var shadow=new WarlockSimRunner().Run(warlock,System.Threading.CancellationToken.None);
        Assert.DoesNotContain(shadow.DamageByAbility.Keys,key=>
            key.Contains("incinerate",StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EditingSupportedMageRecoveryThresholdChangesEngineOwnedCondition()
    {
        var mage=MageSimRequest.CreatePreset("arcane");
        mage.MajorManaPotionBelowManaPercent=37m;
        mage.EvocationBelowManaPercent=18m;
        var profile=MageAdaptiveRotation.Build(mage);
        var potion=Assert.Single(profile.Entries,x=>x.AbilityKey=="major-mana-potion");
        var evocation=Assert.Single(profile.Entries,x=>x.AbilityKey=="evocation");
        Assert.Equal(37m,Assert.Single(potion.Conditions).Value);
        Assert.Equal(18m,Assert.Single(evocation.Conditions).Value);
    }

}
