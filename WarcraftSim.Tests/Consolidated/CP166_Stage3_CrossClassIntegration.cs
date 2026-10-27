using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>
/// Integration smoke coverage for representative existing class spellbooks;
/// these do not alter talent presets or compare unrelated classes' DPS.
/// </summary>
public sealed class Cp166Stage3CrossClassIntegrationTests
{
    [Theory]
    [InlineData("mage")]
    [InlineData("warlock")]
    [InlineData("shadow-priest")]
    [InlineData("elemental-shaman")]
    [InlineData("balance-druid")]
    public void ExistingCasterRunnersPreserveDamageEventAccounting(string classKey)
    {
        var run=classKey switch
        {
            "mage"=>new MageSimRunner().Run(MageRequest()).Run,
            "warlock"=>new WarlockSimRunner().Run(
                WarlockRequest()).Run,
            "shadow-priest"=>new ShadowPriestSimRunner().Run(new ShadowPriestSimRequest {
                DurationSeconds=24m,CaptureTimeline=true
            }).Run,
            "elemental-shaman"=>new ElementalShamanSimRunner().Run(
                new ElementalShamanSimRequest{
                    DurationSeconds=24m,CaptureTimeline=true
                }).Run,
            "balance-druid"=>new BalanceDruidSimRunner().Run(new BalanceDruidSimRequest {
                DurationSeconds=24m,CaptureTimeline=true
            }).Run,
            _=>throw new ArgumentOutOfRangeException(nameof(classKey))
        };
        var events=run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey is not null && e.Amount is >0m).ToArray();
        Assert.NotEmpty(events);
        Assert.All(events,e=>{
            Assert.True(e.RawAmount is >=0m);
            Assert.True(e.Amount is >=0m);
        });
        // An already active DoT cannot produce individual tick misses.
        Assert.DoesNotContain(run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.EffectDeliveryType==CombatEffectDeliveryType.Periodic &&
            e.ResultKey=="miss");
        Assert.True(run.Summary.DamageDone>=0m);
    }

    [Theory]
    [InlineData("holy-priest")]
    [InlineData("restoration-shaman")]
    [InlineData("restoration-druid")]
    public void ExistingHealerRunnersPreserveHealingConservation(string classKey)
    {
        var run=classKey switch
        {
            "holy-priest"=>new HolyPriestPreviewRunner().Run(new HolyPriestPreviewRequest {
                DurationSeconds=24m,CaptureTimeline=true
            }).Run,
            "restoration-shaman"=>new RestorationShamanSimRunner().Run(
                new RestorationShamanSimRequest {
                    DurationSeconds=24m,CaptureTimeline=true
                }).Run,
            "restoration-druid"=>new RestorationDruidSimRunner().Run(
                new RestorationDruidSimRequest {
                    DurationSeconds=24m,CaptureTimeline=true
                }).Run,
            _=>throw new ArgumentOutOfRangeException(nameof(classKey))
        };
        var healing=run.Timeline.Where(e=>e.Type==CombatEventType.Healing &&
            e.RawAmount is >=0m && e.Amount is >=0m).ToArray();
        Assert.NotEmpty(healing);
        Assert.All(healing,e=>{
            Assert.True(e.OverhealingAmount.GetValueOrDefault()>=0m);
            Assert.InRange(Math.Abs((e.RawAmount??0m)-
                (e.Amount??0m)-(e.OverhealingAmount??0m)),
                0m,.001m);
        });
    }

    private static MageSimRequest MageRequest()
    {
        var req=MageSimRequest.CreatePreset("fire");
        req.DurationSeconds=24m;
        req.CaptureTimeline=true;
        return req;
    }

    private static WarlockSimRequest WarlockRequest()
    {
        var req=WarlockSimRequest.CreatePreset("affliction");
        req.DurationSeconds=24m;
        req.CaptureTimeline=true;
        return req;
    }
}
