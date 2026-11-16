using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Druid;

namespace WarcraftSim.Tests.Consolidated;

// Bounded preview policy, not an optimal rotation or new coefficient certification.
public sealed class Cp175FeralWorkflowGates
{
    private static FeralDruidSimRequest Request() => new() {
        DurationSeconds = 40, CaptureTimeline = true, TargetBehind = false,
        Tuning = new() { MeleeHitPercent = 100, MeleeCritPercent = 0, OmenProcPercent = 0 }
    };
    private static FeralDruidSimRequest Fast() {
        var r = Request(); r.InitialComboPointsByTarget["target"] = 5;
        r.Tuning.MeleeCritPercent = 100;
        r.Talents.Ranks["ferocity"] = 5; r.Talents.Ranks["blood-frenzy"] = 2;
        return r;
    }
    private static CombatEvent[] Casts(SimulationRunResult r) => r.Timeline.Where(e => e.Type == CombatEventType.AbilityCastCompleted).ToArray();
    private static string Canonical(object value) => Regex.Replace(JsonSerializer.Serialize(value),
        "[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", "INSTANCE-ID", RegexOptions.IgnoreCase);

    [Fact]
    public void PublicPriorityReachesFourPointBiteAtFourteenWithoutReplacingFivePointRip()
    {
        var r = new FeralDruidSimRunner().Run(Fast());
        var first = Casts(r.Run).Take(4).Select(e => (e.AbilityKey!, e.TimeSeconds));
        Assert.Equal(new[] { ("rip",3m),("rake",6.5m),("claw",10.5m),("ferocious-bite",14m) }, first);
        var bite = r.Run.Timeline.First(e => e.Type == CombatEventType.Damage && e.AbilityKey == "ferocious-bite");
        Assert.Equal(1600m,bite.Amount); Assert.Equal("target",bite.TargetActorKey);
        Assert.Contains(r.Run.Timeline,e => e.Type == CombatEventType.ResourceChanged && e.AbilityKey == "ferocious-bite" && e.TimeSeconds == 14 && e.Amount == -35);
        Assert.Equal(new[] {5m,7m,9m,11m,13m,15m}, r.Run.Timeline.Where(e => e.Type == CombatEventType.Damage && e.AbilityKey == "rip" && e.IsPeriodic && e.TimeSeconds <= 15).Select(e => e.TimeSeconds));
        Assert.All(r.Run.Timeline.Where(e => e.Type == CombatEventType.Damage && e.AbilityKey == "rip" && e.IsPeriodic && e.TimeSeconds <= 15),e => Assert.Equal(450m,e.Amount));
        Assert.Equal(r.DamageDone,r.DamageByAbility.Values.Sum());
    }

