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
    public void ClearcastingIsRankGatedAndItsManaReceiptReplaysAtTheSameSeed()
    {
        MageSimRequest Setup(int rank)
        {
            var r=MageSimRequest.CreatePreset("arcane");
            r.DurationSeconds=400m;r.Seed=132001;
            r.BaseSpellHitPercent=100m;r.SpellCritPercent=0m;
            r.Talents.Ranks["arcane-concentration"]=rank;
            r.Talents.Ranks["arcane-meditation"]=0;
            r.UseEvocation=false;r.UseMissileBarrage=false;
            r.UseMajorManaPotion=false;r.CombatItems.ManaRubyCharges=0;
            r.Tuning.FireballMana=10m;r.Tuning.ManaRegenPerSecond=100m;
            r.CustomPriority=["fireball"];
            return r;
        }
        var off=new MageSimRunner().Run(Setup(0));
        var on=new MageSimRunner().Run(Setup(5));
        var replay=new MageSimRunner().Run(Setup(5));
        Assert.Equal(0,off.ClearcastingProcs);
        Assert.True(on.ClearcastingProcs>0);
        Assert.True(on.ClearcastingConsumed>0);
        Assert.True(on.ClearcastingConsumed<=on.ClearcastingProcs);
        Assert.Contains(on.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="arcane-concentration" && e.TargetActorKey=="mage");
        Assert.Contains(on.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="fireball" && e.ResourceKey=="mana" && e.Amount==0m);
        var receipt=(MageSimResponse x)=>x.Run.Timeline
            .Where(e=>e.AbilityKey is "fireball" or "arcane-concentration" &&
                e.Type is CombatEventType.ResourceChanged or CombatEventType.AuraApplied)
            .Select(e=>(e.TimeSeconds,e.Type,e.AbilityKey,e.Amount)).ToArray();
        Assert.Equal(receipt(on),receipt(replay));
    }

    private static MageSimRequest ShieldBuild(int rank, decimal incoming, string school="physical")
    {
        var r=MageSimRequest.CreatePreset("arcane");
        r.Talents.Ranks["arcane-shielding"]=rank;
        r.DurationSeconds=6m;
        r.BaseSpellHitPercent=100m;r.SpellCritPercent=0m;
        r.Talents.Ranks["arcane-concentration"]=0;
        r.Talents.Ranks["arcane-meditation"]=0;
        r.Tuning.ManaRegenPerSecond=0m;
        r.Character.BaseStats.Values["spirit"]=0m;
        r.Character.BaseStats.Values["mp5"]=0m;
        r.UseEvocation=false;r.UseMissileBarrage=false;
        r.UseMajorManaPotion=false;r.CombatItems.ManaRubyCharges=0;
        r.IncomingDamageAtSeconds=2m;
        r.IncomingDamageAmount=incoming;
        r.IncomingDamageSchool=school;
        r.CustomPriority=["mana-shield","fireball"];
        return r;
    }

    [Theory]
    [InlineData(0,200)]
    [InlineData(1,166)]
    [InlineData(2,134)]
    public void ArcaneShieldingChargesRankSpecificManaForPhysicalAbsorb(
        int rank,int manaSpent)
    {
        var run=new MageSimRunner().Run(ShieldBuild(rank,100m));
        Assert.Equal(100m,run.Run.CustomMetrics["mage-mana-shield-absorbed"]);
        Assert.Equal((decimal)manaSpent,run.Run.CustomMetrics["mage-mana-shield-spent-mana"]);
        Assert.Equal(3000m,run.EndingHealth);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.AbsorbApplied &&
            e.AbilityKey=="mana-shield"&&e.Amount==570m);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.AbsorbConsumed &&
            e.AbilityKey=="mana-shield"&&e.Amount==100m);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="mana-shield"&&e.EffectKey=="mana-shield-absorption-cost"&&
            e.Amount==-(decimal)manaSpent);
        Assert.Single(run.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey=="mana-shield");
    }

    [Fact]
    public void ManaShieldNeverConsumesManaOrAbsorbsMagicalDamage()
    {
        var run=new MageSimRunner().Run(ShieldBuild(2,100m,"fire"));
        Assert.Equal(0m,run.Run.CustomMetrics["mage-mana-shield-absorbed"]);
        Assert.Equal(0m,run.Run.CustomMetrics["mage-mana-shield-spent-mana"]);
        Assert.Equal(2900m,run.EndingHealth);
        Assert.Contains(run.Run.Timeline,e=>e.AbilityKey=="scripted-caster-incoming-damage"&&
            e.Type==CombatEventType.Damage&&e.Amount==100m&&e.AbsorbedAmount==0m);
    }

    [Fact]
    public void ManaShieldCannotAbsorbMoreDamageThanAvailableManaPaysFor()
    {
        var r=ShieldBuild(0,100m);
        r.StartingManaPercent=5m;
        var result=new MageSimRunner().Run(r);
        var absorbed=result.Run.CustomMetrics["mage-mana-shield-absorbed"];
        var spent=result.Run.CustomMetrics["mage-mana-shield-spent-mana"];
        Assert.InRange(absorbed,0m,100m);
        Assert.True(absorbed<100m);
        Assert.Equal(2m*absorbed,spent);
        Assert.Equal(r.MageMaximumHealth-(100m-absorbed),result.EndingHealth);
        Assert.True(result.EndingMana>=0m);
    }

    [Fact]
    public void UntalentedFireMageCanExplicitlyCastTrainerManaShield()
    {
        var r=MageSimRequest.CreatePreset("fire");
        r.DurationSeconds=3m;
        r.CustomPriority=["mana-shield","fireball"];
        r.UseEvocation=false;r.UseMajorManaPotion=false;
        r.CombatItems.ManaRubyCharges=0;
        var run=new MageSimRunner().Run(r);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.AbsorbApplied &&
            e.AbilityKey=="mana-shield");
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
