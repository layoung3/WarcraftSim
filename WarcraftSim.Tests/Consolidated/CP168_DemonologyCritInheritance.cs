using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp168DemonologyCritInheritance
{
    private static WarlockSimRequest Setup(string family,decimal ownerCrit)
    {
        var r=WarlockSimRequest.CreatePreset("demonology");
        r.PetType=family;
        r.EnablePet=true;
        r.PrePullSacrificedDemon=null;
        r.DurationSeconds=20m;
        r.TargetMaximumHealth=1000000m;
        r.BaseSpellHitPercent=100m;
        r.DemonTuning.DemonSpellHitPercent=100m;
        r.PetMeleeHitPercent=100m;
        r.SpellCritPercent=ownerCrit;
        r.Talents.Ranks["demonic-brand"]=0;
        r.UseLifeTap=false;
        r.CustomPriority=["shadow-bolt"];
        r.CaptureTimeline=true;
        return r;
    }

    [Theory]
    [InlineData("imp","demon-firebolt")]
    [InlineData("succubus","demon-lash-of-pain")]
    [InlineData("felhunter","demon-shadow-bite")]
    [InlineData("doomguard","demon-doom-bolt")]
    public void OwnerSpellCritTransfersToRealDemonSpells(string family,string spell)
    {
        var zero=Setup(family,0m);
        var hundred=Setup(family,100m);
        if(family=="doomguard"){
            zero.AllowTemporaryDemon=true;zero.AllowDoomguardRitual=true;
            hundred.AllowTemporaryDemon=true;hundred.AllowDoomguardRitual=true;
        }
        var a=new WarlockSimRunner().Run(zero);
        var b=new WarlockSimRunner().Run(hundred);
        var hits=a.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="warlock-pet" && e.AbilityKey==spell &&
            e.Amount>0m).ToArray();
        var crits=b.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="warlock-pet" && e.AbilityKey==spell &&
            e.Amount>0m).ToArray();
        Assert.NotEmpty(hits);
        Assert.NotEmpty(crits);
        Assert.All(hits,e=>Assert.False(e.IsCritical));
        Assert.All(crits,e=>Assert.True(e.IsCritical));
    }

    [Theory]
    [InlineData("voidwalker")]
    [InlineData("succubus")]
    [InlineData("incubus")]
    [InlineData("felhunter")]
    public void OwnerCritTransfersThroughCreatureMeleeAttackTable(string family)
    {
        var zero=new WarlockSimRunner().Run(Setup(family,0m));
        var full=new WarlockSimRunner().Run(Setup(family,100m));
        var baseMelee=zero.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="warlock-pet" && e.AbilityKey=="pet-melee" &&
            e.Amount>0m).ToArray();
        var boosted=full.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="warlock-pet" && e.AbilityKey=="pet-melee" &&
            e.Amount>0m).ToArray();
        Assert.NotEmpty(baseMelee);
        Assert.NotEmpty(boosted);
        Assert.All(baseMelee,e=>Assert.False(e.IsCritical));
        Assert.All(boosted,e=>Assert.True(e.IsCritical));
    }

    [Fact]
    public void ExplicitPetSpellCritOverrideStillPermitsIndependentTestInputs()
    {
        var r=Setup("imp",100m);
        r.DemonTuning.DemonSpellCritPercent=0m;
        var sim=new WarlockSimRunner().Run(r);
        Assert.Contains(sim.Run.Timeline,e=>e.SourceActorKey=="warlock-pet" &&
            e.AbilityKey=="demon-firebolt" && e.Type==CombatEventType.Damage &&
            e.Amount>0m && !e.IsCritical);
        Assert.DoesNotContain(sim.Run.Timeline,e=>e.SourceActorKey=="warlock-pet" &&
            e.AbilityKey=="demon-firebolt" && e.IsCritical);
    }
}
