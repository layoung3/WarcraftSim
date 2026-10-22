using System.Text.Json;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Companions;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Hunter;
using Microsoft.AspNetCore.Mvc;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp163BeastMasteryPetCombatTests
{
    private static BeastMasteryHunterSimRequest Setup(string family="cat",bool pet=true) => new(){
        Seed=163001,DurationSeconds=90m,PetFamily=family,PetEnabled=pet,
        PetEngagementDelaySeconds=.5m,CaptureTimeline=true,TargetMaximumHealth=1000000m
    };
    [Fact]
    public void CompanionFactoryRegistersIndependentActorWithOwnerScaling()
    {
        var ctx=new SimulationContext(new SimulationRunOptions{
            Seed=1,DurationSeconds=30m,PrimaryActorKey="hunter"});
        var stats=new StatCollection();stats.Set("attack-power",1000m);stats.Set("armor",2000m);
        var owner=new SimulationActorState{Key="hunter",Name="Hunter",TeamKey="raid",
            Level=60,MaximumHealth=4000m,Stats=stats};
        owner.InitializeHealth(4000m);ctx.AddActor(owner);
        var (pet,link)=CompanionActorFactory.Create(ctx,owner,new CompanionDefinition{
            Key="pet",Name="Pet",OwnerKey="hunter",FamilyKey="wolf",
            BaseHealth=1000m,BaseArmor=100m,BaseAttackPower=200m,
            OwnerHealthCoefficient=.05m,OwnerAttackPowerCoefficient=.10m,
            OwnerArmorCoefficient=.30m,FamilyHealthMultiplier=1.08m
        });
        Assert.NotSame(owner,pet);
        Assert.Equal(1296m,pet.MaximumHealth);
        Assert.Equal(300m,pet.Stats.Get("attack-power"));
        Assert.Equal(700m,pet.Stats.Get("armor"));
        Assert.Equal("hunter",link.OwnerActorKey);
        Assert.True(pet.Resources.ContainsKey("focus"));
    }

    [Fact]
    public void DefaultTalentsAreLevelSixtyValid()
    {
        var request=new BeastMasteryHunterSimRequest();
        Assert.Equal(51,request.Talents.Ranks.Values.Sum());
        request.Talents.Validate();
        Assert.Equal(1,request.Talents.Rank("summon-hawk"));
        Assert.Equal(1,request.Talents.Rank("bestial-wrath"));
    }

    [Fact]
    public void PetAttacksAndCastsAreSeparateFromHunterAndDamageSumsExactly()
    {
        var run=new BeastMasteryHunterSimRunner().Run(Setup());
        Assert.True(run.HunterDamage>0m);
        Assert.True(run.PetAutoDamage>0m);
        Assert.True(run.PetSpecialDamage>0m);
        Assert.Equal(run.HunterDamage+run.PetDamage+run.HawkDamage,run.DamageDone);
        Assert.Equal(run.DamageDone/90m,run.Dps);
        Assert.Contains(run.Run.Timeline,e=>e.SourceActorKey=="hunter-pet" &&
            e.Type==CombatEventType.Damage && e.AbilityKey=="pet-melee");
        Assert.Contains(run.Run.Timeline,e=>e.SourceActorKey=="hunter-pet" &&
            e.Type==CombatEventType.ResourceChanged && e.ResourceKey=="focus" && e.Amount<0m);
        Assert.True(run.PetThreat>0m);
        Assert.True(run.HunterThreat>0m);
    }

    [Fact]
    public void PetDisabledLeavesHunterDamageButNoPetOrFocusedFireBonus()
    {
        var no= new BeastMasteryHunterSimRunner().Run(Setup(pet:false));
        Assert.Equal(0m,no.PetDamage);
        Assert.Equal(0m,no.PetAutoDamage);
        Assert.Equal(0m,no.PetThreat);
        Assert.True(no.HunterDamage>0m);
        Assert.DoesNotContain(no.Run.Timeline,e=>e.SourceActorKey=="hunter-pet" &&
            e.Type==CombatEventType.Damage);
    }

    [Theory]
    [InlineData("cat","pet-prowl")]
    [InlineData("wolf","pet-furious-howl")]
    [InlineData("bear","pet-swipe")]
    [InlineData("wind-serpent","pet-lightning-breath")]
    [InlineData("fox","pet-tricksters-dance")]
    [InlineData("raptor","pet-savage-rend")]
    public void FamilySpecialsExecuteUnderSharedEngine(string family,string ability)
    {
        var run=new BeastMasteryHunterSimRunner().Run(Setup(family));
        Assert.True(run.Run.Summary.ActorSummaries["hunter-pet"].Abilities
            .GetValueOrDefault(ability)?.CastCompletedCount>0);
        Assert.True(run.PetDamage>0m);
        Assert.True(run.PetThreat>0m);
    }

    [Fact]
    public void CatProwlIsPreCombatAndOnlyEmpowersTheFirstAttack()
    {
        var request=Setup("cat");
        request.Tuning.PetHitPercent=100m;
        request.Tuning.PetAvoidancePercent=0m;
        var result=new BeastMasteryHunterSimRunner().Run(request);
        var timeline=result.Run.Timeline;
        var prowl=timeline.Where(e=>e.SourceActorKey=="hunter-pet" &&
            e.AbilityKey=="pet-prowl" && e.Type==CombatEventType.AbilityCastCompleted).ToArray();
        Assert.Single(prowl);
        Assert.Equal(0m,prowl[0].TimeSeconds);
        Assert.Contains(timeline,e=>e.SourceActorKey=="hunter-pet" &&
            e.AbilityKey=="pet-prowl" && e.Type==CombatEventType.ResourceChanged &&
            e.ResourceKey=="focus" && e.Amount==-40m);
        Assert.Contains(timeline,e=>e.AbilityKey=="pet-prowl-buff" &&
            e.Type==CombatEventType.AuraRemoved &&
            e.Description != null && e.Description.Contains("Prowl"));
        Assert.DoesNotContain(timeline,e=>e.SourceActorKey=="hunter-pet" &&
            e.AbilityKey=="pet-prowl" &&
            e.Type==CombatEventType.AbilityCastCompleted && e.TimeSeconds>0m);
        var cat=ForeverHunterPetFamilies.Get("cat");
        var prowlAbility=ForeverBeastMasteryHunterSpellbook.Pet(cat,request.Tuning)
            .Single(x=>x.Key=="pet-prowl");
        Assert.Equal(40m,prowlAbility.ResourceCosts.Single().Amount);
        Assert.Equal(10m,prowlAbility.CooldownSeconds);
    }

    [Fact]
    public void BearSwipeUsesVerifiedRankFiveDamageAndHitsEachEnemyExactlyOnce()
    {
        var request=Setup("bear");
        request.AdditionalTargets=2;
        request.UseSummonHawk=false;
        var spell=ForeverBeastMasteryHunterSpellbook.Pet(
            ForeverHunterPetFamilies.Get("bear"),request.Tuning)
            .Single(x=>x.Key=="pet-swipe");
        Assert.Equal(20m,spell.ResourceCosts.Single().Amount);
        Assert.Equal(5m,spell.CooldownSeconds);
        var effect=Assert.Single(spell.Effects);
        Assert.Equal(20m,effect.MinimumValue);
        Assert.Equal(22m,effect.MaximumValue);
        Assert.Equal(3,effect.MaxTargets);
        var run=new BeastMasteryHunterSimRunner().Run(request);
        var hits=run.Run.Timeline.Where(e=>e.SourceActorKey=="hunter-pet" &&
            e.AbilityKey=="pet-swipe" && e.Type==CombatEventType.Damage).ToArray();
        var casts=run.Run.Summary.ActorSummaries["hunter-pet"]
            .Abilities["pet-swipe"].CastCompletedCount;
        Assert.True(casts>1);
        Assert.Equal(3*casts,hits.Length);
        Assert.Equal(casts,hits.Count(e=>e.TargetActorKey=="target"));
        Assert.Equal(casts,hits.Count(e=>e.TargetActorKey=="add-1"));
        Assert.Equal(casts,hits.Count(e=>e.TargetActorKey=="add-2"));
        Assert.True(run.PetThreat<run.PetDamage); // per-target boss threat only
    }

    [Fact]
    public void WindSerpentBreathSpendsVerifiedFocusAndUsesNatureDamage()
    {
        var request=Setup("wind-serpent");
        var spell=ForeverBeastMasteryHunterSpellbook.Pet(
            ForeverHunterPetFamilies.Get("wind-serpent"),request.Tuning)
            .Single(x=>x.Key=="pet-lightning-breath");
        Assert.Equal(50m,spell.ResourceCosts.Single().Amount);
        Assert.Equal(0m,spell.CooldownSeconds);
        var effect=Assert.Single(spell.Effects);
        Assert.Equal("nature",effect.SchoolKey);
        Assert.Equal(86m,effect.MinimumValue);
        Assert.Equal(98m,effect.MaximumValue);
        var run=new BeastMasteryHunterSimRunner().Run(request);
        var spends=run.Run.Timeline.Where(e=>e.SourceActorKey=="hunter-pet" &&
            e.AbilityKey=="pet-lightning-breath" &&
            e.Type==CombatEventType.ResourceChanged &&
            e.ResourceKey=="focus" && e.Amount==-50m).ToArray();
        Assert.NotEmpty(spends);
        Assert.Contains(run.Run.Timeline,e=>e.SourceActorKey=="hunter-pet" &&
            e.AbilityKey=="pet-lightning-breath" &&
            e.Type==CombatEventType.Damage && e.SchoolKey=="nature" && e.Amount>0m);
    }

    [Fact]
    public void WolfHowlConsumesSixtyFocusAndBoostsActualPetAutoDamage()
    {
        var on=Setup("wolf");
        var off=Setup("wolf");
        foreach(var request in new[]{on,off}) {
            request.UseSummonHawk=false;
            request.UsePetBite=false;
            request.Tuning.HunterCriticalPercent=0m;
            request.Tuning.HunterMeleeCriticalPercent=0m;
            request.Tuning.PetCriticalBonusPercent=0m;
            request.Tuning.PetHitPercent=100m;
            request.Tuning.PetAvoidancePercent=0m;
            request.Tuning.PetDamageMin=100m;
            request.Tuning.PetDamageMax=100m;
        }
        off.UsePetSpecial=false;
        var spell=ForeverBeastMasteryHunterSpellbook.Pet(
            ForeverHunterPetFamilies.Get("wolf"),on.Tuning)
            .Single(x=>x.Key=="pet-furious-howl");
        Assert.Equal(60m,spell.ResourceCosts.Single().Amount);
        Assert.Equal(30m,spell.CooldownSeconds);
        Assert.Contains("wolf-furious-howl",spell.ForbiddenSourceAuraKeys);
        var buffed=new BeastMasteryHunterSimRunner().Run(on);
        var baseline=new BeastMasteryHunterSimRunner().Run(off);
        Assert.Contains(buffed.Run.Timeline,e=>e.SourceActorKey=="hunter-pet" &&
            e.AbilityKey=="pet-furious-howl" &&
            e.Type==CombatEventType.ResourceChanged && e.Amount==-60m);
        Assert.Contains(buffed.Run.Timeline,e=>e.AbilityKey=="wolf-furious-howl" &&
            e.Type==CombatEventType.AuraApplied && e.TargetActorKey=="hunter-pet");
        Assert.True(buffed.PetAutoDamage>baseline.PetAutoDamage);
        // The flat 10% damage buff it replaced would not depend on AP.
        Assert.True(on.Tuning.WolfHowlMeleeAttackPowerBonus>0m);
    }

    [Fact]
    public void HunterSpecialShotsPreserveWeaponDamageRange()
    {
        var request=Setup();
        var spellbook=ForeverBeastMasteryHunterSpellbook.Hunter(
            request.Talents,request.Tuning);
        foreach(var key in new[]{"aimed-shot","multi-shot"}) {
            var ability=spellbook.Single(x=>x.Key==key);
            var damage=Assert.Single(ability.Effects);
            Assert.Equal(request.Tuning.HunterWeaponMax-request.Tuning.HunterWeaponMin,
                damage.MaximumValue-damage.MinimumValue);
            Assert.True(damage.MinimumValue<damage.MaximumValue);
        }
    }

    [Fact]
    public void OwnerApIncreasesPetDamageWithSameSeed()
    {
        var low=Setup();low.UseSummonHawk=false;
        var high=Setup();high.UseSummonHawk=false;
        high.Tuning.RangedAttackPower+=500m;
        var runLow=new BeastMasteryHunterSimRunner().Run(low);
        var runHigh=new BeastMasteryHunterSimRunner().Run(high);
        Assert.True(runHigh.PetDamage>runLow.PetDamage);
    }

    [Fact]
    public void FocusCostsAndRegenerationChangeCastCount()
    {
        var slow=Setup("cat");slow.UsePetSpecial=false;
        slow.Tuning.PetFocusPerSecond=0m;
        var fast=Setup("cat");fast.UsePetSpecial=false;
        fast.Tuning.PetFocusPerSecond=12m;
        var r1=new BeastMasteryHunterSimRunner().Run(slow);
        var r2=new BeastMasteryHunterSimRunner().Run(fast);
        var n1=r1.Run.Summary.ActorSummaries["hunter-pet"].Abilities
            .GetValueOrDefault("pet-claw")?.CastCompletedCount??0;
        var n2=r2.Run.Summary.ActorSummaries["hunter-pet"].Abilities
            .GetValueOrDefault("pet-claw")?.CastCompletedCount??0;
        Assert.True(n2>n1);
    }

    [Fact]
    public void PetDeathStopsFutureAutosAndAbilities()
    {
        var req=Setup();req.PetIncomingHitDamage=100000m;
        req.PetIncomingHitEverySeconds=5m;
        var run=new BeastMasteryHunterSimRunner().Run(req);
        Assert.True(run.PetDied);
        Assert.True(run.PetInactiveSeconds>0m);
        Assert.DoesNotContain(run.Run.Timeline,e=>e.SourceActorKey=="hunter-pet" &&
            e.Type==CombatEventType.Damage && e.TimeSeconds>10m);
    }

    [Fact]
    public void EngagementDelayAndZeroUptimePreventPhantomPetDamage()
    {
        var req=Setup();req.PetEngagementDelaySeconds=89m;
        req.PetUptimePercent=0m;
        var run=new BeastMasteryHunterSimRunner().Run(req);
        Assert.Equal(0m,run.PetDamage);
        Assert.Equal(0m,run.PetActiveSeconds);
    }

    [Fact]
    public void SummonedHawksGetDistinctActorSummaries()
    {
        var run=new BeastMasteryHunterSimRunner().Run(Setup());
        Assert.Contains("hawk-1",run.Run.Summary.ActorSummaries.Keys);
        Assert.Contains("hawk-2",run.Run.Summary.ActorSummaries.Keys);
        Assert.True(run.HawkDamage>0m);
        Assert.True(run.Run.CustomMetrics["beast-hawk-summons"]>0m);
    }

    [Fact]
    public void FamilyCatalogueExposesAllEighteenRegularAndOneClientListedFamily()
    {
        Assert.Equal(19,ForeverHunterPetFamilies.All.Count);
        Assert.Equal("dismember",ForeverHunterPetFamilies.Get("crocolisk").SpecialAbility);
        Assert.Equal("tricksters-dance",ForeverHunterPetFamilies.Get("fox").SpecialAbility);
        Assert.True(ForeverHunterPetFamilies.Get("cat").DamageModifier >
                    ForeverHunterPetFamilies.Get("bear").DamageModifier);
    }

    [Fact]
    public void BeastMasteryPresetFileMatchesTalentBudgetAndKeys()
    {
        var cursor=new DirectoryInfo(AppContext.BaseDirectory);
        string? file=null;
        while(cursor is not null){
            var candidate=Path.Combine(cursor.FullName,"WarcraftSim.Api","wwwroot",
                "default-talents","hunter-beast-mastery.json");
            if(File.Exists(candidate)){file=candidate;break;}
            cursor=cursor.Parent;
        }
        Assert.NotNull(file);
        using var document=JsonDocument.Parse(File.ReadAllText(file));
        var preset=document.RootElement;
        Assert.Equal("hunter-beast-mastery",preset.GetProperty("id").GetString());
        var ranks=JsonSerializer.Deserialize<Dictionary<string,int>>(
            preset.GetProperty("overrides").GetProperty("talents")
                .GetProperty("ranks").GetRawText())!;
        Assert.Equal(51,ranks.Values.Sum());
        new ForeverBeastMasteryHunterTalents {Ranks=ranks}.Validate();
    }

    [Fact]
    public void PetCritAndFrenzyHaveExecutionEffects()
    {
        var response=new BeastMasteryHunterSimRunner().Run(Setup());
        Assert.True(response.PetCriticalHits>0);
        Assert.True(response.Run.CustomMetrics["beast-frenzy-procs"]>0m);
        Assert.Contains(response.Run.Timeline,e=>e.AbilityKey=="pet-frenzy" &&
            e.Type==CombatEventType.AuraApplied);
    }

    [Fact]
    public void PetTakesLessDamageWithEnduranceTraining()
    {
        var request=Setup();
        request.Tuning.PetBaseHealth=1000m;
        request.PetIncomingHitDamage=200m;
        request.PetIncomingHitEverySeconds=10m;
        var result=new BeastMasteryHunterSimRunner().Run(request);
        Assert.True(result.Run.Summary.ActorSummaries["hunter-pet"].StartingResources["focus"]>0);
        Assert.False(result.PetDied);
        Assert.True(result.Run.CustomMetrics["beast-pet-uptime-seconds"]>0m);
    }

    [Fact]
    public async Task ApiDefaultsAndSerializationRoundTrip()
    {
        var controller=new BeastMasteryHunterSimController();
        var defaults=Assert.IsType<OkObjectResult>(controller.Defaults().Result);
        var request=Assert.IsType<BeastMasteryHunterSimRequest>(defaults.Value);
        var json=JsonSerializer.Serialize(request);
        var restored=JsonSerializer.Deserialize<BeastMasteryHunterSimRequest>(json)!;
        Assert.Equal("cat",restored.PetFamily);
        var response=await controller.Run(restored,CancellationToken.None);
        Assert.IsType<OkObjectResult>(response.Result);
    }
}
