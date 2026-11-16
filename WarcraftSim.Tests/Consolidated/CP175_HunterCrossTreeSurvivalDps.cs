using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Hunter;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175HunterCrossTreeSurvivalDps
{
    // Legal 33-point Survival allocation, independent of starting-preset identity.
    private static Dictionary<string,int> Ranks(int expose=2,int bleed=1,int clever=2,
        int tactics=0,int discipline=0,int resourcefulness=0) => new() {
        ["improved-tracking"]=5,["deflection"]=5,["entrapment"]=5,["survivalist"]=5,
        ["clever-traps"]=clever,["surefooted"]=3,["predators-edge"]=5,
        ["expose-prey"]=expose,["lacerating-strikes"]=bleed,
        ["survival-tactics"]=tactics,["survivalists-discipline"]=discipline,
        ["resourcefulness"]=resourcefulness
    };
    private static SimulationRunResult Run(string preset,Dictionary<string,int>? ranks=null,
        bool melee=true,bool mark=true,string? trap=null,bool trigger=true,bool rangedTrigger=false,
        decimal duration=120m,decimal hit=100m,int seed=175925,bool automatic=false,
        int adds=0,decimal crit=0m,bool tracked=false)
    {
        ranks??=Ranks();
        var priority=automatic?new List<string>():trap is null?["mongoose-bite"]:[trap];
        if(preset=="marksmanship")
        {
            var r=Cp175HunterOffTreeAgilityParity.Marksmanship(0,melee,crit:crit);
            r.Talents.Ranks=ranks;r.Talents.SurvivalPointsSpent=ranks.Values.Sum();
            r.HasPet=false;r.DurationSeconds=duration;r.Seed=seed;r.ApplyHuntersMark=mark;
            r.TargetIsTracked=tracked;r.CustomPriority=priority;r.AdditionalTargets=adds;
            r.UseFireTraps=trap is "explosive-trap" or "immolation-trap";
            r.UseFrostTraps=trap is "frost-trap" or "freezing-trap";
            r.FireTrapKind=trap=="immolation-trap"?"immolation":"explosive";
            r.FrostTrapKind=trap=="freezing-trap"?"freezing":"frost";
            r.TriggerTraps=trigger;r.TriggerRangedTraps=rangedTrigger;
            r.Tuning.HitChancePercent=hit;r.Tuning.MaximumMana=50000m;
            r.Tuning.RangedAttackPower=1000m;r.PetTuning.MeleeAttackPower=700m;
            return new MarksmanshipHunterSimRunner().Run(r).Run;
        }
        if(preset=="beast-mastery")
        {
            var r=Cp175HunterOffTreeAgilityParity.BeastMastery(0,melee,crit:crit);
            r.Talents.Ranks=ranks;r.PetEnabled=false;r.DurationSeconds=duration;r.Seed=seed;
            r.ApplyHuntersMark=mark;r.TargetIsTracked=tracked;r.CustomPriority=priority;
            r.AdditionalTargets=adds;r.TriggerTraps=trigger;r.TriggerRangedTraps=rangedTrigger;
            r.UseFireTraps=trap is "explosive-trap" or "immolation-trap";
            r.UseFrostTraps=trap is "frost-trap" or "freezing-trap";
            r.FireTrapKind=trap=="immolation-trap"?"immolation":"explosive";
            r.FrostTrapKind=trap=="freezing-trap"?"freezing":"frost";
            r.Tuning.HunterHitPercent=hit;r.Tuning.HunterMana=50000m;
            r.Tuning.RangedAttackPower=1000m;r.Tuning.MeleeAttackPower=700m;
            return new BeastMasteryHunterSimRunner().Run(r).Run;
        }
        var sv=new SurvivalHunterSimRequest {
            Talents=new(){Ranks=ranks},DurationSeconds=duration,Seed=seed,
            TargetMaximumHealth=10000000m,AdditionalTargets=adds,
            CombatMode=melee?"melee":"ranged",ApplyHuntersMark=mark,
            ScriptedAvoidanceEverySeconds=null,TargetIsTracked=tracked,
            UseRapidFire=false,UseAimedShot=false,UseMultiShot=false,UseWingClip=false,
            CustomPriority=priority,TriggerTraps=trigger,TriggerRangedTraps=rangedTrigger,
            UseFireTraps=trap is "explosive-trap" or "immolation-trap",
            UseFrostTraps=trap is "frost-trap" or "freezing-trap",
            FireTrapKind=trap=="immolation-trap"?"immolation":"explosive",
            FrostTrapKind=trap=="freezing-trap"?"freezing":"frost",
            Tuning=new(){HitChancePercent=hit,CriticalChancePercent=crit,
                MaximumMana=50000m,TargetArmor=0m,TargetDodgePercent=0m,TargetParryPercent=0m,
                MeleeWeaponMinDamage=100m,MeleeWeaponMaxDamage=100m,MeleeWeaponSpeedSeconds=2.8m,
                MeleeAttackPower=700m,RangedAttackPower=1000m}
        };
        return new SurvivalHunterSimRunner().Run(sv).Run;
    }
    private static CombatEvent[] Damage(SimulationRunResult r,string key) => r.Timeline
        .Where(x=>x.Type==CombatEventType.Damage && x.AbilityKey==key && x.Amount>0m).ToArray();

    [Theory]
    [InlineData("marksmanship",false)] [InlineData("marksmanship",true)]
    [InlineData("beast-mastery",false)] [InlineData("beast-mastery",true)]
    [InlineData("survival",false)] [InlineData("survival",true)]
    public void ExposePreyFeedsRealMongooseAndSnapshotBleedInAutomaticAndCustomPriorities(string preset,bool automatic)
    {
        var run=Run(preset,automatic:automatic,tracked:true);
        var mongoose=Damage(run,"mongoose-bite");var ticks=Damage(run,"lacerating-strikes");
        Assert.NotEmpty(mongoose);Assert.NotEmpty(ticks);
        Assert.All(mongoose,x=>Assert.Equal(297m*1.05m,x.Amount));
        Assert.All(ticks,tick=> {
            var application=mongoose.Last(x=>x.TimeSeconds<=tick.TimeSeconds-3m);
            Assert.True(tick.IsPeriodic);Assert.Equal("hunter",tick.SourceActorKey);
            Assert.Equal(application.Amount!.Value*.40m/7m,tick.Amount);
            Assert.InRange(tick.TimeSeconds-application.TimeSeconds,3m,21m);
        });
        var without=Run(preset,Ranks(expose:0,bleed:0),automatic:automatic);
        Assert.Empty(Damage(without,"mongoose-bite"));Assert.Empty(Damage(without,"lacerating-strikes"));
        var noBleed=Run(preset,Ranks(bleed:0),automatic:automatic);
        Assert.NotEmpty(Damage(noBleed,"mongoose-bite"));Assert.Empty(Damage(noBleed,"lacerating-strikes"));
    }

    [Theory]
    [InlineData("marksmanship")] [InlineData("beast-mastery")]
    public void UnmarkedTargetsAndAbsentMeleeProximityNeverGrantFreeMongooseDamage(string preset)
    {
        var noMark=Run(preset,mark:false);
        Assert.Empty(Damage(noMark,"mongoose-bite"));
        Assert.Equal(0m,noMark.CustomMetrics["hunter-expose-prey-procs"]);
        var ranged=Run(preset,melee:false);
        Assert.True(ranged.CustomMetrics["hunter-expose-prey-procs"]>0m);
        Assert.Empty(Damage(ranged,"mongoose-bite"));Assert.Empty(Damage(ranged,"lacerating-strikes"));
        var first=Run(preset);var replay=Run(preset);
        Assert.Equal(first.Timeline.Select(x=>(x.TimeSeconds,x.Type,x.AbilityKey,x.Amount)),
            replay.Timeline.Select(x=>(x.TimeSeconds,x.Type,x.AbilityKey,x.Amount)));
    }

    [Theory]
    [InlineData("marksmanship","explosive-trap")] [InlineData("marksmanship","immolation-trap")]
    [InlineData("beast-mastery","explosive-trap")] [InlineData("beast-mastery","immolation-trap")]
    [InlineData("survival","explosive-trap")] [InlineData("survival","immolation-trap")]
    public void CleverTrapsScalesEveryDirectAndPeriodicEventAndRemovalReversesIt(string preset,string trap)
    {
        var ranked=Run(preset,Ranks(expose:0,bleed:0),trap:trap,duration:24m,adds:2);
        var removed=Run(preset,Ranks(expose:0,bleed:0,clever:0),trap:trap,duration:24m,adds:2);
        var before=Damage(removed,trap);var after=Damage(ranked,trap);
        Assert.Equal(trap=="explosive-trap"?18:5,after.Length);
        Assert.Equal(before.Length,after.Length);
        for(var i=0;i<before.Length;i++) {
            Assert.Equal(before[i].Amount!.Value*1.3m,after[i].Amount!.Value,24);
            Assert.Equal(before[i].TargetActorKey,after[i].TargetActorKey);
        }
        var firstTick=after.First(x=>x.IsPeriodic);
        Assert.Equal(trap=="explosive-trap"?85.8m:205.4m,firstTick.Amount);
    }

    [Theory]
    [InlineData("marksmanship")] [InlineData("beast-mastery")] [InlineData("survival")]
    public void SurvivalTacticsClosesOnlyTheTrapHitWindowAndRankRemovalRestoresMisses(string preset)
    {
        var changed=false;
        for(var seed=1;seed<=60;seed++) {
            var ranks=Ranks(expose:0,bleed:0,tactics:2);ranks["surefooted"]=0;
            var ranked=Run(preset,ranks,trap:"explosive-trap",duration:2m,hit:90m,seed:seed);
            ranks["survival-tactics"]=0;
            var removed=Run(preset,ranks,trap:"explosive-trap",duration:2m,hit:90m,seed:seed);
            Assert.Single(Damage(ranked,"explosive-trap"));
            changed|=Damage(removed,"explosive-trap").Length==0;
            Assert.Equal(Damage(removed,"melee-auto").Select(x=>x.Amount),
                Damage(ranked,"melee-auto").Select(x=>x.Amount));
        }
        Assert.True(changed);
    }

    [Theory]
    [InlineData("marksmanship")] [InlineData("beast-mastery")]
    public void TrapPlacementNeedsExplicitTriggerAndRangedProximityAndUsesSharedCooldownAndCost(string preset)
    {
        var ranks=Ranks(expose:0,bleed:0,discipline:2,resourcefulness:2);
        var disabled=Run(preset,ranks,trap:"explosive-trap",trigger:false,duration:40m);
        Assert.Empty(Damage(disabled,"explosive-trap"));
        var distant=Run(preset,ranks,melee:false,trap:"explosive-trap",duration:40m);
        Assert.Empty(Damage(distant,"explosive-trap"));
        var nearby=Run(preset,ranks,melee:false,trap:"explosive-trap",rangedTrigger:true,duration:40m);
        Assert.NotEmpty(Damage(nearby,"explosive-trap"));
        var casts=nearby.Timeline.Where(x=>x.Type==CombatEventType.AbilityCastCompleted && x.AbilityKey=="explosive-trap").ToArray();
        Assert.True(casts.Length>=2);Assert.True(casts[1].TimeSeconds-casts[0].TimeSeconds>=18m);
        Assert.All(nearby.Timeline.Where(x=>x.Type==CombatEventType.ResourceChanged && x.AbilityKey=="explosive-trap" && x.Amount<0m),
            x=>Assert.Equal(-208m,x.Amount));
        var book=ForeverSurvivalHunterSpellbook.Create(new(){Ranks=ranks},new());
        var actor=new SimulationActorState{Key="hunter"};
        HunterOffTreeMeleeActions.Attach(actor,new(){Ranks=ranks},new());
        foreach(var key in new[]{"explosive-trap","immolation-trap","frost-trap","freezing-trap"}) {
            var ability=actor.Abilities[key].Definition;
            Assert.Equal("hunter-traps",ability.SharedCooldownGroupKey);
            Assert.Equal(18m,ability.SharedCooldownSeconds);
            Assert.Equal(book.Single(x=>x.Key==key).ResourceCosts.Single().Amount,ability.ResourceCosts.Single().Amount);
        }
    }

    [Theory]
    [InlineData("marksmanship")] [InlineData("beast-mastery")] [InlineData("survival")]
    public void CritEligibleBleedDoesNotDoubleApplyTrackedDamage(string preset)
    {
        var run=Run(preset,crit:100m,tracked:true);
        var mongoose=Damage(run,"mongoose-bite");var ticks=Damage(run,"lacerating-strikes");
        Assert.NotEmpty(ticks);Assert.All(ticks,tick=> {
            var application=mongoose.Last(x=>x.TimeSeconds<=tick.TimeSeconds-3m);
            Assert.True(tick.IsCritical);
            Assert.Equal(application.Amount!.Value*.40m/7m*2m,tick.Amount!.Value,24);
        });
    }

    [Fact]
    public void SharedProcessorRejectsPetPeriodicTrapAndCombatItemExposeTriggers()
    {
        var ctx=new SimulationContext(new(){Seed=175925,DurationSeconds=40m,CaptureTimeline=true});
        var hunter=new SimulationActorState{Key="hunter",TeamKey="raid",MaximumHealth=1000m};
        var target=new SimulationActorState{Key="target",TeamKey="enemy",MaximumHealth=100000m};
        hunter.InitializeHealth(1000m);target.InitializeHealth(100000m);ctx.AddActor(hunter);ctx.AddActor(target);
        var auras=new AuraManager();var executor=new AbilityExecutor(auras,new SimpleCombatRollResolver(),
            new RulesetDamageMitigationResolver(new WarcraftSim.Core.Rulesets.CombatRulesetDefinition()));
        var processor=new HunterSurvivalEffectProcessor(ctx,hunter,target,auras,executor,new(){Ranks=Ranks()},new(),true,false,.25m,true);
        auras.ApplyAura(ctx,target,new(){Key="hunters-mark",Name="Mark",DurationSeconds=40m},hunter.Key,"hunters-mark","mark");
        foreach(var (source,ability,delivery) in new[]{
            ("hunter-pet","pet-melee",CombatEffectDeliveryType.Direct),
            ("hawk-1","hawk-auto",CombatEffectDeliveryType.Direct),
            ("hunter","auto-shot",CombatEffectDeliveryType.Periodic),
            ("hunter","explosive-trap",CombatEffectDeliveryType.Direct),
            ("hunter","immolation-trap",CombatEffectDeliveryType.Periodic),
            ("hunter","combat-item",CombatEffectDeliveryType.Direct)})
            for(var i=0;i<200;i++)processor.Process(ctx,new(){Type=CombatEventType.Damage,
                SourceActorKey=source,TargetActorKey=target.Key,AbilityKey=ability,Amount=100m,EffectDeliveryType=delivery});
        Assert.Equal(0,processor.ExposePreyProcs);
        for(var i=0;i<200;i++)processor.Process(ctx,new(){Type=CombatEventType.Damage,
            SourceActorKey=hunter.Key,TargetActorKey=target.Key,AbilityKey="auto-shot",Amount=100m});
        Assert.True(processor.ExposePreyProcs>0);
    }
    private static (SimulationContext Context,SimulationActorState Hunter,SimulationActorState Target,
        AuraManager Auras,AbilityExecutor Executor,HunterSurvivalEffectProcessor Processor) Harness(decimal duration=30m)
    {
        var ctx=new SimulationContext(new(){Seed=175925,DurationSeconds=duration,CaptureTimeline=true});
        var hunter=new SimulationActorState{Key="hunter",Name="Hunter",TeamKey="raid",Level=60};
        var target=new SimulationActorState{Key="target",Name="Target",TeamKey="enemy",Level=63};
        hunter.InitializeHealth(1000m);target.InitializeHealth(100000m);ctx.AddActor(hunter);ctx.AddActor(target);
        var auras=new AuraManager();var executor=new AbilityExecutor(auras,new SimpleCombatRollResolver(),
            new RulesetDamageMitigationResolver(new WarcraftSim.Core.Rulesets.CombatRulesetDefinition()));
        var processor=new HunterSurvivalEffectProcessor(ctx,hunter,target,auras,executor,
            new(){Ranks=Ranks()},new(),true,true,.25m,true);
        return(ctx,hunter,target,auras,executor,processor);
    }

    [Fact]
    public void SharedEventRuntimeWeakerOverwriteCancelsOnlySameTargetAndRunsSevenTicks()
    {
        var h=Harness();var add=new SimulationActorState{Key="add-1",Name="Add",TeamKey="enemy",Level=63};
        add.InitializeHealth(100000m);h.Context.AddActor(add);
        var result=new SimulationEngine([h.Executor,h.Auras,h.Processor]).Run(h.Context,ctx=> {
            foreach(var (time,target,amount) in new[]{(0m,"target",700m),(0m,"add-1",280m),(1m,"target",350m)})
                ctx.ScheduleEvent(new(){TimeSeconds=time,Type=CombatEventType.Damage,
                    SourceActorKey="hunter",TargetActorKey=target,AbilityKey="mongoose-bite",Amount=amount});
        });
        var ticks=Damage(result,"lacerating-strikes");Assert.Equal(14,ticks.Length);
        var primary=ticks.Where(x=>x.TargetActorKey=="target").ToArray();
        Assert.Equal(7,primary.Length);Assert.All(primary,x=>Assert.Equal(20m,x.Amount));
        Assert.Equal(Enumerable.Range(1,7).Select(i=>1m+i*3m),primary.Select(x=>x.TimeSeconds));
        var secondary=ticks.Where(x=>x.TargetActorKey=="add-1").ToArray();
        Assert.Equal(7,secondary.Length);Assert.All(secondary,x=>Assert.Equal(16m,x.Amount));
        Assert.Equal(Enumerable.Range(1,7).Select(i=>i*3m),secondary.Select(x=>x.TimeSeconds));
    }

    private sealed class TargetDeathFixture(SimulationActorState target) : ICombatEventProcessor
    {
        public void Process(SimulationContext context,CombatEvent ev)
        {
            if(ev.EffectKey=="fixture-target-death")target.TakeDamage(target.MaximumHealth);
        }
    }
    [Fact]
    public void SharedBleedTicksDoNotDamageADeadTarget()
    {
        var h=Harness();var result=new SimulationEngine([h.Executor,h.Auras,h.Processor,
            new TargetDeathFixture(h.Target)]).Run(h.Context,ctx=> {
            ctx.ScheduleEvent(new(){TimeSeconds=0m,Type=CombatEventType.Damage,
                SourceActorKey="hunter",TargetActorKey="target",AbilityKey="mongoose-bite",Amount=700m});
            ctx.ScheduleEvent(new(){TimeSeconds=1m,Type=CombatEventType.RotationDecision,
                SourceActorKey="hunter",TargetActorKey="target",EffectKey="fixture-target-death",IsInternal=true});
        });
        Assert.Empty(Damage(result,"lacerating-strikes"));
    }

    [Fact]
    public void SharedFrostMarkerDurationUsesCleverTrapsWithoutInventingSpatialControl()
    {
        var h=Harness(2m);
        new SimulationEngine([h.Executor,h.Auras,h.Processor]).Run(h.Context,ctx=>
            ctx.ScheduleEvent(new(){TimeSeconds=0m,Type=CombatEventType.AuraApplied,
                SourceActorKey="hunter",TargetActorKey="hunter",AbilityKey="frost-trap"}));
        var slow=Assert.Single(h.Target.ActiveAuras,x=>x.Definition.Key=="hunter-frost-slow");
        Assert.Equal(39m,slow.Definition.DurationSeconds);
        Assert.DoesNotContain(h.Target.ActiveAuras,x=>x.Definition.Key=="hunter-entrapment");
    }

    [Theory]
    [InlineData("hunter-pet",false)][InlineData("hawk-1",false)]
    [InlineData("hunter-pet",true)][InlineData("hawk-1",true)]
    public void CompanionDamageBreaksFreezingTrapAndInvalidatesAuraConditionWithoutOwnerProcs(
        string source,bool periodic)
    {
        var h=Harness();
        h.Auras.ApplyAura(h.Context,h.Target,new(){Key="hunter-frozen",Name="Frozen",DurationSeconds=20m},
            "hunter","freezing-trap","fixture-freeze");
        h.Auras.ApplyAura(h.Context,h.Target,new(){Key="hunter-frost-slow",Name="Frost",DurationSeconds=30m},
            "hunter","frost-trap","fixture-slow");
        h.Auras.ApplyAura(h.Context,h.Target,new(){Key="hunters-mark",Name="Mark",DurationSeconds=30m},
            "hunter","hunters-mark","fixture-mark");
        var condition=new WarcraftSim.Core.Rotations.RotationConditionDefinition {
            ConditionType=WarcraftSim.Core.Rotations.RotationConditionTypes.TargetAuraActive,
            Key="hunter-frozen",RequireSourceOwnership=true
        };
        Assert.True(RotationConditionEvaluator.AreSatisfied(h.Context,h.Hunter,h.Target,[condition]));
        h.Processor.Process(h.Context,new(){Type=CombatEventType.Damage,SourceActorKey=source,
            TargetActorKey="target",AbilityKey=source=="hunter-pet"?"pet-melee":"hawk-auto",
            Amount=100m,EffectDeliveryType=periodic?CombatEffectDeliveryType.Periodic:CombatEffectDeliveryType.Direct});
        Assert.False(RotationConditionEvaluator.AreSatisfied(h.Context,h.Hunter,h.Target,[condition]));
        Assert.DoesNotContain(h.Target.ActiveAuras,x=>x.Definition.Key=="hunter-frozen");
        Assert.Contains(h.Target.ActiveAuras,x=>x.Definition.Key=="hunter-frost-slow");
        Assert.Equal(0,h.Processor.ExposePreyProcs);Assert.Equal(0,h.Processor.BleedTicks);
        Assert.DoesNotContain(h.Target.ActiveAuras,x=>x.Definition.Key=="hunter-lacerating-strikes");
    }

    [Theory]
    [InlineData(0,"target")][InlineData(100,"other-target")]
    public void MissesAndDamageElsewhereDoNotBreakFreezingTrap(int amount,string target)
    {
        var h=Harness();
        h.Auras.ApplyAura(h.Context,h.Target,new(){Key="hunter-frozen",Name="Frozen",DurationSeconds=20m},
            "hunter","freezing-trap","fixture-freeze");
        h.Processor.Process(h.Context,new(){Type=CombatEventType.Damage,SourceActorKey="hunter-pet",
            TargetActorKey=target,AbilityKey="pet-melee",Amount=amount});
        Assert.Contains(h.Target.ActiveAuras,x=>x.Definition.Key=="hunter-frozen" && x.IsActiveAt(h.Context.CurrentTimeSeconds));
    }

}
