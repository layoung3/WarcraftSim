using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175E1ScriptedBossHealth
{
    private static SoloDpsEncounterScript Example()=>new(){
        Enabled=true,StartingHealthPercent=100m,
        BossHealthMilestones=[
            new(){TimeSeconds=60m,HealthPercent=75m},
            new(){TimeSeconds=120m,HealthPercent=35m},
            new(){TimeSeconds=150m,HealthPercent=20m},
            new(){TimeSeconds=180m,HealthPercent=0m}
        ]
    };

    [Fact]
    public void ScriptUsesStepwiseHealthAndAllowsZeroOnlyAtFightBoundary()
    {
        var timeline=Example().Build(180m);
        Assert.Equal(100m,timeline.GetHealthPercentAt(59.9m));
        Assert.Equal(75m,timeline.GetHealthPercentAt(60m));
        Assert.Equal(35m,timeline.GetHealthPercentAt(120m));
        Assert.Equal(20m,timeline.GetHealthPercentAt(179m));
        Assert.Equal(4,timeline.Checkpoints.Count);
        Assert.Equal(180m,Example().BossHealthMilestones.Last().TimeSeconds);
    }

    [Fact]
    public void InvalidAndNonmonotoneHealthScriptsAreRejected()
    {
        var script=Example();
        script.BossHealthMilestones[1].HealthPercent=90m;
        Assert.Throws<ArgumentException>(()=>script.Build(180m));
        script=Example();
        script.BossHealthMilestones[3].TimeSeconds=175m;
        Assert.Throws<ArgumentException>(()=>script.Build(180m));
        script=Example();
        script.StartingHealthPercent=0m;
        Assert.Throws<ArgumentException>(()=>script.Build(180m));
    }

    [Fact]
    public void WarriorExecuteUsesScriptedHealthRatherThanArtificialActorHp()
    {
        var request=new WarriorSimRequest {
            Preset="fury",TalentMode="tree",Iterations=1,Seed=175501,
            DurationSeconds=24m,StartingRage=100m,
            ExecutePhaseDurationSeconds=0m,
            UseBloodrage=false,UseExecute=true,UseHeroicStrike=false,
            CaptureRepresentativeTimeline=true,TargetLevel=60,
            TargetDodgePercent=0m,TargetParryPercent=0m,
            SoloEncounter=new(){
                Enabled=true,StartingHealthPercent=100m,
                BossHealthMilestones=[new(){TimeSeconds=8m,HealthPercent=20m}]
            }
        };
        var result=new ForeverWarriorSimRunner().Run(request);
        var casts=result.Batch.RepresentativeRun.Timeline
            .Where(e=>e.Type==CombatEventType.AbilityCastCompleted&&e.AbilityKey=="execute")
            .ToArray();
        Assert.NotEmpty(casts);
        Assert.All(casts,e=>Assert.True(e.TimeSeconds>=8m));
    }
}
