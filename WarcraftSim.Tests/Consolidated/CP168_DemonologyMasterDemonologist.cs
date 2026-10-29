using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp168DemonologyMasterDemonologist
{
    private static WarlockSimRequest Setup(string demon,string school,int rank)
    {
        var r=WarlockSimRequest.CreatePreset("demonology");
        r.EnablePet=true;
        r.PetType=demon;
        r.PrePullSacrificedDemon=null;
        r.UseLifeTap=false;
        r.DurationSeconds=9m;
        r.TargetMaximumHealth=1000000m;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.CustomPriority=["shadow-bolt"];
        r.Talents.Ranks["master-demonologist"]=rank;
        r.IncomingDamageAtSeconds=3m;
        r.IncomingDamageAmount=1000m;
        r.IncomingDamageSchool=school;
        r.DemonIncomingDamageAtSeconds=4m;
        r.DemonIncomingDamageAmount=500m;
        r.DemonIncomingDamageSchool=school;
        r.CaptureTimeline=true;
        return r;
    }

    [Theory]
    [InlineData("voidwalker","physical")]
    [InlineData("felhunter","fire")]
    public void EligibleFamilyReducesOwnerAndPetIncomingDamageAtRankFive(string demon,string school)
    {
        var zero=new WarlockSimRunner().Run(Setup(demon,school,0));
        var five=new WarlockSimRunner().Run(Setup(demon,school,5));
        decimal OwnerHit(WarlockSimResponse r)=>r.Run.Timeline.Single(e=>
            e.Type==CombatEventType.Damage &&
            e.AbilityKey=="scripted-caster-incoming-damage" &&
            e.TargetActorKey=="warlock").Amount!.Value;
        decimal PetHit(WarlockSimResponse r)=>r.Run.Timeline.Single(e=>
            e.Type==CombatEventType.Damage &&
            e.AbilityKey=="scripted-demon-incoming-hit").Amount!.Value;
        Assert.Equal(OwnerHit(zero)*.90m,OwnerHit(five));
        Assert.Equal(PetHit(zero)*.90m,PetHit(five));
    }

    [Theory]
    [InlineData("voidwalker","fire")]
    [InlineData("felhunter","physical")]
    [InlineData("imp","fire")]
    public void OtherFamilyOrDamageSchoolDoesNotGrantMitigation(string demon,string school)
    {
        var zero=new WarlockSimRunner().Run(Setup(demon,school,0));
        var five=new WarlockSimRunner().Run(Setup(demon,school,5));
        Assert.Equal(zero.EndingHealth,five.EndingHealth);
        var a=zero.Run.Timeline.Single(e=>e.AbilityKey=="scripted-demon-incoming-hit" &&
            e.Type==CombatEventType.Damage);
        var b=five.Run.Timeline.Single(e=>e.AbilityKey=="scripted-demon-incoming-hit" &&
            e.Type==CombatEventType.Damage);
        Assert.Equal(a.Amount,b.Amount);
    }
}