    // The harness executes the actual internal processor, roll and armor adapters,
    // source-aura gates, and priority/ability executors. Synthetic state only sets
    // preconditions that the public request cannot directly author.
    private sealed class Harness
    {
        public SimulationContext Context { get; }
        public SimulationActorState Actor { get; }
        public AuraManager Auras { get; } = new();
        public RotationProfile Rotation { get; } = new() { Name = "Feral gate test", ClassKey = "druid", RulesetKey = "wow-forever", SimulationType = SimulationType.Dps };
        public ICombatEventProcessor Mechanics { get; }
        private readonly AbilityExecutor executor;
        private readonly PriorityRotationExecutor decisions;
        public int Points(string target) => (int)Mechanics.GetType().GetMethod("GetComboPoints")!.Invoke(Mechanics,[target])!;
        public int Clearcasts => (int)Mechanics.GetType().GetProperty("ClearcastsUsed")!.GetValue(Mechanics)!;
        public decimal Multiplier(string ability,string target) => (decimal)Mechanics.GetType().GetMethod("DamageMultiplier")!.Invoke(Mechanics,[ability,target,CombatEffectDeliveryType.Periodic,null])!;
        public Harness(FeralDruidSimRequest r, decimal energy = 35, string form = "cat", decimal duration = .1m)
        {
            Context = new(new() { Seed = 175701, DurationSeconds = duration, CaptureTimeline = true, PrimaryActorKey = "druid", MaximumProcessedEvents = 10000 });
            Actor = new() { Key = "druid", TeamKey = "raid", Level = 60, MaximumHealth = 5000 };
            Actor.InitializeHealth(5000); Actor.AddResource(new("energy",100,energy)); Actor.AddResource(new("rage",100,0)); Actor.AddResource(new("mana",5000,5000)); Context.AddActor(Actor);
            foreach(var key in new[] {"target","target-1"}) {
                var target = new SimulationActorState { Key = key, TeamKey = "enemy", Level = 63, MaximumHealth = 1000000 };
                target.InitializeHealth(1000000); Context.AddActor(target);
            }
            foreach(var ability in ForeverFeralDruidSpellbook.Create(r.Talents,r.Tuning)) Actor.AddAbility(ability);
            Rotation.Entries.Add(new() { Priority = 0, AbilityKey = "rip" }); Rotation.Entries.Add(new() { Priority = 1, AbilityKey = "ferocious-bite" });
            var state = new ForeverDruidFormState(5,0);
            var chosen = form == "cat" ? ForeverDruidForm.Cat : form == "bear" ? ForeverDruidForm.DireBear : ForeverDruidForm.Caster;
            if(chosen != ForeverDruidForm.Caster) Assert.True(state.EstablishInitialForm(Actor,chosen,0));
            Actor.Resources["energy"].RegenerationPerSecond = 0;
            Aura(Actor.Key,ForeverDruidFormState.AuraKey(chosen));
            var type = typeof(FeralDruidSimRunner).Assembly.GetType("WarcraftSim.Api.Simulation.FeralDruidCombatProcessor")!;
            Mechanics = (ICombatEventProcessor)Activator.CreateInstance(type,[Actor,r,Rotation,state,Auras,null,null,null,null,null])!;
            var get = Expression.Lambda(typeof(Func<>).MakeGenericType(type),Expression.Constant(Mechanics,type)).Compile();
            object Adapter(string name,params object[] args) => Activator.CreateInstance(typeof(FeralDruidSimRunner).GetNestedType(name,BindingFlags.NonPublic)!,BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,null,args,null)!;
            executor = new(Auras,(ICombatRollResolver)Adapter("FeralRolls",r,get),(IDamageMitigationResolver)Adapter("FeralArmorMitigation",r.TargetArmor,r.Tuning));
            decisions = new(Rotation,"druid","target",executor);
        }
        public void Aura(string target,string key, decimal duration = 60, string source = "druid") => Auras.ApplyAura(Context,Context.Actors[target],new AuraDefinition { Key = key, DurationSeconds = duration },source,key,"test-state");
        public void Switch(string key) => Mechanics.Process(Context,new() { Type = CombatEventType.RotationDecision, SourceActorKey = "druid", TargetActorKey = key, EffectKey = "feral-target-switch:" + key });
        public void Decide() => Mechanics.Process(Context,new() { Type = CombatEventType.RotationDecision, SourceActorKey = "druid" });
        public SimulationRunResult Run(Action<SimulationContext>? start = null, Action<SimulationContext,CombatEvent>? observe = null) =>
            new SimulationEngine([executor,Auras,new Callback(observe),Mechanics,decisions]).Run(Context,start);
        private sealed class Callback(Action<SimulationContext,CombatEvent>? action) : ICombatEventProcessor {
            public void Process(SimulationContext context,CombatEvent ev) => action?.Invoke(context,ev);
        }
    }

    public static IEnumerable<object[]> PolicyCases() {
        for(var cp=0;cp<=5;cp++) foreach(var rip in new[]{false,true}) foreach(var prowl in new[]{false,true})
            yield return [cp,rip,prowl];
    }
    [Theory][MemberData(nameof(PolicyCases))]
    public void ComplementaryFinisherGatesUseActiveTargetPoints(int cp,bool rip,bool prowl)
    {
        var r=Request();r.InitialComboPointsByTarget["target"]=cp;
        var h=new Harness(r,100); if(rip)h.Aura("target","rip");if(prowl)h.Aura("druid","druid-prowl");
        h.Decide();Assert.Equal(!prowl && cp>=4 && !rip,h.Rotation.Entries[0].IsEnabled);
        Assert.Equal(!prowl && cp>=4 && rip,h.Rotation.Entries[1].IsEnabled);
        var casts=Casts(h.Run());
        if(prowl || cp<4)Assert.Empty(casts);
        else Assert.Equal(rip?"ferocious-bite":"rip",Assert.Single(casts).AbilityKey);
    }

