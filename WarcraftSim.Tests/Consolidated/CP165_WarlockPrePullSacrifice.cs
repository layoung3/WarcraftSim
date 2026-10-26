using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp165WarlockPrePullSacrificeTests
{
    private static WarlockSimRequest Setup(string? sacrificed,string? active="none",bool pact=true,
        string spec="demonology")
    {
        var r=WarlockSimRequest.CreatePreset(spec);
        r.DurationSeconds=80m;r.CaptureTimeline=true;r.Seed=165001;
        r.PrePullSacrificedDemon=sacrificed;
        r.EnablePet=active!="none";
        if(r.EnablePet)r.PetType=active!;
        if(!pact)r.Talents.Ranks.Remove("demonic-pact");
        return r;
    }

    [Fact]
    public void ImpSacrificeWithoutCompanionBoostsShadowAndNeverSummonsPet()
    {
        var baseline=Setup(null);
        var sacrifice=Setup("imp");
        var plain=new WarlockSimRunner().Run(baseline);
        var result=new WarlockSimRunner().Run(sacrifice);
        Assert.Equal("imp",result.SacrificeBuffAtPull);
        Assert.Null(result.ActiveDemonAtPull);
        Assert.False(result.SacrificeCanceledBeforePull);
        Assert.Equal(0m,result.PetDamage);
        Assert.False(result.Run.Summary.ActorSummaries.ContainsKey("warlock-pet"));
        Assert.DoesNotContain(result.Run.Timeline,e=>e.SourceActorKey=="warlock-pet");
        Assert.DoesNotContain(result.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey=="demonic-sacrifice");
        Assert.True(result.WarlockDamage>plain.WarlockDamage);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="demonic-sacrifice" && e.TimeSeconds==0m);
    }

    [Theory]
    [InlineData("imp","felhunter",true,false)]
    [InlineData("imp","imp",true,true)]
    [InlineData("imp","felhunter",false,true)]
    [InlineData("succubus","voidwalker",true,false)]
    [InlineData("succubus","incubus",true,true)]
    [InlineData("incubus","succubus",true,true)]
    public void PactAndSayaadEquivalenceResolveBeforePull(string sacrificed,string active,
        bool pact,bool canceled)
    {
        var result=new WarlockSimRunner().Run(Setup(sacrificed,active,pact));
        Assert.Equal(canceled,result.SacrificeCanceledBeforePull);
        Assert.Equal(canceled?null:sacrificed,result.SacrificeBuffAtPull);
        Assert.Equal(active,result.ActiveDemonAtPull);
        Assert.True(result.PetDamage>0m);
        Assert.InRange(Math.Abs(result.WarlockDamage+result.PetDamage-result.DamageDone),0m,.001m);
    }

    [Fact]
    public void VoidwalkerPrePullRestoresManaWithoutACompanion()
    {
        var request=Setup("voidwalker");
        request.StartingManaPercent=10m;
        var result=new WarlockSimRunner().Run(request);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="demonic-sacrifice" && e.ResourceKey=="mana" && e.Amount>0m);
    }

    [Fact]
    public void FelhunterPrePullRegeneratesHealthWhenDamaged()
    {
        var request=Setup("felhunter");
        request.StartingManaPercent=10m;
        request.AssumeExternalHealingForLifeTap=false;
        request.WarlockMaximumHealth=12000m;
        var result=new WarlockSimRunner().Run(request);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.Healing &&
            e.AbilityKey=="demonic-sacrifice" && e.Amount>0m);
    }

    [Fact]
    public void PrePullRequiresDemonicSacrificeTalent()
    {
        var request=Setup("imp");
        request.Talents.Ranks.Remove("demonic-sacrifice");
        Assert.Throws<ArgumentException>(()=>new WarlockSimRunner().Run(request));
    }

    [Fact]
    public void SerializedRequestAndResponseRetainResolvedSacrificeState()
    {
        var request=Setup("imp","felhunter");
        var roundTrip=JsonSerializer.Deserialize<WarlockSimRequest>(
            JsonSerializer.Serialize(request))!;
        var result=new WarlockSimRunner().Run(roundTrip);
        Assert.Equal("imp",result.SacrificeBuffAtPull);
        Assert.Equal("felhunter",result.ActiveDemonAtPull);
        var json=JsonSerializer.Serialize(result);
        Assert.Contains("SacrificeBuffAtPull",json);
        Assert.Contains("SacrificeCanceledBeforePull",json);
    }

    [Fact]
    public void HunterCompanionRegressionStillProducesIndependentDamage()
    {
        var request=new BeastMasteryHunterSimRequest {DurationSeconds=35m,
            PetFamily="bear",AdditionalTargets=2,Seed=163001};
        var result=new BeastMasteryHunterSimRunner().Run(request);
        Assert.True(result.PetDamage>0m);
        Assert.Equal(result.HunterDamage+result.PetDamage+result.HawkDamage,result.DamageDone);
    }
}
