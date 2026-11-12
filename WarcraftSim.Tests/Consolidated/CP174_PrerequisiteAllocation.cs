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
        Assert.Equal(24,icons.Length);
        Assert.Equal("inv_staff_20",
            WarcraftSim.Api.Simulation.ForeverEquipmentCatalog.FindItem(19102).IconKey);
        // CP175 adds four exact published-client records without relabeling the
        // original twenty icon references or granting them equipment eligibility.
        var published=new[] {
            (Id:18434,Name:"First Sergeant's Dragonhide Armguards",Icon:"inv_bracer_03"),
            (Id:18452,Name:"Sergeant Major's Leather Armsplints",Icon:"inv_bracer_07"),
            (Id:19829,Name:"Zandalar Augur's Belt",Icon:"inv_belt_19"),
            (Id:19830,Name:"Zandalar Augur's Bracers",Icon:"inv_bracer_15")
        };
        foreach(var expected in published)
        {
            var item=Assert.Single(icons,x=>x.Id==expected.Id);
            Assert.Equal(expected.Name,item.Name);
            Assert.Equal(expected.Icon,item.IconKey);
            Assert.Equal("published-client-item-record-provisional",item.IconSourceStatus);
        }
        var legacy=icons.Where(x=>!published.Any(p=>p.Id==x.Id)).ToArray();
        Assert.Equal(20,legacy.Length);
        Assert.All(legacy.Where(x=>x.Id is not (11287 or 14152 or 19682 or 19683 or 19684)),item=>Assert.Equal("pinned-forever-beta-provisional",
            item.IconSourceStatus));
        Assert.DoesNotContain(catalog.Items,x=>x.IconKey is not null &&
            !System.Text.RegularExpressions.Regex.IsMatch(x.IconKey,"^[a-z0-9_]+$"));
    }

}
