using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Druid;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167CDruidCooldowns
{
    [Fact]
    public void SharedDruidCooldownsMatchCurrentClientTimeAndBaseManaRules()
    {
        var innervate=ForeverDruidSharedCooldowns.CreateInnervate();
        var barkskin=ForeverDruidSharedCooldowns.CreateBarkskin();
        Assert.Equal(360m,innervate.CooldownSeconds);
        Assert.Equal(20m,innervate.Effects.Single().DurationSeconds);
        Assert.Equal(1244m*.05m,innervate.ResourceCosts.Single().Amount);
        Assert.Equal(60m,barkskin.CooldownSeconds);
        Assert.Equal(15m,barkskin.Effects.Single().DurationSeconds);
        Assert.Empty(barkskin.ResourceCosts);
    }

    [Fact]
    public void BalanceInnervateActuallyRegeneratesMoreManaThanWithoutIt()
    {
        var with=new BalanceDruidSimRequest {
            DurationSeconds=65m, CaptureTimeline=true, UseInnervate=true,
            UseMoonfire=false,UseInsectSwarm=false,UseStarfire=false,
            UseHurricane=false,UseOmenOfClarity=false,
            UseEclipse=false,UseFaerieFire=false,UseThorns=false,
            UseBarkskin=false, StartingManaPercent=25m,
            CustomPriority=["innervate","wrath"]
        };
        var baseline=new BalanceDruidSimRequest {
            DurationSeconds=65m,CaptureTimeline=true,UseInnervate=false,
            UseMoonfire=false,UseInsectSwarm=false,UseStarfire=false,
            UseHurricane=false,UseOmenOfClarity=false,
            UseEclipse=false,UseFaerieFire=false,UseThorns=false,
            UseBarkskin=false,StartingManaPercent=25m,
            CustomPriority=["wrath"]
        };
        var a=new BalanceDruidSimRunner().Run(with);
        var b=new BalanceDruidSimRunner().Run(baseline);
        Assert.Contains(a.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="innervate");
        Assert.True(a.EndingMana>b.EndingMana);
    }

    [Fact]
    public void BarkskinReducesOnlyPhysicalScriptedDamage()
    {
        var request=new BalanceDruidSimRequest {
            DurationSeconds=11m, CaptureTimeline=true,
            UseBarkskin=true, UseInnervate=false,
            IncomingBossHitDamage=200m, BossSwingIntervalSeconds=3m,
            UseMoonfire=false, UseInsectSwarm=false,UseStarfire=false,
            UseHurricane=false, UseFaerieFire=false,UseThorns=false,
            CustomPriority=["barkskin","wrath"]
        };
        var result=new BalanceDruidSimRunner().Run(request);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="barkskin");
        var incoming=result.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.TargetActorKey=="druid" && e.EncounterEventKey=="balance-incoming-test").ToList();
        Assert.NotEmpty(incoming);
        Assert.All(incoming,e=>Assert.Equal(160m,e.Amount));
    }

    [Fact]
    public void RestorationCooldownSpellbookExecutesInHealerSimulation()
    {
        var req=new RestorationDruidSimRequest {
            DurationSeconds=65m,CaptureTimeline=true,
            UseInnervate=true,UseBarkskin=false,
            IncomingDamage=1000m,DamageEverySeconds=2m
        };
        var result=new RestorationDruidSimRunner().Run(req);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="innervate");
        Assert.True(result.HealingDone>0m);
    }
}