    [Theory][InlineData("bear")][InlineData("caster")]
    public void CatSourceAuraStillRequiredEvenWithEligiblePointsAndBleed(string form)
    {
        var r=Request();r.InitialComboPointsByTarget["target"]=5;
        var h=new Harness(r,100,form);h.Aura("target","rip");Assert.Empty(Casts(h.Run()));
    }

    [Theory][InlineData(11.999,true)][InlineData(12,false)][InlineData(12.001,false)]
    public void ExpirationBoundaryUsesExistingIsActiveAtContract(double at,bool bite)
    {
        var r=Request();r.InitialComboPointsByTarget["target"]=4;
        var h=new Harness(r,35,duration:(decimal)at+.01m);h.Aura("target","rip",12);
        h.Actor.ConfigureActionTiming((decimal)at,0);
        Assert.Equal(bite?"ferocious-bite":"rip",Assert.Single(Casts(h.Run())).AbilityKey);
    }

    [Theory][InlineData("target","target-1")][InlineData("target-1","target")]
    public void SwitchingToOwnedBleedSpendsOnlyThatTargetsPointsThenReturnsToFreshRip(string bleeding,string other)
    {
        var r=Request();r.InitialComboPointsByTarget[bleeding]=4;r.InitialComboPointsByTarget[other]=5;
        var h=new Harness(r,100,duration:2.1m);h.Aura(bleeding,"rip");h.Switch(bleeding);
        var run=h.Run(ctx=>ctx.ScheduleEvent(new() { Type=CombatEventType.RotationDecision,TimeSeconds=2,SourceActorKey="druid",TargetActorKey=other,EffectKey="feral-target-switch:"+other }));
        var casts=Casts(run);Assert.Equal(new[]{"ferocious-bite","rip"},casts.Select(e=>e.AbilityKey));
        Assert.Equal(new[]{bleeding,other},casts.Select(e=>e.TargetActorKey));
        Assert.Equal(0,h.Points(bleeding));Assert.Equal(0,h.Points(other));
        Assert.Equal(5m,h.Multiplier("rip",other));
    }

    [Fact]
    public void AnotherTargetsRipCannotEnableBiteOrSuppressFreshRip()
    {
        var r=Request();r.InitialComboPointsByTarget["target"]=5;r.InitialComboPointsByTarget["target-1"]=4;
        var h=new Harness(r);h.Aura("target-1","rip");
        Assert.Equal("rip",Assert.Single(Casts(h.Run())).AbilityKey);Assert.Equal(4,h.Points("target-1"));
    }

    [Theory][InlineData(4,0,800)][InlineData(5,0,1000)][InlineData(4,10557.5,400)]
    public void BiteUsesCurrentPointsAndDirectArmorWithoutChangingFivePointRipSnapshot(int cp,double armor,int damage)
    {
        var r=Request();r.TargetArmor=(decimal)armor;r.InitialComboPointsByTarget["target"]=5;
        var h=new Harness(r,100,duration:4.1m);
        var run=h.Run(ctx=>ctx.ScheduleEvent(new() { Type=CombatEventType.RotationDecision,TimeSeconds=1,SourceActorKey="druid",EffectKey="test-refill" }),
            (ctx,e)=> { if(e.EffectKey=="test-refill") for(var i=0;i<cp;i++)h.Mechanics.Process(ctx,new() { Type=CombatEventType.Damage,SourceActorKey="druid",TargetActorKey="target",AbilityKey="claw",AbilityExecutionId=Guid.NewGuid(),Amount=1 }); });
        Assert.Equal(new[]{"rip","ferocious-bite"},Casts(run).Select(e=>e.AbilityKey));
        var bite=Assert.Single(run.Timeline,e=>e.Type==CombatEventType.Damage && e.AbilityKey=="ferocious-bite");Assert.Equal(damage,bite.Amount);
        Assert.All(run.Timeline.Where(e=>e.Type==CombatEventType.Damage && e.AbilityKey=="rip" && e.IsPeriodic),e=>Assert.Equal(225m,e.Amount));
        Assert.Equal(0,h.Points("target"));Assert.Equal(5m,h.Multiplier("rip","target"));
    }

