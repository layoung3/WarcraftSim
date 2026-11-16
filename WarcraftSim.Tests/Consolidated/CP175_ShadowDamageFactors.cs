using System.Reflection;
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Priest;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>Historical arithmetic only: distinct school multipliers and the magic critical bonus.
/// Literal expected amounts are independent of the production factor helpers.</summary>
public sealed class Cp175ShadowDamageFactorsTests
{
    private static readonly string[] Spells = ["mind-blast", "shadow-word-death", "shadow-word-pain", "devouring-plague", "mind-flay"];
    private static ShadowPriestSimRequest Request(string spell, bool form = false, bool critical = false, int darkness = 0)
    {
        var r = new ShadowPriestSimRequest {
            DurationSeconds=3.1m, CaptureTimeline=true, Seed=175130,
            SpellPowerOverride=0m, SpellCritOverridePercent=critical?100m:0m, BaseSpellHitPercent=100m,
            TargetMaximumHealth=1000000000m, TargetInitialHealthPercent=spell=="shadow-word-death"?20m:100m,
            UseShadowWordPain=spell=="shadow-word-pain", UseDevouringPlague=spell=="devouring-plague",
            UseMindBlast=spell=="mind-blast", UseMindFlay=spell=="mind-flay",
            UseShadowWordDeathExecute=spell=="shadow-word-death", UseVampiricEmbrace=false,
            Tuning=new(){MindBlastDamage=100m,ShadowWordDeathDamage=100m,ShadowWordPainTick=100m,DevouringPlagueTick=100m,MindFlayTick=100m},
            StatAssumptions=new(){SpiritManaRegenerationPerSecondPerPoint=0m}
        };
        r.Character.BaseStats.Set("mp5",0m);
        r.Talents.Ranks["darkness"]=darkness; r.Talents.Ranks["shadowform"]=form?1:0;
        r.Talents.Ranks["shadow-weaving"]=0; r.Talents.Ranks["early-demise"]=0;
        r.Talents.Ranks["improved-mind-flay"]=0; r.Talents.Ranks["shadow-focus"]=0;
        return r;
    }
    private static CombatEvent[] Damage(ShadowPriestSimResponse r,string spell) =>
        r.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage&&e.AbilityKey==spell&&e.TargetActorKey=="target").ToArray();

    public static IEnumerable<object[]> SpellCases()
    {
        foreach(var spell in Spells) foreach(var form in new[]{false,true}) foreach(var crit in new[]{false,true})
            yield return [spell,form,crit,form?(crit?220m:110m):(crit?150m:100m)];
    }
    [Theory][MemberData(nameof(SpellCases))]
    public void AllFiveActualSpellDeliveriesUseLiteralMagicCriticalTotals(string spell,bool form,bool critical,decimal expected)
    {
        var r=Request(spell,form,critical); var output=new ShadowPriestSimRunner().Run(r); var hits=Damage(output,spell);
        Assert.Equal(spell=="mind-flay"?3:1,hits.Length);
        Assert.All(hits,e=>{Assert.Equal(expected,e.RawAmount);Assert.Equal(expected,e.Amount);Assert.Equal(critical?CombatResultTypes.Critical:CombatResultTypes.Hit,e.ResultKey);});
        Assert.Equal(expected*hits.Length,output.DamageDone);
        Assert.Equal(output.DamageDone,output.DamageByAbility[spell]);
        var cost=ForeverPriestShadowSpellbook.Create(r.Tuning,r.Talents,0m).Single(a=>a.Key==spell).ResourceCosts.Single().Amount;
        var debits=output.Run.Timeline.Where(e=>e.Type==CombatEventType.ResourceChanged&&e.AbilityKey==spell).ToArray();
        Assert.All(debits,e=>Assert.Equal(-cost,e.Amount));
        Assert.Equal(output.MaximumMana-cost*debits.Length,output.EndingMana);
    }

    public static IEnumerable<object[]> InitialCases()
    {
        decimal[] plain=[100m,102m,104m,106m,108m,110m], form=[110m,112.2m,114.4m,116.6m,118.8m,121m];
        for(var rank=0;rank<=5;rank++) foreach(var on in new[]{false,true}) yield return [rank,on,on?form[rank]:plain[rank]];
    }
    [Theory][MemberData(nameof(InitialCases))]
    public void InitialDarknessAndFormAreIndependentInPreviewAndActualHit(int rank,bool form,decimal amount)
    {
        var r=Request("mind-blast",form,darkness:rank);
        var before=JsonSerializer.Serialize(r);var preview=ShadowPriestSimRunner.Preview(r);
        Assert.Equal(amount-100m,preview.EffectiveStats.Get("shadow-damage-bonus-percent"));
        Assert.Equal(0m,preview.EffectiveStats.Get("spell-critical-chance-percent"));
        Assert.Equal(amount,Assert.Single(Damage(new ShadowPriestSimRunner().Run(r),"mind-blast")).Amount);
        Assert.Equal(before,JsonSerializer.Serialize(r));
    }

    [Theory][InlineData(false,false)][InlineData(false,true)][InlineData(true,false)][InlineData(true,true)]
    public void ProgressiveChannelCombinesGrowthSchoolAndCriticalFactors(bool form,bool critical)
    {
        var r=Request("mind-flay",form,critical,5);r.Talents.Ranks["shadow-weaving"]=3;r.Talents.Ranks["improved-mind-flay"]=2;
        decimal[] expected=(form,critical) switch {
            (false,false)=>[110m,134.64m,164.736m], (false,true)=>[165m,201.96m,247.104m],
            (true,false)=>[121m,148.104m,181.2096m], _=>[242m,296.208m,362.4192m]};
        var result=new ShadowPriestSimRunner().Run(r);var hits=Damage(result,"mind-flay");
        Assert.Equal(expected,hits.Select(e=>e.Amount!.Value));Assert.Equal(new[]{1m,2m,3m},hits.Select(e=>e.TimeSeconds));
        Assert.Equal(3,result.MaximumShadowWeavingStacks);
    }

    [Theory][InlineData(false,150,180,216)][InlineData(true,220,264,316.8)]
    public void NoWeavingProgressiveCriticalChannelKeepsSuccessiveTickGrowth(bool form,double a,double b,double c)
    {
        var r=Request("mind-flay",form,true);r.Talents.Ranks["improved-mind-flay"]=2;
        Assert.Equal(new[]{(decimal)a,(decimal)b,(decimal)c},Damage(new ShadowPriestSimRunner().Run(r),"mind-flay").Select(e=>e.Amount!.Value));
    }

    [Theory][InlineData("mind-blast",1078)][InlineData("shadow-word-death",924)][InlineData("shadow-word-pain",279.4)][InlineData("devouring-plague",233.2)][InlineData("mind-flay",286)]
    public void UnchangedProvisionalBasesAreNotRetunedToHideCorrectCriticalFactor(string spell,double expected)
    {
        var r=Request(spell,true,true);r.Tuning=new();
        Assert.All(Damage(new ShadowPriestSimRunner().Run(r),spell),e=>Assert.Equal((decimal)expected,e.Amount));
    }
    [Theory][InlineData("mind-blast",345.818)][InlineData("shadow-word-death",345.818)][InlineData("shadow-word-pain",290.4)][InlineData("devouring-plague",266.2)][InlineData("mind-flay",282.414)]
    public void AuthoredSpellPowerCoefficientsComposeWithSchoolAndCriticalMultiplier(string spell,double expected)
    {
        var r=Request(spell,true,true,5);r.SpellPowerOverride=100m;
        Assert.All(Damage(new ShadowPriestSimRunner().Run(r),spell),e=>Assert.Equal((decimal)expected,e.Amount));
    }

    [Theory][InlineData(2)][InlineData(3)][InlineData(0)]
    public void WeavingCapRefreshAndRealExpiryReplaceRatherThanAccumulateFactor(int percent)
    {
        var r=Request("mind-blast",true,darkness:5);r.Talents.Ranks["shadow-weaving"]=3;r.Tuning.ShadowWeavingDamagePerStackPercent=percent;
        var h=new Harness(r,23m);
        for(var i=0;i<7;i++){var time=(decimal)i;h.At(time,()=>h.Cast());}
        h.At(22m,()=>h.Cast()); h.Run();
        decimal[] expected=percent switch {
            2=>[121m,123.42m,125.84m,128.26m,130.68m,133.1m,133.1m,121m],
            3=>[121m,124.63m,128.26m,131.89m,135.52m,139.15m,139.15m,121m],
            _=>[121m,121m,121m,121m,121m,121m,121m,121m]};
        Assert.Equal(expected,h.Context.Timeline.Where(e=>e.Type==CombatEventType.Damage&&e.AbilityKey=="mind-blast").Select(e=>e.Amount!.Value));
        Assert.Equal(5,h.Reactive.MaximumStacksReached);
        Assert.Contains(h.Context.Timeline,e=>e.Type==CombatEventType.AuraRemoved&&e.EffectKey=="shadow-weaving-personal"&&e.TimeSeconds==21m);
        Assert.Equal(percent==0?21m:percent==2?23.42m:24.63m,h.Actor.Stats.Get("shadow-damage-bonus-percent"));
    }

    [Fact]
    public void NonEligibleReactiveEventsCannotChangeSchoolFactor()
    {
        var r=Request("mind-blast",true,darkness:5);r.Talents.Ranks["shadow-weaving"]=3;var h=new Harness(r,1m);
        foreach(var e in new[]{
            new CombatEvent{Type=CombatEventType.Damage,SourceActorKey="priest",TargetActorKey="target",SchoolKey="shadow",Amount=0m},
            new CombatEvent{Type=CombatEventType.Damage,SourceActorKey="priest",TargetActorKey="target",SchoolKey="fire",Amount=100m},
            new CombatEvent{Type=CombatEventType.Damage,SourceActorKey="priest",TargetActorKey="priest",SchoolKey="shadow",Amount=100m},
            new CombatEvent{Type=CombatEventType.Damage,SourceActorKey="target",TargetActorKey="priest",SchoolKey="shadow",Amount=100m},
            new CombatEvent{Type=CombatEventType.Damage,SourceActorKey="priest",TargetActorKey="target",SchoolKey="shadow",Amount=100m,AbilityKey="shadow-word-death-backlash"}})
            h.Reactive.Process(h.Context,e);
        Assert.Equal(21m,h.Actor.Stats.Get("shadow-damage-bonus-percent"));Assert.Equal(0,h.Reactive.MaximumStacksReached);
        Assert.Empty(h.Actor.ActiveAuras);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void DeathBacklashIsFixedMaximumHealthNotScaledDamageAndLethalHitSuppressesIt(bool lethal)
    {
        var r=Request("shadow-word-death",true,true,5);if(lethal)r.TargetMaximumHealth=500m;
        var output=new ShadowPriestSimRunner().Run(r);var hit=Assert.Single(Damage(output,"shadow-word-death"));
        Assert.Equal(242m,hit.RawAmount);Assert.Equal(lethal?100m:242m,hit.Amount);
        var backlash=output.Run.Timeline.Where(e=>e.AbilityKey=="shadow-word-death-backlash"&&e.Type==CombatEventType.Damage).ToArray();
        if(lethal){Assert.Empty(backlash);Assert.Equal(output.MaximumHealth,output.EndingHealth);}
        else {Assert.Equal(350m,Assert.Single(backlash).Amount);Assert.Equal(output.MaximumHealth-350m,output.EndingHealth);}
    }

    [Fact]
    public void FullHealthPlagueReportsRawHealingAndOverhealWithoutInventingEffectiveHealing()
    {
        var r=Request("devouring-plague",true,true,5);var output=new ShadowPriestSimRunner().Run(r);
        Assert.Equal(242m,Assert.Single(Damage(output,"devouring-plague")).Amount);
        var heal=Assert.Single(output.Run.Timeline,e=>e.Type==CombatEventType.Healing&&e.AbilityKey=="devouring-plague-heal");
        Assert.Equal(242m,heal.RawAmount);Assert.Equal(0m,heal.Amount);Assert.Equal(242m,heal.OverhealingAmount);
        Assert.Equal(0m,output.HealingDone);Assert.Equal(output.MaximumMana,output.EndingMana);
    }

    [Theory][InlineData("mind-blast")][InlineData("shadow-word-pain")][InlineData("devouring-plague")][InlineData("mind-flay")][InlineData("shadow-word-death")]
    public void GuaranteedMissRetainsNoDamageNoWeavingNoHealing(string spell)
    {
        var r=Request(spell,true,true,5);r.BaseSpellHitPercent=0m;r.Talents.Ranks["shadow-weaving"]=3;
        var output=new ShadowPriestSimRunner().Run(r);var misses=Damage(output,spell);Assert.NotEmpty(misses);
        Assert.All(misses,e=>{Assert.Equal(0m,e.Amount);Assert.Equal(CombatResultTypes.Miss,e.ResultKey);});Assert.Equal(0m,output.DamageDone);
        Assert.Equal(0,output.MaximumShadowWeavingStacks);Assert.Equal(0m,output.HealingDone);
    }

    public static IEnumerable<object[]> PowerCases()
    {
        foreach(var spell in Spells) foreach(var critical in new[]{false,true}) yield return [spell,critical,critical?180m:120m];
    }
    [Theory][MemberData(nameof(PowerCases))]
    public void LegalPowerInfusionBuildMultipliesCorrectNoFormCriticalTotal(string spell,bool critical,decimal expected)
    {
        var r=Request(spell,critical:critical);r.DurationSeconds=4.6m;r.UsePowerInfusion=true;
        r.Talents=new(){DisciplinePointsSpent=31,ShadowPointsSpent=20,Ranks=new(){["power-infusion"]=1,["penance"]=1,["mind-flay"]=1}};
        var result=new ShadowPriestSimRunner().Run(r);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastCompleted&&e.AbilityKey=="power-infusion");
        var hits=Damage(result,spell);Assert.NotEmpty(hits);Assert.All(hits,e=>Assert.Equal(expected,e.Amount));
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void InjuredVampiricAndPlagueHealingUseActualDamageAndIndependentHealthCap(bool smallDeficit)
    {
        var r=Request("devouring-plague",true,true,5);r.DurationSeconds=4.6m;r.TargetInitialHealthPercent=20m;
        r.UseVampiricEmbrace=true;r.UseShadowWordDeathExecute=true;r.Tuning.ShadowWordDeathBacklashFraction=smallDeficit?.02m:.10m;
        var result=new ShadowPriestSimRunner().Run(r);
        Assert.Equal(242m,Assert.Single(Damage(result,"shadow-word-death")).Amount);
        Assert.Equal(242m,Assert.Single(Damage(result,"devouring-plague")).Amount);
        var heals=result.Run.Timeline.Where(e=>e.Type==CombatEventType.Healing).ToArray();
        Assert.Equal(new[]{"vampiric-embrace-heal","vampiric-embrace-heal","devouring-plague-heal"},heals.Select(e=>e.AbilityKey));
        Assert.Equal(new[]{48.4m,48.4m,242m},heals.Select(e=>e.RawAmount!.Value));
        Assert.Equal(smallDeficit?new[]{48.4m,21.6m,0m}:new[]{48.4m,48.4m,242m},heals.Select(e=>e.Amount!.Value));
        Assert.Equal(smallDeficit?new[]{0m,26.8m,242m}:new[]{0m,0m,0m},heals.Select(e=>e.OverhealingAmount!.Value));
        Assert.Equal(smallDeficit?70m:338.8m,result.HealingDone);
        Assert.Equal(smallDeficit?3500m:3488.8m,result.EndingHealth);
    }

    [Fact]
    public void RepeatedPreviewAndSerializedRunsPreserveCallerAndDeterministicCombat()
    {
        var r=Request("mind-flay",true,true,5);r.DurationSeconds=12m;r.Talents.Ranks["shadow-weaving"]=2;
        var before=JsonSerializer.Serialize(r);var preview=ShadowPriestSimRunner.Preview(r);
        var first=new ShadowPriestSimRunner().Run(r);var second=new ShadowPriestSimRunner().Run(JsonSerializer.Deserialize<ShadowPriestSimRequest>(before)!);
        Assert.Equal(first.DamageDone,second.DamageDone);Assert.Equal(first.EndingMana,second.EndingMana);
        Assert.Equal(first.Run.Timeline.Select(e=>(e.Type,e.TimeSeconds,e.SourceActorKey,e.TargetActorKey,e.AbilityKey,e.EffectKey,e.Amount,e.RawAmount,e.ResultKey)),
            second.Run.Timeline.Select(e=>(e.Type,e.TimeSeconds,e.SourceActorKey,e.TargetActorKey,e.AbilityKey,e.EffectKey,e.Amount,e.RawAmount,e.ResultKey)));
        Assert.Equal(before,JsonSerializer.Serialize(r));
        preview.EffectiveStats.Set("shadow-damage-bonus-percent",999m);
        Assert.Equal(21m,ShadowPriestSimRunner.Preview(r).EffectiveStats.Get("shadow-damage-bonus-percent"));
    }

    [Fact]
    public void NonCriticalEffectKeepsHitMultiplierEvenAtGuaranteedCriticalChance()
    {
        var r=Request("mind-blast",true,true);var h=new Harness(r,1m);var aura=new AuraManager();
        var major=new PriestMajorCooldownProcessor(h.Actor,aura);
        var type=typeof(ShadowPriestSimRunner).GetNestedType("ShadowPriestRollResolver",BindingFlags.NonPublic)!;
        var roll=(ICombatRollResolver)Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,[100m,100m,2m,major],null)!;
        var ability=h.Actor.Abilities["mind-blast"].Definition;var effect=Assert.Single(ability.Effects);effect.CanCrit=false;
        var result=roll.Resolve(h.Context,h.Actor,h.Context.GetActor("target")!,ability,effect);
        Assert.True(result.Landed);Assert.Equal(CombatResultTypes.Hit,result.ResultKey);Assert.Equal(1m,result.AmountMultiplier);
    }

    private sealed class Harness : ICombatEventProcessor
    {
        public SimulationContext Context {get;}
        public SimulationActorState Actor {get;}
        public ForeverPriestShadowReactiveProcessor Reactive {get;}
        private readonly AuraManager _auras=new();
        private readonly AbilityExecutor _executor;
        private readonly Dictionary<string,Action> _actions=new();
        public Harness(ShadowPriestSimRequest request,decimal duration)
        {
            request.DurationSeconds=duration;
            var p=typeof(ShadowPriestSimRunner).GetMethod("Prepare",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[request,default(CancellationToken)])!;
            Context=(SimulationContext)p.GetType().GetProperty("Context")!.GetValue(p)!;
            Actor=(SimulationActorState)p.GetType().GetProperty("Actor")!.GetValue(p)!;
            var baseline=(decimal)p.GetType().GetProperty("BaseDamageBonus")!.GetValue(p)!;
            Reactive=new("priest",_auras,request.Talents,request.Tuning,baseline);
            _executor=new(_auras,new Hit(),new Unmitigated());
            var blast=Actor.Abilities["mind-blast"].Definition;blast.CastTimeSeconds=0m;blast.CooldownSeconds=0m;blast.GlobalCooldownSeconds=0m;blast.ResourceCosts.Clear();
        }
        public void At(decimal time,Action action){var key="damage-test-"+_actions.Count;_actions.Add(key,action);Context.ScheduleEvent(new(){TimeSeconds=time,Type=CombatEventType.RotationDecision,SourceActorKey="priest",EffectKey=key,IsInternal=true});}
        public void Cast()=>Assert.True(_executor.TryStartAbility(Context,"priest","target","mind-blast").Success);
        public void Process(SimulationContext context,CombatEvent e){if(e.EffectKey is {} key&&_actions.TryGetValue(key,out var a))a();}
        public void Run()=>new SimulationEngine([_executor,_auras,Reactive,this]).Run(Context);
    }
    private sealed class Hit:ICombatRollResolver
    {
        public CombatRollResult Resolve(SimulationContext context,SimulationActorState source,SimulationActorState target,AbilityDefinition ability,AbilityEffectDefinition effect)=>new(){Landed=true,ResultKey=CombatResultTypes.Hit,AmountMultiplier=1m};
    }
    private sealed class Unmitigated:IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext context,SimulationActorState source,SimulationActorState target,AbilityDefinition ability,AbilityEffectDefinition effect,decimal rawAmount)=>DamageMitigationResult.Unmitigated(rawAmount);
    }
}
