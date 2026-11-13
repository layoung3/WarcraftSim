using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Warlock;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175F3C4WarlockIntegratedCombat
{
    private static ForeverWarlockTalentBuild Build(string kind)
    {
        var af31=new Dictionary<string,int> {
            ["improved-corruption"]=5,["suppression"]=5,["improved-drains"]=3,
            ["soul-harvest"]=2,["improved-bane-of-agony"]=2,["fel-concentration"]=3,
            ["pandemic"]=3,["malevolence"]=5,["nightfall"]=2,["siphon-life"]=1
        };
        var af20=new Dictionary<string,int> {
            ["improved-corruption"]=5,["suppression"]=5,["improved-drains"]=3,
            ["soul-harvest"]=2,["improved-bane-of-agony"]=2,["fel-concentration"]=3
        };
        var af17=new Dictionary<string,int> {
            ["improved-corruption"]=5,["suppression"]=5,["improved-drains"]=3,
            ["soul-harvest"]=2,["improved-bane-of-agony"]=2
        };
        var demo20=new Dictionary<string,int> {
            ["unholy-power"]=5,["improved-imp"]=3,["demonic-embrace"]=5,
            ["fel-vitality"]=3,["demonic-energies"]=2,["improved-voidwalker"]=2
        };
        var demo17=new Dictionary<string,int> {
            ["unholy-power"]=5,["improved-imp"]=3,["demonic-embrace"]=5,
            ["fel-vitality"]=3,["demonic-energies"]=1
        };
        var dest31=new Dictionary<string,int> {
            ["bane"]=5,["improved-shadow-bolt"]=5,["cataclysm"]=3,["aftermath"]=5,
            ["ruin"]=5,["shadowburn"]=1,["conflagrate"]=1,
            ["agonizing-flames"]=3,["shadow-and-flame"]=3
        };
        var dest17=new Dictionary<string,int> {
            ["bane"]=5,["improved-shadow-bolt"]=5,["cataclysm"]=3,
            ["aftermath"]=3,["conflagrate"]=1
        };
        var (aff,demo,dest)=kind switch {
            "affliction-demonology" => (af31,demo20,new Dictionary<string,int>()),
            "affliction-destruction" => (af20,new Dictionary<string,int>(),dest31),
            "demonology-destruction" => (new Dictionary<string,int>(),demo20,dest31),
            "three-tree" => (af17,demo17,dest17),
            _ => throw new ArgumentException("Unknown F3C4 fixture.",nameof(kind))
        };
        var build=new ForeverWarlockTalentBuild {
            AfflictionPointsSpent=aff.Values.Sum(),
            DemonologyPointsSpent=demo.Values.Sum(),
            DestructionPointsSpent=dest.Values.Sum()
        };
        foreach(var (key,rank) in aff.Concat(demo).Concat(dest))build.Ranks[key]=rank;
        Assert.Equal(51,build.Ranks.Values.Sum());
        Assert.Equal(51,build.AfflictionPointsSpent+
            build.DemonologyPointsSpent+build.DestructionPointsSpent);
        return build;
    }

    private static WarlockSimRequest Request(string kind)
    {
        var spec=kind switch {
            "affliction-demonology" or "affliction-destruction"=>"affliction",
            _=>"demonology"
        };
        var r=WarlockSimRequest.CreatePreset(spec);
        r.Talents=Build(kind);
        r.Seed=175400+kind.Length;
        r.DurationSeconds=45m;
        r.TargetMaximumHealth=10_000_000m;
        r.EnablePet=true;
        r.PetType="imp";
        r.PrePullSacrificedDemon=null;
        r.UseLifeTap=false;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.DemonTuning.DemonSpellHitPercent=100m;
        r.PetMeleeHitPercent=100m;
        r.UseImmolate=true;
        r.UseBaneOfAgony=true;
        r.UseConflagrate=true;
        r.UseCorruption=true;
        r.UseSiphonLife=true;
        r.Tuning.ManaRegenerationPerSecond=100m;
        r.CaptureTimeline=true;
        r.CustomPriority=kind switch {
            "affliction-demonology" => ["corruption","siphon-life","bane-of-agony","shadow-bolt"],
            "affliction-destruction" => ["corruption","bane-of-agony","immolate","conflagrate","shadow-bolt"],
            "demonology-destruction" => ["immolate","conflagrate","shadow-bolt"],
            _ => ["corruption","bane-of-agony","immolate","conflagrate","shadow-bolt"]
        };
        r.Talents.Validate(60,r.Specialization);
        return r;
    }

    [Theory]
    [InlineData("affliction-demonology","siphon-life")]
    [InlineData("affliction-destruction","conflagrate")]
    [InlineData("demonology-destruction","conflagrate")]
    [InlineData("three-tree","conflagrate")]
    public void FourLegalFiftyOnePointBuildsExecuteTheirLearnedDamageAndReplay(
        string kind,string learnedAction)
    {
        var request=Request(kind);
        var json=JsonSerializer.Serialize(request);
        var copy=JsonSerializer.Deserialize<WarlockSimRequest>(json)!;
        Assert.Equal(request.Talents.Ranks.OrderBy(x=>x.Key),copy.Talents.Ranks.OrderBy(x=>x.Key));
        Assert.Equal(request.Specialization,copy.Specialization);
        Assert.Equal("imp",copy.PetType);
        var run=new WarlockSimRunner().Run(copy);
        Assert.True(run.WarlockDamage>0m);
        Assert.True(run.PetDamage>0m);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="warlock" && e.AbilityKey==learnedAction &&
            e.Amount is >0m);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="warlock-pet" && e.AbilityKey=="demon-firebolt" &&
            e.Amount is >0m);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="corruption" && e.SourceActorKey=="warlock" ||
            kind=="demonology-destruction" && e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="immolate");
        Assert.InRange(Math.Abs(run.DamageDone-run.PetDamage-run.WarlockDamage),0m,.000001m);
        var replay=new WarlockSimRunner().Run(
            JsonSerializer.Deserialize<WarlockSimRequest>(json)!);
        Assert.Equal(run.DamageDone,replay.DamageDone);
        Assert.Equal(run.DamageByAbility.OrderBy(x=>x.Key),
            replay.DamageByAbility.OrderBy(x=>x.Key));
    }

    [Fact]
    public void ThreeTreeTalentAwareAutomaticPriorityIncludesLearnedConflagrate()
    {
        var req=Request("three-tree");
        req.TalentAwareAutoRotation=true;
        req.CustomPriority=[];
        req.DurationSeconds=30m;
        var profile=WarlockAdaptiveRotation.Build(req);
        Assert.Contains(profile.Entries,e=>e.AbilityKey=="conflagrate");
        Assert.Contains(profile.Entries,e=>e.AbilityKey=="corruption");
        var result=new WarlockSimRunner().Run(req);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="conflagrate" && e.Amount is >0m);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="corruption" && e.IsPeriodic && e.Amount is >0m);
    }

    [Fact]
    public void RemovingLearnedActionsPreventsStaleCrossTreePriorities()
    {
        var request=Request("three-tree");
        request.Talents.Ranks["conflagrate"]=0;
        Assert.Throws<ArgumentException>(()=>WarlockAdaptiveRotation.Build(request));
        request.CustomPriority=["corruption","shadow-bolt"];
        Assert.DoesNotContain("conflagrate",
            WarlockAdaptiveRotation.Build(request).Entries.Select(x=>x.AbilityKey));
        request.Talents.Ranks["improved-corruption"]=0;
        request.Talents.Ranks["unholy-power"]=0;
        request.Talents.Validate(60,request.Specialization);
        var sim=new WarlockSimRunner().Run(request);
        Assert.Equal(0m,sim.Run.CustomMetrics["warlock-shadow-and-flame-shadow-procs"]);
    }

    [Fact]
    public void RemovingPermanentPetTalentRevertsRealImpFireboltDamage()
    {
        WarlockSimResponse Run(int rank)
        {
            var r=Request("three-tree");
            r.Talents.Ranks["unholy-power"]=rank;
            return new WarlockSimRunner().Run(r);
        }
        var untalented=Run(0);
        var ranked=Run(5);
        static decimal First(WarlockSimResponse r) => r.Run.Timeline.First(e=>
            e.Type==CombatEventType.Damage && e.SourceActorKey=="warlock-pet" &&
            e.AbilityKey=="demon-firebolt" && e.Amount is >0m).Amount!.Value;
        Assert.InRange(Math.Abs(First(ranked)-First(untalented)*1.10m),0m,.000001m);
        Assert.Equal(untalented.WarlockDamage,ranked.WarlockDamage);
    }

    [Theory]
    [InlineData(true,"imp",true)]
    [InlineData(false,"imp",false)]
    [InlineData(true,"felhunter",false)]
    public void SourceBackedDemonicPactControlsFelhunterSacrificeWithActivePet(
        bool pact,string family,bool survives)
    {
        var r=WarlockSimRequest.CreatePreset("demonology");
        r.PrePullSacrificedDemon="felhunter";
        r.PetType=family;
        r.Talents.Ranks["demonic-pact"]=pact?1:0;
        r.UseLifeTap=false;
        r.IncomingDamageAtSeconds=2m;
        r.IncomingDamageAmount=800m;
        r.DurationSeconds=12m;
        r.TargetMaximumHealth=1_000_000m;
        r.BaseSpellHitPercent=100m;
        r.CustomPriority=["shadow-bolt"];
        var sim=new WarlockSimRunner().Run(r);
        Assert.Equal(survives?"felhunter":null,sim.SacrificeBuffAtPull);
        Assert.Equal(!survives,sim.SacrificeCanceledBeforePull);
        Assert.Equal(survives,sim.Run.Timeline.Any(e=>
            e.Type==CombatEventType.Healing &&
            e.AbilityKey=="demonic-sacrifice" && e.Amount is >0m));
        Assert.True(sim.PetDamage>0m);
    }

    [Fact]
    public void DemonicKnowledgeEngageAndDeathDoNotDoubleCountLiveAgonySpellPower()
    {
        var r=WarlockSimRequest.CreatePreset("demonology");
        r.PrePullSacrificedDemon=null;
        r.PetType="imp";
        r.Talents.Ranks["demonic-knowledge"]=3;
        r.Talents.Ranks["soul-link"]=0;
        r.Talents.Ranks["demonic-pact"]=0;
        r.Talents.Ranks["master-demonologist"]=0;
        r.UseLifeTap=false;
        r.DurationSeconds=22m;
        r.TargetMaximumHealth=1_000_000m;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.DemonDeathAtSeconds=14m;
        r.Character.BaseStats.Values["spell-power"]=200m;
        r.CustomPriority=["bane-of-agony"];
        var run=new WarlockSimRunner().Run(r);
        decimal Tick(decimal at) => Assert.Single(run.Run.Timeline,e=>
            e.Type==CombatEventType.Damage && e.AbilityKey=="bane-of-agony" &&
            e.IsPeriodic && e.TimeSeconds==at).Amount!.Value;
        // Stage 2 is identical for ticks at 10s and 16s. A living Imp
        // grants precisely 60 SP from rank-3 Knowledge; death removes it.
        Assert.Equal(72m,Tick(10m));
        Assert.Equal(66m,Tick(16m));
        Assert.Equal(6m,Tick(10m)-Tick(16m));
        Assert.True(run.DemonDied);
    }

    [Fact]
    public void TemporaryBloodFuryChangesAlreadyActiveAgonyAndRestoresFollowingTicks()
    {
        WarlockSimRequest Make(bool active)
        {
            var r=WarlockSimRequest.CreatePreset("affliction");
            r.EnablePet=false;r.PrePullSacrificedDemon=null;
            r.Character.Race="orc";
            r.UseRacialActive=active;
            r.Talents.Ranks["shadow-mastery"]=0;
            r.Talents.Ranks["improved-bane-of-agony"]=0;
            r.Talents.Ranks["malevolence"]=0;
            r.UseLifeTap=false;r.SpellCritPercent=0m;r.BaseSpellHitPercent=100m;
            r.TargetMaximumHealth=1_000_000m;r.DurationSeconds=22m;
            r.Character.BaseStats.Values["spell-power"]=200m;
            r.CustomPriority=["bane-of-agony"];
            return r;
        }
        var no=new WarlockSimRunner().Run(Make(false));
        var active=new WarlockSimRunner().Run(Make(true));
        static decimal Tick(WarlockSimResponse r,decimal at) =>
            Assert.Single(r.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
                e.AbilityKey=="bane-of-agony" && e.IsPeriodic &&
                e.TimeSeconds==at).Amount!.Value;
        Assert.True(Tick(active,14m)>Tick(no,14m),
            "Blood Fury must modify a pre-existing Bane while active.");
        Assert.Equal(Tick(no,16m),Tick(active,16m));
        Assert.Equal(Tick(no,18m),Tick(active,18m));
    }

    [Theory]
    [InlineData("drain-mana",2.4)]
    [InlineData("drain-soul",3.4)]
    public void LegallyInterruptedChannelsNeverGrantFurtherSameExecutionTicks(
        string spell,double hitAt)
    {
        var r=WarlockSimRequest.CreatePreset("affliction");
        r.EnablePet=false;r.PrePullSacrificedDemon=null;r.UseLifeTap=false;
        r.TargetMaximumHealth=1_000_000m;r.TargetStartingMana=10000m;
        r.StartingSoulShards=0;r.TargetYieldsSoulShard=true;
        r.DrainSoulShardChancePercent=100m;
        r.Talents.Ranks["fel-concentration"]=0;
        r.DurationSeconds=9m;r.BaseSpellHitPercent=100m;r.SpellCritPercent=0m;
        r.IncomingDamageAtSeconds=(decimal)hitAt;
        r.IncomingDamageAmount=250m;r.IncomingDamageSchool="physical";
        r.CustomPriority=[spell];
        var run=new WarlockSimRunner().Run(r);
        // An earlier channel can be independently cancelled/restarted by
        // rotation decisions. Resolve the ACTUALLY interrupted execution,
        // rather than assuming only one channel ever started before the hit.
        var cancelled=Assert.Single(run.Run.Timeline,e=>
            e.Type==CombatEventType.AbilityChannelCancelled &&
            e.AbilityKey==spell && e.TimeSeconds==(decimal)hitAt);
        var started=Assert.Single(run.Run.Timeline,e=>
            e.Type==CombatEventType.AbilityChannelStarted &&
            e.AbilityKey==spell &&
            e.AbilityExecutionId==cancelled.AbilityExecutionId);
        Assert.Equal((decimal)hitAt,cancelled.TimeSeconds);
        Assert.DoesNotContain(run.Run.Timeline,e=>e.AbilityExecutionId==
            started.AbilityExecutionId && e.TimeSeconds>cancelled.TimeSeconds &&
            (e.Type==CombatEventType.Damage && e.IsChannelTick ||
             e.Type==CombatEventType.ResourceChanged &&
             e.EffectKey is ("drain-mana-target" or "drain-mana-caster" or
                 "drain-soul-damage-shard")));
        Assert.Contains(run.Run.Timeline,e=>e.AbilityExecutionId==
            started.AbilityExecutionId && e.Type==CombatEventType.Damage &&
            e.IsChannelTick || spell=="drain-mana" &&
            e.AbilityExecutionId==started.AbilityExecutionId &&
            e.Type==CombatEventType.ResourceChanged &&
            e.EffectKey=="drain-mana-target");
        Assert.True(run.Run.CustomMetrics["warlock-channel-cancellations"]>=1m);
    }
}
