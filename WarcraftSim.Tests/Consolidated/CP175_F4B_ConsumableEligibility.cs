using System;
using System.Collections.Generic;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Items;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175F4BConsumableEligibility
{
    private static MageSimRequest Mage(int quantity,bool disabled,decimal startingMana,
        decimal threshold=90m)
    {
        var request=MageSimRequest.CreatePreset("fire");
        request.DurationSeconds=30m;
        request.Seed=1758420;
        request.TargetMaximumHealth=10000000m;
        request.StartingManaPercent=startingMana;
        request.UseEvocation=false;
        // The legacy Boolean is deliberately off. Only explicit inventory
        // in CombatItems is allowed to enable shared combat item execution.
        request.UseMajorManaPotion=false;
        request.MajorManaPotionCharges=0;
        request.CombatItems=new CombatUseItemsOptions{
            MajorManaPotionCharges=quantity,
            MajorManaPotionBelowManaPercent=threshold,
            DisabledItemKeys=disabled?["major-mana-potion"]:[]
        };
        return request;
    }

    private static int Casts(MageSimResponse response)
        =>response.Run.Timeline.Count(e=>
            e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey==ForeverCombatItemCatalog.MajorManaPotion);

    [Fact]
    public void AbsentAndExplicitlyDisabledMajorManaPotionsNeverAppearInCombat()
    {
        var absent=new MageSimRunner().Run(Mage(0,false,10m));
        var disabled=new MageSimRunner().Run(Mage(1,true,10m));
        Assert.Equal(0,Casts(absent));
        Assert.Equal(0,Casts(disabled));
        Assert.Equal(0,absent.CombatItemUses.GetValueOrDefault(
            ForeverCombatItemCatalog.MajorManaPotion));
        Assert.Equal(0,disabled.CombatItemUses.GetValueOrDefault(
            ForeverCombatItemCatalog.MajorManaPotion));
    }

    [Fact]
    public void PreparedEnabledPotionConsumesOneChargeOnlyUnderValidManaConditions()
    {
        var used=new MageSimRunner().Run(Mage(1,false,10m));
        var replay=new MageSimRunner().Run(Mage(1,false,10m));
        Assert.Equal(1,Casts(used));
        Assert.Equal(1,used.CombatItemUses.GetValueOrDefault(
            ForeverCombatItemCatalog.MajorManaPotion));
        Assert.Contains(used.Run.Timeline,e=>
            e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey==ForeverCombatItemCatalog.MajorManaPotion &&
            e.ResourceKey=="mana" && e.Amount>0m);
        Assert.Equal(used.DamageDone,replay.DamageDone);
        Assert.Equal(used.Run.Timeline.Select(e=>
            (e.TimeSeconds,e.Type,e.AbilityKey,e.Amount)).ToArray(),
            replay.Run.Timeline.Select(e=>
            (e.TimeSeconds,e.Type,e.AbilityKey,e.Amount)).ToArray());
    }

    [Fact]
    public void PreparedPotionDoesNotExecuteBeforeItsManaThresholdIsReached()
    {
        var request=Mage(1,false,100m,10m);
        request.DurationSeconds=1m;
        var result=new MageSimRunner().Run(request);
        Assert.Equal(0,Casts(result));
    }

    [Fact]
    public void DisabledOrAbsentConsumableCannotBeResurrectedByUnrelatedBuildSelections()
    {
        var req=Mage(1,true,10m);
        req.ExternalModifiers.Buffs=["arcane-intellect"];
        req.ExternalModifiers.Consumables=["nightfin-soup"];
        var result=new MageSimRunner().Run(req);
        Assert.Equal(0,Casts(result));
        Assert.Contains("major-mana-potion",req.CombatItems.DisabledItemKeys);
        Assert.Equal(1,req.CombatItems.MajorManaPotionCharges);
    }
}
