using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>Actual repeated seeded simulations, not invented distribution samples.</summary>
public sealed class Cp175SharedIterationsTests
{
    private static ForeverLevel60SimulationRequest Mage()
    {
        var mage=MageSimRequest.CreatePreset("fire");
        mage.Character.Race="human";
        mage.UseProvisionalLevel60Baseline=true;
        mage.DurationSeconds=9m;
        mage.TargetMaximumHealth=2000000m;
        mage.Seed=175010;
        mage.CaptureTimeline=false;
        return Cp174ItemizedPresetFixture.Apply(new ForeverLevel60SimulationRequest{
            ClassKey="mage",Specialization="fire",Mage=mage
        });
    }

    private static JsonElement Root(object? result)=>
        JsonSerializer.SerializeToElement(result,new JsonSerializerOptions(JsonSerializerDefaults.Web));

    [Fact]
    public async Task TwoRealMageSeedsHaveAnActualRepresentativeAndExactReproducibleOutput()
    {
        var controller=new ForeverSharedIterationsController();
        var request=new ForeverSharedIterationsController.IterationRequest{
            Configuration=Mage(),Iterations=2
        };
        var first=Assert.IsType<OkObjectResult>(
            await controller.SimulateIterations(request,CancellationToken.None));
        var a=Root(first.Value);
        Assert.Equal(2,a.GetProperty("iterations").GetInt32());
        Assert.True(a.GetProperty("hasStatisticalDistribution").GetBoolean());
        var samples=a.GetProperty("samples").EnumerateArray().ToArray();
        Assert.Equal(175010,samples[0].GetProperty("seed").GetInt32());
        Assert.Equal(175011,samples[1].GetProperty("seed").GetInt32());
        var dps=a.GetProperty("dps");
        Assert.Equal(2,dps.GetProperty("count").GetInt32());
        Assert.InRange(dps.GetProperty("mean").GetDecimal(),
            dps.GetProperty("minimum").GetDecimal(),dps.GetProperty("maximum").GetDecimal());
        Assert.InRange(dps.GetProperty("percentile99").GetDecimal(),
            dps.GetProperty("minimum").GetDecimal(),dps.GetProperty("maximum").GetDecimal());
        Assert.NotEmpty(a.GetProperty("representative").GetProperty("sim")
            .GetProperty("run").GetProperty("timeline").EnumerateArray());
        Assert.Equal(175010,request.Configuration!.Mage!.Seed); // not mutated
        var second=Assert.IsType<OkObjectResult>(
            await controller.SimulateIterations(request,CancellationToken.None));
        var b=Root(second.Value);
        Assert.Equal(a.GetProperty("samples").ToString(),b.GetProperty("samples").ToString());
        Assert.Equal(a.GetProperty("representativeSeed").GetInt32(),
            b.GetProperty("representativeSeed").GetInt32());
    }

    [Fact]
    public async Task SingleDebugRunIsExplicitlyNotStatisticallyMeaningful()
    {
        var ok=Assert.IsType<OkObjectResult>(
            await new ForeverSharedIterationsController().SimulateIterations(
                new(){Configuration=Mage(),Iterations=1},CancellationToken.None));
        var result=Root(ok.Value);
        Assert.False(result.GetProperty("hasStatisticalDistribution").GetBoolean());
        var distribution=result.GetProperty("dps");
        Assert.Equal(1,distribution.GetProperty("count").GetInt32());
        Assert.Equal(0m,distribution.GetProperty("standardDeviation").GetDecimal());
        Assert.Equal(distribution.GetProperty("mean").GetDecimal(),
            distribution.GetProperty("percentile99").GetDecimal());
    }

    [Fact]
    public async Task InvalidCountAndMissingItemizedAuthorizationFailClosed()
    {
        var controller=new ForeverSharedIterationsController();
        var request=new ForeverSharedIterationsController.IterationRequest{
            Configuration=Mage(),Iterations=101
        };
        Assert.IsType<BadRequestObjectResult>(await controller.SimulateIterations(
            request,CancellationToken.None));
        request.Iterations=2;
        request.Configuration!.ItemizedTalentMode=false;
        Assert.IsType<BadRequestObjectResult>(await controller.SimulateIterations(
            request,CancellationToken.None));
        request.Configuration=null;
        Assert.IsType<BadRequestObjectResult>(await controller.SimulateIterations(
            request,CancellationToken.None));
    }

    [Fact]
    public async Task WarriorUsesItsOriginalSeededRunnerAndPreservesSourceRequest()
    {
        var warrior=new WarriorSimRequest{
            Preset="arms",DurationSeconds=12m,Iterations=2,Seed=175888,
            CaptureRepresentativeTimeline=true
        };
        var request=new ForeverSharedIterationsController.IterationRequest {
            Warrior=warrior,Iterations=3
        };
        var result=Assert.IsType<OkObjectResult>(
            await new ForeverSharedIterationsController().SimulateIterations(
                request,CancellationToken.None));
        var root=Root(result.Value);
        Assert.Equal("warrior",root.GetProperty("cls").GetString());
        Assert.Equal(3,root.GetProperty("dps").GetProperty("count").GetInt32());
        Assert.Equal(3,root.GetProperty("samples").GetArrayLength());
        var samples=root.GetProperty("samples").EnumerateArray().ToArray();
        Assert.Equal(175888,samples[0].GetProperty("seed").GetInt32());
        Assert.Equal(175889,samples[1].GetProperty("seed").GetInt32());
        Assert.Equal(175890,samples[2].GetProperty("seed").GetInt32());
        Assert.Equal(2,warrior.Iterations); // caller request untouched
        Assert.Equal("warrior",root.GetProperty("representative")
            .GetProperty("classKey").GetString());
        Assert.InRange(root.GetProperty("dps").GetProperty("percentile99").GetDecimal(),
            root.GetProperty("dps").GetProperty("minimum").GetDecimal(),
            root.GetProperty("dps").GetProperty("maximum").GetDecimal());
        Assert.True(root.GetProperty("representative").GetProperty("sim")
            .GetProperty("batch").GetProperty("representativeRun")
            .GetProperty("summary").GetProperty("durationSeconds").GetDecimal()>0m);
    }

    [Fact]
    public async Task MixingWarriorAndCasterOrOmittingBothFailsClosed()
    {
        var c=new ForeverSharedIterationsController();
        var req=new ForeverSharedIterationsController.IterationRequest {
            Configuration=Mage(),Warrior=new WarriorSimRequest()
        };
        Assert.IsType<BadRequestObjectResult>(await c.SimulateIterations(req,CancellationToken.None));
        req.Configuration=null;req.Warrior=null;
        Assert.IsType<BadRequestObjectResult>(await c.SimulateIterations(req,CancellationToken.None));
    }
}
