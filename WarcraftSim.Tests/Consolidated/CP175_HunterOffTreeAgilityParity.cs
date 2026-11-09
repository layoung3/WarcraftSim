using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Hunter;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175HunterOffTreeAgilityParity
{
    private static Dictionary<string,int> Ranks(int lightning) => new(StringComparer.OrdinalIgnoreCase) {
        ["improved-tracking"]=5,["deflection"]=5,["entrapment"]=5,
        ["survivalist"]=5,["improved-wing-clip"]=3,["clever-traps"]=2,
        ["lightning-reflexes"]=lightning
    };

    internal static MarksmanshipHunterSimRequest Marksmanship(int rank,bool melee=false,
        decimal agility=220m,decimal crit=100m)
    {
        var request=new MarksmanshipHunterSimRequest {
            DurationSeconds=12m,Seed=175901,TargetMaximumHealth=10000000m,
            HasPet=true,PetFamily="wolf",UseMeleeAuto=melee,
            UseSerpentSting=false,UseAimedShot=false,UseMultiShot=false,
            UseArcaneShot=false,UseSniperShot=false,UseRapidFire=false,
            UsePetSpecial=false,UsePetBite=false,UsePetClaw=false,
            TargetIsTracked=false,
            Talents=new() { Ranks=Ranks(rank),MarksmanshipPointsSpent=0,
                BeastMasteryPointsSpent=0,SurvivalPointsSpent=25+rank }
        };
        request.Tuning.Agility=agility;
        request.Tuning.CriticalChancePercent=crit;
        request.Tuning.HitChancePercent=100m;
        request.Tuning.TargetArmor=0m;
        request.Tuning.WeaponMinDamage=request.Tuning.WeaponMaxDamage=100m;
        request.PetTuning.HunterWeaponMin=request.PetTuning.HunterWeaponMax=100m;
        request.PetTuning.HunterMeleeCriticalPercent=crit;
        request.PetTuning.PetHitPercent=100m;
        request.PetTuning.PetAvoidancePercent=0m;
        request.PetTuning.PetDamageMin=request.PetTuning.PetDamageMax=72m;
        return request;
    }

    internal static BeastMasteryHunterSimRequest BeastMastery(int rank,bool melee=false,
        decimal agility=220m,decimal crit=100m)
    {
        var request=new BeastMasteryHunterSimRequest {
            DurationSeconds=12m,Seed=175901,TargetMaximumHealth=10000000m,
            PetEnabled=true,PetFamily="wolf",UseMeleeAuto=melee,
            UseSerpentSting=false,UseAimedShot=false,UseMultiShot=false,
            UseArcaneShot=false,UseRapidFire=false,UseSummonHawk=false,
            UseBestialWrath=false,UseIntimidation=false,UseMendPet=false,
            UsePetSpecial=false,UsePetBite=false,UsePetClaw=false,
            TargetIsTracked=false,Talents=new() { Ranks=Ranks(rank) }
        };
        request.Tuning.Agility=agility;
        request.Tuning.HunterCriticalPercent=request.Tuning.HunterMeleeCriticalPercent=crit;
        request.Tuning.HunterHitPercent=100m;
        request.Tuning.TargetArmor=0m;
        request.Tuning.HunterWeaponMin=request.Tuning.HunterWeaponMax=100m;
        request.Tuning.PetHitPercent=100m;
        request.Tuning.PetAvoidancePercent=0m;
        request.Tuning.PetDamageMin=request.Tuning.PetDamageMax=72m;
        return request;
    }

    private static CombatEvent FirstDamage(SimulationRunResult run,string actor,string ability) =>
        run.Timeline.First(x=>x.Type==CombatEventType.Damage &&
            x.SourceActorKey==actor && x.AbilityKey==ability && x.Amount>0m);

    [Theory]
    [InlineData("marksmanship",false)]
    [InlineData("marksmanship",true)]
    [InlineData("beast-mastery",false)]
    [InlineData("beast-mastery",true)]
    public void LegalOffTreeLightningReflexesChangesOwnerAndPetDamageExactlyOnceAndReverses(
        string preset,bool melee)
    {
        SimulationRunResult ranked,removed,replay;
        if(preset=="marksmanship")
        {
            var input=Marksmanship(5,melee);
            var result=new MarksmanshipHunterSimRunner().Run(input);
            Assert.Equal(1066m,result.RangedAttackPower);
            ranked=result.Run;
            replay=new MarksmanshipHunterSimRunner().Run(input).Run;
            Assert.Equal(5,input.Talents.Ranks["lightning-reflexes"]);
            input.Talents.Ranks["lightning-reflexes"]=0;
            removed=new MarksmanshipHunterSimRunner().Run(input).Run;
        }
        else
        {
            var input=BeastMastery(5,melee);
            ranked=new BeastMasteryHunterSimRunner().Run(input).Run;
            replay=new BeastMasteryHunterSimRunner().Run(input).Run;
            Assert.Equal(5,input.Talents.Ranks["lightning-reflexes"]);
            input.Talents.Ranks["lightning-reflexes"]=0;
            removed=new BeastMasteryHunterSimRunner().Run(input).Run;
        }
        var ability=melee?"melee-auto":"auto-shot";
        var baseOwner=FirstDamage(removed,"hunter",ability);
        var talentedOwner=FirstDamage(ranked,"hunter",ability);
        Assert.True(baseOwner.IsCritical && talentedOwner.IsCritical);
        Assert.Equal((melee?33m:66m)*2.8m/14m*2m,
            talentedOwner.Amount!.Value-baseOwner.Amount!.Value);
        var basePet=FirstDamage(removed,"hunter-pet","pet-melee");
        var talentedPet=FirstDamage(ranked,"hunter-pet","pet-melee");
        Assert.Equal(66m*.10m*2m/14m*2m,
            talentedPet.Amount!.Value-basePet.Amount!.Value,24);
        Assert.Equal(ranked.Timeline.Select(x=>(x.TimeSeconds,x.Type,x.AbilityKey,x.Amount,x.IsCritical)),
            replay.Timeline.Select(x=>(x.TimeSeconds,x.Type,x.AbilityKey,x.Amount,x.IsCritical)));
    }

    [Theory]
    [InlineData("marksmanship",false)]
    [InlineData("marksmanship",true)]
    [InlineData("beast-mastery",false)]
    [InlineData("beast-mastery",true)]
    public void OwnerAndPetCritRollsConsumeLightningReflexesLiveStats(string preset,bool melee)
    {
        // Explicit synthetic boundary fixture: the talent alone crosses 100%
        // crit. This is not a claim about attainable equipment or live stats.
        SimulationRunResult Run(int rank)=>preset=="marksmanship"
            ?new MarksmanshipHunterSimRunner().Run(Marksmanship(rank,melee,40000m,0m)).Run
            :new BeastMasteryHunterSimRunner().Run(BeastMastery(rank,melee,40000m,0m)).Run;
        var ranked=Run(5);var removed=Run(0);
        var attacks=ranked.Timeline.Where(x=>x.Type==CombatEventType.Damage && x.Amount>0m).ToArray();
        Assert.NotEmpty(attacks);
        Assert.Contains(attacks,x=>x.SourceActorKey=="hunter");
        Assert.Contains(attacks,x=>x.SourceActorKey=="hunter-pet");
        Assert.All(attacks,x=>Assert.True(x.IsCritical));
        Assert.All(removed.Timeline.Where(x=>x.Type==CombatEventType.Damage && x.Amount>0m),
            x=>Assert.False(x.IsCritical));
    }

    [Fact]
    public void LegacyNumericTuningDoesNotInventRawAgility()
    {
        Assert.Equal(0m,new ForeverMarksmanshipHunterTuning().Agility);
        Assert.Equal(0m,new ForeverBeastMasteryHunterTuning().Agility);
        var with=new MarksmanshipHunterSimRunner().Run(Marksmanship(5,agility:0m));
        var without=new MarksmanshipHunterSimRunner().Run(Marksmanship(0,agility:0m));
        Assert.Equal(without.DamageDone,with.DamageDone);
        Assert.Throws<ArgumentException>(()=>new ForeverMarksmanshipHunterTuning{Agility=-1m}.Validate());
        Assert.Throws<ArgumentException>(()=>new ForeverBeastMasteryHunterTuning{Agility=-1m}.Validate());
    }

    private static ForeverLevel60BaselineObservation Baseline()=>new() {
        Race="orc",ClassKey="hunter",Level=60,
        Strength=50m,Agility=75m,Stamina=110m,Intellect=165m,Spirit=145m,
        ObservedUnbuffedMaximumHealth=3300m,ObservedUnbuffedMaximumMana=3500m,
        ObservedUnbuffedMeleeAttackPower=450m,ObservedUnbuffedRangedAttackPower=760m,
        ObservedUnbuffedArmor=1020m,ClientBuild="1.60.1.70707",
        SourceKind="user-unverified-observation",VerificationStatus="unverified-user-supplied",
        SourceReference="synthetic-ci-fixture-not-a-real-player",RecordedAtUtc="2026-11-09T00:00:00Z"
    };
    private static CharacterEquipmentLoadout Gear()=>new() { Items=[new() {
        SlotKey="head",ItemKey="synthetic-agility-fixture",Name="Synthetic test only",
        Stats=new() {Values=new(StringComparer.OrdinalIgnoreCase) {
            ["agility"]=40m,["strength"]=10m,["attack-power"]=20m,["armor"]=11m
        }}
    }]};

    [Fact]
    public void BaselineEquipmentAndMongooseAgilityReachEveryHunterWithoutPreviewDoubleCounting()
    {
        var modifiers=new ForeverExternalModifierSelection {Consumables=["elixir-of-the-mongoose"]};
        var profile=new CharacterProfile {ClassKey="hunter",Race="orc",Level=60,Equipment=Gear()};
        var preview=Assert.IsType<OkObjectResult>(new ForeverCharacterController().GenerateLevel60(new() {
            Character=profile,Baseline=Baseline(),Modifiers=modifiers
        }));
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(preview.Value));
        var stats=json.RootElement.GetProperty("effectiveStats").GetProperty("Values");
        var mm=Marksmanship(5);
        mm.Race="orc";mm.Level60Baseline=Baseline();mm.Equipment=Gear();mm.ExternalModifiers=modifiers;
        var mmResolved=ForeverExternalHunterStatAdapter.Apply(ForeverEquipmentStatBridge.Resolve(
            ForeverLevel60HunterObservationAdapter.Apply(mm)));
        var bm=BeastMastery(5);
        bm.Race="orc";bm.Level60Baseline=Baseline();bm.Equipment=Gear();bm.ExternalModifiers=modifiers;
        var bmResolved=ForeverExternalHunterStatAdapter.Apply(ForeverEquipmentStatBridge.Resolve(
            ForeverLevel60HunterObservationAdapter.Apply(bm)));
        var sv=ForeverExternalHunterStatAdapter.Apply(ForeverEquipmentStatBridge.Resolve(
            ForeverLevel60HunterObservationAdapter.Apply(new SurvivalHunterSimRequest {
                Race="orc",Level60Baseline=Baseline(),Equipment=Gear(),ExternalModifiers=modifiers
            })));
        Assert.Equal(140m,mmResolved.Tuning.Agility);
        Assert.Equal(mmResolved.Tuning.Agility,bmResolved.Tuning.Agility);
        Assert.Equal(mmResolved.Tuning.Agility,sv.Tuning.Agility);
        Assert.Equal(stats.GetProperty("agility").GetDecimal(),mmResolved.Tuning.Agility);
        // Naked RAP 760 + gear (80 Agility RAP + 20 generic AP) + Mongoose 50.
        Assert.Equal(910m,mmResolved.Tuning.RangedAttackPower);
        Assert.Equal(910m,bmResolved.Tuning.RangedAttackPower);
        Assert.Equal(910m,sv.Tuning.RangedAttackPower);
        Assert.Equal(545m,mmResolved.PetTuning.MeleeAttackPower);
        Assert.Equal(545m,bmResolved.Tuning.MeleeAttackPower);
        Assert.Equal(545m,sv.Tuning.MeleeAttackPower);
        Assert.Equal(545m,stats.GetProperty("attack-power").GetDecimal());
        // Display RAP now includes source-attributed generic equipment AP.
        Assert.Equal(910m,stats.GetProperty("ranged-attack-power").GetDecimal());
        var run=new MarksmanshipHunterSimRunner().Run(mm);
        Assert.Equal(910m+140m*.15m*2m,run.RangedAttackPower);
        Assert.Equal(220m,mm.Tuning.Agility); // original request was not mutated
    }

    private sealed class NoMitigation : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext context,SimulationActorState source,
            SimulationActorState target,AbilityDefinition ability,AbilityEffectDefinition effect,
            decimal amount)=>DamageMitigationResult.Unmitigated(amount);
    }
    private sealed class Observer(SimulationActorState owner,SimulationActorState pet) : ICombatEventProcessor
    {
        public List<Dictionary<string,decimal>> Applied {get;}=[];
        public List<Dictionary<string,decimal>> Removed {get;}=[];
        public Dictionary<string,decimal> Snapshot()
        {
            var stats=new Dictionary<string,decimal>(owner.Stats.Values,StringComparer.OrdinalIgnoreCase);
            stats["pet-ap"]=pet.Stats.Get("attack-power");
            stats["pet-armor"]=pet.Stats.Get("armor");
            return stats;
        }
        public void Process(SimulationContext context,CombatEvent ev)
        {
            if(ev.EffectKey!="synthetic-hunter-agility@trinket1")return;
            if(ev.Type==CombatEventType.AuraApplied)Applied.Add(Snapshot());
            if(ev.Type==CombatEventType.AuraRemoved)Removed.Add(Snapshot());
        }
    }

    [Theory]
    [InlineData(false,0)]
    [InlineData(false,5)]
    [InlineData(true,0)]
    [InlineData(true,5)]
    public void ActualEquipmentProcessorAppliesTemporaryAgilityOnceAndRestoresOwnerPetAndDamage(
        bool rangedPrimary,int ranks)
    {
        var ctx=new SimulationContext(new SimulationRunOptions {
            DurationSeconds=7m,Seed=175903,PrimaryActorKey="hunter",CaptureTimeline=true
        });
        var hunter=new SimulationActorState {Key="hunter",TeamKey="raid",Level=60,MaximumHealth=3900m};
        hunter.InitializeHealth(3900m);
        hunter.AddResource(new ResourceState("mana",4200m,4200m));
        var tune=new ForeverSurvivalHunterTuning();
        if(rangedPrimary)
        {
            hunter.Stats.Set("attack-power",1000m);
            hunter.Stats.Set("hunter-melee-attack-power",950m);
            hunter.Stats.Set("ranged-critical-chance-percent",18m);
            hunter.Stats.Set("melee-critical-chance-percent",18m);
            hunter.Stats.Set("armor",2400m);
            SurvivalHunterAgilityStats.InitializeRanged(hunter.Stats,220m,ranks);
        }
        else
        {
            SurvivalHunterAgilityStats.Initialize(hunter.Stats,tune,ranks);
            hunter.Stats.Set("armor",2400m+220m*.03m*ranks*2m);
        }
        ctx.AddActor(hunter);
        var target=new SimulationActorState {Key="target",TeamKey="enemy",Level=63,MaximumHealth=1000000m};
        target.InitializeHealth(target.MaximumHealth);ctx.AddActor(target);
        var petTuning=new ForeverBeastMasteryHunterTuning();
        var pet=HunterCompanionComposition.Create(ctx,hunter,ForeverHunterPetFamilies.Get("cat"),petTuning);
        var observer=new Observer(hunter,pet);
        var baseline=observer.Snapshot();
        var effect=new ForeverExecutableEquipmentEffect {
            Key="synthetic-hunter-agility",Kind="on-use",Trigger="manual",Modeled=true,
            SourceBuild="synthetic",SourceUrl="https://example.test/synthetic",
            AutoUse=true,DurationSeconds=2m,CooldownSeconds=4m,
            StatBonuses=new(StringComparer.OrdinalIgnoreCase) {
                ["agility"]=40m,["strength"]=10m,["attack-power"]=20m,
                ["melee-attack-power"]=7m,["ranged-attack-power"]=9m,["armor"]=11m
            }
        };
        var aura=new AuraManager();
        var executor=new AbilityExecutor(aura,new SimpleCombatRollResolver(),new NoMitigation());
        var auto=new AutoAttackProcessor(executor,new ForeverAutoAttackTimingProvider());
        var effects=new ForeverEquipmentEffectProcessor(new(),"hunter","target",aura,executor,175903,
            explicitBindings:[new("trinket1",effect)],classKey:"hunter",
            onStatDelta:(context,actor,delta)=>SurvivalHunterAgilityStats.ApplyRuntimeDelta(
                context,actor,pet,petTuning,ranks,delta,rangedPrimary));
        AutoAttackDefinition Attack(string key,string stat)=>new() {
            Key=key,Name=key,SwingIntervalSeconds=1m,
            DamageEffect=new() {Key=key,EffectType=AbilityEffectTypes.DirectDamage,
                TargetType=AbilityTargetTypes.Enemy,SchoolKey="physical",
                MinimumValue=100m,MaximumValue=100m,ScalingStatKey=stat,ScalingCoefficient=.2m}
        };
        var rangedKey=rangedPrimary?"attack-power":"hunter-ranged-attack-power";
        var meleeKey=rangedPrimary?"hunter-melee-attack-power":"attack-power";
        var run=new SimulationEngine([executor,aura,effects,auto,observer]).Run(ctx,started=> {
            auto.Start(started,"hunter","target",Attack("test-owner",rangedKey),.5m);
            auto.Start(started,"hunter-pet","target",Attack("test-pet","attack-power"),.5m);
        });
        effects.WriteMetrics(run);
        Assert.Equal(2,observer.Applied.Count);
        Assert.Equal(2,observer.Removed.Count);
        var extra=40m*.03m*ranks;
        var rangedDelta=109m+extra*2m;
        foreach(var snapshot in observer.Applied)
        {
            Assert.Equal(baseline["agility"]+40m+extra,snapshot["agility"]);
            Assert.Equal(snapshot["agility"],snapshot["hunter-effective-agility"]);
            Assert.Equal(baseline[meleeKey]+77m+extra,snapshot[meleeKey]);
            Assert.Equal(baseline[rangedKey]+rangedDelta,snapshot[rangedKey]);
            Assert.Equal(baseline["armor"]+91m+extra*2m,snapshot["armor"]);
            Assert.Equal(baseline["ranged-critical-chance-percent"]+(40m+extra)/52.9m,
                snapshot["ranged-critical-chance-percent"],25);
            Assert.Equal(snapshot["ranged-critical-chance-percent"],snapshot["melee-critical-chance-percent"]);
            Assert.Equal(baseline["pet-ap"]+rangedDelta*.10m,snapshot["pet-ap"]);
            Assert.Equal(baseline["pet-armor"]+(91m+extra*2m)*.30m,snapshot["pet-armor"]);
        }
        foreach(var snapshot in observer.Removed)
            foreach(var (key,value) in baseline)Assert.Equal(value,snapshot[key]);
        foreach(var damage in run.Timeline.Where(x=>x.Type==CombatEventType.Damage))
        {
            var active=damage.TimeSeconds<2m || damage.TimeSeconds is >=4m and <6m;
            var petAttack=damage.SourceActorKey=="hunter-pet";
            var ap=baseline[petAttack?"pet-ap":rangedKey]+(active?rangedDelta*(petAttack?.10m:1m):0m);
            Assert.Equal(100m+ap*.2m,damage.Amount);
        }
        Assert.Equal(4m,run.CustomMetrics["equipment-synthetic-hunter-agility@trinket1-uptime-seconds"]);
    }
}
