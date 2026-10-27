using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Hunter;
using WarcraftSim.Data.Forever.Warlock;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>Stage 4B: execution-level checks against the actual solo runners,
/// pet spellbooks and live status events, without inventing server coefficients.</summary>
public sealed class Cp166Stage4BHunterWarlockAuditTests
{
    [Fact]
    public void AllSixHunterAndWarlockSpecsExecuteAndSerializeWithSharedEngine()
    {
        var mm = new MarksmanshipHunterSimRunner().Run(new MarksmanshipHunterSimRequest {
            DurationSeconds = 35m, CaptureTimeline = true
        });
        var sv = new SurvivalHunterSimRunner().Run(new SurvivalHunterSimRequest {
            DurationSeconds = 35m, CaptureTimeline = true, CombatMode = "weave"
        });
        var bm = new BeastMasteryHunterSimRunner().Run(new BeastMasteryHunterSimRequest {
            DurationSeconds = 35m, CaptureTimeline = true, PetFamily = "bear",
            AdditionalTargets = 2
        });
        Assert.Equal("marksmanship", mm.Specialization);
        Assert.Equal("survival", sv.Specialization);
        Assert.Equal("beast-mastery", bm.Specialization);
        Assert.True(mm.DamageDone > 0m);
        Assert.True(sv.DamageDone > 0m);
        Assert.True(bm.HunterDamage > 0m);
        Assert.True(bm.PetDamage > 0m);
        Assert.True(mm.Run.ProcessedEventCount > 0);
        Assert.True(sv.Run.ProcessedEventCount > 0);
        Assert.True(bm.Run.ProcessedEventCount > 0);
        Assert.Contains("DamageDone", JsonSerializer.Serialize(mm));
        Assert.Contains("DamageDone", JsonSerializer.Serialize(sv));
        Assert.Contains("PetDamage", JsonSerializer.Serialize(bm));
        foreach (var spec in new[] { "affliction", "demonology", "destruction" })
        {
            var req = WarlockSimRequest.CreatePreset(spec);
            req.DurationSeconds = 35m;
            req.CaptureTimeline = true;
            var result = new WarlockSimRunner().Run(req);
            Assert.Equal(spec, result.Specialization);
            Assert.True(result.WarlockDamage > 0m);
            Assert.True(result.Run.ProcessedEventCount > 0);
            Assert.Contains("PetDamage", JsonSerializer.Serialize(result));
            Assert.InRange(Math.Abs(result.DamageDone-result.WarlockDamage-result.PetDamage),
                0m,.001m);
        }
    }

    [Fact]
    public void MarksmanshipRapidFireRetimesAlreadyActiveAutoShotsAndExpires()
    {
        MarksmanshipHunterSimResponse Run(bool rapid)
        {
            var req = new MarksmanshipHunterSimRequest {
                DurationSeconds = 45m, CaptureTimeline = true,
                UseRapidFire = rapid, UseAimedShot = false,
                UseMultiShot = false, UseArcaneShot = false,
                UseSerpentSting = false, UseSniperShot = false
            };
            req.Tuning.WeaponSpeedSeconds = 2.8m;
            req.Tuning.HitChancePercent = 100m;
            req.Tuning.CriticalChancePercent = 0m;
            return new MarksmanshipHunterSimRunner().Run(req);
        }
        var normal = Run(false);
        var rapid = Run(true);
        var regularShots = normal.Run.Timeline.Count(e =>
            e.Type == CombatEventType.Damage && e.AbilityKey == "auto-shot");
        var boostedShots = rapid.Run.Timeline.Count(e =>
            e.Type == CombatEventType.Damage && e.AbilityKey == "auto-shot");
        Assert.True(boostedShots > regularShots,
            $"Rapid Fire failed to accelerate autos: base={regularShots}, boosted={boostedShots}");
        Assert.True(rapid.RapidFireCasts > 0);
        Assert.Contains(rapid.Run.Timeline,e=>e.Type==CombatEventType.AuraRemoved &&
            e.AbilityKey=="rapid-fire");
    }

