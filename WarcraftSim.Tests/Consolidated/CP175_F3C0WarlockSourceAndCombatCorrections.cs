using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Warlock;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>
/// Source-backed November 2/3 Forever Warlock changes. Announced release
/// values are provisional until the live combat server is verified.
/// </summary>
public sealed class Cp175F3C0WarlockSourceAndCombatCorrections
{
    private static WarlockSimRequest Owner(string spec,string spell)
    {
        var r=WarlockSimRequest.CreatePreset(spec);
        r.EnablePet=false;
        r.PrePullSacrificedDemon=null;
        r.UseLifeTap=false;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.TargetMaximumHealth=1_000_000m;
        r.DurationSeconds=12m;
        r.CustomPriority=[spell];
        r.Tuning.ManaRegenerationPerSecond=100m;
        return r;
    }

    [Theory]
    [InlineData(0,86)]
    [InlineData(100,114.6)]
    [InlineData(200,143.2)]
    public void AnnouncedWrackUsesEightySixPerTickAndPointTwoEightSixSpellPower(decimal sp,double expected)
    {
        var r=Owner("affliction","wrack");
        r.Talents.Ranks["shadow-mastery"]=0;
        r.Talents.Ranks["malevolence"]=0;
        r.Talents.Ranks["pandemic"]=0;
        r.Talents.Ranks["soul-siphon"]=0;
        var effect=ForeverWarlockDpsSpellbook.Create(r.Specialization,r.Talents,r.Tuning,sp,0m)
            .Single(x=>x.Key=="wrack").Effects.Single(x=>x.Key=="wrack-tick");
        Assert.Equal((decimal)expected,effect.MinimumValue);
        Assert.Equal((decimal)expected,effect.MaximumValue);
        r.Character.BaseStats.Values["spell-power"]=sp;
        r.DurationSeconds=7m;
        var run=new WarlockSimRunner().Run(r);
        var ticks=run.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="wrack" && e.IsChannelTick && e.Amount is >0m)
            .OrderBy(e=>e.TimeSeconds).ToArray();
        Assert.True(ticks.Length>=5,$"Expected actual Wrack channel ticks at {sp} SP.");
        Assert.Equal((decimal)expected,ticks[0].Amount);
        Assert.True(ticks[1].TimeSeconds>ticks[0].TimeSeconds);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ImprovedDrainsDoesNotFalselyIncreaseWrack(int ranks)
    {
        var r=Owner("affliction","wrack");
        r.Talents.Ranks["improved-drains"]=ranks;
        r.Talents.Ranks["shadow-mastery"]=0;
        r.Talents.Ranks["soul-siphon"]=0;
        r.Talents.Ranks["pandemic"]=0;
        r.Talents.Ranks["malevolence"]=0;
        r.Character.BaseStats.Values["spell-power"]=0m;
        var spell=ForeverWarlockDpsSpellbook.Create(r.Specialization,r.Talents,r.Tuning,0m,0m)
            .Single(x=>x.Key=="wrack");
        Assert.Equal(86m,spell.Effects.Single(x=>x.Key=="wrack-tick").MinimumValue);
        r.DurationSeconds=7m;
        var sim=new WarlockSimRunner().Run(r);
        Assert.Contains(sim.Run.Timeline,e=>e.AbilityKey=="wrack" &&
            e.Type==CombatEventType.Damage && e.IsChannelTick && e.Amount==86m);
        Assert.DoesNotContain(sim.Run.Timeline,e=>e.AbilityKey=="drain-mana");
    }

    [Theory]
    [InlineData(0,1)]
    [InlineData(1,1.04)]
    [InlineData(2,1.08)]
    [InlineData(3,1.12)]
    [InlineData(5,1.20)]
    public void AnnouncedShadowAndFlameScalesActualFireDamageAfterLandedShadowburn(
        int ranks,double expected)
    {
        WarlockSimRequest Setup(int chosen)
        {
            var r=Owner("destruction","immolate");
            r.Talents.Ranks["shadow-and-flame"]=chosen;
            r.Talents.Ranks["agonizing-flames"]=0;
            r.Talents.Ranks["cataclysm"]=0;
            r.Talents.Ranks["ruin"]=0;
            r.Talents.Ranks["aftermath"]=0;
            r.StartingSoulShards=4;
            r.CustomPriority=["shadowburn","immolate"];
            return r;
        }
        decimal FireDamage(int selected)
        {
            var sim=new WarlockSimRunner().Run(Setup(selected));
            Assert.Contains(sim.Run.Timeline,e=>e.AbilityKey=="shadowburn" &&
                e.Type==CombatEventType.Damage && e.Amount is >0m);
            return sim.Run.Timeline.First(e=>e.AbilityKey=="immolate" &&
                e.Type==CombatEventType.Damage && !e.IsPeriodic && e.Amount is >0m).Amount!.Value;
        }
        var baseline=FireDamage(0);
        var talented=FireDamage(ranks);
        Assert.Equal(baseline*(decimal)expected,talented);
    }

