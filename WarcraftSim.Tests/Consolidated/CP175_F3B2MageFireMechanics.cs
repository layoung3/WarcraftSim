using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Mage;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>70707 provisional, source-backed Mage Fire effects; real damage and event receipts.</summary>
public sealed class Cp175F3B2MageFireMechanics
{
    private static MageSimRequest Setup(string action,int firePower=0,int ignite=0)
    {
        var r=MageSimRequest.CreatePreset("fire");
        r.DurationSeconds=8m;r.TargetMaximumHealth=1000000m;
        r.BaseSpellHitPercent=100m;r.SpellCritPercent=0m;
        r.Talents.Ranks["fire-power"]=firePower;
        r.Talents.Ranks["ignite"]=ignite;
        r.Talents.Ranks["critical-mass"]=0;
        r.Talents.Ranks["incineration"]=0;
        r.UseIgnite=ignite>0;
        r.UseHeatingUp=false;r.UseCombustion=false;
        r.UseEvocation=false;r.UseMajorManaPotion=false;
        r.CombatItems.ManaRubyCharges=0;
        r.Tuning.ManaRegenPerSecond=100m;
        r.CustomPriority=[action];r.CaptureTimeline=true;
        return r;
    }

    [Fact]
    public void FirePowerCountsOnlyFiveSourceOwnedEffectsWithFourEffectCap()
    {
        var context=new SimulationContext(new SimulationRunOptions {
            DurationSeconds=35m,Seed=175682,CaptureTimeline=true,PrimaryActorKey="mage"
        });
        SimulationActorState Actor(string key,string team)
        {
            var a=new SimulationActorState{Key=key,TeamKey=team,Level=60,MaximumHealth=10000m};
            a.InitializeHealth(10000m);context.AddActor(a);return a;
        }
        Actor("mage","raid");var target=Actor("target","enemy");
        var aura=new AuraManager();
        Assert.Equal(1m,MageFirePowerModifier.Multiplier(target,"mage","fireball",3,0m));
        foreach(var key in new[]{"mage-ignite","mage-improved-scorch-vulnerability",
            "fireball-dot","pyroblast-dot","mage-impact-stun"})
            aura.ApplyAura(context,target,new AuraDefinition {Key=key,Name=key,DurationSeconds=30m},
                "mage","fireball","owned-fire-effect");
        Assert.Equal(1.12m,MageFirePowerModifier.Multiplier(target,"mage","fireball",3,0m));
        Assert.Equal(1.08m,MageFirePowerModifier.Multiplier(target,"mage","frostfire-bolt",2,0m));
        Assert.Equal(1m,MageFirePowerModifier.Multiplier(target,"mage","mage-ignite",3,0m));
        Assert.Equal(1m,MageFirePowerModifier.Multiplier(target,"other-mage","fireball",3,0m));
        Assert.Equal(1m,MageFirePowerModifier.Multiplier(target,"mage","fireball",3,31m));
        // An active Flamestrike/Frostfire ground effect cannot add a Fire Power stack.
        aura.ApplyAura(context,target,new AuraDefinition {
            Key="flamestrike",Name="Flamestrike",DurationSeconds=30m},
            "mage","flamestrike","ground");
        Assert.Equal(1m,MageFirePowerModifier.Multiplier(target,"mage","fireball",3,31m));
    }

    [Theory]
    [InlineData(1,1.01)]
    [InlineData(2,1.02)]
    [InlineData(3,1.03)]
    public void ImprovedScorchNowCountsAsOwnedFirePowerEffectInRealDamage(int rank,double multiplier)
    {
        var control=Setup("scorch",0);control.Talents.Ranks["improved-scorch"]=3;
        var altered=Setup("scorch",rank);altered.Talents.Ranks["improved-scorch"]=3;
        var plain=new MageSimRunner().Run(control);
        var bonus=new MageSimRunner().Run(altered);
        static decimal[] Hits(MageSimResponse r)=>r.Run.Timeline.Where(e=>
            e.Type==CombatEventType.Damage&&e.AbilityKey=="scorch" &&
            !e.IsPeriodic&&e.Amount>0m).Select(e=>e.Amount!.Value).ToArray();
        var ordinary=Hits(plain);var talented=Hits(bonus);
        Assert.True(ordinary.Length>=2 && talented.Length>=2);
        Assert.Equal(ordinary[0],talented[0]);
        Assert.Equal(ordinary[1]*(decimal)multiplier,talented[1]);
        Assert.Contains(bonus.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="improved-scorch" && e.TargetActorKey=="target");
    }

