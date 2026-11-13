using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Healing;
using WarcraftSim.Data.Forever.Priest;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>Literal historical cost-composition oracles, not certification of other Shadow mechanics.
/// gunba/wow-forever-sim@1cc5ae1: devouring_plague.go initializes 100-25*rank;
/// talents.go Shadowform subtracts 50 from that same multiplier after registration.
/// Provisional local base 790 and allocated-form-as-active modeling are intentionally retained.</summary>
public sealed class Cp175ShadowPlagueCostTests
{
    private const string Plague = "devouring-plague";
    private static ShadowPriestSimRequest Request(int rank = 2, bool form = true, decimal cost = 790m)
    {
        var r = new ShadowPriestSimRequest {
            DurationSeconds = 49m, Seed = 175129, CaptureTimeline = true,
            BaseSpellHitPercent = 100m, SpellCritOverridePercent = 0m,
            SpellPowerOverride = 0m, TargetMaximumHealth = 1000000000m,
            UseVampiricEmbrace = false, UseShadowWordPain = false,
            UseMindBlast = false, UseMindFlay = false, UseShadowWordDeathExecute = false,
            Tuning = new() { DevouringPlagueMana = cost },
            StatAssumptions = new() { SpiritManaRegenerationPerSecondPerPoint = 0m }
        };
        r.Talents.Ranks["devouring-contagion"] = rank;
        r.Talents.Ranks["shadowform"] = form ? 1 : 0;
        r.Character.BaseStats.Set("mp5", 0m);
        return r;
    }

    private static IReadOnlyList<AbilityDefinition> Definitions(ShadowPriestSimRequest r) =>
        ForeverPriestShadowSpellbook.Create(r.Tuning, r.Talents, 0m);
    private static decimal PlagueCost(ShadowPriestSimRequest r) =>
        Assert.Single(Definitions(r), a => a.Key == Plague).ResourceCosts.Single().Amount;
    public static IEnumerable<object[]> CostCases()
    {
        decimal[][] costs = [[790m,592.5m,395m],[395m,197.5m,0m],
            [400m,300m,200m],[200m,100m,0m]];
        for (var b = 0; b < 2; b++) for (var form = 0; form < 2; form++) for (var rank = 0; rank < 3; rank++)
            yield return [b == 0 ? 790m : 400m, rank, form == 1, costs[2*b+form][rank]];
    }

    [Theory][MemberData(nameof(CostCases))]
    public void LiteralThreeByTwoDefinitionsRetainDamageTimingAndOtherSpells(decimal cost, int rank, bool form, decimal expected)
    {
        var r = Request(rank, form, cost); r.Talents.Validate(60);
        Assert.Equal(expected, PlagueCost(r));
        var spells = Definitions(r); var plague = Assert.Single(spells, a => a.Key == Plague);
        Assert.Equal(24m, plague.CooldownSeconds); Assert.Equal(0m, plague.CastTimeSeconds);
        Assert.Equal(1.5m, plague.GlobalCooldownSeconds);
        var effect = Assert.Single(plague.Effects);
        Assert.Equal(106m, effect.MinimumValue); Assert.Equal(106m, effect.MaximumValue);
        Assert.Equal(24m, effect.DurationSeconds); Assert.Equal(3m, effect.TickIntervalSeconds);
        Assert.Equal(AbilityEffectTypes.PeriodicDamage, effect.EffectType);
        Assert.True(effect.IncludeExpirationBoundaryTick);
        Assert.Equal(JsonSerializer.Serialize(Definitions(Request(0, form, cost)).Where(a => a.Key != Plague)),
            JsonSerializer.Serialize(spells.Where(a => a.Key != Plague)));
        Assert.Equal(form ? 175m : 350m, spells.Single(a => a.Key == "mind-blast").ResourceCosts.Single().Amount);
    }

