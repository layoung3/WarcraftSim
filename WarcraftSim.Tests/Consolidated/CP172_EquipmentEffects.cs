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
                e=>e.GetProperty("spellId").GetInt32()==25122);
        }
        Assert.IsType<OkObjectResult>(controller.GetEffect("ephemeral-power"));
        Assert.IsType<NotFoundObjectResult>(controller.GetEffect("unknown-equipment-proc"));
        Assert.IsType<BadRequestObjectResult>(controller.Enchants(pageSize:9999));
        var resolve=Assert.IsType<OkObjectResult>(controller.Resolve(new ForeverEquipmentResolveRequest {
            ClassKey="mage",Level=60,Equipment=Gear(("trinket1",18820,null,null))
        }));
        using var data=JsonDocument.Parse(JsonSerializer.Serialize(resolve.Value));
        Assert.Contains(data.RootElement.GetProperty("equippedEffects").EnumerateArray(),
            x=>x.GetProperty("key").GetString()=="ephemeral-power");
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
        Assert.DoesNotContain(result.Run.Timeline,x=>x.AbilityKey is not null &&
            x.AbilityKey.StartsWith("equipment-") && x.SourceActorKey=="hunter-pet");
    }
}
