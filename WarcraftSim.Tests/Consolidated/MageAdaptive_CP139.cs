using WarcraftSim.Api.Simulation;
using WarcraftSim.Data.Forever.Mage;
using Xunit;

namespace WarcraftSim.Tests;
public sealed class MageAdaptiveCp139Tests
{
    [Theory]
    [InlineData("arcane","arcane-blast")]
    [InlineData("fire","pyroblast")]
    [InlineData("frost","ice-lance")]
    public void StandardRotationContainsSpecsTalentAction(string spec,string key)
    {
        var r=MageSimRequest.CreatePreset(spec);
        Assert.Contains(MageAdaptiveRotation.Build(r).Entries,e=>e.AbilityKey==key);
    }
    [Theory]
    [InlineData("arcane")]
    [InlineData("fire")]
    [InlineData("frost")]
    public void AllSpecsFinishWithAdaptivePriority(string spec)
    {
        var r=MageSimRequest.CreatePreset(spec);r.CaptureTimeline=false;
        var result=new MageSimRunner().Run(r);
        Assert.True(result.DamageDone>0m);
        Assert.Contains(" > ",result.ResolvedRotation);
    }
    [Fact]
    public void CustomFireballOverridesArcaneBlastLoop()
    {
        var r=MageSimRequest.CreatePreset("arcane");r.CustomPriority=["fireball"];
        var result=new MageSimRunner().Run(r);
        Assert.DoesNotContain("arcane-blast",result.ResolvedRotation);
        Assert.Equal(0,result.ArcaneBlastCasts);
        Assert.Contains("fireball",result.ResolvedRotation);
    }
    [Fact]
    public void ArcaneWithoutBlastTalentUsesFallbackOnly()
    {
        var r=MageSimRequest.CreatePreset("arcane");
        r.Talents.Ranks.Remove("arcane-blast");r.Talents.Ranks.Remove("missile-barrage");
        var rotation=MageAdaptiveRotation.Build(r);
        Assert.DoesNotContain(rotation.Entries,e=>e.AbilityKey=="arcane-blast");
        Assert.Contains(rotation.Entries,e=>e.AbilityKey=="fireball");
    }
    [Fact]
    public void FireWithoutHeatingUpDoesNotRequirePyroblast()
    {
        var r=MageSimRequest.CreatePreset("fire");r.Talents.Ranks.Remove("heating-up");
        var rotation=MageAdaptiveRotation.Build(r);
        Assert.DoesNotContain(rotation.Entries,e=>e.AbilityKey=="pyroblast");
    }
    [Fact]
    public void FrostWithoutFingersDefaultsToFrostbolt()
    {
        var r=MageSimRequest.CreatePreset("frost");r.Talents.Ranks.Remove("fingers-of-frost");
        var rotation=MageAdaptiveRotation.Build(r);
        Assert.DoesNotContain(rotation.Entries,e=>e.AbilityKey=="ice-lance");
        Assert.Contains(rotation.Entries,e=>e.AbilityKey=="frostbolt");
    }
    [Fact]
    public void IllegalCustomSpellIsRejected()
    {
        var r=MageSimRequest.CreatePreset("frost");r.CustomPriority=["conflagrate"];
        Assert.Throws<ArgumentException>(()=>MageAdaptiveRotation.Build(r));
    }
    [Fact]
    public void InvalidRotationModeIsRejected()
    {
        var r=MageSimRequest.CreatePreset("fire");r.RotationPolicy="unknown";
        Assert.Throws<ArgumentException>(()=>MageAdaptiveRotation.Build(r));
    }
    [Fact]
    public void CurrentArcaneBlastTuningUsesHighManaPenalty()
    {
        var tuning=new ForeverMageTuning();
        Assert.Equal(175m,tuning.ArcaneBlastExtraManaPerStackPercent);
        Assert.Equal(394m,tuning.ArcaneBlastDamage);
    }
    [Fact]
    public void HighManaBudgetImprovesMageManaPool()
    {
        var a=MageSimRequest.CreatePreset("frost");a.Character.BaseStats.Values["intellect"]=200m;
        var b=MageSimRequest.CreatePreset("frost");b.Character.BaseStats.Values["intellect"]=270m;
        Assert.True(new MageSimRunner().Run(b).MaximumMana>new MageSimRunner().Run(a).MaximumMana);
    }
}
