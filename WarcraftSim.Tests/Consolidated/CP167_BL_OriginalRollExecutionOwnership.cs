using System;
using System.Collections.Generic;
using System.Linq;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>Shared regression: original execution ID is visible during EACH
/// resolver call even when multiple casters resolve simultaneous impacts.</summary>
public sealed class Cp167BlOriginalRollExecutionOwnership
{
    [Fact]
    public void TwoSameTimestampCasterHitsKeepDistinctResolverExecutionIDs()
    {
        var ctx=new SimulationContext(new SimulationRunOptions {
            DurationSeconds=3m,Seed=167212,PrimaryActorKey="mage",CaptureTimeline=true
        });
        SimulationActorState Actor(string key,string team)
        {
            var a=new SimulationActorState {Key=key,Name=key,TeamKey=team,Level=60};
            a.InitializeHealth(1000m);
            return a;
        }
        var mage=Actor("mage","raid");
        var priest=Actor("priest","raid");
        var target=Actor("target","enemy");
        ctx.AddActor(mage);ctx.AddActor(priest);ctx.AddActor(target);
        AbilityDefinition Spell(string key,string classKey)=>new() {
            Key=key,Name=key,ClassKey=classKey,CastTimeSeconds=0m,
            IsOffGlobalCooldown=true,GlobalCooldownSeconds=0m,
            Effects=[new AbilityEffectDefinition {
                Key=key+"-impact",EffectType=AbilityEffectTypes.DirectDamage,
                TargetType=AbilityTargetTypes.Enemy,
                ResolutionType=CombatResolutionTypes.AlwaysHits,
                MinimumValue=30m,MaximumValue=30m,CanMiss=false,CanCrit=false
            }]
        };
        mage.AddAbility(Spell("mage-hit","mage"));
        priest.AddAbility(Spell("priest-hit","priest"));
        var rolls=new CapturingRolls();
        var auras=new AuraManager();
        var executor=new AbilityExecutor(auras,rolls,
            new RulesetDamageMitigationResolver(new CombatRulesetDefinition{
                RulesetKey="cp167-roll-owner",Version="1"
            }));
        var run=new SimulationEngine([executor,auras]).Run(ctx,start=>{
            Assert.True(executor.TryStartAbility(start,mage.Key,target.Key,"mage-hit").Success);
            Assert.True(executor.TryStartAbility(start,priest.Key,target.Key,"priest-hit").Success);
        });
        var hits=run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.Amount is >0m).ToArray();
        Assert.Equal(2,hits.Length);
        Assert.Equal(2,rolls.Seen.Count);
        Assert.All(hits,e=>{
            Assert.True(e.AbilityExecutionId.HasValue);
            Assert.Contains(rolls.Seen,x=>x.Ability==e.AbilityKey &&
                x.Execution==e.AbilityExecutionId);
        });
        Assert.NotEqual(hits[0].AbilityExecutionId,hits[1].AbilityExecutionId);
        Assert.Null(ctx.CurrentResolvingAbilityExecutionId);
        Assert.Equal(940m,target.CurrentHealth);
    }

    private sealed class CapturingRolls : ICombatRollResolver
    {
        public List<(string Ability,Guid? Execution)> Seen {get;}=[];
        public CombatRollResult Resolve(SimulationContext context,
            SimulationActorState source,SimulationActorState target,
            AbilityDefinition ability,AbilityEffectDefinition effect)
        {
            Seen.Add((ability.Key,context.CurrentResolvingAbilityExecutionId));
            return CombatRollResult.Hit();
        }
    }
}
