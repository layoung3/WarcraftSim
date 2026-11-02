using System;
using System.Collections.Generic;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;
using Xunit;
using Xunit.Abstractions;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175F3A2WarriorTalentMechanics(ITestOutputHelper output)
{
    private static WarriorSimRequest Fury()
    {
        var r=new WarriorSimRequest {
            Preset="fury",TalentMode="tree",Iterations=1,Seed=1753202,
            DurationSeconds=24m,StartingStance="berserker",StartingRage=0m,
            CaptureRepresentativeTimeline=true,UseBloodrage=false,
            UseRend=false,UseExecute=false,UseHeroicStrike=false,
            TargetDodgePercent=0m,TargetParryPercent=0m,TargetBlockPercent=0m
        };
        r.TalentRanks=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase) {
            ["cruelty"]=5,["unbridled-wrath"]=5,
            ["blood-craze"]=3,["furious-precision"]=2,["enrage"]=5,
            ["death-wish"]=1,["improved-berserker-rage"]=2
        };
        ForeverWarriorTalentCatalog.Validate(r);
        return r;
    }

    [Theory]
    [InlineData(0,0)]
    [InlineData(1,5)]
    [InlineData(2,10)]
    public void ImprovedBerserkerRageGrantsOnlyLearnedRankOnActualLegalCast(
        int rank,int expected)
    {
        var r=Fury();
        if(rank==0)r.TalentRanks.Remove("improved-berserker-rage");
        else r.TalentRanks["improved-berserker-rage"]=rank;
        r.MajorCooldowns=[new WarriorMajorCooldownPolicy {
            AbilityKey="berserker-rage",Mode="at-times",PlannedUseSeconds=[2m]
        }];
        var run=new ForeverWarriorSimRunner().Run(r);
        var events=run.Batch.RepresentativeRun.Timeline;
        Assert.Single(events,x=>x.AbilityKey=="berserker-rage" &&
            x.Type==CombatEventType.AbilityCastCompleted);
        var gain=events.Where(x=>x.AbilityKey=="improved-berserker-rage" &&
            x.Type==CombatEventType.ResourceChanged).Sum(x=>x.Amount??0m);
        Assert.Equal(expected,(int)gain);
        Assert.Equal(run.MeanDps,new ForeverWarriorSimRunner().Run(r).MeanDps);
        output.WriteLine($"F3A2 Improved Berserker Rage {rank}/2: {gain:0.##} Rage gained");
        r.StartingStance="battle";
        var other=new ForeverWarriorSimRunner().Run(r);
        Assert.DoesNotContain(other.Batch.RepresentativeRun.Timeline,x=>
            x.AbilityKey=="improved-berserker-rage" && x.Type==CombatEventType.ResourceChanged);
    }

    [Fact]
    public void DeathWishRaisesIncomingPhysicalBossDamageWhileActiveAndExpires()
    {
        WarriorSimRequest Make(bool selected)
        {
            var r=Fury();
            r.DurationSeconds=40m;
            r.StartingRage=100m;
            r.EnableBossAutoAttacks=true;
            r.BossSwingMinimumDamage=1000m;
            r.BossSwingMaximumDamage=1000m;
            r.BossFirstSwingSeconds=2m;
            r.BossSwingSpeedSeconds=2m;
            r.BossMeleeCritPercent=0m;
            r.BossMeleeCrushPercent=0m;
            r.PlayerParryPercent=0m;r.PlayerDodgePercent=0m;r.PlayerBlockPercent=0m;
            if(selected)r.MajorCooldowns=[new WarriorMajorCooldownPolicy{
                AbilityKey="death-wish",Mode="at-times",PlannedUseSeconds=[0m]
            }];
            return r;
        }
        var plain=new ForeverWarriorSimRunner().Run(Make(false));
        var buffed=new ForeverWarriorSimRunner().Run(Make(true));
        var before=plain.Batch.RepresentativeRun.Timeline.Where(x=>x.AbilityKey=="boss-main-hand-auto" &&
            x.Type==CombatEventType.Damage && x.Amount>0m).ToArray();
        var after=buffed.Batch.RepresentativeRun.Timeline.Where(x=>x.AbilityKey=="boss-main-hand-auto" &&
            x.Type==CombatEventType.Damage && x.Amount>0m).ToArray();
        Assert.NotEmpty(before);Assert.NotEmpty(after);
        Assert.Contains(buffed.Batch.RepresentativeRun.Timeline,x=>
            x.AbilityKey=="death-wish" && x.Type==CombatEventType.AbilityCastCompleted);
        var active=after.FirstOrDefault(x=>x.TimeSeconds<30m);
        Assert.NotNull(active);
        Assert.True(active.Amount>before.First(x=>x.TimeSeconds==active.TimeSeconds).Amount);
        var expired=after.FirstOrDefault(x=>x.TimeSeconds>=32m);
        Assert.NotNull(expired);
        Assert.Equal(before.First(x=>x.TimeSeconds==expired.TimeSeconds).Amount,expired.Amount);
        Assert.Equal(buffed.MeanDps,new ForeverWarriorSimRunner().Run(Make(true)).MeanDps);
        output.WriteLine($"F3A2 Death Wish incoming {before[0].Amount:0.##} -> {after[0].Amount:0.##} while active");
    }

    [Fact]
    public void GoreDrinkerUsesActualEnrageProcAndOnlyThreeLandedMeleeHits()
    {
        var context=new SimulationContext(new SimulationRunOptions{
            Seed=9,DurationSeconds=60m,PrimaryActorKey="warrior",CaptureTimeline=true
        });
        var warrior=new SimulationActorState{Key="warrior",Name="Warrior",TeamKey="raid",Level=60};
        warrior.InitializeHealth(1000m,500m);
        warrior.AddResource(new ResourceState("rage",100m,0m));
        var target=new SimulationActorState{Key="target",Name="Target",TeamKey="enemy",Level=63};
        target.InitializeHealth(100000m);
        context.AddActor(warrior);context.AddActor(target);
        var r=Fury();r.TalentRanks["gore-drinker"]=2;
        var auto=new AutoAttackProcessor(
            new AbilityExecutor(new AuraManager(),new SimpleCombatRollResolver(),
                new RulesetDamageMitigationResolver(ForeverCombatRulesetFactory.Create())),
            new ForeverAutoAttackTimingProvider());
        var executor=new AbilityExecutor(new AuraManager(),new SimpleCombatRollResolver(),
            new RulesetDamageMitigationResolver(ForeverCombatRulesetFactory.Create()));
        var main=ForeverWarriorAutoAttackFactory.CreateOneHandedMainHand(
            "main-hand-auto","MH",10m,20m,2m,"main-hand-weapon-skill");
        var incoming=new WarriorIncomingTalentProcessor(r);
        var weapon=new WarriorWeaponTalentProcessor(r,auto,executor,main,null);
        for(int i=0;i<100;i++)
            incoming.Process(context,new CombatEvent{Type=CombatEventType.Damage,
                SourceActorKey="target",TargetActorKey="warrior",Amount=10m});
        Assert.Equal(10m,warrior.Stats.Get(WarriorIncomingTalentProcessor.EnrageBonusKey));
        var proc=new CombatEvent{Type=CombatEventType.AbilityEffectImpact,
            SourceActorKey="warrior",TargetActorKey="warrior",
            AbilityKey="enrage",EffectKey="enrage-proc"};
        weapon.Process(context,proc);
        weapon.Process(context,new CombatEvent{Type=CombatEventType.Damage,
            SourceActorKey="warrior",TargetActorKey="target",Amount=50m,IsPeriodic=true,
            WeaponHandKey="main-hand"});
        Assert.Equal(500m,warrior.CurrentHealth);
        for(int i=0;i<4;i++)weapon.Process(context,new CombatEvent{
            Type=CombatEventType.Damage,SourceActorKey="warrior",TargetActorKey="target",
            Amount=50m,WeaponHandKey="main-hand",AbilityKey="main-hand-auto"
        });
        Assert.Equal(530m,warrior.CurrentHealth);
        output.WriteLine("F3A2 real Enrage proc primes 3 landed hits; Gore Drinker heals exactly 30 / 1000 HP");
    }
}