    [Theory]
    [InlineData(1,0.08)]
    [InlineData(5,0.40)]
    public void IgniteUsesDirectCriticalPoolOnceAndDoesNotCritAgain(int rank,double fraction)
    {
        var r=Setup("fire-blast",0,rank);
        r.SpellCritPercent=100m;r.DurationSeconds=5.5m;
        var sim=new MageSimRunner().Run(r);
        var impact=Assert.Single(sim.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="fire-blast"&&!e.IsPeriodic&&e.IsCritical);
        var ticks=sim.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="mage-ignite"&&e.IsPeriodic).ToArray();
        Assert.Equal(2,ticks.Length);
        Assert.Equal(impact.Amount!.Value*(decimal)fraction,ticks.Sum(e=>e.Amount??0m));
        Assert.All(ticks,e=>{Assert.False(e.IsCritical);Assert.Equal("target",e.TargetActorKey);});
        var noIgnite=Setup("fire-blast",0,0);noIgnite.SpellCritPercent=100m;
        noIgnite.DurationSeconds=5.5m;
        Assert.DoesNotContain(new MageSimRunner().Run(noIgnite).Run.Timeline,e=>
            e.Type==CombatEventType.Damage&&e.AbilityKey=="mage-ignite");
    }

    [Fact]
    public void HeatingUpNowEmitsRealAuraAndConsumesItForNonPomPyroblast()
    {
        var r=Setup("fire-blast",0);r.Talents.Ranks["heating-up"]=1;
        r.UseHeatingUp=true;r.DurationSeconds=2m;
        var hits=new MageSimRunner().Run(r);
        Assert.Contains(hits.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="heating-up"&&e.EffectKey=="heating-up-stack");
        var miss=Setup("fire-blast",0);miss.UseHeatingUp=true;
        miss.Talents.Ranks["heating-up"]=1;miss.BaseSpellHitPercent=0m;miss.DurationSeconds=2m;
        Assert.DoesNotContain(new MageSimRunner().Run(miss).Run.Timeline,e=>
            e.Type==CombatEventType.AuraApplied&&e.AbilityKey=="heating-up");
        var pyro=Setup("fire-blast",0);pyro.UseHeatingUp=true;pyro.DurationSeconds=9m;
        pyro.Talents.Ranks["heating-up"]=1;pyro.CustomPriority=["fire-blast","pyroblast"];
        var run=new MageSimRunner().Run(pyro);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.AuraRemoved &&
            e.AbilityKey=="heating-up");
        Assert.True(run.HeatingUpPyroblasts>0);
    }

    [Fact]
    public void CombustionCountsThreeNonPeriodicLandedCritsAndIgnoresMisses()
    {
        var r=Setup("scorch",0);r.DurationSeconds=15m;
        r.SpellCritPercent=100m;r.CustomPriority=["combustion","scorch"];
        r.UseCombustion=true;r.Talents.Ranks["combustion"]=1;
        r.Talents.Ranks["critical-mass"]=3;
        var run=new MageSimRunner().Run(r);
        Assert.Equal(3m,run.Run.CustomMetrics["mage-combustion-crits-total"]);
        Assert.True(run.Run.CustomMetrics["mage-combustion-hits-total"]>=3m);
        Assert.Single(run.Run.Timeline,e=>e.Type==CombatEventType.AuraRemoved&&
            e.AbilityKey=="combustion");
        var miss=Setup("scorch",0);miss.DurationSeconds=9m;
        miss.BaseSpellHitPercent=0m;miss.SpellCritPercent=100m;
        miss.Talents.Ranks["combustion"]=1;
        miss.Talents.Ranks["critical-mass"]=3;miss.CustomPriority=["combustion","scorch"];
        var missing=new MageSimRunner().Run(miss);
        Assert.Equal(0m,missing.Run.CustomMetrics["mage-combustion-crits-total"]);
        Assert.DoesNotContain(missing.Run.Timeline,e=>e.Type==CombatEventType.AuraRemoved&&
            e.AbilityKey=="combustion");
    }

    [Fact]
    public void ImpactUsesThreeSevenTenPercentWithPerExecutionRollAndTargetAura()
    {
        int Run(int rank)
        {
            var context=new SimulationContext(new SimulationRunOptions {
                DurationSeconds=50m,Seed=175683,PrimaryActorKey="mage",CaptureTimeline=true
            });
            SimulationActorState Actor(string key,string team)
            {
                var actor=new SimulationActorState{
                    Key=key,TeamKey=team,Level=60,MaximumHealth=100000m
                };
                actor.InitializeHealth(100000m);context.AddActor(actor);return actor;
            }
            var mage=Actor("mage","raid");Actor("target","enemy");
            var auras=new AuraManager();
            var proc=new MageImpactProcProcessor(mage,auras,rank);
            var run=new SimulationEngine([auras,proc]).Run(context,start=>{
                for(int i=1;i<=200;i++){
                    var id=new Guid(i,0,0,new byte[8]);
                    start.ScheduleEvent(new CombatEvent{
                        TimeSeconds=i*.2m,Type=CombatEventType.Damage,
                        SourceActorKey="mage",TargetActorKey="target",
                        AbilityKey="scorch",SchoolKey="fire",ResultKey="hit",Amount=100m,
                        AbilityExecutionId=id
                    });
                    // One different target hit from the same execution cannot roll twice.
                    start.ScheduleEvent(new CombatEvent{
                        TimeSeconds=i*.2m,Type=CombatEventType.Damage,
                        SourceActorKey="mage",TargetActorKey="target",
                        AbilityKey="scorch",SchoolKey="fire",ResultKey="hit",Amount=100m,
                        AbilityExecutionId=id
                    });
                }
            });
            Assert.Equal(proc.Procs,run.Timeline.Count(e=>e.Type==CombatEventType.AuraApplied &&
                e.AbilityKey=="impact"));
            return proc.Procs;
        }
        Assert.Equal(0,Run(0));
        var a=Run(1);var b=Run(2);var c=Run(3);
        Assert.True(a<=b && b<=c && c>0,$"Impact counts: {a}, {b}, {c}");
    }

    [Fact]
    public void OffPresetFireTalentScalesRealArcaneStartingScorch()
    {
        MageSimRequest Build(int ranks)
        {
            var r=MageSimRequest.CreatePreset("arcane");
            r.DurationSeconds=4.5m;r.BaseSpellHitPercent=100m;r.SpellCritPercent=0m;
            r.Talents.Ranks["critical-mass"]=0;
            r.Talents.Ranks["incineration"]=0;
            r.Talents.Ranks["fire-power"]=ranks;
            r.Talents.Ranks["improved-scorch"]=3;
            r.Talents.FirePointsSpent=30;
            r.Talents.ArcanePointsSpent=21;
            r.Talents.Ranks["arcane-power"]=0;
            r.Talents.Ranks["arcane-mind"]=0;
            r.Talents.Ranks["arcane-instability"]=0;
            r.Talents.Ranks["arcane-meditation"]=0;
            r.UseIgnite=false;r.UseHeatingUp=false;
            r.UseEvocation=false;r.UseMajorManaPotion=false;
            r.CombatItems.ManaRubyCharges=0;
            r.CustomPriority=["scorch"];
            return r;
        }
        var no=new MageSimRunner().Run(Build(0));
        var yes=new MageSimRunner().Run(Build(3));
        static decimal[] Hits(MageSimResponse run)=>run.Run.Timeline
            .Where(e=>e.Type==CombatEventType.Damage&&e.AbilityKey=="scorch" &&
                !e.IsPeriodic&&e.Amount>0m).Select(e=>e.Amount!.Value).ToArray();
        Assert.True(Hits(no).Length>=2);
        Assert.Equal(Hits(no)[0],Hits(yes)[0]);
        Assert.Equal(Hits(no)[1]*1.03m,Hits(yes)[1]);
    }
}
