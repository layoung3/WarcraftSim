using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp172EquipmentEffectIntegration
{
    private static CharacterEquipmentLoadout Gear(params (string Slot,int Id,int? Permanent,int? Temporary)[] inputs) =>
        new(){Items=inputs.Select(x=>new CharacterEquipmentItem {
            SlotKey=x.Slot,ItemId=x.Id,EnchantSpellId=x.Permanent,
            TemporaryEnchantSpellId=x.Temporary
        }).ToList()};

    [Fact]
    public void PermanentAndTemporaryWeaponEnchantsAreIndependentStatSources()
    {
        var gear=Gear(("main-hand",19102,22749,25122),("hands",22501,13815,null));
        var resolved=ForeverSharedEquipmentResolver.Resolve(gear,"mage",60);
        Assert.Equal(66m,resolved.RawStats.Get("spell-power")-ForeverEquipmentCatalog.FindItem(19102).Stats.GetValueOrDefault("spell-power")-ForeverEquipmentCatalog.FindItem(22501).Stats.GetValueOrDefault("spell-power"));
        Assert.Equal(1m,resolved.RawStats.Get("spell-critical-chance-percent")-
            ForeverEquipmentCatalog.FindItem(22501).Stats.GetValueOrDefault("spell-critical-chance-percent"));
        Assert.Equal(7m,resolved.RawStats.Get("agility"));
        Assert.Equal(2,resolved.Loadout.Items.Single(x=>x.ItemId==19102).Modifiers.Count);
        var noOil=Gear(("main-hand",19102,22749,null));
        Assert.Equal(30m,ForeverSharedEquipmentResolver.Resolve(noOil,"mage").RawStats.Get("spell-power")-
            ForeverEquipmentCatalog.FindItem(19102).Stats.GetValueOrDefault("spell-power"));
        Assert.Throws<ArgumentException>(()=>
            ForeverSharedEquipmentResolver.Resolve(Gear(("main-hand",19102,25122,null)),"mage"));
        Assert.Throws<ArgumentException>(()=>
            ForeverSharedEquipmentResolver.Resolve(Gear(("hands",22501,null,25122)),"mage"));
        Assert.Throws<ArgumentException>(()=>
            ForeverSharedEquipmentResolver.Resolve(Gear(("main-hand",19102,null,22749)),"mage"));
    }

    [Fact]
    public void EnchantsAndEffectsAreSearchableAndUnknownIdsAreRejected()
    {
        var controller=new ForeverEquipmentController();
        Assert.IsType<OkObjectResult>(controller.GetEnchant(22749));
        Assert.IsType<NotFoundObjectResult>(controller.GetEnchant(999999));
        var list=Assert.IsType<OkObjectResult>(controller.Enchants(slot:"main-hand",temporary:true));
        using(var j=JsonDocument.Parse(JsonSerializer.Serialize(list.Value)))
        {
            Assert.True(j.RootElement.GetProperty("total").GetInt32()>=1);
            Assert.Contains(j.RootElement.GetProperty("enchants").EnumerateArray(),
                e=>e.GetProperty("SpellId").GetInt32()==25122);
        }
        Assert.IsType<OkObjectResult>(controller.GetEffect("ephemeral-power"));
        Assert.IsType<NotFoundObjectResult>(controller.GetEffect("unknown-equipment-proc"));
        Assert.IsType<BadRequestObjectResult>(controller.Enchants(pageSize:9999));
        var resolve=Assert.IsType<OkObjectResult>(controller.Resolve(new ForeverEquipmentResolveRequest {
            ClassKey="mage",Level=60,Equipment=Gear(("trinket1",18820,null,null))
        }));
        using var data=JsonDocument.Parse(JsonSerializer.Serialize(resolve.Value));
        Assert.Contains(data.RootElement.GetProperty("equippedEffects").EnumerateArray(),
            x=>x.GetProperty("Key").GetString()=="ephemeral-power");
    }

    [Fact]
    public void ExpandedForeverPermanentEnchantsResolveByCatalogAndRejectWrongSlots()
    {
        var cases=new (int SpellId,string Slot,int ItemId,string Stat,decimal Value)[] {
            (25080,"hands",22501,"agility",15m),
            (1220624,"wrist",22502,"spell-power",16m),
            (1248661,"wrist",22502,"intellect",9m),
            (1248599,"wrist",22502,"agility",9m),
            (20025,"chest",14152,"stamina",4m),
            (20014,"back",22731,"fire-resistance",5m)
        };
        var catalog=ForeverEquipmentCatalog.GetCatalog();
        foreach(var entry in cases)
        {
            var enchant=ForeverEquipmentCatalog.FindEnchant(entry.SpellId);
            Assert.True(enchant.Modeled);
            Assert.False(enchant.IsTemporary);
            Assert.Equal(entry.Value,enchant.Stats[entry.Stat]);
            Assert.Single(enchant.AllowedSlots);
            Assert.Contains(entry.Slot,enchant.AllowedSlots);
            Assert.False(string.IsNullOrWhiteSpace(enchant.SourceUrl));
        }
        var allStats=ForeverEquipmentCatalog.FindEnchant(20025).Stats;
        foreach(var key in new[]{"strength","agility","stamina","intellect","spirit"})
            Assert.Equal(4m,allStats[key]);
        Assert.Equal(5,allStats.Count);
        var resist=ForeverEquipmentCatalog.FindEnchant(20014).Stats;
        foreach(var key in new[]{"arcane-resistance","fire-resistance","frost-resistance",
            "nature-resistance","shadow-resistance","holy-resistance"})
            Assert.Equal(5m,resist[key]);
        Assert.Equal(18,catalog.Enchants.Count);
        // Slot validation is independent of client data and must fail closed.
        Assert.Throws<ArgumentException>(() =>
            ForeverSharedEquipmentResolver.Resolve(Gear(("chest",14152,25080,null)),"mage"));
        Assert.Throws<ArgumentException>(() =>
            ForeverSharedEquipmentResolver.Resolve(Gear(("chest",14152,20014,null)),"mage"));
    }

    [Fact]
    public void Cp172DoesNotReregisterCanonicalSuperiorAgilityEnchant()
    {
        var overlay=ForeverEquipmentEffectsCatalog.GetCatalog();
        var registered=ForeverEquipmentCatalog.GetCatalog();
        Assert.DoesNotContain(overlay.Enchants,x=>x.SpellId==25080);
        Assert.Single(registered.Enchants.Where(x=>x.SpellId==25080));
        var existing=ForeverEquipmentCatalog.FindEnchant(25080);
        Assert.Equal("Enchant Gloves - Superior Agility",existing.Name);
        Assert.Equal(15m,existing.Stats["agility"]);
        Assert.Equal(new[]{"hands"},existing.AllowedSlots);
        var resolved=ForeverSharedEquipmentResolver.Resolve(
            Gear(("hands",22501,25080,null)),"mage");
        Assert.Equal(15m,resolved.RawStats.Get("agility")-
            ForeverEquipmentCatalog.FindItem(22501).Stats.GetValueOrDefault("agility"));
        Assert.Equal(2,overlay.Items.Count);
        Assert.Contains(overlay.Items,x=>x.Id==18820);
        Assert.Contains(overlay.Items,x=>x.Id==22954);
    }

    [Fact]
    public void AdditiveCatalogDuplicateItemAndEnchantIdsFailClosedWithExactId()
    {
        var baseline=new ForeverEquipmentCatalogDocument {
            Items=[new ForeverEquipmentItem{Id=18820}],
            Enchants=[new ForeverEquipmentEnchant{SpellId=25080}]
        };
        var distinct=new ForeverEquipmentEffectsDocument {
            Items=[new ForeverEquipmentItem{Id=22954}],
            Enchants=[new ForeverEquipmentEnchant{SpellId=20025}]
        };
        ForeverEquipmentCatalog.ValidateAdditiveIdentifiers(baseline,distinct);
        var duplicateItem=new ForeverEquipmentEffectsDocument {
            Items=[new ForeverEquipmentItem{Id=18820}]
        };
        var itemError=Assert.Throws<InvalidOperationException>(()=>
            ForeverEquipmentCatalog.ValidateAdditiveIdentifiers(baseline,duplicateItem));
        Assert.Contains("item ID 18820",itemError.Message);
        var duplicateEnchant=new ForeverEquipmentEffectsDocument {
            Enchants=[new ForeverEquipmentEnchant{SpellId=25080}]
        };
        var enchantError=Assert.Throws<InvalidOperationException>(()=>
            ForeverEquipmentCatalog.ValidateAdditiveIdentifiers(baseline,duplicateEnchant));
        Assert.Contains("enchant spell ID 25080",enchantError.Message);
        var withinOverlay=new ForeverEquipmentEffectsDocument {
            Items=[new ForeverEquipmentItem{Id=22954},new ForeverEquipmentItem{Id=22954}]
        };
        Assert.Throws<InvalidOperationException>(()=>
            ForeverEquipmentCatalog.ValidateAdditiveIdentifiers(baseline,withinOverlay));
    }

    [Fact]
    public void EphemeralPowerUsesSharedAuraLifecycleAndActualMageDamage()
    {
        MageSimRequest Configure(bool selected)
        {
            var request=MageSimRequest.CreatePreset("fire");
            request.DurationSeconds=36m;
            request.TargetMaximumHealth=1000000m;
            request.BaseSpellHitPercent=100m;
            request.SpellCritPercent=0m;
            request.CaptureTimeline=true;
            if(selected)request.Character.Equipment=Gear(("trinket1",18820,null,null));
            return request;
        }
        var before=new MageSimRunner().Run(Configure(false));
        var withGear=Configure(true);
        var after=new MageSimRunner().Run(withGear);
        Assert.True(after.DamageDone>before.DamageDone);
        Assert.Equal(1m,after.Run.CustomMetrics["equipment-ephemeral-power@trinket1-activations"]);
        Assert.Contains(after.Run.Timeline,x=>x.Type==CombatEventType.AuraApplied &&
            x.EffectKey=="ephemeral-power@trinket1");
        Assert.Contains(after.Run.Timeline,x=>x.Type==CombatEventType.AuraRemoved &&
            x.EffectKey=="ephemeral-power@trinket1");
        Assert.Equal(after.DamageDone,new MageSimRunner().Run(withGear).DamageDone);
    }

    [Fact]
    public void EphemeralPowerChangesActualWarlockOwnerDamageWithoutPetProcs()
    {
        WarlockSimRequest Configure(bool selected)
        {
            var request=WarlockSimRequest.CreatePreset("demonology");
            request.DurationSeconds=40m;
            request.TargetMaximumHealth=1000000m;
            request.BaseSpellHitPercent=100m;
            request.SpellCritPercent=0m;
            request.EnablePet=true;
            request.PetType="imp";
            request.PrePullSacrificedDemon=null;
            if(selected)request.Character.Equipment=Gear(("trinket1",18820,null,null));
            return request;
        }
        var plain=new WarlockSimRunner().Run(Configure(false));
        var buffed=new WarlockSimRunner().Run(Configure(true));
        Assert.True(buffed.WarlockDamage>plain.WarlockDamage);
        Assert.True(buffed.PetDamage>=0m);
        Assert.Equal(1m,buffed.Run.CustomMetrics["equipment-ephemeral-power@trinket1-activations"]);
        Assert.Equal(buffed.DamageDone,buffed.WarlockDamage+buffed.PetDamage);
    }

    [Fact]
    public void EphemeralPowerRespectsNinetySecondCooldownAndExpiresAfterFifteenSeconds()
    {
        var request=MageSimRequest.CreatePreset("fire");
        request.DurationSeconds=116m;request.TargetMaximumHealth=5000000m;
        request.Character.Equipment=Gear(("trinket1",18820,null,null));
        request.CaptureTimeline=true;
        var response=new MageSimRunner().Run(request);
        Assert.Equal(2m,response.Run.CustomMetrics["equipment-ephemeral-power@trinket1-activations"]);
        var starts=response.Run.Timeline.Where(x=>x.Type==CombatEventType.AuraApplied &&
            x.EffectKey=="ephemeral-power@trinket1").Select(x=>x.TimeSeconds).ToArray();
        Assert.Equal(new[]{0m,90m},starts);
        Assert.Equal(2,response.Run.Timeline.Count(x=>x.Type==CombatEventType.AuraRemoved &&
            x.EffectKey=="ephemeral-power@trinket1"));
    }

    [Fact]
    public void KissOfTheSpiderOnUseReschedulesOwnerRangedSwingsAndExpires()
    {
        MarksmanshipHunterSimRequest Setup(bool equip)
        {
            var request=new MarksmanshipHunterSimRequest {
                DurationSeconds=35m,TargetMaximumHealth=9000000m,
                HasPet=false,UseRapidFire=false,UseAimedShot=false,
                UseSniperShot=false,UseMultiShot=false,UseArcaneShot=false,
                UseSerpentSting=false,CaptureTimeline=true
            };
            request.Tuning.HitChancePercent=100m;
            request.Tuning.CriticalChancePercent=0m;
            if(equip)request.Equipment=Gear(("trinket1",22954,null,null));
            return request;
        }
        var baseRun=new MarksmanshipHunterSimRunner().Run(Setup(false));
        var run=new MarksmanshipHunterSimRunner().Run(Setup(true));
        decimal[] Hits(MarksmanshipHunterSimResponse result)=>
            result.Run.Timeline.Where(x=>x.Type==CombatEventType.Damage &&
                x.SourceActorKey=="hunter" && x.AbilityKey=="auto-shot" &&
                x.Amount>0m).Select(x=>x.TimeSeconds).ToArray();
        var regular=Hits(baseRun);var hasted=Hits(run);
        Assert.True(regular.Length>4 && hasted.Length>4);
        Assert.True(hasted[2]-hasted[1] < regular[2]-regular[1]);
        Assert.Contains(run.Run.Timeline,x=>x.Type==CombatEventType.AuraRemoved &&
            x.EffectKey=="kiss-of-the-spider@trinket1");
        Assert.Equal(1m,run.Run.CustomMetrics["equipment-kiss-of-the-spider@trinket1-activations"]);
        Assert.Equal(run.DamageDone,new MarksmanshipHunterSimRunner().Run(Setup(true)).DamageDone);
    }

    [Fact]
    public void TwoOnUseTrinketsRespectSharedCooldownAndIndependentDurations()
    {
        var request=MageSimRequest.CreatePreset("fire");
        request.DurationSeconds=40m;
        request.TargetMaximumHealth=9000000m;
        request.CaptureTimeline=true;
        request.Character.Equipment=Gear(("trinket1",18820,null,null),
                                         ("trinket2",22954,null,null));
        var run=new MageSimRunner().Run(request);
        var ephemeral=run.Run.Timeline.Where(x=>x.Type==CombatEventType.AuraApplied &&
            x.EffectKey=="ephemeral-power@trinket1").Select(x=>x.TimeSeconds).ToArray();
        var spider=run.Run.Timeline.Where(x=>x.Type==CombatEventType.AuraApplied &&
            x.EffectKey=="kiss-of-the-spider@trinket2").Select(x=>x.TimeSeconds).ToArray();
        Assert.Equal(new[]{0m},ephemeral);
        Assert.Equal(new[]{15m},spider);
        Assert.Contains(run.Run.Timeline,x=>x.Type==CombatEventType.AuraRemoved &&
            x.EffectKey=="kiss-of-the-spider@trinket2" && x.TimeSeconds==30m);
        Assert.Equal(1m,run.Run.CustomMetrics["equipment-ephemeral-power@trinket1-activations"]);
        Assert.Equal(1m,run.Run.CustomMetrics["equipment-kiss-of-the-spider@trinket2-activations"]);
    }

    [Fact]
    public void RobeOfArchmageOnUseRestoresRealManaWithoutInventingFullManaProc()
    {
        var request=MageSimRequest.CreatePreset("fire");
        request.DurationSeconds=35m;
        request.TargetMaximumHealth=1000000m;
        request.StartingManaPercent=20m;
        request.CaptureTimeline=true;
        request.Character.Equipment=Gear(("chest",14152,null,null));
        var run=new MageSimRunner().Run(request);
        var generated=run.Run.Timeline.Where(x=>x.Type==CombatEventType.ResourceChanged &&
            x.AbilityKey=="equipment-robe-of-archmage-mana@chest" &&
            x.ResourceKey=="mana" && x.Amount>0m).ToArray();
        var one=Assert.Single(generated);
        Assert.InRange(one.Amount!.Value,375m,625m);
        Assert.NotNull(one.ResourceTotalAfter);
        Assert.Equal(1m,run.Run.CustomMetrics["equipment-robe-of-archmage-mana@chest-activations"]);
        Assert.Equal("modeled",ForeverEquipmentCatalog.FindItem(14152).Effects
            .Single(x=>x.SpellId==18385).ExecutionStatus);
        Assert.Equal(run.DamageDone,new MageSimRunner().Run(request).DamageDone);
    }

    [Fact]
    public void FieryWeaponHasSourcedDamageAndProvisionalPpmWithoutPetOwnership()
    {
        var source=ForeverEquipmentEffectsCatalog.GetCatalog();
        var fiery=Assert.Single(source.Effects,x=>x.EnchantSpellId==13898);
        Assert.Equal(40m,fiery.Damage);
        Assert.Equal(6m,fiery.ProcsPerMinute);
        Assert.True(fiery.Provisional);
        var crusader=Assert.Single(source.Effects,x=>x.EnchantSpellId==20034);
        Assert.False(crusader.Modeled);
        Assert.True(crusader.Provisional);
        var request=new SurvivalHunterSimRequest {
            CombatMode="melee",DurationSeconds=100m,
            TargetMaximumHealth=9000000m,UseFireTraps=false,
            UseFrostTraps=false,HasPet=false,CaptureTimeline=true
        };
        request.Equipment=Gear(("main-hand",279259,13898,null));
        request.Tuning.HitChancePercent=100m;
        request.Tuning.TargetDodgePercent=0m;
        request.Tuning.TargetParryPercent=0m;
        var result=new SurvivalHunterSimRunner().Run(request);
        Assert.Contains(result.Run.Timeline,x=>x.Type==CombatEventType.Damage &&
            x.AbilityKey=="equipment-fiery-weapon@main-hand" && x.Amount>0m);
        Assert.True(result.Run.CustomMetrics["equipment-fiery-weapon@main-hand-activations"]>0m);
        var procDamage=result.Run.Timeline.Where(x=>x.Type==CombatEventType.Damage &&
            x.AbilityKey=="equipment-fiery-weapon@main-hand" && x.Amount>0m)
            .Sum(x=>x.Amount??0m);
        Assert.Equal(procDamage,result.Run.CustomMetrics["equipment-fiery-weapon@main-hand-damage"]);
        Assert.DoesNotContain(result.Run.Timeline,x=>x.AbilityKey is not null &&
            x.AbilityKey.StartsWith("equipment-") && x.SourceActorKey=="hunter-pet");
    }
}
