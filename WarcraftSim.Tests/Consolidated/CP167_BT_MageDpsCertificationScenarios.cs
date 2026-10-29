using System;
using System.Linq;
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>
/// CP167 Mage mechanical certification: real 90-second default and mixed
/// custom DPS rotations against explicit targets, with full resource and
/// combat-report conservation and JSON request compatibility.
/// </summary>
public sealed class Cp167BtMageDpsCertificationScenarios
{
    [Theory]
    [InlineData("arcane")]
    [InlineData("fire")]
    [InlineData("frost")]
    public void EachLevelSixtyDefaultExecutesAndConservesDamageAndMana(string spec)
    {
        var r=MageSimRequest.CreatePreset(spec);
        r.DurationSeconds=90m;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=18m;
        r.AdditionalTargets=2;
        r.TargetMaximumHealth=1000000m;
        r.CaptureTimeline=true;
        r.UseMajorManaPotion=false;
        r.CombatItems.ManaRubyCharges=0;
        var sim=new MageSimRunner().Run(r);
        Assert.True(sim.DamageDone>0m,spec+" has no resolved damage.");
        Assert.InRange(Math.Abs(sim.DamageDone-sim.DamageByAbility.Values.Sum()),
            0m,.000001m);
        Assert.Equal(sim.DamageDone,sim.Run.Summary.ActorSummaries["mage"].DamageDone);
        Assert.InRange(sim.EndingMana,0m,sim.MaximumMana);
        Assert.True(sim.Run.ProcessedEventCount>0);
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="mage" && e.Amount is >0m);
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.ResourceKey=="mana" && e.Amount is <0m);
        var roundTrip=JsonSerializer.Deserialize<MageSimRequest>(JsonSerializer.Serialize(r));
        Assert.NotNull(roundTrip);
        Assert.Equal(spec,roundTrip.Specialization);
        roundTrip.Talents.Validate(spec);
    }

    [Theory]
    [InlineData("arcane")]
    [InlineData("fire")]
    [InlineData("frost")]
    public void RepresentativeCooldownProcAndAoeCustomRotationExecutes(string spec)
    {
        var r=MageSimRequest.CreatePreset(spec);
        r.DurationSeconds=90m;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=18m;
        r.AdditionalTargets=2;
        r.TargetMaximumHealth=1000000m;
        r.CaptureTimeline=true;
        r.UseEvocation=false;
        r.UseMajorManaPotion=false;
        r.CombatItems.ManaRubyCharges=0;
        r.Tuning.ManaRegenPerSecond=100m;
        r.CustomPriority=spec switch
        {
            "arcane"=>["arcane-power","presence-of-mind","arcane-missiles",
                "arcane-blast","arcane-wrath","arcane-explosion","fireball"],
            "fire"=>["combustion","fire-blast","scorch","pyroblast",
                "flamestrike","fireball"],
            "frost"=>["frost-nova","cone-of-cold","cold-snap",
                "blizzard","frost-blast","frostbolt","ice-lance"],
            _=>throw new ArgumentException(spec)
        };
        var sim=new MageSimRunner().Run(r);
        Assert.True(sim.DamageDone>0m,spec);
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.TargetActorKey=="target-1" && e.Amount is >0m);
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastCompleted &&
            e.SourceActorKey=="mage");
        Assert.InRange(Math.Abs(sim.DamageDone-sim.DamageByAbility.Values.Sum()),
            0m,.000001m);
        Assert.Equal(sim.DamageDone,sim.Run.Summary.ActorSummaries["mage"].DamageDone);
        Assert.InRange(sim.EndingMana,0m,sim.MaximumMana);
    }
}