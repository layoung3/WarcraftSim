using WarcraftSim.Api.Simulation;
using Xunit;

namespace WarcraftSim.Tests;
public sealed class WarlockAdaptiveCp140Tests
{
    [Theory]
    [InlineData("affliction","wrack")]
    [InlineData("demonology","soul-fire")]
    [InlineData("destruction","incinerate")]
    public void EachSpecOffersModeledTalentedAction(string spec,string ability)
    {
        var r=WarlockSimRequest.CreatePreset(spec);
        Assert.Contains(WarlockAdaptiveRotation.Build(r).Entries,e=>e.AbilityKey==ability);
    }
    [Theory]
    [InlineData("affliction")]
    [InlineData("demonology")]
    [InlineData("destruction")]
    public void AllSpecsCompleteWithAdaptiveDefault(string spec)
    {
        var r=WarlockSimRequest.CreatePreset(spec);r.CaptureTimeline=false;
        var result=new WarlockSimRunner().Run(r);
        Assert.True(result.DamageDone>0m);
        Assert.NotEmpty(result.ResolvedRotation);
        Assert.InRange(Math.Abs(result.DamageDone-result.DamageByAbility.Values.Sum()),0m,.001m);
    }
    [Fact]
    public void AlternativeCustomPriorityOverridesAfflictionSpells()
    {
        var r=WarlockSimRequest.CreatePreset("affliction");r.CustomPriority=["shadow-bolt"];
        var result=new WarlockSimRunner().Run(r);
        Assert.DoesNotContain("wrack",result.ResolvedRotation);
        Assert.DoesNotContain("corruption",result.ResolvedRotation);
        Assert.Contains("shadow-bolt",result.ResolvedRotation);
    }
    [Fact]
    public void WrackIsAbsentWhenTalentIsNotChosen()
    {
        var r=WarlockSimRequest.CreatePreset("affliction");r.Talents.Ranks.Remove("wrack");
        Assert.DoesNotContain(WarlockAdaptiveRotation.Build(r).Entries,e=>e.AbilityKey=="wrack");
    }
    [Fact]
    public void DestructionWithoutIncinerateFallsBackToShadowBolt()
    {
        var r=WarlockSimRequest.CreatePreset("destruction");r.Talents.Ranks.Remove("incinerate");
        var rotation=WarlockAdaptiveRotation.Build(r);
        Assert.DoesNotContain(rotation.Entries,e=>e.AbilityKey=="incinerate");
        Assert.Contains(rotation.Entries,e=>e.AbilityKey=="shadow-bolt");
    }
    [Fact]
    public void InvalidCustomSpecAbilityIsRejected()
    {
        var r=WarlockSimRequest.CreatePreset("demonology");r.CustomPriority=["incinerate"];
        Assert.Throws<ArgumentException>(()=>WarlockAdaptiveRotation.Build(r));
    }
    [Fact]
    public void InvalidPolicyIsRejected()
    {
        var r=WarlockSimRequest.CreatePreset("destruction");r.RotationPolicy="not-a-policy";
        Assert.Throws<ArgumentException>(()=>WarlockAdaptiveRotation.Build(r));
    }
    [Fact]
    public void GearIntellectChangesManaPool()
    {
        var a=WarlockSimRequest.CreatePreset("demonology");a.Character.BaseStats.Values["intellect"]=200m;
        var b=WarlockSimRequest.CreatePreset("demonology");b.Character.BaseStats.Values["intellect"]=250m;
        Assert.True(new WarlockSimRunner().Run(b).MaximumMana>new WarlockSimRunner().Run(a).MaximumMana);
    }
}
