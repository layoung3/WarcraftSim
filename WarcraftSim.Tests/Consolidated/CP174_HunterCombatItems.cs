using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Items;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>Hunter consumables execute through the same inventory and ability processor as Mage/Warlock.</summary>
public sealed class Cp174HunterCombatItemsTests
{
    [Theory]
    [InlineData("marksmanship")]
    [InlineData("survival")]
    [InlineData("beast-mastery")]
    public void EngineeringConsumableAddsActualEventDamageToHunterRun(string spec)
    {
        var items=new CombatUseItemsOptions{EngineeringSkill=250,DenseDynamiteCharges=1};
        decimal damage,reported;
        SimulationRunResult run;
        IReadOnlyDictionary<string,int> used;
        switch(spec)
        {
            case "marksmanship":
            {
                var r=new MarksmanshipHunterSimRequest {
                    DurationSeconds=15m,AdditionalTargets=0,CombatItems=items,
                    HasPet=false,Seed=174201,TargetMaximumHealth=1000000m
                };
                var result=new MarksmanshipHunterSimRunner().Run(r);
                damage=result.DamageByAbility.GetValueOrDefault(ForeverCombatItemCatalog.DenseDynamite);
                reported=result.DamageDone;
                used=result.CombatItemUses;run=result.Run;
                break;
            }
            case "survival":
            {
                var r=new SurvivalHunterSimRequest {
                    DurationSeconds=15m,AdditionalTargets=0,CombatItems=items,
                    HasPet=false,Seed=174201,TargetMaximumHealth=1000000m
                };
                var result=new SurvivalHunterSimRunner().Run(r);
                damage=result.DamageByAbility
                    .Where(x=>x.Key.EndsWith(":"+ForeverCombatItemCatalog.DenseDynamite,StringComparison.OrdinalIgnoreCase))
                    .Sum(x=>x.Value);
                reported=result.DamageDone;used=result.CombatItemUses;run=result.Run;
                break;
            }
            default:
            {
                var r=new BeastMasteryHunterSimRequest {
                    DurationSeconds=15m,AdditionalTargets=0,CombatItems=items,
                    PetEnabled=true,Seed=174201,TargetMaximumHealth=1000000m
                };
                var result=new BeastMasteryHunterSimRunner().Run(r);
                damage=result.DamageByAbility.GetValueOrDefault(ForeverCombatItemCatalog.DenseDynamite);
                reported=result.DamageDone;used=result.CombatItemUses;run=result.Run;
                break;
            }
        }
        Assert.Equal(1,used.GetValueOrDefault(ForeverCombatItemCatalog.DenseDynamite));
        Assert.InRange(damage,340m,460m);
        Assert.True(reported>=damage);
        Assert.Contains(run.Timeline,e=>e.AbilityKey==ForeverCombatItemCatalog.DenseDynamite
            && e.Type==CombatEventType.Damage);
    }

    [Fact]
    public void HunterSapperIsHealthGatedAndAllCombatItemsAreInventoryLimited()
    {
        var options=new CombatUseItemsOptions{
            EngineeringSkill=250,GoblinSapperCharges=1,DenseDynamiteCharges=1,
            MinimumHealthToUseSapper=100000m
        };
        var r=new MarksmanshipHunterSimRequest{
            CombatItems=options,DurationSeconds=12m,HasPet=false,Seed=174202
        };
        var result=new MarksmanshipHunterSimRunner().Run(r);
        Assert.Equal(0,result.CombatItemUses.GetValueOrDefault(ForeverCombatItemCatalog.GoblinSapperCharge));
        Assert.Equal(1,result.CombatItemUses.GetValueOrDefault(ForeverCombatItemCatalog.DenseDynamite));
        r.CombatItems=new CombatUseItemsOptions{EngineeringSkill=200,GoblinSapperCharges=1};
        Assert.Throws<ArgumentException>(()=>new MarksmanshipHunterSimRunner().Run(r));
        r.CombatItems=new CombatUseItemsOptions{ManaRubyCharges=1};
        Assert.Throws<ArgumentException>(()=>new MarksmanshipHunterSimRunner().Run(r));
    }

    [Fact]
    public void HunterExplosiveAndRuneOutcomeIsDeterministicAtFixedSeed()
    {
        BeastMasteryHunterSimRequest Build()=>new(){
            DurationSeconds=65m,Seed=174203,PetEnabled=true,TargetMaximumHealth=1000000m,
            CombatItems=new(){EngineeringSkill=250,GoblinSapperCharges=1,DenseDynamiteCharges=1,
                DarkRuneCharges=1,RuneBelowManaPercent=50m}
        };
        var a=new BeastMasteryHunterSimRunner().Run(Build());
        var b=new BeastMasteryHunterSimRunner().Run(Build());
        Assert.Equal(a.DamageDone,b.DamageDone);
        Assert.Equal(a.CombatItemUses,b.CombatItemUses);
        Assert.Equal(a.CombatItemDecisionReasons,b.CombatItemDecisionReasons);
    }
}
