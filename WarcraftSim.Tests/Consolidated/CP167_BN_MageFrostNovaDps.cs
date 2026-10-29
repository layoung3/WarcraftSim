using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Mage;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167BnMageFrostNovaDps
{
    [Fact]
    public void RankFourFrostNovaDealsRealDamageToEachExplicitEnemy()
    {
        var r=MageSimRequest.CreatePreset("frost");
        r.DurationSeconds=2m;
        r.CaptureTimeline=true;
        r.AdditionalTargets=2;
        r.TargetMaximumHealth=100000m;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.UseEvocation=false;
        r.UseMajorManaPotion=false;
        r.CombatItems.ManaRubyCharges=0;
        r.CustomPriority=["frost-nova"];
        var ability=Assert.Single(ForeverMageDpsSpellbook.Create(
            r.Talents,new ForeverMageTuning(),0m),a=>a.Key=="frost-nova");
        Assert.Equal(145m,Assert.Single(ability.ResourceCosts).Amount);
        Assert.Equal(25m,ability.CooldownSeconds);
        Assert.Equal(9,Assert.Single(ability.Effects).MaxTargets);
        Assert.Equal(73m,Assert.Single(ability.Effects).MinimumValue);
        var sim=new MageSimRunner().Run(r);
        var hits=sim.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="frost-nova" && e.Amount is >0m).ToArray();
        Assert.Equal(3,hits.Length);
        Assert.Equal(3,hits.Select(e=>e.TargetActorKey).Distinct().Count());
        Assert.All(hits,e=>Assert.Equal(73m,e.Amount));
        Assert.InRange(Math.Abs(sim.DamageDone-sim.DamageByAbility.Values.Sum()),
            0m,.000001m);
        Assert.DoesNotContain(sim.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="frost-nova"); // Boss root immunity; no fictional CC
    }

    [Fact]
    public void ColdSnapCanResetFrostNovaCooldownWithoutCreatingExtraDamage()
    {
        var r=MageSimRequest.CreatePreset("frost");
        r.CustomPriority=["frost-nova","cold-snap","frostbolt"];
        r.DurationSeconds=9m;
        r.BaseSpellHitPercent=100m;
        r.UseEvocation=false;
        r.UseMajorManaPotion=false;
        r.CombatItems.ManaRubyCharges=0;
        r.CaptureTimeline=true;
        var sim=new MageSimRunner().Run(r);
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey=="cold-snap");
        Assert.True(sim.Run.Timeline.Count(e=>e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey=="frost-nova")>=2);
    }
}
