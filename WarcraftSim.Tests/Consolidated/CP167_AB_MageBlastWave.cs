using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Mage;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167AbMageBlastWave
{
    [Fact]
    public void CurrentRankFiveBlastWaveIsTalentGatedAndHasVerifiedCostCooldown()
    {
        var talents=ForeverMageTalentBuild.Default("fire");
        var none=ForeverMageDpsSpellbook.Create(talents,new ForeverMageTuning(),0m);
        Assert.DoesNotContain(none,x=>x.Key=="blast-wave");
        talents.Ranks["blast-wave"]=1;
        talents.Validate("fire");
        var learned=ForeverMageDpsSpellbook.Create(talents,new ForeverMageTuning(),0m);
        var wave=Assert.Single(learned,x=>x.Key=="blast-wave");
        Assert.Equal(545m,wave.ResourceCosts.Single().Amount);
        Assert.Equal(45m,wave.CooldownSeconds);
        Assert.Equal(0m,wave.CastTimeSeconds);
        Assert.Equal(493m,wave.Effects.Single(x=>x.Key=="blast-wave-impact").MinimumValue);
        Assert.Equal(9,wave.Effects.Single(x=>x.Key=="blast-wave-impact").MaxTargets);
        Assert.Equal(6m,wave.Effects.Single(x=>x.Key=="blast-wave-daze").DurationSeconds);
        Assert.Equal("blast-wave-impact",wave.Effects.Single(x=>x.Key=="blast-wave-daze").DependsOnEffectKey);
    }

    [Theory]
    [InlineData(100,true)]
    [InlineData(0,false)]
    public void BlastWaveRequiresLandedHitToDazeEachExplicitEnemy(int hit,bool landed)
    {
        var req=MageSimRequest.CreatePreset("fire");
        req.Talents.Ranks["blast-wave"]=1;
        req.DurationSeconds=7m;
        req.AdditionalTargets=2;
        req.BaseSpellHitPercent=hit;
        req.SpellCritPercent=0m;
        req.UseEvocation=false;req.UseMajorManaPotion=false;
        req.CombatItems.ManaRubyCharges=0;
        req.CustomPriority=["blast-wave"];
        var sim=new MageSimRunner().Run(req);
        foreach(var target in new[]{"target","target-1","target-2"})
        {
            var applied=sim.Run.Timeline.Any(e=>e.Type==CombatEventType.AuraApplied &&
                e.AbilityKey=="blast-wave" && e.TargetActorKey==target &&
                e.EffectKey=="blast-wave-daze");
            Assert.Equal(landed,applied);
        }
        Assert.Single(sim.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="blast-wave" && e.ResourceKey=="mana" && e.Amount==-545m);
        Assert.InRange(Math.Abs(sim.DamageDone-sim.DamageByAbility.Values.Sum()),0m,.000001m);
    }
}
