using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Shaman;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167OShamanTotemLifecycle
{
    private static readonly string[] AllFour=[
        "strength-of-earth-totem","searing-totem",
        "mana-spring-totem","windfury-totem"
    ];

    [Fact]
    public void CallRequiresExactlyOneLearnedTotemPerSlotAndCombinedCost()
    {
        var book=ForeverElementalShamanSpellbook.Create(
            new ForeverElementalShamanTalents(),new ForeverElementalShamanTuning(),0m);
        var call=ForeverShamanTotemCalls.Call("call-of-elements",book,AllFour);
        Assert.Equal(3m,call.CastTimeSeconds);
        Assert.Equal(1.5m,call.GlobalCooldownSeconds);
        Assert.Equal(30,ForeverShamanTotemCalls.Call("call-of-ancestors",book,
            ["stoneskin-totem","windfury-totem"]).RequiredLevel);
        Assert.Equal(40,ForeverShamanTotemCalls.Call("call-of-spirits",book,
            ["stoneskin-totem","windfury-totem"]).RequiredLevel);
        Assert.Equal(AllFour.Sum(k=>book.Single(x=>x.Key==k).ResourceCosts[0].Amount),
            call.ResourceCosts.Single().Amount);
        Assert.Equal(4,call.Effects.Count);
        Assert.Throws<ArgumentException>(()=>ForeverShamanTotemCalls.Call(
            "call-of-elements",book,["strength-of-earth-totem","strength-of-earth-totem"]));
    }

    [Fact]
    public void ElementalCallDeploysFourEffectsWithOneCombinedManaSpend()
    {
        var req=ElementalPreRaidProfileCatalog.Setup("throughput","raid-sustain");
        req.DurationSeconds=16m;
        req.CaptureTimeline=true;
        req.CustomPriority=["call-of-elements","lightning-bolt"];
        var sim=new ElementalShamanSimRunner().Run(req);
        var applied=sim.Run.Timeline.Where(e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="call-of-elements").ToList();
        Assert.Contains(applied,e=>e.EffectKey=="strength-of-earth-aura");
        Assert.Contains(applied,e=>e.EffectKey=="searing-totem-tick");
        Assert.Contains(applied,e=>e.EffectKey=="mana-spring-totem-tick");
        Assert.Contains(applied,e=>e.EffectKey=="windfury-totem-air");
        Assert.Single(sim.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey=="call-of-elements");
        var spent=sim.Run.Timeline.Where(e=>e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="call-of-elements" && e.ResourceKey=="mana" &&
            e.Amount is <0m).ToList();
        Assert.Single(spent);
        Assert.True(spent[0].Amount<0m);
        Assert.True(sim.DamageDone>0m);
    }

    [Fact]
    public void RecallClearsFourSlotsAndReturnsQuarterOfActuallyPaidMana()
    {
        var req=ElementalPreRaidProfileCatalog.Setup("throughput","raid-sustain");
        req.DurationSeconds=7m;
        req.CaptureTimeline=true;
        req.CustomPriority=["call-of-elements","totemic-recall"];
        var sim=new ElementalShamanSimRunner().Run(req);
        var spend=sim.Run.Timeline.First(e=>e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="call-of-elements" && e.ResourceKey=="mana" &&
            e.Amount<0m);
        var refund=sim.Run.Timeline.First(e=>e.Type==CombatEventType.ResourceChanged &&
            e.EffectKey=="totemic-recall-refund");
        Assert.True(refund.Amount>0m);
        Assert.Equal(-spend.Amount!.Value*.25m,refund.Amount!.Value);
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.AuraRemoved &&
            e.EffectKey=="strength-of-earth-aura");
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.AuraRemoved &&
            e.EffectKey=="windfury-totem-air");
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.AuraRemoved &&
            e.EffectKey=="searing-totem-tick");
    }

    [Theory]
    [InlineData("call-of-ancestors",30)]
    [InlineData("call-of-spirits",40)]
    public void AdditionalCallPresetsUseDistinctVerifiedLevelAndAuras(string key,int requiredLevel)
    {
        var book=ForeverElementalShamanSpellbook.Create(
            new ForeverElementalShamanTalents(),new ForeverElementalShamanTuning(),0m);
        var call=ForeverShamanTotemCalls.Call(key,book,
            ["stoneskin-totem","searing-totem","mana-spring-totem","windfury-totem"]);
        Assert.Equal(requiredLevel,call.RequiredLevel);
        Assert.Equal(3m,call.CastTimeSeconds);
        Assert.Equal(1.5m,call.GlobalCooldownSeconds);
        Assert.Contains(call.Effects,e=>e.AuraKey=="shaman-stoneskin-totem");
        Assert.DoesNotContain(call.Effects,e=>e.AuraKey=="shaman-strength-of-earth-totem");
        Assert.Equal("shaman-stoneskin-totem",
            ForeverShamanTotemCalls.SelectedPresetAura(
                ["stoneskin-totem","searing-totem","mana-spring-totem","windfury-totem"]));
    }

    [Fact]
    public void EnhancementCallActivatesRealWindfuryAndFourTotemAuras()
    {
        var req=new EnhancementShamanSimRequest {
            DurationSeconds=18m,CaptureTimeline=true,
            UseCallOfElements=true,UseWindfuryWeapon=false,UseWindfuryTotem=true,
            MainHandImbue="none",WindfuryTotemProcChancePercent=100m,
            BaseMeleeHitPercent=100m,CriticalChancePercent=0m,
            UseRageOfTheFarseer=false
        };
        var sim=new EnhancementShamanSimRunner().Run(req);
        Assert.Contains(sim.Run.Timeline,e=>e.AbilityKey=="call-of-elements" &&
            e.Type==CombatEventType.AbilityCastCompleted);
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.EffectKey=="windfury-totem-air");
        Assert.True(sim.WindfuryTotemProcs>0);
    }
}
