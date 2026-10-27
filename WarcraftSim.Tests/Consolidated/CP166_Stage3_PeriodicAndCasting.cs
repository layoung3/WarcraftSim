using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp166Stage3PeriodicAndCastingTests
{
    [Fact]
    public void StandaloneDot_MissedApplicationCreatesNoAuraOrPeriodicDamage()
    {
        var (ctx,caster,target,executor,auras)=Prepare();
        caster.AddAbility(Dot(canMiss:true));
        var run=Run(ctx,executor,auras,new SimpleCombatRollResolver(hitChancePercent:0m));
        Assert.Empty(target.ActiveAuras);
        Assert.Contains(run.Timeline,e=>e.AbilityKey=="audit-dot" &&
            e.Type==CombatEventType.Damage && e.ResultKey==CombatResultTypes.Miss);
        Assert.DoesNotContain(run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.EffectDeliveryType==CombatEffectDeliveryType.Periodic);
    }

    [Fact]
    public void LandedDot_NeverRerollsApplicationMissButCanCritEachTick()
    {
        var (ctx,caster,target,_,auras)=Prepare();
        caster.AddAbility(Dot(canMiss:true));
        var resolver=new TickMissTrapResolver();
        var executor=Executor(auras,resolver);
        var run=Run(ctx,executor,auras,resolver);
        var ticks=run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="audit-dot" &&
            e.EffectDeliveryType==CombatEffectDeliveryType.Periodic).ToArray();
        Assert.Equal(3,ticks.Length);
        Assert.All(ticks,e=>{
            Assert.Equal(CombatResultTypes.Critical,e.ResultKey);
            Assert.Equal(40m,e.Amount);
            Assert.True(e.IsCritical);
        });
        Assert.Equal(880m,target.CurrentHealth);
        Assert.Equal(1,resolver.ApplicationCalls);
        Assert.Equal(3,resolver.PeriodicCalls);
    }

    [Fact]
    public void PeriodicDamage_DependencyOnMissedDirectHitDoesNotApplyDot()
    {
        var (ctx,caster,target,_,auras)=Prepare();
        var spell=Dot(canMiss:false);
        spell.Effects.Insert(0,new AbilityEffectDefinition{
            Key="direct-hit",EffectType=AbilityEffectTypes.DirectDamage,
            TargetType=AbilityTargetTypes.Enemy,CanMiss=true,CanCrit=false,
            MinimumValue=5m,MaximumValue=5m
        });
        spell.Effects[1].DependsOnEffectKey="direct-hit";
        caster.AddAbility(spell);
        var resolver=new SimpleCombatRollResolver(hitChancePercent:0m);
        var executor=Executor(auras,resolver);
        var run=Run(ctx,executor,auras,resolver);
        Assert.Empty(target.ActiveAuras);
        Assert.DoesNotContain(run.Timeline,e=>
            e.Type==CombatEventType.Damage &&
            e.EffectDeliveryType==CombatEffectDeliveryType.Periodic);
    }

    [Fact]
    public void DuplicateCastCompletion_CannotDoubleApplyDamageOrResourceCost()
    {
        var (ctx,caster,target,_,auras)=Prepare();
        caster.AddResource(new ResourceState("mana",100m,100m));
        var spell=new AbilityDefinition{
            Key="audit-cast",Name="Audit cast",ClassKey="mage",
            CastTimeSeconds=0m,
            ResourceCosts=[new AbilityResourceCost{ResourceKey="mana",Amount=15m}],
            Effects=[new AbilityEffectDefinition{
                Key="damage",EffectType=AbilityEffectTypes.DirectDamage,
                CanMiss=false,CanCrit=false,MinimumValue=10m,MaximumValue=10m
            }]
        };
        caster.AddAbility(spell);
        var executor=Executor(auras,new SimpleCombatRollResolver());
        var run=new SimulationEngine([executor,auras]).Run(ctx,started=>{
            Assert.True(executor.TryStartAbility(started,caster.Key,target.Key,spell.Key).Success);
            var execution=Assert.Single(started.AbilityExecutions.Values);
            started.ScheduleEvent(new CombatEvent{
                TimeSeconds=0m,Type=CombatEventType.AbilityCastCompleted,
                SourceActorKey=caster.Key,TargetActorKey=target.Key,
                AbilityKey=spell.Key,AbilityExecutionId=execution.Id
            });
        });
        Assert.Equal(85m,caster.Resources["mana"].Current);
        Assert.Equal(990m,target.CurrentHealth);
        Assert.Single(run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey==spell.Key);
        Assert.Single(run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey==spell.Key);
    }

    [Fact]
    public void HealingReceivedBonus_AboveOneIsValidAndReconcilesWithEffectiveHeal()
    {
        var (ctx,caster,target,_,auras)=Prepare(targetHealth:900m);
        target.TeamKey=caster.TeamKey;
        var heal=new AbilityDefinition{Key="audit-heal",Name="Audit Heal",ClassKey="priest",
            Effects=[new AbilityEffectDefinition{
                Key="healing",EffectType=AbilityEffectTypes.DirectHealing,
                TargetType=AbilityTargetTypes.Friendly,
                CanMiss=false,CanCrit=false,MinimumValue=50m,MaximumValue=50m
            }]
        };
        caster.AddAbility(heal);
        var executor=Executor(auras,new SimpleCombatRollResolver());
        var run=new SimulationEngine([executor,auras]).Run(ctx,start=>{
            auras.ApplyAura(start,target,new AuraDefinition{
                Key="increased-healing",Name="Increased Healing",
                DurationSeconds=10m,HealingReceivedMultiplier=1.2m
            },caster.Key,"test-buff","buff");
            Assert.True(executor.TryStartAbility(start,caster.Key,target.Key,heal.Key).Success);
        });
        var eventHeal=Assert.Single(run.Timeline,e=>e.Type==CombatEventType.Healing &&
            e.AbilityKey==heal.Key);
        Assert.Equal(60m,eventHeal.RawAmount);
        Assert.Equal(60m,eventHeal.Amount);
        Assert.Equal(0m,eventHeal.OverhealingAmount);
        Assert.Equal(960m,target.CurrentHealth);
    }

    private static AbilityDefinition Dot(bool canMiss)=>
        new(){Key="audit-dot",Name="Audit DoT",ClassKey="warlock",
            Effects=[new AbilityEffectDefinition{
                Key="dot-tick",EffectType=AbilityEffectTypes.PeriodicDamage,
                TargetType=AbilityTargetTypes.Enemy,
                CanMiss=canMiss,CanCrit=true,MinimumValue=20m,MaximumValue=20m,
                AuraKey="audit-dot-aura",DurationSeconds=6m,
                TickIntervalSeconds=2m,IncludeExpirationBoundaryTick=true
            }]};

    private static (SimulationContext,SimulationActorState,SimulationActorState,
        AbilityExecutor,AuraManager) Prepare(decimal targetHealth=1000m)
    {
        var ctx=new SimulationContext(new SimulationRunOptions{
            Seed=166301,DurationSeconds=7m,CaptureTimeline=true,
            PrimaryActorKey="caster"
        });
        var caster=new SimulationActorState{Key="caster",Name="Caster",TeamKey="raid",Level=60};
        caster.InitializeHealth(1000m);
        var target=new SimulationActorState{Key="target",Name="Target",TeamKey="enemy",Level=63};
        target.InitializeHealth(1000m,targetHealth);
        ctx.AddActor(caster);ctx.AddActor(target);
        var auras=new AuraManager();
        return (ctx,caster,target,Executor(auras,new SimpleCombatRollResolver()),auras);
    }

    private static AbilityExecutor Executor(AuraManager auras,ICombatRollResolver rolls)=>
        new(auras,rolls,new RulesetDamageMitigationResolver(
            new CombatRulesetDefinition{RulesetKey="cp166-stage3",Version="1"}));

    private static SimulationRunResult Run(SimulationContext ctx,
        AbilityExecutor ignored,AuraManager auras,ICombatRollResolver rolls)
    {
        var caster=ctx.GetActor("caster")!;
        var executor=Executor(auras,rolls);
        return new SimulationEngine([executor,auras]).Run(ctx,start=>{
            Assert.True(executor.TryStartAbility(start,caster.Key,"target","audit-dot").Success);
        });
    }

    private sealed class TickMissTrapResolver : ICombatRollResolver
    {
        public int ApplicationCalls {get;private set;}
        public int PeriodicCalls {get;private set;}

        public CombatRollResult Resolve(SimulationContext context,
            SimulationActorState source,SimulationActorState target,
            AbilityDefinition ability,AbilityEffectDefinition effect)
            => Resolve(context,source,target,ability,effect,CombatEffectDeliveryType.Direct);

        public CombatRollResult Resolve(SimulationContext context,
            SimulationActorState source,SimulationActorState target,
            AbilityDefinition ability,AbilityEffectDefinition effect,
            CombatEffectDeliveryType delivery)
        {
            if(delivery==CombatEffectDeliveryType.Periodic)
            {
                PeriodicCalls++;
                return effect.CanMiss ? CombatRollResult.Miss() :
                    effect.CanCrit ? CombatRollResult.Critical(2m) : CombatRollResult.Hit();
            }
            ApplicationCalls++;
            return CombatRollResult.Hit();
        }
    }
}
