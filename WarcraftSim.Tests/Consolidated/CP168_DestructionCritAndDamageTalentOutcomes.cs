using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp168DestructionCriticalAndDamageTalents
{
    private static WarlockSimRequest Setup(string spell,int flames=0,int ruin=0,decimal crit=0m)
    {
        var r=WarlockSimRequest.CreatePreset("destruction");
        r.EnablePet=false;
        r.PrePullSacrificedDemon=null;
        r.UseLifeTap=false;
        r.Talents.Ranks["agonizing-flames"]=flames;
        r.Talents.Ranks["ruin"]=ruin;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=crit;
        r.TargetMaximumHealth=1000000m;
        r.AdditionalTargets=2;
        r.StartingSoulShards=4;
        r.WarlockMaximumHealth=10000m;
        r.DurationSeconds=20m;
        r.CaptureTimeline=true;
        r.CustomPriority=spell=="conflagrate"?
            ["immolate","conflagrate"]: [spell];
        return r;
    }

    private static decimal First(WarlockSimResponse result,string spell) =>
        result.Run.Timeline.First(e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="warlock" && e.TargetActorKey=="target" &&
            e.AbilityKey==spell && e.Amount>0m).Amount!.Value;

    [Theory]
    [InlineData("shadow-bolt")]
    [InlineData("searing-pain")]
    [InlineData("soul-fire")]
    [InlineData("shadowburn")]
    [InlineData("incinerate")]
    [InlineData("immolate")]
    [InlineData("conflagrate")]
    [InlineData("rain-of-fire")]
    [InlineData("hellfire")]
    public void AgonizingFlamesRankThreeEmpowersEachDestructionDamagePath(string spell)
    {
        var normal=new WarlockSimRunner().Run(Setup(spell,0));
        var talented=new WarlockSimRunner().Run(Setup(spell,3));
        Assert.Equal(First(normal,spell)*1.10m,First(talented,spell));
    }

    [Theory]
    [InlineData("shadow-bolt")]
    [InlineData("searing-pain")]
    [InlineData("shadowburn")]
    [InlineData("incinerate")]
    [InlineData("rain-of-fire")]
    [InlineData("hellfire")]
    public void RuinRankFiveRaisesCriticalBonusForAllEligibleDamage(string spell)
    {
        var baseRun=new WarlockSimRunner().Run(Setup(spell,0,0,100m));
        var critRun=new WarlockSimRunner().Run(Setup(spell,0,5,100m));
        Assert.Equal(First(baseRun,spell)*4m/3m,First(critRun,spell));
        Assert.Contains(critRun.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey==spell && e.TargetActorKey=="target" && e.IsCritical);
    }

    [Fact]
    public void RuinAndAgonizingFlamesNeverBuffHellfireCasterSelfDamage()
    {
        var baseline=new WarlockSimRunner().Run(Setup("hellfire",0,0,100m));
        var enhanced=new WarlockSimRunner().Run(Setup("hellfire",3,5,100m));
        decimal Self(WarlockSimResponse result)=>result.Run.Timeline.First(e=>
            e.Type==CombatEventType.Damage && e.AbilityKey=="hellfire" &&
            e.TargetActorKey=="warlock").Amount!.Value;
        Assert.Equal(Self(baseline),Self(enhanced));
        Assert.True(enhanced.DamageDone>baseline.DamageDone);
        Assert.InRange(Math.Abs(enhanced.DamageDone-enhanced.DamageByAbility.Values.Sum()),
            0m,.000001m);
    }
}
