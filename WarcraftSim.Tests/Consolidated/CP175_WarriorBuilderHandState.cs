using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175WarriorBuilderHandState
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    private static CharacterEquipmentLoadout Gear(params (string Slot,int Id)[] items)=>new() {
        Items=items.Select(x=>new CharacterEquipmentItem{SlotKey=x.Slot,ItemId=x.Id}).ToList()
    };
    private static WarriorSimRequest Manual()=>new() {
        Preset="fury",TalentMode="tree",Race="human",IsTwoHanded=false,
        MainHandMinimumDamage=91m,MainHandMaximumDamage=123m,MainHandSpeedSeconds=2.3m,
        OffHandMinimumDamage=37m,OffHandMaximumDamage=52m,OffHandSpeedSeconds=1.6m,
        MainHandWeaponType="mace",OffHandWeaponType="axe",PlayerBlockValue=75m,
        ShieldIntrinsicBlockValue=12m,DurationSeconds=12m,Iterations=1,StartingRage=100m,
        CaptureRepresentativeTimeline=true
    };
    private static WarriorSimRequest Prepare(WarriorSimRequest input,CharacterEquipmentLoadout gear)
    {
        var result=Assert.IsType<OkObjectResult>(new ForeverWarriorBuilderController().Prepare(new(){Warrior=input,Equipment=gear}));
        return JsonSerializer.SerializeToElement(result.Value,Json).GetProperty("warrior").Deserialize<WarriorSimRequest>(Json)!;
    }
    private static void SameManualHands(WarriorSimRequest expected,WarriorSimRequest actual)
    {
        Assert.Equal(expected.MainHandMinimumDamage,actual.MainHandMinimumDamage);
        Assert.Equal(expected.MainHandMaximumDamage,actual.MainHandMaximumDamage);
        Assert.Equal(expected.MainHandSpeedSeconds,actual.MainHandSpeedSeconds);
        Assert.Equal(expected.MainHandWeaponType,actual.MainHandWeaponType);
        Assert.Equal(expected.IsTwoHanded,actual.IsTwoHanded);Assert.Equal(expected.DualWield,actual.DualWield);
        Assert.Equal(expected.OffHandMinimumDamage,actual.OffHandMinimumDamage);
        Assert.Equal(expected.OffHandMaximumDamage,actual.OffHandMaximumDamage);
        Assert.Equal(expected.OffHandSpeedSeconds,actual.OffHandSpeedSeconds);
        Assert.Equal(expected.OffHandWeaponType,actual.OffHandWeaponType);
        Assert.Equal(expected.ShieldEquipped,actual.ShieldEquipped);
        Assert.Equal(expected.ShieldIntrinsicBlockValue,actual.ShieldIntrinsicBlockValue);
        Assert.Equal(expected.PlayerBlockValue,actual.PlayerBlockValue);
    }

    [Theory]
    [InlineData(false,false)][InlineData(false,true)][InlineData(true,false)][InlineData(true,true)]
    public void RemovingCatalogHandsRestoresManualInputsAndRemovesOffHandAttacks(bool fullHands,bool keepArmor)
    {
        var original=Manual();
        var equipment=fullHands?Gear(("main-hand",279259),("off-hand",19168)):Gear(("off-hand",279259));
        var prepared=Prepare(original,equipment);
        Assert.True(prepared.DualWield);
        var repeat=Prepare(prepared,equipment);
        Assert.Equal(JsonSerializer.Serialize(prepared,Json),JsonSerializer.Serialize(repeat,Json));
        var removed=Prepare(repeat,keepArmor?Gear(("hands",15063)):Gear());
        SameManualHands(original,removed);
        var run=new ForeverWarriorSimRunner().Run(removed);
        Assert.DoesNotContain(run.Batch.RepresentativeRun.Timeline,x=>x.AbilityKey=="off-hand-auto"&&x.Type==CombatEventType.Damage);
        Assert.Null(original.BuilderHandState);Assert.Empty(original.Gear);
    }

    [Fact]
    public void ReplacingCatalogOffHandUsesReplacementThenRestoresOriginalOnRemoval()
    {
        var original=Manual();var first=Prepare(original,Gear(("off-hand",279259)));
        var replaced=Prepare(first,Gear(("off-hand",19168)));
        Assert.Equal(65m,replaced.OffHandMinimumDamage);Assert.Equal(121m,replaced.OffHandMaximumDamage);
        Assert.Equal(1.8m,replaced.OffHandSpeedSeconds);
        SameManualHands(original,Prepare(replaced,Gear()));
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public void GenuineManualShieldOrDualWieldSurvivesRepeatedPreparationAndCatalogRemoval(bool shield)
    {
        var original=Manual();original.ShieldEquipped=shield;original.DualWield=!shield;
        var initial=Prepare(original,Gear());SameManualHands(original,Prepare(initial,Gear()));
        var catalog=Prepare(initial,Gear(("main-hand",279259),("off-hand",19168)));
        Assert.False(catalog.ShieldEquipped);Assert.True(catalog.DualWield);
        var removed=Prepare(catalog,Gear());SameManualHands(original,removed);
        Assert.Equal(75m,removed.PlayerBlockValue);
        Assert.Equal(75m,Prepare(removed,Gear()).PlayerBlockValue);
    }

    [Fact]
    public void ExplicitEditsToPreparedManualInputsRemainEditable()
    {
        var prepared=Prepare(Manual(),Gear());
        prepared.MainHandMinimumDamage=101m;prepared.MainHandMaximumDamage=131m;
        prepared.ShieldEquipped=true;prepared.PlayerBlockValue=95m;
        var edited=Prepare(prepared,Gear());SameManualHands(prepared,edited);
        SameManualHands(edited,Prepare(edited,Gear()));
        Assert.Equal(95m,edited.BuilderHandState!.ManualInputs.PlayerBlockValue);
    }

    [Fact]
    public void ExplicitManualEditsNeverOverrideCurrentCatalogButSurviveItsRemoval()
    {
        var prepared=Prepare(Manual(),Gear(("main-hand",279259),("off-hand",19168)));
        prepared.MainHandMinimumDamage=201m;prepared.MainHandMaximumDamage=251m;
        prepared.PlayerBlockValue=99m;
        var stillCatalog=Prepare(prepared,Gear(("main-hand",279259),("off-hand",19168)));
        Assert.Equal(79m,stillCatalog.MainHandMinimumDamage);Assert.Equal(148m,stillCatalog.MainHandMaximumDamage);
        var removed=Prepare(stillCatalog,Gear());Assert.Equal(201m,removed.MainHandMinimumDamage);
        Assert.Equal(251m,removed.MainHandMaximumDamage);Assert.Equal(99m,removed.PlayerBlockValue);
        Assert.False(removed.DualWield);
    }

    [Fact]
    public void NormalizedSlotKeysAndEnhancementIdentityAreAuthoritative()
    {
        var canonical=Gear(("main-hand",279259),("off-hand",19168));
        canonical.Items[0].EnchantSpellId=20031;
        var noncanonical=Gear((" MAIN-HAND ",279259),("OFF-HAND",19168));
        noncanonical.Items[0].EnchantSpellId=20031;
        var expected=Prepare(Manual(),canonical);var actual=Prepare(Manual(),noncanonical);
        Assert.Equal(JsonSerializer.Serialize(expected,Json),JsonSerializer.Serialize(actual,Json));
        Assert.Equal(JsonSerializer.Serialize(actual,Json),JsonSerializer.Serialize(Prepare(actual,noncanonical),Json));
        Assert.Equal(84m,actual.MainHandMinimumDamage);
        // Preserve the real temporary ID through normalization, without pretending
        // the Warrior stat pipeline implements the caster-only oil's stats.
        noncanonical.Items[0].TemporaryEnchantSpellId=25122;
        var normalized=ForeverSharedEquipmentResolver.Resolve(noncanonical,"warrior",60,"human");
        Assert.Equal(25122,normalized.Loadout.Items.Single(x=>x.SlotKey=="main-hand").TemporaryEnchantSpellId);
        Assert.IsType<BadRequestObjectResult>(new ForeverWarriorBuilderController().Prepare(new(){Warrior=Manual(),Equipment=noncanonical}));
    }

    [Fact]
    public void RawCatalogRequestsRemainSupportedButUnrecoverableLegacyPreparedHandsFailClosed()
    {
        var raw=Manual();raw.EquipmentMode="catalog";raw.Gear=[new(){Slot="main-hand",ItemId=279259}];
        var current=Prepare(raw,Gear(("main-hand",279259)));
        Assert.Equal(79m,current.MainHandMinimumDamage);
        current.BuilderHandState=null;
        var invalid=Assert.IsType<BadRequestObjectResult>(new ForeverWarriorBuilderController().Prepare(new(){Warrior=current,Equipment=Gear()}));
        Assert.Contains("no original manual inputs",JsonSerializer.Serialize(invalid.Value));
        // Explicit reset/rebase is a fresh manual declaration, never a guessed original.
        current.Gear=[];current.EquipmentMode="manual";current.MainHandMinimumDamage=88m;
        var rebased=Prepare(current,Gear());Assert.Equal(88m,rebased.MainHandMinimumDamage);
        Assert.Equal(88m,rebased.BuilderHandState!.ManualInputs.MainHandMinimumDamage);
    }

    [Fact]
    public void ExplicitFullRebaseCanAdoptLastResolvedCatalogNumbers()
    {
        var catalog=Prepare(Manual(),Gear(("main-hand",279259)));
        Assert.Equal(79m,catalog.MainHandMinimumDamage);
        Assert.Equal(91m,Prepare(catalog,Gear()).MainHandMinimumDamage);
        catalog.Gear=[];catalog.EquipmentMode="manual";catalog.BuilderHandState=null;
        var rebased=Prepare(catalog,Gear());
        Assert.Equal(79m,rebased.MainHandMinimumDamage);
        Assert.Equal(79m,rebased.BuilderHandState!.ManualInputs.MainHandMinimumDamage);
    }

    [Fact]
    public void HistoricalShieldFixtureRestoresManualBlockWithoutPromotingItToSharedCatalog()
    {
        var manual=Prepare(Manual(),Gear());
        var source=ForeverEquipmentCatalog.FindItem(12602);
        // The existing native historical fixture can resolve shield block, but
        // remains deliberately rejected by the shared level-60 item picker.
        manual.EquipmentMode="catalog";
        manual.Gear=[new(){Slot="main-hand",ItemId=279259},new(){Slot="off-hand",ItemId=12602}];
        var legacy=ForeverEquipmentCatalog.ResolveWarriorLoadout(manual);
        Assert.Equal(75m+source.ShieldBlockValue,legacy.PlayerBlockValue);
        legacy.BuilderHandState=legacy.BuilderHandState! with {
            LastResolvedInputs=legacy.BuilderHandState!.LastResolvedInputs with {
                MainHandMinimumDamage=legacy.MainHandMinimumDamage,MainHandMaximumDamage=legacy.MainHandMaximumDamage,
                MainHandSpeedSeconds=legacy.MainHandSpeedSeconds,MainHandWeaponType=legacy.MainHandWeaponType,
                ShieldEquipped=true,ShieldIntrinsicBlockValue=source.ShieldBlockValue,PlayerBlockValue=legacy.PlayerBlockValue,
                OffHandWeaponType=legacy.OffHandWeaponType
            }
        };
        var removed=Prepare(legacy,Gear());Assert.Equal(75m,removed.PlayerBlockValue);
        Assert.False(removed.ShieldEquipped);Assert.Equal(12m,removed.ShieldIntrinsicBlockValue);
        Assert.Equal(75m,Prepare(removed,Gear()).PlayerBlockValue);
        Assert.IsType<BadRequestObjectResult>(new ForeverWarriorBuilderController().Prepare(new(){
            Warrior=removed,Equipment=Gear(("main-hand",279259),("off-hand",12602))
        }));
    }
}
