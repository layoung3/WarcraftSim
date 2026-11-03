using System;
using System.Collections.Generic;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Warlock;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175F3C2DemonologyMechanicalCompletion
{
    private static WarlockSimRequest Request(string family,string spec="demonology")
    {
        var r=WarlockSimRequest.CreatePreset(spec);
        r.PetType=family;
        r.EnablePet=true;
        r.PrePullSacrificedDemon=null;
        r.AllowTemporaryDemon=true;
        r.AllowDoomguardRitual=true;
        r.UseLifeTap=false;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.DemonTuning.DemonSpellHitPercent=100m;
        r.PetMeleeHitPercent=100m;
        r.TargetMaximumHealth=10_000_000m;
        r.DurationSeconds=15m;
        r.CustomPriority=["shadow-bolt"];
        r.Talents.Ranks["unholy-power"]=0;
        r.Talents.Ranks["master-demonologist"]=0;
        r.Talents.Ranks["soul-link"]=0;
        r.Talents.Ranks["demonic-pact"]=0; // prerequisite removed with Soul Link
        return r;
    }

    [Theory]
    [InlineData("imp",0,1.0)]
    [InlineData("imp",1,1.1)]
    [InlineData("imp",3,1.3)]
    [InlineData("succubus",0,1.0)]
    [InlineData("incubus",2,1.2)]
    [InlineData("incubus",3,1.3)]
    public void ImprovedPetSpellTalentScalesBothBaseAndInheritedSpellPower(
        string family,int rank,double expected)
    {
        var profile=ForeverWarlockDemons.Get(family);
        var book=ForeverWarlockDemonSpellbook.Create(profile,
            new ForeverWarlockDemonTuning(),0,
            family=="imp"?rank:0,
            family=="imp"?0:rank);
        var effect=family=="imp"?
            book.Single(a=>a.Key=="demon-firebolt").Effects.Single() :
            book.Single(a=>a.Key=="demon-lash-of-pain").Effects.Single();
        var baseDamage=family=="imp"?165m:55m;
        Assert.Equal(baseDamage*(decimal)expected,effect.MinimumValue);
        Assert.Equal(.15m*(decimal)expected,effect.ScalingCoefficient);
    }

    [Theory]
    [InlineData(0,13)]
    [InlineData(1,14.3)]
    [InlineData(3,16.9)]
    public void ImpFireShieldRequiresRealCastAndPhysicalIncomingHit(int ranks,double expected)
    {
        var req=Request("imp");
        req.UseImpFireShield=true;
        req.DurationSeconds=8m;
        req.IncomingDamageAtSeconds=3m;
        req.IncomingDamageAmount=400m;
        req.IncomingDamageSchool="physical";
        req.Talents.Ranks["improved-imp"]=ranks;
        var result=new WarlockSimRunner().Run(req);
        Assert.Contains(result.Run.Timeline,e=>
            e.Type==CombatEventType.AbilityCastCompleted &&
            e.SourceActorKey=="warlock-pet" && e.AbilityKey=="demon-fire-shield");
        Assert.Contains(result.Run.Timeline,e=>
            e.Type==CombatEventType.ResourceChanged &&
            e.SourceActorKey=="warlock-pet" && e.ResourceKey=="mana" &&
            e.AbilityKey=="demon-fire-shield" && e.Amount==-165m);
        var reflected=Assert.Single(result.Run.Timeline,e=>
            e.Type==CombatEventType.Damage && e.AbilityKey=="demon-fire-shield");
        Assert.Equal((decimal)expected,reflected.Amount);
        Assert.Equal("warlock-pet",reflected.SourceActorKey);
        Assert.Equal("target",reflected.TargetActorKey);
        Assert.Equal(1m,result.Run.CustomMetrics["warlock-imp-fire-shield-procs"]);
    }

    [Fact]
    public void ImpFireShieldExcludesMagicalHitsAndMissingDemon()
    {
        var req=Request("imp");
        req.UseImpFireShield=true;
        req.IncomingDamageAtSeconds=3m;
        req.IncomingDamageAmount=400m;
        req.IncomingDamageSchool="fire";
        var magic=new WarlockSimRunner().Run(req);
        Assert.DoesNotContain(magic.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="demon-fire-shield");
        req.PetType="felhunter";
        Assert.Throws<ArgumentException>(()=>new WarlockSimRunner().Run(req));
        req.PetType="imp";
        req.EnablePet=false;
        Assert.Throws<ArgumentException>(()=>new WarlockSimRunner().Run(req));
    }

    [Theory]
    [InlineData(0,1930)]
    [InlineData(1,2123)]
    [InlineData(3,2509)]
    public void VoidwalkerSacrificeCreatesFiniteAbsorbAndEndsItsAttacks(
        int rank,int expectedShield)
    {
        var req=Request("voidwalker");
        req.Talents.Ranks["improved-voidwalker"]=rank;
        req.VoidwalkerSacrificeAtSeconds=2m;
        req.IncomingDamageAtSeconds=3m;
        req.IncomingDamageAmount=3000m;
        req.IncomingDamageSchool="physical";
        req.DurationSeconds=8m;
        var run=new WarlockSimRunner().Run(req);
        var absorb=Assert.Single(run.Run.Timeline,e=>
            e.Type==CombatEventType.AbsorbApplied &&
            e.AbilityKey=="demon-voidwalker-sacrifice");
        Assert.Equal((decimal)expectedShield,absorb.Amount);
        var spent=Assert.Single(run.Run.Timeline,e=>
            e.Type==CombatEventType.AbsorbConsumed &&
            e.AbilityKey=="demon-voidwalker-sacrifice");
        Assert.Equal((decimal)expectedShield,spent.Amount);
        var hit=Assert.Single(run.Run.Timeline,e=>
            e.Type==CombatEventType.Damage &&
            e.AbilityKey=="scripted-caster-incoming-damage");
        Assert.Equal(3000m-expectedShield,hit.Amount);
        Assert.True(run.DemonDied);
        Assert.Equal(1m,run.Run.CustomMetrics["warlock-voidwalker-sacrifice-shields"]);
        Assert.DoesNotContain(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="warlock-pet" && e.TimeSeconds>2m &&
            e.TargetActorKey=="target");
    }

    [Fact]
    public void VoidwalkerSacrificeIsNotAvailableToOtherDemonsOrAbsentPets()
    {
        var req=Request("imp");
        req.VoidwalkerSacrificeAtSeconds=2m;
        Assert.Throws<ArgumentException>(()=>new WarlockSimRunner().Run(req));
        req.PetType="voidwalker";
        req.EnablePet=false;
        Assert.Throws<ArgumentException>(()=>new WarlockSimRunner().Run(req));
    }

    [Theory]
    [InlineData("imp",true)]
    [InlineData("felhunter",true)]
    [InlineData("infernal",false)]
    [InlineData("doomguard",false)]
    public void UnholyPowerDoesNotLeakToTemporaryDemonFamilies(string family,bool eligible)
    {
        WarlockSimRequest WithRank(int rank)
        {
            var r=Request(family);
            r.Talents.Ranks["unholy-power"]=rank;
            r.DurationSeconds=18m;
            return r;
        }
        var plain=new WarlockSimRunner().Run(WithRank(0));
        var ranked=new WarlockSimRunner().Run(WithRank(5));
        Assert.True(plain.PetDamage>0m);
        Assert.Equal(eligible?plain.PetDamage*1.10m:plain.PetDamage,ranked.PetDamage);
        Assert.Equal(plain.WarlockDamage,ranked.WarlockDamage);
    }

    private sealed class NoMitigation : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext context,
            SimulationActorState source,SimulationActorState target,
            AbilityDefinition ability,AbilityEffectDefinition effect,decimal amount) =>
            DamageMitigationResult.Unmitigated(amount);
    }

    [Theory]
    [InlineData("imp",.65)]
    [InlineData("voidwalker",1.50)]
    [InlineData("felhunter",1.0)]
    [InlineData("doomguard",1.60)]
    public void LiveOwnerStaminaApplyExpirePreservesPetHealthDeficit(
        string family,double multiplier)
    {
        var r=Request(family);
        r.Talents.Ranks["fel-vitality"]=3;
        r.Talents.Ranks["demonic-embrace"]=5;
        r.Tuning.PetAttackPowerFromIntellect=0m;
        var context=new SimulationContext(new SimulationRunOptions {DurationSeconds=30m});
        var owner=new SimulationActorState{Key="warlock",TeamKey="raid",Level=60,
            Name="Owner",MaximumHealth=4000m};
        owner.Stats.Set("stamina",115m);
        owner.Stats.Set("intellect",190m);
        owner.Stats.Set("spell-power",200m);
        owner.InitializeHealth(owner.MaximumHealth);
        var enemy=new SimulationActorState{Key="target",TeamKey="enemy",Level=63,
            Name="Target",MaximumHealth=100000m};
        enemy.InitializeHealth(enemy.MaximumHealth);
        context.AddActor(owner);context.AddActor(enemy);
        var auras=new AuraManager();
        var executor=new AbilityExecutor(auras,new SimpleCombatRollResolver(),
            new NoMitigation());
        var pet=new WarlockDemonCompanion(r,context,owner,enemy,auras,
            new AutoAttackProcessor(executor),executor,200m,180m);
        var before=pet.Actor.MaximumHealth;
        pet.Actor.TakeDamage(300m);
        pet.ApplyOwnerDynamicDelta(new Dictionary<string,decimal>{{"stamina",10m}});
        var familyMultiplier=(decimal)multiplier;
        var felVitality=family is "doomguard"?1m:1.15m;
        var delta=10m*1.15m*2m*familyMultiplier*felVitality;
        Assert.Equal(before+delta,pet.Actor.MaximumHealth);
        Assert.Equal(before-300m+delta,pet.Actor.CurrentHealth);
        pet.ApplyOwnerDynamicDelta(new Dictionary<string,decimal>{{"stamina",-10m}});
        Assert.Equal(before,pet.Actor.MaximumHealth);
        Assert.Equal(before-300m,pet.Actor.CurrentHealth);
        pet.ApplyOwnerDynamicDelta(new Dictionary<string,decimal>{{"stamina",10m}});
        pet.ApplyOwnerDynamicDelta(new Dictionary<string,decimal>{{"stamina",-10m}});
        Assert.Equal(before,pet.Actor.MaximumHealth);
    }

    [Fact]
    public void CrossTreeDestructionPresetPreservesDemonologyImpFireShieldAndReplay()
    {
        var request=Request("imp","destruction");
        request.Talents=ForeverWarlockTalentBuild.Default("demonology");
        request.Talents.Ranks["improved-imp"]=3;
        request.Talents.Ranks["unholy-power"]=0;
        request.Talents.Ranks["master-demonologist"]=0;
        request.Talents.Ranks["soul-link"]=0;
        request.Talents.Ranks["demonic-pact"]=0;
        request.Talents.Validate(60,"destruction");
        request.UseImpFireShield=true;
        request.IncomingDamageAtSeconds=3m;
        request.IncomingDamageAmount=500m;
        request.IncomingDamageSchool="physical";
        var a=new WarlockSimRunner().Run(request);
        var b=new WarlockSimRunner().Run(request);
        Assert.Equal(a.PetDamage,b.PetDamage);
        Assert.Equal(a.WarlockDamage,b.WarlockDamage);
        Assert.Contains(a.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="demon-fire-shield" && e.Amount==16.9m);
        Assert.Contains(a.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="shadow-bolt" && e.SourceActorKey=="warlock");
    }
}