    [Theory]
    [InlineData("raptor-strike", true)]
    [InlineData("mongoose-bite", true)]
    [InlineData("strider-kick", true)]
    [InlineData("immolation-trap", false)]
    [InlineData("frost-trap", false)]
    public void SurvivalMeleeAndTrapsHaveCorrectRegisteredActionEligibility(
        string ability, bool meleeOnly)
    {
        var talents = new ForeverSurvivalHunterTalents();
        // A valid row-five selection requires 20 points in earlier rows.
        talents.Ranks["improved-tracking"] = 5;
        talents.Ranks["deflection"] = 5;
        talents.Ranks["entrapment"] = 5;
        talents.Ranks["survivalist"] = 5;
        talents.Ranks["strider-kick"] = 1;
        var defs = ForeverSurvivalHunterSpellbook.Create(talents,
            new ForeverSurvivalHunterTuning());
        var action = Assert.Single(defs, a => a.Key == ability);
        if (ability == "raptor-strike")
            Assert.Empty(action.Effects); // next-swing replacement, not instant strike
        if (ability == "mongoose-bite")
            Assert.Equal("hunter-mongoose-ready", action.RequiredTargetOpportunityKey);
        Assert.Equal(meleeOnly, ability is "raptor-strike" or "mongoose-bite" or "strider-kick");
    }

    [Fact]
    public void SurvivalFireAndFrostTrapCooldownGroupsRemainSeparate()
    {
        var abilities=ForeverSurvivalHunterSpellbook.Create(
            new ForeverSurvivalHunterTalents(),new ForeverSurvivalHunterTuning());
        foreach(var key in new[]{"immolation-trap","explosive-trap"})
            Assert.Equal("hunter-fire-traps",abilities.Single(x=>x.Key==key).SharedCooldownGroupKey);
        foreach(var key in new[]{"frost-trap","freezing-trap"})
            Assert.Equal("hunter-frost-traps",abilities.Single(x=>x.Key==key).SharedCooldownGroupKey);
        Assert.NotEqual(abilities.Single(x=>x.Key=="frost-trap").SharedCooldownGroupKey,
            abilities.Single(x=>x.Key=="explosive-trap").SharedCooldownGroupKey);
    }

    [Fact]
    public void AllNineteenPetFamiliesHaveIndependentExecutableSpecialAbilities()
    {
        Assert.Equal(19,ForeverHunterPetFamilies.All.Count);
        foreach(var family in ForeverHunterPetFamilies.All)
        {
            var abilities=ForeverBeastMasteryHunterSpellbook.Pet(
                family,new ForeverBeastMasteryHunterTuning());
            var special=Assert.Single(abilities,a=>a.Key=="pet-"+family.SpecialAbility);
            Assert.True(special.CooldownSeconds>=0m,family.Key);
            Assert.Contains(special.ResourceCosts,c=>c.ResourceKey=="focus");
            var request=new BeastMasteryHunterSimRequest {
                PetFamily=family.Key,DurationSeconds=32m,CaptureTimeline=true,
                UsePetSpecial=true,UseRapidFire=false,UseAimedShot=false,
                UseMultiShot=false,UseArcaneShot=false,UseSerpentSting=false,
                UseSummonHawk=false
            };
            var run=new BeastMasteryHunterSimRunner().Run(request);
            Assert.True(run.PetDamage>0m,family.Key);
            Assert.Contains(run.Run.Timeline,e=>e.SourceActorKey=="hunter-pet" &&
                e.AbilityKey==special.Key && e.Type==CombatEventType.AbilityCastCompleted);
            Assert.InRange(Math.Abs(run.DamageDone-run.HunterDamage-run.PetDamage-run.HawkDamage),
                0m,.001m);
        }
    }

