using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Warlock;
using System.Text.Json;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp164WarlockDemonCompanionTests
{
    private static WarlockSimRequest Request(string demon, string spec="demonology")
    {
        var request=WarlockSimRequest.CreatePreset(spec);
        request.PetType=demon;
        request.DurationSeconds=90m;
        request.Seed=164001;
        request.CaptureTimeline=true;
        if(ForeverWarlockDemons.Get(demon).IsTemporary)
        {
            request.AllowTemporaryDemon=true;
            request.AllowDoomguardRitual=true;
        }
        return request;
    }
    [Theory]
    [InlineData("imp","demon-firebolt")]
    [InlineData("voidwalker","pet-melee")]
    [InlineData("succubus","demon-lash-of-pain")]
    [InlineData("incubus","demon-lash-of-pain")]
    [InlineData("felhunter","demon-shadow-bite")]
    [InlineData("infernal","demon-immolation")]
    [InlineData("doomguard","demon-doom-bolt")]
    public void EachDemonExecutesOwnCombatArchetype(string demon,string ability)
    {
        var result=new WarlockSimRunner().Run(Request(demon));
        var pet=result.Run.Summary.ActorSummaries["warlock-pet"];
        Assert.True(pet.DamageDone>0m,demon+" no damage");
        Assert.True(ability=="pet-melee" || pet.Abilities.ContainsKey(ability),
            demon+" missing "+ability);
        Assert.Contains(result.Run.Timeline,e=>e.SourceActorKey=="warlock-pet" &&
            e.AbilityKey==ability && (e.Type==CombatEventType.Damage ||
                e.Type==CombatEventType.AbilityCastCompleted));
        Assert.InRange(Math.Abs(result.DamageDone-result.WarlockDamage-result.PetDamage),
            0m,.0001m);
        Assert.InRange(Math.Abs(result.PetDamage-result.DemonMeleeDamage-
            result.DemonSpellDamage),0m,.0001m);
    }
    [Fact]
    public void ImpHasSpellDamageAndNoGenericMelee()
    {
        var result=new WarlockSimRunner().Run(Request("imp"));
        Assert.Equal(0m,result.DemonMeleeDamage);
        Assert.True(result.DemonSpellDamage>0m);
        Assert.DoesNotContain(result.Run.Timeline,e=>e.SourceActorKey=="warlock-pet" &&
            e.AbilityKey=="pet-melee" && e.Type==CombatEventType.Damage);
        Assert.Contains(result.Run.Timeline,e=>e.SourceActorKey=="warlock-pet" &&
            e.AbilityKey=="demon-firebolt" && e.SchoolKey=="fire" &&
            e.Type==CombatEventType.Damage && e.Amount>0m);
    }
    [Fact]
    public void VoidwalkerGeneratesDistinctThreatBeyondDamage()
    {
        var result=new WarlockSimRunner().Run(Request("voidwalker"));
        Assert.True(result.DemonThreat>result.PetDamage);
        Assert.Contains(result.Run.Timeline,e=>e.SourceActorKey=="warlock-pet" &&
            e.AbilityKey=="demon-torment" && e.Type==CombatEventType.ThreatChanged &&
            e.Amount>0m);
    }
    [Fact]
    public void FelhunterAndImpSpendManaUsingSharedResourceExecution()
    {
        foreach(var demon in new[]{"imp","felhunter"})
        {
            var result=new WarlockSimRunner().Run(Request(demon));
            Assert.Contains(result.Run.Timeline,e=>e.SourceActorKey=="warlock-pet" &&
                e.Type==CombatEventType.ResourceChanged && e.ResourceKey=="mana" &&
                e.Amount<0m);
        }
    }
    [Fact]
    public void DemonDeathAndSacrificeStopActionsWithoutDoubleCounting()
    {
        foreach(var sacrifice in new[]{false,true})
        {
            var req=Request("succubus");
            if(sacrifice) req.DemonSacrificeAtSeconds=20m;
            else req.DemonDeathAtSeconds=20m;
            var result=new WarlockSimRunner().Run(req);
            Assert.Equal(sacrifice,result.DemonSacrificed);
            Assert.Equal(!sacrifice,result.DemonDied);
            Assert.InRange(result.DemonUptimeSeconds,18m,21m);
            Assert.DoesNotContain(result.Run.Timeline,e=>e.SourceActorKey=="warlock-pet" &&
                e.Type==CombatEventType.Damage && e.TimeSeconds>22m);
        }
    }
    [Fact]
    public void TemporarySummonsExpireButCannotBeSelectedWithoutPermission()
    {
        var forbidden=Request("doomguard");
        forbidden.AllowDoomguardRitual=false;
        Assert.Throws<ArgumentException>(()=>new WarlockSimRunner().Run(forbidden));
        var req=Request("infernal");
        req.DurationSeconds=100m;
        req.DemonTuning.TemporaryDemonDurationSeconds=20m;
        var result=new WarlockSimRunner().Run(req);
        Assert.InRange(result.DemonUptimeSeconds,19m,21m);
    }
    [Fact]
    public void PetDisabledLeavesAllWarlockSpecializationsFunctional()
    {
        foreach(var spec in new[]{"affliction","demonology","destruction"})
        {
            var req=Request("imp",spec);
            req.EnablePet=false;
            var result=new WarlockSimRunner().Run(req);
            Assert.Equal(0m,result.PetDamage);
            Assert.True(result.WarlockDamage>0m);
            Assert.False(result.Run.Summary.ActorSummaries.ContainsKey("warlock-pet"));
        }
    }
    [Fact]
    public void PresetAndResponseSerializeWithDemonFields()
    {
        var req=Request("felhunter","affliction");
        var roundTrip=JsonSerializer.Deserialize<WarlockSimRequest>(JsonSerializer.Serialize(req))!;
        Assert.Equal("felhunter",roundTrip.PetType);
        var result=new WarlockSimRunner().Run(roundTrip);
        var json=JsonSerializer.Serialize(result);
        Assert.Contains("DemonSpellDamage",json);
        Assert.Contains("DemonThreat",json);
    }
    [Fact]
    public void BeastMasteryHunterStillUsesIndependentCompanionFoundation()
    {
        var hunter=new BeastMasteryHunterSimRunner().Run(new BeastMasteryHunterSimRequest{
            PetFamily="bear",AdditionalTargets=2,DurationSeconds=45m,Seed=163001
        });
        Assert.True(hunter.PetAutoDamage>0m);
        Assert.Equal(hunter.HunterDamage+hunter.PetDamage+hunter.HawkDamage,hunter.DamageDone);
    }
    [Fact]
    public void ReplacementUsesSecondIndependentActorAndPreservesCombinedDamage()
    {
        var req=Request("succubus");
        req.DemonReplacementAtSeconds=30m;
        req.ReplacementDemonType="imp";
        var result=new WarlockSimRunner().Run(req);
        Assert.True(result.ReplacementDemonDamage>0m);
        Assert.Equal("imp",result.ReplacementDemonType);
        Assert.True(result.Run.Summary.ActorSummaries["warlock-replacement"].DamageDone>0m);
        Assert.InRange(Math.Abs(result.WarlockDamage+result.PetDamage-result.DamageDone),0m,.001m);
        Assert.True(result.DemonSpellDamage>0m);
    }

    [Fact]
    public void DemonicPactRetainsDifferentDemonSacrificeButNotSameFamily()
    {
        foreach(var replacement in new[]{"imp","succubus"})
        {
            var req=Request("succubus");
            req.DemonSacrificeAtSeconds=20m;
            req.DemonReplacementAtSeconds=30m;
            req.ReplacementDemonType=replacement;
            var result=new WarlockSimRunner().Run(req);
            var active=result.Run.Summary.ActorSummaries.ContainsKey("warlock-replacement");
            Assert.True(active);
            var ownerAuras=result.Run.Timeline.Where(e=>e.AbilityKey=="demonic-sacrifice" &&
                e.Type==CombatEventType.AuraApplied).ToArray();
            Assert.NotEmpty(ownerAuras);
            if(replacement=="succubus")
                Assert.Contains(result.Run.Timeline,e=>e.AbilityKey=="demonic-sacrifice" &&
                    e.Type==CombatEventType.AuraApplied);
            Assert.True(result.ReplacementDemonDamage>0m);
        }
    }

    [Fact]
    public void SacrificedVoidwalkerProducesActualManaRegeneration()
    {
        var req=Request("voidwalker");
        req.StartingManaPercent=10m;
        req.DemonSacrificeAtSeconds=20m;
        var result=new WarlockSimRunner().Run(req);
        Assert.True(result.DemonSacrificed);
        Assert.Contains(result.Run.Timeline,e=>e.SourceActorKey=="warlock" &&
            e.AbilityKey=="demonic-sacrifice" &&
            e.Type==CombatEventType.ResourceChanged && e.Amount>0m);
    }

    [Fact]
    public void ImprovedImpAndSayaadActuallyIncreaseTheirSpellDamage()
    {
        foreach(var pair in new[]{("imp","improved-imp"),("succubus","improved-sayaad")})
        {
            var baseline=Request(pair.Item1);
            var improved=Request(pair.Item1);
            improved.Talents.Ranks[pair.Item2]=3;
            var without=new WarlockSimRunner().Run(baseline);
            var with=new WarlockSimRunner().Run(improved);
            Assert.True(with.DemonSpellDamage>without.DemonSpellDamage,
                pair.Item1+" talent had no spell effect");
        }
    }

    [Fact]
    public void DemonManaRegenerationIsReported()
    {
        var req=Request("imp");
        var result=new WarlockSimRunner().Run(req);
        Assert.Contains(result.Run.Timeline,e=>e.SourceActorKey=="warlock-pet" &&
            e.AbilityKey=="demon-mana-regeneration" &&
            e.Type==CombatEventType.ResourceChanged && e.Amount>0m);
    }
}
