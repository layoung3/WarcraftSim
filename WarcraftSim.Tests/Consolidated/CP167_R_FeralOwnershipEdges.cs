using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167RFeralOwnershipEdges
{
    [Fact]
    public void FinisherOnSecondTargetCannotConsumeFirstTargetsSeededPoints()
    {
        var req = new FeralDruidSimRequest {
            Role="cat", DurationSeconds=5m, CaptureTimeline=true,
            AdditionalTargets=1, TargetBehind=false,
            TargetTransitions=[new() { AtSeconds=0m, TargetKey="target-1" }],
            InitialComboPointsByTarget=new(StringComparer.OrdinalIgnoreCase) {
                ["target"]=5, ["target-1"]=5
            }
        };
        req.Tuning.MeleeHitPercent=100m;
        req.Tuning.MeleeCritPercent=0m;
        var sim=new FeralDruidSimRunner().Run(req);
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey=="rip" && e.TargetActorKey=="target-1");
        Assert.Equal(5,sim.ComboPointsByTarget.GetValueOrDefault("target"));
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="rip" && e.TargetActorKey=="target-1");
        Assert.DoesNotContain(sim.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="rip" && e.TargetActorKey=="target");
    }

    [Fact]
    public void MissedFinisherOnSecondTargetNeverTransfersPointsToFirst()
    {
        var req = new FeralDruidSimRequest {
            Role="cat", DurationSeconds=5m, CaptureTimeline=true,
            AdditionalTargets=1, TargetBehind=false,
            TargetTransitions=[new() {AtSeconds=0m,TargetKey="target-1"}],
            InitialComboPointsByTarget=new(StringComparer.OrdinalIgnoreCase) {
                ["target"]=2,["target-1"]=5
            }
        };
        req.Tuning.MeleeHitPercent=0m;
        req.Tuning.MeleeCritPercent=0m;
        var sim=new FeralDruidSimRunner().Run(req);
        Assert.Equal(2,sim.ComboPointsByTarget.GetValueOrDefault("target"));
        Assert.Equal(5,sim.ComboPointsByTarget.GetValueOrDefault("target-1"));
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="rip-miss-refund" && e.Amount>0m);
    }
}
