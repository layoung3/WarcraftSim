using System;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Data.Forever.Mage;
using WarcraftSim.Data.Forever.Warlock;
using WarcraftSim.Data.Forever.Hunter;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp174PrerequisiteAllocationTests
{
    [Fact]
    public void MageDeepTierCannotCountOwnRanksAsEarlierPoints()
    {
        var build=new ForeverMageTalentBuild {
            ArcanePointsSpent=6,
            Ranks=new(StringComparer.OrdinalIgnoreCase) {["missile-barrage"]=2}
        };
        Assert.Throws<ArgumentException>(()=>build.Validate("arcane"));
        Assert.IsType<BadRequestObjectResult>(new ForeverSoloTalentController()
            .Validate("mage","arcane",JsonSerializer.SerializeToElement(build)));
        build.ArcanePointsSpent=7;
        build.Validate("arcane");
    }

    [Fact]
    public void WarlockDeepTalentCannotCountOwnTwoRanksTowardTierAndNeedsPrerequisite()
    {
        var build=new ForeverWarlockTalentBuild {
            DestructionPointsSpent=21,
            Ranks=new(StringComparer.OrdinalIgnoreCase) {
                ["intensity"]=3,["pyroclasm"]=2
            }
        };
        Assert.Throws<ArgumentException>(()=>build.Validate(60,"affliction"));
        build.DestructionPointsSpent=22;
        build.Validate(60,"affliction");
        build.Ranks.Remove("intensity");
        Assert.Throws<ArgumentException>(()=>build.Validate(60,"affliction"));
    }

    [Fact]
    public void RemovingMageParentWhileDependentRemainsIsRejectedByEngineAndApi()
    {
        var mage=ForeverMageTalentBuild.Default("fire");
        mage.Validate("fire");
        mage.Ranks["critical-mass"]=2;
        Assert.Throws<ArgumentException>(()=>mage.Validate("fire"));
        var result=new ForeverSoloTalentController().Validate(
            "mage","fire",JsonSerializer.SerializeToElement(mage));
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void HunterDeepTierAndRequiredParentFailClosed()
    {
        var marksman=new ForeverMarksmanshipHunterTalents {
            MarksmanshipPointsSpent=31,
            BeastMasteryPointsSpent=0,
            Ranks=new(StringComparer.OrdinalIgnoreCase) {["sniper-shot"]=1}
        };
        Assert.Throws<ArgumentException>(()=>marksman.Validate());
        marksman.Ranks["trueshot-aura"]=1;
        marksman.Validate();
        marksman.Ranks.Remove("trueshot-aura");
        Assert.Throws<ArgumentException>(()=>marksman.Validate());

        var survival=new ForeverSurvivalHunterTalents();
        survival.Ranks["lacerating-strikes"]=1;
        Assert.Throws<ArgumentException>(()=>survival.Validate());
    }

    [Theory]
    [InlineData("mage","fire","combustion","critical-mass",3)]
    [InlineData("mage","arcane","arcane-power","presence-of-mind",1)]
    [InlineData("warlock","destruction","incinerate","bane-of-havoc",1)]
    [InlineData("hunter","marksmanship","sniper-shot","trueshot-aura",1)]
    public void CatalogPrerequisitesAreSourceOwnedAndExposedToSvg(
        string cls,string spec,string key,string parent,int requiredRank)
    {
        var action=Assert.IsType<OkObjectResult>(
            new ForeverSoloTalentController().Catalog(cls,spec));
        using var doc=JsonDocument.Parse(JsonSerializer.Serialize(action.Value));
        var node=Assert.Single(doc.RootElement.GetProperty("talents").EnumerateArray(),
            x=>x.GetProperty("key").GetString()==key);
        Assert.Equal(parent,node.GetProperty("enginePrerequisiteKey").GetString());
        Assert.Equal(requiredRank,node.GetProperty("enginePrerequisiteRank").GetInt32());
    }
    [Fact]
    public void ProvisionalItemArtRequiresExactPinnedIdAndName()
    {
        var catalog=WarcraftSim.Api.Simulation.ForeverEquipmentCatalog.GetCatalog();
        var icons=catalog.Items.Where(x=>!string.IsNullOrWhiteSpace(x.IconKey)).ToArray();
        Assert.Equal(15,icons.Length);
        Assert.Equal("inv_staff_20",
            WarcraftSim.Api.Simulation.ForeverEquipmentCatalog.FindItem(19102).IconKey);
        Assert.All(icons,item=>Assert.Equal("pinned-forever-beta-provisional",
            item.IconSourceStatus));
        Assert.DoesNotContain(catalog.Items,x=>x.IconKey is not null &&
            !System.Text.RegularExpressions.Regex.IsMatch(x.IconKey,"^[a-z0-9_]+$"));
    }

}
