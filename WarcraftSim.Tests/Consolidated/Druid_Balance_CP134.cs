using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests;

public sealed class DruidBalanceCp134Tests
{
    private static BalanceDruidSimResponse Run(BalanceDruidSimRequest? r=null)=>
        new BalanceDruidSimRunner().Run(r??new BalanceDruidSimRequest());

    [Fact]
    public void BalanceDealsDamageAndReconcilesAbilityReporting()
    {
        var result=Run();
        Assert.True(result.DamageDone>0m);
        Assert.Equal(result.DamageDone,result.Run.Summary.ActorSummaries["druid"].DamageDone);
        Assert.InRange(Math.Abs(result.DamageDone-result.DamageByAbility.Values.Sum()),0m,.000001m);
        Assert.Equal(result.DamageDone,result.Run.Summary.ActorSummaries["target"].DamageTaken);
    }

    [Fact]
    public void EclipseEmpowersActualStarfireCasts()
    {
        var r=new BalanceDruidSimRequest{BaseSpellHitPercent=100m};
        var result=Run(r);
        Assert.True(result.EclipseChargesGenerated>0);
        Assert.True(result.EclipseEmpoweredStarfires>0);
        Assert.InRange(result.MaximumEclipseCharges,2,4);
        Assert.Contains(result.Run.Timeline,ev=>ev.AbilityKey=="starfire"&&
            ev.Type==CombatEventType.AbilityCastCompleted);
    }

    [Fact]
    public void EclipseReducesStarfireCastDuration()
    {
        var r=new BalanceDruidSimRequest{BaseSpellHitPercent=100m,UseMoonfire=false,
            UseInsectSwarm=false};
        var result=Run(r);
        var starts=result.Run.Timeline.Where(e=>e.Type==CombatEventType.AbilityCastStarted&&
            e.AbilityKey=="starfire").ToList();
        var ends=result.Run.Timeline.Where(e=>e.Type==CombatEventType.AbilityCastCompleted&&
            e.AbilityKey=="starfire").ToList();
        Assert.NotEmpty(starts);Assert.NotEmpty(ends);
        Assert.InRange(ends[0].TimeSeconds-starts[0].TimeSeconds,2.99m,3.01m);
    }

    [Fact]
    public void DisablingEclipseForcesWrathOnlyFiller()
    {
        var r=new BalanceDruidSimRequest{UseEclipse=false,
            UseMoonfire=false,UseInsectSwarm=false};
        var result=Run(r);
        Assert.Equal(0,result.EclipseEmpoweredStarfires);
        Assert.True(result.DamageByAbility.GetValueOrDefault("wrath")>0m);
        Assert.DoesNotContain(result.Run.Timeline,ev=>ev.AbilityKey=="starfire"&&
            ev.Type==CombatEventType.AbilityCastStarted);
    }

    [Fact]
    public void MoonfireAndInsectSwarmUseRealPeriodicTicks()
    {
        var result=Run(new BalanceDruidSimRequest{BaseSpellHitPercent=100m});
        Assert.Contains(result.Run.Timeline,e=>e.AbilityKey=="moonfire"&&e.IsPeriodic&&
            e.Type==CombatEventType.Damage);
        Assert.Contains(result.Run.Timeline,e=>e.AbilityKey=="insect-swarm"&&e.IsPeriodic&&
            e.Type==CombatEventType.Damage);
    }

    [Fact]
    public void ClearingInsectSwarmTalentPreventsItBeingCast()
    {
        var r=new BalanceDruidSimRequest();r.Talents.Ranks.Remove("insect-swarm");
        var result=Run(r);
        Assert.DoesNotContain(result.Run.Timeline,e=>e.AbilityKey=="insect-swarm");
    }

    [Fact]
    public void InvalidTreeAllocationIsRejected()
    {
        var r=new BalanceDruidSimRequest();r.Talents.BalancePointsSpent=52;
        Assert.Throws<ArgumentException>(()=>Run(r));
    }

    [Fact]
    public void ManaPotionAvailableToBalanceWithoutConsumingEclipseCharges()
    {
        var r=new BalanceDruidSimRequest{MajorManaPotionCharges=1,
            MajorManaPotionBelowManaPercent=95m};
        var result=Run(r);
        Assert.Equal(1,result.MajorManaPotionUses);
        Assert.True(result.EclipseEmpoweredStarfires>0);
    }

    [Fact]
    public void SameSeedProducesSameBalanceDamage()
    {
        var r=new BalanceDruidSimRequest();
        Assert.Equal(Run(r).DamageDone,Run(r).DamageDone);
    }
}
