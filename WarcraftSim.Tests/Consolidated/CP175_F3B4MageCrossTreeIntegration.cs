using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Mage;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>Cross-tree F3B4: real legal rank allocations, combat effects,
/// reverse eligibility and exact-seed receipts; no preset-invented talent.</summary>
public sealed class Cp175F3B4MageCrossTreeIntegration
{
    private static readonly Dictionary<string,int> Arc36=new(StringComparer.OrdinalIgnoreCase){
        ["improved-channeling"]=5,["arcane-impact"]=3,["arcane-concentration"]=5,
        ["arcane-meditation"]=3,["arcane-mind"]=5,["arcane-instability"]=3,
        ["arcane-blast"]=1,["missile-barrage"]=2,["arcane-wrath"]=1,
        ["presence-of-mind"]=1,["arcane-power"]=1,["arcane-subtlety"]=1,
        ["magic-absorption"]=2,["arcane-resilience"]=3
    };
    private static readonly Dictionary<string,int> Arc16=new(StringComparer.OrdinalIgnoreCase){
        ["improved-channeling"]=5,["arcane-concentration"]=5,
        ["arcane-impact"]=3,["missile-barrage"]=2,["arcane-blast"]=1
    };
    private static readonly Dictionary<string,int> Fire15=new(StringComparer.OrdinalIgnoreCase){
        ["accelerant"]=5,["incineration"]=5,["ignite"]=5
    };
    private static readonly Dictionary<string,int> Fire20=new(StringComparer.OrdinalIgnoreCase){
        ["accelerant"]=5,["incineration"]=5,["ignite"]=5,["impact"]=3,
        ["pyroblast"]=1,["improved-fire-blast"]=1
    };
    private static readonly Dictionary<string,int> Fire36=new(StringComparer.OrdinalIgnoreCase){
        ["accelerant"]=5,["ignite"]=5,["incineration"]=4,["pyroblast"]=1,
        ["heating-up"]=1,["fire-power"]=3,["critical-mass"]=3,
        ["combustion"]=1,["impact"]=3,["improved-flamestrike"]=3,
        ["improved-scorch"]=3,["master-of-elements"]=3,["blast-wave"]=1
    };
    private static readonly Dictionary<string,int> Frost15=new(StringComparer.OrdinalIgnoreCase){
        ["improved-frostbolt"]=5,["precision"]=3,["ice-shards"]=5,
        ["frostbite"]=1,["ice-lance"]=1
    };

    private static MageSimRequest Mixed(string kind)
    {
        var (spec,arc,fire,frost,actions)=kind switch {
            "arcane-fire"=>("arcane",Arc36,Fire15,new Dictionary<string,int>(),new[]{"arcane-power","presence-of-mind","fireball"}),
            "arcane-frost"=>("frost",Arc36,new Dictionary<string,int>(),Frost15,new[]{"arcane-power","ice-lance","frostbolt"}),
            "fire-frost"=>("fire",new Dictionary<string,int>(),Fire36,Frost15,new[]{"combustion","frostfire-bolt"}),
            "three-trees"=>("arcane",Arc16,Fire20,Frost15,new[]{"frostfire-bolt"}),
            _=>throw new ArgumentException(kind)
        };
        var r=MageSimRequest.CreatePreset(spec);
        var ranks=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        foreach(var part in new[]{arc,fire,frost})
            foreach(var (key,value) in part)ranks.Add(key,value);
        r.Talents=new ForeverMageTalentBuild{
            ArcanePointsSpent=arc.Values.Sum(),FirePointsSpent=fire.Values.Sum(),
            FrostPointsSpent=frost.Values.Sum(),Ranks=ranks
        };
        r.DurationSeconds=26m;r.TargetMaximumHealth=1_000_000m;
        r.BaseSpellHitPercent=100m;r.SpellCritPercent=30m;
        r.UseArcanePower=ranks.ContainsKey("arcane-power");
        r.UsePresenceOfMind=ranks.ContainsKey("presence-of-mind");
        r.UseEvocation=false;r.UseMajorManaPotion=false;
        r.CombatItems.ManaRubyCharges=0;
        r.Tuning.ManaRegenPerSecond=200m;
        r.CustomPriority=actions.ToList();
        return r;
    }

