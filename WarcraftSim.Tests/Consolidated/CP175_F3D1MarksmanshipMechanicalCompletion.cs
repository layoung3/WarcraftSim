using System;
using System.Collections.Generic;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Hunter;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175F3D1MarksmanshipMechanicalCompletion
{
    private static ForeverMarksmanshipHunterTalents ScatterTalents(bool learned=true) =>
        new() {
            MarksmanshipPointsSpent=21,
            BeastMasteryPointsSpent=0,
            SurvivalPointsSpent=0,
            Ranks=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase) {
                ["scatter-shot"]=learned?1:0
            }
        };

    private static MarksmanshipHunterSimRequest ScatterRequest(
        bool learned=true,decimal hit=100m,List<string>? priority=null)
    {
        var r=new MarksmanshipHunterSimRequest {
            Seed=175510,DurationSeconds=9m,CaptureTimeline=true,
            TargetMaximumHealth=1_000_000m,HasPet=false,
            Talents=ScatterTalents(learned),
            UseRapidFire=false,UseSerpentSting=false,
            UseAimedShot=false,UseMultiShot=false,UseArcaneShot=false,
            UseSniperShot=false,
            CustomPriority=priority??["scatter-shot"]
        };
        r.Tuning.BaseMana=4000m;
        r.Tuning.MaximumMana=8000m;
        r.Tuning.RangedAttackPower=0m;
        r.Tuning.WeaponMinDamage=r.Tuning.WeaponMaxDamage=300m;
        r.Tuning.HitChancePercent=hit;
        r.Tuning.CriticalChancePercent=0m;
        r.Tuning.TargetArmor=0m;
        return r;
    }

    [Fact]
    public void ScatterShotUsesHalfWeaponAndRangedApButNeverBarrage()
    {
        var r=ScatterRequest();
        var spell=Assert.Single(ForeverMarksmanshipHunterSpellbook.Create(
            r.Talents,r.Tuning,0m),x=>x.Key=="scatter-shot");
        Assert.Equal(0m,spell.CastTimeSeconds);
        Assert.Equal(30m,spell.CooldownSeconds);
        Assert.Null(spell.SharedCooldownGroupKey);
        Assert.Equal(320m,spell.ResourceCosts.Single().Amount);
        var damage=Assert.Single(spell.Effects,x=>x.Key=="scatter-shot-impact");
        Assert.Equal(150m,damage.MinimumValue);
        Assert.Equal(150m,damage.MaximumValue);
        Assert.Equal(.10m,damage.ScalingCoefficient);
        Assert.Equal("attack-power",damage.ScalingStatKey);
        var aura=Assert.Single(spell.Effects,x=>x.Key=="scatter-shot-disorient");
        Assert.Equal(4m,aura.DurationSeconds);
        Assert.Equal("scatter-shot-impact",aura.DependsOnEffectKey);
        r.Talents.Ranks["barrage"]=3;
        var withBarrage=Assert.Single(ForeverMarksmanshipHunterSpellbook.Create(
            r.Talents,r.Tuning,0m),x=>x.Key=="scatter-shot");
        Assert.Equal(damage.MinimumValue,withBarrage.Effects[0].MinimumValue);
        Assert.Equal(damage.ScalingCoefficient,withBarrage.Effects[0].ScalingCoefficient);
    }

    [Fact]
    public void ScatterShotPaysBaseManaStopsAutoAndAppliesFourSecondAuraOnHit()
    {
        var run=new MarksmanshipHunterSimRunner().Run(ScatterRequest());
        var events=run.Run.Timeline;
        var hit=Assert.Single(events,x=>x.Type==CombatEventType.Damage &&
            x.AbilityKey=="scatter-shot" && x.Amount is >0m);
        Assert.Equal(150m,hit.Amount);
        var disorient=Assert.Single(events,x=>x.Type==CombatEventType.AuraApplied &&
            x.AbilityKey=="scatter-shot" && x.TargetActorKey=="target");
        Assert.Equal(hit.TimeSeconds,disorient.TimeSeconds);
        Assert.Contains(events,x=>x.Type==CombatEventType.AuraRemoved &&
            x.AbilityKey=="scatter-shot" &&
            x.TimeSeconds==disorient.TimeSeconds+4m);
        Assert.Equal(1m,run.Run.CustomMetrics["hunter-scatter-shot-auto-stops"]);
        Assert.Contains(events,x=>x.Type==CombatEventType.AutoAttackStopped &&
            x.SourceActorKey=="hunter" && x.AbilityKey=="auto-shot");
        Assert.DoesNotContain(events,x=>x.Type==CombatEventType.Damage &&
            x.AbilityKey=="auto-shot" && x.TimeSeconds>=hit.TimeSeconds);
        Assert.Contains(events,x=>x.Type==CombatEventType.ResourceChanged &&
            x.AbilityKey=="scatter-shot" && x.ResourceKey=="mana" && x.Amount==-320m);
    }

    [Fact]
    public void ScatterShotMissCannotGrantDisorientOrFreeDamage()
    {
        var r=ScatterRequest(hit:0m);
        var run=new MarksmanshipHunterSimRunner().Run(r);
        Assert.DoesNotContain(run.Run.Timeline,x=>x.Type==CombatEventType.AuraApplied &&
            x.AbilityKey=="scatter-shot");
        Assert.DoesNotContain(run.Run.Timeline,x=>x.Type==CombatEventType.Damage &&
            x.AbilityKey=="scatter-shot" && x.Amount is >0m);
        Assert.Equal(1m,run.Run.CustomMetrics["hunter-scatter-shot-auto-stops"]);
    }

    [Fact]
    public void LaterRealShotDamageBreaksScatterDisorient()
    {
        var r=ScatterRequest(priority:["scatter-shot","arcane-shot"]);
        var run=new MarksmanshipHunterSimRunner().Run(r);
        var applied=Assert.Single(run.Run.Timeline,x=>
            x.Type==CombatEventType.AuraApplied && x.AbilityKey=="scatter-shot");
        Assert.Contains(run.Run.Timeline,x=>x.Type==CombatEventType.Damage &&
            x.AbilityKey=="arcane-shot" && x.Amount is >0m);
        Assert.Equal(1m,run.Run.CustomMetrics["hunter-scatter-shot-disorient-breaks"]);
        var removed=Assert.Single(run.Run.Timeline,x=>
            x.Type==CombatEventType.AuraRemoved && x.AbilityKey=="scatter-shot");
        Assert.InRange(removed.TimeSeconds,applied.TimeSeconds,applied.TimeSeconds+4m);
        Assert.True(removed.TimeSeconds<applied.TimeSeconds+4m);
    }

    [Fact]
    public void ScatterShotRequiresLearnedTalentAndIsNeverForcedIntoAutoRotation()
    {
        Assert.Throws<ArgumentException>(()=>
            new MarksmanshipHunterSimRunner().Run(ScatterRequest(learned:false)));
        var noTalent=ScatterRequest(learned:false,priority:["arcane-shot"]);
        Assert.DoesNotContain(ForeverMarksmanshipHunterSpellbook.Create(
            noTalent.Talents,noTalent.Tuning,0m),x=>x.Key=="scatter-shot");
        var defaultRequest=ScatterRequest();
        defaultRequest.CustomPriority=[];
        var auto=new MarksmanshipHunterSimRunner().Run(defaultRequest);
        Assert.DoesNotContain(auto.Run.Timeline,x=>
            x.Type==CombatEventType.AbilityCastCompleted && x.AbilityKey=="scatter-shot");
    }

    [Theory]
    [InlineData(0,336)]
    [InlineData(2,315.84)]
    [InlineData(5,285.6)]
    public void EfficiencyDiscountsScatterBaseManaNotMaximum(int efficiency,double cost)
    {
        var r=ScatterRequest();
        r.Tuning.BaseMana=4200m;
        r.Tuning.MaximumMana=12000m;
        r.Talents.Ranks["efficiency"]=efficiency;
        var spell=Assert.Single(ForeverMarksmanshipHunterSpellbook.Create(
            r.Talents,r.Tuning,0m),x=>x.Key=="scatter-shot");
        Assert.Equal((decimal)cost,spell.ResourceCosts.Single().Amount);
    }

    [Fact]
    public void RapidKillingUsesActualKillOrSourceOwnedActiveSting()
    {
        var context=new SimulationContext(new SimulationRunOptions {
            Seed=175511,DurationSeconds=30m
        });
        var owner=new SimulationActorState {Key="hunter",TeamKey="raid",Level=60};
        var pet=new SimulationActorState {Key="hunter-pet",TeamKey="raid",Level=60};
        var enemy=new SimulationActorState {Key="target",TeamKey="enemy",Level=63};
        context.AddActor(owner);context.AddActor(pet);context.AddActor(enemy);
        CombatEvent Died(string source)=>new() {
            Type=CombatEventType.ActorDied,SourceActorKey=source,
            TargetActorKey="target"
        };
        Assert.True(MarksmanshipHunterSimRunner.QualifiesRapidKillingDeath(
            context,Died("hunter"),"hunter"));
        Assert.False(MarksmanshipHunterSimRunner.QualifiesRapidKillingDeath(
            context,Died("hunter-pet"),"hunter"));
        Assert.False(MarksmanshipHunterSimRunner.QualifiesRapidKillingDeath(
            context,Died("other-hunter"),"hunter"));
        new AuraManager().ApplyAura(context,enemy,new AuraDefinition {
            Key="serpent-sting",Name="Serpent Sting",DurationSeconds=15m
        },"hunter","serpent-sting","serpent-sting-tick");
        Assert.True(MarksmanshipHunterSimRunner.QualifiesRapidKillingDeath(
            context,Died("hunter-pet"),"hunter"));
        Assert.True(MarksmanshipHunterSimRunner.QualifiesRapidKillingDeath(
            context,Died("other-hunter"),"hunter"));
    }

    [Fact]
    public void TrueshotAuraAddsAndReversesOneHundredLiveRangedPowerOnlyOnce()
    {
        var ctx=new SimulationContext(new SimulationRunOptions {
            Seed=175512,DurationSeconds=20m
        });
        var owner=new SimulationActorState{Key="hunter",TeamKey="raid",Level=60};
        owner.Stats.Set("attack-power",1000m);
        owner.Stats.Set("companion-attack-power",1000m);
        owner.Stats.Set("hunter-melee-attack-power",950m);
        ctx.AddActor(owner);
        var petTuning=new ForeverBeastMasteryHunterTuning();
        var pet=HunterCompanionComposition.Create(ctx,owner,ForeverHunterPetFamilies.Get("wolf"),petTuning);
        var processor=new HunterTrueshotAuraProcessor(owner,pet,petTuning,100m);
        var apply=new CombatEvent {
            Type=CombatEventType.AuraApplied,AbilityKey="trueshot-aura",
            SourceActorKey="hunter",TargetActorKey="hunter"
        };
        processor.Process(ctx,apply);
        processor.Process(ctx,apply);
        Assert.Equal(1100m,owner.Stats.Get("attack-power"));
        Assert.Equal(1100m,owner.Stats.Get("companion-attack-power"));
        Assert.Equal(420m,pet.Stats.Get("attack-power"));
        owner.Stats.Add("attack-power",150m);
        var remove=new CombatEvent{
            Type=CombatEventType.AuraRemoved,AbilityKey="trueshot-aura",
            SourceActorKey="hunter",TargetActorKey="hunter"
        };
        processor.Process(ctx,remove);
        processor.Process(ctx,remove);
        Assert.Equal(1150m,owner.Stats.Get("attack-power"));
        // Removing only the aura preserves independent owner AP and updates
        // the pet from the actual maximum owner AP, not an obsolete snapshot.
        Assert.Equal(1150m,owner.Stats.Get("companion-attack-power"));
        Assert.Equal(425m,pet.Stats.Get("attack-power"));
        Assert.Equal(1,processor.Applications);
        Assert.Equal(1,processor.Removals);
    }

    [Fact]
    public void RangedShotCooldownsAndRapidFireStayOnTheExistingEngine()
    {
        var talents=ScatterTalents();
        talents.Ranks["sniper-shot"]=1;
        talents.Ranks["trueshot-aura"]=1;
        talents.MarksmanshipPointsSpent=31;
        var t=new ForeverMarksmanshipHunterTuning();
        var book=ForeverMarksmanshipHunterSpellbook.Create(talents,t,1200m);
        var aimed=Assert.Single(book,x=>x.Key=="aimed-shot");
        var multi=Assert.Single(book,x=>x.Key=="multi-shot");
        var arcane=Assert.Single(book,x=>x.Key=="arcane-shot");
        var sniper=Assert.Single(book,x=>x.Key=="sniper-shot");
        var scatter=Assert.Single(book,x=>x.Key=="scatter-shot");
        Assert.Equal(2m,aimed.CastTimeSeconds);
        Assert.Equal(6m,aimed.SharedCooldownSeconds);
        Assert.Equal(aimed.SharedCooldownGroupKey,multi.SharedCooldownGroupKey);
        Assert.Null(arcane.SharedCooldownGroupKey);
        Assert.Null(scatter.SharedCooldownGroupKey);
        Assert.Equal(4m,sniper.CastTimeSeconds);
        Assert.Equal(15m,sniper.CooldownSeconds);
        Assert.Equal(30m,scatter.CooldownSeconds);
        Assert.Equal(15m,Assert.Single(book,x=>x.Key=="rapid-fire")
            .Effects.Single().DurationSeconds);
    }
}
