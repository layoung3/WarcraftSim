using System;
using System.Collections.Generic;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Data.Forever.Items;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>CP174 source-backed stacking and actual shared-item scheduling regression.</summary>
public sealed class Cp174ConsumablePriorityTests
{
    [Fact]
    public void DistinctForeverElixirsCanStackWithoutTbcBattleGuardianSlots()
    {
        var selected = ForeverExternalModifierCatalog.Resolve(new ForeverExternalModifierSelection {
            Consumables=["greater-arcane-elixir","elixir-of-the-mongoose",
                "elixir-of-the-sages","flask-of-supreme-power"]
        },60);
        Assert.Equal(4,selected.Count);
        Assert.Contains(selected,x=>x.Key=="elixir-of-the-mongoose");
    }

    [Fact]
    public void FlaskDuplicatesFoodAndAgilityEffectDuplicatesAreRejected()
    {
        Assert.Throws<ArgumentException>(()=>ForeverExternalModifierCatalog.Resolve(
            new ForeverExternalModifierSelection {
                Consumables=["flask-of-supreme-power","flask-of-the-titans"]
            },60));
        Assert.Throws<ArgumentException>(()=>ForeverExternalModifierCatalog.Resolve(
            new ForeverExternalModifierSelection {
                Consumables=["elixir-of-the-mongoose","elixir-of-greater-agility"]
            },60));
        Assert.Throws<ArgumentException>(()=>ForeverExternalModifierCatalog.Resolve(
            new ForeverExternalModifierSelection {
                Consumables=["nightfin-soup","grilled-squid"]
            },60));
    }

    [Fact]
    public void SapperWinsSingleTargetDamageAndDynamiteUsesNextSharedWindow()
    {
        var request=MageSimRequest.CreatePreset("fire");
        request.DurationSeconds=90m;
        request.CombatItems=new() {
            EngineeringSkill=250,GoblinSapperCharges=1,DenseDynamiteCharges=2
        };
        var result=new MageSimRunner().Run(request);
        Assert.Equal(1,result.CombatItemUses.GetValueOrDefault(ForeverCombatItemCatalog.GoblinSapperCharge));
        Assert.True(result.CombatItemUses.GetValueOrDefault(ForeverCombatItemCatalog.DenseDynamite)>=1);
        var casts=result.Run.Timeline
            .Where(x=>x.Type==CombatEventType.AbilityCastCompleted &&
                x.AbilityKey is ForeverCombatItemCatalog.GoblinSapperCharge or ForeverCombatItemCatalog.DenseDynamite)
            .OrderBy(x=>x.TimeSeconds).ToArray();
        Assert.NotEmpty(casts);
        Assert.Equal(ForeverCombatItemCatalog.GoblinSapperCharge,casts[0].AbilityKey);
        Assert.All(casts.Skip(1).Zip(casts,(later,earlier)=>(later,earlier)),
            pair=>Assert.True(pair.later.TimeSeconds-pair.earlier.TimeSeconds>=60m));
    }

    [Fact]
    public void UnsafeSapperFallsBackToEligibleDynamite()
    {
        var request=MageSimRequest.CreatePreset("fire");
        request.DurationSeconds=12m;
        request.CombatItems=new() {
            EngineeringSkill=250,GoblinSapperCharges=1,DenseDynamiteCharges=1,
            MinimumHealthToUseSapper=100000m
        };
        var result=new MageSimRunner().Run(request);
        Assert.Equal(0,result.CombatItemUses.GetValueOrDefault(ForeverCombatItemCatalog.GoblinSapperCharge));
        Assert.Equal(1,result.CombatItemUses.GetValueOrDefault(ForeverCombatItemCatalog.DenseDynamite));
    }

    [Fact]
    public void ManaRuneBeatsLowerExpectedManaGemWhenBothAreReady()
    {
        var request=MageSimRequest.CreatePreset("frost");
        request.DurationSeconds=15m;request.StartingManaPercent=10m;
        request.UseEvocation=false;request.UseMajorManaPotion=false;
        request.CombatItems=new() {
            ManaRubyCharges=1,DarkRuneCharges=1,
            ManaRubyBelowManaPercent=90m,RuneBelowManaPercent=90m
        };
        var result=new MageSimRunner().Run(request);
        var first=result.Run.Timeline.First(x=>x.Type==CombatEventType.AbilityCastCompleted &&
            x.AbilityKey is ForeverCombatItemCatalog.ManaRuby or ForeverCombatItemCatalog.DarkRune);
        Assert.Equal(ForeverCombatItemCatalog.DarkRune,first.AbilityKey);
        Assert.Equal(1,result.CombatItemUses.GetValueOrDefault(ForeverCombatItemCatalog.DarkRune));
        Assert.InRange(result.CombatItemUses.GetValueOrDefault(ForeverCombatItemCatalog.ManaRuby),0,1);
    }
}
