using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests;

public sealed class CasterSingleTargetOptimizationCp135Tests
{
    [Fact]
    public void ElementalSingleTargetDefaultUsesEfficientLightningBoltInsteadOfChainLightning()
    {
        var request = new ElementalShamanSimRequest();
        // Adaptive priority suppresses Chain Lightning on one target even if enabled.
        var result = new ElementalShamanSimRunner().Run(request);
        Assert.Contains(result.Run.Timeline, e => e.AbilityKey=="lightning-bolt" &&
            e.Type==CombatEventType.AbilityCastCompleted);
        Assert.DoesNotContain(result.Run.Timeline, e => e.AbilityKey=="chain-lightning");
    }

    [Fact]
    public void ElementalCanEnableChainLightningExplicitly()
    {
        var request = new ElementalShamanSimRequest { UseChainLightning=true, UseManaSpringTotem=false, RotationPolicy="legacy" };
        var result = new ElementalShamanSimRunner().Run(request);
        Assert.Contains(result.Run.Timeline, e => e.AbilityKey=="chain-lightning" &&
            e.Type==CombatEventType.AbilityCastCompleted);
    }

    [Fact]
    public void ManaSpringIsRealSelfPeriodicResourceGainAndCanBeDisabled()
    {
        var enabled = new ElementalShamanSimRunner().Run(new ElementalShamanSimRequest { BaseSpellHitPercent=100m });
        Assert.Contains(enabled.Run.Timeline,e=>e.AbilityKey=="mana-spring-totem"&&
            e.Type==CombatEventType.AbilityCastCompleted);
        Assert.Contains(enabled.Run.Timeline,e=>e.AbilityKey=="mana-spring-totem"&&
            e.Type==CombatEventType.ResourceChanged&&e.Amount>0m);
        var disabled = new ElementalShamanSimRunner().Run(new ElementalShamanSimRequest { UseManaSpringTotem=false });
        Assert.DoesNotContain(disabled.Run.Timeline,e=>e.AbilityKey=="mana-spring-totem");
    }

    [Fact]
    public void BalanceManaConservationMaintainsEclipseWithoutBreakingDamageAccounting()
    {
        var result=new BalanceDruidSimRunner().Run(new BalanceDruidSimRequest { BaseSpellHitPercent=100m });
        Assert.True(result.EclipseChargesGenerated>0);
        Assert.True(result.EclipseEmpoweredStarfires>0);
        Assert.InRange(Math.Abs(result.DamageDone-result.DamageByAbility.Values.Sum()),0m,.000001m);
    }

    [Fact]
    public void BalanceCanDisableManaBudgetingForUnrestrictedEclipseSpending()
    {
        var conservative=new BalanceDruidSimRequest { UseMoonfire=false,UseInsectSwarm=false,
            ConserveManaForEncounter=true };
        var unrestricted=new BalanceDruidSimRequest { UseMoonfire=false,UseInsectSwarm=false,
            ConserveManaForEncounter=false };
        var a=new BalanceDruidSimRunner().Run(conservative);
        var b=new BalanceDruidSimRunner().Run(unrestricted);
        Assert.True(a.EclipseEmpoweredStarfires>0);
        Assert.True(b.EclipseEmpoweredStarfires>0);
        Assert.True(a.DamageDone>=0m && b.DamageDone>=0m);
    }
}
