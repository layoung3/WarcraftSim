using System;
using System.Linq;
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Warlock;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp168DestructionSpellbookAndCastGates
{
    private static WarlockSimRequest Request(params string[] priority)
    {
        var r=WarlockSimRequest.CreatePreset("destruction");
        r.EnablePet=false;
        r.PrePullSacrificedDemon=null;
        r.UseLifeTap=false;
        r.TargetMaximumHealth=1000000m;
        r.DurationSeconds=18m;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.CaptureTimeline=true;
        r.CustomPriority=priority.ToList();
        return r;
    }

    [Theory]
    [InlineData("affliction")]
    [InlineData("demonology")]
    [InlineData("destruction")]
    public void SoulFireIsBaselineAllSpecsAndCostsOneActualShard(string spec)
    {
        var talents=ForeverWarlockTalentBuild.Default(spec);
        var spell=Assert.Single(ForeverWarlockDpsSpellbook.Create(spec,talents,
            new ForeverWarlockTuning(),0m,0m),s=>s.Key=="soul-fire");
        Assert.Equal(335m,spell.ResourceCosts.Single(c=>c.ResourceKey=="mana").Amount);
        Assert.Equal(1m,spell.ResourceCosts.Single(c=>c.ResourceKey=="soul-shard").Amount);
        Assert.Equal(spec=="demonology"?6m:60m,spell.CooldownSeconds);
        var r=WarlockSimRequest.CreatePreset(spec);
        r.EnablePet=false;
        r.PrePullSacrificedDemon=null;
        r.UseLifeTap=false;
        r.TargetMaximumHealth=1000000m;
        r.DurationSeconds=9m;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.CaptureTimeline=true;
        r.CustomPriority=["soul-fire"];
        var result=new WarlockSimRunner().Run(r);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="soul-fire" && e.Amount>0m);
        Assert.Single(result.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="soul-fire" && e.ResourceKey=="soul-shard" && e.Amount==-1m);
    }

    [Fact]
    public void RankSixConflagrateRequiresOwnImmolateBeforeSpendingMana()
    {
        var s=Assert.Single(ForeverWarlockDpsSpellbook.Create("destruction",
            ForeverWarlockTalentBuild.Default("destruction"),
            new ForeverWarlockTuning(),0m,0m),x=>x.Key=="conflagrate");
        Assert.Equal(255m,s.ResourceCosts.Single().Amount);
        Assert.Equal(282m,s.Effects.Single().MinimumValue);
        Assert.Equal(10m,s.CooldownSeconds);
        Assert.Contains("immolate",s.RequiredAnyTargetAuraKeys);
        var noImmolate=new WarlockSimRunner().Run(Request("conflagrate"));
        Assert.DoesNotContain(noImmolate.Run.Timeline,e=>e.AbilityKey=="conflagrate" &&
            e.Type is (CombatEventType.AbilityCastStarted or CombatEventType.Damage));
        var with=new WarlockSimRunner().Run(Request("immolate","conflagrate","shadow-bolt"));
        Assert.Contains(with.Run.Timeline,e=>e.AbilityKey=="conflagrate" &&
            e.Type==CombatEventType.Damage && e.Amount>0m);
        Assert.Contains(with.Run.Timeline,e=>e.AbilityKey=="conflagrate" &&
            e.Type==CombatEventType.ResourceChanged && e.ResourceKey=="mana" &&
            e.Amount==-255m);
    }

    [Fact]
    public void PyroclasmAndDestructiveReachHaveCurrentRanksWithoutPhantomDps()
    {
        var t=ForeverWarlockTalentBuild.Default("destruction");
        t.Ranks["pyroclasm"]=2;
        t.Ranks["intensity"]=3;
        t.Ranks["destructive-reach"]=2;
        t.Validate(60,"destruction");
        t.Ranks["pyroclasm"]=3;
        Assert.Throws<ArgumentException>(()=>t.Validate(60,"destruction"));
        t.Ranks["pyroclasm"]=2;
        t.Ranks["intensity"]=2;
        Assert.Throws<ArgumentException>(()=>t.Validate(60,"destruction"));
        t.Ranks["intensity"]=3;
        t.Ranks["destructive-reach"]=3;
        Assert.Throws<ArgumentException>(()=>t.Validate(60,"destruction"));
    }

    [Fact]
    public void NewHavocTargetMustExistAndRequestRoundTrips()
    {
        var request=Request("bane-of-havoc","shadow-bolt");
        request.AdditionalTargets=1;
        request.BaneOfHavocTargetKey="target-1";
        request.UseBaneOfHavoc=true;
        var copy=JsonSerializer.Deserialize<WarlockSimRequest>(
            JsonSerializer.Serialize(request));
        Assert.NotNull(copy);
        Assert.Equal("target-1",copy.BaneOfHavocTargetKey);
        request.BaneOfHavocTargetKey="target-9";
        Assert.Throws<ArgumentException>(()=>new WarlockSimRunner().Run(request));
    }
}
