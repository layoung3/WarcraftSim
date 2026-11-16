using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175PaladinPriority
{
    private sealed class NoMitigation:IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext context,SimulationActorState source,
            SimulationActorState target,AbilityDefinition ability,AbilityEffectDefinition effect,decimal amount)=>
            DamageMitigationResult.Unmitigated(amount);
    }
    private static SimulationRunResult Run(bool optIn,decimal delay=0m,decimal cast=0m,decimal mana=100m)
    {
        var context=new SimulationContext(new(){Seed=175113,DurationSeconds=4m,PrimaryActorKey="paladin",CaptureTimeline=true});
        var actor=new SimulationActorState {Key="paladin",Name="Paladin",TeamKey="raid",Level=60};
        actor.ConfigureActionTiming(initialActionDelaySeconds:0m,inputDelaySeconds:delay);
        actor.InitializeHealth(10000m);actor.AddResource(new ResourceState("mana",100m,mana));context.AddActor(actor);
        var target=new SimulationActorState {Key="target",Name="Target",TeamKey="enemy",Level=60};
        target.InitializeHealth(10000m);context.AddActor(target);
        AbilityDefinition Action(string key,bool off)=>new() {
            Key=key,Name=key,ClassKey="paladin",GlobalCooldownSeconds=off?0m:1.5m,
            CastTimeSeconds=off?0m:cast,CooldownSeconds=off?2m:5m,IsOffGlobalCooldown=off,
            ResourceCosts=off?[new(){ResourceKey="mana",Amount=10m}]:[],
            Effects=[new(){Key=key+"-impact",EffectType=AbilityEffectTypes.DirectDamage,
                TargetType=AbilityTargetTypes.Enemy,SchoolKey="holy",ResolutionType=CombatResolutionTypes.AlwaysHits,
                CanMiss=false,CanCrit=false,MinimumValue=1m,MaximumValue=1m}]};
        actor.AddAbility(Action("normal",false));actor.AddAbility(Action("off-global",true));
        var auras=new AuraManager();var executor=new AbilityExecutor(auras,new SimpleCombatRollResolver(),new NoMitigation());
        var rotation=new RotationProfile {Name="Explicit input clocks",ClassKey="paladin",RulesetKey="wow-forever",
            Entries=[new(){Priority=1,AbilityKey="normal"},new(){Priority=2,AbilityKey="off-global"}]};
        return new SimulationEngine([executor,auras,new PriorityRotationExecutor(rotation,"paladin","target",executor,
            evaluateOffGlobalCooldownDuringInputLock:optIn)]).Run(context);
    }

    [Theory]
    [InlineData(false,0,0,1.5)]
    [InlineData(true,0,0,0)]
    [InlineData(true,.1,0,.1)]
    [InlineData(true,0,2,2)]
    public void OptInUsesTheExistingOffGlobalClockWithoutBypassingDelayOrActiveCasts(
        bool optIn,double delay,double cast,double firstTime)
    {
        var result=Run(optIn,(decimal)delay,(decimal)cast);
        var off=result.Timeline.Where(x=>x.Type==CombatEventType.AbilityCastStarted&&x.AbilityKey=="off-global").ToArray();
        Assert.NotEmpty(off);Assert.Equal((decimal)firstTime,off[0].TimeSeconds);
        Assert.All(off.Zip(off.Skip(1)),pair=>Assert.True(pair.Second.TimeSeconds-pair.First.TimeSeconds>=2m));
        Assert.Single(result.Timeline,x=>x.Type==CombatEventType.AbilityCastStarted&&x.AbilityKey=="normal");
    }

    [Fact]
    public void OffGlobalOptionCannotCreateManaOrSkipTheActualResourceGate()
    {
        var result=Run(true,mana:0m);
        Assert.DoesNotContain(result.Timeline,x=>x.Type==CombatEventType.AbilityCastStarted&&x.AbilityKey=="off-global");
        Assert.Single(result.Timeline,x=>x.Type==CombatEventType.AbilityCastStarted&&x.AbilityKey=="normal");
    }
}
