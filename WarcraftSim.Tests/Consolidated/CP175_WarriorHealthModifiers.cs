using WarcraftSim.Api.Simulation;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175WarriorHealthModifiers
{
    private static WarriorSimRequest Build() => new() {
        Preset="fury",Level=60,Iterations=1,DurationSeconds=8m,
        Seed=175283,MaximumHealth=5000m,StatSourceMode="manual",
        CaptureRepresentativeTimeline=true,UseBloodrage=false
    };

    [Fact]
    public void SharedFlaskAndElixirHealthIncreaseActualActorCapWithoutMutatingRequest()
    {
        var baseline=Build();
        var plain=new ForeverWarriorSimRunner().Run(baseline);
        Assert.Equal(5000m,plain.Batch.RepresentativeRun.CustomMetrics["warrior-health-maximum"]);
        var flask=Build();
        flask.ExternalModifiers.Consumables.Add("flask-of-the-titans");
        var buffed=new ForeverWarriorSimRunner().Run(flask);
        Assert.Equal(6200m,buffed.Batch.RepresentativeRun.CustomMetrics["warrior-health-maximum"]);
        Assert.Equal(5000m,flask.MaximumHealth);
        Assert.Equal(5000m,baseline.MaximumHealth);
        var elixir=Build();
        elixir.ExternalModifiers.Consumables.Add("elixir-of-lesser-fortitude");
        Assert.Equal(5120m,new ForeverWarriorSimRunner().Run(elixir)
            .Batch.RepresentativeRun.CustomMetrics["warrior-health-maximum"]);
    }

    [Fact]
    public void RemovingFlaskReturnsToIdenticalSeededActorHealthAndDamage()
    {
        var request=Build();
        var first=new ForeverWarriorSimRunner().Run(request);
        request.ExternalModifiers.Consumables.Add("flask-of-the-titans");
        var withFlask=new ForeverWarriorSimRunner().Run(request);
        Assert.Equal(6200m,withFlask.Batch.RepresentativeRun.CustomMetrics["warrior-health-maximum"]);
        request.ExternalModifiers.Consumables.Clear();
        var after=new ForeverWarriorSimRunner().Run(request);
        Assert.Equal(first.Batch.RepresentativeRun.CustomMetrics["warrior-health-maximum"],
            after.Batch.RepresentativeRun.CustomMetrics["warrior-health-maximum"]);
        Assert.Equal(first.MeanDps,after.MeanDps);
    }
}
