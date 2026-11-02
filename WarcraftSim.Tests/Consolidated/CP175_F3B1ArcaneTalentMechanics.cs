using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175F3B1ArcaneTalentMechanics
{
    private static MageSimRequest Setup(string action)
    {
        var req=MageSimRequest.CreatePreset("arcane");
        req.DurationSeconds=12m;
        req.BaseSpellHitPercent=100m;
        req.SpellCritPercent=0m;
        req.Talents.Ranks["arcane-concentration"]=0;
        req.Talents.Ranks["arcane-meditation"]=0;
        req.UseArcanePower=false; req.UsePresenceOfMind=false;
        req.UseEvocation=false; req.UseMajorManaPotion=false;
        req.CombatItems.ManaRubyCharges=0;
        req.CustomPriority=[action];
        return req;
    }

    [Fact]
    public void MissileBarrageNoLongerProcsFromArcaneMissilesChannelTicks()
    {
        var request=Setup("arcane-missiles");
        request.Tuning.MissileBarrageArcaneMissileChancePercentPerRank=50m;
        request.Talents.Ranks["missile-barrage"]=2;
        var run=new MageSimRunner().Run(request);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="arcane-missiles" && e.IsChannelTick && e.Amount>0m);
        Assert.Equal(0,run.MissileBarrageProcs);
        Assert.DoesNotContain(run.Run.Timeline,e=>e.AbilityKey=="missile-barrage" &&
            e.Type==CombatEventType.AuraApplied);
    }

    [Fact]
    public void MissileBarrageFromLandedArcaneBlastEmitsProcAndConsumesOnChannel()
    {
        var r=MageSimRequest.CreatePreset("arcane");
        r.DurationSeconds=22m; r.BaseSpellHitPercent=100m;r.SpellCritPercent=0m;
        r.Talents.Ranks["arcane-concentration"]=0;
        r.Talents.Ranks["arcane-meditation"]=0;
        r.UseArcanePower=false;r.UsePresenceOfMind=false;
        r.UseEvocation=false;r.UseMajorManaPotion=false;
        r.CombatItems.ManaRubyCharges=0;
        r.Tuning.MissileBarrageArcaneBlastChancePercent=100m;
        var run=new MageSimRunner().Run(r);
        Assert.True(run.MissileBarrageProcs>0);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="missile-barrage");
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.AbilityChannelStarted &&
            e.AbilityKey=="arcane-missiles");
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.AuraRemoved &&
            e.AbilityKey=="missile-barrage");
    }

    [Theory]
    [InlineData(0,100)]
    [InlineData(1,85)]
    [InlineData(2,70)]
    public void ArcaneSubtletyReducesRealArcaneThreatWithoutChangingDamage(
        int rank,int expectedThreatPercent)
    {
        var req=Setup("arcane-blast");
        req.DurationSeconds=2.1m;
        req.Talents.Ranks["arcane-subtlety"]=rank;
        var result=new MageSimRunner().Run(req);
        var damage=Assert.Single(result.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="arcane-blast" && e.Amount>0m);
        var threat=Assert.Single(result.Run.Timeline,e=>e.Type==CombatEventType.ThreatChanged &&
            e.TargetActorKey=="target");
        Assert.Equal(damage.Amount!.Value*expectedThreatPercent/100m,threat.Amount);
        Assert.True(result.DamageDone>0m);
    }

    [Fact]
    public void FirePresetCanUseRankedArcaneSubtletyOnTrainerArcaneMissiles()
    {
        var baseline=MageSimRequest.CreatePreset("fire");
        baseline.DurationSeconds=6m;
        baseline.BaseSpellHitPercent=100m;baseline.SpellCritPercent=0m;
        baseline.UseEvocation=false;baseline.UseMajorManaPotion=false;
        baseline.CombatItems.ManaRubyCharges=0;
        baseline.CustomPriority=["arcane-missiles"];
        baseline.Talents.Ranks["arcane-subtlety"]=0;
        var talented=MageSimRequest.CreatePreset("fire");
        talented.DurationSeconds=6m;
        talented.BaseSpellHitPercent=100m;talented.SpellCritPercent=0m;
        talented.UseEvocation=false;talented.UseMajorManaPotion=false;
        talented.CombatItems.ManaRubyCharges=0;
        talented.CustomPriority=["arcane-missiles"];
        talented.Talents.Ranks["arcane-subtlety"]=2;
        var a=new MageSimRunner().Run(baseline);
        var b=new MageSimRunner().Run(talented);
        decimal Threat(MageSimResponse x)=>x.Run.Timeline.Where(e=>
            e.Type==CombatEventType.ThreatChanged&&e.TargetActorKey=="target")
            .Sum(e=>e.Amount??0m);
        Assert.True(Threat(a)>0m);
        Assert.Equal(a.DamageDone,b.DamageDone);
        Assert.Equal(Threat(a)*.7m,Threat(b));
    }
}
