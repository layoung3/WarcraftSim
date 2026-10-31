using System;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Characters.Equipment;
using Xunit;
using Xunit.Abstractions;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp173ProvisionalBaselines(ITestOutputHelper output)
{
    private static CharacterEquipmentLoadout Gear(params (string Slot,int Item,int? Enchant)[] slots)=>
        new(){Items=slots.Select(x=>new CharacterEquipmentItem {
            SlotKey=x.Slot,ItemId=x.Item,EnchantSpellId=x.Enchant
        }).ToList()};

    [Fact]
    public void StrictModeNeverSilentlyAcceptsClassicAndSkyborneRemainsUnsupported()
    {
        Assert.Equal(46,ForeverLevel60BaselineCatalog.Entries.Count);
        Assert.Equal(40,ForeverLevel60BaselineCatalog.Entries.Count(x=>
            x.ConfidenceClassification=="medium-historical-combination"));
        Assert.Equal(6,ForeverLevel60BaselineCatalog.Entries.Count(x=>
            x.ConfidenceClassification=="low-offset-extrapolated-forever-only-combination"));
        Assert.Throws<ArgumentException>(()=>
            ForeverLevel60BaselineCatalog.Find("human","mage"));
        Assert.Throws<ArgumentException>(()=>
            ForeverLevel60BaselineCatalog.FindProvisional("high-order-skyborne","mage"));
        Assert.Throws<ArgumentException>(()=>
            ForeverLevel60Baseline.Resolve(null,false,"high-order-skyborne","mage",true));
        Assert.Throws<ArgumentException>(()=>
            ForeverLevel60Baseline.Resolve(null,true,"human","mage",true));
        var historical=ForeverLevel60BaselineCatalog.FindProvisional("human","mage");
        Assert.Throws<ArgumentException>(()=>
            ForeverLevel60Baseline.Resolve(historical,false,"human","mage"));
        Assert.Equal(historical,ForeverLevel60Baseline.Resolve(
            historical,false,"human","mage",true));
        Assert.Equal("provisional-documented-proxy",historical.VerificationStatus);
        Assert.False(historical.IncludesRacialPassives);
    }

    [Theory]
    [InlineData("human","mage",30,35,45,125,120,2788)]
    [InlineData("orc","mage",33,32,47,122,123,2743)]
    [InlineData("human","warlock",45,50,65,110,115,2723)]
    [InlineData("dwarf","hunter",57,121,93,64,69,2380)]
    public void SourcePinnedClassicStatsAndForeverBaseManaStaySeparate(
        string race,string cls,int strength,int agility,int stamina,int intellect,
        int spirit,int combinedMana)
    {
        var b=ForeverLevel60BaselineCatalog.FindProvisional(race,cls);
        Assert.Equal(strength,b.Strength);
        Assert.Equal(agility,b.Agility);
        Assert.Equal(stamina,b.Stamina);
        Assert.Equal(intellect,b.Intellect);
        Assert.Equal(spirit,b.Spirit);
        Assert.Equal(combinedMana,b.ObservedUnbuffedMaximumMana);
        Assert.Equal("1.60.1.70707",ForeverClientBaseManaCatalog.SourceBuild);
        Assert.Contains("classic",b.ClientBuild,StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual("reviewed-forever-in-game",b.VerificationStatus);
        if(race=="orc" && cls=="mage")
            Assert.StartsWith("low-offset",b.ConfidenceClassification);
    }

    [Fact]
    public void ProvisionalApiReturnsTraceableSimulationInputAndResolvesRacialOnce()
    {
        var request=MageSimRequest.CreatePreset("fire");
        request.Character.Race="human";
        request.Character.Equipment=Gear(("main-hand",19102,22749));
        var controller=new ForeverCharacterController();
        var selection=new ForeverExternalModifierSelection{
            Buffs=["arcane-intellect"],
            Consumables=["flask-of-supreme-power"],
            TargetDebuffs=["curse-of-elements"]
        };
        var configured=new ForeverCharacterResolutionRequest{
            Character=request.Character,Modifiers=selection,UseProvisionalBaseline=true
        };
        var payload=Assert.IsType<OkObjectResult>(controller.GenerateLevel60(configured));
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(payload.Value));
        Assert.Equal("provisional-documented-proxy",
            json.RootElement.GetProperty("sourceStatus").GetString());
        Assert.Equal(125m,json.RootElement.GetProperty("readyProfile")
            .GetProperty("BaseStats").GetProperty("Values")
            .GetProperty("intellect").GetDecimal());
        // The raw input must not already have external buffs and racial
        // contributors; runners apply them once using the selected IDs.
        Assert.Empty(json.RootElement.GetProperty("readyProfile")
            .GetProperty("Buffs").GetProperty("Selections").EnumerateArray());
        var resolved=json.RootElement.GetProperty("resolvedProfile")
            .GetProperty("Buffs").GetProperty("Selections").ToString();
        Assert.Contains("cp173-arcane-intellect",resolved);
        Assert.Contains("cp173-racial-human",resolved);
        Assert.Equal("historical-classic-stat-proxy",json.RootElement.GetProperty("baseline")
            .GetProperty("SourceKind").GetString());
        Assert.Throws<ArgumentException>(()=>ForeverLevel60BaselineCatalog.Find("human","mage"));
    }

    [Fact]
    public void MageProvisionalRaceGearEnchantBuffAndDebuffChangeActualCombat()
    {
        MageSimRequest Build(bool geared)
        {
            var r=MageSimRequest.CreatePreset("fire");
            r.Character.Race="human";r.UseProvisionalLevel60Baseline=true;
            r.DurationSeconds=65m;r.TargetMaximumHealth=6000000m;
            r.BaseSpellHitPercent=100m;r.SpellCritPercent=0m;
            if(geared)
            {
                r.Character.Equipment=Gear(("main-hand",19102,22749));
                r.ExternalModifiers.Buffs.Add("arcane-intellect");
                r.ExternalModifiers.Consumables.Add("flask-of-supreme-power");
                r.ExternalModifiers.TargetDebuffs.Add("curse-of-elements");
            }
            return r;
        }
        var plain=new MageSimRunner().Run(Build(false));
        var modified=new MageSimRunner().Run(Build(true));
        Assert.True(modified.Dps>plain.Dps);
        Assert.True(modified.MaximumMana>plain.MaximumMana);
        Assert.Equal(plain.Dps,new MageSimRunner().Run(Build(false)).Dps);
        output.WriteLine($"CP173 historical Mage 60 DPS {plain.Dps:0.####} -> {modified.Dps:0.####}; Mana {plain.MaximumMana:0.##} -> {modified.MaximumMana:0.##}");
    }

    [Fact]
    public void WarlockProvisionalRaceTalentGearEnchantBuffAndImpDamage()
    {
        WarlockSimRequest Build(bool modified)
        {
            var r=WarlockSimRequest.CreatePreset("demonology");
            r.Character.Race="orc";r.UseProvisionalLevel60Baseline=true;
            r.DurationSeconds=72m;r.TargetMaximumHealth=9000000m;
            r.BaseSpellHitPercent=100m;r.PetType="imp";
            r.PrePullSacrificedDemon=null;
            if(modified)
            {
                r.Character.Equipment=Gear(("wrist",22511,1217189));
                r.ExternalModifiers.Consumables.Add("flask-of-supreme-power");
                r.ExternalModifiers.Buffs.Add("arcane-intellect");
                r.ExternalModifiers.TargetDebuffs.Add("curse-of-elements");
            }
            return r;
        }
        var plain=new WarlockSimRunner().Run(Build(false));
        var changed=new WarlockSimRunner().Run(Build(true));
        Assert.True(changed.WarlockDamage>plain.WarlockDamage);
        Assert.True(changed.PetDamage>plain.PetDamage);
        output.WriteLine($"CP173 historical Warlock DPS {plain.Dps:0.####} -> {changed.Dps:0.####}, Imp {plain.PetDamage:0.##} -> {changed.PetDamage:0.##}");
    }

    [Fact]
    public void HunterProvisionalDwarfPetAndRealGearUseDefaultFreeAPAndCrit()
    {
        MarksmanshipHunterSimRequest Build(bool modified)
        {
            var r=new MarksmanshipHunterSimRequest{
                Race="dwarf",UseProvisionalLevel60Baseline=true,
                DurationSeconds=80m,TargetMaximumHealth=9000000m,
                HasPet=true,PetFamily="cat",UseRapidFire=false,
                UseAimedShot=false,UseSniperShot=false,UseMultiShot=false,
                UseArcaneShot=false,UseSerpentSting=false
            };
            r.Equipment=Gear(("ranged",20368,null));
            if(modified)
            {
                r.Equipment=Gear(("ranged",20368,null),("trinket1",23041,null));
                r.ExternalModifiers.Consumables.Add("elixir-of-the-mongoose");
            }
            return r;
        }
        var plain=new MarksmanshipHunterSimRunner().Run(Build(false));
        var buffed=new MarksmanshipHunterSimRunner().Run(Build(true));
        Assert.True(buffed.HunterDamage>plain.HunterDamage);
        Assert.True(buffed.PetDamage>plain.PetDamage);
        output.WriteLine($"CP173 historical Hunter DPS {plain.Dps:0.####} -> {buffed.Dps:0.####}, Pet {plain.PetDamage:0.##} -> {buffed.PetDamage:0.##}");
        var b=ForeverLevel60BaselineCatalog.FindProvisional("dwarf","hunter");
        Assert.Equal(342m,b.ObservedUnbuffedRangedAttackPower);
    }

    [Fact]
    public void IneligibleClassRaceAndStrictRunFailBeforeSimulating()
    {
        var r=MageSimRequest.CreatePreset("fire");
        r.Character.Race="tauren";r.UseProvisionalLevel60Baseline=true;
        Assert.Throws<ArgumentException>(()=>new MageSimRunner().Run(r));
        r.Character.Race="human";
        r.UseProvisionalLevel60Baseline=false;
        r.UseReviewedLevel60Baseline=true;
        Assert.Throws<ArgumentException>(()=>new MageSimRunner().Run(r));
    }
}
