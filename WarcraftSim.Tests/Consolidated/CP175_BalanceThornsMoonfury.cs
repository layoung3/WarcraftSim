using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Druid;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175BalanceThornsMoonfuryTests
{
    // Literal local-tuning oracles, not certification of upstream base18/provider behavior.
    private static readonly decimal[] NoPower = [45m,45.9m,46.8m,47.7m,48.6m,49.5m];
    private static readonly decimal[] Powered = [57.6m,58.752m,59.904m,61.056m,62.208m,63.36m];
    private static BalanceDruidSimRequest Request(int rank=5,decimal power=0m,bool prepared=false,int naturalist=0) {
        var r=new BalanceDruidSimRequest {
            DurationSeconds=18m,TargetMaximumHealth=1000000000m,MaximumHealth=10000m,
            CaptureTimeline=true,BaseSpellHitPercent=100m,SpellCritPercent=0m,
            UseThorns=true,IncomingBossHitDamage=10m,BossSwingIntervalSeconds=3m,
            MajorManaPotionCharges=0,UseOmenOfClarity=false,UseInnervate=false,
            UseEclipse=false,StarfireOnlyWithEclipse=false,CustomPriority=["faerie-fire"],
            Talents=new() {BalancePointsSpent=30,RestorationPointsSpent=10,
                Ranks=new(StringComparer.OrdinalIgnoreCase) { ["moonfury"]=rank,["naturalist"]=naturalist }},
            Tuning=new() {ManaRegenPerSecond=0m}
        };
        r.Character.BaseStats.Values=new(StringComparer.Ordinal) {
            ["intellect"]=1000m,["spirit"]=0m,["spell-power"]=power,["mp5"]=0m };
        if(prepared) {
            r.Character.Race="tauren";r.UseProvisionalLevel60Baseline=true;
            // Prepared base stats are replaced by the naked baseline. Authored equipment
            // deliberately supplies the exact power oracle through the real preparation path.
            r.Character.Equipment.Items.Add(new() {SlotKey="chest",ItemKey="thorns-oracle",
                Stats=new() {Values=new() {["spell-power"]=power,["intellect"]=1000m}}});
        }
        return r;
    }
    private static BalanceDruidSimResponse Run(BalanceDruidSimRequest r)=>new BalanceDruidSimRunner().Run(r);
    private static CombatEvent[] Hits(BalanceDruidSimResponse r)=>r.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage&&e.AbilityKey=="thorns").ToArray();
    private static string Schedule(BalanceDruidSimResponse r)=>JsonSerializer.Serialize(r.Run.Timeline.Select(e=>new {
        e.TimeSeconds,e.Type,e.SourceActorKey,e.TargetActorKey,e.AbilityKey,e.EffectKey,e.ResourceKey,
        e.ResourceTotalAfter,e.ResultKey,e.IsCritical,e.EffectDeliveryType,
        Amount=e.Type==CombatEventType.ResourceChanged?e.Amount:null
    }));
    public static IEnumerable<object[]> RankCases() {
        for(var rank=0;rank<=5;rank++)foreach(var prepared in new[]{false,true})foreach(var power in new[]{0m,210m})
            yield return [rank,prepared,power];
    }
    [Theory][MemberData(nameof(RankCases))]
    public void LiteralRankCurveReachesActualDamageThreatAndReports(int rank,bool prepared,decimal power) {
        var r=Request(rank,power,prepared);var result=Run(r);var hits=Hits(result);var expected=power==0m?NoPower[rank]:Powered[rank];
        Assert.Equal(power,result.SpellPower);Assert.Equal(6,hits.Length);
        Assert.Equal(new[]{2m,5m,8m,11m,14m,17m},hits.Select(e=>e.TimeSeconds));
        Assert.All(hits,e=>{Assert.Equal(expected,e.RawAmount);Assert.Equal(expected,e.Amount);Assert.False(e.IsCritical);Assert.Equal("nature",e.SchoolKey);Assert.Equal("thorns-reactive-nature",e.EffectKey);Assert.False(e.IsPeriodic);});
        var threats=result.Run.Timeline.Where(e=>e.Type==CombatEventType.ThreatChanged).ToArray();
        Assert.Equal(6,threats.Length);Assert.All(threats,e=>Assert.Equal(expected,e.Amount));
        Assert.Equal(6m,result.Run.CustomMetrics["balance-thorns-hits"]);
        Assert.Equal(hits.Sum(e=>e.Amount!.Value),result.DamageDone);
        Assert.Equal(result.DamageDone,result.DamageByAbility["thorns"]);
        Assert.Equal(result.DamageDone/18m,result.Dps);Assert.Equal(result.MaximumHealth-60m,result.EndingHealth);
        var control=Run(Request(0,power,prepared));Assert.Equal(Schedule(control),Schedule(result));
        Assert.Equal(control.EndingMana,result.EndingMana);Assert.Equal(control.EndingHealth,result.EndingHealth);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void NaturalistCompositionCustomCoefficientAndZeroBaseUseEntirePowerTerm(bool prepared) {
        var r=Request(5,0m,prepared,5);Assert.All(Hits(Run(r)),e=>Assert.Equal(51.975m,e.RawAmount));
        r=Request(5,210m,prepared,5);Assert.All(Hits(Run(r)),e=>Assert.Equal(66.528m,e.RawAmount));
        r=Request(5,10m,prepared);r.Tuning.ThornsBaseDamage=12m;r.Tuning.ThornsSpellPowerCoefficient=.2m;
        Assert.All(Hits(Run(r)),e=>Assert.Equal(15.4m,e.RawAmount));
        r.Talents.Ranks["naturalist"]=5;Assert.All(Hits(Run(r)),e=>Assert.Equal(16.17m,e.RawAmount));
        r.Tuning.ThornsBaseDamage=0m;Assert.All(Hits(Run(r)),e=>Assert.Equal(2.31m,e.RawAmount));
        r.Tuning.ThornsSpellPowerCoefficient=0m;Assert.Empty(Hits(Run(r)));Assert.Equal(0m,Run(r).DamageDone);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void AbsenceZeroOriginalComparerAndDecimalSerializationRemainPreserved(bool insensitive) {
        var r=Request(0);r.Talents.Ranks=new(r.Talents.Ranks,insensitive?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal);
        var zero=Run(r);Assert.Equal("45.00",JsonSerializer.Serialize(Hits(zero)[0].RawAmount));
        r.Talents.Ranks.Remove("moonfury");var absent=Run(r);
        Assert.Equal(JsonSerializer.Serialize(Hits(zero)),JsonSerializer.Serialize(Hits(absent)));
        r.Talents.Ranks["MOONFURY"]=5;var before=JsonSerializer.Serialize(r);
        Assert.All(Hits(Run(r)),e=>Assert.Equal(insensitive?49.5m:45m,e.RawAmount));
        Assert.Equal(before,JsonSerializer.Serialize(r));Assert.Same(insensitive?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal,r.Talents.Ranks.Comparer);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task PreparedPreviewAndRepeatedConcurrentReuseStayIsolated(bool prepared) {
        var r=Request(5,210m,prepared,5);var before=JsonSerializer.Serialize(r);
        var preview=BalanceDruidSimRunner.Preview(r);var expected=Run(r);
        Assert.Equal(210m,preview.SpellPower);Assert.All(Hits(expected),e=>Assert.Equal(66.528m,e.RawAmount));
        var results=await Task.WhenAll(Enumerable.Range(0,4).Select(_=>Task.Run(()=>Run(r))));
        Assert.All(results,a=>{Assert.Equal(expected.DamageDone,a.DamageDone);Assert.Equal(Schedule(expected),Schedule(a));Assert.Equal(expected.EndingMana,a.EndingMana);});
        Assert.Equal(before,JsonSerializer.Serialize(r));
        var after=BalanceDruidSimRunner.Preview(r);Assert.Equal(preview.SpellPower,after.SpellPower);Assert.Equal(preview.MaximumMana,after.MaximumMana);
        Assert.Equal(JsonSerializer.Serialize(preview.EffectiveStats),JsonSerializer.Serialize(after.EffectiveStats));Assert.Equal(preview.ModelingNotes,after.ModelingNotes);
        Assert.Equal(preview.Rotation.Entries.Select(e=>e.AbilityKey),after.Rotation.Entries.Select(e=>e.AbilityKey));
        preview.EffectiveStats.Set("spell-power",99999m);Assert.Equal(210m,BalanceDruidSimRunner.Preview(r).SpellPower);
        Assert.Equal(before,JsonSerializer.Serialize(r));Assert.All(Hits(Run(r)),e=>Assert.Equal(66.528m,e.RawAmount));
    }
    [Theory][InlineData(0)][InlineData(5)]
    public void ForcedCritAndVengeanceDoNotCritOrAmplifyThorns(int vengeance) {
        var r=Request();r.SpellCritPercent=100m;r.Talents.Ranks["vengeance"]=vengeance;
        var result=Run(r);Assert.All(Hits(result),e=>{Assert.False(e.IsCritical);Assert.Equal(49.5m,e.RawAmount);});
        Assert.Equal(0,result.NaturesGraceProcs);Assert.Equal(0,result.OmenProcs);
    }
    [Fact]
    public void ClippedDamageThreatAndLivingEnemyGateRetainCurrentDeathContract() {
        var r=Request(naturalist:5);r.TargetMaximumHealth=20m;r.Talents.Ranks["subtlety"]=3;
        var result=Run(r);var hit=Assert.Single(Hits(result));Assert.Equal(51.975m,hit.RawAmount);Assert.Equal(20m,hit.Amount);
        Assert.Equal(14m,Assert.Single(result.Run.Timeline,e=>e.Type==CombatEventType.ThreatChanged).Amount);
        Assert.Equal(20m,result.DamageDone);Assert.Equal(1m,result.Run.CustomMetrics["balance-thorns-hits"]);
        Assert.DoesNotContain(result.Run.Timeline,e=>e.Type==CombatEventType.ActorDied&&e.TargetActorKey=="target");
    }
    [Theory][InlineData("disabled")][InlineData("zero-incoming")][InlineData("zero-raw")]
    public void InactiveRunnerPathsStayOff(string mode) {
        var r=Request();if(mode=="disabled")r.UseThorns=false;if(mode=="zero-incoming")r.IncomingBossHitDamage=0m;if(mode=="zero-raw")r.Tuning.ThornsBaseDamage=0m;
        var result=Run(r);Assert.Empty(Hits(result));Assert.Equal(0m,result.DamageDone);
    }
    private static ForeverLevel60SimulationRequest Strict(int rank,int naturalist=0) {
        var r=Request(rank,0m,true,naturalist);r.Talents=new() {
            BalancePointsSpent=25+rank,RestorationPointsSpent=naturalist==0?0:5+naturalist,
            Ranks=new(StringComparer.OrdinalIgnoreCase) { ["improved-wrath"]=5,["genesis"]=5,["improved-moonfire"]=2,
                ["moonglow"]=3,["vengeance"]=5,["improved-starfire"]=5,["moonfury"]=rank }};
        if(naturalist>0){r.Talents.Ranks["natures-focus"]=5;r.Talents.Ranks["naturalist"]=naturalist;}
        return new() {ClassKey="druid",Specialization="balance",ItemizedTalentMode=true,ItemizedTalentRanks=new(r.Talents.Ranks),Balance=r};
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)]
    public async Task StrictNamedTierSixPartialMapExecutesLiteralRankOracle(int rank) {
        var q=Strict(rank);var before=JsonSerializer.Serialize(q);
        Assert.Equal(25+rank,q.ItemizedTalentRanks!.Values.Sum());
        var ok=Assert.IsType<OkObjectResult>(await new ForeverLevel60SimulationController().Simulate(q,default));
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(ok.Value,new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal(ForeverHistoricalCasterTalentCatalog.SourceStatus,json.RootElement.GetProperty("talentSourceStatus").GetString());
        var hits=json.RootElement.GetProperty("sim").GetProperty("run").GetProperty("timeline").EnumerateArray().Where(e=>e.GetProperty("type").GetInt32()==6&&e.GetProperty("abilityKey").GetString()=="thorns").ToArray();
        Assert.Equal(6,hits.Length);Assert.All(hits,e=>Assert.Equal(NoPower[rank],e.GetProperty("rawAmount").GetDecimal()));
        Assert.Equal(before,JsonSerializer.Serialize(q));
    }
    [Fact]
    public async Task StrictNaturalistCompositionUsesExactNamedRestorationTotal() {
        var q=Strict(5,5);Assert.Equal(40,q.ItemizedTalentRanks!.Values.Sum());
        var ok=Assert.IsType<OkObjectResult>(await new ForeverLevel60SimulationController().Simulate(q,default));
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(ok.Value,new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var hits=json.RootElement.GetProperty("sim").GetProperty("run").GetProperty("timeline").EnumerateArray().Where(e=>e.GetProperty("type").GetInt32()==6&&e.GetProperty("abilityKey").GetString()=="thorns").ToArray();
        Assert.Equal(6,hits.Length);Assert.All(hits,e=>Assert.Equal(51.975m,e.GetProperty("rawAmount").GetDecimal()));
    }
    [Theory][InlineData("mismatch")][InlineData("overrank")][InlineData("lower-tier")][InlineData("wrong-total")]
    public async Task StrictAdmissionRejectionsRemainUnchanged(string mode) {
        var q=Strict(5);
        if(mode=="mismatch")q.ItemizedTalentRanks!["moonfury"]=4;
        if(mode=="overrank"){q.Balance!.Talents.Ranks["moonfury"]=6;q.ItemizedTalentRanks!["moonfury"]=6;q.Balance!.Talents.BalancePointsSpent=31;}
        if(mode=="lower-tier"){q.Balance!.Talents.Ranks["improved-starfire"]=4;q.ItemizedTalentRanks!["improved-starfire"]=4;q.Balance!.Talents.BalancePointsSpent=29;}
        if(mode=="wrong-total")q.Balance!.Talents.BalancePointsSpent=31;
        Assert.IsType<BadRequestObjectResult>(await new ForeverLevel60SimulationController().Simulate(q,default));
    }
    [Theory]
    [InlineData("disabled")][InlineData("no-aura")][InlineData("expired-aura")][InlineData("zero-incoming")]
    [InlineData("nonphysical")][InlineData("self")][InlineData("unknown-source")][InlineData("empty-source")]
    [InlineData("wrong-target")][InlineData("wrong-type")][InlineData("nonpositive-raw")][InlineData("dead-enemy")]
    public void SharedReflectionEligibilityRemainsUnchanged(string mode) {
        var ctx=new SimulationContext(new() {DurationSeconds=1m,CaptureTimeline=true,PrimaryActorKey="druid"});
        var druid=new SimulationActorState {Key="druid",TeamKey="raid",MaximumHealth=100m};druid.InitializeHealth(100m);
        var enemy=new SimulationActorState {Key="target",TeamKey="enemy",MaximumHealth=100m};enemy.InitializeHealth(100m);
        ctx.AddActor(druid);ctx.AddActor(enemy);
        if(mode!="no-aura")druid.ActiveAuras.Add(new() {Definition=new() {Key="druid-thorns",DurationSeconds=10m},AppliedAtSeconds=0m,ExpiresAtSeconds=mode=="expired-aura"?0m:10m});
        if(mode=="dead-enemy")enemy.TakeDamage(100m);
        var ev=new CombatEvent {Type=mode=="wrong-type"?CombatEventType.ScriptedDamage:CombatEventType.Damage,
            SourceActorKey=mode=="self"?"druid":mode=="unknown-source"?"unknown":mode=="empty-source"?"":"target",
            TargetActorKey=mode=="wrong-target"?"other":"druid",Amount=mode=="zero-incoming"?0m:10m,
            SchoolKey=mode=="nonphysical"?"nature":"physical"};
        var processor=new FeralThornsProcessor("druid",mode!="disabled",mode=="nonpositive-raw"?0m:49.5m);
        processor.Process(ctx,ev);Assert.Equal(0,processor.Hits);Assert.Equal(mode=="dead-enemy"?0m:100m,enemy.CurrentHealth);
    }
    [Fact]
    public void SharedReflectionStillAllowsDeadDruidAndClipsWithoutEmittingActorDied() {
        var ctx=new SimulationContext(new() {DurationSeconds=1m,CaptureTimeline=true,PrimaryActorKey="druid"});
        var druid=new SimulationActorState {Key="druid",TeamKey="raid",MaximumHealth=100m};druid.InitializeHealth(100m);druid.TakeDamage(100m);
        var enemy=new SimulationActorState {Key="target",TeamKey="enemy",MaximumHealth=20m};enemy.InitializeHealth(20m);
        ctx.AddActor(druid);ctx.AddActor(enemy);
        druid.ActiveAuras.Add(new() {Definition=new() {Key="druid-thorns",DurationSeconds=10m},AppliedAtSeconds=0m,ExpiresAtSeconds=10m});
        var processor=new FeralThornsProcessor("druid",true,51.975m);
        processor.Process(ctx,new() {Type=CombatEventType.Damage,SourceActorKey="target",TargetActorKey="druid",Amount=10m,SchoolKey="physical"});
        Assert.False(druid.IsAlive);Assert.Equal(1,processor.Hits);Assert.Equal(0m,enemy.CurrentHealth);
        var queued=new List<CombatEvent>();while(ctx.TryGetNextEvent(out var ev))queued.Add(ev!);
        var hit=Assert.Single(queued);Assert.Equal(CombatEventType.Damage,hit.Type);Assert.Equal(51.975m,hit.RawAmount);Assert.Equal(20m,hit.Amount);
    }
}