    [Theory]
    [InlineData("raptor", "pet-rend-tick")]
    [InlineData("hyena", "pet-tendon-tick")]
    [InlineData("scorpid", "pet-scorpid-tick")]
    [InlineData("spider", "pet-corrosive-web-tick")]
    public void PetFamilyPeriodicEffectsRollApplicationHitButNotTickHit(
        string familyKey,string effectKey)
    {
        var family=ForeverHunterPetFamilies.Get(familyKey);
        var special=Assert.Single(ForeverBeastMasteryHunterSpellbook.Pet(
            family,new ForeverBeastMasteryHunterTuning()),a=>a.Key=="pet-"+family.SpecialAbility);
        var dot=Assert.Single(special.Effects,e=>e.Key==effectKey);
        Assert.True(dot.CanMiss);
        Assert.False(dot.CanCrit); // DoT critical eligibility remains provisional
        var req=new BeastMasteryHunterSimRequest {
            PetFamily=familyKey,DurationSeconds=32m,CaptureTimeline=true,
            UseRapidFire=false,UseSummonHawk=false
        };
        req.Tuning.PetHitPercent=0m;
        var result=new BeastMasteryHunterSimRunner().Run(req);
        Assert.Contains(result.Run.Timeline,e=>e.AbilityKey==special.Key &&
            e.Type==CombatEventType.Damage && e.ResultKey=="miss");
        Assert.DoesNotContain(result.Run.Timeline,e=>e.AbilityKey==special.Key &&
            e.EffectDeliveryType==CombatEffectDeliveryType.Periodic &&
            e.Type==CombatEventType.Damage && e.Amount>0m);
    }

