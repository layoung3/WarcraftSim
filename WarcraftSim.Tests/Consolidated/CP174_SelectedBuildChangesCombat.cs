using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Data.Forever.Items;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>Numerical differences are produced by real event-queue execution, never UI math.</summary>
public sealed class Cp174SelectedBuildChangesCombatTests
{
    [Fact]
    public void FireMageAccelerantRankAndEquippedWeaponChangeRealDamage()
    {
        MageSimRequest Build(int rank,bool equip)
        {
            var typed=new MageSimRequest();
            var request=Cp174ItemizedPresetFixture.Apply(new ForeverLevel60SimulationRequest{
                ClassKey="mage",Specialization="fire",Mage=typed
            });
            typed.Specialization="fire";
            typed.Character.Race="human";
            typed.Character.SpecializationKey="fire";
            typed.UseProvisionalLevel60Baseline=true;
            typed.DurationSeconds=100m;
            typed.TargetMaximumHealth=9000000m;
            typed.Seed=174701;
            typed.CustomPriority=["fireball"];
            typed.Talents.Ranks["accelerant"]=rank;
            if(equip)typed.Character.Equipment=new CharacterEquipmentLoadout{
                Items=[new CharacterEquipmentItem{SlotKey="main-hand",ItemId=19102}]
            };
            return typed;
        }
        var fast=new MageSimRunner().Run(Build(5,false));
        var slow=new MageSimRunner().Run(Build(0,false));
        Assert.NotEqual(fast.DamageDone,slow.DamageDone);
        var geared=new MageSimRunner().Run(Build(5,true));
        Assert.NotEqual(geared.SpellPower,fast.SpellPower);
        Assert.NotEqual(geared.DamageDone,fast.DamageDone);
    }

    [Fact]
    public void WarlockBaneRankChangesActualSpellCastingPerformance()
    {
        WarlockSimRequest Build(int rank)
        {
            var typed=WarlockSimRequest.CreatePreset("destruction");
            Cp174ItemizedPresetFixture.Apply(new ForeverLevel60SimulationRequest{
                ClassKey="warlock",Specialization="destruction",Warlock=typed
            });
            typed.Character.Race="orc";
            typed.UseProvisionalLevel60Baseline=true;
            typed.DurationSeconds=100m;
            typed.TargetMaximumHealth=9000000m;
            typed.Seed=174702;
            typed.CustomPriority=["shadow-bolt"];
            typed.Talents.Ranks["bane"]=rank;
            return typed;
        }
        var a=new WarlockSimRunner().Run(Build(5));
        var b=new WarlockSimRunner().Run(Build(0));
        Assert.NotEqual(a.WarlockDamage,b.WarlockDamage);
    }

    [Fact]
    public void HunterLoneWolfRankChangesRealDamageAndConsumableProducesEvents()
    {
        MarksmanshipHunterSimRequest Build(int rank,bool dynamite)
        {
            var typed=new MarksmanshipHunterSimRequest();
            Cp174ItemizedPresetFixture.Apply(new ForeverLevel60SimulationRequest{
                ClassKey="hunter",Specialization="marksmanship",Marksmanship=typed
            });
            typed.DurationSeconds=65m;
            typed.TargetMaximumHealth=9000000m;
            typed.Seed=174703;
            typed.HasPet=false;
            typed.Talents.Ranks["lone-wolf"]=rank;
            if(dynamite)typed.CombatItems=new CombatUseItemsOptions{
                EngineeringSkill=250,DenseDynamiteCharges=1};
            return typed;
        }
        var a=new MarksmanshipHunterSimRunner().Run(Build(1,false));
        var b=new MarksmanshipHunterSimRunner().Run(Build(0,false));
        Assert.NotEqual(a.HunterDamage,b.HunterDamage);
        var item=new MarksmanshipHunterSimRunner().Run(Build(1,true));
        Assert.Equal(1,item.CombatItemUses.GetValueOrDefault(ForeverCombatItemCatalog.DenseDynamite));
        Assert.True(item.DamageDone>a.DamageDone);
    }
}
