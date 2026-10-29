using System;
using System.Linq;
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Hunter;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp169MarksmanshipRangedSpecialization
{
    private static MarksmanshipHunterSimRequest Setup(int rank)
    {
        var r=new MarksmanshipHunterSimRequest {
            DurationSeconds=28m,TargetMaximumHealth=1000000m,
            UseAspectOfTheHawk=false,UseRapidFire=false,
            UseSerpentSting=false,UseSniperShot=false,UseMultiShot=false,
            UseArcaneShot=false,UseAimedShot=true,CaptureTimeline=true,
            CustomPriority=["aimed-shot"]
        };
        r.Tuning.HitChancePercent=100m;
        r.Tuning.CriticalChancePercent=0m;
        r.Talents.Ranks["ranged-weapon-specialization"]=rank;
        return r;
    }

    [Theory]
    [InlineData("auto-shot")]
    [InlineData("aimed-shot")]
    public void FiveRanksIncreaseActualRangedPhysicalDamageByFivePercent(string ability)
    {
        var original=new MarksmanshipHunterSimRunner().Run(Setup(0));
        var enhanced=new MarksmanshipHunterSimRunner().Run(Setup(5));
        decimal First(MarksmanshipHunterSimResponse run)=>run.Run.Timeline.First(e=>
            e.Type==CombatEventType.Damage && e.SourceActorKey=="hunter" &&
            e.AbilityKey==ability && e.Amount>0m).Amount!.Value;
        Assert.Equal(First(original)*1.05m,First(enhanced));
        Assert.InRange(Math.Abs(enhanced.DamageDone-enhanced.DamageByAbility.Values.Sum()),
            0m,.000001m);
    }

    [Fact]
    public void RankDoesNotBoostUnrelatedArcaneShotDamage()
    {
        MarksmanshipHunterSimRequest Request(int rank)
        {
            var request=Setup(rank);
            request.CustomPriority=["arcane-shot"];
            return request;
        }
        var baseline=new MarksmanshipHunterSimRunner().Run(Request(0));
        var enhanced=new MarksmanshipHunterSimRunner().Run(Request(5));
        decimal First(MarksmanshipHunterSimResponse sim)=>sim.Run.Timeline.First(e=>
            e.Type==CombatEventType.Damage && e.AbilityKey=="arcane-shot" &&
            e.Amount>0m).Amount!.Value;
        Assert.Equal(First(baseline),First(enhanced));
    }

    [Fact]
    public void RapidKillingRecognizesOtherKillerOnlyWhenOwnSerpentStingIsLive()
    {
        var ctx=new SimulationContext(new SimulationRunOptions{DurationSeconds=30m});
        var hunter=new SimulationActorState{Key="hunter",TeamKey="raid"};
        var ally=new SimulationActorState{Key="ally",TeamKey="raid"};
        var boss=new SimulationActorState{
            Key="target",TeamKey="enemy",MaximumHealth=1000m
        };
        boss.InitializeHealth(1000m);
        ctx.AddActor(hunter);ctx.AddActor(ally);ctx.AddActor(boss);
        var death=new CombatEvent{
            Type=CombatEventType.ActorDied,
            SourceActorKey="ally",TargetActorKey="target"
        };
        Assert.False(MarksmanshipHunterSimRunner.QualifiesRapidKillingDeath(
            ctx,death,"hunter"));
        var auras=new AuraManager();
        auras.ApplyAura(ctx,boss,new AuraDefinition{
            Key="serpent-sting",Name="Serpent Sting",DurationSeconds=15m
        },"ally","serpent-sting",null);
        Assert.False(MarksmanshipHunterSimRunner.QualifiesRapidKillingDeath(
            ctx,death,"hunter"));
        auras.ApplyAura(ctx,boss,new AuraDefinition{
            Key="serpent-sting",Name="Serpent Sting",DurationSeconds=15m
        },"hunter","serpent-sting",null);
        Assert.True(MarksmanshipHunterSimRunner.QualifiesRapidKillingDeath(
            ctx,death,"hunter"));
        auras.RemoveAuras(ctx,boss,"serpent-sting","hunter","removed");
        Assert.False(MarksmanshipHunterSimRunner.QualifiesRapidKillingDeath(
            ctx,death,"hunter"));
        death.SourceActorKey="hunter";
        Assert.True(MarksmanshipHunterSimRunner.QualifiesRapidKillingDeath(
            ctx,death,"hunter"));
    }

    [Fact]
    public void RankPrerequisitesAndCustomPriorityAreValidatedAndSerialized()
    {
        var request=Setup(5);
        request.Talents.Validate();
        request.Talents.Ranks["ranged-weapon-specialization"]=6;
        Assert.Throws<ArgumentException>(()=>request.Talents.Validate());
        request.Talents.Ranks["ranged-weapon-specialization"]=5;
        request.Talents.Ranks["careful-aim"]=4;
        Assert.Throws<ArgumentException>(()=>request.Talents.Validate());
        request.Talents.Ranks["careful-aim"]=5;
        request.Talents.Ranks["rapid-killing"]=1;
        request.Talents.Ranks["rapid-recuperation"]=1;
        Assert.Throws<ArgumentException>(()=>request.Talents.Validate());
        request.Talents.Ranks["rapid-killing"]=2;
        request.Talents.Validate();
        request.CustomPriority=["arcane-shot","aimed-shot"];
        var copy=JsonSerializer.Deserialize<MarksmanshipHunterSimRequest>(
            JsonSerializer.Serialize(request));
        Assert.NotNull(copy);
        Assert.Equal(request.CustomPriority,copy.CustomPriority);
        var run=new MarksmanshipHunterSimRunner().Run(copy);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="arcane-shot" && e.Amount>0m);
        request.CustomPriority=["invented-shot"];
        Assert.Throws<ArgumentException>(()=>new MarksmanshipHunterSimRunner().Run(request));
    }
}
