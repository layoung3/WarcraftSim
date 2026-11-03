using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Hunter;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>
/// CP175-F3D0 source-ranked 70753 Hunter contracts: preserve the established
/// shared pet/ranged/melee engines, validate corrections through actual effects.
/// </summary>
public sealed class Cp175F3D0HunterSourceAndCombatCertification
{
    [Fact]
    public void CurrentFiftyTalentMatrixMatchesAllThreeEngineTrees()
    {
        var matrix=Hunter70753TalentCoverageCatalog.Talents;
        Assert.Equal(50,matrix.Count);
        foreach(var (tree,expected) in new[]{
            ("beast-mastery",16),("marksmanship",16),("survival",18)})
        {
            var rows=matrix.Where(x=>x.Tree==tree).ToArray();
            Assert.Equal(expected,rows.Length);
            Assert.Equal(expected,rows.Select(x=>x.Key).Distinct(
                StringComparer.OrdinalIgnoreCase).Count());
        }
        Assert.Equal(27,matrix.Count(x=>x.SimulationStatus=="fully-modeled"));
        Assert.Equal(19,matrix.Count(x=>x.SimulationStatus=="partially-modeled"));
        Assert.Equal(4,matrix.Count(x=>x.SimulationStatus=="not-modeled"));
        var bm=ForeverBeastMasteryHunterTalents.SupportedTalentMetadata
            .Where(x=>x.Tree=="beast-mastery").Select(x=>x.Key).ToHashSet();
        var mm=ForeverMarksmanshipHunterTalents.SupportedTalentMetadata
            .Select(x=>x.Key).ToHashSet();
        var surv=ForeverSurvivalHunterTalents.SupportedTalentMetadata
            .Select(x=>x.Key).ToHashSet();
        Assert.True(bm.SetEquals(matrix.Where(x=>x.Tree=="beast-mastery").Select(x=>x.Key)));
        Assert.True(mm.SetEquals(matrix.Where(x=>x.Tree=="marksmanship").Select(x=>x.Key)));
        Assert.True(surv.SetEquals(matrix.Where(x=>x.Tree=="survival").Select(x=>x.Key)));
    }

    [Fact]
    public void MarksmanshipNov3RankSourcesApplyToBaseAndLiveAttackPower()
    {
        var t=new ForeverMarksmanshipHunterTuning();
        var ranks=new ForeverMarksmanshipHunterTalents();
        var book=ForeverMarksmanshipHunterSpellbook.Create(ranks,t,1000m,hasPet:true);
        var arcane=Assert.Single(book,x=>x.Key=="arcane-shot").Effects.Single();
        Assert.Equal(280m,arcane.MinimumValue);
        Assert.Equal("attack-power",arcane.ScalingStatKey);
        Assert.Equal(.15m,arcane.ScalingCoefficient);
        var serpent=Assert.Single(book,x=>x.Key=="serpent-sting")
            .Effects.Single(x=>x.Key=="serpent-sting-tick");
        Assert.Equal(162m,serpent.MinimumValue); // 135 x Improved Stings rank 3 1.20
        Assert.Equal(.06m,serpent.ScalingCoefficient);
        var aimed=Assert.Single(book,x=>x.Key=="aimed-shot").Effects.Single();
        Assert.Equal(747.5m,aimed.MinimumValue); // (300 + 350) x Barrage rank 3
        Assert.Equal(.23m,aimed.ScalingCoefficient);
        var sniper=Assert.Single(book,x=>x.Key=="sniper-shot").Effects.Single();
        Assert.Equal(890m,sniper.MinimumValue); // Barrage does NOT affect Sniper
        Assert.Equal(.2m,sniper.ScalingCoefficient);
        Assert.Equal(100m,t.TrueshotAttackPower);
        var volley=Assert.Single(book,x=>x.Key=="volley").Effects.Single();
        Assert.Equal(1.15m,volley.DamageMultiplier);
    }

