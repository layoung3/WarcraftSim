using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;
using Xunit.Abstractions;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp173Level60Generation(ITestOutputHelper output)
{
    private static ForeverLevel60BaselineObservation Baseline(string race,string cls) =>
        new() {
            Race=race,ClassKey=cls,Level=60,
            Strength=50m,Agility=75m,Stamina=110m,Intellect=165m,Spirit=145m,
            ObservedUnbuffedMaximumHealth=3300m,
            ObservedUnbuffedMaximumMana=3500m,
            ClientBuild="1.60.1.70707",
            SourceKind="user-unverified-observation",
            VerificationStatus="unverified-user-supplied",
            SourceReference="synthetic-ci-explicit-fixture-not-real-player",
            RecordedAtUtc="2026-10-30T23:00:00Z"
        };

    private static CharacterEquipmentLoadout Gear(params (string Slot,int Id,int? Enchant)[] entries)=>
        new(){Items=entries.Select(x=>new CharacterEquipmentItem {
            SlotKey=x.Slot,ItemId=x.Id,EnchantSpellId=x.Enchant
        }).ToList()};

    [Fact]
    public void Level60BaselineRejectsUnsourcedRaceMismatchesAndKeepsTalentsEquipment()
    {
        var existing=MageSimRequest.CreatePreset("fire").Character;
        existing.Race="human";
        existing.Equipment=Gear(("main-hand",19102,22749));
        var baseline=Baseline("human","mage");
        var generated=ForeverLevel60Baseline.Generate(existing,baseline);
        Assert.Equal(165m,generated.BaseStats.Get("intellect"));
        Assert.Equal(50m,generated.BaseStats.Get("strength"));
        Assert.Equal(1,generated.Equipment.Items.Count);
        Assert.Equal(existing.Talents.Selections.Count,generated.Talents.Selections.Count);
        Assert.Equal(200m,existing.BaseStats.Get("intellect"));
        Assert.Throws<ArgumentException>(()=>ForeverLevel60Baseline.Generate(
            existing,Baseline("orc","mage")));
        Assert.Throws<ArgumentException>(()=>ForeverLevel60Baseline.Generate(
            existing,Baseline("human","hunter")));
        var missing=Baseline("human","mage");
        missing.SourceReference="";
        Assert.Throws<ArgumentException>(()=>ForeverLevel60Baseline.Generate(existing,missing));
    }

    [Fact]
    public void BuilderProducesOneFullyAttributedLevel60SelectedProfile()
    {
        var profile=MageSimRequest.CreatePreset("fire").Character;
        profile.Race="human";profile.Equipment=Gear(("main-hand",19102,22749));
        var selection=new ForeverExternalModifierSelection {
            Buffs=["arcane-intellect"],Consumables=["flask-of-supreme-power",
                "nightfin-soup"],TargetDebuffs=["curse-of-elements"]
        };
        var controller=new ForeverCharacterController();
        var ok=Assert.IsType<OkObjectResult>(controller.GenerateLevel60(
            new ForeverCharacterResolutionRequest{
                Character=profile,Modifiers=selection,Baseline=Baseline("human","mage")
            }));
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        Assert.Equal("human",json.RootElement.GetProperty("race").GetString());
        Assert.True(json.RootElement.GetProperty("effectiveStats")
            .GetProperty("Values").GetProperty("spell-power").GetDecimal()>180m);
        var contributionJson=json.RootElement.GetProperty("contributions").ToString();
        Assert.Contains("buff:cp173-arcane-intellect",contributionJson);
        Assert.Contains("equipment",contributionJson);
        Assert.Equal(4,json.RootElement.GetProperty("selectedModifiers").GetArrayLength());
        Assert.Equal(165m,json.RootElement.GetProperty("readyProfile")
            .GetProperty("BaseStats").GetProperty("Values").GetProperty("intellect").GetDecimal());
    }

    [Fact]
    public void MageObservedBaselinePlusGearEnchantFlaskAndCurseChangesRealDamage()
    {
        MageSimRequest Build(bool all)
        {
            var r=MageSimRequest.CreatePreset("fire");
            r.DurationSeconds=75m;r.BaseSpellHitPercent=100m;
            r.SpellCritPercent=0m;r.TargetMaximumHealth=5000000m;
            r.Character.Race="human";r.Level60Baseline=Baseline("human","mage");
            if(all)
            {
                r.Character.Equipment=Gear(("main-hand",19102,22749));
                r.ExternalModifiers.Consumables.Add("flask-of-supreme-power");
                r.ExternalModifiers.Consumables.Add("nightfin-soup");
                r.ExternalModifiers.TargetDebuffs.Add("curse-of-elements");
                r.ExternalModifiers.Buffs.Add("arcane-intellect");
            }
            return r;
        }
        var before=new MageSimRunner().Run(Build(false));
        var after=new MageSimRunner().Run(Build(true));
        Assert.True(after.DamageDone>before.DamageDone);
        Assert.True(after.MaximumMana>before.MaximumMana);
        Assert.Equal(after.Dps,new MageSimRunner().Run(Build(true)).Dps);
        output.WriteLine($"CP173 generated Mage: {before.Dps:0.####} to {after.Dps:0.####} DPS; Mana {before.MaximumMana:0.##} to {after.MaximumMana:0.##}");
    }

    [Fact]
    public void WarlockObservedBaselineWithItemFlaskAndDemonRetainsPetAndTalents()
    {
        WarlockSimRequest Build(bool all)
        {
            var r=WarlockSimRequest.CreatePreset("demonology");
            r.DurationSeconds=80m;r.BaseSpellHitPercent=100m;
            r.TargetMaximumHealth=8000000m;
            r.PetType="imp";r.PrePullSacrificedDemon=null;
            r.Character.Race="orc";r.Level60Baseline=Baseline("orc","warlock");
            if(all)
            {
                r.Character.Equipment=Gear(("wrist",22511,1217189));
                r.ExternalModifiers.Consumables.Add("flask-of-supreme-power");
                r.ExternalModifiers.Buffs.Add("arcane-intellect");
                r.ExternalModifiers.TargetDebuffs.Add("curse-of-elements");
            }
            return r;
        }
        var before=new WarlockSimRunner().Run(Build(false));
        var after=new WarlockSimRunner().Run(Build(true));
        Assert.True(after.WarlockDamage>before.WarlockDamage);
        Assert.True(after.PetDamage>before.PetDamage);
        output.WriteLine($"CP173 generated Warlock: {before.Dps:0.####} to {after.Dps:0.####} DPS, demon {before.PetDamage:0.##} to {after.PetDamage:0.##}");
    }

    [Fact]
    public void HunterNakedObservationOverridesDefaultTuningAndRetainsEquipmentBuffsAndPet()
    {
        MarksmanshipHunterSimRequest Build(bool all)
        {
            var r=new MarksmanshipHunterSimRequest{
                Race="orc",DurationSeconds=100m,TargetMaximumHealth=8000000m,
                HasPet=true,PetFamily="cat",UseRapidFire=false,
                UseAimedShot=false,UseSniperShot=false,UseArcaneShot=false,
                UseMultiShot=false,UseSerpentSting=false
            };
            var b=Baseline("orc","hunter");
            b.ObservedUnbuffedMeleeAttackPower=450m;
            b.ObservedUnbuffedRangedAttackPower=760m;
            b.ObservedUnbuffedArmor=1020m;
            r.Level60Baseline=b;
            if(all)
            {
                r.Equipment=Gear(("ranged",20368,null),("trinket1",23041,null));
                r.ExternalModifiers.Consumables.Add("elixir-of-the-mongoose");
                r.ExternalModifiers.Buffs.Add("blessing-of-might");
                r.UseRacialActive=true;
            }
            return r;
        }
        var before=new MarksmanshipHunterSimRunner().Run(Build(false));
        var after=new MarksmanshipHunterSimRunner().Run(Build(true));
        Assert.True(after.HunterDamage>before.HunterDamage);
        Assert.True(after.PetDamage>before.PetDamage);
        Assert.Equal(1m,after.Run.CustomMetrics["cp173-racial-blood-fury-activations"]);
        output.WriteLine($"CP173 generated Hunter: {before.Dps:0.####} to {after.Dps:0.####} DPS, pet {before.PetDamage:0.##} to {after.PetDamage:0.##}");
    }

    [Fact]
    public void HunterMissingObservedAttackPowerFailsInsteadOfUsingHiddenPreviewStats()
    {
        var r=new MarksmanshipHunterSimRequest{Race="orc",Level60Baseline=Baseline("orc","hunter")};
        Assert.Throws<ArgumentException>(()=>new MarksmanshipHunterSimRunner().Run(r));
    }

    [Fact]
    public void SourcedExternalArmorDebuffImprovesHunterPhysicalDamageAndExpires()
    {
        MarksmanshipHunterSimRequest Build(bool curse)
        {
            var r=new MarksmanshipHunterSimRequest {
                DurationSeconds=60m,TargetMaximumHealth=9000000m,
                HasPet=false,UseAimedShot=false,UseSniperShot=false,
                UseArcaneShot=false,UseMultiShot=false,UseSerpentSting=false,
                UseRapidFire=false,CaptureTimeline=true
            };
            r.Tuning.TargetArmor=4500m;
            if(curse)r.ExternalModifiers.TargetDebuffs.Add("faerie-fire-rank4");
            return r;
        }
        var normal=new MarksmanshipHunterSimRunner().Run(Build(false));
        var reduced=new MarksmanshipHunterSimRunner().Run(Build(true));
        Assert.True(reduced.HunterDamage>normal.HunterDamage);
        Assert.Contains(reduced.Run.Timeline,x=>x.Type==CombatEventType.AuraRemoved &&
            x.TimeSeconds==40m && x.TargetActorKey=="target");
    }

    [Fact]
    public void FlaskAndWellFedSelectionGroupsAreExclusiveButElixirMayCoexist()
    {
        var entry=new ForeverExternalModifierSelection {
            Consumables=["flask-of-supreme-power","elixir-of-the-sages","nightfin-soup"]
        };
        Assert.Equal(3,ForeverExternalModifierCatalog.Resolve(entry,60).Count);
        entry.Consumables.Add("flask-of-the-titans");
        Assert.Throws<ArgumentException>(()=>ForeverExternalModifierCatalog.Resolve(entry,60));
        entry.Consumables.Remove("flask-of-the-titans");
        entry.Consumables.Add("grilled-squid");
        Assert.Throws<ArgumentException>(()=>ForeverExternalModifierCatalog.Resolve(entry,60));
        Assert.Throws<ArgumentException>(()=>ForeverExternalModifierCatalog.Resolve(
            new ForeverExternalModifierSelection{Consumables=["nightfin-soup"]},60,1000m));
    }
}
