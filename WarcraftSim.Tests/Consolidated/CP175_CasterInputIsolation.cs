using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Items;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175CasterInputIsolation
{
    private static MageSimRequest Mage()
    {
        var request=MageSimRequest.CreatePreset("fire");
        request.DurationSeconds=12m;
        request.UseEvocation=false;
        request.UseCombustion=false;
        request.UseMajorManaPotion=false;
        request.StartingManaPercent=10m;
        request.CustomPriority=["fireball"];
        request.CombatItems=new CombatUseItemsOptions();
        return request;
    }

    [Fact]
    public void LegacyPotionAdaptationCannotStockCallerInventoryOrSurviveDeselection()
    {
        var request=Mage();
        request.UseMajorManaPotion=true;
        request.MajorManaPotionCharges=1;
        request.MajorManaPotionBelowManaPercent=90m;
        var original=JsonSerializer.Serialize(request);
        var selected=new MageSimRunner().Run(request);
        Assert.Equal(1,selected.MajorManaPotionUses);
        Assert.Equal(original,JsonSerializer.Serialize(request));

        request.UseMajorManaPotion=false;
        var disabled=new MageSimRunner().Run(request);
        Assert.Equal(0,disabled.MajorManaPotionUses);
        Assert.Equal(0m,disabled.ManaRestoredByPotions);
        Assert.DoesNotContain(disabled.Run.Timeline,e=>
            e.AbilityKey==ForeverCombatItemCatalog.MajorManaPotion);
    }

    [Fact]
    public void ExplicitSharedPotionInventoryRemainsIndependentOfLegacyToggle()
    {
        var request=Mage();
        request.CombatItems.MajorManaPotionCharges=1;
        request.CombatItems.MajorManaPotionBelowManaPercent=90m;
        var before=JsonSerializer.Serialize(request);
        var first=new MageSimRunner().Run(request);
        var second=new MageSimRunner().Run(request);
        Assert.Equal(1,first.MajorManaPotionUses);
        Assert.Equal(1,second.MajorManaPotionUses);
        Assert.Equal(first.ManaRestoredByPotions,second.ManaRestoredByPotions);
        Assert.Equal(first.DamageDone,second.DamageDone);
        Assert.Equal(before,JsonSerializer.Serialize(request));
    }

    [Fact]
    public void MageHealthAndDamageReplayWithoutCompoundingSelectedStamina()
    {
        var request=Mage();
        request.StartingManaPercent=100m;
        request.Character.BaseStats.Set("stamina",100m);
        request.Character.BaseStats.Set("health",250m);
        var before=JsonSerializer.Serialize(request);
        var first=new MageSimRunner().Run(request);
        var second=new MageSimRunner().Run(request);
        Assert.Equal(4250m,first.EndingHealth);
        Assert.Equal(first.EndingHealth,second.EndingHealth);
        Assert.Equal(first.DamageDone,second.DamageDone);
        Assert.Equal(first.EndingMana,second.EndingMana);
        Assert.Equal(before,JsonSerializer.Serialize(request));
    }

    [Fact]
    public void WarlockHealthLimitedLifeTapReplaysWithoutCompoundingStamina()
    {
        var request=WarlockSimRequest.CreatePreset("affliction");
        request.DurationSeconds=120m;
        request.EnablePet=false;
        request.Character.BaseStats.Set("stamina",100m);
        request.Character.BaseStats.Set("health",250m);
        request.StartingManaPercent=10m;
        request.AssumeExternalHealingForLifeTap=false;
        request.LifeTapAboveHealthPercent=20m;
        request.UseLifeTap=true;
        request.CustomPriority=["shadow-bolt"];
        request.Tuning.ManaRegenerationPerSecond=0m;
        request.Character.BaseStats.Set("mp5",0m);
        var before=JsonSerializer.Serialize(request);
        var first=new WarlockSimRunner().Run(request);
        var second=new WarlockSimRunner().Run(request);
        Assert.True(first.LifeTapCasts>0);
        Assert.Equal(first.LifeTapCasts,second.LifeTapCasts);
        Assert.Equal(first.EndingHealth,second.EndingHealth);
        Assert.Equal(first.EndingMana,second.EndingMana);
        Assert.Equal(first.DamageDone,second.DamageDone);
        Assert.Equal(before,JsonSerializer.Serialize(request));
    }
}
