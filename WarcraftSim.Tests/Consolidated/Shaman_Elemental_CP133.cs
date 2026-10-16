using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests;

public sealed class ShamanElementalCp133Tests
{
    private static ElementalShamanSimResponse Run(ElementalShamanSimRequest? r=null)=>
        new ElementalShamanSimRunner().Run(r??new ElementalShamanSimRequest());

    [Fact]
    public void ElementalDealsDamageAndReconcilesAbilityReporting()
    {
        var result=Run();
        Assert.True(result.DamageDone>0m);
        Assert.Equal(result.DamageDone,result.Run.Summary.ActorSummaries["shaman"].DamageDone);
        Assert.InRange(Math.Abs(result.DamageDone-result.DamageByAbility.Values.Sum()),0m,.000001m);
        Assert.Equal(result.DamageDone,result.Run.Summary.ActorSummaries["target"].DamageTaken);
    }

    [Fact]
    public void ElementalMaintainsFlameShockAsPeriodicDamage()
    {
        var r=new ElementalShamanSimRequest{BaseSpellHitPercent=100m,FlameShockPolicy="always"};
        var result=Run(r);
        Assert.Contains(result.Run.Timeline,ev=>ev.AbilityKey=="flame-shock"&&ev.IsPeriodic&&
            ev.Type==CombatEventType.Damage);
    }

    [Fact]
    public void LavaBurstGetsFlameShockDamageBonusWhenAuraPresent()
    {
        var r=new ElementalShamanSimRequest {BaseSpellHitPercent=100m,SpellCritPercent=0m,
            UseChainLightning=false,UseLightningOverload=false,FlameShockPolicy="always"};
        var result=Run(r);
        Assert.True(result.LavaBurstFlameShockBuffed>0);
        Assert.True(result.DamageByAbility.GetValueOrDefault("lava-burst")>0m);
    }

    [Fact]
    public void LavaBurstNoFlameShockHasNoConditionalBonuses()
    {
        var r=new ElementalShamanSimRequest{BaseSpellHitPercent=100m,SpellCritPercent=0m,
            UseFlameShock=false,UseChainLightning=false,UseLightningOverload=false};
        var result=Run(r);
        Assert.Equal(0,result.LavaBurstFlameShockBuffed);
        Assert.DoesNotContain(result.Run.Timeline,ev=>ev.AbilityKey=="flame-shock");
    }

    [Fact]
    public void LightningOverloadProducesSeparateAttributedHalfDamageAttacks()
    {
        var r=new ElementalShamanSimRequest{BaseSpellHitPercent=100m,
            UseLavaBurst=false,UseChainLightning=false,UseFlameShock=false};
        r.Tuning.LightningOverloadChancePercentRank3=100m;
        var result=Run(r);
        Assert.True(result.LightningOverloadProcs>0);
        Assert.True(result.DamageByAbility.GetValueOrDefault("lightning-overload-bolt")>0m);
    }

    [Fact]
    public void LightningOverloadCanBeDisabled()
    {
        var r=new ElementalShamanSimRequest{UseLightningOverload=false};
        var result=Run(r);
        Assert.Equal(0,result.LightningOverloadProcs);
        Assert.DoesNotContain(result.Run.Timeline,ev=>ev.AbilityKey=="lightning-overload-bolt");
    }

    [Fact]
    public void ElementalMasteryNowExistsInOctober16Tree()
    {
        var result=Run(new ElementalShamanSimRequest());
        Assert.True(result.ElementalMasteryCasts>0);
        Assert.Contains(result.Run.Timeline,e=>e.AbilityKey=="elemental-mastery" &&
            e.Type==CombatEventType.AbilityCastCompleted);
    }

    [Fact]
    public void ManaPotionIsARealLimitedCombatAbility()
    {
        var r=new ElementalShamanSimRequest {MajorManaPotionCharges=1,
            MajorManaPotionBelowManaPercent=95m};
        var result=Run(r);
        Assert.Equal(1,result.MajorManaPotionUses);
        Assert.Contains(result.Run.Timeline,ev=>ev.Type==CombatEventType.ResourceChanged&&
            ev.AbilityKey=="major-mana-potion"&&ev.Amount>0m);
    }

    [Fact]
    public void SameSeedProducesSameElementalDamage()
    {
        var r=new ElementalShamanSimRequest();
        Assert.Equal(Run(r).DamageDone,Run(r).DamageDone);
    }
}
