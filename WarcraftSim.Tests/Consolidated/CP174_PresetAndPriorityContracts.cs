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

}