    [Theory][MemberData(nameof(CostCases))]
    public void ActualExecutorSpendsLiteralCostAndRetainsZeroDebitEvent(decimal cost, int rank, bool form, decimal expected)
    {
        var h = new Harness(Request(rank, form, cost), 1000m, 3.1m);
        h.At(0m, () => h.Cast(Plague)); h.Run();
        var debit = Assert.Single(h.Context.Timeline, e => e.Type == CombatEventType.ResourceChanged && e.AbilityKey == Plague);
        Assert.Equal(-expected, debit.Amount); Assert.Equal(1000m-expected, debit.ResourceTotalAfter);
        Assert.Equal(1000m-expected, h.Mana.Current);
        Assert.Single(h.Context.Timeline, e => e.Type == CombatEventType.AbilityCastCompleted && e.AbilityKey == Plague);
        Assert.Contains(h.Context.Timeline, e => e.Type == CombatEventType.Damage && e.AbilityKey == Plague && e.TimeSeconds == 3m);
    }

    [Theory][InlineData(790,197.5)][InlineData(400,100)]
    public void RankOneExactManaIsAdmittedAndOneThousandthBelowIsRejected(int cost, double expectedValue)
    {
        var expected = (decimal)expectedValue;
        foreach (var exact in new[] {true,false}) {
            var initial = exact ? expected : expected-.001m;
            var h = new Harness(Request(1,true,cost), initial, 3.1m);
            h.At(0m, () => Assert.Equal(exact, h.Executor.TryStartAbility(h.Context,"priest","target",Plague).Success));
            h.Run();
            Assert.Equal(exact ? 0m : initial, h.Mana.Current);
            Assert.Equal(exact ? 1 : 0, h.Context.Timeline.Count(e => e.Type == CombatEventType.ResourceChanged && e.AbilityKey == Plague));
            Assert.Equal(exact ? 1 : 0, h.Context.Timeline.Count(e => e.Type == CombatEventType.AbilityCastCompleted && e.AbilityKey == Plague));
        }
    }

    [Fact]
    public void RankTwoFormAdmitsActualPlagueAtZeroMana()
    {
        var h = new Harness(Request(), 0m, 3.1m);
        h.At(0m, () => h.Cast(Plague)); h.Run();
        Assert.Equal(0m,h.Mana.Current);
        Assert.Contains(h.Context.Timeline,e => e.Type == CombatEventType.Damage && e.AbilityKey == Plague && e.Amount > 0m);
        Assert.Equal(0m,Assert.Single(h.Context.Timeline,e => e.Type == CombatEventType.ResourceChanged && e.AbilityKey == Plague).Amount);
    }

    [Theory][InlineData(3)][InlineData(6)]
    public void FreePlagueDuringOrAfterPaidSuppressionDoesNotRestartFiveSecondRule(int freeAt)
    {
        var h = new Harness(Request(),1000m,8m,spirit:10m);
        h.At(0m,() => h.Cast("shadow-word-pain"));
        h.At(freeAt,() => h.Cast(Plague));
        h.At(4m,() => Assert.Equal(0m,h.Mana.RegenerationPerSecond));
        h.At(5.001m,() => Assert.Equal(10m,h.Mana.RegenerationPerSecond));
        h.Run();
        Assert.Equal(867.5m,h.Mana.Current); // 1000 - 162.5 paid at 0 + 3 seconds * 10 after 5.
        var wake = Assert.Single(h.ProcessedEvents,e => e.EffectKey == FiveSecondManaRegenerationProcessor.WakeKey);
        Assert.Equal(5m,wake.TimeSeconds);
        Assert.Equal(-162.5m,Assert.Single(h.Context.Timeline,e => e.Type == CombatEventType.ResourceChanged && e.AbilityKey == "shadow-word-pain").Amount);
        Assert.Equal(0m,Assert.Single(h.Context.Timeline,e => e.Type == CombatEventType.ResourceChanged && e.AbilityKey == Plague).Amount);
    }

