using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;
using Xunit.Abstractions;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp173CharacterModifiers(ITestOutputHelper output)
{
    [Fact]
    public void ForeverRaceMatrixPreservesNewClassCombosAndFactionSpecificSkyborne()
    {
        Assert.Equal(10,ForeverPlayableRaceCatalog.Races.Count);
        ForeverPlayableRaceCatalog.Validate("orc","mage",60);
        ForeverPlayableRaceCatalog.Validate("undead","paladin",60);
        ForeverPlayableRaceCatalog.Validate("dwarf","shaman",60);
        ForeverPlayableRaceCatalog.Validate("high-order-skyborne","mage",1);
        ForeverPlayableRaceCatalog.Validate("windshaper-skyborne","shaman",60);
        Assert.Throws<ArgumentException>(()=>
            ForeverPlayableRaceCatalog.Validate("tauren","mage",60));
        Assert.Throws<ArgumentException>(()=>
            ForeverPlayableRaceCatalog.Validate("skyborne","hunter",60));
        Assert.Equal("alliance",ForeverRaceFactions.GetFaction("high-order-skyborne"));
        Assert.Equal("horde",ForeverRaceFactions.GetFaction("windshaper-skyborne"));
        var missing=JsonSerializer.Serialize(ForeverPlayableRaceCatalog.BaseStats("orc","mage",60));
        Assert.Contains("forever-primary-attributes-not-established",missing);
    }

    [Fact]
    public void DirectClientBaseManaByClassAndLevelIsPinnedButNoPrimaryStatsInvented()
    {
        Assert.Equal(1213m,ForeverClientBaseManaCatalog.GetBaseMana("mage",60));
        Assert.Equal(1373m,ForeverClientBaseManaCatalog.GetBaseMana("warlock",60));
        Assert.Equal(1720m,ForeverClientBaseManaCatalog.GetBaseMana("hunter",60));
        Assert.Equal(1376m,ForeverClientBaseManaCatalog.GetBaseMana("priest",60));
        Assert.Equal(0m,ForeverClientBaseManaCatalog.GetBaseMana("warrior",60));
        Assert.Equal(100m,ForeverClientBaseManaCatalog.GetBaseMana("mage",1));
        Assert.Equal("1.60.1.70707",ForeverClientBaseManaCatalog.SourceBuild);
        Assert.Equal(64,ForeverClientBaseManaCatalog.SourceSha256.Length);
        Assert.Throws<ArgumentException>(()=>ForeverClientBaseManaCatalog.GetBaseMana("mage",61));
        var builder=new ForeverCharacterController();
        var response=Assert.IsType<OkObjectResult>(builder.BaseStats("orc","mage",60));
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(response.Value));
        Assert.Equal(1213m,json.RootElement.GetProperty("baseMana").GetDecimal());
        Assert.Equal("1.60.1.70707",json.RootElement.GetProperty("baseManaSourceBuild").GetString());
        Assert.Equal(JsonValueKind.Null,json.RootElement.GetProperty("strength").ValueKind);
    }

    [Fact]
    public void ExternalModifiersAreOptInSourceIdentifiedAndDoNotDoubleStack()
    {
        var selection=new ForeverExternalModifierSelection{
            Buffs=["arcane-intellect","power-word-fortitude"],
            Consumables=["greater-arcane-elixir"]
        };
        var sources=ForeverExternalModifierCatalog.Resolve(selection,60);
        Assert.Equal(3,sources.Count);
        Assert.All(sources,x=>Assert.StartsWith("https://",x.SourceUrl));
        var raw=ForeverExternalModifierCatalog.SumSelfRawStats(selection,60);
        Assert.Equal(31m,raw["intellect"]);
        Assert.Equal(70m,raw["stamina"]);
        Assert.Equal(35m,raw["spell-power"]);
        Assert.Throws<ArgumentException>(()=>ForeverExternalModifierCatalog.Resolve(
            new ForeverExternalModifierSelection {
                Buffs=["battle-shout","blessing-of-might"]
            },60));
        Assert.Throws<ArgumentException>(()=>ForeverExternalModifierCatalog.Resolve(
            new ForeverExternalModifierSelection {Consumables=["greater-arcane-elixir",
                "elixir-of-the-mongoose"]},60));
        Assert.Throws<ArgumentException>(()=>ForeverExternalModifierCatalog.Resolve(
            new ForeverExternalModifierSelection {Buffs=["arcane-intellect"]},20));
        Assert.Throws<ArgumentException>(()=>ForeverExternalModifierCatalog.Resolve(
            new ForeverExternalModifierSelection {Buffs=["nonexistent-raid-buff"]},60));
    }

    [Fact]
    public void CharacterResolutionApiIncludesRaceEligibilityBuffSourcesAndExplicitBaseUncertainty()
    {
        var controller=new ForeverCharacterController();
        Assert.IsType<OkObjectResult>(controller.Races("mage"));
        Assert.IsType<BadRequestObjectResult>(controller.BaseStats("tauren","mage",60));
        var profile=MageSimRequest.CreatePreset("fire").Character;
        profile.Race="orc";
        var options=new ForeverExternalModifierSelection {
            Buffs=["arcane-intellect"],Consumables=["greater-arcane-elixir"]
        };
        var resolved=Assert.IsType<OkObjectResult>(controller.Resolve(
            new ForeverCharacterResolutionRequest{Character=profile,Modifiers=options}));
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(resolved.Value));
        Assert.Equal("horde",json.RootElement.GetProperty("faction").GetString());
        Assert.Equal(2,json.RootElement.GetProperty("selectedModifiers").GetArrayLength());
        Assert.Contains("buff:cp173-arcane-intellect",
            JsonSerializer.Serialize(json.RootElement.GetProperty("contributions")));
        var missing=MageSimRequest.CreatePreset("frost").Character;
        missing.BaseStats.Values.Clear();
        Assert.IsType<BadRequestObjectResult>(controller.Resolve(
            new ForeverCharacterResolutionRequest{Character=missing}));
    }

    [Fact]
    public void ExternalCasterBuffsChangeRealSpellDamageWithoutMutatingRequests()
    {
        MageSimRequest Mage(bool active)
        {
            var request=MageSimRequest.CreatePreset("fire");
            request.DurationSeconds=55m;request.TargetMaximumHealth=5000000m;
            request.BaseSpellHitPercent=100m;request.SpellCritPercent=0m;
            if(active)request.ExternalModifiers.Consumables.Add("greater-arcane-elixir");
            return request;
        }
        WarlockSimRequest Warlock(bool active)
        {
            var request=WarlockSimRequest.CreatePreset("destruction");
            request.DurationSeconds=55m;request.TargetMaximumHealth=5000000m;
            request.BaseSpellHitPercent=100m;request.SpellCritPercent=0m;
            request.EnablePet=false;request.PrePullSacrificedDemon=null;
            if(active)request.ExternalModifiers.Consumables.Add("greater-arcane-elixir");
            return request;
        }
        var m0=new MageSimRunner().Run(Mage(false));
        var m1=new MageSimRunner().Run(Mage(true));
        var w0=new WarlockSimRunner().Run(Warlock(false));
        var w1=new WarlockSimRunner().Run(Warlock(true));
        Assert.True(m1.DamageDone>m0.DamageDone);
        Assert.True(w1.WarlockDamage>w0.WarlockDamage);
        output.WriteLine($"CP173 Mage arcane elixir DPS {m0.Dps:0.####} to {m1.Dps:0.####}");
        output.WriteLine($"CP173 Warlock arcane elixir DPS {w0.Dps:0.####} to {w1.Dps:0.####}");
        Assert.Equal(m0.Dps,new MageSimRunner().Run(Mage(false)).Dps);
    }

    [Fact]
    public void ExternalCurseTargetsEnemyAffectsDamageAndExpiresAtActualFiveMinutes()
    {
        MageSimRequest Config(bool enabled,decimal duration)
        {
            var request=MageSimRequest.CreatePreset("fire");
            request.DurationSeconds=duration;
            request.TargetMaximumHealth=10000000m;
            request.BaseSpellHitPercent=100m;
            request.SpellCritPercent=0m;
            request.CaptureTimeline=true;
            if(enabled)request.ExternalModifiers.TargetDebuffs.Add("curse-of-elements");
            return request;
        }
        var baseRun=new MageSimRunner().Run(Config(false,70m));
        var cursed=new MageSimRunner().Run(Config(true,70m));
        Assert.True(cursed.DamageDone>baseRun.DamageDone);
        var longer=new MageSimRunner().Run(Config(true,310m));
        Assert.Contains(longer.Run.Timeline,x=>x.Type==CombatEventType.AuraApplied &&
            x.EffectKey=="cp173-external" && x.TimeSeconds==0m);
        Assert.Contains(longer.Run.Timeline,x=>x.Type==CombatEventType.AuraRemoved &&
            x.AuraInstanceId.HasValue && x.TimeSeconds==300m);
        output.WriteLine($"CP173 Mage external curse DPS {baseRun.Dps:0.####} to {cursed.Dps:0.####}");
    }

    [Fact]
    public void WarlockCannotStackExternalCurseWithOwnSelectedCurse()
    {
        var request=WarlockSimRequest.CreatePreset("affliction");
        request.UseCurseOfTheElements=true;
        request.ExternalModifiers.TargetDebuffs.Add("curse-of-elements");
        Assert.Throws<ArgumentException>(()=>new WarlockSimRunner().Run(request));
    }

    [Fact]
    public void MongooseIncreasesRealHunterDamageAndInheritedPetOutput()
    {
        MarksmanshipHunterSimRequest Config(bool buff)
        {
            var request=new MarksmanshipHunterSimRequest{
                DurationSeconds=120m,TargetMaximumHealth=10000000m,
                HasPet=true,PetFamily="cat",
                UseRapidFire=false,UseAimedShot=false,UseSniperShot=false,
                UseMultiShot=false,UseArcaneShot=false,UseSerpentSting=false
            };
            request.Tuning.HitChancePercent=100m;
            request.Tuning.CriticalChancePercent=0m;
            if(buff)request.ExternalModifiers.Consumables.Add("elixir-of-the-mongoose");
            return request;
        }
        var without=new MarksmanshipHunterSimRunner().Run(Config(false));
        var with=new MarksmanshipHunterSimRunner().Run(Config(true));
        Assert.True(with.HunterDamage>without.HunterDamage);
        Assert.True(with.PetDamage>without.PetDamage);
        output.WriteLine($"CP173 Hunter Mongoose owner {without.HunterDamage:0.##} to {with.HunterDamage:0.##}; pet {without.PetDamage:0.##} to {with.PetDamage:0.##}");
    }

    [Fact]
    public void OrcBloodFuryIsRaceLockedAndOnUseCooldownModifiesActualMageSpells()
    {
        MageSimRequest Config(bool activate)
        {
            var request=MageSimRequest.CreatePreset("fire");
            request.Character.Race="orc";request.UseRacialActive=activate;
            request.DurationSeconds=47m;request.TargetMaximumHealth=5000000m;
            request.BaseSpellHitPercent=100m;request.SpellCritPercent=0m;
            return request;
        }
        var without=new MageSimRunner().Run(Config(false));
        var active=new MageSimRunner().Run(Config(true));
        Assert.True(active.DamageDone>without.DamageDone);
        Assert.Equal(1m,active.Run.CustomMetrics["cp173-racial-blood-fury-activations"]);
        Assert.Equal(15m,active.Run.CustomMetrics["cp173-racial-blood-fury-uptime-seconds"]);
        var wrong=MageSimRequest.CreatePreset("fire");
        wrong.Character.Race="human";wrong.UseRacialActive=true;
        Assert.Throws<ArgumentException>(()=>new MageSimRunner().Run(wrong));
        output.WriteLine($"CP173 Orc Mage Blood Fury DPS {without.Dps:0.####} to {active.Dps:0.####}");
    }

    [Fact]
    public void GnomeManaAndTaurenHealthAreCapPercentNotClassicPrimaryStatGains()
    {
        var gnome=MageSimRequest.CreatePreset("fire");gnome.DurationSeconds=2m;
        gnome.Character.Race="gnome";
        var baseline=new MageSimRunner().Run(MageSimRequest.CreatePreset("fire"));
        var result=new MageSimRunner().Run(gnome);
        Assert.True(result.Run.Summary.ActorSummaries["mage"].StartingResources["mana"] >
            baseline.Run.Summary.ActorSummaries["mage"].StartingResources["mana"]);
        var stats=ForeverRacialPassives.StaticStats("gnome");
        Assert.False(stats.ContainsKey("intellect"));
    }
}
