using System;
using System.Linq;
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp168SevenDemonOffensiveCertification
{
    private static WarlockSimRequest Setup(string family)
    {
        var r=WarlockSimRequest.CreatePreset("demonology");
        r.PetType=family;
        r.PrePullSacrificedDemon=null;
        r.AllowTemporaryDemon=true;
        r.AllowDoomguardRitual=true;
        r.TargetMaximumHealth=1000000m;
        r.DurationSeconds=32m;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.DemonTuning.DemonSpellHitPercent=100m;
        r.PetMeleeHitPercent=100m;
        r.UseLifeTap=false;
        r.CustomPriority=["shadow-bolt"];
        r.CaptureTimeline=true;
        return r;
    }

    [Theory]
    [InlineData("imp","demon-firebolt",false)]
    [InlineData("voidwalker","pet-melee",true)]
    [InlineData("succubus","demon-lash-of-pain",true)]
    [InlineData("incubus","demon-lash-of-pain",true)]
    [InlineData("felhunter","demon-shadow-bite",true)]
    [InlineData("infernal","demon-immolation",true)]
    [InlineData("doomguard","demon-doom-bolt",true)]
    public void EachFamilyDealsRealOffensiveDamageAndReconcilesThreatAndResource(
        string family,string ability,bool hasMelee)
    {
        var req=Setup(family);
        var result=new WarlockSimRunner().Run(req);
        var pet=result.Run.Summary.ActorSummaries["warlock-pet"];
        Assert.True(pet.DamageDone>0m);
        Assert.True(result.DemonUptimeSeconds>0m);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="warlock-pet" && e.TargetActorKey=="target" &&
            e.AbilityKey==ability && e.Amount>0m);
        Assert.Equal(hasMelee,result.DemonMeleeDamage>0m);
        Assert.InRange(Math.Abs(result.PetDamage-result.DemonMeleeDamage-result.DemonSpellDamage),
            0m,.000001m);
        Assert.InRange(Math.Abs(result.DamageDone-result.PetDamage-result.WarlockDamage),
            0m,.000001m);
        var petThreat=result.Run.Timeline.Where(e=>e.Type==CombatEventType.ThreatChanged &&
            e.SourceActorKey=="warlock-pet").Sum(e=>e.Amount??0m);
        Assert.True(petThreat>=pet.DamageDone,
            family+" threat must include all effective pet damage");
        Assert.Contains("PetDamage",JsonSerializer.Serialize(result));
    }

    [Theory]
    [InlineData("imp")]
    [InlineData("voidwalker")]
    [InlineData("succubus")]
    [InlineData("incubus")]
    [InlineData("felhunter")]
    [InlineData("infernal")]
    [InlineData("doomguard")]
    public void ScriptedCompanionDeathLeavesNoLaterOffensiveDamage(string family)
    {
        var req=Setup(family);
        req.DemonDeathAtSeconds=10m;
        var result=new WarlockSimRunner().Run(req);
        Assert.True(result.DemonDied);
        Assert.DoesNotContain(result.Run.Timeline,e=>
            e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="warlock-pet" && e.TimeSeconds>10m &&
            e.TargetActorKey=="target" && e.Amount>0m);
        Assert.InRange(result.DemonUptimeSeconds,9m,10m);
    }

    [Theory]
    [InlineData("infernal")]
    [InlineData("doomguard")]
    public void TemporaryExpiryStopsAllSpellAndMeleeDamage(string family)
    {
        var req=Setup(family);
        req.DurationSeconds=23m;
        req.DemonTuning.TemporaryDemonDurationSeconds=8m;
        var result=new WarlockSimRunner().Run(req);
        Assert.True(result.DemonUptimeSeconds<=8.001m);
        Assert.DoesNotContain(result.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="warlock-pet" && e.TargetActorKey=="target" &&
            e.TimeSeconds>8.75m && e.Amount>0m);
    }
}
