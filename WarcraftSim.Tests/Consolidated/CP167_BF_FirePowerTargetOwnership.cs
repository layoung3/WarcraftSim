using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167BfFirePowerTargetOwnership
{
    private static MageSimRequest Request(int rank)
    {
        var r=MageSimRequest.CreatePreset("fire");
        r.Talents.Ranks["fire-power"]=rank;
        r.Talents.Ranks["critical-mass"]=0;
        r.Talents.Ranks["combustion"]=0;
        r.UseIgnite=false;
        r.UseHeatingUp=false;
        r.UseEvocation=false;
        r.UseMajorManaPotion=false;
        r.CombatItems.ManaRubyCharges=0;
        r.Character.BaseStats.Values["spell-power"]=0m;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.StartingManaPercent=11m;
        r.DurationSeconds=13m;
        r.CaptureTimeline=true;
        r.CustomPriority=["fireball"];
        return r;
    }

    [Theory]
    [InlineData(1,1.01)]
    [InlineData(2,1.02)]
    [InlineData(3,1.03)]
    public void FirePowerUsesActiveOwnerFireballDotAtResolutionButNotItsPriorImpact(
        int rank,double expected)
    {
        var plain=new MageSimRunner().Run(Request(0));
        var empowered=new MageSimRunner().Run(Request(rank));
        decimal Direct(MageSimResponse sim)=>sim.Run.Timeline.First(e=>
            e.Type==CombatEventType.Damage && e.AbilityKey=="fireball" &&
            !e.IsPeriodic && e.Amount is >0m).Amount!.Value;
        decimal Tick(MageSimResponse sim)=>sim.Run.Timeline.First(e=>
            e.Type==CombatEventType.Damage && e.AbilityKey=="fireball" &&
            e.IsPeriodic && e.Amount is >0m).Amount!.Value;
        Assert.Equal(Direct(plain),Direct(empowered));
        Assert.Equal(Tick(plain)*(decimal)expected,Tick(empowered));
        Assert.Contains(empowered.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="fireball" && e.TargetActorKey=="target");
        Assert.InRange(Math.Abs(empowered.DamageDone-empowered.DamageByAbility.Values.Sum()),
            0m,.000001m);
    }

    [Fact]
    public void FirePowerNeverCreatesAnUnownedFireEffectOnACompleteMiss()
    {
        var r=Request(3);
        r.BaseSpellHitPercent=0m;
        var run=new MageSimRunner().Run(r);
        Assert.DoesNotContain(run.Run.Timeline,e=>
            e.Type==CombatEventType.AuraApplied && e.AbilityKey=="fireball");
        Assert.Equal(0m,run.DamageDone);
    }
}
