using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp168DemonologyKnowledgeTransitions
{
    private static WarlockSimRequest Setup(int rank,decimal engageDelay)
    {
        var r=WarlockSimRequest.CreatePreset("demonology");
        r.PrePullSacrificedDemon=null;
        r.PetType="imp";
        r.DurationSeconds=15m;
        r.TargetMaximumHealth=1000000m;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.UseLifeTap=false;
        r.CustomPriority=["shadow-bolt"];
        r.Talents.Ranks["demonic-knowledge"]=rank;
        r.DemonTuning.DemonEngagementDelaySeconds=engageDelay;
        r.CaptureTimeline=true;
        return r;
    }

    [Fact]
    public void BeforePetEngagesDemonicKnowledgeCannotInflateOwnerDamage()
    {
        var baseline=Setup(0,20m);
        var talented=Setup(3,20m);
        baseline.DurationSeconds=7m;
        talented.DurationSeconds=7m;
        var a=new WarlockSimRunner().Run(baseline);
        var b=new WarlockSimRunner().Run(talented);
        Assert.Equal(a.WarlockDamage,b.WarlockDamage);
        Assert.Equal(0m,b.DemonUptimeSeconds);
        Assert.DoesNotContain(b.Run.Timeline,e=>e.SourceActorKey=="warlock-pet" &&
            e.Type==CombatEventType.Damage && e.Amount>0m);
    }

    [Fact]
    public void EngagedPetAddsOwnerSpellPowerThenDeathRemovesBonus()
    {
        var zero=Setup(0,.75m);
        var three=Setup(3,.75m);
        three.DemonDeathAtSeconds=7m;
        zero.DemonDeathAtSeconds=7m;
        var baseRun=new WarlockSimRunner().Run(zero);
        var enhanced=new WarlockSimRunner().Run(three);
        decimal OwnerBolt(WarlockSimResponse r,bool after)=>
            r.Run.Timeline.First(e=>e.Type==CombatEventType.Damage &&
                e.SourceActorKey=="warlock" && e.AbilityKey=="shadow-bolt" &&
                e.Amount>0m && (after?e.TimeSeconds>7m:e.TimeSeconds<7m))
                .Amount!.Value;
        Assert.True(OwnerBolt(enhanced,false)>OwnerBolt(baseRun,false));
        Assert.Equal(OwnerBolt(baseRun,true),OwnerBolt(enhanced,true));
    }
}
