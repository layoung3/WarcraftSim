using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175WarriorEnchantHandEligibility
{
    private sealed class NoMitigation : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext context,
            SimulationActorState source,SimulationActorState target,
            AbilityDefinition ability,AbilityEffectDefinition effect,decimal amount) =>
            DamageMitigationResult.Unmitigated(amount);
    }

    private static CombatEvent Hit(decimal time,string ability="bloodthirst",string? hand=null) => new() {
        TimeSeconds=time,Type=CombatEventType.Damage,SourceActorKey="owner",
        TargetActorKey="target",AbilityKey=ability,Amount=100m,
        SchoolKey="physical",ResolutionType="melee",WeaponHandKey=hand
    };

    private static ForeverEquipmentEffectBinding Fiery(string hand,decimal speed=60m) => new(
        hand,ForeverEquipmentEffectsCatalog.GetCatalog().Effects.Single(e=>e.Key=="fiery-weapon"),speed);

    private static SimulationRunResult Execute(ForeverEquipmentEffectBinding[] effects,
        CombatEvent[] events,string classKey="warrior")
    {
        var context=new SimulationContext(new SimulationRunOptions {
            Seed=175930,DurationSeconds=100m,PrimaryActorKey="owner",CaptureTimeline=true
        });
        foreach(var key in new[]{"owner","target"})
        {
            var actor=new SimulationActorState { Key=key,Name=key,
                TeamKey=key,Level=60,MaximumHealth=1000000m };
            actor.InitializeHealth(actor.MaximumHealth);context.AddActor(actor);
        }
        var auras=new AuraManager();
        var executor=new AbilityExecutor(auras,new SimpleCombatRollResolver(),new NoMitigation());
        var processor=new ForeverEquipmentEffectProcessor(new CharacterEquipmentLoadout(),
            "owner","target",auras,executor,175930,explicitBindings:effects,classKey:classKey);
        var run=new SimulationEngine([executor,auras,processor]).Run(context,started=>{
            foreach(var ev in events)started.ScheduleEvent(ev);
        });
        processor.WriteMetrics(run);
        return run;
    }

    private static CombatEvent[] Procs(SimulationRunResult run,string hand) => run.Timeline
        .Where(e=>e.Type==CombatEventType.Damage && e.AbilityKey=="equipment-fiery-weapon@"+hand).ToArray();

    [Theory]
    [InlineData(true,false)]
    [InlineData(false,true)]
    [InlineData(true,true)]
    public void BloodthirstTriggersOnlySelectedMainHandFieryAndNeverRewritesTheEvent(bool main,bool off)
    {
        var bindings=new List<ForeverEquipmentEffectBinding>();
        if(main)bindings.Add(Fiery("main-hand"));
        if(off)bindings.Add(Fiery("off-hand"));
        var hit=Hit(1m);var original=JsonSerializer.Serialize(hit);
        // Explicit 60s test binding makes the catalog's provisional 6 PPM
        // deterministic without changing its definition or production rate.
        var run=Execute(bindings.ToArray(),[hit]);
        Assert.Equal(main?1:0,Procs(run,"main-hand").Length);
        Assert.Empty(Procs(run,"off-hand"));
        Assert.All(Procs(run,"main-hand"),e=>Assert.Equal(40m,e.Amount));
        Assert.Equal(original,JsonSerializer.Serialize(hit));
        Assert.Null(hit.WeaponHandKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void UnknownHandlessWarriorAttackFailsClosedForBothEnchants(string? hand)
    {
        var run=Execute([Fiery("main-hand"),Fiery("off-hand")],[Hit(1m,"unknown-melee",hand)]);
        Assert.Empty(Procs(run,"main-hand"));Assert.Empty(Procs(run,"off-hand"));
    }

    [Theory]
    [InlineData("main-hand")]
    [InlineData("off-hand")]
    public void ExplicitMeleeHandStillMatchesOnlyItsOwnEnchant(string hand)
    {
        var run=Execute([Fiery("main-hand"),Fiery("off-hand")],[Hit(1m,"weapon-strike",hand)]);
        Assert.Single(Procs(run,hand));
        Assert.Empty(Procs(run,hand=="main-hand"?"off-hand":"main-hand"));
    }

    [Fact]
    public void OtherClassesAndUnhandedItemAndSetTriggersKeepTheirExistingContracts()
    {
        var other=Execute([Fiery("main-hand"),Fiery("off-hand")],[Hit(1m,"unknown-melee")],"hunter");
        Assert.Single(Procs(other,"main-hand"));Assert.Single(Procs(other,"off-hand"));
        ForeverExecutableEquipmentEffect Proc(string key) => new() {
            Key=key,Kind="proc-damage",Trigger="melee-hit",Damage=40m,
            ProcChancePercent=100m,School="fire",Modeled=true
        };
        var item=Proc("test-item");item.ItemId=1;
        var set=Proc("test-set");set.SetKey="test";set.RequiredPieces=2;
        var run=Execute([new("trinket1",item),new("set-test-2",set)],[Hit(1m,"unknown-melee")]);
        Assert.Contains(run.Timeline,e=>e.Type==CombatEventType.Damage && e.AbilityKey=="equipment-test-item@trinket1");
        Assert.Contains(run.Timeline,e=>e.Type==CombatEventType.Damage && e.AbilityKey=="equipment-test-set@set-test-2");
    }

    [Fact]
    public void IneligibleHandlessEventsDoNotConsumeEnchantRngAndSeededReplayIsExact()
    {
        var hits=Enumerable.Range(1,40).Select(i=>Hit(i*2m,"off-hand-auto","off-hand")).ToArray();
        var mixed=hits.SelectMany(hit=>new[]{Hit(hit.TimeSeconds-1m),hit}).ToArray();
        var binding=new[]{Fiery("off-hand",2.4m)};
        var clean=Execute(binding,hits);
        var withBloodthirst=Execute(binding,mixed);
        var replay=Execute(binding,mixed);
        Assert.NotEmpty(Procs(clean,"off-hand"));
        Assert.Equal(Procs(clean,"off-hand").Select(e=>(e.TimeSeconds,e.Amount)),
            Procs(withBloodthirst,"off-hand").Select(e=>(e.TimeSeconds,e.Amount)));
        Assert.Equal(Procs(withBloodthirst,"off-hand").Select(e=>(e.TimeSeconds,e.Amount)),
            Procs(replay,"off-hand").Select(e=>(e.TimeSeconds,e.Amount)));
        var withUnknown=Execute(binding,hits.SelectMany(hit=>new[]{Hit(hit.TimeSeconds-1m,"unknown"),hit}).ToArray());
        Assert.Equal(Procs(clean,"off-hand").Select(e=>e.TimeSeconds),Procs(withUnknown,"off-hand").Select(e=>e.TimeSeconds));
    }
}