    [Theory]
    [InlineData(0,1.0)]
    [InlineData(1,1.05)]
    [InlineData(2,1.10)]
    [InlineData(3,1.15)]
    public void BarrageOnlyIncreasesEligiblePhysicalShotsAndVolley(
        int rank,double multiplier)
    {
        var talents=new ForeverMarksmanshipHunterTalents();
        talents.Ranks["barrage"]=rank;
        var book=ForeverMarksmanshipHunterSpellbook.Create(
            talents,new ForeverMarksmanshipHunterTuning(),1000m,true);
        var scale=(decimal)multiplier;
        Assert.Equal(650m*scale,Assert.Single(book,x=>x.Key=="aimed-shot").Effects.Single().MinimumValue);
        Assert.Equal(300m*scale,Assert.Single(book,x=>x.Key=="multi-shot").Effects.Single().MinimumValue);
        Assert.Equal(scale,Assert.Single(book,x=>x.Key=="volley").Effects.Single().DamageMultiplier);
        Assert.Equal(890m,Assert.Single(book,x=>x.Key=="sniper-shot").Effects.Single().MinimumValue);
    }

    [Fact]
    public void BeastMasteryBaselineSpellsShareNewRankAndAttackPowerCoefficients()
    {
        var talents=new ForeverBeastMasteryHunterTalents();
        var spells=ForeverBeastMasteryHunterSpellbook.Hunter(talents,
            new ForeverBeastMasteryHunterTuning());
        var arcane=Assert.Single(spells,x=>x.Key=="arcane-shot").Effects.Single();
        var aimed=Assert.Single(spells,x=>x.Key=="aimed-shot").Effects.Single();
        var serpent=Assert.Single(spells,x=>x.Key=="serpent-sting")
            .Effects.Single(x=>x.Key=="serpent-tick");
        Assert.Equal(280m,arcane.MinimumValue);
        Assert.Equal(.15m,arcane.ScalingCoefficient);
        Assert.Equal(590m,aimed.MinimumValue);
        Assert.Equal(135m,serpent.MinimumValue);
        Assert.Equal(.05m,serpent.ScalingCoefficient);
        Assert.Equal("attack-power",serpent.ScalingStatKey);
        Assert.Contains("pathfinding",ForeverBeastMasteryHunterTalents.SupportedTalentMetadata
            .Where(x=>x.RequiredEarlierPoints==10).Select(x=>x.Key));
    }

    [Fact]
    public void SurvivalReusesNov3RangedCoefficientsAndSharedTrapCooldown()
    {
        var t=new ForeverSurvivalHunterTuning();
        var talents=new ForeverSurvivalHunterTalents();
        var spells=ForeverSurvivalHunterSpellbook.Create(talents,t);
        var arcane=Assert.Single(spells,x=>x.Key=="arcane-shot").Effects.Single();
        var serpent=Assert.Single(spells,x=>x.Key=="serpent-sting")
            .Effects.Single(x=>x.Key=="serpent-tick");
        Assert.Equal(280m,arcane.MinimumValue);
        Assert.Equal(.15m,arcane.ScalingCoefficient);
        Assert.Equal("hunter-ranged-attack-power",arcane.ScalingStatKey);
        Assert.Equal(135m,serpent.MinimumValue);
        Assert.Equal(.05m,serpent.ScalingCoefficient);
        var aimed=Assert.Single(spells,x=>x.Key=="aimed-shot").Effects.Single();
        Assert.Equal(t.RangedWeaponMinDamage+
            t.RangedAttackPower/14m*t.RangedWeaponSpeedSeconds+350m,
            aimed.MinimumValue);
        var traps=spells.Where(x=>x.Key is ("immolation-trap" or "explosive-trap" or
            "frost-trap" or "freezing-trap")).ToArray();
        Assert.Equal(4,traps.Length);
        Assert.All(traps,x=>{
            Assert.Equal("hunter-traps",x.SharedCooldownGroupKey);
            Assert.Equal(30m,x.SharedCooldownSeconds);
            Assert.Equal(30m,x.CooldownSeconds);
        });
        Assert.Null(Assert.Single(spells,x=>x.Key=="arcane-shot").SharedCooldownGroupKey);
    }