    [Theory]
    [InlineData("imp")]
    [InlineData("voidwalker")]
    [InlineData("succubus")]
    [InlineData("incubus")]
    [InlineData("felhunter")]
    [InlineData("infernal")]
    [InlineData("doomguard")]
    public void EverySummonedDemonInheritsTwoHealthPerEffectiveOwnerStamina(string family)
    {
        decimal MaxHealth(int stamina,int embraceRank)
        {
            var r=WarlockSimRequest.CreatePreset("demonology");
            r.PetType=family;r.PrePullSacrificedDemon=null;
            r.AllowTemporaryDemon=true;r.AllowDoomguardRitual=true;
            r.EnablePet=true;r.DurationSeconds=4m;
            r.UseLifeTap=false;
            r.Talents.Ranks["fel-vitality"]=0;
            r.Talents.Ranks["demonic-embrace"]=embraceRank;
            r.Character.BaseStats.Values["stamina"]=stamina;
            r.CustomPriority=["shadow-bolt"];
            var run=new WarlockSimRunner().Run(r);
            Assert.Contains(run.Run.Timeline,e=>e.SourceActorKey=="warlock-pet" &&
                e.Type==CombatEventType.Damage && e.Amount is >0m);
            return run.Run.CustomMetrics["warlock-pet-maximum-health-at-pull"];
        }
        var baseHp=MaxHealth(0,0);
        var after=MaxHealth(100,0);
        var multiplier=ForeverWarlockDemons.Get(family).HealthMultiplier;
        Assert.Equal(baseHp+200m*multiplier,after);
        var embrace=MaxHealth(100,5);
        Assert.Equal(after+30m*multiplier,embrace);
    }

    [Fact]
    public void CurrentWarlock70753CoverageHasFiftyTwoTalentsAndEveryTypedRankMapped()
    {
        var rows=Warlock70753TalentCoverageCatalog.Talents;
        Assert.Equal(52,rows.Count);
        Assert.Equal(17,rows.Count(x=>x.Tree=="affliction"));
        Assert.Equal(19,rows.Count(x=>x.Tree=="demonology"));
        Assert.Equal(16,rows.Count(x=>x.Tree=="destruction"));
        Assert.Equal(32,rows.Count(x=>x.SimulationStatus=="fully-modeled"));
        Assert.Equal(16,rows.Count(x=>x.SimulationStatus=="partially-modeled"));
        Assert.Equal(4,rows.Count(x=>x.SimulationStatus=="not-modeled"));
        Assert.Equal(52,rows.Select(x=>x.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        foreach(var t in ForeverWarlockTalentBuild.SupportedTalentMetadata)
        {
            var row=Assert.Single(rows,x=>x.Key==t.Key);
            Assert.Equal(t.Tree,row.Tree);
            Assert.Equal(t.MaxRank,row.MaxRank);
        }
        Assert.Equal("not-modeled",Warlock70753TalentCoverageCatalog.Find("curse-of-exhaustion")!.SimulationStatus);
    }

    [Fact]
    public void PublicWarlockCatalogSeparatesProvisionalConfidenceFromActualMechanics()
    {
        var result=Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(
            new WarcraftSim.Api.Controllers.ForeverSoloTalentController().Catalog("warlock","affliction"));
        using var json=System.Text.Json.JsonDocument.Parse(
            System.Text.Json.JsonSerializer.Serialize(result.Value,
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));
        var entries=json.RootElement.GetProperty("talents").EnumerateArray().ToArray();
        Assert.Equal(52,entries.Length);
        var curse=Assert.Single(entries,x=>
            x.GetProperty("key").GetString()=="curse-of-exhaustion");
        Assert.Equal("not-implemented",curse.GetProperty("implementationStatus").GetString());
        Assert.Equal("provisional",curse.GetProperty("dataConfidence").GetString());
        Assert.Equal("1.60.1.70753",curse.GetProperty("sourceBuild").GetString());
        Assert.Equal("C",curse.GetProperty("gapCategories").GetString());
        var wrack=Assert.Single(entries,x=>x.GetProperty("key").GetString()=="wrack");
        Assert.Equal("fully-implemented",wrack.GetProperty("implementationStatus").GetString());
        Assert.Equal("provisional",wrack.GetProperty("dataConfidence").GetString());
    }

    [Fact]
    public void MissingCurseOfExhaustionIsVisibleAsTalentButCannotPretendToBeAnExecutableSpell()
    {
        var talents=ForeverWarlockTalentBuild.Default("affliction");
        talents.Ranks["curse-of-exhaustion"]=1;
        talents.Validate(60,"affliction");
        talents.Ranks["curse-of-exhaustion"]=2;
        Assert.Throws<ArgumentException>(()=>talents.Validate(60,"affliction"));
        talents.Ranks["curse-of-exhaustion"]=0;
        var request=Owner("affliction","curse-of-exhaustion");
        Assert.Throws<ArgumentException>(()=>WarlockAdaptiveRotation.Build(request));
    }
}
