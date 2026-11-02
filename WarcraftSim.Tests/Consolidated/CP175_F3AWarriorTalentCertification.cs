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

public sealed class Cp175F3AWarriorTalentCertification
{
    private static WarriorSimRequest Base(string preset="fury") => new()
    {
        Preset=preset,TalentMode="tree",Iterations=1,Seed=1753001,
        DurationSeconds=15m,StartingRage=100m,StartingStance="battle",
        CaptureRepresentativeTimeline=true,TargetLevel=60,
        TargetDodgePercent=0m,TargetParryPercent=0m,
        UseBloodrage=false,UseRend=false,UseExecute=false,UseHeroicStrike=false,
        RotationMode="custom"
    };

    private static WarriorSimRequest SpearingOffTree()
    {
        // Legal 20 Arms / 31 Fury: both capstone Bloodthirst and row-4
        // Spearing Strike may be learned regardless of the Fury preset.
        var r=Base();
        r.TalentRanks=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase)
        {
            ["cruelty"]=5,["unbridled-wrath"]=5,
            ["blood-craze"]=3,["furious-precision"]=2,
            ["enrage"]=5,["death-wish"]=1,
            ["improved-berserker-rage"]=2,["improved-intercept"]=2,
            ["flurry"]=5,["bloodthirst"]=1,
            ["improved-rend"]=3,["deflection"]=2,["improved-heroic-strike"]=1,
            ["improved-tactical-mastery"]=5,["deep-wounds"]=3,
            ["anger-management"]=1,["two-handed-weapon-specialization"]=2,
            ["spearing-strike"]=1,["impale"]=2
        };
        r.CustomRotation=[new WarriorRotationRule{Action="spearing-strike",Enabled=true}];
        ForeverWarriorTalentCatalog.Validate(r);
        Assert.Equal(51,r.TalentRanks.Values.Sum());
        return r;
    }

    private static WarriorSimRequest ShieldSlamOffTree()
    {
        var r=Base("arms");
        r.StartingStance="defensive";
        r.IsTwoHanded=false;r.ShieldEquipped=true;
        r.TalentRanks=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase)
        {
            ["improved-bloodrage"]=2,["shield-specialization"]=5,
            ["anticipation"]=5,["improved-revenge"]=3,
            ["improved-thunder-clap"]=3,["last-stand"]=1,
            ["master-of-defense"]=2,["focused-rage"]=3,
            ["concussion-blow"]=1,["bastion"]=5,["shield-slam"]=1,
            ["improved-heroic-strike"]=3,["deflection"]=5,
            ["improved-rend"]=3,["improved-tactical-mastery"]=5,
            ["improved-overpower"]=2,["deep-wounds"]=2
        };
        r.CustomRotation=[new WarriorRotationRule{Action="shield-slam",Enabled=true}];
        ForeverWarriorTalentCatalog.Validate(r);
        Assert.Equal(51,r.TalentRanks.Values.Sum());
        return r;
    }

    [Fact]
    public void CurrentBetaCatalogExposesAllThreeTreesAndIndependentSourceStatus()
    {
        var byTree=ForeverWarriorTalentCatalog.Talents.GroupBy(x=>x.Tree)
            .ToDictionary(x=>x.Key,x=>x.Count());
        Assert.Equal(17,byTree["arms"]);
        Assert.Equal(17,byTree["fury"]);
        Assert.Equal(18,byTree["protection"]);
        Assert.All(ForeverWarriorTalentCatalog.Talents,x=>
        {
            Assert.Equal("1.60.1.70707",x.SourceBuild);
            Assert.Equal("provisional",x.DataConfidence);
            Assert.Contains(x.ImplementationStatus,new[]{"fully-implemented","partially-implemented","not-implemented"});
        });
        var action=Assert.IsType<OkObjectResult>(new ForeverWarriorBuilderController().Talents());
        using var doc=JsonDocument.Parse(JsonSerializer.Serialize(action.Value,new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var impale=Assert.Single(doc.RootElement.GetProperty("talents").EnumerateArray(),
            x=>x.GetProperty("key").GetString()=="impale");
        Assert.Equal(1,impale.GetProperty("enginePrerequisiteRank").GetInt32());
        Assert.Equal("provisional",impale.GetProperty("dataConfidence").GetString());
    }

    [Fact]
    public void ImpaleNeedsOneDeepWoundsRankButNeverBypassesTierOrImprovedRend()
    {
        var r=Base("arms");
        r.TalentRanks=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase)
        {
            ["improved-rend"]=3,["deflection"]=5,
            ["improved-tactical-mastery"]=5,["improved-overpower"]=2,
            ["deep-wounds"]=1,["impale"]=2
        };
        ForeverWarriorTalentCatalog.Validate(r);
        r.TalentRanks["deep-wounds"]=0;
        Assert.Throws<ArgumentException>(()=>ForeverWarriorTalentCatalog.Validate(r));
        r.TalentRanks["deep-wounds"]=1;
        r.TalentRanks["improved-rend"]=2;
        Assert.Throws<ArgumentException>(()=>ForeverWarriorTalentCatalog.Validate(r));
    }

    [Fact]
    public void FuryPresetCanUseLearnedOffTreeSpearingStrikeInBattleStance()
    {
        var r=SpearingOffTree();
        WarriorRotationRules.Validate(r);
        var first=new ForeverWarriorSimRunner().Run(r);
        Assert.Contains(first.Batch.RepresentativeRun.Timeline,
            x=>x.AbilityKey=="spearing-strike" &&
               x.Type==CombatEventType.AbilityCastCompleted);
        Assert.Equal(first.MeanDps,new ForeverWarriorSimRunner().Run(r).MeanDps);
        r.TalentRanks.Remove("spearing-strike");
        ForeverWarriorTalentCatalog.Validate(r);
        Assert.Contains(WarriorRotationRules.GetUnavailableTalentActions(r),
            x=>x.Contains("spearing-strike"));
        Assert.Throws<ArgumentException>(()=>WarriorRotationRules.Validate(r));
        // The saved off-preset row stays visible for diagnostics, but cannot
        // become executable without the actual selected talent.
    }

    [Fact]
    public void ArmsPresetCanUseRankUnlockedProtectionShieldSlamWithShield()
    {
        var r=ShieldSlamOffTree();
        WarriorRotationRules.Validate(r);
        var run=new ForeverWarriorSimRunner().Run(r);
        Assert.Contains(run.Batch.RepresentativeRun.Timeline,
            x=>x.AbilityKey=="shield-slam" &&
               x.Type==CombatEventType.AbilityCastCompleted);
        Assert.Equal(run.MeanDps,new ForeverWarriorSimRunner().Run(r).MeanDps);
        r.ShieldEquipped=false;
        Assert.Throws<ArgumentException>(()=>WarriorRotationRules.Validate(r));
    }

    [Fact]
    public void AutoPriorityOffersLearnedOffPresetArmsActiveWithoutForcingUtility()
    {
        var r=SpearingOffTree();
        r.RotationMode="automatic";
        string[] Actions(WarriorSimRequest request)
        {
            var response=Assert.IsType<OkObjectResult>(
                new ForeverWarriorBuilderController().RotationPreview(request));
            using var doc=JsonDocument.Parse(JsonSerializer.Serialize(response.Value,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            return doc.RootElement.GetProperty("entries").EnumerateArray()
                .Select(x=>x.GetProperty("abilityKey").GetString()??"").ToArray();
        }
        var entries=Actions(r);
        Assert.Contains("spearing-strike",entries);
        Assert.Contains("bloodthirst",entries);
        Assert.DoesNotContain("piercing-howl",entries);
        r.TalentRanks.Remove("spearing-strike");
        Assert.DoesNotContain("spearing-strike",Actions(r));
    }
}
