using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Druid;
using WarcraftSim.Data.Forever.Shaman;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>Executable CP166 Stage 4D invariants across the seven existing specs.
/// Mechanics not available in the Forever client/solo model are documented in
/// the audit matrix, not silently tested as if fully implemented.</summary>
public sealed class Cp166Stage4DShamanDruidAudit
{
    [Fact]
    public void ElementalDamageDoTOverloadManaAndApiResponseReconcile()
    {
        var req=ElementalPreRaidProfileCatalog.Setup("throughput","raid-sustain");
        req.DurationSeconds=90m; req.CaptureTimeline=true;
        req.TargetCount=3;req.BaseSpellHitPercent=100m;
        req.UseLightningOverload=true;
        req.Tuning.LightningOverloadChancePercentRank3=100m;
        req.UseChainLightning=true;
        req.CustomPriority=["chain-lightning","flame-shock","lightning-bolt"];
        var sim=new ElementalShamanSimRunner().Run(req);
        Assert.True(sim.DamageDone>0m);
        Assert.True(sim.LightningOverloadProcs>0);
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="chain-lightning");
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="flame-shock" && e.IsPeriodic);
        Assert.Equal(sim.Run.Summary.ActorSummaries["shaman"].DamageDone,sim.DamageDone);
        Assert.InRange(Math.Abs(sim.DamageByAbility.Values.Sum()-sim.DamageDone),
            0m,.000001m);
        Assert.Contains("DamageByAbility",JsonSerializer.Serialize(sim));
    }

    [Fact]
    public void OldCallOfThunderIsRejectedAndOverloadRequiresThreeAlacrityRanks()
    {
        var t=ElementalPreRaidProfileCatalog.Talents("raid-sustain");
        t.Ranks["call-of-thunder"]=1;
        Assert.Throws<ArgumentException>(()=>t.Validate());
        t.Ranks.Remove("call-of-thunder");
        t.Ranks["elemental-alacrity"]=2;
        Assert.Throws<ArgumentException>(()=>t.Validate());
        t.Ranks["elemental-alacrity"]=3;
        t.Validate();
    }

    [Fact]
    public void EnhancementOffHandIsARealAndSeparatelyReportedWeaponStream()
    {
        var req=new EnhancementShamanSimRequest {
            DurationSeconds=35m,CaptureTimeline=true,DualWield=true,
            BaseMeleeHitPercent=100m,CriticalChancePercent=0m,
            ProvisionalGlancingChancePercent=0m,
            UseWindfuryWeapon=false,UseWindfuryTotem=false,
            UseStormstrike=false,UseEarthShock=false,
            UseMaelstromLightningBolt=false
        };
        var sim=new EnhancementShamanSimRunner().Run(req);
        Assert.True(sim.DualWieldActive);
        Assert.True(sim.OffHandAutoDamage>0m);
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="off-hand-auto" && e.WeaponHandKey=="off-hand" &&
            e.Amount is >0m);
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="main-hand-auto");
        Assert.Equal(sim.DamageByAbility["off-hand-auto"],sim.OffHandAutoDamage);
        Assert.InRange(Math.Abs(sim.DamageDone-sim.DamageByAbility.Values.Sum()),0m,.000001m);
        Assert.Contains("OffHandAutoDamage",JsonSerializer.Serialize(sim));
        req.DualWield=false;
        var twoHand=new EnhancementShamanSimRunner().Run(req);
        Assert.False(twoHand.DualWieldActive);
        Assert.DoesNotContain(twoHand.Run.Timeline,e=>e.AbilityKey=="off-hand-auto");
    }

    [Fact]
    public void EnhancementWindfuryExtraSwingsNeverRecursivelyProcAndSuppressTotem()
    {
        var req=new EnhancementShamanSimRequest {
            DurationSeconds=30m,CaptureTimeline=true,BaseMeleeHitPercent=100m,
            CriticalChancePercent=0m,ProvisionalGlancingChancePercent=0m,
            UseWindfuryWeapon=true,UseWindfuryTotem=true,
            UseStormstrike=false,UseEarthShock=false,
            UseMaelstromLightningBolt=false
        };
        req.Tuning.WindfuryWeaponProcChancePercent=100m;
        req.Tuning.WindfuryWeaponInternalCooldownSeconds=0m;
        var sim=new EnhancementShamanSimRunner().Run(req);
        Assert.True(sim.WindfuryWeaponProcs>0);
        Assert.Equal(sim.WindfuryWeaponProcs*2,sim.WindfuryWeaponExtraAttacks);
        Assert.Equal(0,sim.WindfuryTotemProcs);
        Assert.DoesNotContain(sim.Run.Timeline,e=>e.AbilityKey=="windfury-totem-extra");
    }

    [Fact]
    public void RestorationShamanStreamsHealEachLivingGroupMemberEveryTwoSeconds()
    {
        var talents=new ForeverRestorationShamanTalents();
        var tuning=new ForeverRestorationShamanTuning();
        var stream=ForeverRestorationShamanSpellbook.Create(talents,tuning)
            .Single(a=>a.Key=="healing-stream-totem");
        Assert.Equal(300m,stream.Effects[0].DurationSeconds);
        Assert.Equal(80m,stream.ResourceCosts.Single().Amount);
        Assert.Equal(11m,tuning.HealingStreamTick);
        var req=new RestorationShamanSimRequest {
            DurationSeconds=32m,CaptureTimeline=true,AdditionalAllies=2,
            AllyInitialHealthPercent=50m,IncomingDamage=300m,
            DamageEverySeconds=6m,FirstDamageAtSeconds=2m
        };
        req.Tuning.HealingCritPercent=0m;
        var sim=new RestorationShamanSimRunner().Run(req);
        var pulses=sim.Run.Timeline.Where(e=>e.Type==CombatEventType.Healing &&
            e.AbilityKey=="healing-stream-totem").ToArray();
        Assert.NotEmpty(pulses);
        var group=pulses.GroupBy(e=>e.TimeSeconds)
            .First(g=>g.Select(e=>e.TargetActorKey).Distinct().Count()>1);
        Assert.Contains(group,e=>e.TargetActorKey=="tank");
        Assert.Contains(group,e=>e.TargetActorKey=="raid-1");
        Assert.Contains(group,e=>e.TargetActorKey=="raid-2");
        Assert.InRange(Math.Abs(sim.HealingByAbility.Values.Sum()-sim.HealingDone),
            0m,.000001m);
        Assert.Contains("HealingByAbility",JsonSerializer.Serialize(sim));
    }

    [Fact]
    public void WaterShieldRechargesFromRealHitsAtMostOncePerInternalCooldown()
    {
        var req=new RestorationShamanSimRequest {
            DurationSeconds=48m,CaptureTimeline=true,IncomingDamage=450m,
            DamageEverySeconds=4m,AdditionalAllies=0,
            CasterIncomingDamage=100m,CasterDamageEverySeconds=.5m,
            FirstCasterDamageAtSeconds=4m
        };
        req.Tuning.MaximumHealth=100000m;
        req.Tuning.HealingCritPercent=0m;
        req.Tuning.SpiritRegenPerSecond=0m;req.Tuning.Mp5=0m;
        var sim=new RestorationShamanSimRunner().Run(req);
        var returns=sim.Run.Timeline.Where(e=>e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="water-shield" && e.Amount is >0m).ToArray();
        Assert.NotEmpty(returns);
        Assert.All(returns,e=>Assert.Equal(req.Tuning.MaximumMana*.02m,e.Amount));
        Assert.InRange(returns.Length,1,3);
        for(var i=1;i<returns.Length;i++)
            Assert.True(returns[i].TimeSeconds-returns[i-1].TimeSeconds>=
                req.Tuning.WaterShieldInternalCooldownSeconds);
        Assert.Equal(returns.Sum(e=>e.Amount.GetValueOrDefault()),
            sim.ManaRestoredFromWaterShield);
    }

    [Fact]
    public void BalanceMoonkinExecutesDirectDamageAndHurricaneCleave()
    {
        var req=new BalanceDruidSimRequest {
            DurationSeconds=36m,CaptureTimeline=true,AdditionalTargets=2,
            BaseSpellHitPercent=100m,UseHurricane=true,
            CustomPriority=["hurricane"]
        };
        var sim=new BalanceDruidSimRunner().Run(req);
        Assert.True(sim.DamageDone>0m);
        Assert.Contains(sim.Run.Timeline,e=>e.AbilityKey=="hurricane" &&
            e.Type==CombatEventType.AbilityChannelStarted);
        Assert.Contains(sim.Run.Timeline,e=>e.AbilityKey=="hurricane" &&
            e.Type==CombatEventType.Damage && e.IsChannelTick);
        Assert.True(sim.Run.CustomMetrics["balance-hurricane-secondary-hits"]>0m);
        Assert.InRange(Math.Abs(sim.DamageDone-sim.DamageByAbility.Values.Sum()),0m,.000001m);
        Assert.Contains("DamageByAbility",JsonSerializer.Serialize(sim));
    }

    [Fact]
    public void FeralCatHitsBleedsCombosAndSpendsEnergyWithoutPhantomDamage()
    {
        var req=new FeralDruidSimRequest {
            Role="cat",DurationSeconds=70m,CaptureTimeline=true,
            Tuning=new ForeverFeralDruidTuning {
                MeleeHitPercent=100m,MeleeCritPercent=0m
            }
        };
        var sim=new FeralDruidSimRunner().Run(req);
        Assert.True(sim.DamageDone>0m);
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="feral-auto");
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="rake" && e.IsPeriodic);
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.ResourceKey=="energy" && e.Amount<0m);
        Assert.InRange(sim.EndingEnergy,0m,100m);
        Assert.InRange(Math.Abs(sim.DamageDone-sim.DamageByAbility.Values.Sum()),0m,.000001m);
    }

    [Fact]
    public void FurorPreservesCatEnergyThroughBearButNeverRegeneratesInsideBear()
    {
        var actor=new SimulationActorState {Key="druid",MaximumHealth=5000m};
        actor.InitializeHealth(5000m);
        actor.AddResource(new ResourceState("mana",1000m,1000m));
        actor.AddResource(new ResourceState("energy",100m,80m));
        var full=new ForeverDruidFormState(5,100m);
        Assert.True(full.Shift(actor,ForeverDruidForm.DireBear,0m));
        Assert.Equal(80m,actor.Resources["energy"].Current);
        Assert.Equal(0m,actor.Resources["energy"].RegenerationPerSecond);
        actor.RefreshResources(5m);
        Assert.Equal(80m,actor.Resources["energy"].Current);
        Assert.True(full.Shift(actor,ForeverDruidForm.Cat,6m));
        Assert.Equal(80m,actor.Resources["energy"].Current);
        Assert.Equal(10m,actor.Resources["energy"].RegenerationPerSecond);
        var lesser=new ForeverDruidFormState(4,100m);
        actor.Resources["energy"].SetCurrent(90m);
        Assert.True(lesser.Shift(actor,ForeverDruidForm.DireBear,0m));
        Assert.Equal(0m,actor.Resources["energy"].Current);
    }

    [Fact]
    public void FeralBearRemainsInFixedDurationTankPreviewAndReportsRageAndThreat()
    {
        var req=new FeralDruidSimRequest {
            Role="bear",DurationSeconds=85m,CaptureTimeline=true,
            EnableBossAttacks=true,BearDodgePercent=0m,
            BossHitDamage=950m
        };
        var sim=new FeralDruidSimRunner().Run(req);
        Assert.True(sim.DamageDone>0m);
        Assert.True(sim.ThreatGenerated>0m);
        Assert.True(sim.ArmorPreventedDamage>0m);
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.ResourceKey=="rage" && e.Amount>0m &&
            e.ResourceTotalAfter.HasValue);
        Assert.False(sim.Run.Summary.PrimaryActorDied);
        Assert.True(sim.ProjectedEndingHealth>=0m);
        Assert.InRange(Math.Abs(sim.DamageDone-sim.DamageByAbility.Values.Sum()),0m,.000001m);
    }

    [Fact]
    public void FeralEnrageReportsActualCappedRageAndResourceTotal()
    {
        var req=new FeralDruidSimRequest {
            Role="bear",DurationSeconds=13m,CaptureTimeline=true,
            EnableBossAttacks=false
        };
        var sim=new FeralDruidSimRunner().Run(req);
        var gain=Assert.Single(sim.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="enrage" && e.ResourceKey=="rage" && e.Amount>0m);
        Assert.True(gain.ResourceTotalAfter>=gain.Amount);
    }

    [Fact]
    public void DruidRestorationReportsActualHealingWithoutOverhealInflation()
    {
        var talents=new ForeverRestorationDruidTalents();
        talents.Ranks["living-spirit"]=3;talents.Ranks["wild-growth"]=1;
        talents.Ranks["swiftmend"]=1;
        talents.Ranks["naturalist"]=5;talents.Ranks["natures-swiftness"]=1;
        var req=new RestorationDruidSimRequest {
            DurationSeconds=55m,CaptureTimeline=true,Talents=talents,
            AllyStartingHealthPercent=60m,IncomingDamage=350m,
            AdditionalAllies=4
        };
        var sim=new RestorationDruidSimRunner().Run(req);
        Assert.True(sim.HealingDone>0m);
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.Healing &&
            e.AbilityKey=="wild-growth" && e.IsPeriodic);
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.Healing &&
            e.AbilityKey=="rejuvenation" && e.IsPeriodic);
        Assert.Equal(sim.Run.Summary.ActorSummaries["druid"].HealingDone,sim.HealingDone);
        Assert.InRange(Math.Abs(sim.HealingDone-sim.HealingByAbility.Values.Sum()),
            0m,.000001m);
        Assert.True(sim.OverhealingDone>=0m);
        Assert.Contains("OverhealingDone",JsonSerializer.Serialize(sim));
    }

    [Fact]
    public void RestorationDruidCurrentForeverTalentDependenciesAreMandatory()
    {
        var talents=new ForeverRestorationDruidTalents();
        talents.Ranks["wild-growth"]=1;
        Assert.Throws<ArgumentException>(()=>talents.Validate());
        talents.Ranks["living-spirit"]=3;
        talents.Validate();
        talents.Ranks["natures-swiftness"]=1;
        Assert.Throws<ArgumentException>(()=>talents.Validate());
        talents.Ranks["naturalist"]=5;
        talents.Validate();
    }

    [Fact]
    public void HealingStreamDoesNotRestoreHealthInAnAlreadyFullTeam()
    {
        var req=new RestorationShamanSimRequest {
            DurationSeconds=15m,CaptureTimeline=true,
            IncomingDamage=0m,AllyInitialHealthPercent=100m,
            AdditionalAllies=2
        };
        var sim=new RestorationShamanSimRunner().Run(req);
        Assert.Equal(0m,sim.HealingDone);
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.Healing &&
            e.AbilityKey=="healing-stream-totem" && e.Amount==0m &&
            e.OverhealingAmount>0m);
    }
}
