using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp166Stage2DruidIncomingRageTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(3000)]
    [InlineData(8000)]
    public void BearIncomingRageUsesExpectedArmorNotEquippedArmor(int armor)
    {
        var request=new FeralDruidSimRequest {
            Role="bear",DurationSeconds=4.5m,Seed=166061,
            CaptureTimeline=true,EnableBossAttacks=true,
            BossHitDamage=850m,BossSwingSeconds=2m,
            BearBaseArmor=armor,BearDodgePercent=0m,
            IncomingRageExpectedCreatureHealth=10000m,
            IncomingRageExpectedArmorReductionPercent=35m
        };
        var result=new FeralDruidSimRunner().Run(request);
        var rageEvents=result.Run.Timeline.Where(e=>
            e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey==ForeverDruidDamageTakenRageFactory.DefinitionKey).ToArray();
        Assert.NotEmpty(rageEvents);
        var expected=850m/10000m*20m*(1m-.35m);
        // Armor changes damage taken, not normalized Rage generated from
        // identical pre-armor attacks.
        Assert.All(rageEvents,e=>{
            Assert.InRange(Math.Abs((e.Amount??0m)-expected),0m,.00001m);
            Assert.Equal("rage",e.ResourceKey);
            Assert.True(e.ResourceTotalAfter is >=0m and <=100m);
        });
        Assert.All(result.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="target"&&e.TargetActorKey=="druid" &&
            e.Amount>0m),e=>
                Assert.Equal("armor",e.MitigationType));
    }

    [Fact]
    public void CatFormDoesNotReceiveBearSpecificIncomingRage()
    {
        var request=new FeralDruidSimRequest{
            Role="cat",DurationSeconds=4.5m,Seed=166062,
            CaptureTimeline=true,EnableBossAttacks=true,
            BossHitDamage=850m,BossSwingSeconds=2m
        };
        var result=new FeralDruidSimRunner().Run(request);
        Assert.DoesNotContain(result.Run.Timeline,e=>
            e.AbilityKey==ForeverDruidDamageTakenRageFactory.DefinitionKey &&
            e.Type==CombatEventType.ResourceChanged);
    }

    [Fact]
    public void InvalidIncomingRageCalibrationFailsRatherThanSilentlyGuessing()
    {
        var request=new FeralDruidSimRequest{
            Role="bear",EnableBossAttacks=true,IncomingRageExpectedCreatureHealth=0m
        };
        Assert.Throws<ArgumentException>(()=>new FeralDruidSimRunner().Run(request));
        request.IncomingRageExpectedCreatureHealth=10000m;
        request.IncomingRageExpectedArmorReductionPercent=101m;
        Assert.Throws<ArgumentException>(()=>new FeralDruidSimRunner().Run(request));
    }
}
