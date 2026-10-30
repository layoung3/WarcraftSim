using System;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp171RuntimeEquipmentIntegration
{
    private static CharacterEquipmentLoadout Gear(params (string Slot, int Id)[] selections) =>
        new() { Items = selections.Select(x => new CharacterEquipmentItem {
            SlotKey = x.Slot, ItemId = x.Id
        }).ToList() };

    [Theory]
    [InlineData("mage","mail",false)]
    [InlineData("warlock","plate",false)]
    [InlineData("priest","leather",false)]
    [InlineData("hunter","mail",true)]
    [InlineData("shaman","mail",true)]
    [InlineData("warrior","plate",true)]
    [InlineData("rogue","shield",false)]
    [InlineData("mage","cloth",true)]
    public void ArmorProficiencyIsIndependentOfImportedClassMasks(
        string classKey,string subclass,bool expected)
    {
        Assert.Equal(expected,ForeverSharedEquipmentResolver.CanEquipArmorClass(
            classKey,subclass));
    }

    [Fact]
    public void CurrentBuildClientDerivedIronwoodReviewCorrectsOmittedStamina()
    {
        var sword=ForeverEquipmentCatalog.FindItem(279259);
        Assert.Equal("1.60.1.70707",sword.SourceBuild);
        Assert.Equal("published-client-tooltip",sword.VerificationStatus);
        Assert.Equal(12m,sword.Stats["stamina"]);
        Assert.Equal(10m,sword.Stats["hit-rating"]);
        Assert.Equal(8m,sword.Stats["strength"]);
        Assert.Equal(79m,sword.Weapon!.MinimumDamage);
        Assert.Equal(148m,sword.Weapon.MaximumDamage);
        Assert.Equal(2.4m,sword.Weapon.SpeedSeconds);
        var resolved=ForeverSharedEquipmentResolver.Resolve(Gear(("main-hand",279259)),"hunter");
        Assert.Equal(12m,resolved.RawStats.Get("stamina"));
        Assert.Equal(10m,resolved.RawStats.Get("hit-rating"));
        Assert.Throws<ArgumentException>(()=>
            ForeverSharedEquipmentResolver.Resolve(Gear(("main-hand",279259)),"priest"));
    }

    [Fact]
    public void Approved70707HunterMailHasCompleteStatsAndNonexecutingEffect()
    {
        var item=ForeverEquipmentCatalog.FindItem(272715);
        Assert.Equal("1.60.1.70707",item.SourceBuild);
        Assert.Equal(55,item.RequiredLevel);
        Assert.Equal(221m,item.Stats["armor"]);
        Assert.Equal(20m,item.Stats["stamina"]);
        Assert.Equal(8m,item.Stats["agility"]);
        Assert.Equal(6m,item.Stats["intellect"]);
        Assert.Equal(12m,item.Stats["attack-power"]);
        var effect=Assert.Single(item.Effects);
        Assert.Equal("unsupported",effect.Kind);
        Assert.Equal("not-modeled",effect.ExecutionStatus);
        // Level 55 is the item's minimum equip level, not a certified Hunter
        // Agility-to-crit profile. CP81 currently supplies combat conversions
        // only at levels 30 and 60; resolve combat stats at level 60.
        var equip=ForeverSharedEquipmentResolver.Resolve(Gear(("hands",272715)),"hunter",60,"human");
        Assert.Equal(12m,equip.RawStats.Get("attack-power"));
        Assert.Equal(221m,equip.RawStats.Get("armor"));
        Assert.DoesNotContain(equip.Loadout.Items.SelectMany(x=>x.Modifiers),x=>x.ModifierType=="set-bonus");
        Assert.Contains(ForeverEquipmentCatalog.GetCatalog().Sets,x=>
            x.Key=="lieutenant-commanders-pursuit" && x.RequiredPieces==2 &&
            x.Modeled && x.Stats["agility"]==20m);
        Assert.Contains(ForeverEquipmentCatalog.GetCatalog().Sets,x=>
            x.Key=="lieutenant-commanders-pursuit" && x.RequiredPieces==4 && !x.Modeled);
        Assert.Throws<ArgumentException>(()=>
            ForeverSharedEquipmentResolver.Resolve(Gear(("hands",272715)),"mage",60,"human"));
        Assert.Throws<ArgumentException>(()=>
            ForeverSharedEquipmentResolver.Resolve(Gear(("hands",272715)),"hunter",54,"human"));
        Assert.Throws<ArgumentException>(()=>
            ForeverSharedEquipmentResolver.Resolve(Gear(("hands",272715)),"hunter",60,"orc"));
    }

    [Fact]
    public void Approved70707IronwoodRatingsReachMageAndWarlockSimulationRequests()
    {
        var gear=Gear(("main-hand",279259));
        foreach(var key in new[]{"mage","warlock"})
        {
            var resolved=ForeverSharedEquipmentResolver.Resolve(gear,key,60,"human");
            Assert.Equal(10m,resolved.RawStats.Get("hit-rating"));
            Assert.Equal(1m,resolved.Stats.Get("spell-hit-chance-percent"));
            Assert.Equal(12m,resolved.RawStats.Get("stamina"));
        }
        var mage=MageSimRequest.CreatePreset("fire");
        mage.DurationSeconds=12m;
        mage.BaseSpellHitPercent=100m;
        mage.Character.Equipment=Gear(("main-hand",279259));
        var mageRun=new MageSimRunner().Run(mage);
        Assert.Equal(mageRun.DamageDone,new MageSimRunner().Run(mage).DamageDone);
        var warlock=WarlockSimRequest.CreatePreset("demonology");
        warlock.DurationSeconds=20m;
        warlock.BaseSpellHitPercent=100m;
        warlock.PetType="imp";
        warlock.PrePullSacrificedDemon=null;
        warlock.Character.Equipment=Gear(("main-hand",279259));
        var result=new WarlockSimRunner().Run(warlock);
        Assert.True(result.PetDamage>=0m);
        Assert.Equal(result.DamageDone,result.WarlockDamage+result.PetDamage);
    }

    [Fact]
    public void ReviewedSourceRecordsAreResolvedByExistingRuntimeCatalogAndApi()
    {
        var catalog = ForeverEquipmentCatalog.GetCatalog();
        Assert.Equal(58, catalog.Items.Count);
        Assert.Equal("forever-1.60.1.70707-cp172-effects-seed-b7", catalog.CatalogVersion);
        Assert.Equal("1.60.1.70532", ForeverEquipmentCatalog.FindItem(22496).SourceBuild);
        Assert.Equal("published-client-tooltip", ForeverEquipmentCatalog.FindItem(20368).VerificationStatus);
        var api = new ForeverEquipmentController();
        Assert.IsType<OkObjectResult>(api.Get(22504));
        var page = Assert.IsType<OkObjectResult>(api.Items(search: "Plagueheart", page: 1, pageSize: 20));
        using var content = JsonDocument.Parse(JsonSerializer.Serialize(page.Value));
        Assert.True(content.RootElement.GetProperty("total").GetInt32() >= 9);
        Assert.IsType<NotFoundObjectResult>(api.Get(99999999));
    }

    [Fact]
    public void NewCasterChestChangesMageActualDamageAndKeepsManaStatsInResolver()
    {
        var raw = ForeverSharedEquipmentResolver.Resolve(Gear(("chest", 22496)), "mage");
        Assert.Equal(47m, raw.Stats.Get("spell-power"));
        Assert.Equal(27m, raw.RawStats.Get("intellect"));
        MageSimRequest Setup()
        {
            var request = MageSimRequest.CreatePreset("fire");
            request.DurationSeconds = 15m;
            request.TargetMaximumHealth = 1000000m;
            request.BaseSpellHitPercent = 100m;
            request.SpellCritPercent = 0m;
            return request;
        }
        var plain = Setup();
        var geared = Setup();
        geared.Character.Equipment = Gear(("chest",22496));
        var before = new MageSimRunner().Run(plain);
        var after = new MageSimRunner().Run(geared);
        Assert.Equal(before.SpellPower + 47m, after.SpellPower);
        Assert.True(after.DamageDone > before.DamageDone);
        Assert.Equal(after.DamageDone, new MageSimRunner().Run(geared).DamageDone);
        Assert.Empty(geared.Character.Equipment.Items[0].Stats.Values);
    }

    [Fact]
    public void ImportedWarlockChestImprovesRealOwnerAndDemonDamage()
    {
        WarlockSimRequest Setup()
        {
            var request = WarlockSimRequest.CreatePreset("demonology");
            request.DurationSeconds = 35m;
            request.TargetMaximumHealth = 1000000m;
            request.BaseSpellHitPercent = 100m;
            request.SpellCritPercent = 0m;
            request.PetType = "imp";
            request.PrePullSacrificedDemon = null;
            request.UseLifeTap = false;
            return request;
        }
        var plain = Setup();
        var geared = Setup();
        geared.Character.Equipment = Gear(("chest",22504));
        var before = new WarlockSimRunner().Run(plain);
        var after = new WarlockSimRunner().Run(geared);
        Assert.Equal(before.SpellPower + 51m, after.SpellPower);
        Assert.True(after.WarlockDamage > before.WarlockDamage);
        Assert.True(after.PetDamage > before.PetDamage);
        Assert.Equal(after.DamageDone, after.WarlockDamage + after.PetDamage);
    }

    [Fact]
    public void ImportedBowChangesRealHunterAutoShotInterval()
    {
        MarksmanshipHunterSimRequest Setup()
        {
            var request = new MarksmanshipHunterSimRequest {
                DurationSeconds = 14m, TargetMaximumHealth = 1000000m,
                HasPet = false, UseRapidFire = false, UseAimedShot = false,
                UseSniperShot = false, UseMultiShot = false,
                UseArcaneShot = false, UseSerpentSting = false,
                CaptureTimeline = true
            };
            request.Tuning.HitChancePercent = 100m;
            request.Tuning.CriticalChancePercent = 0m;
            return request;
        }
        var one = Setup();
        one.Equipment = Gear(("ranged",20368));
        var two = Setup();
        two.Equipment = Gear(("ranged",16622));
        var first = new MarksmanshipHunterSimRunner().Run(one);
        var second = new MarksmanshipHunterSimRunner().Run(two);
        decimal Gap(MarksmanshipHunterSimResponse response)
        {
            var hits = response.Run.Timeline
                .Where(x => x.Type == CombatEventType.Damage && x.AbilityKey == "auto-shot" && x.Amount > 0m)
                .Select(x => x.TimeSeconds).Take(2).ToArray();
            Assert.Equal(2,hits.Length);
            return hits[1] - hits[0];
        }
        Assert.True(Gap(first) < Gap(second));
        Assert.Equal(1.5m, ForeverSharedEquipmentResolver.Resolve(one.Equipment,"hunter").Weapons["ranged"].SpeedSeconds);
        Assert.NotEqual(first.DamageDone,second.DamageDone);
        Assert.Equal(first.DamageDone, new MarksmanshipHunterSimRunner().Run(one).DamageDone);
    }

    [Fact]
    public void PublishedCryptstalkerArmorChangesActualPetIncomingMitigation()
    {
        MarksmanshipHunterSimRequest Setup()
        {
            var request = new MarksmanshipHunterSimRequest {
                HasPet = true, PetFamily = "bear", DurationSeconds = 12m,
                TargetMaximumHealth = 1000000m, UseRapidFire = false,
                PetIncomingHitDamage = 600m, PetIncomingHitEverySeconds = 3m,
                CaptureTimeline = true
            };
            request.PetTuning.PetAvoidancePercent = 0m;
            request.PetTuning.PetHitPercent = 100m;
            return request;
        }
        var plain = Setup();
        var selected = Setup();
        selected.Equipment = Gear(("chest",22436));
        var before = new MarksmanshipHunterSimRunner().Run(plain);
        var after = new MarksmanshipHunterSimRunner().Run(selected);
        decimal PetHit(MarksmanshipHunterSimResponse result) =>
            result.Run.Timeline.First(x =>
                x.Type == CombatEventType.Damage &&
                x.AbilityKey == "scripted-pet-incoming-hit" &&
                x.TargetActorKey == "hunter-pet").Amount!.Value;
        Assert.True(PetHit(after) < PetHit(before));
        Assert.Equal(576m, ForeverSharedEquipmentResolver.Resolve(selected.Equipment,"hunter").RawStats.Get("armor"));
        Assert.True(after.PetDamage > 0m);
    }

    [Fact]
    public void CompleteFrostfireSetReconcilesWithPublishedClientStaticTotals()
    {
        var all = Gear(
            ("head",22498), ("wrist",22503), ("waist",22502),
            ("hands",22501), ("legs",22497), ("chest",22496),
            ("feet",22500), ("shoulder",22499), ("finger1",23062));
        var result = ForeverSharedEquipmentResolver.Resolve(all,"mage");
        Assert.Equal(764m,result.RawStats.Get("armor"));
        Assert.Equal(49m,result.RawStats.Get("spirit"));
        Assert.Equal(162m,result.RawStats.Get("stamina"));
        Assert.Equal(177m,result.RawStats.Get("intellect"));
        Assert.Equal(313m,result.RawStats.Get("spell-power"));
        Assert.Equal(4m,result.RawStats.Get("spell-hit-chance-percent"));
        Assert.Equal(5m,result.RawStats.Get("spell-critical-chance-percent"));
        Assert.DoesNotContain(result.Loadout.Items.SelectMany(x=>x.Modifiers),
            x=>x.ModifierType=="set-bonus");
    }

    [Fact]
    public void CraftedBloodvineAndArchmageOnUseRecordsStaySourceAware()
    {
        var caster = ForeverSharedEquipmentResolver.Resolve(
            Gear(("chest",19682),("legs",19683),("feet",19684)),"warlock");
        Assert.Equal(83m,caster.RawStats.Get("spell-power"));
        Assert.Equal(4m,caster.RawStats.Get("spell-hit-chance-percent"));
        Assert.DoesNotContain(caster.Loadout.Items.SelectMany(x=>x.Modifiers),x=>x.ModifierType=="set-bonus");
        var robe = ForeverEquipmentCatalog.FindItem(14152);
        var linked = Assert.Single(robe.Effects);
        Assert.Equal(18385, linked.SpellId);
        Assert.Equal("on-use", linked.Kind);
        Assert.Equal("modeled",linked.ExecutionStatus); // CP172 implements the linked on-use mana effect.
        Assert.Equal(40m,ForeverSharedEquipmentResolver.Resolve(
            Gear(("chest",14152)),"mage",60).RawStats.Get("spell-power"));
        Assert.Throws<ArgumentException>(() => ForeverSharedEquipmentResolver.Resolve(
            Gear(("chest",14152)),"mage",56));
    }

    [Fact]
    public void SetLinksAreStoredButUnmodeledProcBonusesDoNotBecomeStaticStats()
    {
        var items = Gear(("waist",22510),("wrist",22511));
        var loaded = ForeverSharedEquipmentResolver.Resolve(items,"warlock");
        Assert.Equal(34m+23m,loaded.RawStats.Get("spell-power"));
        Assert.DoesNotContain(loaded.Loadout.Items.SelectMany(x => x.Modifiers),x => x.ModifierType=="set-bonus");
        Assert.Contains(ForeverEquipmentCatalog.GetCatalog().Sets,x =>
            x.Key=="plagueheart-raiment" && x.RequiredPieces==2 && !x.Modeled);
        Assert.Throws<ArgumentException>(() =>
            ForeverSharedEquipmentResolver.Resolve(Gear(("chest",22504)),"mage"));
        Assert.Throws<ArgumentException>(() =>
            ForeverSharedEquipmentResolver.Resolve(Gear(("chest",22496)),"hunter"));
        Assert.Throws<ArgumentException>(() =>
            ForeverSharedEquipmentResolver.Resolve(Gear(("chest",99999999)),"mage"));
    }
}
