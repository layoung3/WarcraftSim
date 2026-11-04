using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Characters.Equipment;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175F4BCharacterStatIntegrity
{
    private static Dictionary<string,decimal> Stats(string race,string cls,
        ForeverExternalModifierSelection? selected=null,
        CharacterEquipmentLoadout? gear=null)
    {
        var profile=new CharacterProfile{
            Name="F4B controlled character",ClassKey=cls,
            SpecializationKey=cls switch {
                "warrior"=>"arms","mage"=>"arcane",
                "warlock"=>"demonology",_=>"survival"
            },RulesetKey="wow-forever",Level=60,Race=race,
            Equipment=gear??new CharacterEquipmentLoadout()
        };
        var result=Assert.IsType<OkObjectResult>(
            new ForeverCharacterController().GenerateLevel60(
                new ForeverCharacterResolutionRequest{
                    Character=profile,Modifiers=selected??new(),
                    UseProvisionalBaseline=true
                }));
        using var document=JsonDocument.Parse(JsonSerializer.Serialize(result.Value));
        return document.RootElement.GetProperty("effectiveStats")
            .GetProperty("Values").EnumerateObject()
            .Where(x=>x.Value.ValueKind==JsonValueKind.Number)
            .ToDictionary(x=>x.Name,x=>x.Value.GetDecimal(),
                StringComparer.OrdinalIgnoreCase);
    }
    private static decimal Get(Dictionary<string,decimal> stats,string key)
        =>stats.GetValueOrDefault(key);

    [Fact]
    public void WarriorFoodElixirAndMarkOfWildApplyExactStrengthAndRemoveCleanly()
    {
        var baseline=Stats("human","warrior");
        var food=new ForeverExternalModifierSelection{
            Consumables=["smoked-desert-dumplings"]};
        var withFood=Stats("human","warrior",food);
        Assert.Equal(20m,Get(withFood,"strength")-Get(baseline,"strength"));
        food.Consumables.Add("elixir-of-greater-strength");
        var withElixir=Stats("human","warrior",food);
        Assert.Equal(45m,Get(withElixir,"strength")-Get(baseline,"strength"));
        food.Buffs.Add("mark-of-the-wild");
        var full=Stats("human","warrior",food);
        Assert.Equal(61m,Get(full,"strength")-Get(baseline,"strength"));
        Assert.True(Get(full,"attack-power")>Get(baseline,"attack-power"));
        food.Consumables.Clear();food.Buffs.Clear();
        Assert.Equal(baseline,Stats("human","warrior",food));
    }

    [Theory]
    [InlineData("mage","human","nightfin-soup",22,35)]
    [InlineData("warlock","orc","baked-salmon",28,35)]
    public void CasterFoodElixirAndIntellectBuffAreSelectedOnceAndReversible(
        string cls,string race,string meal,int foodPower,int elixirPower)
    {
        var baseline=Stats(race,cls);
        var selection=new ForeverExternalModifierSelection {
            Consumables=[meal],TargetDebuffs=["curse-of-elements"]
        };
        var afterFood=Stats(race,cls,selection);
        Assert.Equal(foodPower,Get(afterFood,"spell-power")-Get(baseline,"spell-power"));
        selection.Consumables.Add("greater-arcane-elixir");
        var afterElixir=Stats(race,cls,selection);
        Assert.Equal(foodPower+elixirPower,
            Get(afterElixir,"spell-power")-Get(baseline,"spell-power"));
        selection.Buffs.Add("arcane-intellect");
        var full=Stats(race,cls,selection);
        Assert.Equal(31m,Get(full,"intellect")-Get(baseline,"intellect"));
        Assert.Equal(foodPower+elixirPower,
            Get(full,"spell-power")-Get(baseline,"spell-power"));
        selection.Buffs.Clear();
        Assert.Equal(Get(afterElixir,"intellect"),Get(Stats(race,cls,selection),"intellect"));
        selection.Consumables.Clear();selection.TargetDebuffs.Clear();
        Assert.Equal(baseline,Stats(race,cls,selection));
    }

    [Fact]
    public void HunterFoodMongooseAndBattleShoutAdjustIndependentOwnerStatsAndRestore()
    {
        var baseline=Stats("orc","hunter");
        var selection=new ForeverExternalModifierSelection {
            Consumables=["poached-sunscale-salmon"]};
        var food=Stats("orc","hunter",selection);
        Assert.Equal(40m,Get(food,"ranged-attack-power")-
            Get(baseline,"ranged-attack-power"));
        selection.Consumables.Add("elixir-of-the-mongoose");
        var elixir=Stats("orc","hunter",selection);
        Assert.Equal(25m,Get(elixir,"agility")-Get(baseline,"agility"));
        Assert.True(Get(elixir,"attack-critical-chance-percent")>=
            Get(baseline,"attack-critical-chance-percent")+2m);
        selection.Buffs.Add("battle-shout");
        var full=Stats("orc","hunter",selection);
        Assert.True(Get(full,"attack-power")>Get(elixir,"attack-power"));
        selection.Consumables.Clear();selection.Buffs.Clear();
        Assert.Equal(baseline,Stats("orc","hunter",selection));
    }

    [Fact]
    public void RegisteredMageWeaponAndEnchantRemoveBackToIdenticalStatBaseline()
    {
        var baseline=Stats("human","mage");
        var weapon=new CharacterEquipmentLoadout{Items=[
            new CharacterEquipmentItem{
                SlotKey="main-hand",ItemId=19102,EnchantSpellId=1219580
            }
        ]};
        var equipped=Stats("human","mage",gear:weapon);
        Assert.NotEqual(baseline,equipped);
        weapon.Items.Clear();
        Assert.Equal(baseline,Stats("human","mage",gear:weapon));
        weapon.Items.Add(new CharacterEquipmentItem{
            SlotKey="main-hand",ItemId=19102,EnchantSpellId=1219580
        });
        Assert.Equal(equipped,Stats("human","mage",gear:weapon));
    }

    [Fact]
    public void DuplicateFoodAndIncompatibleModifierCategoriesFailInsteadOfStacking()
    {
        Assert.Throws<ArgumentException>(()=>ForeverExternalModifierCatalog.Resolve(
            new ForeverExternalModifierSelection{
                Consumables=["nightfin-soup","baked-salmon"]
            },60));
        Assert.Throws<ArgumentException>(()=>ForeverExternalModifierCatalog.Resolve(
            new ForeverExternalModifierSelection{
                Buffs=["arcane-intellect"],
                TargetDebuffs=["arcane-intellect"]
            },60));
    }
}
