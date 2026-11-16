using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175E2OrTargetPriority
{
    private static RotationConditionDefinition Hp(decimal max)=>new(){
        ConditionType=RotationConditionTypes.TargetHealthPercent,
        ComparisonOperator=RotationComparisonOperators.LessThanOrEqual,Value=max
    };
    private static RotationConditionDefinition Remaining(decimal max)=>new(){
        ConditionType=RotationConditionTypes.EncounterTimeRemainingSeconds,
        ComparisonOperator=RotationComparisonOperators.LessThanOrEqual,Value=max
    };

    [Fact]
    public void OrGroupsRetainMandatoryAndConditionsAndUseEngineTime()
    {
        var rotation=new RotationProfile{Name="OR conditions",ClassKey="mage",RulesetKey="wow-forever"};
        rotation.Entries.Add(new RotationEntry{Priority=1,AbilityKey="fireball"});
        SoloDpsPriorityConditionBridge.Apply(rotation,[
            new(){
                AbilityKey="fireball",
                Conditions=[new(){
                    ConditionType=RotationConditionTypes.SourceResourcePercent,
                    Key="mana",Value=20m,
                    ComparisonOperator=RotationComparisonOperators.GreaterThanOrEqual
                }],
                AnyConditionGroups=[[Hp(20m)],[Remaining(10m)]]
            }
        ],["fireball"],["mage","target"]);
        var ctx=new SimulationContext(new SimulationRunOptions{
            Seed=175900,DurationSeconds=30m,PrimaryActorKey="mage"
        });
        var mage=new SimulationActorState{
            Key="mage",Name="Mage",TeamKey="raid",MaximumHealth=1000m
        };mage.InitializeHealth(1000m);
        mage.AddResource(new ResourceState("mana",100m,80m));
        var boss=new SimulationActorState{
            Key="target",Name="Boss",TeamKey="enemy",MaximumHealth=1000000m
        };boss.InitializeHealth(1000000m);
        ctx.AddActor(mage);ctx.AddActor(boss);
        ctx.SetScriptedTargetHealth("target",new ScriptedTargetHealthTimeline([new(0m,100m)]));
        var entry=rotation.Entries.Single();
        Assert.False(RotationConditionEvaluator.AreSatisfiedForEntry(ctx,mage,boss,entry));
        Assert.Equal(20m,RotationConditionEvaluator.GetNextKnownEvaluationTimeForEntry(ctx,mage,boss,entry));
        Assert.Equal(2,entry.AlternativeConditionGroups.Count);
        mage.Resources["mana"].Spend(75m);
        Assert.False(RotationConditionEvaluator.AreSatisfiedForEntry(ctx,mage,boss,entry));
    }

    [Fact]
    public void ExplicitTargetMustBelongToCurrentCombatActorSet()
    {
        RotationProfile NewProfile(){
            var p=new RotationProfile{Name="fixed add",ClassKey="warlock",RulesetKey="wow-forever"};
            p.Entries.Add(new RotationEntry{Priority=1,AbilityKey="immolate"});
            return p;
        }
        var condition=new SoloDpsPriorityConditionRule{
            AbilityKey="immolate",TargetActorKey="target-1",
            Conditions=[new(){
                ConditionType=RotationConditionTypes.TargetAuraMissing,Key="immolate",
                RequireSourceOwnership=true
            }]
        };
        var valid=NewProfile();
        SoloDpsPriorityConditionBridge.Apply(valid,[condition],["immolate"],
            ["warlock","target","target-1"]);
        Assert.Equal(RotationTargetSelectionModes.Fixed,valid.Entries[0].Target.Mode);
        Assert.Equal("target-1",valid.Entries[0].Target.ActorKey);
        Assert.Throws<ArgumentException>(()=>SoloDpsPriorityConditionBridge.Apply(
            NewProfile(),[condition],["immolate"],["warlock","target"]));
    }

    [Fact]
    public void InvalidOrGroupsRejectInsteadOfBecomingUnconditional()
    {
        var p=new RotationProfile{Name="invalid",ClassKey="mage",RulesetKey="wow-forever"};
        p.Entries.Add(new RotationEntry{Priority=1,AbilityKey="fireball"});
        var invalid=new SoloDpsPriorityConditionRule{
            AbilityKey="fireball",AnyConditionGroups=[[
                new(){ConditionType="not-a-real-condition"}
            ]]
        };
        Assert.Throws<ArgumentException>(()=>SoloDpsPriorityConditionBridge.Apply(
            p,[invalid],["fireball"],["mage","target"]));
        Assert.Empty(p.Entries[0].AlternativeConditionGroups);
    }
}
