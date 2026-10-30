using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Hunter;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp169HunterSharedPetInheritanceAndRoster
{
    private static readonly string[] Families=[
        "bat","bear","bird-of-prey","boar","carrion-bird","cat",
        "crab","crocolisk","fox","gorilla","hyena","raptor","scorpid",
        "spider","tallstrider","turtle","wind-serpent","wolf","core-hound"
    ];

    [Fact]
    public void DynamicOwnerAttackPowerChangesPetDamageWithoutDiscardingPetBuffs()
    {
        var context=new SimulationContext(new SimulationRunOptions {DurationSeconds=30m});
        var hunter=new SimulationActorState {
            Key="hunter",TeamKey="raid",Level=60,MaximumHealth=3000m,
            Stats=new StatCollection()
        };
        hunter.Stats.Set("companion-attack-power",1000m);
        hunter.Stats.Set("armor",1000m);
        hunter.InitializeHealth(3000m);
        context.AddActor(hunter);
        var tuning=new ForeverBeastMasteryHunterTuning();
        var pet=HunterCompanionComposition.Create(context,hunter,
            ForeverHunterPetFamilies.Get("wolf"),tuning);
        var original=pet.Stats.Get("attack-power");
        var armor=pet.Stats.Get("armor");
        // Simulate Wolf's separate family AP aura before dynamic owner stats.
        pet.Stats.Set("attack-power",original+82m);
        hunter.Stats.Set("companion-attack-power",1300m);
        hunter.Stats.Set("armor",1500m);
        HunterCompanionComposition.RefreshOwnerStats(context,pet,tuning);
        Assert.Equal(original+82m+300m*tuning.PetOwnerAttackPowerInheritance,
            pet.Stats.Get("attack-power"));
        Assert.Equal(armor+500m*tuning.PetOwnerArmorInheritance,
            pet.Stats.Get("armor"));
        HunterCompanionComposition.RefreshOwnerStats(context,pet,tuning);
        Assert.Equal(original+82m+300m*tuning.PetOwnerAttackPowerInheritance,
            pet.Stats.Get("attack-power"));
        hunter.Stats.Set("companion-attack-power",1000m);
        HunterCompanionComposition.RefreshOwnerStats(context,pet,tuning);
        Assert.Equal(original+82m,pet.Stats.Get("attack-power"));
    }

    [Theory]
    [MemberData(nameof(PetFamilies))]
    public void EveryFamilyCanDealActualDamageWithMarksmanshipOwner(string family)
    {
        var request=new MarksmanshipHunterSimRequest {
            HasPet=true,PetFamily=family,DurationSeconds=30m,
            TargetMaximumHealth=10000000m,UseRapidFire=false,
            CaptureTimeline=true
        };
        request.Tuning.HitChancePercent=100m;
        request.PetTuning.PetHitPercent=100m;
        request.PetTuning.PetAvoidancePercent=0m;
        var run=new MarksmanshipHunterSimRunner().Run(request);
        Assert.True(run.HunterDamage>0m);
        Assert.True(run.PetDamage>0m);
        Assert.True(run.PetThreat>0m);
        Assert.InRange(Math.Abs(run.DamageDone-run.HunterDamage-run.PetDamage),
            0m,.000001m);
        Assert.Contains(run.Run.Timeline,e=>e.SourceActorKey=="hunter-pet" &&
            e.AbilityKey=="pet-"+ForeverHunterPetFamilies.Get(family).SpecialAbility &&
            e.Type==CombatEventType.AbilityCastCompleted);
    }

    public static TheoryData<string> PetFamilies {
        get {
            var data=new TheoryData<string>();
            foreach(var family in Families)data.Add(family);
            return data;
        }
    }

    [Fact]
    public void PetSpellAndMeleeHitInheritanceCanBeForcedIndependently()
    {
        MarksmanshipHunterSimRequest Build(int hit,string family)
        {
            var req=new MarksmanshipHunterSimRequest {
                HasPet=true,PetFamily=family,DurationSeconds=28m,
                TargetMaximumHealth=10000000m,UseRapidFire=false,
                CaptureTimeline=true
            };
            req.PetTuning.PetHitPercent=hit;
            req.PetTuning.PetAvoidancePercent=0m;
            return req;
        }
        var magicMiss=new MarksmanshipHunterSimRunner().Run(Build(0,"wind-serpent"));
        Assert.DoesNotContain(magicMiss.Run.Timeline,e=>e.SourceActorKey=="hunter-pet" &&
            e.Type==CombatEventType.Damage && e.AbilityKey=="pet-lightning-breath" &&
            e.Amount>0m);
        var magicHit=new MarksmanshipHunterSimRunner().Run(Build(100,"wind-serpent"));
        Assert.Contains(magicHit.Run.Timeline,e=>e.SourceActorKey=="hunter-pet" &&
            e.Type==CombatEventType.Damage && e.AbilityKey=="pet-lightning-breath" &&
            e.Amount>0m);
        var physicalMiss=new MarksmanshipHunterSimRunner().Run(Build(0,"bear"));
        Assert.DoesNotContain(physicalMiss.Run.Timeline,e=>e.SourceActorKey=="hunter-pet" &&
            e.Type==CombatEventType.Damage && e.AbilityKey=="pet-melee" &&
            e.Amount>0m);
        var physicalHit=new MarksmanshipHunterSimRunner().Run(Build(100,"bear"));
        Assert.Contains(physicalHit.Run.Timeline,e=>e.SourceActorKey=="hunter-pet" &&
            e.Type==CombatEventType.Damage && e.AbilityKey=="pet-melee" &&
            e.Amount>0m);
    }
}