    [Theory][InlineData(34.99,false,false)][InlineData(35,false,true)][InlineData(0,true,true)]
    public void BiteAffordabilityAndClearcastingUseActualPaidEnergy(double energy,bool free,bool casts)
    {
        var r=Request();r.InitialComboPointsByTarget["target"]=4;
        var h=new Harness(r,(decimal)energy);h.Aura("target","rip");if(free)h.Aura("druid","druid-clearcasting");
        var run=h.Run();Assert.Equal(casts?1:0,Casts(run).Length);Assert.Equal(casts?0:4,h.Points("target"));
        Assert.Equal(free?1:0,h.Clearcasts);
        var paid=run.Timeline.Where(e=>e.Type==CombatEventType.ResourceChanged && e.ResourceKey=="energy" && e.Amount<0).ToArray();
        if(casts && !free)Assert.Equal(-35m,Assert.Single(paid).Amount);else Assert.Empty(paid);
    }

    [Theory][InlineData(4,false)][InlineData(5,false)][InlineData(4,true)][InlineData(5,true)]
    public void MissedBiteRestoresOnlyOwnedPointsAndRefundsOnlyActualCost(int cp,bool free)
    {
        var r=Request();r.Tuning.MeleeHitPercent=0;r.InitialComboPointsByTarget["target"]=cp;r.InitialComboPointsByTarget["target-1"]=3;
        var h=new Harness(r,free?0:35);h.Aura("target","rip");if(free)h.Aura("druid","druid-clearcasting");
        var run=h.Run();var miss=Assert.Single(run.Timeline,e=>e.Type==CombatEventType.Damage && e.AbilityKey=="ferocious-bite");
        Assert.Equal(0m,miss.Amount);Assert.Equal("miss",miss.ResultKey);Assert.Equal(cp,h.Points("target"));Assert.Equal(3,h.Points("target-1"));
        h.Mechanics.Process(h.Context,miss);Assert.Equal(cp,h.Points("target"));
        Assert.Equal(free?0m:28m,h.Actor.Resources["energy"].Current);
        Assert.Equal(free?0:1,run.Timeline.Count(e=>e.AbilityKey=="ferocious-bite-miss-refund"));
        Assert.Contains(h.Context.Actors["target"].ActiveAuras,a=>a.Definition.Key=="rip");
    }

