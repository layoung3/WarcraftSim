using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167BrMageIgniteEligibleFireCrits
{
    private static MageSimRequest Setup(string spell,int additionalTargets)
    {
        var r=MageSimRequest.CreatePreset("fire");
        r.DurationSeconds=13m;
        r.TargetMaximumHealth=1000000m;
        r.AdditionalTargets=additionalTargets;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=100m;
        r.UseEvocation=false;
        r.UseMajorManaPotion=false;
        r.CombatItems.ManaRubyCharges=0;
        r.UseIgnite=true;
        r.UseHeatingUp=false;
        r.UseCombustion=false;
        r.Talents.Ranks["ignite"]=5;
        r.Talents.Ranks["fire-power"]=0;
        r.Tuning.ManaRegenPerSecond=1000m;
        r.CaptureTimeline=true;
        if(spell=="blast-wave")r.Talents.Ranks["blast-wave"]=1;
        r.CustomPriority=[spell];
        return r;
    }

    [Fact]
    public void ScorchDirectFireCritActuallyTriggersIgniteDamage()
    {
        var run=new MageSimRunner().Run(Setup("scorch",0));
        var scorch=run.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="scorch" && e.IsCritical && e.Amount is >0m).ToArray();
        Assert.NotEmpty(scorch);
        var ignite=run.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="mage-ignite" && e.IsPeriodic && e.Amount is >0m).ToArray();
        Assert.NotEmpty(ignite);
        Assert.All(ignite,e=>Assert.Equal("target",e.TargetActorKey));
        Assert.InRange(Math.Abs(run.DamageDone-run.DamageByAbility.Values.Sum()),
            0m,.000001m);
    }

    [Theory]
    [InlineData("flamestrike")]
    [InlineData("blast-wave")]
    public void EveryLandedDirectFireAoeCritCreatesAnIndependentTargetIgnite(string spell)
    {
        var run=new MageSimRunner().Run(Setup(spell,1));
        var direct=run.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey==spell && !e.IsPeriodic && e.IsCritical &&
            e.Amount is >0m).ToArray();
        Assert.Equal(2,direct.Length);
        var ignite=run.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="mage-ignite" && e.IsPeriodic &&
            e.Amount is >0m).ToArray();
        Assert.Contains(ignite,e=>e.TargetActorKey=="target");
        Assert.Contains(ignite,e=>e.TargetActorKey=="target-1");
        Assert.All(ignite,e=>Assert.True(e.Amount>0m));
        Assert.InRange(Math.Abs(run.DamageDone-run.DamageByAbility.Values.Sum()),
            0m,.000001m);
    }
}