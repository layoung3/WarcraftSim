using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Shaman;

namespace WarcraftSim.Tests.Consolidated;

// Regression for existing imported Searing conventions, not current-server certification.
public sealed class Cp175EnhancementFireTotem
{
    private static EnhancementShamanSimRequest Request(string call, decimal duration = 40m)
    {
        var r = new EnhancementShamanSimRequest {
            DurationSeconds=duration, CaptureTimeline=true, MainHandImbue="none",
            UseWindfuryWeapon=false, UseWindfuryTotem=false, UseStormstrike=false,
            UseEarthShock=false, UseRageOfTheFarseer=false, UseMaelstromLightningBolt=false,
            UseLightningBoltFiller=false, BaseSpellHitPercent=100m, BaseMeleeHitPercent=100m,
            CriticalChancePercent=0m, ProvisionalGlancingChancePercent=0m,
            Talents=new(){Ranks=new()}, Tuning=new(){ManaRegenPerSecond=0m},
            UseCallOfElements=call=="call-of-elements", UseCallOfAncestors=call=="call-of-ancestors",
            UseCallOfSpirits=call=="call-of-spirits",
            CallOfElementsTotems=["searing-totem"], CallOfAncestorsTotems=["searing-totem"],
            CallOfSpiritsTotems=["searing-totem"]
        };
        r.Character.BaseStats.Set("intellect",1000m);
        r.Character.BaseStats.Set("spell-power",100m);
        r.Character.BaseStats.Set("mp5",0m);
        return r;
    }

