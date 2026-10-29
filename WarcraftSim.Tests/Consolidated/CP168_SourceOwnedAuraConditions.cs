using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp168SourceOwnedAuraConditions
{
    [Fact]
    public void TargetAuraCheckCanIgnoreAnotherCasterWhileLegacyCheckStillSeesAura()
    {
        var context=new SimulationContext(new SimulationRunOptions{DurationSeconds=30m});
        var caster=new SimulationActorState{Key="warlock",TeamKey="raid"};
        var other=new SimulationActorState{Key="another-warlock",TeamKey="raid"};
        var target=new SimulationActorState{Key="target",TeamKey="enemy"};
        context.AddActor(caster);
        context.AddActor(other);
        context.AddActor(target);
        var auras=new AuraManager();
        auras.ApplyAura(context,target,new AuraDefinition{
            Key="corruption",Name="Corruption",DurationSeconds=18m
        },other.Key,"corruption",null);
        var owned=new RotationConditionDefinition{
            ConditionType=RotationConditionTypes.TargetAuraMissing,
            Key="corruption",RequireSourceOwnership=true
        };
        var legacy=new RotationConditionDefinition{
            ConditionType=RotationConditionTypes.TargetAuraMissing,Key="corruption"
        };
        Assert.True(RotationConditionEvaluator.AreSatisfied(context,caster,target,[owned]));
        Assert.False(RotationConditionEvaluator.AreSatisfied(context,caster,target,[legacy]));
        Assert.Null(RotationConditionEvaluator.GetNextKnownEvaluationTime(
            context,caster,target,[owned]));
        Assert.Equal(18m,RotationConditionEvaluator.GetNextKnownEvaluationTime(
            context,caster,target,[legacy]));
        auras.ApplyAura(context,target,new AuraDefinition{
            Key="corruption",Name="Corruption",DurationSeconds=18m
        },caster.Key,"corruption",null);
        Assert.False(RotationConditionEvaluator.AreSatisfied(context,caster,target,[owned]));
        Assert.Equal(18m,RotationConditionEvaluator.GetNextKnownEvaluationTime(
            context,caster,target,[owned]));
        auras.RemoveAuras(context,target,"corruption",caster.Key);
        Assert.True(RotationConditionEvaluator.AreSatisfied(context,caster,target,[owned]));
        Assert.False(RotationConditionEvaluator.AreSatisfied(context,caster,target,[legacy]));
    }
}
