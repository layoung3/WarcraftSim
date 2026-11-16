using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Druid;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175FeralFurorTransitions
{
    private static SimulationActorState Actor(decimal energy = 0, decimal maximumEnergy = 100,
        decimal rage = 0, bool hasRage = true)
    {
        var actor = new SimulationActorState { Key = "druid", MaximumHealth = 5000 };
        actor.InitializeHealth(5000);
        actor.AddResource(new ResourceState("mana", 10000, 10000));
        actor.AddResource(new ResourceState("energy", maximumEnergy, energy));
        if (hasRage) actor.AddResource(new ResourceState("rage", 100, rage));
        return actor;
    }

    public static IEnumerable<object[]> Ranks() => Enumerable.Range(1,5).Select(r => new object[] { r });

    // Original numerical tests and genuine red evidence are retained separately.
    // Establish actual Cat history, not an inactive starting pool.
    private static (SimulationActorState Actor, ForeverDruidFormState State) PriorCat(
        int rank, decimal remaining, decimal maximum = 100)
    {
        var actor = Actor(maximumEnergy: maximum);
        var state = new ForeverDruidFormState(rank, 100);
        Assert.True(state.EstablishInitialForm(actor, ForeverDruidForm.Cat, 0));
        actor.RefreshResources(6);
        actor.Resources["energy"].SetCurrent(remaining);
        return (actor, state);
    }

    [Theory][MemberData(nameof(Ranks))]
    public void FractionalEligibleTimeUsesSourcedRankFormulaWithoutCapMasking(int rank)
    {
        var (actor,state) = PriorCat(rank,5);
        Assert.True(state.Shift(actor,ForeverDruidForm.Caster,6,new Random(175)).Succeeded);
        Assert.True(state.Shift(actor,ForeverDruidForm.Cat,12.125m,new Random(175)).Succeeded);
        Assert.Equal(13.25m*rank,actor.Resources["energy"].Current);
    }

    [Theory][MemberData(nameof(Ranks))]
    public void BearTimeExcludesRegenerationAndRetentionAppliesOnlyOnCatReturn(int rank)
    {
        var (actor,state) = PriorCat(rank,80);
        Assert.True(state.Shift(actor,ForeverDruidForm.DireBear,6,new Random(175)).Succeeded);
        Assert.Equal(80,actor.Resources["energy"].Current);
        actor.RefreshResources(30);
        Assert.Equal(80,actor.Resources["energy"].Current);
        Assert.True(state.Shift(actor,ForeverDruidForm.Cat,30,new Random(175)).Succeeded);
        Assert.Equal(16m*rank,actor.Resources["energy"].Current);
    }

    [Theory][MemberData(nameof(Ranks))]
    public void EligibleIntervalsAccumulateAcrossBearWithoutResettingHistory(int rank)
    {
        var (actor,state) = PriorCat(rank,0);
        Assert.True(state.Shift(actor,ForeverDruidForm.Caster,6,new Random(175)).Succeeded);
        Assert.True(state.Shift(actor,ForeverDruidForm.Bear,12,new Random(175)).Succeeded);
        Assert.True(state.Shift(actor,ForeverDruidForm.Moonkin,18,new Random(175)).Succeeded);
        Assert.True(state.Shift(actor,ForeverDruidForm.Cat,24,new Random(175)).Succeeded);
        Assert.Equal(20m*rank,actor.Resources["energy"].Current);
    }

    [Theory][MemberData(nameof(Ranks))]
    public void CatReturnCapsAtRankAndPoolMaximum(int rank)
    {
        var (actor,state) = PriorCat(rank,45,45);
        Assert.True(state.Shift(actor,ForeverDruidForm.Moonkin,6,new Random(175)).Succeeded);
        Assert.True(state.Shift(actor,ForeverDruidForm.Cat,60,new Random(175)).Succeeded);
        Assert.Equal(Math.Min(45,20m*rank),actor.Resources["energy"].Current);
    }

    [Theory][MemberData(nameof(Ranks))]
    public void RepeatedBearCyclesUseMostRecentCatEnergyExactlyOnce(int rank)
    {
        var (actor,state) = PriorCat(rank,80);
        Assert.True(state.Shift(actor,ForeverDruidForm.DireBear,6,new Random(175)).Succeeded);
        Assert.True(state.Shift(actor,ForeverDruidForm.Cat,12,new Random(175)).Succeeded);
        var first = 16m*rank;
        Assert.Equal(first,actor.Resources["energy"].Current);
        actor.RefreshResources(18);
        actor.Resources["energy"].SetCurrent(first);
        Assert.True(state.Shift(actor,ForeverDruidForm.Bear,18,new Random(175)).Succeeded);
        Assert.True(state.Shift(actor,ForeverDruidForm.Cat,24,new Random(175)).Succeeded);
        Assert.Equal(.2m*rank*first,actor.Resources["energy"].Current);
    }

    private sealed class FixedRandom(double roll) : Random
    {
        public int Calls { get; private set; }
        public override double NextDouble() { Calls++; return roll; }
    }

    public static IEnumerable<object[]> AllRanksAndForms()
    {
        foreach (var rank in Enumerable.Range(0,6))
        foreach (var form in new[] { ForeverDruidForm.Cat, ForeverDruidForm.Bear, ForeverDruidForm.DireBear })
            yield return [rank,form];
    }

    [Theory][MemberData(nameof(AllRanksAndForms))]
    public void ExplicitInitialFormPreservesAuthoredPoolsAndOwnsOnePaidLockout(int rank, ForeverDruidForm form)
    {
        var actor=Actor(80,rage:20); var state=new ForeverDruidFormState(rank,250);
        Assert.True(state.EstablishInitialForm(actor,form,0));
        Assert.Equal(form,state.Form); Assert.Equal(1,state.Shifts);
        Assert.Equal(6,state.NextShiftAvailableAtSeconds);
        Assert.Equal(9750,actor.Resources["mana"].Current);
        Assert.Equal(80,actor.Resources["energy"].Current);
        Assert.Equal(20,actor.Resources["rage"].Current);
        var before=StateJson(actor,state);
        Assert.False(state.EstablishInitialForm(actor,ForeverDruidForm.Moonkin,50));
        Assert.Equal(before,StateJson(actor,state));
    }

    [Theory][MemberData(nameof(Ranks))]
    public void InitialBearDoesNotInventPriorCatHistoryFromInactiveEnergy(int rank)
    {
        var actor=Actor(80); var state=new ForeverDruidFormState(rank,100);
        Assert.True(state.EstablishInitialForm(actor,ForeverDruidForm.DireBear,0));
        var random=new FixedRandom(0);
        var entered=state.Shift(actor,ForeverDruidForm.Cat,6,random);
        Assert.True(entered.Succeeded); Assert.Equal(0,actor.Resources["energy"].Current);
        var change=Assert.Single(entered.FurorChanges);
        Assert.Equal("energy",change.ResourceKey); Assert.Equal(-80,change.Amount); Assert.Equal(0,change.TotalAfter);
        Assert.Equal(0,random.Calls);
    }

    [Theory][MemberData(nameof(Ranks))]
    public void FirstCatEntryCountsOnlyEligibleTimeAfterInitialBear(int rank)
    {
        var actor=Actor(80); var state=new ForeverDruidFormState(rank,100); var random=new FixedRandom(0);
        Assert.True(state.EstablishInitialForm(actor,ForeverDruidForm.Bear,0));
        Assert.True(state.Shift(actor,ForeverDruidForm.Caster,12,random).Succeeded);
        Assert.True(state.Shift(actor,ForeverDruidForm.Cat,18,random).Succeeded);
        Assert.Equal(12m*rank,actor.Resources["energy"].Current);
        Assert.Equal(0,random.Calls);
    }

    [Theory][MemberData(nameof(Ranks))]
    public void CatLeaveSnapshotsAfterPassiveRefresh(int rank)
    {
        var actor=Actor(); var state=new ForeverDruidFormState(rank,100); var random=new FixedRandom(.99);
        Assert.True(state.EstablishInitialForm(actor,ForeverDruidForm.Cat,0));
        Assert.True(state.Shift(actor,ForeverDruidForm.Bear,6,random).Succeeded);
        Assert.True(state.Shift(actor,ForeverDruidForm.Cat,12,random).Succeeded);
        Assert.Equal(12m*rank,actor.Resources["energy"].Current);
    }

    [Theory][MemberData(nameof(Ranks))]
    public void FailedAttemptsPreserveHistoryAndAffordabilityRefreshRemainsPassive(int rank)
    {
        var (actor,state)=PriorCat(rank,5); var random=new FixedRandom(.99);
        Assert.True(state.Shift(actor,ForeverDruidForm.Caster,6,random).Succeeded);
        var before=StateJson(actor,state);
        Assert.False(state.Shift(actor,ForeverDruidForm.Bear,8,random).Succeeded);
        Assert.False(state.Shift(actor,ForeverDruidForm.Caster,9,random).Succeeded);
        Assert.False(state.Shift(actor,(ForeverDruidForm)999,12,random).Succeeded);
        Assert.Equal(before,StateJson(actor,state));
        actor.Resources["mana"].SetCurrent(0);
        actor.Resources["mana"].RegenerationPerSecond=10;
        Assert.False(state.Shift(actor,ForeverDruidForm.Bear,12,random).Succeeded);
        Assert.Equal(60,actor.Resources["mana"].Current);
        Assert.Equal(rank==5 ? 65 : 5,actor.Resources["energy"].Current);
        Assert.Equal(12,actor.Resources["energy"].LastUpdatedAtSeconds);
        Assert.Equal(2,state.Shifts); Assert.Equal(12,state.NextShiftAvailableAtSeconds);
        actor.Resources["mana"].SetCurrent(100);
        Assert.True(state.Shift(actor,ForeverDruidForm.Cat,12.125m,random).Succeeded);
        Assert.Equal(13.25m*rank,actor.Resources["energy"].Current);
        Assert.Equal(0,random.Calls);
    }

    public static IEnumerable<object[]> BearRollCases()
    {
        foreach (var rank in Enumerable.Range(1,4))
        foreach (var form in new[] { ForeverDruidForm.Bear, ForeverDruidForm.DireBear })
        foreach (var boundary in new[] { -1,0,1 })
        foreach (var pool in new[] { -1,0,95,100 })
            yield return [rank,form,boundary,pool];
    }

    [Theory][MemberData(nameof(BearRollCases))]
    public void PartialBearRollIsExactOnceIncludingFullOrMissingPool(int rank, ForeverDruidForm form,
        int boundary, int pool)
    {
        var actor=Actor(rage:Math.Max(0,pool),hasRage:pool>=0);
        var state=new ForeverDruidFormState(rank,100);
        Assert.True(state.EstablishInitialForm(actor,ForeverDruidForm.Cat,0));
        var random=new FixedRandom(rank*.2+boundary*.0001);
        var result=state.Shift(actor,form,6,random);
        Assert.True(result.Succeeded); Assert.Equal(1,random.Calls);
        var expected=boundary<0 && pool>=0 ? Math.Min(10,100-pool) : 0;
        if (expected==0) Assert.Empty(result.FurorChanges);
        else
        {
            var change=Assert.Single(result.FurorChanges);
            Assert.Equal("rage",change.ResourceKey); Assert.Equal(expected,change.Amount);
            Assert.Equal(pool+expected,change.TotalAfter);
        }
        Assert.Equal(pool>=0,actor.Resources.ContainsKey("rage"));
        if (pool>=0) Assert.Equal(pool+expected,actor.Resources["rage"].Current);
        Assert.Equal(9800,actor.Resources["mana"].Current);
    }

    [Theory][InlineData(0)][InlineData(5)]
    public void EndpointRanksNeverDrawEvenWhenEnteringBothBearForms(int rank)
    {
        var actor=Actor(rage:95); var state=new ForeverDruidFormState(rank,0);
        var random=new FixedRandom(0);
        Assert.True(state.EstablishInitialForm(actor,ForeverDruidForm.Cat,0));
        var first=state.Shift(actor,ForeverDruidForm.Bear,6,random);
        Assert.Equal(rank==5 ? 100 : 95,actor.Resources["rage"].Current);
        Assert.Equal(rank==5 ? 1 : 0,first.FurorChanges.Count);
        if (rank==5) Assert.Equal(5,first.FurorChanges[0].Amount);
        Assert.True(state.Shift(actor,ForeverDruidForm.DireBear,12,random).Succeeded);
        Assert.False(state.Shift(actor,ForeverDruidForm.DireBear,18,random).Succeeded);
        Assert.Equal(0,random.Calls);
    }

    [Theory][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)]
    public void BearToDireBearIsARealRollButSameFormAndFailuresAreNot(int rank)
    {
        var actor=Actor(); var state=new ForeverDruidFormState(rank,100); var random=new FixedRandom(0);
        Assert.True(state.EstablishInitialForm(actor,ForeverDruidForm.Bear,0));
        Assert.False(state.Shift(actor,ForeverDruidForm.Bear,6,random).Succeeded);
        Assert.True(state.Shift(actor,ForeverDruidForm.DireBear,6,random).Succeeded);
        Assert.False(state.Shift(actor,ForeverDruidForm.Bear,11.999m,random).Succeeded);
        actor.Resources["mana"].SetCurrent(0);
        Assert.False(state.Shift(actor,ForeverDruidForm.Bear,12,random).Succeeded);
        Assert.Equal(1,random.Calls); Assert.Equal(10,actor.Resources["rage"].Current);
        Assert.Equal(2,state.Shifts); Assert.Equal(12,state.NextShiftAvailableAtSeconds);
    }

    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)]
    public void SeededRngAfterShiftMatchesExactlyOneSharedDrawAtPartialRanks(int rank)
    {
        foreach (var seed in new[] { 1,17,175,2047,144001 })
        {
            var actor=Actor(); var state=new ForeverDruidFormState(rank,0);
            var actual=new Random(seed); var oracle=new Random(seed);
            Assert.True(state.EstablishInitialForm(actor,ForeverDruidForm.Cat,0));
            var result=state.Shift(actor,ForeverDruidForm.DireBear,6,actual);
            var proc=rank==5 || rank>0 && oracle.NextDouble()*100 < rank*20;
            Assert.Equal(proc ? 10 : 0,actor.Resources["rage"].Current);
            Assert.Equal(oracle.NextDouble(),actual.NextDouble());
            Assert.Equal(proc ? 1 : 0,result.FurorChanges.Count);
        }
    }

    [Theory][InlineData(1,5,1)][InlineData(4,80,64)][InlineData(5,80,80)]
    public void CatRetentionReportsActualSignedDeltaAndNoZeroEvent(int rank,int prior,int expected)
    {
        var (actor,state)=PriorCat(rank,prior); var random=new FixedRandom(.99);
        Assert.True(state.Shift(actor,ForeverDruidForm.Bear,6,random).Succeeded);
        var before=actor.Resources["energy"].Current;
        var result=state.Shift(actor,ForeverDruidForm.Cat,12,random);
        Assert.Equal(expected,actor.Resources["energy"].Current);
        if (before==expected) Assert.Empty(result.FurorChanges);
        else
        {
            var change=Assert.Single(result.FurorChanges);
            Assert.Equal("energy",change.ResourceKey); Assert.Equal(expected-before,change.Amount);
            Assert.Equal(expected,change.TotalAfter);
        }
    }

    [Fact]
    public void MissingPoolsAreNotInventedAndFailedPaymentNeverRolls()
    {
        var actor=Actor(); actor.Resources.Remove("energy"); actor.Resources.Remove("rage");
        var state=new ForeverDruidFormState(3,100); var random=new FixedRandom(0);
        Assert.True(state.EstablishInitialForm(actor,ForeverDruidForm.Cat,0));
        Assert.Empty(state.Shift(actor,ForeverDruidForm.Bear,6,random).FurorChanges);
        Assert.Equal(1,random.Calls); Assert.Single(actor.Resources);
        actor.Resources.Remove("mana");
        Assert.False(state.Shift(actor,ForeverDruidForm.DireBear,12,random).Succeeded);
        Assert.Equal(1,random.Calls); Assert.Empty(actor.Resources);
    }

    private static string StateJson(SimulationActorState actor,ForeverDruidFormState state) =>
        JsonSerializer.Serialize(new { state.Form,state.Shifts,state.NextShiftAvailableAtSeconds,
            Resources=actor.Resources.ToDictionary(x=>x.Key,x=>new {
                x.Value.Current,x.Value.Maximum,x.Value.RegenerationPerSecond,x.Value.LastUpdatedAtSeconds }) });

    private static FeralDruidSimRequest Request(int rank,string role="cat") => new() {
        Role=role,Seed=144001,DurationSeconds=48,CaptureTimeline=true,MaximumMana=10000,
        Talents=new() { Ranks=new() { ["furor"]=rank } },
        FormTransitions=[new() {AtSeconds=6,Form="caster"},new() {AtSeconds=12,Form="bear"},
            new() {AtSeconds=18,Form="moonkin"},new() {AtSeconds=24,Form="cat"},
            new() {AtSeconds=30,Form="bear"},new() {AtSeconds=36,Form="caster"},new() {AtSeconds=42,Form="cat"}]
    };
    private static string Fingerprint(object value)
    {
        var ids=new Dictionary<string,string>();
        return Regex.Replace(JsonSerializer.Serialize(value),"[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}", m=> {
            if (!ids.TryGetValue(m.Value,out var id)) ids[m.Value]=id=$"ID-{ids.Count}";
            return id;
        });
    }

    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)]
    public async Task RunnerReuseConcurrentCallsAndControllerOwnAllMutableState(int rank)
    {
        var request=Request(rank); var before=JsonSerializer.Serialize(request); var runner=new FeralDruidSimRunner();
        var expected=Fingerprint(runner.Run(request));
        var results=await Task.WhenAll(Enumerable.Range(0,4).Select(_=>Task.Run(()=>runner.Run(request))));
        Assert.All(results,r=>Assert.Equal(expected,Fingerprint(r)));
        var endpoint=await new FeralDruidSimController().Run(request,CancellationToken.None);
        var body=Assert.IsType<FeralDruidSimResponse>(Assert.IsType<OkObjectResult>(endpoint.Result).Value);
        Assert.Equal(expected,Fingerprint(body)); Assert.Equal(before,JsonSerializer.Serialize(request));
        Assert.Equal(expected,Fingerprint(runner.Run(request)));
    }

    [Theory][MemberData(nameof(AllRanksAndForms))]
    public void NoTransitionRunnerAndPreviewRemainIndependentOfFurorRank(int rank,ForeverDruidForm form)
    {
        var role=form==ForeverDruidForm.Cat ? "cat" : "bear";
        var request=Request(rank,role);request.FormTransitions.Clear();
        var control=Request(0,role);control.FormTransitions.Clear();
        var result=new FeralDruidSimRunner().Run(request);
        Assert.Equal(Fingerprint(new FeralDruidSimRunner().Run(control)),Fingerprint(result));
        Assert.Equal(Fingerprint(FeralDruidSimRunner.Preview(control)),Fingerprint(FeralDruidSimRunner.Preview(request)));
        Assert.Equal(1,result.FormChanges);
        Assert.DoesNotContain(result.Run.Timeline,e=>e.AbilityKey=="furor");
    }

    [Theory][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)]
    public void IntegratedFurorEventsHaveOwnedImmediateTotalsAndNoDuplicatePaidCasts(int rank)
    {
        var request=Request(rank); var run=new FeralDruidSimRunner().Run(request);
        var events=run.Run.Timeline.Where(e=>e.AbilityKey=="furor").ToArray();
        Assert.NotEmpty(events);
        Assert.All(events,e=> {
            Assert.Equal(CombatEventType.ResourceChanged,e.Type);
            Assert.Equal("druid",e.SourceActorKey);Assert.Equal("druid",e.TargetActorKey);
            Assert.NotEqual(0,e.Amount);Assert.NotNull(e.ResourceTotalAfter);
            Assert.InRange(e.ResourceTotalAfter!.Value,0,100);
            Assert.Contains(e.TimeSeconds,request.FormTransitions.Select(t=>t.AtSeconds));
            Assert.Contains(e.ResourceKey,new[] {"energy","rage"});
            if(e.ResourceKey=="rage")Assert.InRange(e.Amount!.Value,.0000001m,10m);
        });
        var casts=run.Run.Timeline.Where(e=>e.Type==CombatEventType.AbilityCastStarted).ToArray();
        Assert.All(casts.GroupBy(e=>(e.TimeSeconds,e.AbilityKey)),g=>Assert.Single(g));
        Assert.Equal(run.DamageDone,run.DamageByAbility.Values.Sum());
        Assert.InRange(run.EndingEnergy,0,100);Assert.InRange(run.EndingRage,0,100);
    }

    [Theory][InlineData("cat",2,24,"energy",40,0)]
    [InlineData("cat",4,24,"energy",80,40)]
    [InlineData("bear",4,12,"rage",20,5)]
    public void SameTimestampSpendCannotRewriteImmediateFurorTotal(string role,int rank,int at,
        string resource,int shiftedTotal,int spentTotal)
    {
        var previous=SimulationInputTimingScope.AdditionalDelaySeconds;
        try
        {
            // Match the direct HTTP baseline transport explicitly.
            SimulationInputTimingScope.AdditionalDelaySeconds=.08m;
            var run=new FeralDruidSimRunner().Run(Request(rank,role));
            var same=run.Run.Timeline.Where(e=>e.TimeSeconds==at && e.Type==CombatEventType.ResourceChanged &&
                e.ResourceKey==resource).ToArray();
            var shift=Assert.Single(same,e=>e.AbilityKey=="furor");
            var spend=Assert.Single(same,e=>e.Amount<0 && e.AbilityKey!="furor");
            Assert.Equal(shiftedTotal,shift.ResourceTotalAfter);Assert.Equal(spentTotal,spend.ResourceTotalAfter);
            Assert.True(Array.IndexOf(same,shift)<Array.IndexOf(same,spend));
            Assert.Equal(spentTotal,shift.ResourceTotalAfter+spend.Amount);
        }
        finally {SimulationInputTimingScope.AdditionalDelaySeconds=previous;}
    }

    [Fact]
    public void ZeroAndNegativeCatRetentionDoNotCreatePhantomCastsOrRolls()
    {
        var previous=SimulationInputTimingScope.AdditionalDelaySeconds;
        try
        {
            SimulationInputTimingScope.AdditionalDelaySeconds=.08m;
            var request=Request(1); var result=new FeralDruidSimRunner().Run(request);
            // At24 the capped restoration equals20 already in the inactive
            // pool, so there is no resource event or second talent application.
            Assert.DoesNotContain(result.Run.Timeline,e=>e.AbilityKey=="furor" && e.TimeSeconds==24);
            var loss=Assert.Single(result.Run.Timeline,e=>e.AbilityKey=="furor" && e.TimeSeconds==42);
            Assert.Equal(-20,loss.Amount);Assert.Equal(20,loss.ResourceTotalAfter);
            Assert.DoesNotContain(result.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastStarted &&
                e.TimeSeconds==42);
            Assert.Equal(Fingerprint(result),Fingerprint(new FeralDruidSimRunner().Run(request)));
            // Same seeded gameplay after reconciling exactly two scheduled failed
            // attempts shows no RNG draw, pool change or extra cast.
            var attempted=FeralDruidSimulationRequestSnapshot.Create(request);
            attempted.FormTransitions.Add(new() {AtSeconds=7,Form="bear"});
            attempted.FormTransitions.Add(new() {AtSeconds=8,Form="caster"});
            var withFailures=new FeralDruidSimRunner().Run(attempted);
            Assert.Equal(2,withFailures.Run.CustomMetrics["feral-failed-form-shifts"]);
            Assert.Equal(result.Run.ProcessedEventCount+2,withFailures.Run.ProcessedEventCount);
            withFailures.Run.ProcessedEventCount-=2;
            withFailures.Run.CustomMetrics["feral-failed-form-shifts"]=0;
            Assert.Equal(Fingerprint(result),Fingerprint(withFailures));
        }
        finally {SimulationInputTimingScope.AdditionalDelaySeconds=previous;}
    }

    [Theory][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)]
    public void RejectedReinitializationCannotReplaceSavedCatHistory(int rank)
    {
        var (actor,state)=PriorCat(rank,5);var random=new FixedRandom(.99);
        Assert.True(state.Shift(actor,ForeverDruidForm.Caster,6,random).Succeeded);
        var before=StateJson(actor,state);
        Assert.False(state.EstablishInitialForm(actor,ForeverDruidForm.Bear,9));
        Assert.Equal(before,StateJson(actor,state));
        Assert.True(state.Shift(actor,ForeverDruidForm.Cat,12.125m,random).Succeeded);
        Assert.Equal(13.25m*rank,actor.Resources["energy"].Current);
        Assert.Equal(0,random.Calls);
    }

    [Fact]
    public void PartialStateInstancesDoNotShareBankedEnergyOrTime()
    {
        var (a,sa)=PriorCat(2,5);var (b,sb)=PriorCat(2,80);var rng=new FixedRandom(.99);
        Assert.True(sa.Shift(a,ForeverDruidForm.Caster,6,rng).Succeeded);
        Assert.True(sb.Shift(b,ForeverDruidForm.Bear,6,rng).Succeeded);
        Assert.True(sa.Shift(a,ForeverDruidForm.Cat,12.125m,rng).Succeeded);
        Assert.True(sb.Shift(b,ForeverDruidForm.Cat,30,rng).Succeeded);
        Assert.Equal(26.5m,a.Resources["energy"].Current);Assert.Equal(32,b.Resources["energy"].Current);
    }

    [Fact]
    public void RetainedHistoricalSourceHasExactFiveRankFormulaAndProvisionalConfidence()
    {
        var dir=new DirectoryInfo(AppContext.BaseDirectory);
        while(dir is not null && !File.Exists(Path.Combine(dir.FullName,"WarcraftSim.slnx")))dir=dir.Parent;
        Assert.NotNull(dir);
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir!.FullName,
            "docs/audits/CP175-feral-historical-source.json")));
        var root=doc.RootElement;
        Assert.Equal("1cc5ae1e9abdbf941b7326d2d90421822dce31ba",root.GetProperty("pinnedCommit").GetString());
        Assert.Equal("0e65167a1bcb5416c4d64be4975c892e0b40540bc266c5cf024bc3b52944e518",
            root.GetProperty("localDecodedReceiptSha256").GetString());
        Assert.Equal("historical-provisional-not-current-client-certified",root.GetProperty("sourceStatus").GetString());
        var furor=root.GetProperty("admittedKeys").EnumerateArray().Single(x=>x.GetProperty("canonicalKey").GetString()=="furor")
            .GetProperty("retainedSourceRecord");
        Assert.Contains("not in Bear Form, Cat Form, or Dire Bear Form",furor.GetProperty("description").GetString());
        var ranks=furor.GetProperty("ranks").EnumerateArray().ToArray();Assert.Equal(5,ranks.Length);
        for(var r=1;r<=5;r++)Assert.Equal(new[]{20*r,10,20*r,2*r,20*r},ranks[r-1].EnumerateArray().Select(x=>x.GetInt32()));
    }
}
