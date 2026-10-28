using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Shaman;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167KShamanWeaponImbues
{
    private static EnhancementShamanSimRequest Setup(string main="none",string off="none")
    {
        return new EnhancementShamanSimRequest {
            DurationSeconds=24m,CaptureTimeline=true,DualWield=true,
            MainHandImbue=main,OffHandImbue=off,
            UseWindfuryWeapon=false,UseWindfuryTotem=false,
            UseRageOfTheFarseer=false,UseStormstrike=false,
            UseEarthShock=false,UseMaelstromLightningBolt=false,
            UseLightningBoltFiller=false,
            BaseMeleeHitPercent=100m,CriticalChancePercent=0m,
            ProvisionalGlancingChancePercent=0m,TargetArmor=0m,
            FrostbrandProcChancePercent=100m
        };
    }

    [Fact]
    public void RankAndCostsAreHandSpecific()
    {
        var main=ForeverShamanWeaponImbues.CreateForHand("main-hand");
        Assert.Equal(155m,Assert.Single(main,x=>x.Key=="flametongue-weapon-main-hand")
            .ResourceCosts.Single().Amount);
        Assert.Equal(150m,Assert.Single(main,x=>x.Key=="rockbiter-weapon-main-hand")
            .ResourceCosts.Single().Amount);
        Assert.Equal(160m,Assert.Single(main,x=>x.Key=="frostbrand-weapon-main-hand")
            .ResourceCosts.Single().Amount);
        Assert.All(main,x=>Assert.Equal(3600m,x.Effects.Single().DurationSeconds));
        Assert.All(ForeverShamanWeaponImbues.CreateForHand("off-hand"),
            x=>Assert.Contains("off-hand",x.Key));
    }

    [Fact]
    public void RockbiterMainHandRaisesOnlyMainHandWeaponDamage()
    {
        var baseRun=new EnhancementShamanSimRunner().Run(Setup());
        var rock=new EnhancementShamanSimRunner().Run(Setup("rockbiter"));
        Assert.Equal("rockbiter",rock.ActiveMainHandImbue);
        Assert.Equal("none",rock.ActiveOffHandImbue);
        Assert.True(rock.DamageByAbility.GetValueOrDefault("main-hand-auto")>
            baseRun.DamageByAbility.GetValueOrDefault("main-hand-auto"));
        Assert.Equal(baseRun.DamageByAbility.GetValueOrDefault("off-hand-auto"),
            rock.DamageByAbility.GetValueOrDefault("off-hand-auto"));
        Assert.Contains(rock.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="rockbiter-weapon-main-hand" && e.Amount==-150m);
    }

    [Fact]
    public void RockbiterOffHandDoesNotRaiseMainHandWeaponDamage()
    {
        var baseRun=new EnhancementShamanSimRunner().Run(Setup());
        var rock=new EnhancementShamanSimRunner().Run(Setup("none","rockbiter"));
        Assert.Equal("rockbiter",rock.ActiveOffHandImbue);
        Assert.True(rock.DamageByAbility.GetValueOrDefault("off-hand-auto")>
            baseRun.DamageByAbility.GetValueOrDefault("off-hand-auto"));
        Assert.Equal(baseRun.DamageByAbility.GetValueOrDefault("main-hand-auto"),
            rock.DamageByAbility.GetValueOrDefault("main-hand-auto"));
    }

    [Fact]
    public void FlametongueMainAndFrostbrandOffUseCorrectSchoolsWithoutExtraWeaponSwings()
    {
        var req=Setup("flametongue","frostbrand");
        var sim=new EnhancementShamanSimRunner().Run(req);
        Assert.Equal("flametongue",sim.ActiveMainHandImbue);
        Assert.Equal("frostbrand",sim.ActiveOffHandImbue);
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="flametongue-weapon-main-hand-proc" && e.SchoolKey=="fire");
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="frostbrand-weapon-off-hand-proc" && e.SchoolKey=="frost");
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="frostbrand-weapon-off-hand-proc");
        Assert.Equal(0,sim.WindfuryWeaponExtraAttacks);
        Assert.InRange(Math.Abs(sim.DamageDone-sim.DamageByAbility.Values.Sum()),0m,.000001m);
    }

    [Fact]
    public void ExplicitMainHandImbueOverridesLegacyWindfuryCheckbox()
    {
        var req=Setup("flametongue");
        req.UseWindfuryWeapon=true;
        req.Tuning.WindfuryWeaponProcChancePercent=100m;
        var sim=new EnhancementShamanSimRunner().Run(req);
        Assert.Equal(0,sim.WindfuryWeaponProcs);
        Assert.Equal(0,sim.WindfuryWeaponExtraAttacks);
        Assert.Equal("flametongue",sim.ActiveMainHandImbue);
    }

    [Fact]
    public void OffHandImbueIsRejectedWithoutAnOffHandWeapon()
    {
        var req=Setup("none","frostbrand");
        req.DualWield=false;
        Assert.Throws<ArgumentException>(()=>new EnhancementShamanSimRunner().Run(req));
    }
}
