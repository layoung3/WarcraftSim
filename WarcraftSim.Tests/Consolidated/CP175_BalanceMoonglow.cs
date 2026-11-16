using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Druid;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175BalanceMoonglowTests
{
    // Independent literal historical-source oracles. Hurricane850 is explicit local
    // provisional tuning, not certification of the upstream Hurricane base cost.
    private static readonly decimal[][] Wrath = [
        [120m,110.4m,99.6m,90m],[108m,98.4m,87.6m,78m],
        [96m,86.4m,75.6m,66m],[84m,74.4m,63.6m,54m],
        [72m,62.4m,51.6m,42m],[60m,50.4m,39.6m,30m]];
    private static readonly Dictionary<string,decimal[]> Costs = new() {
        ["starfire"]=[340m,312.8m,282.2m,255m], ["moonfire"]=[375m,345m,311.25m,281.25m],
        ["insect-swarm"]=[160m,147.2m,132.8m,120m], ["hurricane"]=[850m,782m,705.5m,637.5m],
        ["faerie-fire"]=[115m,115m,115m,115m] };
    private static string[] Abilities => ["wrath",..Costs.Keys];
    private static BalanceDruidSimRequest Request(string ability="wrath",int mg=3,int iw=5) {
        var r=new BalanceDruidSimRequest {DurationSeconds=18m,TargetMaximumHealth=1000000000m,
            BaseSpellHitPercent=100m,SpellCritPercent=0m,MajorManaPotionCharges=0,
            UseOmenOfClarity=false,UseInnervate=false,UseEclipse=false,StarfireOnlyWithEclipse=false,
            CustomPriority=[ability],Talents=new() {BalancePointsSpent=5+mg+iw,RestorationPointsSpent=0,
                Ranks=new(StringComparer.OrdinalIgnoreCase) {["genesis"]=5,["improved-wrath"]=iw,["moonglow"]=mg}},
            Tuning=new() {ManaRegenPerSecond=0m}};
        if(ability=="insect-swarm") {
            r.Talents.Ranks=new(StringComparer.OrdinalIgnoreCase) { ["improved-wrath"]=5,["genesis"]=5,
                ["improved-moonfire"]=2,["natures-reach"]=2,["natures-majesty"]=1,["insect-swarm"]=1,["moonglow"]=mg};
            r.Talents.BalancePointsSpent=16+mg;
        }
        r.Character.BaseStats.Values=new(StringComparer.Ordinal) { ["intellect"]=61.6m,["spirit"]=0m,["mp5"]=0m,["spell-power"]=0m};
        return r;
    }
    private static BalanceDruidSimResponse Run(BalanceDruidSimRequest r)=>new BalanceDruidSimRunner().Run(r);
    private static CombatEvent[] Debits(BalanceDruidSimResponse r,string ability)=>r.Run.Timeline.Where(e=>e.Type==CombatEventType.ResourceChanged&&e.ResourceKey=="mana"&&e.AbilityKey==ability&&e.Amount<=0).ToArray();
    private static decimal Expected(string ability,int mg,int iw)=>ability=="wrath"?Wrath[iw][mg]:Costs[ability][mg];
    private static void Reconcile(BalanceDruidSimResponse result,decimal initial) {
        Assert.Equal(initial+result.Run.Timeline.Where(e=>e.Type==CombatEventType.ResourceChanged&&e.ResourceKey=="mana").Sum(e=>e.Amount??0),result.EndingMana);
        Assert.Equal(result.EndingMana,result.Run.Summary.ActorSummaries["druid"].EndingResources["mana"]);
        Assert.Equal(result.DamageDone,result.DamageByAbility.Values.Sum());
    }
    public static IEnumerable<object[]> Ranks() { for(var iw=0;iw<=5;iw++)for(var mg=0;mg<=3;mg++)yield return [iw,mg]; }
    [Theory][MemberData(nameof(Ranks))]
    public void LiteralRankTablesReachSpellbookActualDebitsAndReports(int iw,int mg) {
        foreach(var ability in Abilities) {
            var r=Request(ability,mg,iw);var expected=Expected(ability,mg,iw);
            Assert.Equal(expected,ForeverBalanceDruidSpellbook.Create(r.Talents,r.Tuning,0m).Single(a=>a.Key==ability).ResourceCosts[0].Amount);
            var result=Run(r);var debits=Debits(result,ability);Assert.NotEmpty(debits);Assert.All(debits,e=>Assert.Equal(-expected,e.Amount));Reconcile(result,2000m);
        }
    }
    [Theory][MemberData(nameof(Ranks))]
    public void ExactAndJustBelowAffordabilityUseCorrectedCosts(int iw,int mg) {
        foreach(var ability in Abilities) {
            var r=Request(ability,mg,iw);var cost=Expected(ability,mg,iw);r.StartingManaPercent=cost/20m;
            var exact=Run(r);Assert.Single(Debits(exact,ability));Assert.Equal(0m,exact.EndingMana);Reconcile(exact,cost);
            r.StartingManaPercent=(cost-.001m)/20m;var below=Run(r);Assert.Empty(Debits(below,ability));Assert.Equal(cost-.001m,below.EndingMana);Reconcile(below,cost-.001m);
        }
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)]
    public void CustomAndZeroTuningRemainExplicit(int mg) {
        decimal[] customWrath=[18.5m,15.54m,12.21m,9.25m],others=[37m,34.04m,30.71m,27.75m];
        foreach(var ability in Abilities)foreach(var zero in new[]{false,true}) {
            var r=Request(ability,mg);var value=zero?0m:37m;
            r.Tuning.WrathMana=r.Tuning.StarfireMana=r.Tuning.MoonfireMana=r.Tuning.InsectSwarmMana=r.Tuning.HurricaneMana=r.Tuning.FaerieFireMana=value;
            var expected=zero?0m:ability=="wrath"?customWrath[mg]:ability=="faerie-fire"?37m:others[mg];
            var result=Run(r);Assert.NotEmpty(Debits(result,ability));Assert.All(Debits(result,ability),e=>Assert.Equal(-expected,e.Amount));Reconcile(result,2000m);
        }
    }
    [Theory][InlineData("wrath",1.5)][InlineData("starfire",3.5)][InlineData("moonfire",1)][InlineData("insect-swarm",1)][InlineData("hurricane",1)]
    public void ExistingDebitTimingMissesAndUnfinishedCastsArePreserved(string ability,double at) {
        var r=Request(ability);r.DurationSeconds=4m;var result=Run(r);
        Assert.Equal((decimal)at,Debits(result,ability).First().TimeSeconds);
        if(ability=="hurricane")Assert.Single(Debits(result,ability));
        if(at>0) {r.DurationSeconds=(decimal)at-.01m;var unfinished=Run(r);Assert.Empty(Debits(unfinished,ability));Assert.Equal(2000m,unfinished.EndingMana);}
        if(ability!="insect-swarm") {r.DurationSeconds=4m;r.BaseSpellHitPercent=0m;var miss=Run(r);Assert.Equal(0m,miss.DamageDone);Assert.NotEmpty(Debits(miss,ability));Assert.All(Debits(miss,ability),e=>Assert.Equal(-Expected(ability,3,5),e.Amount));Reconcile(miss,2000m);}
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void MissingZeroAndOriginalComparerRemainUnchanged(bool insensitive) {
        var r=Request(mg:0);r.Talents.Ranks=new(r.Talents.Ranks,insensitive?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal);
        r.Talents.Ranks.Remove("moonglow");var absent=Run(r);r.Talents.Ranks["moonglow"]=0;Assert.Equal(absent.EndingMana,Run(r).EndingMana);
        r.Talents.Ranks.Remove("moonglow");r.Talents.Ranks["MOONGLOW"]=3;r.Talents.BalancePointsSpent=13;var before=JsonSerializer.Serialize(r);
        Assert.All(Debits(Run(r),"wrath"),e=>Assert.Equal(insensitive?-30m:-60m,e.Amount));Assert.Equal(before,JsonSerializer.Serialize(r));
        Assert.Same(insensitive?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal,r.Talents.Ranks.Comparer);
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)]
    public void OmenRestoresCorrectedCachedCostsAndNeverDiscountsWrath(int mg) {
        foreach(var ability in new[]{"wrath","starfire","moonfire"}) {
            var r=Request(ability,mg);r.DurationSeconds=40m;r.OmenProcChancePercent=100m;r.UseOmenOfClarity=true;
            var result=Run(r);var debits=Debits(result,ability);Assert.True(result.OmenProcs>0);Reconcile(result,2000m);
            if(ability=="wrath") {Assert.Equal(0,result.OmenConsumed);Assert.All(debits,e=>Assert.Equal(-Wrath[5][mg],e.Amount));}
            else {Assert.True(result.OmenConsumed>0);Assert.Contains(debits,e=>e.Amount==0m);Assert.All(debits,e=>Assert.True(e.Amount==0m||e.Amount==-Costs[ability][mg]));if(ability=="starfire")Assert.Contains(debits.SkipWhile(e=>e.Amount!=0m).Skip(1),e=>e.Amount==-Costs[ability][mg]);}
        }
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task PreparationRepeatedAndConcurrentRequestsStayIsolated(bool prepared) {
        var r=Request();if(prepared){r.Character.Race="tauren";r.UseProvisionalLevel60Baseline=true;}
        var before=JsonSerializer.Serialize(r);var preview=BalanceDruidSimRunner.Preview(r);var expected=Run(r);
        var results=await Task.WhenAll(Enumerable.Range(0,4).Select(_=>Task.Run(()=>Run(r))));
        foreach(var result in results){Assert.Equal(expected.EndingMana,result.EndingMana);Assert.Equal(expected.DamageDone,result.DamageDone);Assert.Equal(Debits(expected,"wrath").Select(e=>(e.TimeSeconds,e.Amount,e.ResourceTotalAfter)),Debits(result,"wrath").Select(e=>(e.TimeSeconds,e.Amount,e.ResourceTotalAfter)));}
        Assert.Equal(before,JsonSerializer.Serialize(r));Assert.Equal(preview.MaximumMana,expected.MaximumMana);preview.EffectiveStats.Set("mana",1m);Assert.Equal(expected.MaximumMana,BalanceDruidSimRunner.Preview(r).MaximumMana);
    }
    [Theory][InlineData("moonfire")][InlineData("insect-swarm")][InlineData("hurricane")]
    public void PreparedInstantsAndChannelDebitAtStart(string ability) {
        var r=Request(ability);r.Character.Race="tauren";r.UseProvisionalLevel60Baseline=true;r.DurationSeconds=.5m;
        var result=Run(r);var debit=Assert.Single(Debits(result,ability));Assert.Equal(0m,debit.TimeSeconds);
        Assert.Equal(-Costs[ability][3],debit.Amount);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task OmenMutationIsIsolatedAcrossRepeatedAndConcurrentRuns(bool prepared) {
        var r=Request("starfire",mg:2);r.UseOmenOfClarity=true;r.OmenProcChancePercent=100m;r.DurationSeconds=24m;
        if(prepared){r.Character.Race="tauren";r.UseProvisionalLevel60Baseline=true;}
        var before=JsonSerializer.Serialize(r);var expected=Run(r);
        Assert.True(expected.OmenConsumed>0);Assert.Contains(Debits(expected,"starfire"),e=>e.Amount==0m);
        Assert.Contains(Debits(expected,"starfire").SkipWhile(e=>e.Amount!=0m).Skip(1),e=>e.Amount==-282.2m);
        var results=await Task.WhenAll(Enumerable.Range(0,4).Select(_=>Task.Run(()=>Run(r))));
        foreach(var result in results) {
            Assert.Equal(expected.EndingMana,result.EndingMana);Assert.Equal(expected.DamageDone,result.DamageDone);
            Assert.Equal(expected.OmenConsumed,result.OmenConsumed);Assert.Equal(expected.OmenProcs,result.OmenProcs);
            Assert.Equal(Debits(expected,"starfire").Select(e=>(e.TimeSeconds,e.Amount,e.ResourceTotalAfter)),Debits(result,"starfire").Select(e=>(e.TimeSeconds,e.Amount,e.ResourceTotalAfter)));
        }
        Assert.Equal(before,JsonSerializer.Serialize(r));
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)]
    public void ActualOmenProcessorRestoresEveryCachedPriceInNoReprocWindow(int mg) {
        foreach(var ability in Costs.Keys) {
            var r=Request("insect-swarm",mg);r.UseOmenOfClarity=true;r.OmenProcChancePercent=100m;
            var actor=new SimulationActorState{Key="druid"};
            foreach(var spell in ForeverBalanceDruidSpellbook.Create(r.Talents,r.Tuning,0m))actor.AddAbility(spell);
            var context=new SimulationContext(new SimulationRunOptions{Seed=1,DurationSeconds=10m});
            // Exercise the production event processor directly so the interval has
            // exactly one proc and no damage event can accidentally reproc Omen.
            var type=typeof(BalanceDruidSimRunner).GetNestedType("BalanceState",System.Reflection.BindingFlags.NonPublic)!;
            var state=(ICombatEventProcessor)Activator.CreateInstance(type,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic,
                null,new object[]{r,BalanceAdaptiveRotation.Build(r),actor,0m,false},null)!;
            state.Process(context,new CombatEvent{Type=CombatEventType.Damage,SourceActorKey="druid",AbilityKey="wrath",ResultKey="hit"});
            Assert.Equal(Wrath[5][mg],actor.Abilities["wrath"].Definition.ResourceCosts[0].Amount);
            Assert.All(Costs.Keys,key=>Assert.Equal(0m,actor.Abilities[key].Definition.ResourceCosts[0].Amount));
            state.Process(context,new CombatEvent{Type=CombatEventType.AbilityCastStarted,SourceActorKey="druid",AbilityKey="wrath"});
            Assert.All(Costs.Keys,key=>Assert.Equal(0m,actor.Abilities[key].Definition.ResourceCosts[0].Amount));
            state.Process(context,new CombatEvent{Type=CombatEventType.AbilityCastStarted,SourceActorKey="druid",AbilityKey=ability});
            Assert.Equal(0m,actor.Abilities[ability].Definition.ResourceCosts[0].Amount);
            state.Process(context,new CombatEvent{Type=CombatEventType.AbilityCastCompleted,SourceActorKey="druid",AbilityKey=ability});
            foreach(var key in Abilities)Assert.Equal(Expected(key,mg,5),actor.Abilities[key].Definition.ResourceCosts[0].Amount);
        }
    }
    [Theory][MemberData(nameof(Ranks))]
    public async Task StrictLegalPartialAllocationsUseSameCostsAndRejectInconsistentMaps(int iw,int mg) {
        foreach(var ability in new[]{"wrath","insect-swarm"}) {
            var r=Request(ability,mg,iw);r.Character.Race="tauren";r.UseProvisionalLevel60Baseline=true;
            var ranks=new Dictionary<string,int>{{"genesis",5},{"improved-wrath",iw},{"moonglow",mg}};
            if(ability=="insect-swarm"){ranks["improved-wrath"]=5;ranks["improved-moonfire"]=2;ranks["natures-reach"]=2;ranks["natures-majesty"]=1;ranks["insect-swarm"]=1;}
            r.Talents=new(){BalancePointsSpent=ranks.Values.Sum(),RestorationPointsSpent=0,Ranks=ranks};
            var q=new ForeverLevel60SimulationRequest{ClassKey="druid",Specialization="balance",ItemizedTalentMode=true,ItemizedTalentRanks=new(ranks),Balance=r};
            var ok=Assert.IsType<OkObjectResult>(await new ForeverLevel60SimulationController().Simulate(q,default));
            using var json=JsonDocument.Parse(JsonSerializer.Serialize(ok.Value,new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            var events=json.RootElement.GetProperty("sim").GetProperty("run").GetProperty("timeline").EnumerateArray().Where(e=>e.GetProperty("type").GetInt32()==(int)CombatEventType.ResourceChanged&&e.GetProperty("abilityKey").GetString()==ability).ToArray();
            Assert.NotEmpty(events);Assert.All(events,e=>Assert.Equal(-Expected(ability,mg,iw),e.GetProperty("amount").GetDecimal()));
            q.ItemizedTalentRanks["moonglow"]=(mg+1)%4;Assert.IsType<BadRequestObjectResult>(await new ForeverLevel60SimulationController().Simulate(q,default));q.ItemizedTalentRanks["moonglow"]=mg;
            r.Talents.BalancePointsSpent++;Assert.IsType<BadRequestObjectResult>(await new ForeverLevel60SimulationController().Simulate(q,default));
        }
    }
    [Theory][InlineData(-1)][InlineData(4)]
    public async Task InvalidRanksAreRejectedByTypedAndStrictAdmission(int rank) {
        var r=Request(mg:rank);Assert.Throws<ArgumentException>(()=>Run(r));
        var q=new ForeverLevel60SimulationRequest{ClassKey="druid",Specialization="balance",ItemizedTalentMode=true,ItemizedTalentRanks=new(r.Talents.Ranks),Balance=r};
        Assert.IsType<BadRequestObjectResult>(await new ForeverLevel60SimulationController().Simulate(q,default));
    }
    [Fact]
    public void NegativeEligibleCostTuningRemainsRejected() {
        foreach(var field in new[]{"WrathMana","StarfireMana","MoonfireMana","InsectSwarmMana","HurricaneMana"}) {
            var r=Request();typeof(ForeverBalanceDruidTuning).GetProperty(field)!.SetValue(r.Tuning,-1m);Assert.Throws<ArgumentException>(()=>Run(r));
        }
    }
}
