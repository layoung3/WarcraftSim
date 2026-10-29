using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Hunter;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp169MarksmanshipTimingAndDamage
{
    [Theory]
    [InlineData(0,6.0)]
    [InlineData(1,5.7)]
    [InlineData(3,5.1)]
    [InlineData(5,4.5)]
    public void ArcaneShotCooldownRanksDoNotAlterAimedMultiSharedCooldown(
        int rank,double expected)
    {
        var talents=new ForeverMarksmanshipHunterTalents();
        talents.Ranks["improved-arcane-shot"]=rank;
        var spells=ForeverMarksmanshipHunterSpellbook.Create(
            talents,new ForeverMarksmanshipHunterTuning(),1000m);
        Assert.Equal((decimal)expected,spells.Single(a=>a.Key=="arcane-shot").CooldownSeconds);
        var aimed=spells.Single(a=>a.Key=="aimed-shot");
        var multi=spells.Single(a=>a.Key=="multi-shot");
        Assert.Equal("hunter-aimed-multi",aimed.SharedCooldownGroupKey);
        Assert.Equal(aimed.SharedCooldownGroupKey,multi.SharedCooldownGroupKey);
        Assert.Equal(6m,aimed.SharedCooldownSeconds);
        Assert.Equal(6m,multi.SharedCooldownSeconds);
    }

    private static MarksmanshipHunterSimRequest AutoRequest(bool rapid)
    {
        var req=new MarksmanshipHunterSimRequest {
            DurationSeconds=24m,UseRapidFire=rapid,
            UseAspectOfTheHawk=false,UseSerpentSting=false,
            UseAimedShot=false,UseMultiShot=false,UseArcaneShot=false,
            UseSniperShot=false,UseVolley=false,CaptureTimeline=true
        };
        req.Tuning.HitChancePercent=100m;
        req.Tuning.CriticalChancePercent=0m;
        return req;
    }

    [Fact]
    public void RapidFireActuallyRetimesStationaryRangedAutosAndExpires()
    {
        var normal=new MarksmanshipHunterSimRunner().Run(AutoRequest(false));
        var fast=new MarksmanshipHunterSimRunner().Run(AutoRequest(true));
        int Swings(MarksmanshipHunterSimResponse r)=>r.Run.Timeline.Count(e=>
            e.Type==CombatEventType.Damage &&
            e.AbilityKey=="auto-shot" && e.SourceActorKey=="hunter" &&
            e.Amount>0m);
        Assert.True(Swings(fast)>Swings(normal),
            $"Rapid Fire did not improve Auto Shot frequency: {Swings(fast)} vs {Swings(normal)}");
        Assert.Equal(1,fast.RapidFireCasts);
        Assert.Contains(fast.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="rapid-fire" && e.SourceActorKey=="hunter");
        Assert.Contains(fast.Run.Timeline,e=>e.Type==CombatEventType.AuraRemoved &&
            e.AbilityKey=="rapid-fire" && e.SourceActorKey=="hunter");
        Assert.InRange(Math.Abs(fast.DamageDone-fast.DamageByAbility.Values.Sum()),
            0m,.000001m);
    }

    [Theory]
    [InlineData(0,300.0)]
    [InlineData(1,240.0)]
    [InlineData(2,180.0)]
    public void RapidKillingRankReducesRapidFireCooldown(int rank,double expected)
    {
        var talents=new ForeverMarksmanshipHunterTalents();
        talents.Ranks["rapid-killing"]=rank;
        var spell=ForeverMarksmanshipHunterSpellbook.Create(talents,
            new ForeverMarksmanshipHunterTuning(),1000m)
            .Single(s=>s.Key=="rapid-fire");
        Assert.Equal((decimal)expected,spell.CooldownSeconds);
    }

    [Fact]
    public void MortalShotsIncreasesCriticalRangedDamageWithoutChangingNoncritical()
    {
        MarksmanshipHunterSimRequest Build(int rank,decimal crit)
        {
            var r=AutoRequest(false);
            r.Talents.Ranks["mortal-shots"]=rank;
            r.Tuning.CriticalChancePercent=crit;
            r.CustomPriority=["aimed-shot"];
            return r;
        }
        decimal First(int rank,decimal crit)
        {
            var run=new MarksmanshipHunterSimRunner().Run(Build(rank,crit));
            return run.Run.Timeline.First(e=>e.Type==CombatEventType.Damage &&
                e.AbilityKey=="aimed-shot" && e.Amount>0m).Amount!.Value;
        }
        Assert.Equal(First(0,100m)*1.15m,First(5,100m));
        Assert.Equal(First(0,0m),First(5,0m));
    }
}
