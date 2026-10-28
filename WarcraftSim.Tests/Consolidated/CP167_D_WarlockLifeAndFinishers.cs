using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Warlock;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167DWarlockLifeAndFinishers
{
    private static WarlockSimRequest Req(string spec, params string[] priority)
    {
        var r=WarlockSimRequest.CreatePreset(spec);
        r.DurationSeconds=35m;
        r.CaptureTimeline=true;
        r.EnablePet=false;
        r.UseLifeTap=false;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.CustomPriority=priority.ToList();
        r.CombatItems.ManaRubyCharges=0;
        return r;
    }

    [Fact]
    public void LifeDrainsHaveCurrentForeverRanksAndTalentGates()
    {
        var talents=ForeverWarlockTalentBuild.Default("affliction");
        var book=ForeverWarlockDpsSpellbook.Create("affliction",talents,
            new ForeverWarlockTuning(),0m,180m);
        var drain=Assert.Single(book,s=>s.Key=="drain-life");
        Assert.Equal(300m,drain.ResourceCosts.Single().Amount);
        Assert.Equal(5m,drain.ChannelDurationSeconds);
        Assert.Equal(1m,drain.ChannelTickIntervalSeconds);
        var siphon=Assert.Single(book,s=>s.Key=="siphon-life");
        Assert.Equal(365m,siphon.ResourceCosts.Single().Amount);
        Assert.Equal(30m,siphon.Effects.Single().DurationSeconds);
        Assert.Equal(3m,siphon.Effects.Single().TickIntervalSeconds);

        talents.Ranks.Remove("siphon-life");
        book=ForeverWarlockDpsSpellbook.Create("affliction",talents,
            new ForeverWarlockTuning(),0m,180m);
        Assert.DoesNotContain(book,s=>s.Key=="siphon-life");
    }

    [Theory]
    [InlineData("drain-life")]
    [InlineData("siphon-life")]
    public void LifeDrainsActuallyDamageEnemyAndHealFromResolvedDamage(string ability)
    {
        var req=Req("affliction",ability);
        var run=new WarlockSimRunner().Run(req);
        var damage=run.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey==ability && e.Amount is >0m).ToList();
        var healing=run.Run.Timeline.Where(e=>e.Type==CombatEventType.Healing &&
            e.AbilityKey==ability && e.RawAmount is >0m).ToList();
        Assert.NotEmpty(damage);
        Assert.Equal(damage.Count,healing.Count);
        Assert.All(healing,e=>Assert.True(e.RawAmount is >0m && e.Amount>=0m));
        Assert.InRange(Math.Abs(run.WarlockDamage-
            run.DamageByAbility.Where(x=>!x.Key.StartsWith("pet:",StringComparison.OrdinalIgnoreCase) &&
                !x.Key.StartsWith("replacement:",StringComparison.OrdinalIgnoreCase))
                .Sum(x=>x.Value)),0m,.000001m);
    }

    [Fact]
    public void ShadowburnRequiresTalentAndConsumesFiniteShards()
    {
        var req=Req("destruction","shadowburn");
        req.DurationSeconds=52m;
        req.StartingSoulShards=2;
        req.TargetYieldsSoulShard=false;
        var run=new WarlockSimRunner().Run(req);
        Assert.Equal(2,run.Run.Timeline.Count(e=>
            e.Type==CombatEventType.AbilityCastCompleted && e.AbilityKey=="shadowburn"));
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.ResourceKey=="soul-shard" && e.Amount<0m);
        var talents=ForeverWarlockTalentBuild.Default("destruction");
        talents.Ranks.Remove("shadowburn");
        var book=ForeverWarlockDpsSpellbook.Create("destruction",talents,
            new ForeverWarlockTuning(),200m,180m);
        Assert.DoesNotContain(book,s=>s.Key=="shadowburn");
    }

    [Fact]
    public void SearingPainGeneratesMoreThreatThanItsEffectiveDamage()
    {
        var req=Req("demonology","searing-pain");
        req.DurationSeconds=15m;
        var run=new WarlockSimRunner().Run(req);
        var damage=run.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="searing-pain" && e.Amount is >0m).Sum(e=>e.Amount!.Value);
        var threat=run.Run.Timeline.Where(e=>e.Type==CombatEventType.ThreatChanged &&
            e.SourceActorKey=="warlock" && e.TargetActorKey=="target").ToList();
        Assert.True(damage>0m);
        Assert.NotEmpty(threat);
        Assert.Equal(2m*damage,threat.Sum(e=>e.Amount.GetValueOrDefault()));
    }
}
