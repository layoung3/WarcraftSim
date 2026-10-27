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
    public void EnhancementStormstrikeNormalizesSpecialAttackApNotWhiteSwingSpeed()
    {
        var talents=new ForeverEnhancementShamanTalents();
        var tuning=new ForeverEnhancementShamanTuning();
        var twoHand=ForeverEnhancementShamanSpellbook.Create(tuning,talents,
            100m,3.6m,200m,300m);
        var oneHand=ForeverEnhancementShamanSpellbook.Create(tuning,talents,
            100m,2.8m,200m,300m,dualWield:true);
        var two=twoHand.Single(a=>a.Key=="stormstrike").Effects.Single();
        var one=oneHand.Single(a=>a.Key=="stormstrike").Effects.Single();
        Assert.Equal(3.3m/14m,two.ScalingCoefficient);
        Assert.Equal(2.4m/14m,one.ScalingCoefficient);
        Assert.Equal(two.MinimumValue,one.MinimumValue);
        Assert.Equal(two.MaximumValue,one.MaximumValue);
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
        // Shield can legitimately be re-cast after exhausting its
        // three charges. Enforce charges per aura application, not a
        // fictitious once-per-fight ceiling.
        var applications=sim.Run.Timeline.Where(e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="water-shield" && e.TargetActorKey=="shaman").ToArray();
        Assert.NotEmpty(applications);
        foreach(var (application,index) in applications.Select((x,i)=>(x,i)))
        {
            var end=index+1<applications.Length
                ? applications[index+1].TimeSeconds : req.DurationSeconds;
            var triggered=returns.Where(e=>e.TimeSeconds>=application.TimeSeconds &&
                e.TimeSeconds<end).ToArray();
            Assert.InRange(triggered.Length,0,3);
            for(var i=1;i<triggered.Length;i++)
                Assert.True(triggered[i].TimeSeconds-triggered[i-1].TimeSeconds>=
                    req.Tuning.WaterShieldInternalCooldownSeconds);
        }
        Assert.Equal(returns.Sum(e=>e.Amount.GetValueOrDefault()),
            sim.ManaRestoredFromWaterShield);
    }

    [Theory]
    [InlineData(0,1.00)]
    [InlineData(1,1.08)]
    [InlineData(2,1.17)]
    [InlineData(3,1.25)]
    public void HealingWayRankScalingMatchesCurrentForeverLevel60Client(
        int rank,double expectedMultiplier)
    {
        var t=new ForeverRestorationShamanTalents();
        t.Ranks["healing-way"]=rank;
        // Riptide requires 3/3 Healing Way; isolate this rank test.
        t.Ranks.Remove("riptide");
        var spells=ForeverRestorationShamanSpellbook.Create(t,new());
        var wave=spells.Single(a=>a.Key=="healing-wave");
        var baseHealing=1750m; // override only spell rank input
        var tuned=new ForeverRestorationShamanTuning {HealingWaveBase=baseHealing,
            HealingPower=0m};
        wave=ForeverRestorationShamanSpellbook.Create(t,tuned)
            .Single(a=>a.Key=="healing-wave");
        Assert.Equal(baseHealing*(decimal)expectedMultiplier,
            wave.Effects.Single().MinimumValue);
    }

    [Fact]
    public void SourcedLevel60HealingRanksAndManaTideCostAreNotOldProvisionalValues()
    {
        var t=new ForeverRestorationShamanTuning();
        Assert.Equal(1615m,t.HealingWaveBase);
        Assert.Equal(620m,t.HealingWaveMana);
        Assert.Equal(820m,t.LesserHealingWaveBase);
        Assert.Equal(380m,t.LesserHealingWaveMana);
        Assert.Equal(490m,t.ChainHealBase);
        Assert.Equal(405m,t.ChainHealMana);
        Assert.Equal(841m,t.RiptideDirectBase);
        Assert.Equal(161m,t.RiptidePeriodicTick);
        Assert.Equal(385m,t.RiptideMana);
        Assert.Equal(290m,t.ManaTideManaPerTick);
        var spells=ForeverRestorationShamanSpellbook.Create(new(),t);
        Assert.Equal(60m,spells.Single(a=>a.Key=="mana-tide-totem")
            .ResourceCosts.Single().Amount);
    }

    [Fact]
    public void ManaTideReturnsFlatAmountsNotAPercentageOfTotalMana()
    {
        var req=new RestorationShamanSimRequest {
            DurationSeconds=45m,CaptureTimeline=true,
            IncomingDamage=1000m,DamageEverySeconds=2m,
            AllyInitialHealthPercent=40m
        };
        req.Tuning.ManaTideTickPercent=99m; // obsolete JSON compatibility only
        var run=new RestorationShamanSimRunner().Run(req);
        var pulses=run.Run.Timeline.Where(e=>e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="mana-tide-totem" && e.Amount>0m).ToArray();
        Assert.NotEmpty(pulses);
        Assert.All(pulses,e=>Assert.InRange(e.Amount.GetValueOrDefault(),
            0.0000001m,req.Tuning.ManaTideManaPerTick));
        Assert.Equal(pulses.Sum(e=>e.Amount.GetValueOrDefault()),run.ManaRestoredFromManaTide);
    }

    [Fact]
    public void AncestralHealingArmorRanksSurviveInSharedMitigationModel()
    {
        var request=new RestorationShamanSimRequest {
            DurationSeconds=38m,CaptureTimeline=true,
            AdditionalAllies=0,AllyPhysicalArmor=5000m,
            IncomingDamage=2000m,DamageEverySeconds=2m,
            AllyInitialHealthPercent=60m
        };
        request.Tuning.HealingCritPercent=100m;
        request.Talents.Ranks["ancestral-healing"]=0;
        var plain=new RestorationShamanSimRunner().Run(request);
        request.Talents.Ranks["ancestral-healing"]=3;
        var skilled=new RestorationShamanSimRunner().Run(request);
        Assert.True(skilled.ArmorPreventedDamage>plain.ArmorPreventedDamage);
        Assert.Contains(skilled.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="ancestral-healing");
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
        // Exclude competing Mana Tide in a zero-pressure Water-slot test;
        // then the full-health Healing Stream's raw heal must be overheal.
        req.Talents.Ranks.Remove("mana-tide-totem");
        var sim=new RestorationShamanSimRunner().Run(req);
        Assert.Equal(0m,sim.HealingDone);
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.Healing &&
            e.AbilityKey=="healing-stream-totem" && e.Amount==0m &&
            e.OverhealingAmount>0m);
    }
}
