using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using Xunit;
using Xunit.Abstractions;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp173Level60SimulationApi(ITestOutputHelper output)
{
    [Fact]
    public async Task EndToEndTypedMageProvisionalBuildUsesSelectedTalentsAndReportsSource()
    {
        var mage=MageSimRequest.CreatePreset("frost");
        mage.Character.Race="human";
        mage.UseProvisionalLevel60Baseline=true;
        mage.DurationSeconds=24m;
        mage.TargetMaximumHealth=3000000m;
        mage.BaseSpellHitPercent=100m;
        mage.ExternalModifiers.Consumables.Add("flask-of-supreme-power");
        mage.ExternalModifiers.TargetDebuffs.Add("curse-of-elements");
        var request=new ForeverLevel60SimulationRequest {
            ClassKey="mage",Specialization="frost",Mage=mage
        };
        var result=Assert.IsType<OkObjectResult>(await
            new ForeverLevel60SimulationController().Simulate(request,CancellationToken.None));
        using var doc=JsonDocument.Parse(JsonSerializer.Serialize(result.Value));
        var root=doc.RootElement;
        Assert.Equal("provisional-documented-proxy",root.GetProperty("baseline")
            .GetProperty("VerificationStatus").GetString());
        Assert.Equal("frost",root.GetProperty("specialization").GetString());
        Assert.True(root.GetProperty("sim").GetProperty("Dps").GetDecimal()>0m);
        output.WriteLine($"CP173 typed API frost Mage DPS {root.GetProperty("sim").GetProperty("Dps").GetDecimal():0.####}");
    }

    [Fact]
    public async Task TypedWarlockAndHunterRequireExplicitBaselineAndMatchSpecialization()
    {
        var ctrl=new ForeverLevel60SimulationController();
        var warlock=WarlockSimRequest.CreatePreset("affliction");
        warlock.Character.Race="undead";
        warlock.UseProvisionalLevel60Baseline=true;
        warlock.DurationSeconds=20m;
        warlock.TargetMaximumHealth=1000000m;
        var ok=Assert.IsType<OkObjectResult>(await ctrl.Simulate(
            new ForeverLevel60SimulationRequest {
                ClassKey="warlock",Specialization="affliction",Warlock=warlock
            },CancellationToken.None));
        Assert.NotNull(ok.Value);
        var mm=new MarksmanshipHunterSimRequest {
            Race="dwarf",UseProvisionalLevel60Baseline=true,
            DurationSeconds=20m,TargetMaximumHealth=1000000m,HasPet=false,
            UseAimedShot=false,UseSniperShot=false,UseMultiShot=false,
            UseArcaneShot=false,UseSerpentSting=false,UseRapidFire=false
        };
        var hunter=Assert.IsType<OkObjectResult>(await ctrl.Simulate(
            new ForeverLevel60SimulationRequest{
                ClassKey="hunter",Specialization="marksmanship",Marksmanship=mm
            },CancellationToken.None));
        Assert.NotNull(hunter.Value);
        mm.UseProvisionalLevel60Baseline=false;
        var strict=await ctrl.Simulate(new ForeverLevel60SimulationRequest {
            ClassKey="hunter",Specialization="marksmanship",Marksmanship=mm
        },CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(strict);
        var mismatch=await ctrl.Simulate(new ForeverLevel60SimulationRequest {
            ClassKey="mage",Specialization="fire",Warlock=warlock
        },CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(mismatch);
    }

    [Fact]
    public void ClassBuilderReportsExactlyFortySixHistoricalProxiesAndTenUnsupportedSkyborne()
    {
        var ctrl=new ForeverCharacterController();
        var ok=Assert.IsType<OkObjectResult>(ctrl.Level60BaselineCoverage());
        using var doc=JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        Assert.Equal(56,doc.RootElement.GetProperty("totalCombinations").GetInt32());
        Assert.Equal(46,doc.RootElement.GetProperty("provisionalBaselineCount").GetInt32());
        Assert.Equal(0,doc.RootElement.GetProperty("verifiedBaselineCount").GetInt32());
        var unsupported=0;
        foreach(var row in doc.RootElement.GetProperty("combinations").EnumerateArray())
            if(row.GetProperty("status").GetString()=="needs-forever-observation")
                unsupported++;
        Assert.Equal(10,unsupported);
        Assert.IsType<BadRequestObjectResult>(ctrl.BaseStats(
            "high-order-skyborne","mage",60,provisional:true));
        var baseResult=Assert.IsType<OkObjectResult>(ctrl.BaseStats(
            "human","mage",60,provisional:true));
        Assert.Contains("provisional-documented-proxy",JsonSerializer.Serialize(baseResult.Value));
    }
}
