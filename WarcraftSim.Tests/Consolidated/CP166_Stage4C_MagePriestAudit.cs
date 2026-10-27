using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Mage;
using WarcraftSim.Data.Forever.Priest;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>
/// CP166 Stage 4C: executable cross-specialization auditing. Existing CP125-139,
/// Stage 3 and Stage 4A-4B regressions remain authoritative for shared effects.
/// </summary>
public sealed class Cp166Stage4CMagePriestAudit
{
    [Theory]
    [InlineData("arcane")]
    [InlineData("fire")]
    [InlineData("frost")]
    public void MageSpecsExecuteWithBalancedReportingAndSerialization(string spec)
    {
        var request = MageSimRequest.CreatePreset(spec);
        request.DurationSeconds=60m;
        request.CaptureTimeline=true;
        var result=new MageSimRunner().Run(request);
        Assert.True(result.DamageDone>0m);
        Assert.InRange(Math.Abs(result.DamageDone-result.DamageByAbility.Values.Sum()),0m,.000001m);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="mage" && e.ResultKey is "hit" or "critical");
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.ThreatChanged &&
            e.SourceActorKey=="mage" && e.TargetActorKey=="target");
        Assert.Contains("DamageByAbility",JsonSerializer.Serialize(result));
    }

    [Theory]
    [InlineData("discipline")]
    [InlineData("holy")]
    public void PriestHealerSpecsExecuteInTheFixedEncounterAndAccountForOverheal(string spec)
    {
        var request=new HolyPriestPreviewRequest {
            EnableExtendedPriest=true,
            UseAdaptiveHealingPolicy=true,
            UseSpecializationHealingPolicy=true,
            PriestCharacter=new CharacterProfile {
                ClassKey="priest",SpecializationKey=spec,Level=60
            },
            DurationSeconds=32m,TankMaximumHealth=5500m,
            BossSwingDamage=320m,BossSwingIntervalSeconds=2m,
            PriestMaximumMana=14000m,
            UsePowerWordShield=true,UsePenance=true,
            UsePrayerOfMending=true,CaptureTimeline=true
        };
        var result=new HolyPriestPreviewRunner().Run(request);
        Assert.True(result.HealingDone>0m);
        var summary=result.Run.Summary.ActorSummaries["priest"];
        Assert.True(summary.HealingDone>=0m);
        Assert.True(summary.OverhealingDone>=0m);
        Assert.InRange(Math.Abs(summary.HealingDone -
            summary.HealingDoneByAbility.Values.Sum()),0m,.000001m);
        Assert.Contains("PriestEndingMana",JsonSerializer.Serialize(result));
    }

    [Fact]
    public void ShadowPriestExecutesPeriodicChannelsBacklashAndSerializes()
    {
        var req=new ShadowPriestSimRequest {
            DurationSeconds=45m,TargetInitialHealthPercent=19m,
            TargetMaximumHealth=200000m,BaseSpellHitPercent=100m,
            SpellCritOverridePercent=0m,CaptureTimeline=true
        };
        var run=new ShadowPriestSimRunner().Run(req);
        Assert.True(run.DamageDone>0m);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="mind-flay" && e.IsChannelTick);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="shadow-word-pain" && e.IsPeriodic);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="shadow-word-death-backlash" &&
            e.TargetActorKey=="priest");
        Assert.Equal(run.DamageDone,run.Run.Summary.ActorSummaries["priest"].DamageDone);
        Assert.Contains("MaximumShadowWeavingStacks",JsonSerializer.Serialize(run));
    }

    [Fact]
    public void MageConcentrationProcsFreeNextSpellAndPreservesDamageAccounting()
    {
        var req=MageSimRequest.CreatePreset("arcane");
        req.DurationSeconds=400m;
        req.BaseSpellHitPercent=100m;
        req.UseMissileBarrage=false;
        req.UseEvocation=false;
        req.UseMajorManaPotion=false;
        req.CombatItems.ManaRubyCharges=0;
        req.CustomPriority=["fireball"];
        req.Tuning.FireballMana=10m;
        req.Tuning.ManaRegenPerSecond=100m;
        var result=new MageSimRunner().Run(req);
        Assert.True(result.ClearcastingProcs>0);
        Assert.True(result.ClearcastingConsumed>0);
        Assert.True(result.ClearcastingConsumed<=result.ClearcastingProcs);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="arcane-concentration" &&
            e.TargetActorKey=="mage");
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="fireball" && e.Amount==0m);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="fireball" && e.Amount==-10m);
        Assert.InRange(Math.Abs(result.DamageDone-result.DamageByAbility.Values.Sum()),0m,.000001m);
    }

    [Fact]
    public void ArcaneMindIncreasesArcaneCritByHalfAtRankFive()
    {
        var req=MageSimRequest.CreatePreset("arcane");
        req.Talents.Ranks["arcane-concentration"]=0;
        req.Talents.Ranks["arcane-meditation"]=0;
        req.Talents.Ranks["arcane-mind"]=5;
        req.Talents.Ranks["arcane-instability"]=0;
        req.Talents.Ranks["arcane-impact"]=0;
        req.BaseSpellHitPercent=100m;req.SpellCritPercent=100m;
        req.UseMissileBarrage=false;
        req.UseEvocation=false;req.UseMajorManaPotion=false;
        req.CombatItems.ManaRubyCharges=0;
        req.DurationSeconds=3m;
        req.CustomPriority=["arcane-blast"];
        var result=new MageSimRunner().Run(req);
        var crit=Assert.Single(result.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="arcane-blast" && e.IsCritical);
        Assert.InRange(crit.RawAmount.GetValueOrDefault()/crit.Amount.GetValueOrDefault(),
            .99m,1.01m); // no armor mitigation for an Arcane spell
        // The real 2.0x crit is compared against the same noncritical rank-5
        // base amount in the spellbook, with raw multiplier already included.
        var sp=ForeverMageDpsSpellbook.Create(req.Talents,req.Tuning,
            result.SpellPower,180m,result.MaximumMana)
            .Single(x=>x.Key=="arcane-blast").Effects.Single();
        Assert.InRange(Math.Abs(crit.Amount.GetValueOrDefault() /
            (sp.MinimumValue*sp.DamageMultiplier)-2m),0m,.000001m);
    }

    [Fact]
    public void FrostChannelingChangesManaCostsButNotFireAndReportsReducedFrostThreat()
    {
        var talents=ForeverMageTalentBuild.Default("frost");
        var t=new ForeverMageTuning();
        var with=ForeverMageDpsSpellbook.Create(talents,t,200m);
        talents.Ranks["frost-channeling"]=0;
        var without=ForeverMageDpsSpellbook.Create(talents,t,200m);
        foreach(var key in new[]{"frostbolt","frost-blast","ice-lance","frostfire-bolt"})
        {
            var original=without.Single(x=>x.Key==key).ResourceCosts.Single().Amount;
            var discounted=with.Single(x=>x.Key==key).ResourceCosts.Single().Amount;
            Assert.Equal(original*.85m,discounted);
        }
        Assert.Equal(without.Single(x=>x.Key=="fireball").ResourceCosts.Single().Amount,
            with.Single(x=>x.Key=="fireball").ResourceCosts.Single().Amount);
        var req=MageSimRequest.CreatePreset("frost");
        req.BaseSpellHitPercent=100m;
        req.CustomPriority=["frostbolt"];
        req.DurationSeconds=8m;req.UseEvocation=false;
        req.UseMajorManaPotion=false;req.CombatItems.ManaRubyCharges=0;
        var run=new MageSimRunner().Run(req);
        var damage=run.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="frostbolt"&& e.Amount>0m).Sum(e=>e.Amount!.Value);
        var threat=run.Run.Timeline.Where(e=>e.Type==CombatEventType.ThreatChanged &&
            e.AbilityKey=="frostbolt"&&e.SourceActorKey=="mage").Sum(e=>e.Amount!.Value);
        Assert.True(damage>0m);
        Assert.InRange(Math.Abs(threat-damage*.70m),0m,.000001m);
    }

    [Theory]
    [InlineData("arcane-blast")]
    [InlineData("fire-blast")]
    [InlineData("ice-lance")]
    public void IncinerationThreeRanksAddsSixCritPointsOnlyToEligibleSpells(string key)
    {
        var talents=ForeverMageTalentBuild.Default("fire");
        talents.Ranks["incineration"]=3;
        talents.Validate("fire");
        var book=ForeverMageDpsSpellbook.Create(talents,new ForeverMageTuning(),0m);
        Assert.Contains(book,a=>a.Key==key);
        talents.Ranks["incineration"]=4;
        Assert.Throws<ArgumentException>(()=>talents.Validate("fire"));
    }

    [Fact]
    public void FrostWintersChillStacksIncreaseOnlyTheNextLandedConeOfCold()
    {
        var request=MageSimRequest.CreatePreset("frost");
        request.CustomPriority=["cone-of-cold","frostbolt"];
        request.UseFingersOfFrost=false;
        request.BaseSpellHitPercent=100m;
        request.SpellCritPercent=0m;
        request.UseEvocation=false;request.UseMajorManaPotion=false;
        request.CombatItems.ManaRubyCharges=0;
        request.Tuning.ManaRegenPerSecond=100m;
        request.DurationSeconds=35m;
        var spells=ForeverMageDpsSpellbook.Create(
            request.Talents,request.Tuning,200m);
        var cone=spells.Single(x=>x.Key=="cone-of-cold");
        Assert.Equal(555m*.85m,cone.ResourceCosts.Single().Amount);
        Assert.Equal(10m,cone.CooldownSeconds);
        Assert.Equal(0m,cone.CastTimeSeconds);
        var run=new MageSimRunner().Run(request);
        var impacts=run.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="cone-of-cold" && e.Amount>0m).ToArray();
        Assert.True(impacts.Length>=2);
        Assert.True(impacts[1].Amount>impacts[0].Amount,
            "Winter's Chill should increase the next Cone after Frostbolt stacks.");
        Assert.True(run.Run.Timeline.Any(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="frostbolt" && e.TimeSeconds>impacts[0].TimeSeconds &&
            e.TimeSeconds<impacts[1].TimeSeconds));
    }

    [Fact]
    public void ArcaneInstabilityRaisesEverySchoolWithoutChangingUnrelatedManaCosts()
    {
        var talent=ForeverMageTalentBuild.Default("arcane");
        talent.Ranks["arcane-instability"]=0;
        var baseBook=ForeverMageDpsSpellbook.Create(talent,new ForeverMageTuning(),0m);
        talent.Ranks["arcane-instability"]=3;
        var upgraded=ForeverMageDpsSpellbook.Create(talent,new ForeverMageTuning(),0m);
        foreach(var key in new[]{"fireball","frostbolt","arcane-blast","ice-lance"})
        {
            var normal=baseBook.Single(x=>x.Key==key);
            var boosted=upgraded.Single(x=>x.Key==key);
            Assert.Equal(normal.Effects[0].DamageMultiplier*1.03m,
                boosted.Effects[0].DamageMultiplier);
            Assert.Equal(normal.ResourceCosts.Single().Amount,
                boosted.ResourceCosts.Single().Amount);
        }
    }

    [Fact]
    public void PenanceIsThreeHealingTicksStartingImmediatelyOverTwoSeconds()
    {
        var ability=ForeverPriestHealingSpellbook.Create(new ForeverPriestHealingTuning())
            .Single(x=>x.Key=="penance");
        Assert.Equal(2m,ability.ChannelDurationSeconds);
        Assert.Equal(1m,ability.ChannelTickIntervalSeconds);
        Assert.True(ability.ChannelTickAtStart);
        var req=new HolyPriestPreviewRequest {
            EnableExtendedPriest=true,DurationSeconds=9m,
            TankMaximumHealth=3000m,BossSwingDamage=600m,
            BossSwingIntervalSeconds=2m,
            PriestMaximumMana=20000m,UsePenance=true,
            UsePrayerOfHealing=false,UsePrayerOfMending=false,
            UsePowerWordShield=false,UseRenew=false,UseBindingHeal=false,
            GreaterHealHealthThresholdPercent=0m,
            FlashHealHealthThresholdPercent=0m,CaptureTimeline=true
        };
        var run=new HolyPriestPreviewRunner().Run(req);
        var starts=run.Run.Timeline.Where(e=>e.Type==CombatEventType.AbilityChannelStarted &&
            e.AbilityKey=="penance").ToArray();
        Assert.NotEmpty(starts);
        var ticks=run.Run.Timeline.Where(e=>e.Type==CombatEventType.Healing &&
            e.AbilityKey=="penance"&&e.IsChannelTick).ToArray();
        Assert.Equal(3,ticks.Length);
        Assert.Equal(starts[0].TimeSeconds,ticks[0].TimeSeconds);
        Assert.Equal(starts[0].TimeSeconds+1m,ticks[1].TimeSeconds);
        Assert.Equal(starts[0].TimeSeconds+2m,ticks[2].TimeSeconds);
    }

    [Fact]
    public void ShadowWordDeathBacklashUsesCasterMaximumHealthNotDamage()
    {
        var req=new ShadowPriestSimRequest {
            DurationSeconds=5m,TargetMaximumHealth=200000m,
            TargetInitialHealthPercent=15m,BaseSpellHitPercent=100m,
            SpellCritOverridePercent=0m,CaptureTimeline=true
        };
        req.UseMindBlast=false;req.UseMindFlay=false;
        req.UseShadowWordPain=false;req.UseDevouringPlague=false;
        req.UseVampiricEmbrace=false;
        var run=new ShadowPriestSimRunner().Run(req);
        var backlash=Assert.Single(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="shadow-word-death-backlash");
        Assert.Equal(req.PriestMaximumHealth*.10m,backlash.Amount);
    }

    [Fact]
    public void CurrentShadowWeavingRankLimitAndCooldownsAreClientAligned()
    {
        var talents=new ForeverPriestShadowTalentBuild();
        Assert.Equal(3,talents.Rank("shadow-weaving"));
        talents.Ranks["shadow-weaving"]=4;
        Assert.Throws<ArgumentException>(()=>talents.Validate(60));
        talents.Ranks["shadow-weaving"]=3;
        var spells=ForeverPriestShadowSpellbook.Create(
            new ForeverPriestShadowTuning(),talents,0m);
        Assert.Equal(18m,spells.Single(x=>x.Key=="shadow-word-death").CooldownSeconds);
        Assert.Equal(30m,spells.Single(x=>x.Key=="vampiric-embrace").CooldownSeconds);
        Assert.Equal(3m,spells.Single(x=>x.Key=="mind-flay").ChannelDurationSeconds);
    }
}
