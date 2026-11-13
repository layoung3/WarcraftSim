using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Api.Simulation;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175E2AdvancedPredicates
{
    private static (SimulationContext Context, SimulationActorState Owner,
        SimulationActorState Target) Setup()
    {
        var ctx=new SimulationContext(new SimulationRunOptions {
            Seed=175280,DurationSeconds=60m,PrimaryActorKey="owner",
            CaptureTimeline=true
        });
        var owner=new SimulationActorState {
            Key="owner",Name="Owner",Level=60,TeamKey="raid",MaximumHealth=1000m
        };
        owner.InitializeHealth(1000m);
        owner.AddResource(new ResourceState("mana",200m,100m));
        var target=new SimulationActorState {
            Key="target",Name="Boss",Level=63,TeamKey="enemy",MaximumHealth=100000m
        };
        target.InitializeHealth(100000m);
        ctx.AddActor(owner);ctx.AddActor(target);
        return(ctx,owner,target);
    }
    private static RotationConditionDefinition Condition(string type,decimal threshold,
        string? key=null,string comparison=RotationComparisonOperators.LessThanOrEqual,
        bool own=false)=>new(){
            ConditionType=type,Value=threshold,Key=key,
            ComparisonOperator=comparison,RequireSourceOwnership=own
        };
    [Fact]
    public void TimeRemainingUsesFightDurationRatherThanActualBossHealth()
    {
        var (ctx,owner,target)=Setup();
        var lastTen=Condition(RotationConditionTypes.EncounterTimeRemainingSeconds,10m);
        Assert.False(RotationConditionEvaluator.AreSatisfied(ctx,owner,target,[lastTen]));
        Assert.Equal(50m,RotationConditionEvaluator.GetNextKnownEvaluationTime(
            ctx,owner,target,[lastTen]));
        Assert.True(RotationConditionEvaluator.AreSatisfied(ctx,owner,target,[
            Condition(RotationConditionTypes.EncounterTimeRemainingSeconds,60m)
        ]));
    }
    [Fact]
    public void ResourceDeficitAndAbsentAuraReadActualEngineState()
    {
        var (ctx,owner,target)=Setup();
        Assert.True(RotationConditionEvaluator.AreSatisfied(ctx,owner,target,[
            Condition(RotationConditionTypes.SourceResourceDeficit,100m,"mana",
                RotationComparisonOperators.GreaterThanOrEqual)
        ]));
        Assert.False(RotationConditionEvaluator.AreSatisfied(ctx,owner,target,[
            Condition(RotationConditionTypes.SourceResourceDeficit,101m,"mana",
                RotationComparisonOperators.GreaterThanOrEqual)
        ]));
        Assert.True(RotationConditionEvaluator.AreSatisfied(ctx,owner,target,[
            Condition(RotationConditionTypes.TargetAuraRemainingSeconds,3m,"immolate")
        ]));
        Assert.True(RotationConditionEvaluator.AreSatisfied(ctx,owner,target,[
            Condition(RotationConditionTypes.TargetAuraStackCount,0m,"immolate",
                RotationComparisonOperators.Equal)
        ]));
    }
    [Fact]
    public void OwnedTargetAuraRefreshEvaluatesActualDurationAndStacks()
    {
        var (ctx,owner,target)=Setup();
        var auras=new AuraManager();
        auras.ApplyAura(ctx,target,new AuraDefinition {
            Key="immolate",Name="Immolate",DurationSeconds=15m
        },"owner","immolate","impact");
        Assert.True(RotationConditionEvaluator.AreSatisfied(ctx,owner,target,[
            Condition(RotationConditionTypes.TargetAuraRemainingSeconds,15m,"immolate",
                RotationComparisonOperators.Equal,true)
        ]));
        var refresh=Condition(RotationConditionTypes.TargetAuraRemainingSeconds,3m,
            "immolate",RotationComparisonOperators.LessThanOrEqual,true);
        Assert.False(RotationConditionEvaluator.AreSatisfied(ctx,owner,target,[refresh]));
        Assert.Equal(12m,RotationConditionEvaluator.GetNextKnownEvaluationTime(
            ctx,owner,target,[refresh]));
        Assert.True(RotationConditionEvaluator.AreSatisfied(ctx,owner,target,[
            Condition(RotationConditionTypes.TargetAuraStackCount,1m,"immolate",
                RotationComparisonOperators.Equal,true)
        ]));
    }
}
