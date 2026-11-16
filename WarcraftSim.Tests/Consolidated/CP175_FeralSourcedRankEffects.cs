using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Druid;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>Historical pinned Druid tree, not released-client certification.
/// Literal rank oracles are independent of implementation. Periodic/auto crit
/// scope and all base/AP coefficients retain the existing provisional model.</summary>
public sealed class Cp175FeralSourcedRankEffects
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private static FeralDruidSimRequest Request(int fury = 0, int claws = 0, string role = "cat") => new() {
        Role = role, DurationSeconds = 60, CaptureTimeline = true, Seed = 175601,
        Talents = new() { Ranks = new() { ["savage-fury"] = fury, ["sharpened-claws"] = claws } },
        Tuning = new() { MeleeHitPercent = 100, MeleeCritPercent = 0, OmenProcPercent = 0 }
    };
    private static string Fingerprint(FeralDruidSimResponse response)
    {
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response, Web));
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) WriteCanonical(writer,json.RootElement);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
    private static void WriteCanonical(Utf8JsonWriter writer,JsonElement value)
    {
        switch(value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach(var property in value.EnumerateObject()) { writer.WritePropertyName(property.Name); WriteCanonical(writer,property.Value); }
                writer.WriteEndObject();break;
            case JsonValueKind.Array:
                writer.WriteStartArray();foreach(var item in value.EnumerateArray())WriteCanonical(writer,item);writer.WriteEndArray();break;
            case JsonValueKind.Number:
                writer.WriteRawValue(value.GetDecimal().ToString("G29",System.Globalization.CultureInfo.InvariantCulture));break;
            case JsonValueKind.String:
                var text=value.GetString();writer.WriteStringValue(Guid.TryParse(text,out _)?"INSTANCE-ID":text);break;
            default:value.WriteTo(writer);break;
        }
    }

    [Fact]
    public void PinnedSourceReceiptPreservesExactRankArraysAndNamedScope()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName,"WarcraftSim.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        using var receipt = JsonDocument.Parse(File.ReadAllText(Path.Combine(root.FullName,
            "docs/audits/CP175-feral-sourced-ranks-source.json")));
        var json = receipt.RootElement;
        Assert.Equal("1cc5ae1e9abdbf941b7326d2d90421822dce31ba",json.GetProperty("pinnedCommit").GetString());
        Assert.Equal("historical-provisional-not-current-client-certified",json.GetProperty("sourceStatus").GetString());
        var talents = json.GetProperty("selectedTalents").EnumerateArray().ToArray();
        Assert.Equal(2,talents.Length);
        var savage = talents.Single(t=>t.GetProperty("name").GetString()=="Savage Fury");
        var sharpened = talents.Single(t=>t.GetProperty("name").GetString()=="Sharpened Claws");
        Assert.Equal(new[]{5,10},savage.GetProperty("ranks").EnumerateArray().Select(r=>r[0].GetInt32()));
        Assert.Equal(new[]{3,6},sharpened.GetProperty("ranks").EnumerateArray().Select(r=>r[0].GetInt32()));
        Assert.All(talents,t=>Assert.Equal(2,t.GetProperty("maxPoints").GetInt32()));
        Assert.Equal("Increases the damage caused by your Claw, Rake, Shred, Maul, and Swipe abilities by {0}%.",savage.GetProperty("description").GetString());
    }

    // Use actual runner roll/armor adapters without exposing a production API.
    // No combat processor is needed for one independent, fully funded cast.
    private sealed class Harness
    {
        public SimulationContext Context { get; }
        public SimulationActorState Actor { get; }
        public ICombatRollResolver Rolls { get; }
        private readonly AuraManager auras = new();
        private readonly AbilityExecutor executor;
        public Harness(FeralDruidSimRequest r, int seed = 1)
        {
            Context = new(new() { Seed = seed, DurationSeconds = 12, CaptureTimeline = true, PrimaryActorKey = "druid" });
            Actor = new() { Key = "druid", TeamKey = "raid", Level = 60, MaximumHealth = 5000 };
            Actor.InitializeHealth(5000);
            Actor.AddResource(new ResourceState("energy", 100, 100));
            Actor.AddResource(new ResourceState("rage", 100, 100));
            Actor.AddResource(new ResourceState("mana", 5000, 5000));
            Context.AddActor(Actor);
            var target = new SimulationActorState { Key = "target", TeamKey = "enemy", Level = 63, MaximumHealth = 1000000 };
            target.InitializeHealth(1000000); Context.AddActor(target);
            object Adapter(string name, params object[] args) => Activator.CreateInstance(
                typeof(FeralDruidSimRunner).GetNestedType(name, BindingFlags.NonPublic)!,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, args, null)!;
            var mechanicsType = typeof(FeralDruidSimRunner).Assembly.GetType("WarcraftSim.Api.Simulation.FeralDruidCombatProcessor")!;
            var noMechanics = Expression.Lambda(typeof(Func<>).MakeGenericType(mechanicsType),
                Expression.Constant(null, mechanicsType)).Compile();
            Rolls = (ICombatRollResolver)Adapter("FeralRolls", r, noMechanics);
            executor = new(auras, Rolls, (IDamageMitigationResolver)Adapter("FeralArmorMitigation", r.TargetArmor, r.Tuning));
            foreach (var ability in ForeverFeralDruidSpellbook.Create(r.Talents, r.Tuning)) Actor.AddAbility(ability);
            // The existing runner uses these same animal-form/prowl aura gates.
            auras.ApplyAura(Context, Actor, new() { Key = r.Role == "cat" ? "druid-cat-form" : "druid-dire-bear-form", DurationSeconds = 60 }, "druid", "shapeshift", "form");
            auras.ApplyAura(Context, Actor, new() { Key = "druid-prowl", DurationSeconds = 60 }, "druid", "prowl", "state");
        }
        public CombatEvent[] Cast(string key) => new SimulationEngine([executor, auras]).Run(Context, ctx =>
            Assert.True(executor.TryStartAbility(ctx, "druid", "target", key).Success)).Timeline
            .Where(e => e.Type == CombatEventType.Damage && e.AbilityKey == key).ToArray();
    }

    public static IEnumerable<object[]> DamageCases()
    {
        // Rank0/1/2 absolute amounts. No expected value reads a production formula.
        var rows = new (string ability, string effect, string role, decimal[] amounts)[] {
            ("claw", "claw-hit", "cat", [295m,309.75m,324.5m]),
            ("shred", "shred-hit", "cat", [468.75m,492.1875m,515.625m]),
            ("rake", "rake-hit", "cat", [120m,126m,132m]),
            ("rake", "rake-tick", "cat", [64m,67.2m,70.4m]),
            ("maul", "maul-hit", "bear", [307.5m,322.875m,338.25m]),
            ("swipe", "swipe-hit", "bear", [114m,119.7m,125.4m]),
            ("ravage", "ravage-hit", "cat", [485m,485m,485m]),
            ("rip", "rip-tick", "cat", [45m,45m,45m]),
            ("ferocious-bite", "bite-hit", "cat", [200m,200m,200m]),
            ("pounce", "pounce-bleed", "cat", [60m,60m,60m]),
            ("lacerate", "lacerate-impact", "bear", [18.75m,18.75m,18.75m]),
            ("lacerate", "lacerate-tick", "bear", [24m,24m,24m])
        };
        foreach (var row in rows)
            for (var rank = 0; rank <= 2; rank++)
                foreach (var critical in new[] { false, true })
                    foreach (var armor in new[] { false, true })
                        yield return [row.ability, row.effect, row.role, rank, critical, armor, row.amounts[rank]];
    }

    [Theory][MemberData(nameof(DamageCases))]
    public void SavageFuryNamedAndNegativeComponentsHaveExactRanksAndRealDamage(
        string ability, string effect, string role, int rank, bool critical, bool armor, decimal amount)
    {
        var r = Request(rank, role: role); r.Tuning.MeleeCritPercent = critical ? 100 : 0;
        r.TargetArmor = armor ? 10557.5m : 0;
        var h = new Harness(r);
        var definition = h.Actor.Abilities[ability].Definition.Effects.Single(e => e.Key == effect);
        Assert.Equal(amount, definition.MinimumValue); Assert.Equal(amount, definition.MaximumValue);
        var events = h.Cast(ability).Where(e => e.EffectKey == effect).ToArray();
        Assert.NotEmpty(events);
        var periodic = definition.EffectType == AbilityEffectTypes.PeriodicDamage;
        Assert.All(events, e => {
            var raw = amount * (critical ? 2m : 1m);
            Assert.Equal(raw, e.RawAmount);
            Assert.Equal(raw * (armor && !periodic ? .5m : 1m), e.Amount);
            Assert.Equal(periodic, e.IsPeriodic); Assert.Equal(critical, e.IsCritical);
            Assert.Equal("druid", e.SourceActorKey); Assert.Equal("target", e.TargetActorKey);
        });
    }

    [Theory][InlineData(1)][InlineData(2)]
    public void PrimalBiteKeepsItsOwnDamageDespiteRequiredSavageFury(int rank)
    {
        var r = Request(rank, role: "bear"); r.Talents.Ranks["primal-bite"] = 1;
        Assert.Equal(367.5m, Assert.Single(new Harness(r).Cast("primal-bite")).Amount);
    }

    [Theory][InlineData(0,10,148.2)][InlineData(1,10,155.61)][InlineData(2,10,163.02)]
    [InlineData(2,0,125.4)][InlineData(2,7,151.734)]
    public void SavageFuryCompoundsWithAuthoredFeralInstinctIncludingZero(int rank, int perRank, double expected)
    {
        var r = Request(rank, role: "bear"); r.Talents.Ranks["feral-instinct"] = 3;
        r.Tuning.FeralInstinctSwipeDamagePercentPerRank = perRank;
        Assert.Equal((decimal)expected, Assert.Single(new Harness(r).Cast("swipe")).Amount);
    }

    [Theory][InlineData(0)][InlineData(1)][InlineData(2)]
    public void AuthoredDamageInputsAndExplicitZerosAreNotReplacedByDefaults(int rank)
    {
        var r = Request(rank);
        r.Tuning.WeaponDps = 0; r.Tuning.AttackPower = 0;
        r.Tuning.CatClawBonus = 13; r.Tuning.CatShredBonus = 17;
        r.Tuning.CatRakeDamage = 19; r.Tuning.CatRakeTick = 23;
        r.Tuning.BearMaulBonus = 29; r.Tuning.BearSwipeBase = 31;
        r.Tuning.CatOpenersBonus = 37;
        var before = JsonSerializer.Serialize(r);
        var factors = new[] {1m,1.05m,1.10m};
        var book = ForeverFeralDruidSpellbook.Create(r.Talents, r.Tuning);
        foreach (var pair in new[] { ("claw-hit",13m),("shred-hit",17m),("rake-hit",19m),("rake-tick",23m),("maul-hit",29m),("swipe-hit",31m) })
            Assert.Equal(pair.Item2 * factors[rank], book.SelectMany(a => a.Effects).Single(e => e.Key == pair.Item1).MinimumValue);
        Assert.Equal(37m, book.Single(a => a.Key == "ravage").Effects[0].MinimumValue);
        Assert.Equal(before, JsonSerializer.Serialize(r));
        r.Tuning.CatClawBonus = r.Tuning.CatShredBonus = r.Tuning.CatRakeDamage = r.Tuning.CatRakeTick = r.Tuning.BearMaulBonus = r.Tuning.BearSwipeBase = 0;
        var zeros = ForeverFeralDruidSpellbook.Create(r.Talents, r.Tuning);
        Assert.All(zeros.Where(a => new[] {"claw","shred","rake","maul","swipe"}.Contains(a.Key)).SelectMany(a => a.Effects), e => Assert.Equal(0m,e.MinimumValue));
    }

    // The independent System.Random oracle locates a narrow roll interval, not a
    // statistical DPS assertion. Direct melee consumes hit then crit; a periodic
    // tick consumes only crit. Each harness uses the selected seed from the start.
    private static int SeedBetween(decimal lower, decimal upper, bool canMiss)
    {
        for (var seed = 0; seed < 100000; seed++)
        {
            var random = new Random(seed); if (canMiss) random.NextDouble();
            var roll = (decimal)random.NextDouble() * 100m;
            if (roll > lower && roll < upper) return seed;
        }
        throw new InvalidOperationException("No deterministic seed in test interval.");
    }

    public static IEnumerable<object[]> CritCases()
    {
        foreach (var role in new[] {"cat","bear"})
            foreach (var periodic in new[] {false,true})
                foreach (var leader in new[] {false,true})
                    for (var rank=0; rank<=2; rank++)
                        foreach (var baseCrit in new[] {0m,20m,67m})
                            foreach (var below in new[] {false,true})
                                yield return [role,periodic,leader,rank,baseCrit,below];
    }

    [Theory][MemberData(nameof(CritCases))]
    public void SharpenedClawsExactPercentagePointsPreserveBaseAuraAndCurrentPeriodicScope(
        string role, bool periodic, bool leader, int rank, decimal baseCrit, bool below)
    {
        var r = Request(claws: rank, role: role); r.Tuning.MeleeCritPercent = baseCrit;
        if (leader) r.Talents.Ranks["leader-of-the-pack"] = 1;
        var chance = baseCrit + new[] {0m,3m,6m}[rank] + (leader ? 3m : 0m);
        // At 0%, use a small positive roll: no impossible negative roll is needed.
        var lower = below && chance > 0 ? chance - .1m : chance;
        var upper = below && chance > 0 ? chance : chance + .1m;
        var seed = SeedBetween(lower,upper,!periodic);
        var h = new Harness(r,seed);
        var ability = h.Actor.Abilities[role == "cat" ? "rake" : "lacerate"].Definition;
        var effect = ability.Effects.Single(e => (e.EffectType == AbilityEffectTypes.PeriodicDamage) == periodic);
        var result = h.Rolls.Resolve(h.Context,h.Actor,h.Context.Actors["target"],ability,effect,
            periodic ? CombatEffectDeliveryType.Periodic : CombatEffectDeliveryType.Direct);
        Assert.True(result.Landed); Assert.Equal(below && chance > 0,result.IsCritical);
        Assert.Equal(below && chance > 0 ? 2m : 1m,result.AmountMultiplier);
        // No new RNG consumption; periodic still skips hit, direct still uses two rolls.
        var oracle = new Random(seed); if (!periodic) oracle.NextDouble(); oracle.NextDouble();
        Assert.Equal(oracle.NextDouble(),h.Context.Random.NextDouble());
    }

    [Theory][InlineData("cat",0,false)][InlineData("cat",1,true)][InlineData("cat",2,true)]
    [InlineData("bear",0,false)][InlineData("bear",1,true)][InlineData("bear",2,true)]
    public void ControlledTwentyTwoToTwentyThreeRollChangesRealCriticalEvent(string role,int rank,bool critical)
    {
        var r = Request(claws: rank,role: role); r.Tuning.MeleeCritPercent = 20;
        r.TargetArmor = 10557.5m;
        var h = new Harness(r,SeedBetween(22,23,true));
        var e = Assert.Single(h.Cast(role == "cat" ? "claw" : "maul"));
        var amount = role == "cat" ? 295m : 307.5m;
        Assert.Equal(critical,e.IsCritical); Assert.Equal(amount * (critical ? 2m : 1m),e.RawAmount);
        Assert.Equal(amount * (critical ? 1m : .5m),e.Amount);
    }

    [Theory][InlineData(0)][InlineData(1)][InlineData(2)]
    public void MissAndCertainCritRemainAuthoredAndConsumeExistingRollsOnly(int rank)
    {
        var r = Request(claws: rank); r.Tuning.MeleeHitPercent=0; r.Tuning.MeleeCritPercent=100;
        var h = new Harness(r,173); var ability=h.Actor.Abilities["claw"].Definition;
        var result=h.Rolls.Resolve(h.Context,h.Actor,h.Context.Actors["target"],ability,ability.Effects[0]);
        Assert.False(result.Landed); Assert.False(result.IsCritical);
        var oracle=new Random(173);oracle.NextDouble();Assert.Equal(oracle.NextDouble(),h.Context.Random.NextDouble());
        r.Tuning.MeleeHitPercent=100;
        Assert.True(Assert.Single(new Harness(r).Cast("claw")).IsCritical);
    }

    [Theory][InlineData("cat",false,"299C519A2C8477AFFDBE96ABC2DECFE85F160A03940BC59CBDCF49D64BF8DC37")]
    [InlineData("cat",true,"0F6433A67B17F8865721A587F54289F565080F56364B23C9F49F382E3065C5BD")]
    [InlineData("bear",false,"58EE03E175E647148C1DB003A595C74B44E34025FB7747D0127228EBD8ED1242")]
    [InlineData("bear",true,"D6D22E4267665FBBE8AB9ACA52A66B8A317299B501764C5BBFEACC47935928FC")]
    public void RankZeroFullCombatFingerprintsRemainExactlyPreEdit(string role,bool hotw,string expected)
    {
        // Captured with frozen original 5536d000 DLLs, not regenerated expectations.
        var r=new FeralDruidSimRequest {Role=role,Seed=175601,DurationSeconds=60,CaptureTimeline=true};
        if(hotw)r.Talents.Ranks["heart-of-the-wild"]=5;
        Assert.Equal(expected,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Fingerprint(new FeralDruidSimRunner().Run(r))))));
    }

    [Theory][InlineData("cat",0)][InlineData("cat",1)][InlineData("cat",2)]
    [InlineData("bear",0)][InlineData("bear",1)][InlineData("bear",2)]
    public void SavageFuryChangesOnlyNamedDamageInPublicRunnerAndNeverWhiteSwings(string role,int rank)
    {
        var r=Request(role:role);if(role=="bear")r.AdditionalTargets=2;
        var baseline=new FeralDruidSimRunner().Run(r);
        r.Talents.Ranks["savage-fury"]=rank;var ranked=new FeralDruidSimRunner().Run(r);
        var oldEvents=baseline.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage).ToArray();
        var newEvents=ranked.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage).ToArray();
        Assert.Equal(oldEvents.Length,newEvents.Length);
        Assert.Contains(newEvents,e=>e.AbilityKey=="feral-auto" && e.Amount>0);
        Assert.Contains(newEvents,e=>e.AbilityKey==(role=="cat"?"rake":"swipe") && e.Amount>0);
        for(var i=0;i<oldEvents.Length;i++)
        {
            var before=oldEvents[i];var after=newEvents[i];
            Assert.Equal((before.TimeSeconds,before.AbilityKey,before.EffectKey,before.TargetActorKey,before.ResultKey),
                (after.TimeSeconds,after.AbilityKey,after.EffectKey,after.TargetActorKey,after.ResultKey));
            var multiplier=new[]{"claw","rake","shred","maul","swipe","swipe-secondary"}.Contains(after.AbilityKey)?new[]{1m,1.05m,1.1m}[rank]:1m;
            Assert.Equal(before.Amount*multiplier,after.Amount);
        }
        Assert.Equal(baseline.EndingEnergy,ranked.EndingEnergy);Assert.Equal(baseline.EndingRage,ranked.EndingRage);
        Assert.Equal(baseline.EndingMana,ranked.EndingMana);Assert.Equal(baseline.Run.ProcessedEventCount,ranked.Run.ProcessedEventCount);
    }

    [Theory][InlineData("cat")][InlineData("bear")]
    public void ZeroAuthoredAttributesWithHeartOfTheWildNeverAddDefaultConversions(string role)
    {
        var r=Request(2,2,role);r.MaximumHealth=5000;r.MaximumMana=3000;
        r.Tuning.AttackPower=0;r.Tuning.WeaponDps=0;
        r.Tuning.BaseStrength=r.Tuning.BaseStamina=r.Tuning.BaseIntellect=0;
        r.Talents.Ranks["heart-of-the-wild"]=5;
        var run=new FeralDruidSimRunner().Run(r);
        Assert.Equal(0,run.Run.CustomMetrics["feral-heart-of-the-wild-attack-power"]);
        Assert.Equal(5000,run.Run.CustomMetrics["feral-heart-of-the-wild-maximum-health"]);
        Assert.Equal(3000,run.Run.CustomMetrics["feral-heart-of-the-wild-maximum-mana"]);
        Assert.Equal(2750,run.EndingMana);
        Assert.DoesNotContain(run.Run.Timeline,e=>e.AbilityKey=="feral-auto" && e.Amount>0);
    }

    [Theory][InlineData("cat",420,5000)][InlineData("bear",400,5200)]
    public async Task InclusiveHeartOfTheWildBudgetsRemovalAndCallerIsolationRemainIntact(string role,int ap,int health)
    {
        var r=Request(2,2,role);r.MaximumHealth=5000;r.MaximumMana=3000;
        r.Tuning.AttackPower=400;r.Tuning.BaseStrength=r.Tuning.BaseStamina=r.Tuning.BaseIntellect=100;
        r.Talents.Ranks["heart-of-the-wild"]=5;
        var original=JsonSerializer.Serialize(r);var runner=new FeralDruidSimRunner();
        var result=runner.Run(r);
        Assert.Equal(ap,result.Run.CustomMetrics["feral-heart-of-the-wild-attack-power"]);
        Assert.Equal(health,result.Run.CustomMetrics["feral-heart-of-the-wild-maximum-health"]);
        Assert.Equal(3150,result.Run.CustomMetrics["feral-heart-of-the-wild-maximum-mana"]);
        Assert.Equal(2900,result.EndingMana);
        var runs=await Task.WhenAll(Enumerable.Range(0,4).Select(_=>Task.Run(()=>runner.Run(r))));
        Assert.All(runs,x=>Assert.Equal(Fingerprint(result),Fingerprint(x)));
        Assert.Equal(original,JsonSerializer.Serialize(r));
        r.Talents.Ranks.Remove("savage-fury");r.Talents.Ranks.Remove("sharpened-claws");
        var removed=runner.Run(r);var fresh=JsonSerializer.Deserialize<FeralDruidSimRequest>(JsonSerializer.Serialize(r))!;
        Assert.Equal(Fingerprint(removed),Fingerprint(runner.Run(fresh)));
        // Health/Mana budgets do not leak into damage or crit calculations.
        r.MaximumHealth=9000;r.MaximumMana=7000;
        var larger=runner.Run(r);
        Assert.Equal(removed.DamageDone,larger.DamageDone);
        Assert.Equal(JsonSerializer.Serialize(removed.DamageByAbility),JsonSerializer.Serialize(larger.DamageByAbility));
    }
}
