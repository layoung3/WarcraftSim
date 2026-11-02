using System;
using System.Collections.Generic;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;
using Xunit.Abstractions;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175F2B2ModifierExpansion(ITestOutputHelper output)
{
    private static readonly string[] Added =
    [
        "poached-sunscale-salmon","mightfish-steak","baked-salmon",
        "spider-sausage","savory-turtle-stew","steaming-stag-steak",
        "swiftstrike-steak","prehistoric-pulled-raptor","wicked-smoothie",
        "sages-tea","sunny-tea","elixir-of-cunning","elixir-of-ferocity",
        "elixir-of-greater-fortitude","elixir-of-greater-spirit",
        "elixir-of-the-grizzly","elixir-of-the-owl","elixir-of-the-phalanx",
        "greater-mageblood-elixir","elixir-of-brute-force",
        "elixir-of-greater-intellect","elixir-of-greater-defense",
        "distilled-firewater","flask-of-chromatic-resistance",
        "mark-of-the-wild","curse-of-recklessness"
    ];

    [Fact]
    public void NewSourceBackedModifiersAreUniqueSelectableAndProvisional()
    {
        Assert.Equal(26,Added.Length);
        var list=ForeverExternalModifierCatalog.Entries;
        Assert.Equal(list.Count,list.Select(x=>x.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        foreach(var key in Added)
        {
            var entry=Assert.Single(list,x=>x.Key==key);
            Assert.Equal("provisional",entry.DataConfidence);
            Assert.StartsWith("https://",entry.SourceUrl);
            Assert.StartsWith("1.60.1.",entry.SourceBuild);
            Assert.NotEmpty(entry.Stats);
            Assert.True(entry.DurationSeconds>=120m);
            var selection=new ForeverExternalModifierSelection();
            if(entry.Category=="consumable")selection.Consumables.Add(key);
            else if(entry.Category=="external-buff")selection.Buffs.Add(key);
            else selection.TargetDebuffs.Add(key);
            Assert.Single(ForeverExternalModifierCatalog.Resolve(selection,60,90m));
        }
        Assert.Equal("partially-implemented",
            ForeverExternalModifierCatalog.Find("curse-of-recklessness").ImplementationStatus);
        Assert.Equal("fully-implemented",
            ForeverExternalModifierCatalog.Find("baked-salmon").ImplementationStatus);
    }

    [Fact]
    public void NewFoodsElixirsFlasksAndCursesRespectKnownFamilies()
    {
        void Reject(params string[] names) =>
            Assert.Throws<ArgumentException>(()=>ForeverExternalModifierCatalog.Resolve(
                new ForeverExternalModifierSelection {Consumables=[..names]},60,90m));
        Reject("baked-salmon","swiftstrike-steak");
        Reject("flask-of-chromatic-resistance","flask-of-supreme-power");
        Reject("elixir-of-cunning","elixir-of-the-mongoose");
        Reject("elixir-of-greater-fortitude","elixir-of-the-phalanx");
        Assert.Throws<ArgumentException>(()=>ForeverExternalModifierCatalog.Resolve(
            new ForeverExternalModifierSelection {TargetDebuffs=["curse-of-recklessness","curse-of-elements"]},60));
        var selected=new ForeverExternalModifierSelection {
            Consumables=["baked-salmon","greater-mageblood-elixir","flask-of-the-titans"]
        };
        var sums=ForeverExternalModifierCatalog.SumSelfRawStats(selected,60,90m);
        Assert.Equal(28m,sums["spell-power"]);
        Assert.Equal(20m,sums["mana-per-5"]);
        Assert.Equal(1200m,sums["health"]);
        Assert.Equal(3,ForeverExternalModifierCatalog.Resolve(selected,60,90m).Count);
    }

    [Fact]
    public void NewFoodProvidesActualMageAndWarlockSpellDamage()
    {
        MageSimRequest Mage(bool active)
        {
            var r=MageSimRequest.CreatePreset("fire");
            r.DurationSeconds=55m;r.TargetMaximumHealth=5000000m;
            r.BaseSpellHitPercent=100m;r.SpellCritPercent=0m;
            if(active)r.ExternalModifiers.Consumables.Add("baked-salmon");
            return r;
        }
        WarlockSimRequest Warlock(bool active)
        {
            var r=WarlockSimRequest.CreatePreset("destruction");
            r.DurationSeconds=55m;r.TargetMaximumHealth=5000000m;
            r.BaseSpellHitPercent=100m;r.SpellCritPercent=0m;
            r.EnablePet=false;
            if(active)r.ExternalModifiers.Consumables.Add("baked-salmon");
            return r;
        }
        var mb=new MageSimRunner().Run(Mage(false));
        var mm=new MageSimRunner().Run(Mage(true));
        var wb=new WarlockSimRunner().Run(Warlock(false));
        var wm=new WarlockSimRunner().Run(Warlock(true));
        Assert.Equal(mb.SpellPower+28m,mm.SpellPower);
        Assert.True(mm.DamageDone>mb.DamageDone);
        Assert.Equal(wb.SpellPower+28m,wm.SpellPower);
        Assert.True(wm.WarlockDamage>wb.WarlockDamage);
        Assert.Equal(mm.DamageDone,new MageSimRunner().Run(Mage(true)).DamageDone);
        Assert.Equal(wm.DamageDone,new WarlockSimRunner().Run(Warlock(true)).DamageDone);
        output.WriteLine($"F2B2 Mage baked salmon DPS {mb.Dps:0.####} -> {mm.Dps:0.####}");
        output.WriteLine($"F2B2 Warlock baked salmon DPS {wb.Dps:0.####} -> {wm.Dps:0.####}");
    }

    [Fact]
    public void NewAgilityFoodImprovesActualHunterAndWarriorApFoodIncreasesPhysicalStats()
    {
        MarksmanshipHunterSimRequest Hunter(bool active)
        {
            var r=new MarksmanshipHunterSimRequest {
                DurationSeconds=60m,TargetMaximumHealth=5000000m,
                HasPet=false,UseRapidFire=false,UseAimedShot=false,
                UseSniperShot=false,UseMultiShot=false,UseArcaneShot=false,
                UseSerpentSting=false
            };
            r.Tuning.HitChancePercent=100m;r.Tuning.CriticalChancePercent=0m;
            if(active)r.ExternalModifiers.Consumables.Add("swiftstrike-steak");
            return r;
        }
        var hb=new MarksmanshipHunterSimRunner().Run(Hunter(false));
        var hm=new MarksmanshipHunterSimRunner().Run(Hunter(true));
        Assert.True(hm.HunterDamage>hb.HunterDamage);
        Assert.Equal(hm.DamageDone,new MarksmanshipHunterSimRunner().Run(Hunter(true)).DamageDone);
        var warrior=new WarriorSimRequest{Level=60,Iterations=1,DurationSeconds=45m,
            Seed=1752202,TargetArmor=3500m,CaptureRepresentativeTimeline=true};
        var wb=new ForeverWarriorSimRunner().Run(warrior);
        warrior.ExternalModifiers.Consumables.Add("poached-sunscale-salmon");
        var wm=new ForeverWarriorSimRunner().Run(warrior);
        Assert.True(wm.EffectiveCharacterStats["attack-power"]>wb.EffectiveCharacterStats["attack-power"]);
        Assert.Equal(wm.MeanDps,new ForeverWarriorSimRunner().Run(warrior).MeanDps);
        output.WriteLine($"F2B2 Hunter swiftstrike DPS {hb.Dps:0.####} -> {hm.Dps:0.####}");
        output.WriteLine($"F2B2 Warrior poached salmon DPS {wb.MeanDps:0.####} -> {wm.MeanDps:0.####}");
    }

    [Fact]
    public void NewExternalBuffAndPartialCursePreserveSupportedRawValues()
    {
        var m=ForeverExternalModifierCatalog.SumSelfRawStats(
            new ForeverExternalModifierSelection{Buffs=["mark-of-the-wild"]},60,90m);
        Assert.Equal(16m,m["intellect"]);
        Assert.Equal(385m,m["armor"]);
        Assert.Equal(27m,m["fire-resistance"]);
        var curse=ForeverExternalModifierCatalog.Find("curse-of-recklessness");
        Assert.Equal(-640m,curse.Stats["armor"]);
        Assert.DoesNotContain("melee-attack-power",curse.Stats.Keys);
        var w=new WarriorSimRequest{Level=60,Iterations=1,DurationSeconds=60m,
            Seed=1752202,TargetArmor=3500m,CaptureRepresentativeTimeline=true};
        w.ExternalModifiers.TargetDebuffs.Add("curse-of-recklessness");
        var result=new ForeverWarriorSimRunner().Run(w);
        Assert.Equal(2860m,result.EffectiveBossArmor);
        Assert.Contains(result.Batch.RepresentativeRun.Timeline,e=>
            e.AbilityKey=="curse-of-recklessness"&&e.Type==CombatEventType.AuraApplied);
    }
}
