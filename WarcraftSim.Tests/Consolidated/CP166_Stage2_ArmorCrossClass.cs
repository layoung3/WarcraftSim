using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Hunter;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp166Stage2ArmorCrossClassTests
{
    [Fact]
    public void BeastMasteryNeverExceedsSeventyFivePercentArmorMitigation()
    {
        var req=new BeastMasteryHunterSimRequest{
            DurationSeconds=30m,Seed=163001,CaptureTimeline=true,
            PetFamily="bear",
            Tuning=new ForeverBeastMasteryHunterTuning{TargetArmor=1_000_000m}
        };
        var result=new BeastMasteryHunterSimRunner().Run(req);
        var hits=result.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.MitigationType=="armor" && e.RawAmount is >0m &&
            e.SourceActorKey=="hunter").ToArray();
        Assert.NotEmpty(hits);
        Assert.All(hits,e=>{
            // The 75% cap applies to Armor alone; procs/talents can still
            // modify raw damage prior to that mitigation.
            Assert.InRange(e.MitigationPercent??0m,0m,75m);
            Assert.InRange(Math.Abs((e.RawAmount??0m)-
                (e.MitigatedAmount??0m)-(e.Amount??0m)),0m,.0001m);
        });
        Assert.Contains(hits,e=>e.MitigationPercent==75m);
    }

    [Fact]
    public void BearIncomingArmorCapCannotReduceHitByMoreThanSeventyFivePercent()
    {
        var req=new FeralDruidSimRequest{
            Role="bear",EnableBossAttacks=true,
            DurationSeconds=4.5m,CaptureTimeline=true,Seed=166063,
            BossHitDamage=800m,BossSwingSeconds=2m,
            BearBaseArmor=1_000_000m,BearDodgePercent=0m
        };
        var result=new FeralDruidSimRunner().Run(req);
        var hits=result.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="target"&&e.TargetActorKey=="druid"&&
            e.Amount is >0m).ToArray();
        Assert.NotEmpty(hits);
        Assert.All(hits,e=>{
            Assert.InRange(e.MitigationPercent??0m,0m,75m);
            Assert.True((e.Amount??0m)>=.25m*(e.RawAmount??0m)-.0001m);
        });
    }
}
