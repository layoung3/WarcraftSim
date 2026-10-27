using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Data.Forever.Combat;
using WarcraftSim.Data.Forever.Hunter;
using WarcraftSim.Data.Forever.Warlock;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>
/// CP166 Stage 4B: October 20 Forever companion BONUS-hit conversion.
/// Tests inspect actual attack results from the two production runners,
/// preserving each target's base miss/avoidance table and explicit overrides.
/// </summary>
public sealed class Cp166Stage4BPetFormulaTests
{
    private static BeastMasteryHunterSimRequest Hunter(
        string family, decimal bonus = 0m, decimal expertise = 0m)
    {
        var r = new BeastMasteryHunterSimRequest {
            DurationSeconds = 240m, TargetMaximumHealth = 10000000m,
            PetFamily = family, Seed = 166420,
            CaptureTimeline = true, UseSummonHawk = false,
            UseRapidFire = false, UseSerpentSting = false,
            UseAimedShot = false, UseMultiShot = false,
            UseArcaneShot = false, UsePetBite = false,
            UsePetClaw = false, UseBestialWrath = false,
            UseIntimidation = false
        };
        r.Tuning.PetHitPercent = null; // production owner-inheritance path
        r.Tuning.HunterRangedHitBonusPercent = bonus;
        r.Tuning.HunterExpertisePercent = expertise;
        r.Tuning.PetAvoidancePercent = 0m;
        r.Tuning.HunterCriticalPercent = 0m;
        r.Tuning.HunterMeleeCriticalPercent = 0m;
        r.Tuning.PetCriticalBonusPercent = 0m;
        return r;
    }

    private static WarlockSimRequest Warlock(string pet, decimal ownerSpellBonus = 0m)
    {
        var r = WarlockSimRequest.CreatePreset("destruction");
        r.DurationSeconds = 240m;
        r.TargetMaximumHealth = 10000000m;
        r.PetType = pet;
        r.Seed = 166421;
        r.CaptureTimeline = true;
        r.PetMeleeHitPercent = null;
        r.DemonTuning.DemonSpellHitPercent = null;
        r.Character.BaseStats.Set(ForeverCombatStatKeys.SpellHitChancePercent,
            ownerSpellBonus);
        r.TargetDodgePercent = 0m;
        r.TargetParryPercent = 0m;
        r.SpellCritPercent = 0m;
        return r;
    }

    private static CombatEvent[] PetDamage(BeastMasteryHunterSimResponse run,
        string ability) => run.Run.Timeline.Where(e =>
            e.SourceActorKey == "hunter-pet" &&
            e.Type == CombatEventType.Damage && e.AbilityKey == ability).ToArray();

    private static CombatEvent[] DemonDamage(WarlockSimResponse run, string ability) =>
        run.Run.Timeline.Where(e =>
            e.SourceActorKey == "warlock-pet" &&
            e.Type == CombatEventType.Damage && e.AbilityKey == ability).ToArray();

    [Fact]
    public void HunterRangedBonusIncreasesRealPetMeleeHitWithoutReplacingBaseMissChance()
    {
        var baseline = PetDamage(new BeastMasteryHunterSimRunner().Run(
            Hunter("bear")), "pet-melee");
        var capped = PetDamage(new BeastMasteryHunterSimRunner().Run(
            Hunter("bear", bonus: 8m)), "pet-melee");
        Assert.True(baseline.Length > 30);
        Assert.Contains(baseline, e => e.ResultKey == "miss");
        Assert.DoesNotContain(capped, e => e.ResultKey == "miss");
        Assert.Equal(8m, ForeverCompanionHitInheritance.HunterMeleeBonus(8m));
        Assert.Equal(100m, ForeverCompanionHitInheritance.FinalHit(
            ForeverCompanionHitInheritance.CreatureMeleeBaseHit(60,63), 8m));
    }

