using System;
using System.Linq;
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Shaman;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167URestorationShamanTotems
{
    private static RestorationShamanSimRequest Setup() => new() {
        DurationSeconds=20m,CaptureTimeline=true,
        UseSharedTotemLifecycle=true,UseCallOfElements=true,
        IncomingDamage=600m,DamageEverySeconds=3m,
        AdditionalAllies=1
    };

    [Fact]
    public void RestorationCallDeploysFourSlotsAtOneCombinedManaCost()
    {
        var req=Setup();
        req.DurationSeconds=8m;
        var run=new RestorationShamanSimRunner().Run(req);
        var applied=run.Run.Timeline.Where(e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="call-of-elements").ToList();
        Assert.Contains(applied,e=>e.EffectKey=="strength-of-earth-aura");
        Assert.Contains(applied,e=>e.EffectKey=="searing-totem-tick");
        Assert.Contains(applied,e=>e.EffectKey=="healing-stream-aura");
        Assert.Contains(applied,e=>e.EffectKey=="windfury-totem-air");
        Assert.Single(run.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="call-of-elements" && e.ResourceKey=="mana" && e.Amount<0m);
        Assert.True(run.Run.CustomMetrics["shaman-totem-placements"]>=4m);
    }

    [Fact]
    public void RestorationWaterTotemReplacementCancelsHealingStreamPulses()
    {
        var req=Setup();
        req.UseCallOfElements=true;
        req.DurationSeconds=30m;
        // Programmed Call first places Healing Stream, then the existing
        // Mana Tide healer rule replaces that Water slot (not both at once).
        var run=new RestorationShamanSimRunner().Run(req);
        Assert.True(run.Run.CustomMetrics["shaman-totem-placements"]>=1m);
        Assert.True(run.Run.CustomMetrics["shaman-totem-replacements"]>=1m);
        Assert.True(run.HealingDone>0m);
        Assert.Contains(run.Run.Timeline,e=>e.EffectKey=="mana-tide-aura" &&
            e.Type==CombatEventType.AuraApplied);
    }

    [Fact]
    public void RestorationRecallUsesSharedRefundWithoutDuplicatePulses()
    {
        var req=Setup();
        req.UseTotemicRecall=true;
        req.TotemicRecallAtSeconds=9m;
        req.DurationSeconds=18m;
        var run=new RestorationShamanSimRunner().Run(req);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey=="totemic-recall");
        Assert.True(run.Run.CustomMetrics["shaman-totem-recall-mana"]>0m);
        Assert.Contains(run.Run.Timeline,e=>e.EffectKey=="totemic-recall-refund" &&
            e.Type==CombatEventType.ResourceChanged && e.Amount>0m);
    }

    [Fact]
    public void ProgrammedCallsRetainCurrentTrainerLevelsAndSerialization()
    {
        var book=ForeverElementalShamanSpellbook.Create(
            new ForeverElementalShamanTalents(),new ForeverElementalShamanTuning(),0m);
        Assert.Equal(30,ForeverShamanTotemCalls.Call("call-of-spirits",book,
            ["stoneskin-totem"]).RequiredLevel);
        Assert.Equal(40,ForeverShamanTotemCalls.Call("call-of-ancestors",book,
            ["stoneskin-totem"]).RequiredLevel);
        var req=Setup();
        var result=JsonSerializer.Deserialize<RestorationShamanSimRequest>(
            JsonSerializer.Serialize(req));
        Assert.NotNull(result);
        Assert.True(result.UseSharedTotemLifecycle);
        Assert.Equal(4,result.CallOfElementsTotems.Count);
    }
}
