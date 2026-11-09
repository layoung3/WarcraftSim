using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Data.Forever.Hunter;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175HunterResourcefulness
{
    [Theory]
    [InlineData("raptor-strike")]
    [InlineData("aimed-shot")]
    public void DirectAbilityCriticalRestoresHalfSpiritOnlyInsideFiveSecondRule(string ability)
    {
        var h = new Harness();
        h.Add(Spend(0)); h.Add(Hit(0, ability)); h.Run();
        Assert.Equal(1, h.Proc.Procs);
        Assert.Equal(40m, h.Proc.ManaRestored);
        Assert.Equal(new decimal[] {1,2,3,4}, h.Gains.Select(e => e.TimeSeconds));
        Assert.All(h.Gains, e => Assert.Equal(10m, e.Amount));
    }

    [Theory]
    [InlineData("auto-shot")]
    [InlineData("ranged-auto")]
    [InlineData("melee-auto")]
    [InlineData("offhand-auto")]
    [InlineData("explosive-trap")]
    [InlineData("immolation-trap")]
    [InlineData("lacerating-strikes")]
    public void AutosAndTrapsCannotProc(string ability)
    {
        var h = new Harness(); h.Add(Spend(0)); h.Add(Hit(0, ability)); h.Run();
        Assert.Equal(0, h.Proc.Procs); Assert.Empty(h.Gains);
    }

    [Fact]
    public void PetHawkNonCriticalAndPeriodicDamageCannotProc()
    {
        var h = new Harness(); h.Add(Spend(0));
        foreach (var source in new[] {"pet", "hawk"}) {
            var e=Hit(0,"claw"); e.SourceActorKey=source; h.Add(e);
        }
        var normal=Hit(0,"raptor-strike"); normal.IsCritical=false; h.Add(normal);
        foreach (var delivery in new[] {CombatEffectDeliveryType.Periodic}) {
            var e=Hit(0,"serpent-sting"); e.EffectDeliveryType=delivery; h.Add(e);
        }
        h.Run(); Assert.Equal(0,h.Proc.Procs); Assert.Empty(h.Gains);
    }

    [Theory]
    [InlineData("equipment-fire-proc")]
    [InlineData("dense-dynamite")]
    [InlineData("unknown-owner-damage")]
    public void NonHunterOwnerDamageCannotProc(string ability)
    {
        var h=new Harness();
        // Consumables/equipment can be registered actions but are not Hunter abilities.
        if(ability!="unknown-owner-damage")
            h.Owner.Abilities[ability]=new AbilityState(new AbilityDefinition {
                Key=ability,ClassKey="consumable"
            });
        h.Add(Spend(0)); h.Add(Hit(0,ability)); h.Run();
        Assert.Equal(0,h.Proc.Procs); Assert.Empty(h.Gains);
    }

    [Fact]
    public void VolleyChannelCriticalPreservesSurvivalNonPeriodicEligibility()
    {
        var h=new Harness();
        h.Owner.Abilities["volley"]=new AbilityState(ForeverHunterSharedAbilities.CreateVolley());
        var pulse=Hit(0,"volley"); pulse.EffectDeliveryType=CombatEffectDeliveryType.ChannelTick;
        h.Add(Spend(0)); h.Add(pulse); h.Run();
        Assert.Equal(1,h.Proc.Procs); Assert.Equal(40m,h.Proc.ManaRestored);
    }

    [Fact]
    public void OnlyActualOwnerManaSpendsOpenFiveSecondWindow()
    {
        var h=new Harness(); h.Add(Hit(0,"aimed-shot"));
        var gain=Spend(0); gain.Amount=10m; h.Add(gain);
        var pet=Spend(0); pet.SourceActorKey="pet"; h.Add(pet);
        var focus=Spend(0); focus.ResourceKey="focus"; h.Add(focus);
        h.Add(new CombatEvent {TimeSeconds=0,Type=CombatEventType.AbilityCastStarted,
            SourceActorKey="hunter",AbilityKey="aimed-shot"});
        h.Run(); Assert.Equal(1,h.Proc.Procs); Assert.Empty(h.Gains);
    }

    [Theory]
    [InlineData(false,29)]
    [InlineData(true,39)]
    public void ThirtySecondAuraRefreshDoesNotStackTicksAndExpires(bool refresh,int ticks)
    {
        var h=new Harness(); h.Add(Hit(0,"aimed-shot"));
        if(refresh) h.Add(Hit(10,"raptor-strike"));
        for(int t=0;t<=44;t+=4) h.Add(Spend(t));
        h.Run(); Assert.Equal(ticks*10m,h.Proc.ManaRestored);
        Assert.Equal(ticks,h.Gains.Count());
        Assert.Equal(ticks,h.Gains.Select(e=>e.TimeSeconds).Distinct().Count());
        Assert.Empty(h.Owner.ActiveAuras);
    }

    [Fact]
    public void RankZeroRemovesProcAndRankOneRollsFiftyPercentDeterministically()
    {
        foreach(int rank in new[]{0,1,2}) {
            var h=new Harness(rank); var rng=new Random(175);
            int expected=0;
            for(int i=0;i<100;i++) {
                h.Add(Hit(0,"aimed-shot"));
                if(rng.NextDouble()*100 < rank*50) expected++;
            }
            h.Run(); Assert.Equal(expected,h.Proc.Procs);
            Assert.Empty(h.Gains);
        }
    }

    [Fact]
    public void RestorationReportsActualManaAfterCap()
    {
        var h=new Harness(); h.Owner.Resources["mana"]=new ResourceState("mana",15m,0m);
        h.Add(Spend(0)); h.Add(Hit(0,"aimed-shot")); h.Run();
        Assert.Equal(15m,h.Proc.ManaRestored);
        Assert.Equal(new decimal?[]{10m,5m},h.Gains.Select(e=>e.Amount));
        Assert.Equal(15m,h.Owner.Resources["mana"].Current);
    }

    private static CombatEvent Hit(decimal time,string ability)=>new() {
        TimeSeconds=time,Type=CombatEventType.Damage,SourceActorKey="hunter",
        TargetActorKey="target",AbilityKey=ability,Amount=100m,IsCritical=true
    };
    private static CombatEvent Spend(decimal time)=>new() {
        TimeSeconds=time,Type=CombatEventType.ResourceChanged,SourceActorKey="hunter",
        TargetActorKey="hunter",ResourceKey="mana",Amount=-1m
    };
    private sealed class Harness
    {
        public SimulationContext Context {get;}=new(new SimulationRunOptions {
            DurationSeconds=45m,Seed=175,CaptureTimeline=true,PrimaryActorKey="hunter"
        });
        public SimulationActorState Owner {get;}=new() {Key="hunter",Name="Hunter"};
        private readonly AuraManager _auras=new();
        public HunterResourcefulnessProcessor Proc {get;}
        public List<CombatEvent> Events {get;}=[];
        public IEnumerable<CombatEvent> Gains=>Events.Where(e=>
            e.Type==CombatEventType.ResourceChanged && e.AbilityKey=="resourcefulness");
        public Harness(int rank=2) {
            Owner.Resources["mana"]=new ResourceState("mana",10000m,0m);
            foreach(var key in new[]{"raptor-strike","aimed-shot","serpent-sting",
                "auto-shot","ranged-auto","melee-auto","offhand-auto",
                "explosive-trap","immolation-trap","lacerating-strikes"})
                Owner.Abilities[key]=new AbilityState(new AbilityDefinition {
                    Key=key,ClassKey="hunter"
                });
            Context.AddActor(Owner);
            Proc=new(Owner,_auras,rank,20m);
        }
        public void Add(CombatEvent e)=>Context.ScheduleEvent(e);
        public void Run() {
            while(Context.TryGetNextEvent(out var e)) {
                if(e!.TimeSeconds>=Context.Options.DurationSeconds) break;
                Events.Add(e); _auras.Process(Context,e); Proc.Process(Context,e);
            }
        }
    }
}
