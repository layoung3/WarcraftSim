using System;
using System.Collections.Generic;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Hunter;
using WarcraftSim.Data.Forever.Combat;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175F3D2SurvivalMechanicalCompletion
{
    private static ForeverSurvivalHunterTalents Talents(int lightning=5,int lacerating=1)
    {
        return new ForeverSurvivalHunterTalents {
            Ranks=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase) {
                ["improved-tracking"]=5,["deflection"]=5,["entrapment"]=5,
                ["savage-strikes"]=2,["survivalist"]=4,["improved-wing-clip"]=3,
                ["clever-traps"]=2,["surefooted"]=3,["deterrence"]=1,
                ["survival-tactics"]=2,["predators-edge"]=5,
                ["counterattack"]=1,["resourcefulness"]=2,["expose-prey"]=2,
                ["survivalists-discipline"]=2,["strider-kick"]=1,
                ["lightning-reflexes"]=lightning,["lacerating-strikes"]=lacerating
            }
        };
    }

    [Theory]
    [InlineData(0,0,0)]
    [InlineData(1,6.6,0.1247637051039697542533081285)]
    [InlineData(2,13.2,0.2495274102079395085066162571)]
    [InlineData(3,19.8,0.3742911153119092627599243856)]
    [InlineData(4,26.4,0.4990548204158790170132325142)]
    [InlineData(5,33,0.6238185255198487712665406427)]
    public void LightningReflexesUsesApprovedProfileForAPCritAndEffectiveAgility(
        int rank,double bonus,double expectedCrit)
    {
        var t=new ForeverSurvivalHunterTuning {
            Agility=220m,MeleeAttackPower=950m,RangedAttackPower=1000m,
            CriticalChancePercent=18m
        };
        var stats=new StatCollection();
        SurvivalHunterAgilityStats.Initialize(stats,t,rank);
        Assert.Equal((decimal)bonus,SurvivalHunterAgilityStats.TalentBonus(220m,rank));
        Assert.Equal(220m+(decimal)bonus,stats.Get("hunter-effective-agility"));
        Assert.Equal(950m+(decimal)bonus,stats.Get("attack-power"));
        Assert.Equal(1000m+2m*(decimal)bonus,stats.Get("hunter-ranged-attack-power"));
        Assert.Equal(18m+(decimal)bonus/52.9m,stats.Get("melee-critical-chance-percent"));
        Assert.Equal(stats.Get("melee-critical-chance-percent"),
            stats.Get("ranged-critical-chance-percent"));
        Assert.InRange(Math.Abs((double)(stats.Get("ranged-critical-chance-percent")-
            18m)-expectedCrit),0d,.00000000001d);
        Assert.Equal(0m,stats.Get("hunter-dodge-chance-percent"));
    }

    [Fact]
    public void DynamicAgilityApplicationAndExpirationReversesOnlyTalentIncrement()
    {
        var t=new ForeverSurvivalHunterTuning {
            Agility=220m,MeleeAttackPower=950m,RangedAttackPower=1000m,
            CriticalChancePercent=18m,AgilityPerDodgePercentAt60=26.5m
        };
        t.Validate();
        var ctx=new SimulationContext(new SimulationRunOptions {DurationSeconds=30m});
        var owner=new SimulationActorState {
            Key="hunter",Level=60,TeamKey="raid",MaximumHealth=3900m
        };
        owner.InitializeHealth(3900m);
        owner.AddResource(new ResourceState("mana",4200m,4200m));
        ctx.AddActor(owner);
        SurvivalHunterAgilityStats.Initialize(owner.Stats,t,5);
        var baseAp=owner.Stats.Get("attack-power");
        var baseRap=owner.Stats.Get("hunter-ranged-attack-power");
        var baseCrit=owner.Stats.Get("melee-critical-chance-percent");
        var baseDodge=owner.Stats.Get("hunter-dodge-chance-percent");
        var deltas=new Dictionary<string,decimal>(StringComparer.OrdinalIgnoreCase) {
            ["agility"]=40m,["attack-power"]=40m,["ranged-attack-power"]=80m,
            ["attack-critical-chance-percent"]=40m/52.9m,
            ["armor"]=80m
        };
        var petTuning=new ForeverBeastMasteryHunterTuning();
        SurvivalHunterAgilityStats.ApplyRuntimeDelta(ctx,owner,null,petTuning,t,5,deltas);
        Assert.Equal(baseAp+46m,owner.Stats.Get("attack-power"));
        Assert.Equal(baseRap+92m,owner.Stats.Get("hunter-ranged-attack-power"));
        Assert.Equal(baseCrit+46m/52.9m,owner.Stats.Get("melee-critical-chance-percent"));
        Assert.Equal(baseDodge+6m/26.5m,owner.Stats.Get("hunter-dodge-chance-percent"));
        Assert.Equal(299m,owner.Stats.Get("hunter-effective-agility"));
        var reversed=deltas.ToDictionary(x=>x.Key,x=>-x.Value,StringComparer.OrdinalIgnoreCase);
        SurvivalHunterAgilityStats.ApplyRuntimeDelta(ctx,owner,null,petTuning,t,5,reversed);
        Assert.Equal(baseAp,owner.Stats.Get("attack-power"));
        Assert.Equal(baseRap,owner.Stats.Get("hunter-ranged-attack-power"));
        Assert.Equal(baseCrit,owner.Stats.Get("melee-critical-chance-percent"));
        Assert.Equal(baseDodge,owner.Stats.Get("hunter-dodge-chance-percent"));
        Assert.Equal(253m,owner.Stats.Get("hunter-effective-agility"));
    }

    [Fact]
    public void UnknownServerDodgeCoefficientStaysExplicitAndNeverInventsNumber()
    {
        var t=new ForeverSurvivalHunterTuning{Agility=220m};
        Assert.Null(t.AgilityPerDodgePercentAt60);
        Assert.Null(SurvivalHunterAgilityStats.DodgeBonus(220m,5,null));
        t.AgilityPerDodgePercentAt60=26.5m; // explicit Classic-derived fixture, NOT verified Forever
        Assert.Equal(33m/26.5m,
            SurvivalHunterAgilityStats.DodgeBonus(220m,5,t.AgilityPerDodgePercentAt60));
        t.AgilityPerDodgePercentAt60=0m;
        Assert.Throws<ArgumentException>(()=>t.Validate());
    }

    [Fact]
    public void WeakerLaceratingStrikesRefreshReplacesSameHunterInsteadOfProtectingOldDamage()
    {
        var ledger=new SurvivalLaceratingBleedLedger();
        var strong=ledger.Replace("target",700m);
        Assert.Equal(40m,strong.TickDamage);
        var other=ledger.Replace("add-1",280m);
        Assert.True(ledger.IsCurrent("add-1",other.Generation));
        var weak=ledger.Replace("target",350m);
        Assert.Equal(20m,weak.TickDamage);
        Assert.True(ledger.IsCurrent("target",weak.Generation));
        Assert.False(ledger.IsCurrent("target",strong.Generation));
        Assert.True(ledger.IsCurrent("add-1",other.Generation));
        var same=ledger.Replace("target",350m);
        Assert.Equal(20m,same.TickDamage);
        Assert.False(ledger.IsCurrent("target",weak.Generation));
        Assert.True(ledger.IsCurrent("target",same.Generation));
    }

    private static SurvivalHunterSimRequest Survival(
        int lacerating=1,int lightning=5, bool pet=false)
    {
        var t=Talents(lightning,lacerating);
        t.Validate();
        var r=new SurvivalHunterSimRequest {
            Talents=t,DurationSeconds=50m,
            Seed=175521,TargetMaximumHealth=1_000_000m,
            CombatMode="melee",HasPet=pet,PetFamily="cat",
            AdditionalTargets=0,UseRapidFire=false,UseFireTraps=false,
            UseFrostTraps=false,UseAimedShot=false,UseMultiShot=false,
            UseVolley=false,UseWingClip=false,ApplyHuntersMark=false,
            ScriptedAvoidanceEverySeconds=25m,
            CustomPriority=["mongoose-bite"]
        };
        r.Tuning.HitChancePercent=100m;
        r.Tuning.CriticalChancePercent=0m;
        r.Tuning.TargetDodgePercent=0m;
        r.Tuning.TargetParryPercent=0m;
        r.Tuning.TargetArmor=0m;
        r.PetTuning.PetHitPercent=100m;
        r.PetTuning.PetAvoidancePercent=0m;
        return r;
    }

    [Fact]
    public void LandedMongooseBiteCreatesSevenExactBleedTicksAndOwnedAura()
    {
        var req=Survival(lacerating:1,lightning:0);
        var run=new SurvivalHunterSimRunner().Run(req);
        var mongoose=Assert.Single(run.Run.Timeline,x=>x.AbilityKey=="mongoose-bite" &&
            x.Type==CombatEventType.Damage && x.Amount is >0m);
        var bleed=run.Run.Timeline.Where(x=>x.AbilityKey=="lacerating-strikes" &&
            x.Type==CombatEventType.Damage && x.IsPeriodic).ToArray();
        Assert.Equal(7,bleed.Length);
        Assert.Equal(7m,run.Run.CustomMetrics["survival-lacerating-bleed-ticks"]);
        Assert.All(bleed,x=>Assert.Equal(mongoose.Amount!.Value*.40m/7m,
            x.RawAmount));
        for(int index=0;index<7;index++)
            Assert.Equal(mongoose.TimeSeconds+3m*(index+1),bleed[index].TimeSeconds);
        Assert.Contains(run.Run.Timeline,x=>x.Type==CombatEventType.AuraApplied &&
            x.AbilityKey=="lacerating-strikes" &&
            x.SourceActorKey=="hunter" && x.TargetActorKey=="target");
        Assert.Contains(run.Run.Timeline,x=>x.Type==CombatEventType.AuraRemoved &&
            x.AbilityKey=="lacerating-strikes" &&
            x.TimeSeconds==mongoose.TimeSeconds+21m);
        var removed=Survival(lacerating:0,lightning:0);
        var without=new SurvivalHunterSimRunner().Run(removed);
        Assert.DoesNotContain(without.Run.Timeline,x=>
            x.AbilityKey=="lacerating-strikes" && x.Type==CombatEventType.Damage);
    }

    [Fact]
    public void SurvivalMeleeActionsRemainReactiveAndManaConstrained()
    {
        var r=Survival();
        r.CustomPriority=["mongoose-bite","counterattack","raptor-strike"];
        r.ScriptedAvoidanceEverySeconds=12m;
        var run=new SurvivalHunterSimRunner().Run(r);
        Assert.Contains(run.Run.Timeline,x=>x.AbilityKey=="raptor-strike" &&
            x.Type==CombatEventType.Damage && x.Amount is >0m);
        Assert.Contains(run.Run.Timeline,x=>x.AbilityKey=="mongoose-bite" &&
            x.Type==CombatEventType.Damage && x.Amount is >0m);
        Assert.Contains(run.Run.Timeline,x=>x.AbilityKey=="counterattack" &&
            x.Type==CombatEventType.Damage && x.Amount is >0m);
        Assert.Contains(run.Run.Timeline,x=>x.AbilityKey=="raptor-strike" &&
            x.Type==CombatEventType.ResourceChanged && x.Amount is <0m);
        Assert.DoesNotContain(run.Run.Timeline,x=>x.AbilityKey=="mongoose-bite" &&
            x.Type==CombatEventType.Damage && x.TimeSeconds<12m);
    }

    [Theory]
    [InlineData(0,30)]
    [InlineData(1,24)]
    [InlineData(2,18)]
    public void SharedFireAndFrostTrapCooldownScalesWithDiscipline(int ranks,int cooldown)
    {
        var t=Talents();
        t.Ranks["survivalists-discipline"]=ranks;
        var spells=ForeverSurvivalHunterSpellbook.Create(t,
            new ForeverSurvivalHunterTuning());
        var traps=spells.Where(x=>x.Key is ("immolation-trap" or
            "explosive-trap" or "frost-trap" or "freezing-trap")).ToArray();
        Assert.Equal(4,traps.Length);
        Assert.All(traps,x=>{
            Assert.Equal("hunter-traps",x.SharedCooldownGroupKey);
            Assert.Equal((decimal)cooldown,x.SharedCooldownSeconds);
            Assert.Equal((decimal)cooldown,x.CooldownSeconds);
        });
    }

    [Fact]
    public void LegalSurvivalAndBeastMasteryPetTalentAllocationExecutesBothActors()
    {
        var r=Survival(pet:true);
        r.Talents.Ranks["deflection"]=0;
        r.Talents.Ranks["improved-wing-clip"]=1;
        // Simulate the actual unified itemized frontend: all selected
        // ranks arrive in the starting-preset talents dictionary, then the
        // runner routes eligible BM ranks to the existing pet talent owner.
        r.Talents.Ranks["endurance-training"]=5;
        r.Talents.Ranks["focused-fire"]=2;
        Assert.Equal(51,r.Talents.Ranks.Values.Sum());
        Assert.Empty(r.PetTalents.Ranks);
        var first=new SurvivalHunterSimRunner().Run(r);
        var second=new SurvivalHunterSimRunner().Run(r);
        Assert.True(first.HunterDamage>0m);
        Assert.True(first.PetDamage>0m);
        Assert.Contains(first.Run.Timeline,x=>x.Type==CombatEventType.Damage &&
            x.SourceActorKey=="hunter-pet" && x.Amount is >0m);
        Assert.Equal(first.DamageDone,second.DamageDone);
        Assert.Equal(first.PetDamage,second.PetDamage);
    }

    [Fact]
    public void MarksmanshipStartingPresetExecutesLegalFivePointSurvivalTrackingPassive()
    {
        MarksmanshipHunterSimRequest Build(bool tracked)
        {
            var mm=new WarcraftSim.Data.Forever.Hunter.ForeverMarksmanshipHunterTalents {
                MarksmanshipPointsSpent=46,SurvivalPointsSpent=5,
                BeastMasteryPointsSpent=0,
                Ranks=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase) {
                    ["lethal-attacks"]=5,["efficiency"]=5,["careful-aim"]=5,
                    ["lone-wolf"]=1,["improved-stings"]=3,["barrage"]=3,
                    ["mortal-shots"]=5,["trueshot-aura"]=1,["sniper-shot"]=1,
                    ["improved-arcane-shot"]=5,["hawk-eye"]=2,
                    ["rapid-killing"]=2,["rapid-recuperation"]=2,
                    ["ranged-weapon-specialization"]=5,["scatter-shot"]=1,
                    ["improved-tracking"]=5
                }
            };
            Assert.Equal(51,mm.Ranks.Values.Sum());
            var req=new MarksmanshipHunterSimRequest {
                Seed=175528,DurationSeconds=20m,CaptureTimeline=true,
                HasPet=false,Talents=mm,TargetIsTracked=tracked,
                TargetMaximumHealth=1000000m,UseRapidFire=false,
                CustomPriority=["arcane-shot"]
            };
            req.Tuning.HitChancePercent=100m;
            req.Tuning.CriticalChancePercent=0m;
            req.Tuning.TargetArmor=0m;
            return req;
        }
        var untracked=new MarksmanshipHunterSimRunner().Run(Build(false));
        var tracked=new MarksmanshipHunterSimRunner().Run(Build(true));
        decimal Shot(MarksmanshipHunterSimResponse r)=>
            r.Run.Timeline.First(x=>x.Type==CombatEventType.Damage &&
                x.SourceActorKey=="hunter" && x.AbilityKey=="arcane-shot" &&
                x.Amount is >0m).Amount!.Value;
        Assert.Equal(Shot(untracked)*1.05m,Shot(tracked));
        Assert.Equal(0m,tracked.PetDamage);
        var replay=new MarksmanshipHunterSimRunner().Run(Build(true));
        Assert.Equal(tracked.DamageDone,replay.DamageDone);
    }

    [Fact]
    public void UnlearnedOrUnsupportedSurvivalCapstoneCannotEnterMarksmanship()
    {
        var req=new MarksmanshipHunterSimRequest {
            TargetIsTracked=true,HasPet=false,
            DurationSeconds=5m,CustomPriority=["arcane-shot"]
        };
        req.Talents.Ranks["lacerating-strikes"]=1;
        Assert.Throws<ArgumentException>(()=>new MarksmanshipHunterSimRunner().Run(req));
    }

    [Fact]
    public void RemovedCounterattackTalentRejectsStaleSelectedAction()
    {
        var r=Survival();
        r.Talents.Ranks["counterattack"]=0;
        r.CustomPriority=["counterattack"];
        Assert.Throws<ArgumentException>(()=>new SurvivalHunterSimRunner().Run(r));
    }
}
