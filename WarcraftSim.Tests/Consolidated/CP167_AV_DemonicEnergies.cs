using System;
using System.Linq;
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167AvDemonicEnergies
{
    private static WarlockSimRequest Request(int rank,bool injured=true)
    {
        var r=WarlockSimRequest.CreatePreset("demonology");
        r.PetType="imp";
        r.PrePullSacrificedDemon=null;
        r.EnablePet=true;
        r.UseLifeTap=false;
        r.Talents.Ranks["demonic-energies"]=rank;
        r.TargetMaximumHealth=1000000m;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.CaptureTimeline=true;
        r.DurationSeconds=8m;
        r.CustomPriority=["shadow-bolt"];
        if(injured)
        {
            r.DemonIncomingDamageAtSeconds=1m;
            r.DemonIncomingDamageAmount=800m;
        }
        return r;
    }

    [Theory]
    [InlineData(1,0.08)]
    [InlineData(2,0.15)]
    public void OwnerShadowSpellDamageHealsActiveInjuredDemonForClientRankShare(
        int rank,double share)
    {
        var run=new WarlockSimRunner().Run(Request(rank));
        var spell=run.Run.Timeline.First(e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="warlock" && e.AbilityKey=="shadow-bolt" &&
            e.Amount is >0m);
        var heal=run.Run.Timeline.First(e=>e.Type==CombatEventType.Healing &&
            e.AbilityKey=="demonic-energies" && e.TargetActorKey=="warlock-pet");
        Assert.Equal(spell.Amount!.Value*(decimal)share,heal.RawAmount);
        Assert.True(heal.Amount>0m);
        Assert.Equal(run.Run.Timeline.Where(e=>e.Type==CombatEventType.Healing &&
            e.AbilityKey=="demonic-energies").Sum(e=>e.Amount??0m),
            run.Run.CustomMetrics["warlock-demonic-energies-pet-healing"]);
        Assert.InRange(Math.Abs(run.DamageDone-run.DamageByAbility.Values.Sum()),
            0m,.000001m);
    }

    [Fact]
    public void OwnerSpellCannotHealDeadOrAbsentCompanion()
    {
        var dead=Request(2);
        dead.DemonIncomingDamageAmount=10000m;
        var result=new WarlockSimRunner().Run(dead);
        Assert.True(result.DemonDied);
        Assert.Equal(0m,result.Run.CustomMetrics["warlock-demonic-energies-pet-healing"]);
        var absent=Request(2);
        absent.EnablePet=false;
        var without=new WarlockSimRunner().Run(absent);
        Assert.Equal(0m,without.Run.CustomMetrics["warlock-demonic-energies-pet-healing"]);
    }

    [Fact]
    public void LifeTapTransfersManaToRealPetWhenPetHasSpentMana()
    {
        var r=Request(2,false);
        r.UseLifeTap=true;
        r.StartingManaPercent=0m;
        r.LifeTapBelowManaPercent=95m;
        r.DurationSeconds=15m;
        r.DemonTuning.BaseMana=210m;
        r.DemonTuning.ManaRegenPerSecond=0m;
        var run=new WarlockSimRunner().Run(r);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="demonic-energies" &&
            e.EffectKey=="demonic-energies-mana" &&
            e.SourceActorKey=="warlock-pet" && e.Amount is >0m);
        Assert.True(run.Run.CustomMetrics["warlock-demonic-energies-pet-mana"]>0m);
    }

    [Fact]
    public void RankCapAndJsonSerializationRemainValid()
    {
        var r=Request(2);
        var roundtrip=JsonSerializer.Deserialize<WarlockSimRequest>(
            JsonSerializer.Serialize(r));
        Assert.NotNull(roundtrip);
        Assert.Equal(2,roundtrip.Talents.Rank("demonic-energies"));
        r.Talents.Ranks["demonic-energies"]=3;
        Assert.Throws<ArgumentException>(()=>new WarlockSimRunner().Run(r));
    }
}