    [Fact]
    public void HunterRangedBonusUsesSeventeenEighthsForMagicalPetSpecials()
    {
        var baseline = PetDamage(new BeastMasteryHunterSimRunner().Run(
            Hunter("wind-serpent")), "pet-lightning-breath");
        var capped = PetDamage(new BeastMasteryHunterSimRunner().Run(
            Hunter("wind-serpent",bonus:8m)), "pet-lightning-breath");
        Assert.True(baseline.Length >= 10);
        Assert.Contains(baseline, e => e.ResultKey == "miss");
        Assert.DoesNotContain(capped, e => e.ResultKey == "miss");
        Assert.Equal(17m, ForeverCompanionHitInheritance.HunterSpellBonus(8m));
        var magic = ForeverBeastMasteryHunterSpellbook.Pet(
            ForeverHunterPetFamilies.Get("wind-serpent"),
            new ForeverBeastMasteryHunterTuning()).Single(a =>
                a.Key == "pet-lightning-breath").Effects.Single();
        Assert.Equal(CombatResolutionTypes.Spell, magic.ResolutionType);
    }

    [Fact]
    public void DemonSpellsInheritOwnersBonusAndKeepBossSpellMissTable()
    {
        var baseline = DemonDamage(new WarlockSimRunner().Run(Warlock("imp")),
            "demon-firebolt");
        var capped = DemonDamage(new WarlockSimRunner().Run(Warlock("imp",17m)),
            "demon-firebolt");
        Assert.True(baseline.Length >= 10);
        Assert.Contains(baseline, e => e.ResultKey == "miss");
        Assert.DoesNotContain(capped, e => e.ResultKey == "miss");
        Assert.Equal(17m,ForeverCompanionHitInheritance.WarlockSpellBonus(17m));
        Assert.Equal(83m,ForeverCompanionHitInheritance.SpellBaseHit(60,63));
    }

    [Fact]
    public void DemonMeleeInheritsEightSeventeenthsOwnerSpellBonus()
    {
        var baseline = DemonDamage(new WarlockSimRunner().Run(Warlock("voidwalker")),
            "pet-melee");
        var capped = DemonDamage(new WarlockSimRunner().Run(Warlock("voidwalker",17m)),
            "pet-melee");
        Assert.True(baseline.Length >= 30);
        Assert.Contains(baseline,e=>e.ResultKey=="miss");
        Assert.DoesNotContain(capped,e=>e.ResultKey=="miss");
        Assert.Equal(8m,ForeverCompanionHitInheritance.WarlockMeleeBonus(17m));
    }

    [Fact]
    public void WarlockIntermediateOwnerSpellBonusUsesReducedMeleeFractionInActualCombat()
    {
        // At +3 levels the provisional creature melee base chance is 94.4%.
        // An owner with 8.5% spell bonus gives just 4% melee bonus, hence
        // this is NOT capped (98.4%). Misapplying 1:1 would cap melee at 100%.
        var partial=Warlock("voidwalker",8.5m);
        partial.DurationSeconds=720m;
        var melee=DemonDamage(new WarlockSimRunner().Run(partial),"pet-melee");
        Assert.True(melee.Length>150);
        Assert.Contains(melee,e=>e.ResultKey=="miss");
        Assert.Equal(4m,ForeverCompanionHitInheritance.WarlockMeleeBonus(8.5m));

        // Demon spell hit remains a separate 83% base + 8.5% inherited bonus.
        // Misapplying the Hunter's 17/8 multiplier would incorrectly cap it.
        var caster=Warlock("imp",8.5m);
        caster.DurationSeconds=720m;
        var magic=DemonDamage(new WarlockSimRunner().Run(caster),"demon-firebolt");
        Assert.True(magic.Length>30);
        Assert.Contains(magic,e=>e.ResultKey=="miss");
        Assert.Equal(91.5m,ForeverCompanionHitInheritance.FinalHit(
            ForeverCompanionHitInheritance.SpellBaseHit(60,63),
            ForeverCompanionHitInheritance.WarlockSpellBonus(8.5m)));
    }