    [Fact]
    public void FreePlagueWithoutPriorSpendNeverStartsSuppression()
    {
        var h = new Harness(Request(),1000m,6m,spirit:10m);
        h.At(0m,() => h.Cast(Plague)); h.Run();
        Assert.Equal(1060m,h.Mana.Current); Assert.Equal(10m,h.Mana.RegenerationPerSecond);
        Assert.DoesNotContain(h.ProcessedEvents,e => e.EffectKey == FiveSecondManaRegenerationProcessor.WakeKey);
    }

    [Theory][InlineData(0)][InlineData(1)][InlineData(2)]
    public void ZeroNegativeAndDecimalTuningRetainExistingClampingAndCorrectSerialization(int rank)
    {
        foreach (var form in new[] {false,true}) {
            Assert.Equal(0m,PlagueCost(Request(rank,form,0m)));
            Assert.Equal(0m,PlagueCost(Request(rank,form,-10m)));
            var r = Request(rank,form,123.456m);
            var expected = form ? new[]{61.728m,30.864m,0m}[rank] : new[]{123.456m,92.592m,61.728m}[rank];
            Assert.Equal(expected,PlagueCost(r));
            var serialized = JsonSerializer.Serialize(r);
            Assert.Equal(expected,PlagueCost(JsonSerializer.Deserialize<ShadowPriestSimRequest>(serialized)!));
            if (!form || rank == 0) {
                // Exact old operation ordering is an oracle for unchanged decimal scale, not a new formula.
                var old = Math.Max(0m,r.Tuning.DevouringPlagueMana*(1m-.25m*rank)*(form?.5m:1m));
                Assert.Equal(JsonSerializer.Serialize(old),JsonSerializer.Serialize(PlagueCost(r)));
            }
        }
    }

    [Fact]
    public void ValidationBoundariesRemainUnchanged()
    {
        var r = Request(); r.Talents.Ranks["devouring-contagion"] = 3;
        Assert.Throws<ArgumentException>(() => new ShadowPriestSimRunner().Run(r));
        r = Request(); r.Tuning.DevouringPlagueTick = -1m;
        Assert.Throws<ArgumentException>(() => Definitions(r));
        r = Request(); r.Talents.Ranks["shadowform"] = 2;
        Assert.Throws<ArgumentException>(() => new ShadowPriestSimRunner().Run(r));
    }

    [Theory][MemberData(nameof(CostCases))]
    public void FullRunnerManaLedgerUsesLiteralCastDebits(decimal cost, int rank, bool form, decimal expected)
    {
        var r = Request(rank,form,cost); var result = new ShadowPriestSimRunner().Run(r);
        var starts = result.Run.Timeline.Where(e => e.Type == CombatEventType.AbilityCastStarted && e.AbilityKey == Plague).ToArray();
        Assert.Equal(new[]{0m,24m,48m},starts.Select(e => e.TimeSeconds));
        var debits = result.Run.Timeline.Where(e => e.Type == CombatEventType.ResourceChanged && e.AbilityKey == Plague).ToArray();
        Assert.Equal(3,debits.Length); Assert.All(debits,e => Assert.Equal(-expected,e.Amount));
        Assert.Equal(result.MaximumMana-3m*expected,result.EndingMana);
    }

