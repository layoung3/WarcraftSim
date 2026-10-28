using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Shaman;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167MShamanFireNovaAndWindfury
{
    [Fact]
    public void FireNovaMatchesCurrentRankAndRequiresOwnFireTotem()
    {
        var spell=Assert.Single(ForeverElementalShamanSpellbook.Create(
            new ForeverElementalShamanTalents(),new ForeverElementalShamanTuning(),0m),
            x=>x.Key=="fire-nova");
        Assert.Equal(375m,spell.ResourceCosts.Single().Amount);
        Assert.Equal(10m,spell.CooldownSeconds);
        Assert.Equal(239m*(1m+.05m*3m),spell.Effects.Single().MinimumValue);
        Assert.Contains("shaman-fire-totem-active",spell.RequiredSourceAuraKeys);
        Assert.Equal(9,spell.Effects.Single().MaxTargets);
    }

    [Fact]
    public void FireNovaExecutesAgainstMultipleEnemiesAfterSearingTotem()
    {
        var req=ElementalPreRaidProfileCatalog.Setup("throughput","raid-sustain");
        req.DurationSeconds=24m;req.CaptureTimeline=true;
        req.TargetCount=3;req.BaseSpellHitPercent=100m;
        req.SpellCritPercent=0m;req.UseLightningOverload=false;
        req.CustomPriority=["searing-totem","fire-nova","lightning-bolt"];
        var sim=new ElementalShamanSimRunner().Run(req);
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="shaman-fire-totem-state");
        var hits=sim.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="fire-nova" && e.Amount>0m).ToList();
        foreach(var target in new[] {"target","target-2","target-3"})
            Assert.Contains(hits,e=>e.TargetActorKey==target && e.SchoolKey=="fire");
        Assert.InRange(Math.Abs(sim.DamageDone-sim.DamageByAbility.Values.Sum()),
            0m,.000001m);
    }

    [Fact]
    public void WindfuryTotemMatchesCurrentClientCostAndLifetime()
    {
        var totem=ForeverShamanSharedAbilities.CreateWindfuryTotem();
        Assert.Equal(250m,totem.ResourceCosts.Single().Amount);
        Assert.Equal(300m,totem.Effects.Single().DurationSeconds);
        Assert.Equal(1m,totem.GlobalCooldownSeconds);
    }

    [Fact]
    public void WindfuryTotemProcsOnlyWhenDeployedAndNoWindfuryWeapon()
    {
        EnhancementShamanSimRequest req(bool deployed) => new() {
            DurationSeconds=25m,CaptureTimeline=true,
            MainHandImbue="none",UseWindfuryWeapon=false,
            UseWindfuryTotem=true,DeployWindfuryTotem=deployed,
            WindfuryTotemProcChancePercent=100m,
            UseStormstrike=false,UseEarthShock=false,UseRageOfTheFarseer=false,
            UseMaelstromLightningBolt=false,UseLightningBoltFiller=false,
            BaseMeleeHitPercent=100m,CriticalChancePercent=0m,
            ProvisionalGlancingChancePercent=0m,TargetArmor=0m
        };
        var deployed=new EnhancementShamanSimRunner().Run(req(true));
        var staticAssumption=new EnhancementShamanSimRunner().Run(req(false));
        Assert.Contains(deployed.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="windfury-totem");
        Assert.Contains(deployed.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="windfury-totem" && e.Amount==-250m);
        Assert.True(deployed.WindfuryTotemProcs>0);
        Assert.True(staticAssumption.WindfuryTotemProcs>0);
        Assert.Equal(0,deployed.WindfuryWeaponProcs);
    }
}
