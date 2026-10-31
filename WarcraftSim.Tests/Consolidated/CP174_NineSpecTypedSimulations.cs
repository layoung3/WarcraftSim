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

public sealed class Cp174NineSpecTypedSimulationContracts(ITestOutputHelper output)
{
    [Theory]
    [InlineData("mage","arcane")]
    [InlineData("mage","fire")]
    [InlineData("mage","frost")]
    [InlineData("warlock","affliction")]
    [InlineData("warlock","demonology")]
    [InlineData("warlock","destruction")]
    [InlineData("hunter","marksmanship")]
    [InlineData("hunter","survival")]
    [InlineData("hunter","beast-mastery")]
    public async Task SelectedConfigurationExecutesThroughUnifiedTypedEndpoint(string cls,string spec)
    {
        const int seed=174060;
        var request=new ForeverLevel60SimulationRequest {ClassKey=cls,Specialization=spec};
        switch(cls)
        {
            case "mage":
                request.Mage=MageSimRequest.CreatePreset(spec);
                request.Mage.Character.Race="human";
                request.Mage.UseProvisionalLevel60Baseline=true;
                request.Mage.DurationSeconds=16m;
                request.Mage.TargetMaximumHealth=1000000m;
                request.Mage.Seed=seed;
                break;
            case "warlock":
                request.Warlock=WarlockSimRequest.CreatePreset(spec);
                request.Warlock.Character.Race="orc";
                request.Warlock.UseProvisionalLevel60Baseline=true;
                request.Warlock.DurationSeconds=16m;
                request.Warlock.TargetMaximumHealth=1000000m;
                request.Warlock.Seed=seed;
                break;
            default:
                if(spec=="marksmanship")request.Marksmanship=new MarksmanshipHunterSimRequest {
                    Race="dwarf",UseProvisionalLevel60Baseline=true,DurationSeconds=16m,
                    TargetMaximumHealth=1000000m,Seed=seed
                };
                if(spec=="survival")request.Survival=new SurvivalHunterSimRequest {
                    Race="dwarf",UseProvisionalLevel60Baseline=true,DurationSeconds=16m,
                    TargetMaximumHealth=1000000m,Seed=seed,AdditionalTargets=0
                };
                if(spec=="beast-mastery")request.BeastMastery=new BeastMasteryHunterSimRequest {
                    Race="dwarf",UseProvisionalLevel60Baseline=true,DurationSeconds=16m,
                    TargetMaximumHealth=1000000m,Seed=seed,PetFamily="cat"
                };
                break;
        }
        Cp174ItemizedPresetFixture.Apply(request);
        var result=await new ForeverLevel60SimulationController().Simulate(request,CancellationToken.None);
        var ok=Assert.IsType<OkObjectResult>(result);
        using var doc=JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        Assert.Equal(cls,doc.RootElement.GetProperty("classKey").GetString());
        Assert.Equal(spec,doc.RootElement.GetProperty("specialization").GetString());
        var sim=doc.RootElement.GetProperty("sim");
        Assert.True(sim.GetProperty("Dps").GetDecimal()>=0m);
        Assert.True(sim.GetProperty("DamageDone").GetDecimal()>=0m);
        output.WriteLine($"{cls}/{spec}: DPS={sim.GetProperty("Dps").GetDecimal():0.####}; damage={sim.GetProperty("DamageDone").GetDecimal():0.####}");
    }
}