    [Theory][InlineData(0)][InlineData(1)][InlineData(2)]
    public async Task PreparationPreviewSerializationAndConcurrentReuseRemainIsolated(int rank)
    {
        var r = Request(rank,true,400m); r.Character.Race = "human"; r.UseProvisionalLevel60Baseline = true;
        r.Character.Equipment.Items.Add(new(){SlotKey="head",ItemKey="plague-cost-budget",Stats=new(){Values=new(StringComparer.Ordinal){["intellect"]=100m,["spell-power"]=20m}}});
        var before = JsonSerializer.Serialize(r); var preview = ShadowPriestSimRunner.Preview(r);
        var h = new Harness(r,1000m,3.1m); Assert.Equal(new[]{200m,100m,0m}[rank],h.Actor.Abilities[Plague].Definition.ResourceCosts.Single().Amount);
        var result = new ShadowPriestSimRunner().Run(r);
        var replay = new ShadowPriestSimRunner().Run(JsonSerializer.Deserialize<ShadowPriestSimRequest>(before)!);
        Assert.Equal(Canonical(result),Canonical(replay));
        Assert.Equal(result.MaximumMana,preview.MaximumMana); Assert.Equal(result.MaximumHealth,preview.MaximumHealth);
        var repeated = await Task.WhenAll(Enumerable.Range(0,3).Select(_ => Task.Run(() => new ShadowPriestSimRunner().Run(r))));
        Assert.All(repeated,x => Assert.Equal(Canonical(result),Canonical(x)));
        Assert.Equal(Canonical(preview),Canonical(ShadowPriestSimRunner.Preview(r)));
        preview.EffectiveStats.Set("intellect",99999m);
        Assert.NotEqual(99999m,ShadowPriestSimRunner.Preview(r).EffectiveStats.Get("intellect"));
        Assert.Equal(before,JsonSerializer.Serialize(r));
        Assert.Equal(StringComparer.Ordinal,r.Character.Equipment.Items[0].Stats.Values.Comparer);
    }

    private static ForeverLevel60SimulationRequest Strict(int rank, bool form, decimal cost)
    {
        var r = Request(rank,form,cost); r.Character.Race = "human"; r.UseProvisionalLevel60Baseline = true;
        r.Talents.DisciplinePointsSpent = 0; r.Talents.HolyPointsSpent = 0;
        r.Talents.ShadowPointsSpent = r.Talents.Ranks.Values.Sum();
        return new() {ClassKey="priest",Specialization="shadow",ItemizedTalentMode=true,
            ItemizedTalentRanks=new(r.Talents.Ranks),Shadow=r};
    }

