using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Druid;
using WarcraftSim.Data.Forever.Shaman;
using Xunit;

namespace WarcraftSim.Tests;

public sealed class Cp144ClassMechanicsTests
{
    [Fact]
    public void RoguePoisonsAreEquippedPerHandAndDeadlyStacksGenerateTicks()
    {
        var request=RogueSimRequest.CreatePreset("combat");
        request.CaptureTimeline=true;
        request.MainHandPoison="deadly";
        request.OffHandPoison="none";
        request.Tuning.InstantPoisonChancePercent=100m;
        request.PoisonChargesPerWeapon=30;
        var result=new RogueSimRunner().Run(request);
        Assert.True(result.PoisonProcs>0);
        Assert.Contains(result.Run.Timeline, e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="deadly-poison" && e.IsPeriodic && e.Amount is >0m);
        Assert.DoesNotContain(result.Run.Timeline, e=>e.AbilityKey=="instant-poison");
        Assert.True(result.Run.CustomMetrics["rogue-main-hand-poison-charges"] < 30m);
        Assert.Equal(30m, result.Run.CustomMetrics["rogue-off-hand-poison-charges"]);
    }

    [Theory]
    [InlineData("wound")]
    [InlineData("crippling")]
    [InlineData("mind-numbing")]
    public void UtilityPoisonAppliesDebuffWithoutInventingDamage(string poison)
    {
        var request=RogueSimRequest.CreatePreset("combat");
        request.CaptureTimeline=true;
        request.MainHandPoison=poison;
        request.OffHandPoison="none";
        request.Tuning.InstantPoisonChancePercent=100m;
        var response=new RogueSimRunner().Run(request);
        Assert.True(response.PoisonProcs>0);
        Assert.Contains(response.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey==$"{poison}-poison");
        Assert.DoesNotContain(response.DamageByAbility.Keys,k=>k==$"{poison}-poison");
    }

    [Fact]
    public void RoguePoisonConfigurationRejectsUnrecognizedTypes()
    {
        var request=RogueSimRequest.CreatePreset("assassination");
        request.OffHandPoison="unverified-poison";
        Assert.Throws<ArgumentException>(()=>new RogueSimRunner().Run(request));
    }

    [Fact]
    public void RoguePoisonChargesNeverBecomeNegative()
    {
        var request=RogueSimRequest.CreatePreset("combat");
        request.MainHandPoison="instant";
        request.OffHandPoison="none";
        request.PoisonChargesPerWeapon=2;
        request.Tuning.InstantPoisonChancePercent=100m;
        var response=new RogueSimRunner().Run(request);
        Assert.InRange(response.PoisonProcs,0,2);
        Assert.Equal(0m,response.Run.CustomMetrics["rogue-main-hand-poison-charges"]);
    }

    [Fact]
    public void ExposeArmorDebuffMitigatesLessPhysicalDamageAfterSuccessfulFinisher()
    {
        var request=RogueSimRequest.CreatePreset("combat");
        request.CaptureTimeline=true;
        request.UseExposeArmor=true;
        request.TargetArmor=4000m;
        request.DurationSeconds=180m;
        var response=new RogueSimRunner().Run(request);
        Assert.Contains(response.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="expose-armor");
        Assert.True(response.Run.CustomMetrics["rogue-expose-armor-points"]>0m);
        Assert.Contains(response.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="rogue-main-hand" && e.MitigatedAmount is >0m);
    }

    [Fact]
    public void ForeverWeaponDpsUsesFixedFormAttackSpeeds()
    {
        Assert.Equal(75m,ForeverDruidFormState.WeaponDamage(75m,ForeverDruidForm.Cat));
        Assert.Equal(187.5m,ForeverDruidFormState.WeaponDamage(75m,ForeverDruidForm.DireBear));
        Assert.Equal(0m,ForeverDruidFormState.WeaponDamage(75m,ForeverDruidForm.Caster));
    }

    [Fact]
    public void DruidShiftingCostsManaAndPreservesEnergyWithFullFuror()
    {
        var actor=new SimulationActorState { Key="druid",MaximumHealth=5000m };
        actor.InitializeHealth(5000m);
        actor.AddResource(new ResourceState("mana",1000m,1000m));
        actor.AddResource(new ResourceState("energy",100m,80m));
        var form=new ForeverDruidFormState(furorRank:5,shiftManaCost:200m);
        Assert.True(form.Shift(actor,ForeverDruidForm.DireBear,0m));
        Assert.Equal(80m,actor.Resources["energy"].Current);
        Assert.Equal(0m,actor.Resources["energy"].RegenerationPerSecond);
        Assert.Equal(800m,actor.Resources["mana"].Current);
        Assert.False(form.Shift(actor,ForeverDruidForm.DireBear,1m));
        Assert.Equal(800m,actor.Resources["mana"].Current);
        Assert.False(form.Shift(actor,ForeverDruidForm.Cat,2m));
        Assert.True(form.Shift(actor,ForeverDruidForm.Cat,6m));
        Assert.Equal(80m,actor.Resources["energy"].Current);
        Assert.Equal(10m,actor.Resources["energy"].RegenerationPerSecond);
    }

