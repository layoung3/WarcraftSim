using WarcraftSim.Api.Simulation;
using WarcraftSim.Data.Forever.Druid;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests;

public sealed class BalanceOptimizationCp138Tests
{
    [Theory]
    [InlineData("throughput")]
    [InlineData("sustain")]
    public void AllGearCandidatesHaveLevelSixtyDruidIdentity(string gear)
    {
        var profile=BalancePreRaidProfileCatalog.Character(gear);
        Assert.Equal(60,profile.Level);Assert.Equal("druid",profile.ClassKey);
        Assert.Equal("balance",profile.SpecializationKey);
        Assert.True(profile.Equipment.Items.Count>=16);
    }
    [Theory]
    [InlineData("throughput")]
    [InlineData("raid-sustain")]
    [InlineData("deep-regen")]
    public void AllTalentBuildsPassAllocationValidation(string build)
    {
        var talents=BalancePreRaidProfileCatalog.Talents(build);
        talents.Validate();
        Assert.Equal(51,talents.BalancePointsSpent+talents.FeralPointsSpent+talents.RestorationPointsSpent);
    }
    [Fact]
    public void OmenIsBaselineAndNotConsumedByWrath()
    {
        var request=BalancePreRaidProfileCatalog.Setup("throughput","throughput");
        request.CaptureTimeline=true;request.DurationSeconds=180m;
        request.OmenProcChancePercent=100m;request.BaseSpellHitPercent=100m;
        var result=new BalanceDruidSimRunner().Run(request);
        Assert.True(result.OmenProcs>0);
        Assert.True(result.OmenConsumed>0);
        Assert.InRange(result.OmenConsumed,0,result.OmenProcs);
    }
    [Fact]
    public void DisablingOmenRemovesClearcastingProcs()
    {
        var request=BalancePreRaidProfileCatalog.Setup("sustain","raid-sustain");
        request.UseOmenOfClarity=false;
        var result=new BalanceDruidSimRunner().Run(request);
        Assert.Equal(0,result.OmenProcs);
    }
    [Fact]
    public void NoEclipseTalentDisablesStarfireWhenEclipseRequired()
    {
        var request=new BalanceDruidSimRequest();
        request.Talents.Ranks.Remove("eclipse");
        request.UseMoonfire=false;request.UseInsectSwarm=false;
        var result=new BalanceDruidSimRunner().Run(request);
        Assert.Equal(0,result.EclipseEmpoweredStarfires);
        Assert.DoesNotContain(result.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastStarted&&e.AbilityKey=="starfire");
    }
    [Fact]
    public void UnrecognizedCustomSpellIsRejected()
    {
        var request=new BalanceDruidSimRequest();request.CustomPriority=["mind-flay"];
        Assert.Throws<ArgumentException>(()=>new BalanceDruidSimRunner().Run(request));
    }
    [Fact]
    public void ExplicitPriorityOverridesAutomaticRotation()
    {
        var request=new BalanceDruidSimRequest{CustomPriority=["wrath"],UseOmenOfClarity=false};
        var result=new BalanceDruidSimRunner().Run(request);
        Assert.Equal("wrath",result.ResolvedRotation);
        Assert.True(result.DamageByAbility.GetValueOrDefault("wrath")>0m);
        Assert.DoesNotContain(result.DamageByAbility.Keys,key=>key=="starfire");
    }
    [Fact]
    public void FortyOnePointBuildReducesStarfireBaseCastTime()
    {
        var ranks=BalancePreRaidProfileCatalog.Talents("throughput");
        var spells=ForeverBalanceDruidSpellbook.Create(ranks,new ForeverBalanceDruidTuning(),280m);
        var starfire=Assert.Single(spells,s=>s.Key=="starfire");
        Assert.Equal(3.0m,starfire.CastTimeSeconds);
    }
    [Fact]
    public void MaxRankWrathAndInsectSwarmUseForeverCosts()
    {
        var spells=ForeverBalanceDruidSpellbook.Create(
            BalancePreRaidProfileCatalog.Talents("throughput"),new ForeverBalanceDruidTuning(),0m);
        var wrath=Assert.Single(spells,s=>s.Key=="wrath");
        Assert.Equal(45m,wrath.ResourceCosts[0].Amount); // 120 * 0.5 * 0.75
        var insect=Assert.Single(spells,s=>s.Key=="insect-swarm");
        Assert.Equal(120m,insect.ResourceCosts[0].Amount); // 160 * 0.75
    }
    [Fact]
    public void DefaultsRunReconcilesDamageUnderUpdatedCoefficients()
    {
        var result=new BalanceDruidSimRunner().Run(new BalanceDruidSimRequest());
        Assert.True(result.DamageDone>0m);
        Assert.InRange(Math.Abs(result.DamageDone-result.DamageByAbility.Values.Sum()),0m,0.000001m);
    }
}
