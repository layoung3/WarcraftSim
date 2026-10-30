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

    [Fact]
    public void ReviewedSourceRecordsAreResolvedByExistingRuntimeCatalogAndApi()
    {
        var catalog = ForeverEquipmentCatalog.GetCatalog();
        Assert.Equal(53, catalog.Items.Count);
        Assert.Equal("forever-cp171-reviewed-mixed-2026-10-30-b2", catalog.CatalogVersion);
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
        Assert.Equal("not-modeled",linked.ExecutionStatus);
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
