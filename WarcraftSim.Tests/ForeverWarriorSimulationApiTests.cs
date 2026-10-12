using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using Microsoft.AspNetCore.Mvc;

namespace WarcraftSim.Tests;

public sealed class ForeverWarriorSimulationApiTests
{
    private static WarriorSimRequest FastArms() => new()
    {
        Iterations = 2,
        DurationSeconds = 15m,
        ExecutePhaseDurationSeconds = 0m,
        TargetLevel = 60,
        TargetDodgePercent = 0m,
        StartingRage = 50m,
        UseBloodrage = false
    };

    [Fact]
    public void Arms_ProducesDamageAndAbilityBreakdowns()
    {
        var result = new ForeverWarriorSimRunner().Run(FastArms());
        Assert.Equal("arms", result.Preset);
        Assert.Equal(2, result.Batch.Summary.Iterations);
        Assert.True(result.MeanDps > 0m);
        Assert.True(result.Batch.Summary.AverageDamageDoneByAbility.ContainsKey("main-hand-auto"));
        Assert.True(result.Batch.Summary.AverageDamageDoneByAbility.ContainsKey("mortal-strike"));
    }

    [Fact]
    public void Fury_ProducesBloodthirstDamage()
    {
        var request = FastArms();
        request.Preset = "fury";
        var result = new ForeverWarriorSimRunner().Run(request);
        Assert.True(result.MeanDps > 0m);
        Assert.True(result.Batch.Summary.AverageDamageDoneByAbility.ContainsKey("bloodthirst"));
    }

    [Fact]
    public void Level63Boss_UsesAnExplicitGlancingMultiplier()
    {
        var request = FastArms();
        request.TargetLevel = 63;
        request.ProvisionalGlancingDamageMultiplier = 0.70m;
        var result = new ForeverWarriorSimRunner().Run(request);
        Assert.True(result.MeanDps > 0m);
    }

    [Fact]
    public void IncreasingTargetArmor_ReducesDamageInTheProvisionalModel()
    {
        var request = FastArms();
        request.TargetArmor = 0m;
        var runner = new ForeverWarriorSimRunner();
        var unarmored = runner.Run(request);
        request.TargetArmor = 6000m;
        var armored = runner.Run(request);
        Assert.True(unarmored.MeanDps > armored.MeanDps);
    }

    [Fact]
    public void SameInputAndSeed_ProduceTheSameMeanDps()
    {
        var request = FastArms();
        var runner = new ForeverWarriorSimRunner();
        var a = runner.Run(request);
        var b = runner.Run(request);
        Assert.Equal(a.MeanDps, b.MeanDps);
        Assert.Equal(a.Batch.Summary.AverageDamageDoneByAbility["main-hand-auto"],
            b.Batch.Summary.AverageDamageDoneByAbility["main-hand-auto"]);
        Assert.Equal(a.Batch.RepresentativeSeed, b.Batch.RepresentativeSeed);
    }

    [Fact]
    public void RepresentativeTimeline_CanBeDisabled()
    {
        var request = FastArms();
        request.CaptureRepresentativeTimeline = false;
        var result = new ForeverWarriorSimRunner().Run(request);
        Assert.Empty(result.Batch.RepresentativeRun.Timeline);
        Assert.True(result.Batch.RepresentativeRun.Summary.DamageDone > 0m);
    }

    [Fact]
    public void RepresentativeTimeline_CanBeCapturedOnDemand()
    {
        var request = FastArms();
        request.CaptureRepresentativeTimeline = true;
        var result = new ForeverWarriorSimRunner().Run(request);
        Assert.NotEmpty(result.Batch.RepresentativeRun.Timeline);
    }

    [Fact]
    public void ExplicitProvisionalInputs_AreReportedAsLimitations()
    {
        var result = new ForeverWarriorSimRunner().Run(FastArms());
        Assert.Contains(result.ModelingNotes, note => note.Contains("Glancing", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.ModelingNotes, note => note.Contains("Armor", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.ModelingNotes, note => note.Contains("Rend", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DualWieldFury_RunsBothIndependentAutoStreams()
    {
        var request = FastArms();
        request.Preset = "fury";
        request.DualWield = true;
        request.IsTwoHanded = false;
        var result = new ForeverWarriorSimRunner().Run(request);
        Assert.True(result.Batch.Summary.AverageDamageDoneByAbility.ContainsKey("main-hand-auto"));
        Assert.True(result.Batch.Summary.AverageDamageDoneByAbility.ContainsKey("off-hand-auto"));
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("")]
    public void InvalidPreset_IsRejected(string preset)
    {
        var request = FastArms();
        request.Preset = preset;
        Assert.Throws<ArgumentException>(() => new ForeverWarriorSimRunner().Run(request));
    }

    [Fact]
    public void InvalidImpalePrerequisite_IsRejected()
    {
        var request = FastArms();
        request.DeepWoundsRank = 2;
        Assert.Throws<ArgumentException>(() => new ForeverWarriorSimRunner().Run(request));
    }

    [Fact]
    public void TwoHandWithOffhand_IsRejected()
    {
        var request = FastArms();
        request.DualWield = true;
        Assert.Throws<ArgumentException>(() => new ForeverWarriorSimRunner().Run(request));
    }

    [Fact]
    public void InvalidIterationCount_IsRejected()
    {
        var request = FastArms();
        request.Iterations = 501;
        Assert.Throws<ArgumentException>(() => new ForeverWarriorSimRunner().Run(request));
    }

    [Fact]
    public void UnverifiedArmorOrGlancingInputs_AreNeverAcceptedOutsideBounds()
    {
        var request = FastArms();
        request.ProvisionalGlancingDamageMultiplier = -0.1m;
        Assert.Throws<ArgumentException>(() => new ForeverWarriorSimRunner().Run(request));
        request.ProvisionalGlancingDamageMultiplier = 0.7m;
        request.ProvisionalArmorBaseConstant = 0m;
        request.ProvisionalArmorPerAttackerLevelConstant = 0m;
        Assert.Throws<ArgumentException>(() => new ForeverWarriorSimRunner().Run(request));
    }

    [Fact]
    public void FuryBloodthrillWithoutStanceSwitching_IsRejected()
    {
        var request = FastArms();
        request.Preset = "fury";
        request.BloodthrillRank = 1;
        Assert.Throws<ArgumentException>(() => new ForeverWarriorSimRunner().Run(request));
    }

    [Fact]
    public void ControllerReturnsBadRequestForInvalidInput()
    {
        var request = FastArms();
        request.DurationSeconds = 0m;
        var action = new WarriorSimulationController().Run(request);
        Assert.IsType<BadRequestObjectResult>(action.Result);
    }

    [Fact]
    public void ControllerReturnsSimulationResponseForValidInput()
    {
        var action = new WarriorSimulationController().Run(FastArms());
        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var result = Assert.IsType<WarriorSimResponse>(ok.Value);
        Assert.Equal(2, result.Batch.Summary.Iterations);
    }
}
