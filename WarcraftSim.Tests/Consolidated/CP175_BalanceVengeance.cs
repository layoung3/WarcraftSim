using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Druid;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175BalanceVengeanceTests
{
    // Independent historical critical-damage BONUS oracle: 50% * (1 + 20% per rank).
    private static readonly decimal[] CriticalDamage = [150m,160m,170m,180m,190m,200m];
    private static readonly string[] Abilities = ["wrath","starfire","moonfire","insect-swarm","hurricane"];
    private static BalanceDruidSimRequest Request(string ability="wrath",int rank=5,decimal crit=100m) {
        var r=new BalanceDruidSimRequest {
            DurationSeconds=18m,TargetMaximumHealth=1000000000m,BaseSpellHitPercent=100m,
            SpellCritPercent=crit,MajorManaPotionCharges=0,UseOmenOfClarity=false,UseInnervate=false,
            StarfireOnlyWithEclipse=false,CustomPriority=[ability],AdditionalTargets=ability=="hurricane"?2:0,
            Talents=new() {BalancePointsSpent=16,RestorationPointsSpent=0,
                Ranks=new(StringComparer.OrdinalIgnoreCase) {["vengeance"]=rank,["insect-swarm"]=1}},
            Tuning=new() {WrathDamage=100m,StarfireDamage=100m,MoonfireDirectDamage=100m,
                MoonfireTickDamage=100m,InsectSwarmTickDamage=100m,HurricaneTickDamage=100m,ManaRegenPerSecond=0m}
        };
        r.Character.BaseStats.Values=new(StringComparer.Ordinal) {
            ["intellect"]=1000m,["spirit"]=0m,["spell-power"]=0m,["mp5"]=0m};return r;
    }
    private static BalanceDruidSimResponse Run(BalanceDruidSimRequest r)=>new BalanceDruidSimRunner().Run(r);
    private static CombatEvent[] Damage(BalanceDruidSimResponse r)=>r.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage&&e.SourceActorKey=="druid").ToArray();
    private static string Schedule(BalanceDruidSimResponse r)=>JsonSerializer.Serialize(r.Run.Timeline.Select(e=>new {
        e.TimeSeconds,e.Type,e.SourceActorKey,e.TargetActorKey,e.AbilityKey,e.EffectKey,
        e.ResourceKey,e.ResourceTotalAfter,e.ResultKey,e.IsCritical,e.EffectDeliveryType,
        ResourceAmount=e.Type==CombatEventType.ResourceChanged?e.Amount:null}));
    private static void Reporting(BalanceDruidSimResponse r) {
        var damage=Damage(r);Assert.Equal(damage.Sum(e=>e.Amount!.Value),r.DamageDone);
        Assert.Equal(r.DamageDone,r.Run.Summary.ActorSummaries["druid"].DamageDone);
        Assert.Equal(r.DamageDone,r.DamageByAbility.Values.Sum());
        foreach(var group in damage.GroupBy(e=>e.AbilityKey!))Assert.Equal(group.Sum(e=>e.Amount!.Value),r.DamageByAbility[group.Key]);
        foreach(var group in damage.GroupBy(e=>e.TargetActorKey!))Assert.Equal(group.Sum(e=>e.Amount!.Value),r.Run.Summary.ActorSummaries[group.Key].DamageTaken);
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)]
    public void ActualSevenEffectsFollowLiteralCriticalRankOracle(int rank) {
        var seen=new HashSet<string>();
        foreach(var ability in Abilities) {
            var r=Run(Request(ability,rank));var damage=Damage(r);Assert.NotEmpty(damage);
            Assert.All(damage,e=>{Assert.True(e.IsCritical);Assert.Equal(CriticalDamage[rank],e.RawAmount);Assert.Equal(e.RawAmount,e.Amount);seen.Add(e.EffectKey!);});
            Assert.Equal(damage.Select(e=>e.Amount),r.Run.Timeline.Where(e=>e.Type==CombatEventType.ThreatChanged).Select(e=>e.Amount));Reporting(r);
        }
        Assert.Equal(new[]{"hurricane-secondary-tick","hurricane-tick","insect-swarm-tick","moonfire-impact","moonfire-tick","starfire-impact","wrath-impact"},seen.Order().ToArray());
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)]
    public void NoncriticalCostsTimingAndSeededRollOutcomesDoNotDependOnRank(int rank) {
        foreach(var ability in Abilities)foreach(var chance in new[]{0m,37m,100m}) {
            var r=Request(ability,rank,chance);var result=Run(r);var control=Run(Request(ability,0,chance));
            Assert.Equal(Schedule(control),Schedule(result));Assert.Equal(control.EndingMana,result.EndingMana);
            Assert.All(Damage(result),e=>Assert.Equal(e.IsCritical?CriticalDamage[rank]:100m,e.Amount));
            Assert.Equal(control.OmenProcs,result.OmenProcs);Assert.Equal(control.NaturesGraceProcs,result.NaturesGraceProcs);Reporting(result);
        }
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void MissingZeroAndComparerDependentLookupRemainUnchanged(bool ignoreCase) {
        var r=Request(rank:0);r.Talents.Ranks=new(ignoreCase?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal);
        var absent=Run(r);r.Talents.Ranks["vengeance"]=0;Assert.Equal(absent.DamageDone,Run(r).DamageDone);
        r.Talents.Ranks.Remove("vengeance");r.Talents.Ranks["VENGEANCE"]=5;var before=JsonSerializer.Serialize(r);
        Assert.All(Damage(Run(r)),e=>Assert.Equal(ignoreCase?200m:150m,e.Amount));
        Assert.Equal(before,JsonSerializer.Serialize(r));Assert.Same(ignoreCase?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal,r.Talents.Ranks.Comparer);
    }
    [Theory][InlineData("wrath")][InlineData("starfire")][InlineData("moonfire")][InlineData("insect-swarm")][InlineData("hurricane")]
    public void NaturalistAndMoonfuryMultiplyOrdinaryDamageBeforeCriticalBonus(string ability) {
        var r=Request(ability);r.Talents.RestorationPointsSpent=5;r.Talents.Ranks["naturalist"]=5;r.Talents.Ranks["moonfury"]=5;
        Assert.All(Damage(Run(r)),e=>Assert.Equal(231m,e.RawAmount));r.SpellCritPercent=0m;
        var ordinary=Damage(Run(r));Assert.Contains(ordinary,e=>!e.IsCritical);
        Assert.All(ordinary,e=>Assert.Equal(e.IsCritical?231m:115.5m,e.RawAmount));
    }
    [Theory][InlineData("wrath")][InlineData("starfire")][InlineData("moonfire")][InlineData("insect-swarm")][InlineData("hurricane")]
    public void CustomZeroDamageAndNonEligibleThornsArePreserved(string ability) {
        var r=Request(ability);r.Tuning.WrathDamage=r.Tuning.StarfireDamage=r.Tuning.MoonfireDirectDamage=r.Tuning.MoonfireTickDamage=r.Tuning.InsectSwarmTickDamage=r.Tuning.HurricaneTickDamage=37m;
        Assert.All(Damage(Run(r)),e=>Assert.Equal(74m,e.RawAmount));
        r.Tuning.WrathDamage=r.Tuning.StarfireDamage=r.Tuning.MoonfireDirectDamage=r.Tuning.MoonfireTickDamage=r.Tuning.InsectSwarmTickDamage=r.Tuning.HurricaneTickDamage=0m;
        var zero=Run(r);Assert.Equal(0m,zero.DamageDone);Assert.DoesNotContain(Damage(zero),e=>e.AbilityKey=="hurricane-secondary");
        r.CustomPriority=["faerie-fire"];r.UseThorns=true;r.IncomingBossHitDamage=10m;
        var a=Run(r);r.Talents.Ranks["vengeance"]=0;var b=Run(r);Assert.Equal(a.DamageDone,b.DamageDone);Assert.All(Damage(a),e=>Assert.False(e.IsCritical));
    }
    [Theory][InlineData("wrath")][InlineData("starfire")][InlineData("moonfire")][InlineData("hurricane")]
    public void ForcedMissNeverCreatesDamageOrThreat(string ability) {
        var r=Request(ability);r.BaseSpellHitPercent=0m;var result=Run(r);
        Assert.Equal(0m,result.DamageDone);Assert.Contains(result.Run.Timeline,e=>e.ResultKey=="miss");
        Assert.DoesNotContain(result.Run.Timeline,e=>e.Type==CombatEventType.ThreatChanged);
    }
    [Fact]
    public void FiniteHealthClampsThreatAndCanChangeDeathTime() {
        var r=Request();r.TargetMaximumHealth=225m;r.Talents.RestorationPointsSpent=3;r.Talents.Ranks["subtlety"]=3;
        var result=Run(r);var damage=Damage(result);Assert.Equal(2,damage.Length);
        Assert.Equal(new[]{200m,25m},damage.Select(e=>e.Amount!.Value));Assert.All(damage,e=>Assert.Equal(200m,e.RawAmount));
        Assert.Equal(157.5m,result.Run.Timeline.Where(e=>e.Type==CombatEventType.ThreatChanged).Sum(e=>e.Amount));
        Assert.True(result.Run.Summary.ActorSummaries["target"].Died);Assert.Equal(4m,result.Run.Summary.ActorSummaries["target"].DeathTimeSeconds);Reporting(result);
        r.TargetMaximumHealth=50m;Assert.Equal(50m,Run(r).DamageDone);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task RepeatedAndConcurrentRequestsAndPreparationRemainIsolated(bool prepared) {
        var r=Request();if(prepared){r.Character.Race="tauren";r.UseProvisionalLevel60Baseline=true;}
        var before=JsonSerializer.Serialize(r);var preview=BalanceDruidSimRunner.Preview(r);var expected=Run(r);
        var runs=await Task.WhenAll(Enumerable.Range(0,4).Select(_=>Task.Run(()=>Run(r))));
        Assert.All(runs,a=>{Assert.Equal(expected.DamageDone,a.DamageDone);Assert.Equal(Schedule(expected),Schedule(a));Reporting(a);});
        Assert.Equal(before,JsonSerializer.Serialize(r));Assert.Equal(preview.SpellPower,expected.SpellPower);
        preview.EffectiveStats.Set("spell-power",99999m);Assert.Equal(expected.SpellPower,BalanceDruidSimRunner.Preview(r).SpellPower);
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)]
    public async Task StrictPartialAllocationUsesExact15PlusRankAndLiteralWrathOracle(int rank) {
        var r=Request(rank:rank);r.Character.Race="tauren";r.UseProvisionalLevel60Baseline=true;
        r.Talents=new() {BalancePointsSpent=15+rank,FeralPointsSpent=0,RestorationPointsSpent=0,
            Ranks=new(){["improved-wrath"]=5,["genesis"]=5,["improved-moonfire"]=2,["moonglow"]=3,["vengeance"]=rank}};
        var q=new ForeverLevel60SimulationRequest {ClassKey="druid",Specialization="balance",ItemizedTalentMode=true,ItemizedTalentRanks=new(r.Talents.Ranks),Balance=r};
        var before=JsonSerializer.Serialize(q);var ok=Assert.IsType<OkObjectResult>(await new ForeverLevel60SimulationController().Simulate(q,default));
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(ok.Value,new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal(ForeverHistoricalCasterTalentCatalog.SourceStatus,json.RootElement.GetProperty("talentSourceStatus").GetString());
        var hits=json.RootElement.GetProperty("sim").GetProperty("run").GetProperty("timeline").EnumerateArray().Where(e=>e.GetProperty("type").GetInt32()==6).ToArray();
        Assert.NotEmpty(hits);Assert.All(hits,e=>Assert.Equal(CriticalDamage[rank],e.GetProperty("rawAmount").GetDecimal()));Assert.Equal(before,JsonSerializer.Serialize(q));
        q.ItemizedTalentRanks["vengeance"]=(rank+1)%6;Assert.IsType<BadRequestObjectResult>(await new ForeverLevel60SimulationController().Simulate(q,default));
        q.ItemizedTalentRanks["vengeance"]=rank;r.Talents.BalancePointsSpent++;
        Assert.IsType<BadRequestObjectResult>(await new ForeverLevel60SimulationController().Simulate(q,default));
    }
    [Theory][InlineData("throughput")][InlineData("raid-sustain")][InlineData("deep-regen")]
    public void Cp138ProfilesScaleOnlyCriticalEventsAndKeepHighHealthSchedule(string build) {
        var r=BalancePreRaidProfileCatalog.Setup("throughput",build);r.CaptureTimeline=true;r.TargetMaximumHealth=1000000000m;
        Assert.Equal(5,r.Talents.Rank("vengeance"));var result=Run(r);r.Talents.Ranks["vengeance"]=0;var control=Run(r);
        var a=Damage(control);var b=Damage(result);Assert.Equal(a.Length,b.Length);Assert.Contains(b,e=>e.IsCritical);
        Assert.Equal(Schedule(control),Schedule(result));Assert.Equal(control.EndingMana,result.EndingMana);
        for(var i=0;i<a.Length;i++)Assert.Equal(a[i].IsCritical?a[i].RawAmount/1.5m*2m:a[i].RawAmount,b[i].RawAmount);
        Reporting(result);
    }
    [Fact]
    public void DefaultActuallyCritsAndRankAdmissionStaysBounded() {
        var r=new BalanceDruidSimRequest{CaptureTimeline=true,TargetMaximumHealth=1000000000m};Assert.Equal(5,r.Talents.Rank("vengeance"));Assert.Contains(Damage(Run(r)),e=>e.IsCritical);
        foreach(var rank in new[]{-1,6}){r.Talents.Ranks["vengeance"]=rank;Assert.Throws<ArgumentException>(()=>Run(r));}
    }
}
