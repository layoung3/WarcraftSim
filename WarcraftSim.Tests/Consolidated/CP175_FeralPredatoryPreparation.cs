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

/// <summary>Independent literal AP/damage oracles; historical source, not full Feral certification.</summary>
public sealed class Cp175FeralPredatoryPreparation
{
    private static FeralDruidSimRequest Request(int rank = 0, string role = "cat") => new() {
        Role = role, DurationSeconds = 45, CaptureTimeline = true, Seed = 175611,
        Talents = new() { Ranks = new() { ["predatory-strikes"] = rank } },
        Tuning = new() { MeleeHitPercent = 100, MeleeCritPercent = 0, OmenProcPercent = 0 }
    };
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
            foreach (var ability in FeralDruidSimRunner.Preview(r).Abilities) Actor.AddAbility(ability);
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
        var rows = new (string Ability, string Effect, string Role, decimal[] Amounts)[] {
            ("claw", "claw-hit", "cat", [295m,299.5m,304m,308.5m]),
            ("shred", "shred-hit", "cat", [468.75m,471.75m,474.75m,477.75m]),
            ("rake", "rake-hit", "cat", [120m,121.5m,123m,124.5m]),
            ("rake", "rake-tick", "cat", [64m,64.9m,65.8m,66.7m]),
            ("swipe", "swipe-hit", "bear", [114m,114.9m,115.8m,116.7m]),
            ("maul", "maul-hit", "bear", [307.5m,307.5m,307.5m,307.5m]),
            ("ravage", "ravage-hit", "cat", [485m,485m,485m,485m]),
            ("rip", "rip-tick", "cat", [45m,45m,45m,45m]),
            ("ferocious-bite", "bite-hit", "cat", [200m,200m,200m,200m]),
            ("pounce", "pounce-bleed", "cat", [60m,60m,60m,60m]),
            ("lacerate", "lacerate-impact", "bear", [18.75m,18.75m,18.75m,18.75m]),
            ("lacerate", "lacerate-tick", "bear", [24m,24m,24m,24m])
        };
        foreach (var row in rows)
            for (var rank = 0; rank <= 3; rank++)
                foreach (var critical in new[] { false, true })
                    foreach (var armor in new[] { false, true })
                        yield return [row.Ability,row.Effect,row.Role,rank,critical,armor,row.Amounts[rank]];
    }

    [Theory][MemberData(nameof(DamageCases))]
    public void ExistingConsumersAndNonconsumersHaveIndependentAbsoluteDamageOracles(
        string ability, string effect, string role, int rank, bool critical, bool armor, decimal amount)
    {
        var r = Request(rank,role); r.Tuning.MeleeCritPercent = critical ? 100 : 0;
        r.TargetArmor = armor ? 10557.5m : 0;
        var h = new Harness(r); var definition = h.Actor.Abilities[ability].Definition.Effects.Single(e => e.Key == effect);
        Assert.Equal(amount,definition.MinimumValue); Assert.Equal(amount,definition.MaximumValue);
        var periodic = definition.EffectType == AbilityEffectTypes.PeriodicDamage;
        var events = h.Cast(ability).Where(e => e.EffectKey == effect).ToArray(); Assert.NotEmpty(events);
        Assert.All(events,e => {
            Assert.Equal(amount * (critical ? 2m : 1m), e.RawAmount);
            Assert.Equal(amount * (critical ? 2m : 1m) * (armor && !periodic ? .5m : 1m), e.Amount);
            Assert.Equal(critical,e.IsCritical); Assert.Equal(periodic,e.IsPeriodic);
            Assert.Equal("druid",e.SourceActorKey); Assert.Equal("target",e.TargetActorKey);
        });
    }

    [Theory][InlineData(0,800)][InlineData(1,830)][InlineData(2,860)][InlineData(3,890)]
    public void LevelSixtyFlatBonusIsNotAnAuthoredApPercentAndApZeroStillGetsTalent(int rank,int expected)
    {
        var r = Request(rank); Assert.Equal(expected,FeralDruidSimRunner.Preview(r).SpellbookAttackPower);
        r.Tuning.AttackPower = 0;
        var p = FeralDruidSimRunner.Preview(r); Assert.Equal(0,p.AuthoredAttackPower);
        Assert.Equal(expected-800,p.SpellbookAttackPower); Assert.Equal(expected-800,p.PredatoryStrikesAttackPower); Assert.Equal(0,r.Tuning.AttackPower);
        r.Tuning.AttackPower = 1234; Assert.Equal(expected+434,FeralDruidSimRunner.Preview(r).SpellbookAttackPower);
    }

    [Theory][InlineData("cat",926,5500,836)][InlineData("bear",890,5940,800)]
    public void HotwAndPredatoryStaySeparateAndResourcesDoNotChange(string role,int ap,int health,int hotwAp)
    {
        var r = Request(3,role); r.Talents.Ranks["heart-of-the-wild"] = 5;
        var p = FeralDruidSimRunner.Preview(r); var run = new FeralDruidSimRunner().Run(r);
        Assert.Equal(ap,p.SpellbookAttackPower); Assert.Equal(health,p.MaximumHealth);
        Assert.Equal(4270,p.MaximumMana); Assert.Equal(4020,p.ManaAfterInitialShift);
        Assert.Equal(hotwAp,run.Run.CustomMetrics["feral-heart-of-the-wild-attack-power"]);
        Assert.Equal(hotwAp-800,p.HeartOfTheWildAttackPowerIncrement);
        Assert.Equal(800,r.Tuning.AttackPower);
    }

    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)]
    public void NaturalInstinctRemainsIndependentAndPrimalBiteStillIgnoresAp(int rank)
    {
        var r = Request(rank,"bear"); r.Talents.Ranks["savage-fury"] = 1;
        r.Talents.Ranks["primal-bite"] = 1; r.Tuning.MeleeCritPercent = 100;
        Assert.Equal(735m,Assert.Single(new Harness(r).Cast("primal-bite")).Amount);
        r.Talents.Ranks["natural-instinct"] = 2;
        Assert.Equal(808.5m,Assert.Single(new Harness(r).Cast("primal-bite")).Amount);
    }

    [Theory][InlineData(0,340.725)][InlineData(1,345.9225)][InlineData(2,351.12)][InlineData(3,356.3175)]
    public void SavageFuryStillCompoundsWithPredatoryConsumerAndIndependentNaturalInstinct(int rank,double expected)
    {
        var r = Request(rank); r.Talents.Ranks["savage-fury"] = 2;
        r.Talents.Ranks["natural-instinct"] = 1; r.Tuning.MeleeCritPercent = 100; r.TargetArmor = 10557.5m;
        // 295/299.5/304/308.5 * 1.10 * 2.10 * 0.50.
        Assert.Equal((decimal)expected,Assert.Single(new Harness(r).Cast("claw")).Amount);
    }

    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)]
    public void WhiteDamageDoesNotGainApAndLosesOnlyUnsupportedCritMultiplier(int rank)
    {
        var r = Request(rank); r.DurationSeconds = 1.1m;
        Assert.Equal(75,Assert.Single(new FeralDruidSimRunner().Run(r).Run.Timeline,
            e => e.Type == CombatEventType.Damage && e.AbilityKey == "feral-auto").Amount);
        r.Tuning.MeleeCritPercent = 100;
        Assert.Equal(150,Assert.Single(new FeralDruidSimRunner().Run(r).Run.Timeline,
            e => e.Type == CombatEventType.Damage && e.AbilityKey == "feral-auto").Amount);
    }

    [Theory][InlineData("cat",0)][InlineData("cat",100)][InlineData("cat",31)]
    [InlineData("bear",0)][InlineData("bear",100)][InlineData("bear",31)]
    public void PredatoryNeverChangesRollDecisionsCostsResourceEventsOrEventOwnership(string role,int crit)
    {
        var r = Request(0,role); r.Tuning.MeleeCritPercent = crit;
        r.AdditionalTargets = 1; r.TargetTransitions.Add(new() { AtSeconds = 8, TargetKey = "target-1" });
        var a = new FeralDruidSimRunner().Run(r);
        r.Talents.Ranks["predatory-strikes"] = 3; var b = new FeralDruidSimRunner().Run(r);
        string Decisions(FeralDruidSimResponse value) => JsonSerializer.Serialize(value.Run.Timeline.Select(e => new {
            e.TimeSeconds,e.Type,e.SourceActorKey,e.TargetActorKey,e.AbilityKey,e.EffectKey,
            e.ResultKey,e.IsCritical,e.ResourceKey,e.ResourceTotalAfter,
            ResourceAmount = e.Type == CombatEventType.ResourceChanged ? e.Amount : 0
        }));
        Assert.Equal(Decisions(a),Decisions(b)); Assert.Equal(a.EndingMana,b.EndingMana);
        Assert.Equal(a.EndingEnergy,b.EndingEnergy); Assert.Equal(a.EndingRage,b.EndingRage);
        Assert.Equal(a.ComboPointsByTarget,b.ComboPointsByTarget);
        r.Talents.Ranks.Remove("predatory-strikes"); Assert.Equal(a.DamageDone,new FeralDruidSimRunner().Run(r).DamageDone);
    }

    [Theory][InlineData("cat")][InlineData("bear")]
    public void AllZeroAuthoredBaseTermsRemainZeroAndOnlyFlatBonusReachesExistingConsumers(string role)
    {
        var r = Request(3,role); r.Talents.Ranks["heart-of-the-wild"] = 5;
        r.Tuning.AttackPower = 0; r.Tuning.WeaponDps = 0;
        r.Tuning.BaseStrength = 0; r.Tuning.BaseStamina = 0; r.Tuning.BaseIntellect = 0;
        r.Tuning.CatClawBonus = 0; r.Tuning.CatShredBonus = 0;
        r.Tuning.CatRakeDamage = 0; r.Tuning.CatRakeTick = 0; r.Tuning.BearSwipeBase = 0;
        r.Tuning.BearMaulBonus = 0; r.Tuning.ShapeshiftMana = 0;
        var p = FeralDruidSimRunner.Preview(r);
        Assert.Equal(0,p.AuthoredAttackPower); Assert.Equal(0,p.HeartOfTheWildAttackPowerIncrement);
        Assert.Equal(90,p.PredatoryStrikesAttackPower); Assert.Equal(90,p.SpellbookAttackPower);
        Assert.Equal(5500,p.MaximumHealth); Assert.Equal(4000,p.MaximumMana);
        Assert.Equal(0,p.InitialShiftManaCost); Assert.Equal(4000,p.ManaAfterInitialShift);
        var expected = new Dictionary<string,decimal> { ["claw-hit"] = 13.5m, ["shred-hit"] = 9m,
            ["rake-hit"] = 4.5m, ["rake-tick"] = 2.7m, ["swipe-hit"] = 2.7m, ["maul-hit"] = 0m };
        foreach (var effect in p.Abilities.SelectMany(a => a.Effects).Where(e => expected.ContainsKey(e.Key)))
            Assert.Equal(expected[effect.Key],effect.MinimumValue);
        r.Tuning.CatClawBonus = 17; r.Tuning.WeaponDps = 3;
        Assert.Equal(33.5m,FeralDruidSimRunner.Preview(r).Abilities.Single(a => a.Key == "claw").Effects[0].MinimumValue);
        Assert.Equal(0,r.Tuning.AttackPower);
    }

    [Fact]
    public void SourceReceiptPinsExactRankArrayAndDoesNotClaimRawUpstreamVerification()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName,"WarcraftSim.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        using var receipt = JsonDocument.Parse(File.ReadAllText(Path.Combine(root.FullName,"docs/audits/CP175-feral-precombat-source.json")));
        var source = receipt.RootElement;
        Assert.Equal("1cc5ae1e9abdbf941b7326d2d90421822dce31ba",source.GetProperty("pinnedCommit").GetString());
        Assert.Equal("historical-provisional-not-current-client-certified",source.GetProperty("sourceStatus").GetString());
        Assert.Contains("not upstream raw",source.GetProperty("localReceiptHashMeaning").GetString());
        var talent = source.GetProperty("selectedTalent"); Assert.Equal(3,talent.GetProperty("maxPoints").GetInt32());
        Assert.Equal(new[] {50,100,150},talent.GetProperty("ranks").EnumerateArray().Select(r => r[0].GetInt32()));
        Assert.Equal("Increases your melee Attack Power in Cat Form, Bear Form, and Dire Bear Form by {0}% of your level.",talent.GetProperty("description").GetString());
    }
}