    [Fact]
    public void ExplicitFinalHitOverridesRemainAvailableForControlledTests()
    {
        var forcedHunter=Hunter("wind-serpent",bonus:100m);
        forcedHunter.Tuning.PetHitPercent=0m;
        var hunter=PetDamage(new BeastMasteryHunterSimRunner().Run(forcedHunter),
            "pet-lightning-breath");
        Assert.NotEmpty(hunter);
        Assert.All(hunter,e=>Assert.Equal("miss",e.ResultKey));
        var forcedDemon=Warlock("imp",ownerSpellBonus:100m);
        forcedDemon.DemonTuning.DemonSpellHitPercent=0m;
        var demon=DemonDamage(new WarlockSimRunner().Run(forcedDemon),"demon-firebolt");
        Assert.NotEmpty(demon);
        Assert.All(demon,e=>Assert.Equal("miss",e.ResultKey));
        var forcedMelee=Warlock("voidwalker",ownerSpellBonus:100m);
        forcedMelee.PetMeleeHitPercent=0m;
        var melee=DemonDamage(new WarlockSimRunner().Run(forcedMelee),"pet-melee");
        Assert.NotEmpty(melee);
        Assert.All(melee,e=>Assert.Equal("miss",e.ResultKey));
    }

    [Fact]
    public void HunterExpertiseReducesPetMeleeAvoidanceButNeverMagicMiss()
    {
        var noExpertise=Hunter("bear");
        noExpertise.Tuning.PetHitPercent=100m;
        noExpertise.Tuning.PetAvoidancePercent=100m;
        var dodged=PetDamage(new BeastMasteryHunterSimRunner().Run(noExpertise),
            "pet-melee");
        Assert.NotEmpty(dodged);
        Assert.All(dodged,e=>Assert.Equal("dodge",e.ResultKey));

        var fullExpertise=Hunter("bear",expertise:100m);
        fullExpertise.Tuning.PetHitPercent=100m;
        fullExpertise.Tuning.PetAvoidancePercent=100m;
        var landed=PetDamage(new BeastMasteryHunterSimRunner().Run(fullExpertise),
            "pet-melee");
        Assert.NotEmpty(landed);
        Assert.DoesNotContain(landed,e=>e.ResultKey is "dodge" or "parry");
        Assert.Contains(landed,e=>e.Amount>0m);

        var spell=Hunter("wind-serpent",expertise:100m);
        spell.Tuning.PetHitPercent=0m;
        spell.Tuning.PetAvoidancePercent=100m;
        var spells=PetDamage(new BeastMasteryHunterSimRunner().Run(spell),
            "pet-lightning-breath");
        Assert.NotEmpty(spells);
        Assert.All(spells,e=>Assert.Equal("miss",e.ResultKey));
    }

    [Theory]
    [InlineData(100,0)]
    [InlineData(0,100)]
    public void HunterPetsInheritHigherOwnerCritAndRetainSeparatePetTalents(
        decimal ranged,decimal melee)
    {
        var req=Hunter("bear",bonus:100m);
        req.Tuning.HunterCriticalPercent=ranged;
        req.Tuning.HunterMeleeCriticalPercent=melee;
        var attacks=PetDamage(new BeastMasteryHunterSimRunner().Run(req),
            "pet-melee").Where(e=>e.Amount>0m).ToArray();
        Assert.True(attacks.Length>30);
        Assert.All(attacks,e=>Assert.True(e.IsCritical));
        Assert.True(req.Talents.Rank("ferocity")>=0);
    }

    [Fact]
    public void HunterAndWarlockSimulationDamageAttributionStillBalances()
    {
        var hunt=new BeastMasteryHunterSimRunner().Run(Hunter("scorpid",5m));
        Assert.Equal(hunt.HunterDamage+hunt.PetDamage+hunt.HawkDamage,hunt.DamageDone);
        foreach(var demon in new[]{"imp","voidwalker","succubus","incubus",
                    "felhunter","infernal","doomguard"})
        {
            var req=Warlock(demon,7m);
            req.DurationSeconds=50m;
            if(demon is "infernal" or "doomguard")
            {
                req.AllowTemporaryDemon=true;
                req.AllowDoomguardRitual=true;
            }
            var result=new WarlockSimRunner().Run(req);
            Assert.InRange(Math.Abs(result.WarlockDamage+result.PetDamage-
                result.DamageDone),0m,.001m);
        }
    }
}
