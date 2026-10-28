using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Hunter;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167IHunterAspectVolley
{
    private static MarksmanshipHunterSimRequest AutoOnly(bool hawk)
    {
        return new MarksmanshipHunterSimRequest {
            DurationSeconds = 18m, CaptureTimeline = true,
            UseAspectOfTheHawk = hawk, UseVolley = false,
            UseRapidFire = false, UseSerpentSting = false,
            UseSniperShot = false, UseMultiShot = false,
            UseAimedShot = false, UseArcaneShot = false,
            Tuning = new ForeverMarksmanshipHunterTuning {
                HitChancePercent = 100m, CriticalChancePercent = 0m,
                TargetArmor = 0m
            }
        };
    }

    [Fact]
    public void HawkAndVolleyUseVerifiedLevel60Ranks()
    {
        var hawk = ForeverHunterSharedAbilities.CreateAspectOfTheHawk();
        Assert.Equal(120m, hawk.ResourceCosts.Single().Amount);
        Assert.Equal("hunter-aspect-hawk", hawk.Effects.Single().AuraKey);
        var volley = ForeverHunterSharedAbilities.CreateVolley(1.1m);
        Assert.Equal(490m, volley.ResourceCosts.Single().Amount);
        Assert.Equal(6m, volley.ChannelDurationSeconds);
        Assert.Equal(1m, volley.ChannelTickIntervalSeconds);
        Assert.Equal(112m, volley.Effects.Single().MinimumValue);
        Assert.Equal(1.1m, volley.Effects.Single().DamageMultiplier);
        Assert.Equal(9, volley.Effects.Single().MaxTargets);
    }

    [Fact]
    public void HawkActuallyRaisesAutoShotDamageAndCostsMana()
    {
        var baseline = new MarksmanshipHunterSimRunner().Run(AutoOnly(false));
        var empowered = new MarksmanshipHunterSimRunner().Run(AutoOnly(true));
        Assert.Equal(baseline.RangedAttackPower + 120m, empowered.RangedAttackPower);
        Assert.Contains(empowered.Run.Timeline, e =>
            e.Type == CombatEventType.ResourceChanged && e.AbilityKey == "aspect-of-the-hawk" &&
            e.ResourceKey == "mana" && e.Amount == -120m);
        Assert.Contains(empowered.Run.Timeline, e =>
            e.Type == CombatEventType.AuraApplied && e.AbilityKey == "aspect-of-the-hawk");
        var baseAuto = baseline.DamageByAbility.GetValueOrDefault("auto-shot");
        var buffedAuto = empowered.DamageByAbility.GetValueOrDefault("auto-shot");
        Assert.True(buffedAuto > baseAuto);
        Assert.InRange(Math.Abs(empowered.DamageDone - empowered.DamageByAbility.Values.Sum()),
            0m,.000001m);
    }

    [Fact]
    public void VolleyChannelsArcaneHitsIndependentlyAgainstExplicitEnemies()
    {
        var request = AutoOnly(false);
        request.UseVolley = true;
        request.AdditionalTargets = 2;
        request.DurationSeconds = 7.5m;
        var sim = new MarksmanshipHunterSimRunner().Run(request);
        var pulses = sim.Run.Timeline.Where(e => e.Type == CombatEventType.Damage &&
            e.AbilityKey == "volley" && e.IsChannelTick && e.Amount is > 0m).ToList();
        Assert.Contains(pulses,e=>e.TargetActorKey=="target");
        Assert.Contains(pulses,e=>e.TargetActorKey=="add-1");
        Assert.Contains(pulses,e=>e.TargetActorKey=="add-2");
        Assert.All(pulses,e=>Assert.Equal("arcane",e.SchoolKey));
        Assert.InRange(Math.Abs(sim.DamageDone - sim.DamageByAbility.Values.Sum()),0m,.000001m);
    }

    [Fact]
    public void MarksmanshipPhysicalShotsNowScaleFromLiveAttackPower()
    {
        var t = new ForeverMarksmanshipHunterTuning();
        var spells = ForeverMarksmanshipHunterSpellbook.Create(
            new ForeverMarksmanshipHunterTalents(),t,1200m);
        foreach(var key in new[] {"aimed-shot","multi-shot","sniper-shot"})
        {
            var effect = Assert.Single(spells,a=>a.Key==key).Effects.Single();
            Assert.Equal("attack-power",effect.ScalingStatKey);
            Assert.True(effect.ScalingCoefficient > 0m);
        }
    }
}
