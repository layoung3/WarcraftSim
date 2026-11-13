using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Characters.Equipment;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175F2B1ProvisionalEnchants
{
    private static readonly (int Id,string Slot,string Stat,decimal Value)[] Reviewed=
    [
        (1249059,"neck","agility",5m),(1249019,"neck","strength",5m),
        (20015,"back","armor",70m),(20028,"chest","intellect",10m),
        (20010,"wrist","strength",9m),(23802,"wrist","spell-power",8m),
        (1248665,"wrist","defense",4m),(1248640,"hands","strength",15m),
(25079,"hands","spell-power",12m),
        (20024,"feet","spirit",5m),(27837,"main-hand","agility",25m),
        (20036,"main-hand","intellect",9m),(20035,"main-hand","spirit",9m),
        (23800,"main-hand","agility",15m),(23799,"main-hand","strength",15m),
        (22750,"main-hand","spell-power",19m),(23804,"main-hand","intellect",22m),
        (23803,"main-hand","spirit",22m),(20016,"off-hand","spirit",9m),
        (1219581,"off-hand","stamina",12m),(1219578,"off-hand","spirit",12m),
        (1219577,"off-hand","intellect",9m),(1219579,"off-hand","intellect",6m)
    ];

    private static CharacterEquipmentLoadout Gear(string slot,int itemId,int? enchant=null)=>
        new(){Items=[new CharacterEquipmentItem {
            SlotKey=slot,ItemId=itemId,EnchantSpellId=enchant
        }]};

    [Fact]
    public void TwentyThreeProvisionalDefinitionsHaveSourceAndIndependentImplementationMetadata()
    {
        var all=ForeverEquipmentCatalog.GetCatalog().Enchants;
        Assert.Equal(54,all.Count);
        Assert.Equal(all.Count,all.Select(x=>x.SpellId).Distinct().Count());
        foreach(var (id,slot,stat,value) in Reviewed)
        {
            var enchant=Assert.Single(all,x=>x.SpellId==id);
            Assert.Equal("provisional",enchant.DataConfidence);
            Assert.Equal(id==1248665?"partially-implemented":"fully-implemented",enchant.ImplementationStatus);
            Assert.Equal("1.60.1.70707",enchant.SourceBuild);
            Assert.Equal("2026-11-02",enchant.SourceReviewedAt);
            Assert.Equal("https://www.wowhead.com/forever/spell="+id,enchant.SourceUrl);
            Assert.Contains(slot,enchant.AllowedSlots);
            Assert.Equal(value,enchant.Stats[stat]);
            Assert.True(enchant.Modeled);
            Assert.False(enchant.IsTemporary);
        }
        var roundtrip=JsonSerializer.Deserialize<ForeverEquipmentEnchant>(
            JsonSerializer.Serialize(ForeverEquipmentCatalog.FindEnchant(1249059)))!;
        Assert.Equal("provisional",roundtrip.DataConfidence);
        Assert.Equal("fully-implemented",roundtrip.ImplementationStatus);
        Assert.Equal("1.60.1.70707",roundtrip.SourceBuild);
    }

    [Theory]
    [InlineData("warrior","main-hand",279260,27837,"agility",25)]
    [InlineData("mage","main-hand",19102,20036,"intellect",9)]
    [InlineData("warlock","main-hand",19102,20035,"spirit",9)]
    [InlineData("hunter","neck",18404,1249059,"agility",5)]
    public void ProvisionalEnchantsResolveOnceForAllFourClasses(
        string cls,string slot,int itemId,int spellId,string stat,int amount)
    {
        var before=ForeverSharedEquipmentResolver.Resolve(Gear(slot,itemId),cls,60,"orc");
        var with=ForeverSharedEquipmentResolver.Resolve(Gear(slot,itemId,spellId),cls,60,"orc");
        Assert.Equal(amount,with.RawStats.Get(stat)-before.RawStats.Get(stat));
        Assert.Single(with.Loadout.Items[0].Modifiers,
            x=>x.ModifierKey=="enchant-"+spellId);
        Assert.Equal(spellId,with.Loadout.Items[0].EnchantSpellId);
        Assert.Equal(with.RawStats.Get(stat),
            ForeverSharedEquipmentResolver.Resolve(Gear(slot,itemId,spellId),cls,60,"orc").RawStats.Get(stat));
    }

    [Fact]
    public void ShieldsHeldItemsAndTwoHandersNeverReceiveIncompatibleEffects()
    {
        var staff=ForeverEquipmentCatalog.FindItem(19102);
        var sword=ForeverEquipmentCatalog.FindItem(19168);
        var shield=ForeverEquipmentCatalog.FindItem(12602);
        var shieldEnchant=ForeverEquipmentCatalog.FindEnchant(1219581);
        var heldEnchant=ForeverEquipmentCatalog.FindEnchant(1219577);
        var twoHanded=ForeverEquipmentCatalog.FindEnchant(27837);
        ForeverEquipmentCatalog.ValidateEnchantTarget(shield,shieldEnchant,"off-hand");
        Assert.Throws<ArgumentException>(()=>
            ForeverEquipmentCatalog.ValidateEnchantTarget(sword,shieldEnchant,"off-hand"));
        Assert.Throws<ArgumentException>(()=>
            ForeverEquipmentCatalog.ValidateEnchantTarget(shield,heldEnchant,"off-hand"));
        Assert.Throws<ArgumentException>(()=>
            ForeverEquipmentCatalog.ValidateEnchantTarget(sword,heldEnchant,"off-hand"));
        Assert.Throws<ArgumentException>(()=>
            ForeverEquipmentCatalog.ValidateEnchantTarget(sword,twoHanded,"main-hand"));
        ForeverEquipmentCatalog.ValidateEnchantTarget(staff,twoHanded,"main-hand");
        Assert.Throws<ArgumentException>(()=>
            ForeverSharedEquipmentResolver.Resolve(Gear("main-hand",19168,27837),"warrior"));
        var controller=new ForeverEquipmentController();
        var shieldChoices=Assert.IsType<OkObjectResult>(controller.CompatibleEnchants(12602,"off-hand"));
        var shieldJson=JsonSerializer.Serialize(shieldChoices.Value);
        Assert.Contains("1219581",shieldJson);
        Assert.DoesNotContain("1219577",shieldJson);
        var weaponChoices=Assert.IsType<OkObjectResult>(controller.CompatibleEnchants(19168,"off-hand"));
        Assert.DoesNotContain("1219581",JsonSerializer.Serialize(weaponChoices.Value));
    }

    [Fact]
    public void KnownEffectsRemainIndependentOfConfidenceAndExistingExecutionUntouched()
    {
        var proc=ForeverEquipmentCatalog.FindEnchant(13898);
        Assert.True(proc.Modeled);
        Assert.Empty(proc.Stats);
        Assert.NotNull(ForeverEquipmentEffectsCatalog.GetCatalog().Effects.Single(
            x=>x.EnchantSpellId==13898 && x.Modeled));
        Assert.Equal(30m,ForeverEquipmentCatalog.FindEnchant(22749).Stats["spell-power"]);
        Assert.Equal(24m,ForeverEquipmentCatalog.FindEnchant(23802).Stats["healing-power"]);
        Assert.Equal(8m,ForeverEquipmentCatalog.FindEnchant(23802).Stats["spell-power"]);
    }
}
