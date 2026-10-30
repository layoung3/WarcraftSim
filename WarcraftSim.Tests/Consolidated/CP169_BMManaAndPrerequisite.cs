using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Hunter;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp169BeastMasteryCurrentManaAndPrerequisites
{
    [Fact]
    public void BestialWrathAndIntimidationUseActualBaseManaPercentages()
    {
        var talents=new ForeverBeastMasteryHunterTalents();
        var tuning=new ForeverBeastMasteryHunterTuning{
            HunterMana=8000m,HunterBaseMana=4200m
        };
        var book=ForeverBeastMasteryHunterSpellbook.Hunter(talents,tuning);
        var wrath=book.Single(a=>a.Key=="bestial-wrath");
        var intimidation=book.Single(a=>a.Key=="intimidation");
        Assert.Equal(504m,wrath.ResourceCosts.Single(c=>c.ResourceKey=="mana").Amount);
        Assert.Equal(336m,intimidation.ResourceCosts.Single(c=>c.ResourceKey=="mana").Amount);
        Assert.Equal(120m,wrath.CooldownSeconds);
        Assert.Equal(60m,intimidation.CooldownSeconds);
    }

    [Fact]
    public void CapstoneMustHaveIntimidationAndFocusCostsArePaidInRealCombat()
    {
        var talents=new ForeverBeastMasteryHunterTalents();
        talents.Ranks["intimidation"]=0;
        Assert.Throws<ArgumentException>(()=>talents.Validate());
        talents.Ranks["intimidation"]=1;
        talents.Validate();
        var req=new BeastMasteryHunterSimRequest{
            DurationSeconds=30m,PetFamily="cat",
            UseIntimidation=true,UseBestialWrath=true,
            CaptureTimeline=true,
            TargetMaximumHealth=10000000m
        };
        var result=new BeastMasteryHunterSimRunner().Run(req);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.SourceActorKey=="hunter" && e.AbilityKey=="bestial-wrath" &&
            e.ResourceKey=="mana" && e.Amount==-504m);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.SourceActorKey=="hunter" && e.AbilityKey=="intimidation" &&
            e.ResourceKey=="mana" && e.Amount==-336m);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="hunter-pet" && e.Amount>0m);
        Assert.InRange(Math.Abs(result.DamageDone-result.DamageByAbility.Values.Sum()),
            0m,.000001m);
    }
}
