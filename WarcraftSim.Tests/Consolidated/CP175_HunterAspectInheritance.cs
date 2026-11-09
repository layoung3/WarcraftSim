using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Hunter;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175HunterAspectInheritance
{
    private sealed class NoMitigation : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext ctx,SimulationActorState source,
            SimulationActorState target,AbilityDefinition ability,AbilityEffectDefinition effect,
            decimal amount) => DamageMitigationResult.Unmitigated(amount);
    }
    private sealed class Driver(Action<SimulationContext,CombatEvent> action) : ICombatEventProcessor
    {
        public void Process(SimulationContext ctx,CombatEvent ev) => action(ctx,ev);
    }

    [Theory]
    [InlineData(true,"troll")]
    [InlineData(false,"troll")]
    [InlineData(true,"night-elf")]
    [InlineData(false,"night-elf")]
    public void OwnerOnlyAspectsNeverLeakThroughEquipmentRacialOrTrueshotRefreshes(
        bool rangedPrimary,string race)
    {
        var context=new SimulationContext(new SimulationRunOptions{
            Seed=17593,DurationSeconds=24m,PrimaryActorKey="hunter",CaptureTimeline=true
        });
        var owner=new SimulationActorState{Key="hunter",Name="Hunter",TeamKey="raid",Level=60};
        owner.InitializeHealth(5000m);
        owner.Stats.Set("attack-power",1000m);
        owner.Stats.Set("hunter-melee-attack-power",1000m);
        owner.Stats.Set("hunter-ranged-attack-power",1000m);
        if(rangedPrimary)SurvivalHunterAgilityStats.InitializeRanged(owner.Stats,100m,5);
        else SurvivalHunterAgilityStats.Initialize(owner.Stats,new ForeverSurvivalHunterTuning{
            Agility=100m,MeleeAttackPower=1000m,RangedAttackPower=1000m
        },5);
        context.AddActor(owner);
        var tuning=new ForeverBeastMasteryHunterTuning{
            PetBaseAttackPower=0m,PetOwnerAttackPowerInheritance=.25m
        };
        var pet=HunterCompanionComposition.Create(context,owner,ForeverHunterPetFamilies.Get("cat"),tuning);
        var initialPetAp=pet.Stats.Get("attack-power");
        var aura=new AuraManager();
        var executor=new AbilityExecutor(aura,new SimpleCombatRollResolver(),new NoMitigation());
        void Delta(SimulationContext ctx,SimulationActorState actor,IReadOnlyDictionary<string,decimal> delta) =>
            SurvivalHunterAgilityStats.ApplyRuntimeDelta(ctx,actor,pet,tuning,5,delta,rangedPrimary);
        var equipment=new ForeverEquipmentEffectProcessor(new CharacterEquipmentLoadout(),
            "hunter","hunter",aura,executor,17593,explicitBindings:[
                new ForeverEquipmentEffectBinding("trinket1",new ForeverExecutableEquipmentEffect{
                    Key="aspect-inheritance-fixture",Kind="on-use",Trigger="manual",Modeled=true,
                    SourceBuild="synthetic",SourceUrl="https://example.test/hunter-inheritance",
                    AutoUse=false,DurationSeconds=3m,CooldownSeconds=100m,
                    StatBonuses=new(){["agility"]=20m,["attack-power"]=40m}
                })
            ],classKey:"hunter",onStatDelta:Delta);
        var racial=new ForeverRacialActiveProcessor("hunter",race,true,aura,onStatDelta:Delta);
        // Activate the actual racial processor after Hawk is present, rather than at pull.
        var delayedRacial=new Driver((ctx,ev)=> {
            if(ev.Type!=CombatEventType.SimulationStarted)racial.Process(ctx,ev);
        });
        var aspects=new HunterAspectProcessor(owner,aura,rangedPrimary);
        var trueshot=new HunterTrueshotAuraProcessor(owner,pet,tuning,100m,rangedPrimary);
        var samples=new Dictionary<decimal,(decimal Eligible,decimal Pet,decimal Ranged,decimal Melee)>();
        void ApplyAspect(SimulationContext ctx,string key,string ability,decimal duration) =>
            aura.ApplyAura(ctx,owner,new AuraDefinition{Key=key,Name=ability,DurationSeconds=duration},
                owner.Key,ability,key);
        var driver=new Driver((ctx,ev)=> {
            if(ev.EffectKey=="apply-trueshot")
                ApplyAspect(ctx,"hunter-trueshot-aura","trueshot-aura",2m);
            if(ev.EffectKey=="apply-beast")
                ApplyAspect(ctx,"hunter-aspect-beast","aspect-of-the-beast",3m);
            if(ev.EffectKey!="sample")return;
            // A zero-AP refresh is exactly what unrelated haste/crit callbacks do.
            ForeverEquipmentHunterDynamicStats.RefreshCompanionAttackPower(owner,rangedPrimary);
            HunterCompanionComposition.RefreshOwnerStats(ctx,pet,tuning);
            samples[ctx.CurrentTimeSeconds]=(owner.Stats.Get("companion-attack-power"),
                pet.Stats.Get("attack-power"),
                owner.Stats.Get(rangedPrimary?"attack-power":"hunter-ranged-attack-power"),
                owner.Stats.Get(rangedPrimary?"hunter-melee-attack-power":"attack-power"));
        });
        var run=new SimulationEngine([executor,aura,aspects,trueshot,equipment,delayedRacial,driver])
            .Run(context,ctx=> {
                ApplyAspect(ctx,"hunter-aspect-hawk","aspect-of-the-hawk",20m);
                void Schedule(decimal at,string key,CombatEventType type=CombatEventType.RotationDecision) =>
                    ctx.ScheduleEvent(new CombatEvent{TimeSeconds=at,Type=type,
                        SourceActorKey="hunter",EffectKey=key,IsInternal=true});
                Schedule(2m,"aspect-inheritance-fixture@trinket1",CombatEventType.EquipmentEffectActivation);
                Schedule(2m,race=="troll"?ForeverRacialActiveProcessor.BerserkingEffect:
                    ForeverRacialActiveProcessor.ElunesLightEffect,CombatEventType.EquipmentEffectActivation);
                Schedule(7m,"apply-trueshot");Schedule(18m,"apply-beast");
                foreach(var at in new decimal[]{1,3,6,8,10,16,19,22})Schedule(at,"sample");
            });
        Assert.Equal(1,racial.Activations);
        Assert.Equal(1,equipment.Applications["aspect-inheritance-fixture@trinket1"]);
        foreach(var at in new decimal[]{1,6,10,16,19,22}) {
            Assert.Equal(1030m,samples[at].Eligible);
            Assert.Equal(initialPetAp,samples[at].Pet);
        }
        Assert.Equal(1116m,samples[3m].Eligible); // 40 generic AP + 20*1.15*2 RAP.
        Assert.Equal(initialPetAp+86m*.25m,samples[3m].Pet);
        Assert.Equal(1130m,samples[8m].Eligible); // Trueshot inherits once, Hawk never does.
        Assert.Equal(initialPetAp+100m*.25m,samples[8m].Pet);
        Assert.Equal(1150m,samples[1m].Ranged);
        Assert.Equal(1030m,samples[19m].Ranged);
        Assert.Equal(1125m,samples[19m].Melee);
        Assert.Equal(1015m,samples[22m].Melee);
        Assert.Equal(0m,owner.Stats.Get(HunterAspectProcessor.OwnerOnlyRangedAttackPower));
        Assert.Equal(0m,owner.Stats.Get(HunterAspectProcessor.OwnerOnlyMeleeAttackPower));
        Assert.Equal(0m,owner.Stats.Get("haste-percent"));
        Assert.Contains(run.Timeline,e=>e.Type==CombatEventType.AuraRemoved &&
            e.AbilityKey=="aspect-of-the-beast");
    }
}
