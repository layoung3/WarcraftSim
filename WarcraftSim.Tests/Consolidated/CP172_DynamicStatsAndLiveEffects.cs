using System;
using System.Collections.Generic;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp172DynamicStatsAndLiveEffects
{
    private static CharacterEquipmentLoadout Gear(params (string Slot,int Id)[] entries) =>
        new(){Items=entries.Select(x=>new CharacterEquipmentItem{
            SlotKey=x.Slot,ItemId=x.Id
        }).ToList()};

    [Fact]
    public void TemporaryRawPrimaryRatingsAndAttackPowerUseRealDerivedContributorDeltas()
    {
        var raw=new Dictionary<string,decimal>{
            ["intellect"]=120m,["agility"]=40m,["attack-power"]=100m,
            ["critical-strike-rating"]=28m,["hit-rating"]=12m,
            ["haste-rating"]=20m,["armor"]=150m
        };
        var delta=ForeverEquipmentDynamicStatResolver.ResolveDelta("mage",60,raw);
        Assert.Equal(120m,delta["intellect"]);
        Assert.Equal(100m,delta["attack-power"]);
        Assert.Equal(150m+80m,delta["armor"]); // Existing 2 armor per Agility.
        Assert.True(delta["spell-critical-chance-percent"]>0m);
        Assert.True(delta["attack-critical-chance-percent"]>0m);
        Assert.True(delta["spell-hit-chance-percent"]>0m);
        Assert.True(delta["haste-percent"]>0m);
        Assert.Equal(delta["haste-percent"],
            ForeverEquipmentDynamicStatResolver.ResolveDelta("mage",60,raw)["haste-percent"]);
    }

    [Fact]
    public void TemporaryIntellectAndStaminaChangeResourceCapsAndRestoreWithoutFreeMana()
    {
        var context=new SimulationContext(new SimulationRunOptions{
            Seed=172,DurationSeconds=20m,PrimaryActorKey="owner"
        });
        var actor=new SimulationActorState{
            Key="owner",Name="Mage",TeamKey="raid",Level=60,
            MaximumHealth=3000m
        };
        actor.InitializeHealth(3000m,2500m);
        actor.AddResource(new ResourceState("mana",2000m,1800m));
        context.AddActor(actor);
        var gains=new Dictionary<string,decimal>{
            ["intellect"]=20m,["stamina"]=10m
        };
        ForeverEquipmentDynamicResourceCaps.Apply(context,actor,gains);
        Assert.Equal(2300m,actor.Resources["mana"].Maximum);
        Assert.Equal(1800m,actor.Resources["mana"].Current);
        Assert.Equal(3100m,actor.MaximumHealth);
        Assert.Equal(2600m,actor.CurrentHealth);
        actor.Resources["mana"].Gain(500m);
        var losses=gains.ToDictionary(x=>x.Key,x=>-x.Value,
            StringComparer.OrdinalIgnoreCase);
        ForeverEquipmentDynamicResourceCaps.Apply(context,actor,losses);
        Assert.Equal(2000m,actor.Resources["mana"].Maximum);
        Assert.Equal(2000m,actor.Resources["mana"].Current);
        Assert.Equal(3000m,actor.MaximumHealth);
        Assert.Equal(2500m,actor.CurrentHealth);
        // A second identical temporary application/removal must be reversible
        // and cannot duplicate permanent source contributions.
        ForeverEquipmentDynamicResourceCaps.Apply(context,actor,gains);
        ForeverEquipmentDynamicResourceCaps.Apply(context,actor,losses);
        Assert.Equal(2000m,actor.Resources["mana"].Maximum);
        Assert.Equal(3000m,actor.MaximumHealth);
    }

    [Fact]
    public void DynamicHasteAdjustsFutureHardcastsButDoesNotAlterExistingGcdOrChannels()
    {
        var context=new SimulationContext(new SimulationRunOptions{
            Seed=172,DurationSeconds=40m,PrimaryActorKey="mage"
        });
        var actor=new SimulationActorState{
            Key="mage",Name="Mage",TeamKey="raid",Level=60,MaximumHealth=5000m
        };
        actor.InitializeHealth(5000m);
        context.AddActor(actor);
        var cast=new AbilityDefinition {
            Key="synthetic-hardcast",CastTimeSeconds=3m,
            GlobalCooldownSeconds=1.5m,
            ChannelDurationSeconds=0m
        };
        var timing=new ForeverEquipmentCastHasteTimingProvider(BaseAbilityTimingProvider.Instance);
        var plain=timing.Resolve(context,actor,cast);
        actor.Stats.Set("cast-haste-percent",33m);
        var fast=timing.Resolve(context,actor,cast);
        Assert.Equal(3m,plain.CastTimeSeconds);
        Assert.Equal(3m/1.33m,fast.CastTimeSeconds);
        Assert.Equal(1.5m,fast.GlobalCooldownSeconds);
        Assert.Equal(plain.ChannelTickIntervalSeconds,fast.ChannelTickIntervalSeconds);
        // The already-returned timing snapshot remains fixed for an in-progress cast.
        Assert.Equal(3m,plain.CastTimeSeconds);
        actor.Stats.Set("cast-haste-percent",0m);
        Assert.Equal(3m,timing.Resolve(context,actor,cast).CastTimeSeconds);
    }

    private sealed class NoMitigation : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext context,
            SimulationActorState source,SimulationActorState target,
            AbilityDefinition ability,AbilityEffectDefinition effect,decimal amount) =>
            DamageMitigationResult.Unmitigated(amount);
    }
    private sealed class StatObserver(SimulationActorState owner) : ICombatEventProcessor
    {
        public Dictionary<string,decimal> AtActivation { get; }=new();
        public Dictionary<string,decimal> AtExpiration { get; }=new();
        public void Process(SimulationContext context,CombatEvent ev)
        {
            if(ev.EffectKey!="synthetic-derived@trinket1")return;
            var dest=ev.Type switch {
                CombatEventType.AuraApplied=>AtActivation,
                CombatEventType.AuraRemoved=>AtExpiration,
                _=>null
            };
            if(dest is null)return;
            foreach(var key in new[]{"intellect","spell-critical-chance-percent",
                "attack-critical-chance-percent","haste-percent","attack-power"})
                dest[key]=owner.Stats.Get(key);
        }
    }

    [Fact]
    public void TimedIntellectRatingAndHasteRecalculateAndRestoreBaselineAtExpiration()
    {
        var context=new SimulationContext(new SimulationRunOptions{
            Seed=172,DurationSeconds=8m,PrimaryActorKey="owner",
            CaptureTimeline=true,MaximumProcessedEvents=3000
        });
        var owner=new SimulationActorState{
            Key="owner",Name="Mage",TeamKey="raid",Level=60,MaximumHealth=5000m
        };
        owner.InitializeHealth(5000m);
        context.AddActor(owner);
        var target=new SimulationActorState{
            Key="target",Name="Target",TeamKey="enemy",Level=63,MaximumHealth=100000m
        };
        target.InitializeHealth(target.MaximumHealth);
        context.AddActor(target);
        var effect=new ForeverExecutableEquipmentEffect{
            Key="synthetic-derived",Kind="on-use",Trigger="manual",Modeled=true,
            SourceBuild="synthetic",SourceUrl="https://example.test/synthetic",
            AutoUse=true,DurationSeconds=2m,CooldownSeconds=30m,
            StatBonuses=new Dictionary<string,decimal>{
                ["intellect"]=100m,["agility"]=10m,
                ["critical-strike-rating"]=30m,["haste-rating"]=20m,
                ["attack-power"]=45m
            }
        };
        var aura=new AuraManager();
        var executor=new AbilityExecutor(aura,new SimpleCombatRollResolver(),new NoMitigation());
        var effects=new ForeverEquipmentEffectProcessor(
            new CharacterEquipmentLoadout(),"owner","target",aura,executor,172,
            explicitBindings:[new ForeverEquipmentEffectBinding("trinket1",effect)],
            classKey:"mage");
        var observer=new StatObserver(owner);
        var engine=new SimulationEngine([executor,aura,effects,observer]);
        var run=engine.Run(context);
        effects.WriteMetrics(run);
        Assert.Equal(100m,observer.AtActivation["intellect"]);
        Assert.True(observer.AtActivation["spell-critical-chance-percent"]>0m);
        Assert.True(observer.AtActivation["attack-critical-chance-percent"]>0m);
        Assert.True(observer.AtActivation["haste-percent"]>0m);
        Assert.Equal(45m,observer.AtActivation["attack-power"]);
        Assert.All(observer.AtExpiration.Values,x=>Assert.Equal(0m,x));
        Assert.Equal(2m,run.CustomMetrics["equipment-synthetic-derived@trinket1-uptime-seconds"]);
    }

    [Fact]
    public void ForeverMindQuickeningGemSpeedsMageHardcastsAndExpiresInRealTimeline()
    {
        MageSimRequest Configure(bool equipped)
        {
            var request=MageSimRequest.CreatePreset("fire");
            request.DurationSeconds=48m;
            request.TargetMaximumHealth=5000000m;
            request.BaseSpellHitPercent=100m;
            request.SpellCritPercent=0m;
            request.CaptureTimeline=true;
            if(equipped)request.Character.Equipment=Gear(("trinket1",19339));
            return request;
        }
        var original=new MageSimRunner().Run(Configure(false));
        var haste=new MageSimRunner().Run(Configure(true));
        static decimal FirstHardcastSeconds(SimulationRunResult run)
        {
            var complete=run.Timeline.First(x=>x.Type==CombatEventType.AbilityCastCompleted &&
                x.AbilityKey=="fireball" && x.AbilityExecutionId.HasValue);
            var start=run.Timeline.First(x=>x.Type==CombatEventType.AbilityCastStarted &&
                x.AbilityExecutionId==complete.AbilityExecutionId);
            return complete.TimeSeconds-start.TimeSeconds;
        }
        Assert.True(FirstHardcastSeconds(haste.Run)<FirstHardcastSeconds(original.Run));
        Assert.Equal(1m,haste.Run.CustomMetrics[
            "equipment-mind-quickening-gem@trinket1-activations"]);
        Assert.Equal(20m,haste.Run.CustomMetrics[
            "equipment-mind-quickening-gem@trinket1-uptime-seconds"]);
        Assert.Contains(haste.Run.Timeline,x=>x.Type==CombatEventType.AuraRemoved &&
            x.EffectKey=="mind-quickening-gem@trinket1" && x.TimeSeconds==20m);
    }

    [Fact]
    public void ForeverSlayersCrestIncreasesHunterAndPetDamageWithoutPermanentStatLeak()
    {
        MarksmanshipHunterSimRequest Configure(bool equipped)
        {
            var request=new MarksmanshipHunterSimRequest{
                DurationSeconds=50m,TargetMaximumHealth=10000000m,
                HasPet=true,PetFamily="cat",UseRapidFire=false,
                UseAimedShot=false,UseSniperShot=false,UseMultiShot=false,
                UseArcaneShot=false,UseSerpentSting=false,CaptureTimeline=true
            };
            request.Tuning.HitChancePercent=100m;
            request.Tuning.CriticalChancePercent=0m;
            if(equipped)request.Equipment=Gear(("trinket1",23041));
            return request;
        }
        var without=new MarksmanshipHunterSimRunner().Run(Configure(false));
        var with=new MarksmanshipHunterSimRunner().Run(Configure(true));
        Assert.True(with.HunterDamage>without.HunterDamage);
        Assert.True(with.PetDamage>without.PetDamage);
        Assert.Equal(1m,with.Run.CustomMetrics[
            "equipment-slayers-crest@trinket1-activations"]);
        Assert.Equal(20m,with.Run.CustomMetrics[
            "equipment-slayers-crest@trinket1-uptime-seconds"]);
        Assert.Contains(with.Run.Timeline,x=>x.Type==CombatEventType.AuraRemoved &&
            x.EffectKey=="slayers-crest@trinket1" && x.TimeSeconds==20m);
    }

    [Fact]
    public void SourceBackedTalismanTemporarilyReachesWarlockImpWithoutPetGeneratedProcs()
    {
        WarlockSimRequest Configure(bool equipped)
        {
            var request=WarlockSimRequest.CreatePreset("demonology");
            request.DurationSeconds=42m;
            request.TargetMaximumHealth=5000000m;
            request.BaseSpellHitPercent=100m;
            request.SpellCritPercent=0m;
            request.EnablePet=true;
            request.PetType="imp";
            request.PrePullSacrificedDemon=null;
            if(equipped)request.Character.Equipment=Gear(("trinket1",18820));
            return request;
        }
        var plain=new WarlockSimRunner().Run(Configure(false));
        var active=new WarlockSimRunner().Run(Configure(true));
        Assert.True(active.WarlockDamage>plain.WarlockDamage);
        Assert.True(active.PetDamage>plain.PetDamage);
        Assert.Equal(15m,active.Run.CustomMetrics[
            "equipment-ephemeral-power@trinket1-uptime-seconds"]);
        Assert.DoesNotContain(active.Run.Timeline,x=>x.AbilityKey is not null &&
            x.AbilityKey.StartsWith("equipment-",StringComparison.Ordinal) &&
            x.SourceActorKey=="warlock-pet");
    }
}
