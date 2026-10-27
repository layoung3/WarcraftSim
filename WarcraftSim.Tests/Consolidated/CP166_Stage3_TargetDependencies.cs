using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp166Stage3TargetDependenciesTests
{
    [Fact]
    public void MultitargetHitDependentDotsApplyOnlyToLandedTarget()
    {
        var ctx=new SimulationContext(new SimulationRunOptions {
            DurationSeconds=7m,Seed=166351,PrimaryActorKey="caster",CaptureTimeline=true
        });
        var caster=Actor("caster","raid");
        var hit=Actor("enemy-a","enemy");
        var missed=Actor("enemy-b","enemy");
        ctx.AddActor(caster);ctx.AddActor(hit);ctx.AddActor(missed);
        var ability=new AbilityDefinition {
            Key="multiple",Name="MultiTarget",ClassKey="mage",
            Effects=[
                new AbilityEffectDefinition {
                    Key="impact",EffectType=AbilityEffectTypes.DirectDamage,
                    TargetType=AbilityTargetTypes.Enemy,MaxTargets=2,
                    MinimumValue=10m,MaximumValue=10m,CanMiss=true,CanCrit=false
                },
                new AbilityEffectDefinition {
                    Key="dot",EffectType=AbilityEffectTypes.PeriodicDamage,
                    TargetType=AbilityTargetTypes.Enemy,MaxTargets=2,
                    AuraKey="multi-dot",DurationSeconds=6m,TickIntervalSeconds=2m,
                    IncludeExpirationBoundaryTick=true,DependsOnEffectKey="impact",
                    CanMiss=false,CanCrit=false,MinimumValue=20m,MaximumValue=20m
                }
            ]
        };
        caster.AddAbility(ability);
        var auras=new AuraManager();
        var executor=new AbilityExecutor(auras,new TargetRolls(),
            new RulesetDamageMitigationResolver(new CombatRulesetDefinition {
                RulesetKey="stage3-multitarget",Version="1"
            }));
        var run=new SimulationEngine([executor,auras]).Run(ctx,start=>
        {
            Assert.True(executor.TryStartAbility(start,caster.Key,hit.Key,ability.Key).Success);
        });
        var ticks=run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="multiple"&&
            e.EffectDeliveryType==CombatEffectDeliveryType.Periodic).ToArray();
        Assert.Equal(3,ticks.Length);
        Assert.All(ticks,e=>Assert.Equal(hit.Key,e.TargetActorKey));
        Assert.Equal(930m,hit.CurrentHealth);
        Assert.Equal(1000m,missed.CurrentHealth);
    }

    private static SimulationActorState Actor(string key,string team)
    {
        var actor=new SimulationActorState{Key=key,Name=key,TeamKey=team,Level=60};
        actor.InitializeHealth(1000m);
        return actor;
    }

    private sealed class TargetRolls : ICombatRollResolver
    {
        public CombatRollResult Resolve(SimulationContext context,
            SimulationActorState source,SimulationActorState target,
            AbilityDefinition ability,AbilityEffectDefinition effect)
            => target.Key=="enemy-b" ? CombatRollResult.Miss() : CombatRollResult.Hit();
    }
}