    [Theory][MemberData(nameof(CostCases))]
    public async Task StrictLegalDefaultAndCustomMapsReplayWithoutFillingUnspentPoints(decimal cost,int rank,bool form,decimal expected)
    {
        var q = Strict(rank,form,cost); var before = JsonSerializer.Serialize(q);
        var controller = new ForeverLevel60SimulationController();
        var first = Assert.IsType<OkObjectResult>(await controller.Simulate(q,default));
        var replay = Assert.IsType<OkObjectResult>(await controller.Simulate(JsonSerializer.Deserialize<ForeverLevel60SimulationRequest>(before)!,default));
        Assert.Equal(Canonical(first.Value),Canonical(replay.Value)); Assert.Equal(before,JsonSerializer.Serialize(q));
        Assert.Equal(36-(2-rank)-(form?0:1),q.Shadow!.Talents.ShadowPointsSpent);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(first.Value,new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var sim = json.RootElement.GetProperty("sim");
        var debits = sim.GetProperty("run").GetProperty("timeline").EnumerateArray().Where(e =>
            e.GetProperty("type").GetInt32() == (int)CombatEventType.ResourceChanged &&
            e.TryGetProperty("abilityKey",out var key) && key.GetString() == Plague).ToArray();
        Assert.Equal(3,debits.Length); Assert.All(debits,e => Assert.Equal(-expected,e.GetProperty("amount").GetDecimal()));
        Assert.Equal(sim.GetProperty("maximumMana").GetDecimal()-3m*expected,sim.GetProperty("endingMana").GetDecimal());
    }

    [Theory][InlineData("mismatch")][InlineData("overrank")][InlineData("total")]
    public async Task StrictInvalidMapsStillFail(string mode)
    {
        var q = Strict(2,true,790m);
        if(mode=="mismatch") q.ItemizedTalentRanks!["devouring-contagion"] = 1;
        if(mode=="overrank") {q.ItemizedTalentRanks!["devouring-contagion"] = 3;q.Shadow!.Talents.Ranks["devouring-contagion"] = 3;q.Shadow.Talents.ShadowPointsSpent++;}
        if(mode=="total") q.Shadow!.Talents.ShadowPointsSpent++;
        Assert.IsType<BadRequestObjectResult>(await new ForeverLevel60SimulationController().Simulate(q,default));
    }

    private sealed class Harness : ICombatEventProcessor
    {
        public List<CombatEvent> ProcessedEvents {get;} = [];
        public SimulationContext Context {get;}
        public SimulationActorState Actor {get;}
        public ResourceState Mana => Actor.Resources["mana"];
        public AbilityExecutor Executor {get;}
        private readonly AuraManager _auras = new();
        private readonly FiveSecondManaRegenerationProcessor _regen;
        private readonly Dictionary<string,Action> _actions = new();
        public Harness(ShadowPriestSimRequest request,decimal startingMana,decimal duration,decimal spirit=0m)
        {
            var clone = JsonSerializer.Deserialize<ShadowPriestSimRequest>(JsonSerializer.Serialize(request))!;
            clone.DurationSeconds = duration;
            var prepared = typeof(ShadowPriestSimRunner).GetMethod("Prepare",BindingFlags.Static|BindingFlags.NonPublic)!
                .Invoke(null,[clone,default(CancellationToken)])!;
            Context = (SimulationContext)prepared.GetType().GetProperty("Context")!.GetValue(prepared)!;
            Actor = (SimulationActorState)prepared.GetType().GetProperty("Actor")!.GetValue(prepared)!;
            Mana.SetCurrent(startingMana);
            Executor = new(_auras,new Hits(),new NoMitigation());
            _regen = new("priest",spirit,0m,0m); _regen.Initialize(Context);
        }
        public void At(decimal time,Action action) {
            var key = "plague-test-"+_actions.Count; _actions.Add(key,action);
            Context.ScheduleEvent(new(){TimeSeconds=time,Type=CombatEventType.RotationDecision,SourceActorKey="priest",EffectKey=key,IsInternal=true});
        }
        public void Cast(string key) => Assert.True(Executor.TryStartAbility(Context,"priest","target",key).Success);
        public void Process(SimulationContext context,CombatEvent e) {ProcessedEvents.Add(e);if(e.EffectKey is {} key && _actions.TryGetValue(key,out var action)) action();}
        public void Run() {new SimulationEngine([Executor,_auras,_regen,this]).Run(Context);Mana.AdvanceTo(Context.Options.DurationSeconds);}
    }
    private sealed class Hits : ICombatRollResolver
    {
        public CombatRollResult Resolve(SimulationContext context,SimulationActorState source,SimulationActorState target,AbilityDefinition ability,AbilityEffectDefinition effect) =>
            new(){Landed=true,ResultKey=CombatResultTypes.Hit,AmountMultiplier=1m};
    }
    private sealed class NoMitigation : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext context,SimulationActorState source,SimulationActorState target,AbilityDefinition ability,AbilityEffectDefinition effect,decimal rawAmount) => DamageMitigationResult.Unmitigated(rawAmount);
    }
    // Preserve all fields and joins, normalizing only generated IDs as in the established Alacrity tests.
    private static string Canonical(object? value)
    {
        var node = JsonNode.Parse(JsonSerializer.Serialize(value)); var ids = new Dictionary<string,string>();
        void Visit(JsonNode? n,string parent="") {
            if(n is JsonObject o) foreach(var kv in o.ToArray()) {
                if((kv.Key is "AbilityExecutionId" or "AuraInstanceId" || kv.Key=="Id" && parent is "Rotation" or "configuredRotation") &&
                   kv.Value is JsonValue v && v.TryGetValue<string>(out var id) && Guid.TryParse(id,out _)) {
                    if(!ids.TryGetValue(id,out var mapped)) {mapped="generated-"+ids.Count;ids[id]=mapped;}o[kv.Key]=mapped;
                } else Visit(kv.Value,kv.Key);
            } else if(n is JsonArray a) foreach(var child in a) Visit(child);
        }
        Visit(node);return node!.ToJsonString();
    }
}
