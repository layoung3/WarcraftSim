using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175ManualProtectionAndDemonSelection
{
    private static WarriorSimRequest ManualProtection(decimal blockValue=60m) => new()
    {
        Preset="protection",TalentMode="tree",StatSourceMode="race-base",Race="human",
        TankMode=true,EnableBossAutoAttacks=true,ShieldEquipped=true,IsTwoHanded=false,
        TankStatMode="manual",PlayerBlockPercent=5m,PlayerBlockValue=blockValue,
        MainHandMinimumDamage=80m,MainHandMaximumDamage=120m,MainHandSpeedSeconds=2m,
        StartingStance="defensive",StartingRage=100m,DurationSeconds=20m,Iterations=1,
        Seed=175901,CaptureRepresentativeTimeline=true,RotationMode="custom",
        CustomRotation=[new WarriorRotationRule{Action="shield-slam",Enabled=true}],
        TalentRanks=new Dictionary<string,int>
        {
            ["improved-bloodrage"]=2,["shield-specialization"]=5,["anticipation"]=5,
            ["improved-revenge"]=3,["improved-thunder-clap"]=3,["last-stand"]=1,
            ["master-of-defense"]=2,["focused-rage"]=3,["concussion-blow"]=1,
            ["bastion"]=5,["shield-slam"]=1
        }
    };

    private static WarriorSimRequest Prepare(WarriorSimRequest request)
    {
        var result=Assert.IsType<OkObjectResult>(new ForeverWarriorBuilderController().Prepare(new(){Warrior=request}));
        var options=new JsonSerializerOptions(JsonSerializerDefaults.Web);
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(result.Value,options));
        Assert.Empty(json.RootElement.GetProperty("normalizedLoadout").GetProperty("items").EnumerateArray());
        return json.RootElement.GetProperty("warrior").Deserialize<WarriorSimRequest>(options)!;
    }

    [Fact]
    public void DefaultsDoNotPretendARealShieldOrTankPermissionExists()
    {
        var defaults=Assert.IsType<WarriorSimRequest>(Assert.IsType<OkObjectResult>(
            new ForeverWarriorBuilderController().Defaults("protection")).Value);
        Assert.False(defaults.ShieldEquipped);Assert.False(defaults.TankMode);
        Assert.False(defaults.EnableBossAutoAttacks);Assert.True(defaults.IsTwoHanded);
    }

    [Fact]
    public void ExplicitManualProtectionPreparesRunsAndReplaysShieldSlamWithoutInventedItems()
    {
        var prepared=Prepare(ManualProtection());
        Assert.Equal("gear-bonuses",prepared.EquipmentMode);Assert.Empty(prepared.Gear);
        Assert.True(prepared.ShieldEquipped);Assert.False(prepared.IsTwoHanded);Assert.False(prepared.DualWield);
        var runner=new ForeverWarriorSimRunner();var run=runner.Run(prepared);
        Assert.Contains(run.Batch.RepresentativeRun.Timeline,x=>x.AbilityKey=="shield-slam"&&x.Type==CombatEventType.Damage&&x.Amount>0m);
        Assert.Equal(run.MeanDps,runner.Run(prepared).MeanDps);
        var moreBlock=runner.Run(Prepare(ManualProtection(160m)));
        decimal ShieldDamage(WarriorSimResponse response)=>response.Batch.RepresentativeRun.Timeline
            .Where(x=>x.AbilityKey=="shield-slam"&&x.Type==CombatEventType.Damage).Sum(x=>x.Amount??0m);
        Assert.True(ShieldDamage(moreBlock)>ShieldDamage(run));
    }

    [Theory]
    [InlineData("manual")][InlineData("derived")]
    public void PreparedDefensePreviewMatchesActualRunAndPreservesRawProfile(string mode)
    {
        var input=ManualProtection();input.TankStatMode=mode;input.ShieldIntrinsicBlockValue=40m;
        // Deliberately stale legacy ranks cannot affect the ranked-tree preview.
        input.DeepWoundsRank=3;input.ImprovedRendRank=3;
        var result=Assert.IsType<OkObjectResult>(new ForeverWarriorBuilderController().Prepare(new(){Warrior=input}));
        var options=new JsonSerializerOptions(JsonSerializerDefaults.Web);
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(result.Value,options));
        var root=json.RootElement;var prepared=root.GetProperty("warrior").Deserialize<WarriorSimRequest>(options)!;
        var actual=new ForeverWarriorSimRunner().Run(prepared).EffectiveCharacterStats;
        foreach(var key in new[]{"dodge-chance-percent","parry-chance-percent","block-chance-percent","block-value","defense"})
            Assert.Equal(actual[key],root.GetProperty("effectiveStats").GetProperty(key).GetDecimal());
        Assert.Contains(mode=="manual"?"manual dodge/parry/block":"engine-derived defenses",root.GetProperty("statPreviewScope").GetString());
        if(mode=="manual")
        {
            Assert.Equal(60m,root.GetProperty("effectiveStats").GetProperty("block-value").GetDecimal());
            Assert.Equal(10m,root.GetProperty("effectiveStats").GetProperty("block-chance-percent").GetDecimal());
            Assert.NotEqual(60m,root.GetProperty("profileStats").GetProperty("block-value").GetDecimal());
        }
        Assert.NotEmpty(root.GetProperty("contributions").EnumerateArray());
    }

    [Theory]
    [InlineData("tank")][InlineData("incoming")][InlineData("shield")][InlineData("two-hand")][InlineData("dual-wield")]
    public void ManualProtectionNeverBypassesRequiredTankAndWeaponConditions(string invalid)
    {
        var r=ManualProtection();
        if(invalid=="tank")r.TankMode=false;
        if(invalid=="incoming")r.EnableBossAutoAttacks=false;
        if(invalid=="shield")r.ShieldEquipped=false;
        if(invalid=="two-hand")r.IsTwoHanded=true;
        if(invalid=="dual-wield")r.DualWield=true;
        Assert.Throws<ArgumentException>(()=>new ForeverWarriorSimRunner().Run(r));
    }

    [Theory]
    [InlineData("imp")][InlineData("voidwalker")][InlineData("succubus")][InlineData("incubus")]
    [InlineData("felhunter")][InlineData("infernal")][InlineData("doomguard")]
    public void EveryDemonRunsWithOnlyItsExplicitRequiredAssumptions(string demon)
    {
        var r=WarlockSimRequest.CreatePreset("affliction");r.PetType=demon;r.EnablePet=true;
        r.DurationSeconds=20m;r.CaptureTimeline=true;
        r.AllowTemporaryDemon=demon is "infernal" or "doomguard";r.AllowDoomguardRitual=demon=="doomguard";
        var runner=new WarlockSimRunner();var result=runner.Run(r);
        Assert.True(result.PetDamage>0m);Assert.Equal(result.PetDamage,runner.Run(r).PetDamage);
    }

    [Theory]
    [InlineData("infernal",false,false)][InlineData("doomguard",false,false)][InlineData("doomguard",true,false)]
    public void TemporaryDemonsRejectMissingPermission(string demon,bool temporary,bool ritual)
    {
        var r=WarlockSimRequest.CreatePreset("affliction");r.PetType=demon;r.EnablePet=true;
        r.AllowTemporaryDemon=temporary;r.AllowDoomguardRitual=ritual;
        Assert.Throws<ArgumentException>(()=>new WarlockSimRunner().Run(r));
    }
}
