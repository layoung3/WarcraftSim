using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp168AfflictionAmplifyOwnership
{
    [Fact]
    public void AgonyAmplificationIsTargetAndOwnerBoundAndEndsWithBane()
    {
        var ctx=new SimulationContext(new SimulationRunOptions{DurationSeconds=90m});
        var owner=new SimulationActorState{Key="warlock",Name="Warlock",TeamKey="player"};
        var other=new SimulationActorState{Key="other-warlock",Name="Other",TeamKey="player"};
        ctx.AddActor(owner);
        ctx.AddActor(other);
        var targets=Enumerable.Range(1,3).Select(i=>
            new SimulationActorState{Key=$"enemy-{i}",Name=$"Enemy {i}",TeamKey="enemy"}).ToArray();
        foreach(var target in targets)ctx.AddActor(target);
        var auras=new AuraManager();
        var processor=new WarlockAmplifyCurseProcessor(auras);
        AuraDefinition agony=new(){Key="bane-of-agony",Name="Bane of Agony",DurationSeconds=24m};
        AuraDefinition amplify=new(){Key="warlock-amplify-curse",Name="Amplify Curse",DurationSeconds=30m};

        auras.ApplyAura(ctx,owner,amplify,owner.Key,"amplify-curse",null);
        auras.ApplyAura(ctx,targets[0],agony,owner.Key,"bane-of-agony",null);
        processor.Process(ctx,new CombatEvent{
            Type=CombatEventType.AuraApplied,SourceActorKey=owner.Key,
            TargetActorKey=targets[0].Key,AbilityKey="bane-of-agony"});
        Assert.Equal(1,processor.AmplifiedBanes);
        Assert.Contains(targets[0].ActiveAuras,a=>a.Definition.Key=="warlock-amplified-agony");
        Assert.DoesNotContain(owner.ActiveAuras,a=>a.Definition.Key=="warlock-amplify-curse");

        // A second caster's independent Bane and another enemy cannot share
        // the original Warlock's amplified application.
        auras.ApplyAura(ctx,targets[0],agony,other.Key,"bane-of-agony",null);
        auras.ApplyAura(ctx,targets[1],agony,owner.Key,"bane-of-agony",null);
        processor.Process(ctx,new CombatEvent{
            Type=CombatEventType.AuraApplied,SourceActorKey=owner.Key,
            TargetActorKey=targets[1].Key,AbilityKey="bane-of-agony"});
        Assert.DoesNotContain(targets[1].ActiveAuras,a=>a.Definition.Key=="warlock-amplified-agony");

        auras.RemoveAuras(ctx,targets[0],"bane-of-agony",owner.Key,"replaced by Havoc");
        processor.Process(ctx,new CombatEvent{
            Type=CombatEventType.AuraRemoved,SourceActorKey=owner.Key,
            TargetActorKey=targets[0].Key,AbilityKey="bane-of-agony"});
        Assert.DoesNotContain(targets[0].ActiveAuras,a=>a.Definition.Key=="warlock-amplified-agony");
        Assert.Contains(targets[0].ActiveAuras,a=>
            a.Definition.Key=="bane-of-agony" && a.SourceActorKey==other.Key);
        Assert.Contains(targets[1].ActiveAuras,a=>
            a.Definition.Key=="bane-of-agony" && a.SourceActorKey==owner.Key);
    }

    [Fact]
    public void StaleRemovalCannotClearAmplificationOfRefreshedAgony()
    {
        var ctx=new SimulationContext(new SimulationRunOptions{DurationSeconds=90m});
        var owner=new SimulationActorState{Key="warlock",Name="Warlock",TeamKey="player"};
        var target=new SimulationActorState{Key="enemy",Name="Enemy",TeamKey="enemy"};
        ctx.AddActor(owner);
        ctx.AddActor(target);
        var auras=new AuraManager();
        var processor=new WarlockAmplifyCurseProcessor(auras);
        auras.ApplyAura(ctx,target,new AuraDefinition{
            Key="bane-of-agony",Name="Agony",DurationSeconds=24m
        },owner.Key,"bane-of-agony",null);
        auras.ApplyAura(ctx,target,new AuraDefinition{
            Key="warlock-amplified-agony",Name="Amplified",DurationSeconds=24m
        },owner.Key,"amplify-curse",null);
        processor.Process(ctx,new CombatEvent{
            Type=CombatEventType.AuraRemoved,SourceActorKey=owner.Key,
            TargetActorKey=target.Key,AbilityKey="bane-of-agony"});
        Assert.Contains(target.ActiveAuras,a=>a.Definition.Key=="warlock-amplified-agony");
    }
}