    [Fact]
    public void BeastMasteryRevivedPetsResumeScriptedIncomingAttacks()
    {
        var request=new BeastMasteryHunterSimRequest {
            DurationSeconds=70m, PetFamily="bear", CaptureTimeline=true,
            PetIncomingHitDamage=10000m,PetIncomingHitEverySeconds=3m,
            UseRevivePet=true, UseMendPet=false, UseRapidFire=false,
            UseBestialWrath=false, UseIntimidation=false,
            UseSerpentSting=false,UseAimedShot=false,UseMultiShot=false,
            UseArcaneShot=false,UseSummonHawk=false
        };
        var result=new BeastMasteryHunterSimRunner().Run(request);
        var timeline=result.Run.Timeline;
        var revive=timeline.FirstOrDefault(e=>e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey=="revive-pet");
        Assert.NotNull(revive);
        var incomingAfterRevive=timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="scripted-pet-incoming-hit" && e.TimeSeconds>revive.TimeSeconds).ToArray();
        Assert.NotEmpty(incomingAfterRevive);
    }

    [Fact]
    public void EveryWarlockDemonUsesItsOwnAbilityManaAndActivityState()
    {
        Assert.Equal(7,ForeverWarlockDemons.All.Count);
        foreach(var family in ForeverWarlockDemons.All)
        {
            var defs=ForeverWarlockDemonSpellbook.Create(family,new ForeverWarlockDemonTuning());
            Assert.NotEmpty(defs);
            Assert.All(defs,a=>Assert.Contains("warlock-demon-engaged",a.RequiredSourceAuraKeys));
            var req=WarlockSimRequest.CreatePreset("demonology");
            req.PetType=family.Key;
            req.DurationSeconds=70m;
            req.AllowTemporaryDemon=family.IsTemporary;
            req.AllowDoomguardRitual=family.IsTemporary;
            req.CaptureTimeline=true;
            var run=new WarlockSimRunner().Run(req);
            Assert.True(run.PetDamage>0m,family.Key);
            Assert.InRange(Math.Abs(run.DamageDone-run.WarlockDamage-run.PetDamage),0m,.001m);
            Assert.Contains(run.Run.Timeline,e=>e.SourceActorKey=="warlock-pet" &&
                e.Type==CombatEventType.AbilityCastCompleted);
        }
    }

    [Theory]
    [InlineData("imp","felhunter",false)]
    [InlineData("imp","imp",true)]
    [InlineData("succubus","incubus",true)]
    [InlineData("felhunter","succubus",false)]
    public void DemonicPactPreservesDifferentSacrificeButCancelsSameFamily(
        string sacrificed,string summoned,bool canceled)
    {
        var req=WarlockSimRequest.CreatePreset("demonology");
        req.PrePullSacrificedDemon=sacrificed;
        req.PetType=summoned;
        req.DurationSeconds=25m;
        var result=new WarlockSimRunner().Run(req);
        Assert.Equal(canceled,result.SacrificeCanceledBeforePull);
        Assert.Equal(canceled?null:sacrificed,result.SacrificeBuffAtPull);
    }

    [Fact]
    public void ImprovedShadowBoltRequiresWarlockCriticalStrikeAndOnlyBuffsOwnShadowDamage()
    {
        var context=new SimulationContext(new SimulationRunOptions {
            DurationSeconds=25m,PrimaryActorKey="warlock"
        });
        var warlock=new SimulationActorState { Key="warlock", TeamKey="raid" };
        warlock.InitializeHealth(4000m);
        var pet=new SimulationActorState { Key="warlock-pet", TeamKey="raid" };
        pet.InitializeHealth(2000m);
        var boss=new SimulationActorState { Key="target", TeamKey="enemy" };
        boss.InitializeHealth(10000m);
        var other=new SimulationActorState { Key="add", TeamKey="enemy" };
        other.InitializeHealth(10000m);
        context.AddActor(warlock);context.AddActor(pet);
        context.AddActor(boss);context.AddActor(other);
        var auras=new AuraManager();
        var proc=new WarlockImprovedShadowBoltProcessor(5,auras);
        proc.Process(context,new CombatEvent {
            Type=CombatEventType.Damage,SourceActorKey="warlock",
            TargetActorKey="target",AbilityKey="shadow-bolt",
            IsCritical=false,Amount=100m
        });
        Assert.Equal(1m,WarlockImprovedShadowBoltProcessor.DamageMultiplier(
            boss,0m,"warlock","shadow",5));
        proc.Process(context,new CombatEvent {
            Type=CombatEventType.Damage,SourceActorKey="warlock",
            TargetActorKey="target",AbilityKey="shadow-bolt",
            IsCritical=true,Amount=200m
        });
        Assert.Equal(1.20m,WarlockImprovedShadowBoltProcessor.DamageMultiplier(
            boss,0m,"warlock","shadow",5));
        Assert.Equal(1m,WarlockImprovedShadowBoltProcessor.DamageMultiplier(
            boss,0m,"warlock-pet","shadow",5));
        Assert.Equal(1m,WarlockImprovedShadowBoltProcessor.DamageMultiplier(
            boss,0m,"warlock","fire",5));
        Assert.Equal(1m,WarlockImprovedShadowBoltProcessor.DamageMultiplier(
            other,0m,"warlock","shadow",5));
        Assert.Equal(1m,WarlockImprovedShadowBoltProcessor.DamageMultiplier(
            boss,13m,"warlock","shadow",5));
    }

    [Fact]
    public void DestructionImprovedShadowBoltCriticalAppliesBuffInRealSimulation()
    {
        var req=WarlockSimRequest.CreatePreset("destruction");
        req.DurationSeconds=22m;
        req.EnablePet=false;req.UseLifeTap=false;
        req.CustomPriority=["shadow-bolt"];
        req.BaseSpellHitPercent=100m;req.SpellCritPercent=100m;
        req.Talents.Ranks["improved-shadow-bolt"]=5;
        var run=new WarlockSimRunner().Run(req);
        Assert.Contains(run.Run.Timeline,e=>
            e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="improved-shadow-bolt" &&
            e.TargetActorKey=="target");
        var hits=run.Run.Timeline.Where(e=>
            e.Type==CombatEventType.Damage && e.AbilityKey=="shadow-bolt" &&
            e.Amount>0m).ToArray();
        Assert.True(hits.Length>=2);
        Assert.True(hits[1].Amount>hits[0].Amount,
            "A subsequent Shadow Bolt must benefit from the prior crit's own-target debuff.");
    }

    [Fact]
    public void WarlockImmolateApplicationMissPreventsPeriodicDamage()
    {
        var request=WarlockSimRequest.CreatePreset("destruction");
        request.EnablePet=false;
        request.UseLifeTap=false;
        request.UseCorruption=false;
        request.UseImmolate=true;
        request.CustomPriority=["immolate"];
        request.BaseSpellHitPercent=0m;
        request.SpellCritPercent=0m;
        request.CaptureTimeline=true;
        request.DurationSeconds=45m;
        var result=new WarlockSimRunner().Run(request);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="immolate" && e.ResultKey=="miss");
        Assert.DoesNotContain(result.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="immolate" && e.EffectDeliveryType==CombatEffectDeliveryType.Periodic &&
            e.Amount>0m);
    }
}
