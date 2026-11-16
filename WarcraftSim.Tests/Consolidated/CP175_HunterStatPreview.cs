using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Core.Characters.Talents;
using WarcraftSim.Core.Stats;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175HunterStatPreview
{
    private static ForeverLevel60BaselineObservation Baseline()=>new() {
        ClassKey="hunter",Race="orc",Level=60,Strength=50m,Agility=220m,
        Stamina=110m,Intellect=165m,Spirit=145m,
        ObservedUnbuffedMaximumHealth=3300m,ObservedUnbuffedMaximumMana=3500m,
        ObservedUnbuffedMeleeAttackPower=950m,ObservedUnbuffedRangedAttackPower=1000m,
        ObservedUnbuffedArmor=2400m,ClientBuild="1.60.1.70707",
        SourceKind="user-unverified-observation",VerificationStatus="unverified-user-supplied",
        SourceReference="synthetic-ci-preview-fixture-not-real-player",RecordedAtUtc="2026-11-09T00:00:00Z"
    };
    private static CharacterEquipmentLoadout Gear()=>new(){Items=[new(){
        SlotKey="head",ItemKey="synthetic-preview-gear",Name="Synthetic test fixture",
        Stats=new(){Values=new(){["agility"]=40m,["strength"]=10m,["attack-power"]=20m}}
    }]};
    private static ForeverCharacterResolutionRequest Input(int lightning,bool gear=false,int lethal=0,int surefooted=0)
    {
        var selections=new List<CharacterTalentSelection>();
        foreach(var (key,rank) in new[]{("lightning-reflexes",lightning),("lethal-attacks",lethal),("surefooted",surefooted)})
            if(rank>0)selections.Add(new(){TalentKey=key,Rank=rank});
        return new(){
            Character=new(){ClassKey="hunter",Race="orc",Level=60,
                Equipment=gear?Gear():new(),Talents=new(){Selections=selections}},
            Baseline=Baseline(),Modifiers=gear?new(){Consumables=["elixir-of-the-mongoose"]}:new()
        };
    }
    private static JsonElement Preview(ForeverCharacterResolutionRequest input,bool resolve=false)
    {
        var controller=new ForeverCharacterController();
        var result=Assert.IsType<OkObjectResult>(resolve?controller.Resolve(input):controller.GenerateLevel60(input));
        return JsonSerializer.SerializeToElement(result.Value);
    }
    private static decimal Stat(JsonElement result,string key)=>
        result.GetProperty("effectiveStats").GetProperty("Values").TryGetProperty(key,out var value)?value.GetDecimal():0m;

    [Theory]
    [InlineData(false,false)]
    [InlineData(false,true)]
    [InlineData(true,false)]
    [InlineData(true,true)]
    public void EndpointLightningRanksChangeActualDisplayStatsAndRemovalRestoresThem(bool resolve,bool gear)
    {
        var input=Input(0,gear);var before=Preview(input,resolve);
        input.Character.Talents.Selections.Add(new(){TalentKey="lightning-reflexes",Rank=5});
        var ranked=Preview(input,resolve);var replay=Preview(input,resolve);
        var raw=gear?285m:220m;var extra=raw*.15m;
        Assert.Equal(raw*1.15m,Stat(ranked,"agility"));
        Assert.Equal(Stat(before,"attack-power")+extra,Stat(ranked,"attack-power"));
        Assert.Equal(Stat(before,"ranged-attack-power")+extra*2m,Stat(ranked,"ranged-attack-power"));
        Assert.Equal(Stat(before,"armor")+extra*2m,Stat(ranked,"armor"));
        Assert.Equal(Stat(before,"attack-critical-chance-percent")+extra/52.9m,
            Stat(ranked,"attack-critical-chance-percent"));
        Assert.Equal(ranked.GetProperty("effectiveStats").GetRawText(),replay.GetProperty("effectiveStats").GetRawText());
        Assert.Contains("Other talent effects",ranked.GetProperty("statPreviewScope").GetString());
        Assert.Equal(extra,ranked.GetProperty("statPreviewAdjustments").GetProperty("agility").GetDecimal());
        input.Character.Talents.Selections.Clear();
        Assert.Equal(before.GetProperty("effectiveStats").GetRawText(),Preview(input,resolve).GetProperty("effectiveStats").GetRawText());
        Assert.Equal(220m,input.Baseline!.Agility);
        Assert.Empty(input.Character.BaseStats.Values);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReadyProfileRetainsUntalentedSourcesAndRoundtripsWithoutDoubleApplication(bool gear)
    {
        var input=Input(5,gear);var result=Preview(input);
        var ready=result.GetProperty("readyProfile").Deserialize<CharacterProfile>()!;
        Assert.Equal(220m,ready.BaseStats.Get("agility"));
        Assert.Equal(950m-50m-220m,ready.BaseStats.Get("attack-power"));
        Assert.Empty(ready.Buffs.Selections);
        var next=new ForeverCharacterResolutionRequest {Character=ready,Modifiers=input.Modifiers};
        var resolved=Preview(next,resolve:true);
        foreach(var key in new[]{"agility","attack-power","ranged-attack-power","armor"})
            Assert.Equal(Stat(result,key),Stat(resolved,key));
        Assert.Equal(220m,ready.BaseStats.Get("agility"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StaticLethalAndSurefootedAreDisplayedAndReversible(bool resolve)
    {
        var before=Preview(Input(0),resolve);
        var ranked=Preview(Input(0,lethal:5,surefooted:3),resolve);
        Assert.Equal(Stat(before,"attack-critical-chance-percent")+5m,Stat(ranked,"attack-critical-chance-percent"));
        Assert.Equal(Stat(before,"hit-chance-percent")+3m,Stat(ranked,"hit-chance-percent"));
        Assert.Equal(Stat(before,"agility"),Stat(ranked,"agility"));
        Assert.Equal(before.GetProperty("effectiveStats").GetRawText(),Preview(Input(0),resolve).GetProperty("effectiveStats").GetRawText());
    }

    [Theory]
    [InlineData("marksmanship")]
    [InlineData("beast-mastery")]
    public void DisplayRangedPowerMatchesRealCombatWithoutAddingNakedMeleeResidual(string preset)
    {
        var input=Input(5,gear:true);var preview=Preview(input);
        Assert.Equal(1235.5m,Stat(preview,"ranged-attack-power"));
        Assert.Equal(1087.75m,Stat(preview,"attack-power"));
        var request=Cp175HunterOffTreeAgilityParity.Marksmanship(5,crit:0m);
        request.Race="orc";request.Level60Baseline=Baseline();request.Equipment=Gear();request.ExternalModifiers=input.Modifiers;
        request.HasPet=false;request.DurationSeconds=1m;
        WarcraftSim.Core.Simulation.Engine.SimulationRunResult run;
        if(preset=="marksmanship")
        {
            var actual=new MarksmanshipHunterSimRunner().Run(request);
            Assert.Equal(Stat(preview,"ranged-attack-power"),actual.RangedAttackPower);
            run=actual.Run;
        }
        else
        {
            var bm=Cp175HunterOffTreeAgilityParity.BeastMastery(5,crit:0m);
            bm.Race="orc";bm.Level60Baseline=Baseline();bm.Equipment=Gear();bm.ExternalModifiers=input.Modifiers;
            bm.PetEnabled=false;bm.DurationSeconds=1m;
            run=new BeastMasteryHunterSimRunner().Run(bm).Run;
        }
        var shot=Assert.Single(run.Timeline,x=>x.Type==WarcraftSim.Core.Simulation.Engine.CombatEventType.Damage &&
            x.SourceActorKey=="hunter" && x.AbilityKey=="auto-shot");
        var expected=100m+Stat(preview,"ranged-attack-power")*2.8m/14m;
        Assert.Equal(expected*(shot.IsCritical?2m:1m),shot.Amount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CatalogEquipmentUsesOriginalSourceOnceWithoutOverrideOrResidualLeak(bool resolve)
    {
        var input=Input(5);
        input.Character.Equipment=new(){Items=[
            new(){SlotKey="ranged",ItemId=20368},
            new(){SlotKey="trinket1",ItemId=23041}
        ]};
        var equipment=ForeverSharedEquipmentResolver.Resolve(input.Character.Equipment,"hunter",60,"orc");
        var result=Preview(input,resolve);
        var agility=220m+equipment.RawStats.Get("agility");
        var expected=1000m+(agility-220m)*2m+equipment.RawStats.Get("ranged-attack-power")+
            equipment.RawStats.Get("attack-power")+agility*.15m*2m;
        Assert.Equal(expected,Stat(result,"ranged-attack-power"));
        Assert.Equal(agility*1.15m,Stat(result,"agility"));
        Assert.All(input.Character.Equipment.Items,item=>Assert.Empty(item.Stats.Values));
        Assert.True(equipment.RawStats.Get("attack-power")>0m);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AuthoredGenericBuffApIsIncludedOnceAndRemovalReversesAlongsideExternalSelections(bool resolve)
    {
        var input=Input(0);
        input.Modifiers=new(){Buffs=["battle-shout"],Consumables=["elixir-of-the-mongoose"]};
        var before=Preview(input,resolve);
        Assert.Equal(1050m,Stat(before,"ranged-attack-power")); // +25 Agility, no melee-only Shout.
        input.Character.Buffs.Selections.Add(new(){BuffKey="synthetic-generic-ap",Stats=new(){
            Values=new(){["attack-power"]=40m}
        }});
        var original=JsonSerializer.Serialize(input.Character);
        var added=Preview(input,resolve);
        Assert.Equal(1090m,Stat(added,"ranged-attack-power"));
        Assert.Equal(Stat(before,"attack-power")+40m,Stat(added,"attack-power"));
        Assert.Equal(added.GetProperty("effectiveStats").GetRawText(),Preview(input,resolve).GetProperty("effectiveStats").GetRawText());
        Assert.Equal(original,JsonSerializer.Serialize(input.Character));
        if(!resolve) {
            var ready=added.GetProperty("readyProfile").Deserialize<CharacterProfile>()!;
            Assert.Single(ready.Buffs.Selections);
            Assert.Equal(40m,ready.Buffs.Selections[0].Stats.Get("attack-power"));
            Assert.Equal(220m,ready.BaseStats.Get("agility"));
        }
        input.Character.Buffs.Selections[0].IsEnabled=false;
        Assert.Equal(before.GetProperty("effectiveStats").GetRawText(),Preview(input,resolve).GetProperty("effectiveStats").GetRawText());
        input.Character.Buffs.Selections.Clear();
        Assert.Equal(before.GetProperty("effectiveStats").GetRawText(),Preview(input,resolve).GetProperty("effectiveStats").GetRawText());
    }
    [Fact]
    public void NonHunterPreviewNeverAppliesHunterNamedSelectionsAndInvalidRanksFailClosed()
    {
        var stats=new StatCollection();stats.Set("agility",220m);
        var profile=new CharacterProfile {ClassKey="mage",Level=60,Talents=new(){Selections=[new(){TalentKey="lightning-reflexes",Rank=5}]}};
        Assert.Empty(HunterPrecombatStatPreview.Apply(stats,profile,new()));
        Assert.Equal(220m,stats.Get("agility"));
        var input=Input(6);
        Assert.IsType<BadRequestObjectResult>(new ForeverCharacterController().GenerateLevel60(input));
        input=Input(0,lethal:6);
        Assert.IsType<BadRequestObjectResult>(new ForeverCharacterController().Resolve(input));
    }
}
