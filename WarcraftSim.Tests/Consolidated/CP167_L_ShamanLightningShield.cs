using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Shaman;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167LShamanLightningShield
{
    [Fact]
    public void LightningShieldHasCurrentRankAndManaAndDuration()
    {
        var shield=ForeverShamanSharedAbilities.CreateLightningShield();
        Assert.Equal(370m,shield.ResourceCosts.Single().Amount);
        Assert.Equal(600m,shield.Effects.Single().DurationSeconds);
        Assert.Contains("shaman-lightning-shield",shield.ForbiddenSourceAuraKeys);
        Assert.Contains(ForeverShamanWeaponImbues.CreateForHand("main-hand"),
            a=>a.Key=="rockbiter-weapon-main-hand");
    }

    [Fact]
    public void EnhancementLightningShieldActuallyReactsToThreeIncomingHits()
    {
        var req=new EnhancementShamanSimRequest {
            DurationSeconds=14m,CaptureTimeline=true,
            UseLightningShield=true,IncomingBossHitDamage=100m,
            IncomingBossSwingSeconds=4m,LightningShieldInternalCooldownSeconds=3m,
            UseWindfuryWeapon=false,UseWindfuryTotem=false,UseStormstrike=false,
            UseEarthShock=false,UseMaelstromLightningBolt=false,
            UseLightningBoltFiller=false,UseRageOfTheFarseer=false
        };
        var sim=new EnhancementShamanSimRunner().Run(req);
        var reacts=sim.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="lightning-shield" && e.SourceActorKey=="shaman").ToList();
        Assert.Equal(3,reacts.Count);
        Assert.All(reacts,e=>Assert.Equal(178m,e.Amount));
        Assert.Equal(534m,sim.DamageByAbility.GetValueOrDefault("lightning-shield"));
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.AuraRemoved &&
            e.AbilityKey=="lightning-shield");
        Assert.InRange(Math.Abs(sim.DamageDone-sim.DamageByAbility.Values.Sum()),
            0m,.000001m);
    }

    [Fact]
    public void ElementalLightningShieldUsesSameThreeChargeMechanic()
    {
        var req=ElementalPreRaidProfileCatalog.Setup("throughput","raid-sustain");
        req.DurationSeconds=14m;
        req.CaptureTimeline=true;
        req.IncomingBossHitDamage=100m;
        req.IncomingBossSwingSeconds=4m;
        req.UseLightningShield=true;
        req.LightningShieldInternalCooldownSeconds=3m;
        req.CustomPriority=["lightning-shield","lightning-bolt"];
        var sim=new ElementalShamanSimRunner().Run(req);
        var reacts=sim.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="lightning-shield" && e.SourceActorKey=="shaman").ToList();
        Assert.Equal(3,reacts.Count);
        Assert.All(reacts,e=>Assert.Equal(178m,e.Amount));
        Assert.InRange(Math.Abs(sim.DamageDone-sim.DamageByAbility.Values.Sum()),
            0m,.000001m);
    }

    [Fact]
    public void ShieldDoesNotTriggerWithoutIncomingDamage()
    {
        var req=new EnhancementShamanSimRequest {
            DurationSeconds=8m,CaptureTimeline=true,UseLightningShield=true,
            UseWindfuryWeapon=false,UseWindfuryTotem=false,
            UseStormstrike=false,UseEarthShock=false,
            UseMaelstromLightningBolt=false,UseLightningBoltFiller=false,
            UseRageOfTheFarseer=false
        };
        var sim=new EnhancementShamanSimRunner().Run(req);
        Assert.DoesNotContain(sim.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="lightning-shield");
    }
}
