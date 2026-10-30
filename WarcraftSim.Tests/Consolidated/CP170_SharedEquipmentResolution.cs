using System;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Data.Forever.Hunter;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp170SharedEquipmentResolution
{
    private static CharacterEquipmentLoadout Gear(params (string Slot,int Id)[] items) =>
        new(){Items=items.Select(x=>new CharacterEquipmentItem{
            SlotKey=x.Slot,ItemId=x.Id
        }).ToList()};

    [Fact]
    public void EmptyLoadoutAndPartialGearAreValidAndDoNotDuplicateSources()
    {
        var empty=ForeverSharedEquipmentResolver.Resolve(Gear(),"mage");
        Assert.Empty(empty.Loadout.Items);
        Assert.Empty(empty.Sources);
        var weapon=ForeverSharedEquipmentResolver.Resolve(Gear(("main-hand",19102)),"mage");
        Assert.Equal(25m,weapon.Stats.Get("spell-power"));
        Assert.Equal(25m,weapon.RawStats.Get("spell-power"));
        Assert.Equal(25m,weapon.Stats.Get("intellect"));
        Assert.Contains(weapon.StatContributions,x=>
            x.StatKey=="spell-power" && x.Amount==25m &&
            x.SourceKey.Contains("forever-item-19102",StringComparison.Ordinal));
        Assert.Equal(138m,weapon.Weapons["main-hand"].MinimumDamage);
        Assert.Equal(3.1m,weapon.Weapons["main-hand"].SpeedSeconds);
        var provenance=Assert.Single(weapon.Sources);
        Assert.Equal(19102,provenance.ItemId);
        Assert.Contains("theforeverera.com",provenance.SourceUrl);
        Assert.Equal("1.60.1.70532",provenance.SourceBuild);
        Assert.NotEmpty(provenance.CatalogVersion);
        Assert.Equal(19102,Assert.Single(weapon.Loadout.Items).ItemId);
        Assert.Equal(25m,Assert.Single(weapon.Loadout.Items).Stats.Get("spell-power"));
    }

    [Theory]
    [InlineData("ranged",19102,"hunter")]
    [InlineData("head",19102,"mage")]
    [InlineData("main-hand",19102,"hunter")]
    [InlineData("off-hand",19167,"warrior")]
    public void InvalidClassOrSlotFailsInsteadOfSilentlyDroppingItem(
        string slot,int id,string classKey)
    {
        Assert.Throws<ArgumentException>(()=>
            ForeverSharedEquipmentResolver.Resolve(Gear((slot,id)),classKey));
    }

    [Fact]
    public void PhysicalArmorEligibilityIsCheckedAgainstRealItemSubclass()
    {
        Assert.Throws<ArgumentException>(()=>
            ForeverSharedEquipmentResolver.Resolve(Gear(("head",12640)),"mage"));
        Assert.Throws<ArgumentException>(()=>
            ForeverSharedEquipmentResolver.Resolve(Gear(("hands",15063)),"warlock"));
        Assert.Single(ForeverSharedEquipmentResolver.Resolve(
            Gear(("head",12640)),"warrior").Sources);
        Assert.Single(ForeverSharedEquipmentResolver.Resolve(
            Gear(("hands",15063)),"hunter").Sources);
    }

    [Fact]
    public void FactionAndProvisionalSourceRestrictionsAreExplicit()
    {
        Assert.Throws<ArgumentException>(()=>
            ForeverSharedEquipmentResolver.Resolve(
                Gear(("trinket1",19120)),"hunter",60,"human"));
        var horde=ForeverSharedEquipmentResolver.Resolve(
            Gear(("trinket1",19120)),"hunter",60,"orc");
        Assert.Equal(14m,horde.RawStats.Get("attack-power"));
        Assert.Throws<ArgumentException>(()=>
            ForeverSharedEquipmentResolver.Resolve(
                Gear(("trinket1",13965)),"hunter"));
        Assert.Throws<ArgumentException>(()=>
            ForeverSharedEquipmentResolver.Resolve(
                Gear(("main-hand",12940)),"warrior"));
    }

    [Fact]
    public void RawRatingsAreConvertedToCombatPercentagesExactlyOnce()
    {
        var result=ForeverSharedEquipmentResolver.Resolve(Gear(("head",12640)),"warrior");
        Assert.Equal(20m,result.RawStats.Get("hit-rating"));
        Assert.Equal(2m,result.Stats.Get("hit-chance-percent"));
        Assert.Equal(28m,result.RawStats.Get("critical-strike-rating"));
    }

    [Fact]
    public void UniqueRingsAndTwoHandOffhandAreRejected()
    {
        Assert.Throws<ArgumentException>(()=>ForeverSharedEquipmentResolver.Resolve(
            Gear(("finger1",17713),("finger2",17713)),"hunter"));
        Assert.Throws<ArgumentException>(()=>ForeverSharedEquipmentResolver.Resolve(
            Gear(("main-hand",19102),("off-hand",19168)),"mage"));
        var permitted=ForeverSharedEquipmentResolver.Resolve(
            Gear(("finger1",18500),("finger2",18500)),"hunter");
        Assert.Equal(2,permitted.Sources.Count);
    }

    [Fact]
    public void ProvenanceCannotBeReplacedWithUserSubmittedFakeStats()
    {
        var gear=Gear(("main-hand",19102));
        gear.Items[0].Stats.Add("spell-power",10000m);
        Assert.Throws<ArgumentException>(()=>
            ForeverSharedEquipmentResolver.Resolve(gear,"mage"));
        gear.Items[0].Stats.Values.Clear();
        gear.Items[0].EnchantSpellId=20034; // Crusader proc not statically modeled.
        Assert.Throws<ArgumentException>(()=>
            ForeverSharedEquipmentResolver.Resolve(gear,"mage"));
        gear.Items[0].EnchantSpellId=25080; // Gloves only
        Assert.Throws<ArgumentException>(()=>
            ForeverSharedEquipmentResolver.Resolve(gear,"mage"));
    }

    [Fact]
    public void SetBonusThresholdAndEnchantAreAppliedExactlyOnce()
    {
        var one=ForeverSharedEquipmentResolver.Resolve(Gear(("legs",15062)),"hunter");
        var both=ForeverSharedEquipmentResolver.Resolve(
            Gear(("legs",15062),("hands",15063)),"hunter");
        Assert.Equal(one.RawStats.Get("hit-chance-percent")+2m,
            both.RawStats.Get("hit-chance-percent"));
        var setBonuses=both.Loadout.Items.SelectMany(x=>x.Modifiers)
            .Where(x=>x.ModifierType=="set-bonus").ToArray();
        Assert.Single(setBonuses);
        var gloves=Gear(("hands",15063));
        gloves.Items[0].EnchantSpellId=25080;
        var enchanted=ForeverSharedEquipmentResolver.Resolve(gloves,"hunter");
        Assert.Equal(15m,enchanted.RawStats.Get("agility"));
        Assert.Single(enchanted.Loadout.Items[0].Modifiers);
    }

    [Fact]
    public void EquipmentEndpointsReturnCatalogVersionPagingAndValidationErrors()
    {
        var api=new ForeverEquipmentController();
        var listing=Assert.IsType<OkObjectResult>(api.Items(page:1,pageSize:3));
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(listing.Value));
        Assert.Equal(3,json.RootElement.GetProperty("items").GetArrayLength());
        Assert.NotEmpty(json.RootElement.GetProperty("catalogVersion").GetString()!);
        Assert.IsType<BadRequestObjectResult>(api.Items(pageSize:201));
        Assert.IsType<NotFoundObjectResult>(api.Get(999999999));
        var resolution=Assert.IsType<OkObjectResult>(api.Resolve(
            new ForeverEquipmentResolveRequest {
                ClassKey="mage",Equipment=Gear(("main-hand",19102))
            }));
        using var normalized=JsonDocument.Parse(JsonSerializer.Serialize(resolution.Value,new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal(25m,normalized.RootElement.GetProperty("stats")
            .GetProperty("values").GetProperty("spell-power").GetDecimal());
        Assert.IsType<BadRequestObjectResult>(api.Resolve(
            new ForeverEquipmentResolveRequest {
                ClassKey="mage",Equipment=Gear(("head",19102))
            }));
    }
}
