using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Druid;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175BalanceNaturalistTests
{
    private static readonly decimal[] RankDamage = [100m,101m,102m,103m,104m,105m];
    private static ForeverBalanceDruidTalents Talents(int rank,int moonfury=0,bool ignoreCase=true) => new() {
        BalancePointsSpent=16,RestorationPointsSpent=10,
        Ranks=new(ignoreCase?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal) {
            ["naturalist"]=rank,["moonfury"]=moonfury,["insect-swarm"]=1 }
    };
    private static ForeverBalanceDruidTuning Tuning(decimal damage=100m) => new() {
        WrathDamage=damage,StarfireDamage=damage,MoonfireDirectDamage=damage,
        MoonfireTickDamage=damage,InsectSwarmTickDamage=damage,HurricaneTickDamage=damage,
        ManaRegenPerSecond=0m
    };
    private static BalanceDruidSimRequest Request(string ability,int rank=5,int moonfury=0) {
        var r=new BalanceDruidSimRequest {
            DurationSeconds=18m,TargetMaximumHealth=1000000000m,
            BaseSpellHitPercent=100m,SpellCritPercent=0m,
            MajorManaPotionCharges=0,UseOmenOfClarity=false,UseInnervate=false,
            StarfireOnlyWithEclipse=false,CustomPriority=[ability],
            Talents=Talents(rank,moonfury),Tuning=Tuning()
        };
        r.Character.BaseStats.Values=new(StringComparer.Ordinal) {
            ["intellect"]=1000m,["spirit"]=0m,["spell-power"]=0m,["mp5"]=0m };
        return r;
    }
    private static BalanceDruidSimResponse Run(BalanceDruidSimRequest r)=>new BalanceDruidSimRunner().Run(r);
    private static AbilityEffectDefinition[] DamageEffects(ForeverBalanceDruidTalents t,decimal sp=0m,
        ForeverBalanceDruidTuning? tuning=null)=>ForeverBalanceDruidSpellbook.Create(t,tuning??Tuning(),sp)
            .SelectMany(a=>a.Effects).Where(e=>e.Key!="faerie-fire-armor").ToArray();
    private static object[] Schedule(BalanceDruidSimResponse r)=>r.Run.Timeline.Select(e=>(object)new {
        e.TimeSeconds,e.Type,e.SourceActorKey,e.TargetActorKey,e.AbilityKey,e.EffectKey,
        e.ResourceKey,e.ResourceTotalAfter,e.ResultKey,e.IsCritical,e.EffectDeliveryType,
        ResourceAmount=e.Type==CombatEventType.ResourceChanged?e.Amount:null
    }).ToArray();

    [Theory]
    [InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)]
    public void SixComponentsFollowIndependentOnePercentRankOracle(int rank) {
        foreach(var ignoreCase in new[]{false,true}) {
            var effects=DamageEffects(Talents(rank,ignoreCase:ignoreCase));
            Assert.Equal(6,effects.Length);
            Assert.Equal(new[]{"hurricane-tick","insect-swarm-tick","moonfire-impact","moonfire-tick","starfire-impact","wrath-impact"},effects.Select(e=>e.Key).Order().ToArray());
            Assert.All(effects,e=>{Assert.Equal(RankDamage[rank],e.MinimumValue);Assert.Equal(e.MinimumValue,e.MaximumValue);});
        }
    }
    [Theory]
    [InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)]
    public void MoonfuryIsMultiplicativeAtEveryNaturalistRank(int rank) {
        decimal[] expected=[110m,111.1m,112.2m,113.3m,114.4m,115.5m];
        Assert.All(DamageEffects(Talents(rank,5)),e=>Assert.Equal(expected[rank],e.MinimumValue));
    }
    [Fact]
    public void RealWrathRankAndSpellPowerUseTheSameMultiplier() {
        var effects=DamageEffects(Talents(5,5),0m,new());
        Assert.Equal(105.105m,Assert.Single(effects,e=>e.Key=="wrath-impact").MinimumValue);
        var powered=DamageEffects(Talents(5,5),210m,new());
        Assert.Equal(243.3585m,Assert.Single(powered,e=>e.Key=="wrath-impact").MinimumValue);
        Assert.Equal(682.605m,Assert.Single(powered,e=>e.Key=="starfire-impact").MinimumValue);
        Assert.Equal(192.3075m,Assert.Single(powered,e=>e.Key=="moonfire-impact").MinimumValue);
        Assert.Equal(48.8565m,Assert.Single(powered,e=>e.Key=="moonfire-tick").MinimumValue);
        Assert.Equal(74.1279m,Assert.Single(powered,e=>e.Key=="insect-swarm-tick").MinimumValue);
        Assert.Equal(210.5565m,Assert.Single(powered,e=>e.Key=="hurricane-tick").MinimumValue);
    }
    [Fact]
    public void ZeroRankKeepsExistingDecimalSerialization() {
        Assert.Equal("110.0000",JsonSerializer.Serialize(DamageEffects(Talents(0,5))[0].MinimumValue));
        var r=Request("faerie-fire",0);r.UseThorns=true;r.IncomingBossHitDamage=10m;
        Assert.Equal("45.00",JsonSerializer.Serialize(Run(r).Run.Timeline.First(e=>e.AbilityKey=="thorns"&&e.Type==CombatEventType.Damage).RawAmount));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void AbsenceZeroAndOriginalRankComparerArePreserved(bool ignoreCase) {
        var t=Talents(0,5,ignoreCase);var zero=JsonSerializer.Serialize(ForeverBalanceDruidSpellbook.Create(t,Tuning(),0m));
        t.Ranks.Remove("naturalist");Assert.Equal(zero,JsonSerializer.Serialize(ForeverBalanceDruidSpellbook.Create(t,Tuning(),0m)));
        t.Ranks["NATURALIST"]=5;
        Assert.All(DamageEffects(t),e=>Assert.Equal(ignoreCase?115.5m:110m,e.MinimumValue));
        var r=Request("wrath");r.Talents=t;var before=JsonSerializer.Serialize(r);
        Assert.Equal(ignoreCase?115.5m:110m,Run(r).Run.Timeline.First(e=>e.Type==CombatEventType.Damage).RawAmount);
        Assert.Equal(before,JsonSerializer.Serialize(r));Assert.Same(ignoreCase?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal,r.Talents.Ranks.Comparer);
    }
    [Theory]
    [InlineData("wrath",120,2)][InlineData("starfire",340,3.5)]
    [InlineData("moonfire",375,1)][InlineData("insect-swarm",160,1)][InlineData("hurricane",850,0)]
    public void ActualDamageManaDebitTimingAndThreatFollowCorrectedEffects(string ability,int cost,double firstCast) {
        var r=Request(ability);r.AdditionalTargets=ability=="hurricane"?2:0;
        var actual=Run(r);var zero=Request(ability,0);zero.AdditionalTargets=r.AdditionalTargets;var control=Run(zero);
        var damage=actual.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage&&e.SourceActorKey=="druid").ToArray();
        Assert.NotEmpty(damage);Assert.All(damage,e=>{Assert.Equal(e.IsCritical?157.5m:105m,e.RawAmount);Assert.Equal(e.RawAmount,e.Amount);});
        if(ability=="hurricane") Assert.Equal(new[]{"target","target-1","target-2"},damage.Select(e=>e.TargetActorKey).Distinct().Order());
        var spends=actual.Run.Timeline.Where(e=>e.Type==CombatEventType.ResourceChanged&&e.ResourceKey=="mana"&&e.Amount<0).ToArray();
        Assert.NotEmpty(spends);Assert.All(spends,e=>Assert.Equal(-(decimal)cost,e.Amount));
        Assert.Equal(actual.MaximumMana+spends.Sum(e=>e.Amount!.Value),actual.EndingMana);
        if(ability!="hurricane")Assert.Equal((decimal)firstCast,actual.Run.Timeline.First(e=>e.Type==CombatEventType.AbilityCastCompleted&&e.AbilityKey==ability).TimeSeconds);
        Assert.Equal(JsonSerializer.Serialize(Schedule(control)),JsonSerializer.Serialize(Schedule(actual)));
        var threats=actual.Run.Timeline.Where(e=>e.Type==CombatEventType.ThreatChanged).ToArray();
        Assert.Equal(damage.Length,threats.Length);Assert.Equal(damage.Select(e=>e.Amount),threats.Select(e=>e.Amount));
        Assert.Equal(damage.Sum(e=>e.Amount!.Value),actual.DamageDone);
    }
    [Theory][InlineData("wrath")][InlineData("starfire")][InlineData("moonfire")][InlineData("insect-swarm")][InlineData("hurricane")]
    public void ForcedCriticalAndZeroCustomDamageRetainExistingResolution(string ability) {
        var r=Request(ability);r.SpellCritPercent=100m;r.AdditionalTargets=ability=="hurricane"?2:0;
        var events=Run(r).Run.Timeline.Where(e=>e.Type==CombatEventType.Damage&&e.Amount>0).ToArray();
        Assert.NotEmpty(events);Assert.All(events,e=>{Assert.True(e.IsCritical);Assert.Equal(157.5m,e.RawAmount);});
        r.Tuning=Tuning(0m);Assert.Equal(0m,Run(r).DamageDone);
        r.Tuning=Tuning(37m);Assert.All(Run(r).Run.Timeline.Where(e=>e.Type==CombatEventType.Damage&&e.Amount>0),e=>Assert.Equal(58.275m,e.RawAmount));
    }
    [Theory][InlineData("wrath")][InlineData("starfire")][InlineData("moonfire")][InlineData("hurricane")]
    public void ForcedMissDoesNotCreateNaturalistDamage(string ability) {
        var r=Request(ability);r.BaseSpellHitPercent=0m;r.AdditionalTargets=2;
        var result=Run(r);Assert.Equal(0m,result.DamageDone);
        Assert.DoesNotContain(result.Run.Timeline,e=>e.Type==CombatEventType.ThreatChanged);
        Assert.DoesNotContain(result.Run.Timeline,e=>e.AbilityKey=="hurricane-secondary");
        Assert.Contains(result.Run.Timeline,e=>e.ResultKey=="miss");
    }
    [Theory][InlineData(0,45)][InlineData(1,45.45)][InlineData(2,45.9)][InlineData(3,46.35)][InlineData(4,46.8)][InlineData(5,47.25)]
    public void BalanceThornsUsesNaturalistWithoutChangingSharedProcessor(int rank,double expected) {
        var r=Request("faerie-fire",rank);r.UseThorns=true;r.IncomingBossHitDamage=10m;
        var result=Run(r);var hits=result.Run.Timeline.Where(e=>e.AbilityKey=="thorns"&&e.Type==CombatEventType.Damage).ToArray();
        Assert.NotEmpty(hits);Assert.All(hits,e=>Assert.Equal((decimal)expected,e.RawAmount));
        Assert.Equal((decimal)hits.Length,result.Run.CustomMetrics["balance-thorns-hits"]);
        r.Character.BaseStats.Set("spell-power",210m);
        Assert.Equal(57.6m*RankDamage[rank]/100m,Run(r).Run.Timeline.First(e=>e.AbilityKey=="thorns"&&e.Type==CombatEventType.Damage).RawAmount);
    }
    [Fact]
    public void ThornsThreatUsesDealtDamageAndExistingSubtletyWhileZeroAndInactivePathsStayOff() {
        var r=Request("faerie-fire");r.UseThorns=true;r.IncomingBossHitDamage=10m;r.Talents.Ranks["subtlety"]=3;
        r.TargetMaximumHealth=20m;var result=Run(r);
        var hit=Assert.Single(result.Run.Timeline,e=>e.AbilityKey=="thorns"&&e.Type==CombatEventType.Damage);
        Assert.Equal(47.25m,hit.RawAmount);Assert.Equal(20m,hit.Amount);
        Assert.Equal(14m,Assert.Single(result.Run.Timeline,e=>e.Type==CombatEventType.ThreatChanged).Amount);
        // Shared Thorns currently does not emit ActorDied; do not change that contract.
        // A single clamped hit and no later reflection prove the living-target gate.
        Assert.Equal(20m,result.DamageDone);Assert.Equal(1m,result.Run.CustomMetrics["balance-thorns-hits"]);
        r.TargetMaximumHealth=100000m;r.UseThorns=false;Assert.Equal(0m,Run(r).DamageDone);
        r.UseThorns=true;r.IncomingBossHitDamage=0m;Assert.Equal(0m,Run(r).DamageDone);
        r.IncomingBossHitDamage=10m;r.Tuning.ThornsBaseDamage=0m;Assert.Equal(0m,Run(r).DamageDone);
        r.Tuning.ThornsBaseDamage=12m;r.Tuning.ThornsSpellPowerCoefficient=.2m;r.Character.BaseStats.Set("spell-power",10m);
        Assert.Equal(14.7m,Run(r).Run.Timeline.First(e=>e.AbilityKey=="thorns"&&e.Type==CombatEventType.Damage).RawAmount);
    }
    [Theory][InlineData("throughput")][InlineData("raid-sustain")][InlineData("deep-regen")]
    public void Cp138BuildsUse115Point5PercentAndPreserveResourceAndActionScheduling(string build) {
        var r=BalancePreRaidProfileCatalog.Setup("throughput",build);r.CaptureTimeline=true;r.TargetMaximumHealth=1000000000m;
        var before=JsonSerializer.Serialize(r);var result=Run(r);
        var wrath=ForeverBalanceDruidSpellbook.Create(r.Talents,new(),0m).Single(a=>a.Key=="wrath");
        Assert.Equal(105.105m,wrath.Effects[0].MinimumValue);Assert.Equal(30m,wrath.ResourceCosts[0].Amount);
        Assert.Equal(before,JsonSerializer.Serialize(r));
        r.Talents.Ranks["naturalist"]=0;var control=Run(r);
        Assert.Equal(control.EndingMana,result.EndingMana);
        Assert.Equal(JsonSerializer.Serialize(Schedule(control)),JsonSerializer.Serialize(Schedule(result)));
        var a=control.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage).ToArray();
        var b=result.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage).ToArray();Assert.Equal(a.Length,b.Length);
        for(var i=0;i<a.Length;i++)Assert.Equal(a[i].Amount*1.05m,b[i].Amount);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task PreparationAndConcurrentReuseDoNotMutateRanksTuningOrPreview(bool prepared) {
        var r=Request("wrath");if(prepared){r.Character.Race="tauren";r.UseProvisionalLevel60Baseline=true;}
        var before=JsonSerializer.Serialize(r);var preview=BalanceDruidSimRunner.Preview(r);var expected=Run(r);
        var actual=await Task.WhenAll(Enumerable.Range(0,4).Select(_=>Task.Run(()=>Run(r))));
        Assert.All(actual,a=>{Assert.Equal(expected.DamageDone,a.DamageDone);Assert.Equal(expected.EndingMana,a.EndingMana);Assert.Equal(JsonSerializer.Serialize(Schedule(expected)),JsonSerializer.Serialize(Schedule(a)));});
        Assert.Equal(before,JsonSerializer.Serialize(r));Assert.Equal(preview.SpellPower,expected.SpellPower);
        preview.EffectiveStats.Set("spell-power",99999m);Assert.Equal(expected.SpellPower,BalanceDruidSimRunner.Preview(r).SpellPower);
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)]
    public async Task StrictPartialItemizedFixtureHasExactNamedTotalsAndCorrectActualDamage(int rank) {
        var r=Request("wrath",rank);r.Character.Race="tauren";r.UseProvisionalLevel60Baseline=true;
        r.Talents=new() {BalancePointsSpent=5,FeralPointsSpent=0,RestorationPointsSpent=5+rank,
            Ranks=new(){["improved-wrath"]=5,["natures-focus"]=5,["naturalist"]=rank}};
        var q=new ForeverLevel60SimulationRequest {ClassKey="druid",Specialization="balance",ItemizedTalentMode=true,
            ItemizedTalentRanks=new(r.Talents.Ranks),Balance=r};
        var before=JsonSerializer.Serialize(q);
        var ok=Assert.IsType<OkObjectResult>(await new ForeverLevel60SimulationController().Simulate(q,default));
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(ok.Value,new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal(ForeverHistoricalCasterTalentCatalog.SourceStatus,json.RootElement.GetProperty("talentSourceStatus").GetString());
        var first=json.RootElement.GetProperty("sim").GetProperty("run").GetProperty("timeline").EnumerateArray().First(e=>e.GetProperty("type").GetInt32()==6);
        Assert.Equal(RankDamage[rank],first.GetProperty("rawAmount").GetDecimal());Assert.Equal(before,JsonSerializer.Serialize(q));
        q.ItemizedTalentRanks["naturalist"]=(rank+1)%6;
        Assert.IsType<BadRequestObjectResult>(await new ForeverLevel60SimulationController().Simulate(q,default));
    }
}
