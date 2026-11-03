using System;
using System.Linq;
using System.Collections.Generic;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Warlock;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175F3C0WarlockCrossTreeIntegration
{
    // Source-backed representative allocations, preserving exact 51-point
    // tree budgets rather than synthetic preset-based effects.
    private static ForeverWarlockTalentBuild Build(string tree)
    {
        var source=ForeverWarlockTalentBuild.Default(tree);
        return source;
    }

    private static WarlockSimRequest Setup(string spec,ForeverWarlockTalentBuild talents,
        params string[] actions)
    {
        var r=WarlockSimRequest.CreatePreset(spec);
        r.Talents=talents;
        r.PrePullSacrificedDemon=null;
        r.UseLifeTap=false;
        r.EnablePet=false;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.TargetMaximumHealth=1_000_000m;
        r.DurationSeconds=26m;
        r.CustomPriority=actions.ToList();
        r.Tuning.ManaRegenerationPerSecond=100m;
        return r;
    }

    [Fact]
    public void AfflictionPresetExecutesLearnedDestructionCapstonesAndRemovalFailsClosed()
    {
        var ranks=Build("destruction");
        var chosen=Setup("affliction",ranks,"immolate","conflagrate","incinerate");
        chosen.Talents.Validate(60,"affliction");
        var run=new WarlockSimRunner().Run(chosen);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="conflagrate"&&e.Amount>0m);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="incinerate"&&e.Amount>0m);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="incinerate"&&e.ResourceKey=="mana"&&e.Amount<0m);
        chosen.Talents.Ranks.Remove("incinerate");
        Assert.Throws<ArgumentException>(()=>WarlockAdaptiveRotation.Build(chosen));
        Assert.Throws<ArgumentException>(()=>new WarlockSimRunner().Run(chosen));
    }

    [Fact]
    public void DestructionStartingPresetAllowsDemonologyUnholyPowerOnActualImpDamage()
    {
        WarlockSimRequest BuildRun(int ranks)
        {
            var talents=Build("demonology");
            talents.Ranks["unholy-power"]=ranks;
            var r=Setup("destruction",talents,"shadow-bolt");
            r.EnablePet=true;r.PetType="imp";r.DurationSeconds=40m;
            r.Talents.Validate(60,"destruction");
            return r;
        }
        var no=new WarlockSimRunner().Run(BuildRun(0));
        var yes=new WarlockSimRunner().Run(BuildRun(5));
        var replay=new WarlockSimRunner().Run(BuildRun(5));
        Assert.True(no.PetDamage>0m);
        Assert.Equal(no.PetDamage*1.10m,yes.PetDamage);
        Assert.Equal(yes.PetDamage,replay.PetDamage);
        Assert.Contains(yes.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="warlock-pet" && e.Amount>0m);
        Assert.Equal(yes.WarlockDamage,no.WarlockDamage);
    }

    [Fact]
    public void DemonologyPresetExecutesAfflictionCorruptionAndWrackWithRealRankBonuses()
    {
        WarlockSimRequest Request(int corruptionRanks)
        {
            var talents=Build("affliction");
            talents.Ranks["improved-corruption"]=corruptionRanks;
            // Isolate the ordinary 2%-per-rank Corruption damage modifier.
            // Pandemic's separate periodic critical rolls are not this test.
            talents.Ranks["pandemic"]=0;
            talents.Ranks["malevolence"]=0;
            var r=Setup("demonology",talents,"corruption","wrack","shadow-bolt");
            r.UseCorruption=true;r.UseWrack=true;
            r.Talents.Validate(60,"demonology");
            return r;
        }
        var no=new WarlockSimRunner().Run(Request(0));
        var selected=new WarlockSimRunner().Run(Request(5));
        static decimal First(WarlockSimResponse result)=>result.Run.Timeline.First(e=>
            e.Type==CombatEventType.Damage && e.AbilityKey=="corruption" &&
            e.IsPeriodic&&e.Amount>0m).Amount!.Value;
        Assert.Equal(First(no)*1.10m,First(selected));
        Assert.Contains(selected.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="wrack"&&e.IsChannelTick&&e.Amount>0m);
        Assert.InRange(Math.Abs(selected.DamageDone-selected.DamageByAbility.Values.Sum()),0m,.000001m);
        var replay=new WarlockSimRunner().Run(Request(5));
        Assert.Equal(selected.DamageDone,replay.DamageDone);
    }

    [Theory]
    [InlineData("amplify-curse")]
    [InlineData("wrack")]
    [InlineData("siphon-life")]
    [InlineData("conflagrate")]
    [InlineData("incinerate")]
    [InlineData("shadowburn")]
    [InlineData("bane-of-havoc")]
    public void UnlearnedTalentActionCannotSurviveChangedPresetOrSavedPriority(string action)
    {
        var r=WarlockSimRequest.CreatePreset("affliction");
        r.Talents.Ranks.Remove(action);
        r.CustomPriority=[action];
        Assert.Throws<ArgumentException>(()=>WarlockAdaptiveRotation.Build(r));
    }
}