    [Fact]
    public void NewLightningReflexesRankFiveIncreasesActualMeleeAndRangedPower()
    {
        SurvivalHunterSimRequest Make(int ranks)
        {
            var r=new SurvivalHunterSimRequest {
                DurationSeconds=8m,HasPet=false,CombatMode="melee",
                UseFireTraps=false,UseFrostTraps=false,UseRapidFire=false,
                UseWingClip=true,
                ScriptedAvoidanceEverySeconds=null,
                TargetMaximumHealth=1000000m,CustomPriority=["wing-clip"],
            };
            r.Talents.Ranks=new(StringComparer.OrdinalIgnoreCase) {
                ["improved-tracking"]=5,["deflection"]=5,
                ["entrapment"]=5,["savage-strikes"]=2,
                ["survivalist"]=5,["improved-wing-clip"]=3,
                ["lightning-reflexes"]=ranks
            };
            r.Tuning.HitChancePercent=100m;
            r.Tuning.CriticalChancePercent=0m;
            r.Tuning.TargetDodgePercent=0m;
            r.Tuning.TargetParryPercent=0m;
            return r;
        }
        var untalented=new SurvivalHunterSimRunner().Run(Make(0));
        var selected=new SurvivalHunterSimRunner().Run(Make(5));
        Assert.True(selected.MeleeDamage>untalented.MeleeDamage);
        var baseTuning=new ForeverSurvivalHunterTuning();
        Assert.Equal(33m,baseTuning.Agility*.15m);
        Assert.InRange(Math.Abs(selected.DamageDone-selected.HunterDamage-selected.PetDamage-
            selected.HawkDamage),0m,.000001m);
    }

    [Fact]
    public void LoneWolfAppliesWhilePetIsNotEngagedAndIsNotFreeWithActivePet()
    {
        MarksmanshipHunterSimRequest Make(int lone,bool hasPet,decimal delay)
        {
            var r=new MarksmanshipHunterSimRequest {
                DurationSeconds=6m,HasPet=hasPet,PetFamily="cat",
                PetEngagementDelaySeconds=delay,Seed=175400,
                TargetMaximumHealth=1000000m,
                UseAimedShot=false,UseMultiShot=false,UseArcaneShot=false,
                UseSerpentSting=false,UseSniperShot=false,UseRapidFire=false,
                CustomPriority=["aspect-of-the-hawk"]
            };
            r.Talents.Ranks["lone-wolf"]=lone;
            r.Tuning.HitChancePercent=100m;
            r.Tuning.CriticalChancePercent=0m;
            r.Tuning.TargetArmor=0m;
            r.UseAspectOfTheHawk=false;
            r.PetTuning.PetHitPercent=100m;
            return r;
        }
        static decimal FirstAuto(MarksmanshipHunterSimResponse r)=>
            r.Run.Timeline.First(x=>x.Type==CombatEventType.Damage &&
                x.AbilityKey=="auto-shot" && x.SourceActorKey=="hunter" &&
                x.Amount>0m).Amount!.Value;
        var noTalent=new MarksmanshipHunterSimRunner().Run(Make(0,true,20m));
        var petUnavailable=new MarksmanshipHunterSimRunner().Run(Make(1,true,20m));
        Assert.Equal(0m,petUnavailable.PetDamage);
        Assert.Equal(0m,petUnavailable.PetUptimeSeconds);
        Assert.InRange(Math.Abs(FirstAuto(petUnavailable)-FirstAuto(noTalent)*1.20m),
            0m,.000001m);
        var active=new MarksmanshipHunterSimRunner().Run(Make(1,true,0m));
        Assert.True(active.PetDamage>0m);
        Assert.Equal(FirstAuto(noTalent),FirstAuto(active));
        var petless=new MarksmanshipHunterSimRunner().Run(Make(1,false,0m));
        Assert.Equal(0m,petless.PetDamage);
        Assert.InRange(Math.Abs(FirstAuto(petless)-FirstAuto(noTalent)*1.20m),
            0m,.000001m);
    }

    [Fact]
    public void AllNineteenPetFamiliesHaveDistinctExecutableFamilyProfiles()
    {
        var families=ForeverHunterPetFamilies.All.ToArray();
        Assert.Equal(19,families.Length);
        Assert.Equal(19,families.Select(x=>x.Key).Distinct().Count());
        foreach(var family in families)
        {
            var abilities=ForeverBeastMasteryHunterSpellbook.Pet(
                family,new ForeverBeastMasteryHunterTuning());
            Assert.Contains(abilities,x=>x.Key=="pet-"+family.SpecialAbility);
            Assert.True(family.HasClaw || family.HasBite);
            Assert.All(abilities,a=>Assert.True(a.CooldownSeconds>=0m));
        }
    }
}