    [Theory]
    [InlineData("arcane-fire","fireball")]
    [InlineData("arcane-frost","frostbolt")]
    [InlineData("fire-frost","frostfire-bolt")]
    [InlineData("three-trees","frostfire-bolt")]
    public void FourActualFiftyOnePointHybridsExecuteAndReplay(string kind,string spell)
    {
        var request=Mixed(kind);
        Assert.Equal(51,request.Talents.Ranks.Values.Sum());
        request.Talents.Validate(request.Specialization);
        var payload=JsonSerializer.SerializeToElement(new {
            ranks=request.Talents.Ranks,
            arcanePointsSpent=request.Talents.ArcanePointsSpent,
            firePointsSpent=request.Talents.FirePointsSpent,
            frostPointsSpent=request.Talents.FrostPointsSpent
        });
        var itemized=new ForeverSoloTalentController().ValidateItemized(
            "mage",request.Specialization,payload);
        Assert.IsType<OkObjectResult>(itemized);
        var sim=new MageSimRunner().Run(request);
        var replay=new MageSimRunner().Run(Mixed(kind));
        Assert.True(sim.DamageDone>0m);
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey==spell&&e.Amount is >0m);
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.ResourceKey=="mana"&&e.Amount is <0m);
        Assert.Equal(sim.DamageDone,replay.DamageDone);
        Assert.Equal(sim.DamageByAbility.OrderBy(x=>x.Key),
            replay.DamageByAbility.OrderBy(x=>x.Key));
        if(kind=="arcane-fire" || kind=="arcane-frost")
            Assert.Contains(sim.Run.Timeline,e=>e.AbilityKey=="arcane-power"&&
                e.Type==CombatEventType.AbilityCastCompleted);
        if(kind=="fire-frost")
            Assert.Contains(sim.Run.Timeline,e=>e.AbilityKey=="combustion"&&
                e.Type==CombatEventType.AbilityCastCompleted);
    }

    [Fact]
    public void FireFrostFrostfireCritUsesIceShardsAndTriggersIgniteNotTwoSchoolCopies()
    {
        MageSimRequest Build(bool shards,bool ignite)
        {
            var r=Mixed("fire-frost");
            r.DurationSeconds=5m;r.SpellCritPercent=100m;
            r.CustomPriority=["frostfire-bolt"];
            r.Talents.Ranks["ice-shards"]=shards?5:0;
            r.Talents.Ranks["ignite"]=ignite?5:0;
            r.UseIgnite=ignite;
            return r;
        }
        var ordinary=new MageSimRunner().Run(Build(false,false));
        var combined=new MageSimRunner().Run(Build(true,true));
        decimal First(MageSimResponse sim)=>sim.Run.Timeline.First(e=>
            e.Type==CombatEventType.Damage && e.AbilityKey=="frostfire-bolt" &&
            !e.IsPeriodic&&e.IsCritical).Amount!.Value;
        Assert.Equal(First(ordinary)*2m/1.5m,First(combined));
        Assert.Contains(combined.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="mage-ignite");
        Assert.Single(combined.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="frostfire-bolt"&&!e.IsPeriodic);
    }

    [Theory]
    [InlineData("arcane-fire","arcane-power")]
    [InlineData("arcane-frost","arcane-power")]
    [InlineData("fire-frost","combustion")]
    [InlineData("three-trees","arcane-blast")]
    public void RemovingAnActiveTalentRejectsStaleCustomPriority(string kind,string learned)
    {
        var r=Mixed(kind);
        if(!r.Talents.Ranks.ContainsKey(learned)){
            r.CustomPriority=[learned];
            Assert.Throws<ArgumentException>(()=>MageAdaptiveRotation.Build(r));
            return;
        }
        r.Talents.Ranks.Remove(learned);
        r.CustomPriority=[learned];
        Assert.Throws<ArgumentException>(()=>MageAdaptiveRotation.Build(r));
        Assert.Throws<ArgumentException>(()=>new MageSimRunner().Run(r));
    }


    [Fact]
    public void ArcaneMindStatPreviewAndLiveResilienceAgreeWithoutDoubleApplication()
    {
        var profile=new WarcraftSim.Core.Characters.CharacterProfile{
            ClassKey="mage",Level=60,
            Talents=new WarcraftSim.Core.Characters.Talents.CharacterTalentLoadout{
                Selections=[
                    new WarcraftSim.Core.Characters.Talents.CharacterTalentSelection {
                        TalentKey="arcane-mind",Rank=5
                    },
                    new WarcraftSim.Core.Characters.Talents.CharacterTalentSelection {
                        TalentKey="arcane-resilience",Rank=1
                    }
                ]
            }
        };
        var preview=new WarcraftSim.Core.Stats.StatCollection{Values=new(StringComparer.OrdinalIgnoreCase){
            ["intellect"]=200m,["armor"]=50m
        }};
        var gained=MageArcaneResilienceArmor.ApplyFromProfile(preview,profile);
        Assert.Equal(220m,preview.Get("intellect"));
        Assert.Equal(55m,gained);
        Assert.Equal(105m,preview.Get("armor"));
        var live=new WarcraftSim.Core.Stats.StatCollection{Values=new(StringComparer.OrdinalIgnoreCase){
            ["intellect"]=200m,["armor"]=50m
        }};
        Assert.Equal(gained,MageArcaneResilienceArmor.Apply(live,1,5));
        Assert.Equal(preview.Get("armor"),live.Get("armor"));
        Assert.Equal(200m,live.Get("intellect"));
        profile.Talents.Selections.RemoveAt(0);
        var removed=new WarcraftSim.Core.Stats.StatCollection{Values=new(StringComparer.OrdinalIgnoreCase){
            ["intellect"]=200m,["armor"]=50m
        }};
        Assert.Equal(50m,MageArcaneResilienceArmor.ApplyFromProfile(removed,profile));
        Assert.Equal(200m,removed.Get("intellect"));
        Assert.Equal(100m,removed.Get("armor"));
    }

    [Fact]
    public void OffTreeAutomaticPriorityUsesLearnedReactiveSpellsNotUnlearnedOnes()
    {
        var arcaneFrost=Mixed("arcane-frost");
        arcaneFrost.Specialization="fire";
        arcaneFrost.CustomPriority=[];
        arcaneFrost.UseFingersOfFrost=true;
        arcaneFrost.Talents.Ranks["fingers-of-frost"]=2;
        Assert.Contains(MageAdaptiveRotation.Build(arcaneFrost).Entries,e=>e.AbilityKey=="ice-lance");
        arcaneFrost.Talents.Ranks.Remove("fingers-of-frost");
        Assert.DoesNotContain(MageAdaptiveRotation.Build(arcaneFrost).Entries,e=>e.AbilityKey=="ice-lance");

        var fireFrost=Mixed("fire-frost");
        fireFrost.Specialization="arcane";
        fireFrost.CustomPriority=[];
        fireFrost.UseCombustion=true;
        Assert.Contains(MageAdaptiveRotation.Build(fireFrost).Entries,e=>e.AbilityKey=="combustion");
        fireFrost.Talents.Ranks.Remove("combustion");
        Assert.DoesNotContain(MageAdaptiveRotation.Build(fireFrost).Entries,e=>e.AbilityKey=="combustion");
    }

    [Fact]
    public void ColdSnapResetsIceLancePersonalAndSharedLockout()
    {
        var r=MageSimRequest.CreatePreset("frost");
        r.DurationSeconds=7.5m;r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;r.UseFingersOfFrost=false;
        r.UseEvocation=false;r.UseMajorManaPotion=false;
        r.CombatItems.ManaRubyCharges=0;
        r.CustomPriority=["ice-lance","cold-snap","frostbolt"];
        var sim=new MageSimRunner().Run(r);
        var casts=sim.Run.Timeline.Where(e=>e.Type==CombatEventType.AbilityCastStarted &&
            e.AbilityKey=="ice-lance").Select(e=>e.TimeSeconds).ToArray();
        Assert.True(casts.Length>=2,
            "Cold Snap must clear both Ice Lance and its shared Fire Blast group.");
        Assert.True(casts[1]-casts[0]<8m);
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey=="cold-snap");
    }

    [Fact]
    public void TalentRemovalDropsPassiveDamageAndProcEffect()
    {
        var selected=Mixed("three-trees");
        selected.CustomPriority=["frostfire-bolt"];
        selected.DurationSeconds=11m;
        selected.SpellCritPercent=100m;
        var ignored=Mixed("three-trees");
        ignored.DurationSeconds=11m;ignored.SpellCritPercent=100m;
        ignored.Talents.Ranks.Remove("ignite");ignored.UseIgnite=false;
        var withIgnite=new MageSimRunner().Run(selected);
        var without=new MageSimRunner().Run(ignored);
        Assert.Contains(withIgnite.Run.Timeline,e=>e.AbilityKey=="mage-ignite"&&
            e.Type==CombatEventType.Damage&&e.IsPeriodic&&e.Amount>0m);
        Assert.DoesNotContain(without.Run.Timeline,e=>e.AbilityKey=="mage-ignite"&&
            e.Type==CombatEventType.Damage);
    }
}
