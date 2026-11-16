using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Characters.Equipment;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175SharedFoodEnhancements
{
    [Fact]
    public void FoodStatAndLifetimeAreSharedBetweenCharacterAndHunterAdapters()
    {
        var selected=new ForeverExternalModifierSelection {
            Consumables=["runn-tum-tuber-surprise"]
        };
        var raw=ForeverExternalModifierCatalog.SumSelfRawStats(selected,60,900m);
        Assert.Equal(15m,raw["intellect"]);
        var mage=MageSimRequest.CreatePreset("fire");
        mage.DurationSeconds=120m;
        var augmented=ForeverExternalModifierCatalog.ApplyToProfile(
            mage.Character,selected,mage.DurationSeconds);
        Assert.Single(augmented.Buffs.Selections,x=>x.BuffKey=="cp173-runn-tum-tuber-surprise"
            && x.Stats.Get("intellect")==15m);
        Assert.Empty(mage.Character.Buffs.Selections);
        var hunter=new MarksmanshipHunterSimRequest { DurationSeconds=120m };
        hunter.ExternalModifiers=selected;
        var adjusted=ForeverExternalHunterStatAdapter.Apply(hunter);
        Assert.Equal(15m,adjusted.Tuning.Intellect-hunter.Tuning.Intellect);
        Assert.Equal(225m,adjusted.Tuning.MaximumMana-hunter.Tuning.MaximumMana);
        Assert.Throws<ArgumentException>(()=>
            ForeverExternalModifierCatalog.Resolve(selected,60,901m));
    }

    [Fact]
    public void TwoFoodEffectsRejectButIndependentForeverElixirsStack()
    {
        var selected=new ForeverExternalModifierSelection {
            Consumables=["smoked-desert-dumplings","elixir-of-the-mongoose",
                "greater-arcane-elixir","elixir-of-the-sages"]
        };
        var totals=ForeverExternalModifierCatalog.SumSelfRawStats(selected,60,120m);
        Assert.Equal(20m,totals["strength"]);
        Assert.Equal(25m,totals["agility"]);
        Assert.Equal(35m,totals["spell-power"]);
        Assert.Equal(18m,totals["intellect"]);
        selected.Consumables.Add("nightfin-soup");
        Assert.Throws<ArgumentException>(()=>
            ForeverExternalModifierCatalog.Resolve(selected,60,120m));
        selected.Consumables.Remove("nightfin-soup");
        selected.Consumables.Add("elixir-of-greater-agility");
        Assert.Throws<ArgumentException>(()=>
            ForeverExternalModifierCatalog.Resolve(selected,60,120m));
    }

    [Fact]
    public void AlreadyAppliedSharedBuffIsNotDoubleCountedOnRepeatImport()
    {
        var mage=MageSimRequest.CreatePreset("fire");
        var selected=new ForeverExternalModifierSelection{
            Buffs=["arcane-intellect"]
        };
        var once=ForeverExternalModifierCatalog.ApplyToProfile(mage.Character,selected,120m);
        Assert.Single(once.Buffs.Selections,x=>x.BuffKey=="cp173-arcane-intellect");
        Assert.Throws<ArgumentException>(()=>
            ForeverExternalModifierCatalog.ApplyToProfile(once,selected,120m));
        Assert.Empty(mage.Character.Buffs.Selections);
    }

    [Fact]
    public void PermanentAndTemporaryWeaponEnchantRetainSeparateSlots()
    {
        var loadout=new CharacterEquipmentLoadout {Items=[
            new CharacterEquipmentItem {
                SlotKey="main-hand",ItemId=19102,
                EnchantSpellId=1219580,TemporaryEnchantSpellId=25122
            }
        ]};
        var result=ForeverSharedEquipmentResolver.Resolve(loadout,"mage",60,"orc");
        var item=Assert.Single(result.Loadout.Items);
        Assert.Equal(1219580,item.EnchantSpellId);
        Assert.Equal(25122,item.TemporaryEnchantSpellId);
        Assert.Single(item.Modifiers,x=>x.PlacementKey=="permanent");
        Assert.Single(item.Modifiers,x=>x.PlacementKey=="temporary");
        // Removing the oil never deletes the permanent enchant selection.
        loadout.Items[0].TemporaryEnchantSpellId=null;
        var permanentOnly=ForeverSharedEquipmentResolver.Resolve(loadout,"mage",60,"orc");
        Assert.Equal(1219580,Assert.Single(permanentOnly.Loadout.Items).EnchantSpellId);
        Assert.DoesNotContain(Assert.Single(permanentOnly.Loadout.Items).Modifiers,
            x=>x.PlacementKey=="temporary");
    }
}
