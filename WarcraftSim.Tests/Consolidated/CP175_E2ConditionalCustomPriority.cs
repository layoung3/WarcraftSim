using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175E2ConditionalCustomPriority
{
    [Fact]
    public void MageActualCastingChangesWithScriptedTargetHpPredicate()
    {
        MageSimRequest Build(decimal hp)
        {
            var request=MageSimRequest.CreatePreset("fire");
            request.DurationSeconds=14m;
            request.Seed=175620;
            request.CaptureTimeline=true;
            request.CustomPriority=["fireball"];
            request.SoloEncounter=new(){Enabled=true,StartingHealthPercent=hp};
            request.PriorityConditions=[new(){
                AbilityKey="fireball",
                Conditions=[new(){
                    ConditionType=RotationConditionTypes.TargetHealthPercent,
                    ComparisonOperator=RotationComparisonOperators.LessThanOrEqual,
                    Value=20m
                }]
            }];
            return request;
        }
        var allowed=new MageSimRunner().Run(Build(20m));
        var rejected=new MageSimRunner().Run(Build(100m));
        int Count(MageSimResponse result)=>result.Run.Timeline.Count(
            e=>e.Type==CombatEventType.AbilityCastCompleted&&e.AbilityKey=="fireball");
        Assert.True(Count(allowed)>0);
        Assert.Equal(0,Count(rejected));
        Assert.True(allowed.DamageDone>rejected.DamageDone);
    }

    [Fact]
    public void InvalidSavedConditionsCannotSilentlyTurnIntoExecutableActions()
    {
        var profile=new RotationProfile {
            Name="test",ClassKey="mage",RulesetKey="wow-forever"
        };
        profile.Entries.Add(new RotationEntry {Priority=1,AbilityKey="fireball"});
        Assert.Throws<ArgumentException>(()=>SoloDpsPriorityConditionBridge.Apply(
            profile,[new(){AbilityKey="fireball",Conditions=[
                new(){ConditionType="nonexistent-client-predicate",Value=20m}
            ]}],["fireball"]));
        Assert.Throws<ArgumentException>(()=>SoloDpsPriorityConditionBridge.Apply(
            profile,[new(){AbilityKey="frostbolt",Conditions=[
                new(){ConditionType=RotationConditionTypes.TargetHealthPercent,Value=20m}
            ]}],["fireball"]));
        Assert.Empty(profile.Entries[0].Conditions);
    }

    [Fact]
    public void MultipleSupportedAndConditionsUseExistingRotationProfile()
    {
        var profile=new RotationProfile{Name="test",ClassKey="warlock",RulesetKey="wow-forever"};
        profile.Entries.Add(new RotationEntry {Priority=1,AbilityKey="immolate"});
        var conditions=new List<RotationConditionDefinition>{
            new(){ConditionType=RotationConditionTypes.TargetAuraMissing,
                Key="immolate",RequireSourceOwnership=true},
            new(){ConditionType=RotationConditionTypes.SourceResourcePercent,
                Key="mana",ComparisonOperator=RotationComparisonOperators.GreaterThanOrEqual,Value=30m}
        };
        SoloDpsPriorityConditionBridge.Apply(profile,[
            new(){AbilityKey="immolate",Conditions=conditions}
        ],["immolate"]);
        Assert.Equal(2,profile.Entries.Single().Conditions.Count);
        Assert.True(profile.Entries.Single().Conditions[0].RequireSourceOwnership);
    }
    [Fact]
    public void ExplicitExpiryWindowReplacesOnlyItsConflictingDefaultMissingAuraGuard()
    {
        var profile=new RotationProfile {Name="maintain",ClassKey="warlock",
            RulesetKey="wow-forever"};
        var entry=new RotationEntry {Priority=1,AbilityKey="immolate"};
        entry.Conditions.Add(new RotationConditionDefinition {
            ConditionType=RotationConditionTypes.TargetAuraMissing,Key="immolate",
            RequireSourceOwnership=true
        });
        entry.Conditions.Add(new RotationConditionDefinition {
            ConditionType=RotationConditionTypes.SourceResourcePercent,
            Key="mana",ComparisonOperator=RotationComparisonOperators.GreaterThanOrEqual,
            Value=20m
        });
        profile.Entries.Add(entry);
        SoloDpsPriorityConditionBridge.Apply(profile,[new(){
            AbilityKey="immolate",Conditions=[new(){
                ConditionType=RotationConditionTypes.TargetAuraRemainingSeconds,
                Key="immolate",ComparisonOperator=RotationComparisonOperators.LessThanOrEqual,
                Value=3m,RequireSourceOwnership=true
            }]
        }],["immolate"]);
        Assert.DoesNotContain(entry.Conditions,x=>
            x.ConditionType==RotationConditionTypes.TargetAuraMissing&&x.Key=="immolate");
        Assert.Contains(entry.Conditions,x=>
            x.ConditionType==RotationConditionTypes.SourceResourcePercent&&x.Value==20m);
        Assert.Contains(entry.Conditions,x=>
            x.ConditionType==RotationConditionTypes.TargetAuraRemainingSeconds&&x.Value==3m);
    }

    [Fact]
    public void WarlockExpiryWindowCanRecastBeforeAuraExpiration()
    {
        var request=WarlockSimRequest.CreatePreset("destruction");
        request.Seed=175733;request.DurationSeconds=35m;
        request.CaptureTimeline=true;request.EnablePet=false;
        request.CustomPriority=["immolate","shadow-bolt"];
        request.PriorityConditions=[new(){
            AbilityKey="immolate",Conditions=[new(){
                ConditionType=RotationConditionTypes.TargetAuraRemainingSeconds,
                Key="immolate",ComparisonOperator=RotationComparisonOperators.LessThanOrEqual,
                Value=5m,RequireSourceOwnership=true
            }]
        }];
        var run=new WarlockSimRunner().Run(request);
        var castTimes=run.Run.Timeline.Where(x=>x.AbilityKey=="immolate"&&
            x.Type==CombatEventType.AbilityCastCompleted)
            .Select(x=>x.TimeSeconds).ToArray();
        Assert.True(castTimes.Length>=2,
            "An eligible expiration window must permit a real second Immolate cast.");
        Assert.True(castTimes[1]-castTimes[0]<15m,
            "A five-second refresh window should allow a cast before a 15-second aura expires.");
    }

}
