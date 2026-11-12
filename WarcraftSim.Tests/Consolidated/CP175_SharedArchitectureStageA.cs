using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Batch;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175SharedArchitectureStageATests
{
    [Theory]
    [InlineData("mage",true,true,true)]
    [InlineData("warlock",true,true,true)]
    [InlineData("hunter",true,true,true)]
    [InlineData("warrior",true,true,true)]
    public void CapabilityFlagsMirrorVerifiedRunnerBoundaries(
        string cls,bool unified,bool seededBatch,bool sharedPreview)
    {
        var value=ForeverClassSimulationCapabilities.Get(cls);
        Assert.Equal(cls,value.ClassKey);
        Assert.Equal(3,value.Specializations.Length);
        Assert.Equal(unified,value.ExecutesThroughCurrentUnifiedEndpoint);
        Assert.Equal(seededBatch,value.HasSeededBatchResults);
        Assert.Equal(sharedPreview,value.HasRotationPreview);
        Assert.True(value.HasSharedCombatItemProcessor);
        Assert.NotEmpty(value.KnownRestrictions);
        Assert.False(value.ExecutesArbitraryCrossTreeModeledTalents);
    }

    [Fact]
    public void CapabilityDiscoveryIncludesFourInitialClassesAndRestrictedCasters()
    {
        var controller=new ForeverSharedCharacterController();
        var response=Assert.IsType<OkObjectResult>(controller.Capabilities());
        Assert.NotNull(response.Value);
        Assert.Equal(8,ForeverClassSimulationCapabilities.All.Count);
        var shadow=ForeverClassSimulationCapabilities.Get("priest");
        Assert.Equal(new[]{"shadow"},shadow.Specializations);
        Assert.False(shadow.HasExecutableCustomRotation);
        Assert.False(shadow.HasSharedCombatItemProcessor);
        Assert.Contains(shadow.KnownRestrictions,x=>x.Contains("15 unspent"));
        var balance=ForeverClassSimulationCapabilities.Get("druid");
        Assert.Equal(new[]{"balance"},balance.Specializations);
        Assert.False(balance.HasSeededBatchResults);
        Assert.Contains(balance.KnownRestrictions,x=>x.Contains("16 unspent"));
        Assert.DoesNotContain("racial-active",balance.EncounterControls);
        var elemental=ForeverClassSimulationCapabilities.Get("shaman");
        Assert.Equal(new[]{"elemental"},elemental.Specializations);
        Assert.False(elemental.HasSeededBatchResults);
        Assert.Contains(elemental.KnownRestrictions,x=>x.Contains("historical provisional"));
        Assert.DoesNotContain("racial-active",elemental.EncounterControls);
        Assert.IsType<OkObjectResult>(controller.Capability("rogue"));
        var combat=Assert.IsType<ForeverClassSimulationCapability>(Assert.IsType<OkObjectResult>(controller.Capability("rogue","combat")).Value);
        Assert.Equal(new[]{"combat"},combat.Specializations);Assert.False(combat.HasRotationPreview);
        Assert.IsType<OkObjectResult>(controller.Capability("Warlock"));
    }

    private static ForeverSharedCharacterConfiguration Valid() => new()
    {
        ClassKey="mage",Specialization="arcane",Race="orc",
        ItemizedTalents=new(StringComparer.OrdinalIgnoreCase) {
            ["improved-channeling"]=5
        }
    };

    [Fact]
    public void EnvelopeIsStructuralOnlyAndNeverClaimsCombatCertification()
    {
        var value=ForeverSharedCharacterConfigurationValidator.Validate(Valid());
        Assert.Equal(5,value.RankedPoints);
        Assert.False(value.ReadyForUnifiedSimulation);
        Assert.Contains(value.Limitations,x=>x.Contains("prerequisites",StringComparison.OrdinalIgnoreCase));
        Assert.IsType<OkObjectResult>(
            new ForeverSharedCharacterController().Validate(Valid()));
    }

    [Fact]
    public void EnvelopeRejectsInvalidClassRankBudgetUnsupportedItemsAndFakeSurvivability()
    {
        var value=Valid();value.ClassKey="paladin";
        Assert.Throws<ArgumentException>(()=>ForeverSharedCharacterConfigurationValidator.Validate(value));
        value=Valid();value.ItemizedTalents["arcane-mind"]=47;
        Assert.Throws<ArgumentException>(()=>ForeverSharedCharacterConfigurationValidator.Validate(value));
        value=Valid();value.ItemizedTalents["arcane-mind"]=5;
        value.ItemizedTalents["arcane-blast"]=5;
        value.ItemizedTalents["arcane-wrath"]=5;
        value.ItemizedTalents["arcane-power"]=5;
        value.ItemizedTalents["presence-of-mind"]=5;
        value.ItemizedTalents["arcane-impact"]=5;
        value.ItemizedTalents["arcane-concentration"]=5;
        value.ItemizedTalents["arcane-subtlety"]=5;
        value.ItemizedTalents["wand-specialization"]=5;
        value.ItemizedTalents["magic-absorption"]=5;
        Assert.Throws<ArgumentException>(()=>ForeverSharedCharacterConfigurationValidator.Validate(value));
        value=Valid();value.ClassKey="warlock";value.Specialization="affliction";
        value.CombatItems.ManaRubyCharges=1;
        Assert.Throws<ArgumentException>(()=>ForeverSharedCharacterConfigurationValidator.Validate(value));
        value=Valid();value.Encounter.SurvivalPolicy="nonlethal-dps-benchmark";
        Assert.Throws<ArgumentException>(()=>ForeverSharedCharacterConfigurationValidator.Validate(value));
        value=Valid();value.Modifiers.TargetDebuffMode="unknown";
        Assert.Throws<ArgumentException>(()=>ForeverSharedCharacterConfigurationValidator.Validate(value));
    }

    [Fact]
    public void ExistingBatchRunnerP99AndPopulationStandardDeviationAreReproducible()
    {
        var requested=new List<int>();
        SimulationRunResult One(int seed,bool representative)
        {
            requested.Add(seed);
            return new SimulationRunResult {
                Summary=new SimulationRunSummary {
                    Seed=seed,DurationSeconds=1m,
                    DamageDone=seed-100m
                }
            };
        }
        var result=new SimulationBatchRunner().Run(
            new SimulationBatchOptions {Iterations=5,BaseSeed=101,SimulationType=SimulationType.Dps},One);
        Assert.Equal(new[]{101,102,103,104,105,103},requested);
        Assert.Equal(103,result.RepresentativeSeed);
        var d=result.Summary.DamagePerSecond;
        Assert.Equal(3m,d.Mean);
        Assert.Equal(3m,d.Median);
        Assert.Equal(4.8m,d.Percentile95);
        Assert.Equal(4.96m,d.Percentile99);
        Assert.Equal(1m,d.Minimum);
        Assert.Equal(5m,d.Maximum);
        Assert.InRange(d.StandardDeviation,1.41421m,1.41422m);
        Assert.Contains("population",result.Summary.StandardDeviationMethod);
        Assert.Contains("N - 1",result.Summary.PercentileMethod);
    }

    [Fact]
    public void SingleIterationMustNotImplyVarianceOrArtificialPercentiles()
    {
        var result=new SimulationBatchRunner().Run(
            new SimulationBatchOptions{Iterations=1,BaseSeed=777},
            (seed,_)=>new SimulationRunResult{Summary=new SimulationRunSummary{
                Seed=seed,DurationSeconds=10m,DamageDone=1000m}});
        var d=result.Summary.DamagePerSecond;
        Assert.Equal(1,d.Count);
        Assert.Equal(100m,d.Mean);
        Assert.Equal(0m,d.StandardDeviation);
        Assert.Equal(100m,d.Percentile99);
    }
}
