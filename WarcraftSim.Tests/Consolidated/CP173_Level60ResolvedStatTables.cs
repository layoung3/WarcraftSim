using System;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Characters.Equipment;
using Xunit;
using Xunit.Abstractions;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp173Level60ResolvedStatTables(ITestOutputHelper output)
{
    private static CharacterEquipmentLoadout Gear(params (string Slot,int Item,int? Enchant)[] entries) =>
        new(){Items=entries.Select(x=>new CharacterEquipmentItem {
            SlotKey=x.Slot,ItemId=x.Item,EnchantSpellId=x.Enchant
        }).ToList()};

    [Theory]
    [InlineData("human","mage")]
    [InlineData("orc","warlock")]
    [InlineData("dwarf","hunter")]
    public void ProvisionalFullySelectedProfileReportsPrimaryDerivedAndAllContributionSources(
        string race,string classKey)
    {
        var mage=MageSimRequest.CreatePreset("fire");
        var warlock=WarlockSimRequest.CreatePreset("demonology");
        var character=classKey switch {
            "mage"=>mage.Character,
            "warlock"=>warlock.Character,
            _=>new CharacterProfile{
                Name="Provisional Hunter",
                RulesetKey="wow-forever",ClassKey="hunter",
                SpecializationKey="marksmanship",Level=60,Race="dwarf"
            }
        };
        character.Race=race;
        if(classKey=="mage")
            character.Equipment=Gear(("main-hand",19102,22749));
        else if(classKey=="warlock")
            character.Equipment=Gear(("wrist",22511,1217189));
        else
            character.Equipment=Gear(("ranged",20368,null),("trinket1",23041,null));
        var selected=new ForeverExternalModifierSelection{
            Buffs=["arcane-intellect"],
            Consumables=classKey=="hunter"?
                ["elixir-of-the-mongoose"]:["flask-of-supreme-power"],
            TargetDebuffs=["curse-of-elements"]
        };
        var result=Assert.IsType<OkObjectResult>(new ForeverCharacterController().GenerateLevel60(
            new ForeverCharacterResolutionRequest{
                Character=character,Modifiers=selected,
                UseProvisionalBaseline=true
            }));
        using var doc=JsonDocument.Parse(JsonSerializer.Serialize(result.Value));
        var root=doc.RootElement;
        Assert.Equal("provisional-documented-proxy",
            root.GetProperty("sourceStatus").GetString());
        var stats=root.GetProperty("effectiveStats").GetProperty("Values");
        decimal Get(string key)=>stats.TryGetProperty(key,out var x)?x.GetDecimal():0m;
        var baseline=ForeverLevel60BaselineCatalog.FindProvisional(race,classKey);
        Assert.True(Get("strength")>=baseline.Strength);
        Assert.True(Get("agility")>=baseline.Agility);
        Assert.True(Get("stamina")>=baseline.Stamina);
        Assert.True(Get("intellect")>=baseline.Intellect);
        Assert.True(Get("spirit")>=baseline.Spirit);
        if(classKey!="hunter")Assert.True(Get("spell-power")>=150m);
        else
        {
            // WoWSims naked Hunter AP/RAP include a class base offset.
            // The CP170 derived converter must not discard that offset nor
            // count Agility a second time.
            Assert.True(Get("attack-power")>=278m);
            Assert.True(Get("ranged-attack-power")>=342m);
            Assert.True(Get("armor")>=242m);
        }
        var provenance=root.GetProperty("contributions").ToString();
        Assert.Contains("buff:cp173-",provenance);
        Assert.Contains("equipment",provenance);
        output.WriteLine($"CP173 FULL STATS {race}/{classKey} | STR {Get("strength"):0.###} AGI {Get("agility"):0.###} STA {Get("stamina"):0.###} INT {Get("intellect"):0.###} SPI {Get("spirit"):0.###} | AP {Get("attack-power"):0.###} RAP {Get("ranged-attack-power"):0.###} SP {Get("spell-power"):0.###} | armor {Get("armor"):0.###} | confidence {baseline.ConfidenceClassification}");
    }
}
