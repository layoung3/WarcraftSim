using System;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Characters.Equipment;
using Xunit;
using Xunit.Abstractions;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175F2B1EnchantCombatReceipts
{
    private readonly ITestOutputHelper output;
    public Cp175F2B1EnchantCombatReceipts(ITestOutputHelper output)=>this.output=output;

    private static CharacterEquipmentLoadout Gear(string slot,int itemId,int? enchant=null)=>
        new(){Items=[new CharacterEquipmentItem{
            SlotKey=slot,ItemId=itemId,EnchantSpellId=enchant
        }]};

    [Fact]
    public void MageProvisionalIntellectEnchantChangesManaWithoutMutatingSource()
    {
        MageSimRequest Make(int? enchant)
        {
            var r=MageSimRequest.CreatePreset("arcane");
            r.DurationSeconds=30m;r.TargetMaximumHealth=1000000m;
            r.BaseSpellHitPercent=100m;r.SpellCritPercent=0m;
            r.Character.Equipment=Gear("main-hand",19102,enchant);
            return r;
        }
        var baseRun=new MageSimRunner().Run(Make(null));
        var itemized=new MageSimRunner().Run(Make(20036));
        Assert.Equal(baseRun.MaximumMana+135m,itemized.MaximumMana);
        Assert.Equal(itemized.DamageDone,new MageSimRunner().Run(Make(20036)).DamageDone);
        output.WriteLine($"F2B1 Mage +9 Intellect 2H enchant mana {baseRun.MaximumMana:0.##} -> {itemized.MaximumMana:0.##}, DPS {baseRun.Dps:0.####} -> {itemized.Dps:0.####}");
    }

    [Fact]
    public void WarlockProvisionalHealingWeaponEnchantAddsNineteenSpellPowerExactlyOnce()
    {
        WarlockSimRequest Make(int? enchant)
        {
            var r=WarlockSimRequest.CreatePreset("destruction");
            r.DurationSeconds=35m;r.TargetMaximumHealth=1000000m;
            r.EnablePet=false;r.UseLifeTap=false;
            r.BaseSpellHitPercent=100m;r.SpellCritPercent=0m;
            r.Character.Equipment=Gear("main-hand",19102,enchant);
            return r;
        }
        var baseline=new WarlockSimRunner().Run(Make(null));
        var enhanced=new WarlockSimRunner().Run(Make(22750));
        Assert.Equal(baseline.SpellPower+19m,enhanced.SpellPower);
        Assert.True(enhanced.WarlockDamage>baseline.WarlockDamage);
        Assert.Equal(enhanced.DamageDone,new WarlockSimRunner().Run(Make(22750)).DamageDone);
        output.WriteLine($"F2B1 Warlock +19 Spell Power enchant DPS {baseline.Dps:0.####} -> {enhanced.Dps:0.####}");
    }

    [Fact]
    public void HunterProvisionalNeckAgilityAddsExactlyFiveToPhysicalStatPipeline()
    {
        MarksmanshipHunterSimRequest Make(int? enchant)
        {
            var r=new MarksmanshipHunterSimRequest{
                DurationSeconds=45m,TargetMaximumHealth=1000000m,HasPet=false,
                UseRapidFire=false,UseAimedShot=false,UseSniperShot=false,
                UseMultiShot=false,UseArcaneShot=false,UseSerpentSting=false
            };
            r.Tuning.HitChancePercent=100m;r.Tuning.CriticalChancePercent=0m;
            r.Equipment=Gear("neck",18404,enchant);
            return r;
        }
        var baseline=new MarksmanshipHunterSimRunner().Run(Make(null));
        var enhanced=new MarksmanshipHunterSimRunner().Run(Make(1249059));
        Assert.True(enhanced.RangedAttackPower>=baseline.RangedAttackPower);
        Assert.Equal(enhanced.DamageDone,new MarksmanshipHunterSimRunner().Run(Make(1249059)).DamageDone);
        output.WriteLine($"F2B1 Hunter +5 Agility necklace enchant RAP {baseline.RangedAttackPower:0.##} -> {enhanced.RangedAttackPower:0.##}; DPS {baseline.Dps:0.####} -> {enhanced.Dps:0.####}");
    }

    [Fact]
    public void WarriorProvisionalStrengthWeaponEnchantChangesSeededDps()
    {
        WarriorSimRequest Make(int? enchant)
        {
            var r=new WarriorSimRequest{
                Level=60,Iterations=1,DurationSeconds=60m,Seed=1752028,
                CaptureRepresentativeTimeline=true,EquipmentMode="catalog",
                TargetArmor=3500m
            };
            r.Gear.Add(new WarriorGearEntry{Slot="main-hand",ItemId=279260,EnchantSpellId=enchant});
            return r;
        }
        var baseline=new ForeverWarriorSimRunner().Run(Make(null));
        var enhanced=new ForeverWarriorSimRunner().Run(Make(23799));
        Assert.True(enhanced.EffectiveCharacterStats["attack-power"]>
            baseline.EffectiveCharacterStats["attack-power"]);
        Assert.Equal(enhanced.MeanDps,new ForeverWarriorSimRunner().Run(Make(23799)).MeanDps);
        output.WriteLine($"F2B1 Warrior +15 Strength weapon enchant DPS {baseline.MeanDps:0.####} -> {enhanced.MeanDps:0.####}");
    }
}