    [Theory]
    [InlineData("cat")]
    [InlineData("bear")]
    public void FeralSimulationUsesRealCombatEventsAndSeparateResources(string role)
    {
        var request=new FeralDruidSimRequest { Role=role,CaptureTimeline=true };
        var response=new FeralDruidSimRunner().Run(request);
        Assert.True(response.DamageDone>0m);
        Assert.Contains(response.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="feral-auto");
        if(role=="cat")
            Assert.Contains(response.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastCompleted &&
                e.AbilityKey is "claw" or "shred");
        else
            Assert.Contains(response.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastCompleted &&
                e.AbilityKey=="lacerate");
        Assert.InRange(response.EndingEnergy,0m,100m);
        Assert.InRange(response.EndingRage,0m,100m);
    }

    [Fact]
    public void FeralAbilityFormRestrictionsAreEncodedInEngineDefinitions()
    {
        var list=ForeverFeralDruidSpellbook.Create(new(),new());
        Assert.Contains("druid-cat-form",Assert.Single(list,x=>x.Key=="shred").RequiredSourceAuraKeys);
        Assert.Contains("druid-dire-bear-form",Assert.Single(list,x=>x.Key=="lacerate").RequiredSourceAuraKeys);
        Assert.DoesNotContain(list,x=>x.Key=="tigers-fury");
    }

    [Fact]
    public void RestorationDruidUsesHealingEventsAndActualInjuredAllies()
    {
        var req=new RestorationDruidSimRequest { CaptureTimeline=true };
        var result=new RestorationDruidSimRunner().Run(req);
        Assert.True(result.HealingDone>0m);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.ScriptedDamage);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.Healing && e.Amount is >0m);
    }

    [Fact]
    public void RestorationDruidHotsAndChannelTicksMayCrit()
    {
        var talent=new ForeverRestorationDruidTalents();
        talent.Ranks["living-spirit"]=3;
        talent.Ranks["wild-growth"]=1;
        talent.Ranks["swiftmend"]=1;
        var list=ForeverRestorationDruidSpellbook.Create(talent,new());
        Assert.True(Assert.Single(list,x=>x.Key=="rejuvenation").Effects[0].CanCrit);
        var tranquility=Assert.Single(list,x=>x.Key=="tranquility");
        Assert.Equal(8m,tranquility.ChannelDurationSeconds);
        Assert.Equal(2m,tranquility.ChannelTickIntervalSeconds);
        Assert.True(tranquility.Effects[0].ApplyOnChannelTick);
        Assert.Contains("rejuvenation",Assert.Single(list,x=>x.Key=="swiftmend").RequiredAnyTargetAuraKeys);
    }

    [Fact]
    public void RejuvenationAndGroupHealingScenarioCanRunRepeatably()
    {
        var req=new RestorationDruidSimRequest();
        req.Talents.Ranks["living-spirit"]=3;
        req.Talents.Ranks["wild-growth"]=1;
        var a=new RestorationDruidSimRunner().Run(req);
        var b=new RestorationDruidSimRunner().Run(req);
        Assert.Equal(a.HealingDone,b.HealingDone);
        Assert.Equal(a.EndingMana,b.EndingMana);
    }

    [Fact]
    public void WildGrowthCreatesActualPeriodicGroupHealingEvents()
    {
        var request=new RestorationDruidSimRequest {CaptureTimeline=true};
        request.Talents.Ranks["living-spirit"]=3;
        request.Talents.Ranks["wild-growth"]=1;
        var result=new RestorationDruidSimRunner().Run(request);
        Assert.Contains(result.Run.Timeline,e=>e.AbilityKey=="wild-growth" &&
            e.Type==CombatEventType.Healing && e.IsPeriodic && e.Amount is >0m);
        Assert.Contains("wild-growth",result.HealingByAbility.Keys);
    }

    [Fact]
    public void ShamanRestorationUsesOnlyConnectedTalentRanks()
    {
        var talent=new ForeverRestorationShamanTalents();
        talent.Ranks["natural-grace"]=2;
        talent.Ranks["ancestral-healing"]=3;
        talent.Ranks["improved-reincarnation"]=1;
        talent.Validate();
        Assert.Equal(2,talent.Rank("natural-grace"));
        Assert.True(ForeverRestorationShamanSpellbook.Create(talent,new()).Count>0);
    }
}
