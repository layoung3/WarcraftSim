using System;
using System.Linq;
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp168DemonologySoulLinkTransfers
{
    private static WarlockSimRequest Setup(string family="imp")
    {
        var r=WarlockSimRequest.CreatePreset("demonology");
        r.PetType=family;
        r.PrePullSacrificedDemon=null;
        r.EnablePet=true;
        r.AllowTemporaryDemon=true;
        r.AllowDoomguardRitual=true;
        r.DurationSeconds=12m;
        r.TargetMaximumHealth=1000000m;
        r.UseLifeTap=false;
        r.CustomPriority=["shadow-bolt"];
        r.IncomingDamageAtSeconds=3m;
        r.IncomingDamageAmount=1000m;
        // Physical fixtures isolate Soul Link from Felhunter's independent
        // Master Demonologist magic mitigation.
        r.IncomingDamageSchool="physical";
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.CaptureTimeline=true;
        return r;
    }

    [Theory]
    [InlineData("imp")]
    [InlineData("voidwalker")]
    [InlineData("succubus")]
    [InlineData("incubus")]
    [InlineData("felhunter")]
    public void ActivePermanentDemonTakesThirtyPercentOfScriptedOwnerDamage(string family)
    {
        var request=Setup(family);
        var run=new WarlockSimRunner().Run(request);
        var transfer=Assert.Single(run.Run.Timeline,e=>
            e.Type==CombatEventType.Damage &&
            e.AbilityKey=="soul-link-transfer" && e.TargetActorKey=="warlock-pet");
        Assert.Equal(300m,transfer.RawAmount);
        Assert.Equal(300m,transfer.Amount);
        Assert.Contains(run.Run.Timeline,e=>
            e.Type==CombatEventType.Damage &&
            e.AbilityKey=="scripted-caster-incoming-damage" &&
            e.TargetActorKey=="warlock" && e.Amount==700m);
        Assert.Equal(request.WarlockMaximumHealth-700m,run.EndingHealth);
        Assert.InRange(Math.Abs(run.DamageDone-run.WarlockDamage-run.PetDamage),0m,.0001m);
        Assert.Contains("soul-link-transfer",JsonSerializer.Serialize(run));
    }

    [Theory]
    [InlineData("infernal")]
    [InlineData("doomguard")]
    public void TemporaryDemonsCannotRedirectOrGrantSoulLinkBuff(string family)
    {
        var with=Setup(family);
        var linked=new WarlockSimRunner().Run(with);
        Assert.DoesNotContain(linked.Run.Timeline,e=>e.AbilityKey=="soul-link-transfer");
        Assert.Equal(with.WarlockMaximumHealth-1000m,linked.EndingHealth);
        var noLink=Setup(family);
        noLink.Talents.Ranks["soul-link"]=0;
        noLink.Talents.Ranks["demonic-pact"]=0;
        var baseline=new WarlockSimRunner().Run(noLink);
        Assert.Equal(baseline.WarlockDamage,linked.WarlockDamage);
    }

    [Fact]
    public void DeadLinkedDemonCannotRedirectFurtherOrContinueAttacking()
    {
        var request=Setup();
        request.DemonTuning.BaseHealth=180m;
        request.DurationSeconds=15m;
        var run=new WarlockSimRunner().Run(request);
        Assert.True(run.DemonDied);
        Assert.Single(run.Run.Timeline,e=>e.AbilityKey=="soul-link-transfer" &&
            e.Type==CombatEventType.ActorDied);
        Assert.DoesNotContain(run.Run.Timeline,e=>
            e.SourceActorKey=="warlock-pet" && e.Type==CombatEventType.Damage &&
            e.TimeSeconds>3m);
    }

    [Fact]
    public void UnlinkedWarlockTakesFullDamageDespiteActivePet()
    {
        var request=Setup();
        request.Talents.Ranks["soul-link"]=0;
        request.Talents.Ranks["demonic-pact"]=0;
        var run=new WarlockSimRunner().Run(request);
        Assert.DoesNotContain(run.Run.Timeline,e=>e.AbilityKey=="soul-link-transfer");
        Assert.Equal(request.WarlockMaximumHealth-1000m,run.EndingHealth);
    }
}
