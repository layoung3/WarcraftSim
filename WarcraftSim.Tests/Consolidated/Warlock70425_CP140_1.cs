using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Warlock;
using Xunit;
namespace WarcraftSim.Tests;

public sealed class Warlock70425Cp1401Tests
{
    private static decimal M(string ability, string school, CombatEffectDeliveryType delivery,
        bool imm = false, bool wrack = false, string pet = "felhunter", int mastery = 0,
        bool soul = false, int agony = 0, string source = "warlock") =>
        Warlock70425DamageModifiers.Multiplier(source,ability,school,delivery,
            imm,wrack,pet,mastery,soul,agony);

    [Fact] public void IncinerateBonusRequiresOwnImmolate() {
        Assert.Equal(1m,M("incinerate","fire",CombatEffectDeliveryType.Direct));
        Assert.Equal(1.25m,M("incinerate","fire",CombatEffectDeliveryType.Direct,imm:true));
    }
    [Fact] public void WrackOnlyAmplifiesOtherShadowDots() {
        Assert.Equal(1.1m,M("corruption","shadow",CombatEffectDeliveryType.Periodic,wrack:true));
        Assert.Equal(1.1m,M("bane-of-agony","shadow",CombatEffectDeliveryType.Periodic,wrack:true));
        Assert.Equal(1m,M("corruption","shadow",CombatEffectDeliveryType.Periodic));
        Assert.Equal(1m,M("wrack","shadow",CombatEffectDeliveryType.ChannelTick,wrack:true));
    }
    [Fact] public void WrackHasSixSecondVulnerabilityEffect() {
        var r=WarlockSimRequest.CreatePreset("affliction");
        var wrack=Assert.Single(ForeverWarlockDpsSpellbook.Create("affliction",r.Talents,r.Tuning,200m,180m), x=>x.Key=="wrack");
        Assert.Contains(wrack.Effects,e=>e.AuraKey=="wrack-vulnerability" && e.DurationSeconds==6m);
    }
    [Fact] public void GrimReachIsValidButMaledictionIsRejected() {
        var t=WarlockSimRequest.CreatePreset("affliction").Talents;
        Assert.Equal(2,t.Rank("grim-reach"));
        t.Ranks["malediction"]=3;
        Assert.Throws<ArgumentException>(()=>t.Validate(60,"affliction"));
    }
    [Theory]
    [InlineData(0,0)] [InlineData(1,19.8)] [InlineData(2,40.2)] [InlineData(3,60)]
    public void DemonicKnowledgeNeedsPet(int rank,double expected) {
        Assert.Equal((decimal)expected,Warlock70425DamageModifiers.DemonicKnowledgeSpellPower(rank,60,true));
        Assert.Equal(0m,Warlock70425DamageModifiers.DemonicKnowledgeSpellPower(rank,60,false));
    }
    [Fact] public void SoulLinkAndMasterDemonologistAreConditional() {
        Assert.Equal(1.1m,M("shadow-bolt","shadow",CombatEffectDeliveryType.Direct,pet:"succubus",mastery:5));
        Assert.Equal(1.1m,M("incinerate","fire",CombatEffectDeliveryType.Direct,pet:"imp",mastery:5));
        Assert.Equal(1.03m,M("pet-melee","physical",CombatEffectDeliveryType.Direct,soul:true,source:"warlock-pet"));
    }
    [Fact] public void DemonologyPactHasActualPrerequisites() {
        var t=WarlockSimRequest.CreatePreset("demonology").Talents;
        Assert.Equal(1,t.Rank("demonic-sacrifice")); Assert.Equal(1,t.Rank("soul-link"));
        t.Ranks.Remove("soul-link");
        Assert.Throws<ArgumentException>(()=>t.Validate(60,"demonology"));
    }
    [Fact] public void AgonizingFlamesOnlyImprovesDestructionSpells() {
        Assert.Equal(1.1m,M("incinerate","fire",CombatEffectDeliveryType.Direct,agony:3));
        Assert.Equal(1m,M("corruption","shadow",CombatEffectDeliveryType.Periodic,agony:3));
    }
    [Fact] public void SoulFireUsesForeverOneMinuteCooldownBeforeDecimation() {
        var r=WarlockSimRequest.CreatePreset("demonology");
        r.Talents.Ranks["decimation"]=0;
        var soulFire=Assert.Single(ForeverWarlockDpsSpellbook.Create("demonology",r.Talents,r.Tuning,200m,180m),
            x=>x.Key=="soul-fire");
        Assert.Equal(60m,soulFire.CooldownSeconds);
    }
    [Fact] public void NewWarlockEffectsAppearInFullRun() {
        var a=WarlockSimRequest.CreatePreset("affliction"); a.CaptureTimeline=true;
        Assert.Contains(new WarlockSimRunner().Run(a).Run.Timeline,
            ev=>ev.Type==CombatEventType.AuraApplied && ev.AbilityKey=="wrack" &&
                ev.EffectKey=="wrack-vulnerability-effect");
        var d=WarlockSimRequest.CreatePreset("destruction"); d.CaptureTimeline=true;
        Assert.Contains(new WarlockSimRunner().Run(d).Run.Timeline,
            ev=>ev.AbilityKey=="incinerate" && ev.Type==CombatEventType.Damage);
    }
}
