using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175PaladinEquipmentEvents
{
    private sealed class NoMitigation:IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext context,SimulationActorState source,
            SimulationActorState target,AbilityDefinition ability,AbilityEffectDefinition effect,decimal amount)=>
            DamageMitigationResult.Unmitigated(amount);
    }

    private static CombatEvent Hit(decimal time,string ability="holy-strike",string school="holy",
        string resolution="melee",string? hand="main-hand",bool periodic=false,bool internalEvent=false,
        string source="owner")=>new() {
            TimeSeconds=time,Type=CombatEventType.Damage,SourceActorKey=source,TargetActorKey="target",
            AbilityKey=ability,SchoolKey=school,ResolutionType=resolution,WeaponHandKey=hand,
            EffectDeliveryType=periodic?CombatEffectDeliveryType.Periodic:CombatEffectDeliveryType.Direct,
            IsInternal=internalEvent,Amount=100m};

    private static SimulationRunResult Execute(CombatEvent[] events,string cls="paladin",decimal speed=60m)
    {
        var context=new SimulationContext(new(){Seed=175113,DurationSeconds=100m,PrimaryActorKey="owner",CaptureTimeline=true});
        foreach(var key in new[]{"owner","target","ally"}) {
            var actor=new SimulationActorState {Key=key,Name=key,TeamKey=key,Level=60,MaximumHealth=1000000m};
            actor.InitializeHealth(actor.MaximumHealth);context.AddActor(actor);
        }
        var auras=new AuraManager();var executor=new AbilityExecutor(auras,new SimpleCombatRollResolver(),new NoMitigation());
        var fiery=ForeverEquipmentEffectsCatalog.GetCatalog().Effects.Single(x=>x.Key=="fiery-weapon");
        var processor=new ForeverEquipmentEffectProcessor(new CharacterEquipmentLoadout(),"owner","target",auras,executor,175113,
            explicitBindings:[new("main-hand",fiery,speed),new("off-hand",fiery,speed)],classKey:cls);
        var run=new SimulationEngine([executor,auras,processor]).Run(context,c=>{
            foreach(var ev in events)c.ScheduleEvent(ev);
        });
        processor.WriteMetrics(run);return run;
    }

    private static CombatEvent[] Procs(SimulationRunResult run,string hand="main-hand")=>run.Timeline
        .Where(x=>x.Type==CombatEventType.Damage&&x.AbilityKey=="equipment-fiery-weapon@"+hand).ToArray();

    [Theory]
    [InlineData("holy-strike")]
    [InlineData("seal-righteousness")]
    [InlineData("seal-fury")]
    [InlineData("seal-command")]
    public void HolyWeaponEventsTriggerOnlyTheirEquippedHandWithoutChangingTheOriginalEvent(string ability)
    {
        var ev=Hit(1m,ability);var original=JsonSerializer.Serialize(ev);
        // An explicit test-only slow weapon makes the catalog's existing PPM
        // certain, isolating event admission from random proc-rate evidence.
        var run=Execute([ev]);var proc=Assert.Single(Procs(run));
        Assert.Equal(40m,proc.Amount);Assert.Empty(Procs(run,"off-hand"));
        Assert.Equal(original,JsonSerializer.Serialize(ev));
        Assert.Equal("holy",ev.SchoolKey);Assert.Equal("melee",ev.ResolutionType);
    }

    [Theory]
    [InlineData("holy","ranged",null,false,false,"owner")]
    [InlineData("physical","ranged",null,false,false,"owner")]
    [InlineData("holy","spell",null,false,false,"owner")]
    [InlineData("holy","melee","off-hand",false,false,"owner")]
    [InlineData("holy","melee",null,false,false,"owner")]
    [InlineData("holy","melee","main-hand",true,false,"owner")]
    [InlineData("holy","melee","main-hand",false,true,"owner")]
    [InlineData("holy","melee","main-hand",false,false,"ally")]
    public void JudgementSpellsPeriodicInternalAndUnownedEventsNeverTriggerMeleeEnchants(
        string school,string resolution,string? hand,bool periodic,bool internalEvent,string source)
    {
        var run=Execute([Hit(1m,"judgement-or-control",school,resolution,hand,periodic,internalEvent,source)]);
        Assert.Empty(Procs(run));Assert.Empty(Procs(run,"off-hand"));
    }

    [Fact]
    public void RangedJudgementsDoNotConsumeMeleeProcRngAndReplayKeepsTheExactProcTimes()
    {
        var hits=Enumerable.Range(1,40).Select(i=>Hit(i*2m)).ToArray();
        var mixed=hits.SelectMany(x=>new[]{Hit(x.TimeSeconds-1m,"judgement",resolution:"ranged",hand:null),x}).ToArray();
        var clean=Procs(Execute(hits,speed:3.3m));var first=Procs(Execute(mixed,speed:3.3m));var repeat=Procs(Execute(mixed,speed:3.3m));
        Assert.NotEmpty(clean);
        Assert.Equal(clean.Select(x=>(x.TimeSeconds,x.Amount)),first.Select(x=>(x.TimeSeconds,x.Amount)));
        Assert.Equal(first.Select(x=>(x.TimeSeconds,x.Amount)),repeat.Select(x=>(x.TimeSeconds,x.Amount)));
    }

    [Theory]
    [InlineData("rogue")]
    [InlineData("warrior")]
    [InlineData("shaman")]
    public void ExistingClassPhysicalProcBoundariesArePreserved(string cls)
    {
        Assert.Single(Procs(Execute([Hit(1m,school:"physical")],cls)));
        Assert.Empty(Procs(Execute([Hit(1m)],cls)));
    }
}