    [Fact]
    public void ConsecutivePaidAndFreeMissCannotReuseEarlierPayment()
    {
        var r=Request();r.Tuning.MeleeHitPercent=0;r.InitialComboPointsByTarget["target"]=4;
        var h=new Harness(r,35,duration:2.1m);h.Aura("target","rip");
        var run=h.Run(ctx=>ctx.ScheduleEvent(new() { Type=CombatEventType.RotationDecision,TimeSeconds=2,SourceActorKey="druid",EffectKey="test-free" }),
            (_,e)=>{if(e.EffectKey=="test-free"){h.Actor.Resources["energy"].SetCurrent(0);h.Aura("druid","druid-clearcasting");}});
        Assert.Equal(2,Casts(run).Length);Assert.Equal(1,h.Clearcasts);Assert.Equal(4,h.Points("target"));Assert.Equal(0,h.Actor.Resources["energy"].Current);
        Assert.Equal(28m,Assert.Single(run.Timeline,e=>e.AbilityKey=="ferocious-bite-miss-refund").Amount);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void FirstRipMissRestoresPointsAndRefundsOnlyItsOwnPaidEnergy(bool free)
    {
        var r=Request();r.Tuning.MeleeHitPercent=0;r.InitialComboPointsByTarget["target"]=5;
        var h=new Harness(r,free?0:30);if(free)h.Aura("druid","druid-clearcasting");
        var run=h.Run();Assert.Equal("rip",Assert.Single(Casts(run)).AbilityKey);Assert.Equal(5,h.Points("target"));
        Assert.Equal(free?0m:24m,h.Actor.Resources["energy"].Current);
        Assert.DoesNotContain(run.Timeline,e=>e.Type==CombatEventType.AuraApplied && e.AbilityKey=="rip");
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void FreePaidFreeMissChainOwnsEachExecutionAndNeverCarriesARefundForward(bool rip)
    {
        var r=Request();r.Tuning.MeleeHitPercent=0;r.InitialComboPointsByTarget["target"]=4;
        var h=new Harness(r,0,duration:4.1m);if(!rip)h.Aura("target","rip");h.Aura("druid","druid-clearcasting");
        var run=h.Run(ctx=>{
            foreach(var at in new[]{2m,4m})ctx.ScheduleEvent(new(){Type=CombatEventType.RotationDecision,TimeSeconds=at,SourceActorKey="druid",EffectKey="test-miss-chain"});
        },(ctx,e)=>{if(e.EffectKey=="test-miss-chain"){
            h.Actor.Resources["energy"].SetCurrent(ctx.CurrentTimeSeconds==2?(rip?30:35):0);
            if(ctx.CurrentTimeSeconds==4)h.Aura("druid","druid-clearcasting");
        }});
        Assert.Equal(new[]{0m,2m,4m},Casts(run).Select(e=>e.TimeSeconds));
        var refund=Assert.Single(run.Timeline,e=>e.AbilityKey==(rip?"rip-miss-refund":"ferocious-bite-miss-refund"));Assert.Equal(2m,refund.TimeSeconds);Assert.Equal(rip?24m:28m,refund.Amount);
        Assert.Equal(2,h.Clearcasts);Assert.Equal(4,h.Points("target"));Assert.Equal(0,h.Actor.Resources["energy"].Current);
    }

    [Fact]
    public void ZeroAuthoredBiteStillConsumesLandedPointsAndDeathDoesNotTransferOtherPoints()
    {
        var r=Request();r.Tuning.BitePerPoint=0;r.InitialComboPointsByTarget["target"]=4;r.InitialComboPointsByTarget["target-1"]=5;
        var h=new Harness(r);h.Aura("target","rip");var run=h.Run();
        var hit=Assert.Single(run.Timeline,e=>e.Type==CombatEventType.Damage && e.AbilityKey=="ferocious-bite");Assert.Equal("hit",hit.ResultKey);Assert.Equal(0m,hit.Amount);Assert.Equal(0,h.Points("target"));
        h.Mechanics.Process(h.Context,new(){Type=CombatEventType.ActorDied,TargetActorKey="target"});Assert.Equal(5,h.Points("target-1"));
    }

    [Fact]
    public void CompletionWithoutResolvedPaidExecutionCannotConsumePoints()
    {
        var r=Request();r.InitialComboPointsByTarget["target"]=4;
        var h=new Harness(r);
        h.Mechanics.Process(h.Context,new(){Type=CombatEventType.AbilityCastCompleted,SourceActorKey="druid",TargetActorKey="target",AbilityKey="ferocious-bite",AbilityExecutionId=Guid.NewGuid()});
        Assert.Equal(4,h.Points("target"));
    }

    [Theory][InlineData("druid")][InlineData("other-druid")]
    public void ExistingCurrentTargetRipSourceConventionIsPreserved(string source)
    {
        var r=Request();r.InitialComboPointsByTarget["target"]=4;
        var h=new Harness(r);h.Aura("target","rip",source:source);
        Assert.Equal("ferocious-bite",Assert.Single(Casts(h.Run())).AbilityKey);
    }

    [Fact]
    public void ConsecutivePaidMissesRefundEachPaymentOnce()
    {
        var r=Request();r.Tuning.MeleeHitPercent=0;r.InitialComboPointsByTarget["target"]=4;
        var h=new Harness(r,70,duration:1.1m);h.Aura("target","rip");var run=h.Run();
        Assert.Equal(2,Casts(run).Length);Assert.Equal(4,h.Points("target"));Assert.Equal(56m,h.Actor.Resources["energy"].Current);
        Assert.Equal(new[]{28m,28m},run.Timeline.Where(e=>e.AbilityKey=="ferocious-bite-miss-refund").Select(e=>e.Amount!.Value));
    }

    [Theory][InlineData(4)][InlineData(5)]
    public void ShortFightStillChoosesFreshRipWithoutExecuteHeuristic(int seconds)
    {
        var r=Request();r.DurationSeconds=seconds;r.InitialComboPointsByTarget["target"]=5;
        var run=new FeralDruidSimRunner().Run(r);Assert.Equal("rip",Assert.Single(Casts(run.Run)).AbilityKey);
        Assert.DoesNotContain(run.Run.Timeline,e=>e.Type==CombatEventType.Damage && e.IsPeriodic && e.AbilityKey=="rip");
    }
    [Theory][InlineData(14,false)][InlineData(14.01,true)]
    public void BiteBeforeEncounterEndPreservesExactBoundaryExclusion(double end,bool casts)
    {
        var r=Fast();r.DurationSeconds=(decimal)end;
        Assert.Equal(casts,Casts(new FeralDruidSimRunner().Run(r).Run).Any(e=>e.AbilityKey=="ferocious-bite"));
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void ImpossiblePreferredOpenerIsActionableEvenIfLaterTransitionsCouldAbandonIt(bool transition)
    {
        var r=Request();r.StartInProwl=true;
        if(transition){r.AdditionalTargets=1;r.TargetTransitions.Add(new(){AtSeconds=1,TargetKey="target-1"});r.FormTransitions.Add(new(){AtSeconds=6,Form="bear"});}
        var e=Assert.Throws<ArgumentException>(()=>new FeralDruidSimRunner().Run(r));
        Assert.Contains("TargetBehind=true",e.Message);Assert.Contains("Pounce",e.Message);Assert.Contains("StartInProwl",e.Message);
    }

    [Theory][InlineData("ravage",true,100,6,60)][InlineData("pounce",false,100,5,50)]
    [InlineData("ravage",true,0,6,60)][InlineData("pounce",false,0,5,50)]
    public void ValidOpenersKeepPaymentTimingAndResumeAutosEvenOnMiss(string opener,bool behind,int hit,int at,int energy)
    {
        var r=Request();r.StartInProwl=true;r.PreferredCatOpener=opener;r.TargetBehind=behind;r.Tuning.MeleeHitPercent=hit;r.DurationSeconds=10;
        var run=new FeralDruidSimRunner().Run(r);var first=Casts(run.Run).First();Assert.Equal(opener,first.AbilityKey);Assert.Equal(at,first.TimeSeconds);
        Assert.Contains(run.Run.Timeline,e=>e.AbilityKey==opener && e.Type==CombatEventType.ResourceChanged && e.Amount==-energy);
        var autos=run.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage && e.AbilityKey=="feral-auto").ToArray();Assert.NotEmpty(autos);Assert.All(autos,e=>Assert.True(e.TimeSeconds>at));
    }
    [Theory][InlineData("cat",false)][InlineData("bear",true)]
    public void UnusedOpenerOptionsRemainCompatible(string role,bool prowl)
    {
        var r=Request();r.Role=role;r.StartInProwl=prowl;
        Assert.True(new FeralDruidSimRunner().Run(r).DamageDone>0);
    }

    public static IEnumerable<object[]> ManaCases() {
        foreach(var role in new[]{"cat","bear"})for(var rank=0;rank<=5;rank++)foreach(var delta in new[]{-.01m,0m,.01m})
            yield return [role,rank,delta];
    }
    [Theory][MemberData(nameof(ManaCases))]
    public void InitialShiftUsesIncrementalInclusiveHotwManaAndActualCost(string role,int rank,decimal extraCost)
    {
        var r=Request();r.Role=role;r.DurationSeconds=1;r.MaximumMana=200;r.Talents.Ranks["heart-of-the-wild"]=rank;
        var effective=200m+54m*rank;r.Tuning.ShapeshiftMana=effective+extraCost;
        if(extraCost>0){var e=Assert.Throws<ArgumentException>(()=>new FeralDruidSimRunner().Run(r));Assert.Contains("Heart of the Wild",e.Message);Assert.Contains("ShapeshiftMana",e.Message);return;}
        var run=new FeralDruidSimRunner().Run(r);Assert.Equal(effective,run.Run.CustomMetrics["feral-heart-of-the-wild-maximum-mana"]);Assert.Equal(-extraCost,run.EndingMana);Assert.Equal(1,run.FormChanges);
        Assert.Equal(role=="cat"?800m+7.2m*rank:800m,run.Run.CustomMetrics["feral-heart-of-the-wild-attack-power"]);
        Assert.Equal(role=="bear"?5500m+88m*rank:5500m,run.Run.CustomMetrics["feral-heart-of-the-wild-maximum-health"]);
    }

    [Theory][InlineData("cat")][InlineData("bear")]
    public void FreeShiftZeroConversionsUnrelatedReductionAndLaterUnfundedShiftKeepContracts(string role)
    {
        var r=Request();r.Role=role;r.DurationSeconds=1;r.MaximumMana=1;r.Tuning.ShapeshiftMana=0;
        Assert.Equal(1,new FeralDruidSimRunner().Run(r).EndingMana);
        r.MaximumMana=200;r.Tuning.ShapeshiftMana=250;r.Talents.Ranks["heart-of-the-wild"]=5;r.Tuning.BaseIntellect=0;
        Assert.Throws<ArgumentException>(()=>new FeralDruidSimRunner().Run(r));r.Tuning.BaseIntellect=180;r.Tuning.ManaPerIntellect=0;
        Assert.Throws<ArgumentException>(()=>new FeralDruidSimRunner().Run(r));r.Talents.Ranks.Clear();r.Tuning.ShiftingPowerCostReductionPercent=100;
        Assert.Throws<ArgumentException>(()=>new FeralDruidSimRunner().Run(r));
        r.MaximumMana=250;r.DurationSeconds=8;r.FormTransitions.Add(new(){AtSeconds=6,Form=role=="cat"?"bear":"cat"});
        var run=new FeralDruidSimRunner().Run(r);Assert.Equal(1,run.FormChanges);Assert.Equal(1,run.Run.CustomMetrics["feral-failed-form-shifts"]);Assert.Equal(0,run.EndingMana);
    }

    [Theory][InlineData("Cat","ravage")][InlineData("cat","Ravage")][InlineData("bear","RAVAGE")]
    public void ExistingCaseSensitiveValuesRemainInvalid(string role,string opener)
    {
        var r=Request();r.Role=role;r.PreferredCatOpener=opener;
        Assert.Equal("Invalid Feral simulation request.",Assert.Throws<ArgumentException>(()=>new FeralDruidSimRunner().Run(r)).Message);
    }
    [Fact]
    public void PreexistingInvalidTuningAndPositiveManaRequirementsStillTakePrecedence()
    {
        var r=Request();r.StartInProwl=true;r.Tuning.ShapeshiftMana=-1;
        Assert.DoesNotContain("TargetBehind",Assert.Throws<ArgumentException>(()=>new FeralDruidSimRunner().Run(r)).Message);
        r.Tuning.ShapeshiftMana=0;r.MaximumMana=0;
        Assert.Equal("Invalid Feral simulation request.",Assert.Throws<ArgumentException>(()=>new FeralDruidSimRunner().Run(r)).Message);
    }

    [Theory][InlineData(true)][InlineData(false)]
    public async Task ControllerMapsSpecificValidationToBadRequestError(bool opener)
    {
        var r=Request();if(opener)r.StartInProwl=true;else r.MaximumMana=249.99m;
        var result=await new FeralDruidSimController().Run(r,default);
        var bad=Assert.IsType<BadRequestObjectResult>(result.Result);Assert.Equal(400,bad.StatusCode);
        Assert.Contains(opener?"TargetBehind=true":"ShapeshiftMana",JsonSerializer.Serialize(bad.Value));
    }

    [Fact]
    public async Task RepeatedConcurrentRunsAndValidationDoNotMutateCallerCollectionsOrTuning()
    {
        var r=Fast();r.AdditionalTargets=1;r.InitialComboPointsByTarget["target-1"]=4;
        r.TargetTransitions.Add(new(){AtSeconds=16,TargetKey="target-1"});r.TargetTransitions.Add(new(){AtSeconds=24,TargetKey="target"});
        r.FormTransitions.Add(new(){AtSeconds=32,Form="bear"});r.Talents.Ranks["heart-of-the-wild"]=5;
        var before=JsonSerializer.Serialize(r);var runner=new FeralDruidSimRunner();var expected=Canonical(runner.Run(r));
        var runs=await Task.WhenAll(Enumerable.Range(0,8).Select(_=>Task.Run(()=>runner.Run(r))));
        Assert.All(runs,run=>Assert.Equal(expected,Canonical(run)));Assert.Equal(before,JsonSerializer.Serialize(r));
        r.StartInProwl=true;before=JsonSerializer.Serialize(r);Assert.Throws<ArgumentException>(()=>runner.Run(r));Assert.Equal(before,JsonSerializer.Serialize(r));
    }
}