    private static void Receipt(string name, object value)
    {
        // Opt-in local audit receipts. Ordinary CI has no filesystem side effects here.
        var directory=Environment.GetEnvironmentVariable("CP175_FIRE_RECEIPTS");
        if(string.IsNullOrEmpty(directory)) return;
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory,name+".json"),JsonSerializer.Serialize(value));
    }

    [Theory]
    [InlineData("call-of-elements")]
    [InlineData("call-of-ancestors")]
    [InlineData("call-of-spirits")]
    public void FireOnlyCallPaysOnceAndKeepsLiteralPeriodicDamage(string call)
    {
        var r=Request(call); var s=new EnhancementShamanSimRunner().Run(r);
        Receipt(call,new{request=r,response=s});
        var events=s.Run.Timeline;
        Assert.Equal(3m,Assert.Single(events,e=>e.Type==CombatEventType.AbilityCastCompleted && e.AbilityKey==call).TimeSeconds);
        Assert.Equal(-170m,Assert.Single(events,e=>e.Type==CombatEventType.ResourceChanged && e.AbilityKey==call).Amount);
        var marker=Assert.Single(events,e=>e.Type==CombatEventType.AuraApplied && e.EffectKey=="fire-totem-slot");
        Assert.Equal(3m,marker.TimeSeconds); Assert.Equal("shaman",marker.SourceActorKey); Assert.Equal("shaman",marker.TargetActorKey);
        var ticks=events.Where(e=>e.Type==CombatEventType.Damage && e.EffectKey=="searing-totem-tick").ToArray();
        Assert.Equal(Enumerable.Range(0,18).Select(i=>5m+2m*i),ticks.Select(e=>e.TimeSeconds));
        Assert.All(ticks,e=>{Assert.Equal(call,e.AbilityKey);Assert.Equal(56.005m,e.Amount);});
        Assert.Equal(1008.09m,s.DamageByAbility[call]);
        Assert.Equal(s.MaximumMana-170m,s.EndingMana);
    }

    [Theory]
    [InlineData("call-of-elements")]
    [InlineData("call-of-ancestors")]
    [InlineData("call-of-spirits")]
    public void FireOnlyCallRecastsOnlyAfterOwnedLifetimeExpires(string call)
    {
        var r=Request(call,64m); var s=new EnhancementShamanSimRunner().Run(r);
        Receipt(call+"-expiry",new{request=r,response=s});
        Assert.Equal(new[]{3m,61m},s.Run.Timeline.Where(e=>e.Type==CombatEventType.AbilityCastCompleted && e.AbilityKey==call).Select(e=>e.TimeSeconds));
        Assert.Equal(58m,Assert.Single(s.Run.Timeline,e=>e.Type==CombatEventType.AuraRemoved && e.EffectKey=="fire-totem-slot").TimeSeconds);
        var ticks=s.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage && e.EffectKey=="searing-totem-tick").ToArray();
        Assert.Equal(Enumerable.Range(0,27).Select(i=>5m+2m*i).Append(63m),ticks.Select(e=>e.TimeSeconds));
        Assert.All(ticks,e=>Assert.Equal(56.005m,e.Amount));
        Assert.Equal(s.MaximumMana-340m,s.EndingMana);
    }

    [Theory]
    [InlineData("call-of-elements","flametongue")]
    [InlineData("call-of-ancestors","flametongue")]
    [InlineData("call-of-spirits","flametongue")]
    [InlineData("call-of-elements","frostbrand")]
    [InlineData("call-of-ancestors","frostbrand")]
    [InlineData("call-of-spirits","frostbrand")]
    public void CallDoesNotStarveRealShieldAndImbueDamage(string call,string imbue)
    {
        var r=Request(call); r.UseLightningShield=true; r.IncomingBossHitDamage=10m;
        r.MainHandImbue=imbue; r.FrostbrandProcChancePercent=100m;
        var s=new EnhancementShamanSimRunner().Run(r); Receipt(call+"-"+imbue,new{request=r,response=s});
        Assert.Single(s.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastCompleted && e.AbilityKey==call);
        Assert.Contains(s.Run.Timeline,e=>e.Type==CombatEventType.Damage && e.AbilityKey=="lightning-shield" && e.Amount>0m);
        Assert.Contains(s.Run.Timeline,e=>e.Type==CombatEventType.Damage && e.AbilityKey?.Contains(imbue)==true && e.Amount>0m);
        Assert.Equal(imbue,s.ActiveMainHandImbue);
    }

    [Fact]
    public void PreserveControlReceiptsAndNoUnrequestedFireNova()
    {
        var noCall=Request("none",64m); noCall.UseLightningShield=true; noCall.MainHandImbue="flametongue"; noCall.IncomingBossHitDamage=10m;
        var plain=new EnhancementShamanSimRunner().Run(noCall); Receipt("control-no-call",new{request=noCall,response=plain});
        Assert.DoesNotContain(plain.Run.Timeline,e=>e.EffectKey=="fire-totem-slot");
        foreach(var slot in new[]{"strength-of-earth-totem","windfury-totem","mana-spring-totem"})
        {
            var r=Request("call-of-elements",64m); r.CallOfElementsTotems=[slot,"searing-totem"];
            r.UseLightningShield=true; r.IncomingBossHitDamage=10m; r.MainHandImbue="flametongue";
            var s=new EnhancementShamanSimRunner().Run(r); Receipt("control-multi-"+slot,new{request=r,response=s});
            Assert.Single(s.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastCompleted && e.AbilityKey=="call-of-elements");
            Assert.DoesNotContain(s.Run.Timeline,e=>e.AbilityKey=="fire-nova");
        }
        var elemental=ElementalPreRaidProfileCatalog.Setup("throughput","raid-sustain");
        elemental.DurationSeconds=64m; elemental.CaptureTimeline=true; elemental.CustomPriority=["call-of-elements","fire-nova","lightning-bolt"];
        Receipt("control-elemental",new{request=elemental,response=new ElementalShamanSimRunner().Run(elemental)});
        var resto=new RestorationShamanSimRequest {DurationSeconds=64m,CaptureTimeline=true,UseSharedTotemLifecycle=true,UseCallOfElements=true,UseTotemicRecall=true};
        Receipt("control-restoration",new{request=resto,response=new RestorationShamanSimRunner().Run(resto)});
    }

    [Fact]
    public void RecallRefundsOneLiveSearingOnceAndStopsTicks()
    {
        var h=new Harness(12m); h.At(0m,()=>h.Cast("call-of-elements"));
        h.At(6m,()=>h.Cast("totemic-recall")); h.At(8m,()=>h.Cast("totemic-recall"));
        var run=h.Run(); Receipt("harness-recall",run);
        Assert.Equal(42.5m,Assert.Single(run.Timeline,e=>e.EffectKey=="totemic-recall-refund").Amount);
        Assert.Equal(872.5m,h.Owner.Resources["mana"].Current);
        Assert.Equal(5m,Assert.Single(run.Timeline,e=>e.Type==CombatEventType.Damage).TimeSeconds);
        Assert.Empty(h.Slots.ActiveSlots); Assert.False(h.HasMarker());
        Assert.Equal(6m,Assert.Single(run.Timeline,e=>e.Type==CombatEventType.AuraRemoved && e.EffectKey=="fire-totem-slot").TimeSeconds);
    }

    [Fact]
    public void ExpiredSearingCannotRefundMana()
    {
        var h=new Harness(61m); h.At(0m,()=>h.Cast("call-of-elements")); h.At(59m,()=>h.Cast("totemic-recall"));
        var run=h.Run(); Receipt("harness-expired-recall",run);
        Assert.DoesNotContain(run.Timeline,e=>e.EffectKey=="totemic-recall-refund");
        Assert.Equal(830m,h.Owner.Resources["mana"].Current); Assert.False(h.HasMarker()); Assert.Empty(h.Slots.ActiveSlots);
    }

    [Fact]
    public void OwnershipAndRefreshPreserveOnlyTheLiveOwnedMarker()
    {
        var h=new Harness(60m);
        h.At(0m,()=>h.Apply("other"));
        h.At(.1m,()=>Assert.False(h.HasMarker()));
        h.At(1m,()=>h.Apply("shaman"));
        h.At(2m,()=>h.Apply("shaman"));
        h.At(3m,()=>h.Auras.RemoveAuras(h.Context,h.Target,"searing-totem","other","foreign removal"));
        h.At(3.1m,()=>Assert.True(h.HasMarker()));
        h.At(56.1m,()=>Assert.True(h.HasMarker())); // Original expiry is stale after refresh.
        h.At(57.1m,()=>Assert.False(h.HasMarker()));
        var run=h.Run(); Receipt("harness-ownership-refresh",run);
        Assert.Equal(57m,Assert.Single(run.Timeline,e=>e.Type==CombatEventType.AuraRemoved && e.EffectKey=="fire-totem-slot").TimeSeconds);
    }

    private sealed class Harness
    {
        public SimulationContext Context {get;}
        public SimulationActorState Owner {get;}=new(){Key="shaman",TeamKey="raid",Level=60};
        public SimulationActorState Target {get;}=new(){Key="target",TeamKey="enemy",Level=60};
        public AuraManager Auras {get;}=new();
        public ShamanTotemLifecycleProcessor Slots {get;}
        private readonly AbilityExecutor executor;
        private readonly Dictionary<string,Action> steps=new();
        public Harness(decimal duration)
        {
            Context=new(new(){DurationSeconds=duration,CaptureTimeline=true,PrimaryActorKey="shaman"});
            Owner.InitializeHealth(1000m); Target.InitializeHealth(100000m); Owner.AddResource(new("mana",1000m,1000m));
            Context.AddActor(Owner);Context.AddActor(Target);
            var book=ForeverEnhancementShamanSpellbook.Create(new(),new(){Ranks=new()},100m,2m,0m,0m);
            foreach(var spell in book)Owner.AddAbility(spell);
            Owner.AddAbility(ForeverShamanTotemCalls.Call("call-of-elements",book,["searing-totem"])); Owner.AddAbility(ForeverShamanTotemCalls.Recall());
            executor=new(Auras,new SimpleCombatRollResolver(),new NoMitigation()); Slots=new(Owner,Auras);
        }
        public bool HasMarker()=>Owner.ActiveAuras.Any(a=>a.Definition.Key=="shaman-fire-totem-active" && a.IsActiveAt(Context.CurrentTimeSeconds));
        public void Cast(string key)=>Assert.True(executor.TryStartAbility(Context,"shaman","target",key).Success);
        public void Apply(string source)=>Auras.ApplyAura(Context,Target,new AuraDefinition{Key="searing-totem",Name="Searing",DurationSeconds=55m},source,"call-of-elements","searing-totem-tick");
        public void At(decimal at,Action step){var key="step-"+steps.Count;steps.Add(key,step);Context.ScheduleEvent(new(){TimeSeconds=at,Type=CombatEventType.RotationDecision,EffectKey=key,IsInternal=true});}
        public SimulationRunResult Run()=>new SimulationEngine([executor,Auras,new ShamanFireTotemProcessor(Owner,Auras),Slots,new Steps(steps)]).Run(Context);
    }
    private sealed class Steps(Dictionary<string,Action> steps):ICombatEventProcessor
    {public void Process(SimulationContext context,CombatEvent e){if(e.EffectKey is not null && steps.TryGetValue(e.EffectKey,out var step))step();}}
    private sealed class NoMitigation:IDamageMitigationResolver
    {public DamageMitigationResult Resolve(SimulationContext c,SimulationActorState s,SimulationActorState t,AbilityDefinition a,AbilityEffectDefinition e,decimal amount)=>DamageMitigationResult.Unmitigated(amount);}
}
