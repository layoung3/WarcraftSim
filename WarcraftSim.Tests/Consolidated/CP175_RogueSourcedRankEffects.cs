using System.Reflection;
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Rogue;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>
/// Historical source: gunba/wow-forever-sim 1cc5ae1, rogue.json, not current-client
/// certification. Controlled amounts isolate rank/eligibility from provisional
/// base coefficients. See docs/audits/CP175-rogue-sourced-ranks.md.
/// </summary>
public sealed class Cp175RogueSourcedRankEffects
{
    private static RogueSimRequest Request(string talent, int rank)
    {
        var r = RogueSimRequest.CreatePreset("assassination");
        r.Talents = new() { AssassinationPointsSpent = 21, CombatPointsSpent = 6,
            SubtletyPointsSpent = 21, Ranks = new() { [talent] = rank } };
        r.StartInStealth = false; r.UseInstantPoison = false;
        r.Character.BaseStats = new(); r.Tuning.PhysicalCritPercent = 0;
        r.Tuning.MainHandHitPercent = 100; r.Tuning.OffHandHitPercent = 100;
        r.Tuning.EviscerateDamagePerPoint = 100;
        r.DurationSeconds = 120; r.CaptureTimeline = true;
        return r;
    }

    // Use the runner's actual adapters, without widening the production API just
    // for tests. The regular public runner is exercised separately below.
    private sealed class Harness
    {
        public SimulationContext Context { get; }
        public SimulationActorState Actor { get; }
        public RogueCombatProcessor Combat { get; }
        public AbilityExecutor Executor { get; }
        public AuraManager Auras { get; } = new();
        public Harness(RogueSimRequest r, int seed = 1)
        {
            Context = new(new() { Seed = seed, DurationSeconds = .5m, CaptureTimeline = true,
                PrimaryActorKey = "rogue" });
            Actor = new() { Key = "rogue", TeamKey = "raid", Level = 60, MaximumHealth = 4000 };
            Actor.InitializeHealth(4000); Actor.AddResource(new ResourceState("energy", 100, 100));
            Context.AddActor(Actor);
            foreach (var key in new[] { "target", "target-2" })
            {
                var target = new SimulationActorState { Key = key, TeamKey = "enemy", Level = 63,
                    MaximumHealth = 100000 };
                target.InitializeHealth(100000); Context.AddActor(target);
            }
            RogueCombatProcessor? combat = null;
            Func<RogueCombatProcessor?> getCombat = () => combat;
            object Adapter(string name) => Activator.CreateInstance(
                typeof(RogueSimRunner).GetNestedType(name, BindingFlags.NonPublic)!,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, [r, getCombat], null)!;
            Executor = new(Auras, (ICombatRollResolver)Adapter("RogueRollResolver"),
                (IDamageMitigationResolver)Adapter("RogueArmorMitigation"));
            Combat = combat = new(r, Actor, new RotationProfile(), Executor, Auras,
                new AutoAttackProcessor(Executor), []);
            foreach (var ability in ForeverRogueSpellbook.Create(r.Specialization, r.Talents,
                r.Tuning, 0, true)) Actor.AddAbility(ability);
        }
        public void Hit(string key = "sinister-strike", string target = "target", string source = "rogue",
            bool critical = false, bool periodic = false, decimal amount = 100, Guid? id = null)
            => Combat.Process(Context, new() { Type = CombatEventType.Damage,
                SourceActorKey = source, TargetActorKey = target, AbilityKey = key,
                AbilityExecutionId = id ?? Guid.NewGuid(), Amount = amount,
                ResultKey = amount == 0 ? CombatResultTypes.Miss : critical ? CombatResultTypes.Critical : CombatResultTypes.Hit,
                IsCritical = critical, EffectDeliveryType = periodic ? CombatEffectDeliveryType.Periodic : CombatEffectDeliveryType.Direct });
        public CombatEvent[] Cast(string key, string target = "target")
        {
            var result = new SimulationEngine([Executor, Auras, Combat]).Run(Context, ctx =>
                Assert.True(Executor.TryStartAbility(ctx, "rogue", target, key).Success));
            return result.Timeline.Where(e => e.Type == CombatEventType.Damage && e.AbilityKey == key).ToArray();
        }
    }

    public static IEnumerable<object[]> EviscerateCases()
    {
        var values = new[] { 100m, 107m, 113m, 120m };
        for (var rank = 0; rank <= 3; rank++)
            foreach (var points in new[] { 1, 5 })
                foreach (var critical in new[] { false, true })
                    foreach (var armor in new[] { false, true })
                        yield return [rank, points, critical, armor, values[rank]];
    }

    [Theory][MemberData(nameof(EviscerateCases))]
    public void EviscerateExactRankTableFlowsThroughRealHitCritArmorAndFinisherSpend(
        int rank, int points, bool critical, bool armor, decimal perPoint)
    {
        var r = Request("improved-eviscerate", rank);
        r.Tuning.PhysicalCritPercent = critical ? 100 : 0;
        r.TargetArmor = armor ? r.Tuning.ArmorDamageReductionConstant : 0;
        var h = new Harness(r);
        var effect = h.Actor.Abilities["eviscerate"].Definition.Effects[0];
        Assert.Equal(perPoint, effect.MinimumValue); Assert.Equal(perPoint, effect.MaximumValue);
        for (var i = 0; i < points; i++) h.Hit();
        h.Hit(target: "target-2"); h.Hit(target: "target-2");
        h.Hit(source: "another-rogue"); // Foreign source must not change the five-point oracle.
        var damage = Assert.Single(h.Cast("eviscerate"));
        var raw = perPoint * points * (critical ? 2m : 1m);
        Assert.Equal(raw, damage.RawAmount); Assert.Equal(raw * (armor ? .5m : 1m), damage.Amount);
        Assert.Equal(critical, damage.IsCritical);
        Assert.Equal("rogue", damage.SourceActorKey); Assert.Equal("target", damage.TargetActorKey);
        Assert.Equal(0, h.Combat.ComboPointsFor("target")); Assert.Equal(2, h.Combat.ComboPointsFor("target-2"));
        Assert.Equal(1, h.Combat.FinishersUsed);
        // The cast costs35; the half-second engine run then regenerates5.
        Assert.Equal(70m, h.Actor.Resources["energy"].Current);
        Assert.Contains(h.Context.Timeline, e => e.Type == CombatEventType.ResourceChanged &&
            e.ResourceKey == "energy" && e.Amount == -35m);
    }

    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)]
    public void EviscerateRankDoesNotChangeUnrelatedDefinitionsAndRemovalRestoresBase(int rank)
    {
        var r = Request("improved-eviscerate", rank);
        var ranked = ForeverRogueSpellbook.Create(r.Specialization, r.Talents, r.Tuning, 0, true);
        r.Talents.Ranks.Remove("improved-eviscerate");
        var removed = ForeverRogueSpellbook.Create(r.Specialization, r.Talents, r.Tuning, 0, true);
        Assert.Equal(100m, removed.Single(a => a.Key == "eviscerate").Effects[0].MinimumValue);
        Assert.Equal(JsonSerializer.Serialize(removed.Where(a => a.Key != "eviscerate")),
            JsonSerializer.Serialize(ranked.Where(a => a.Key != "eviscerate")));
        Assert.Equal(JsonSerializer.Serialize(removed.Single(a => a.Key == "eviscerate").ResourceCosts),
            JsonSerializer.Serialize(ranked.Single(a => a.Key == "eviscerate").ResourceCosts));
    }

    public static IEnumerable<object[]> LethalityCases()
    {
        foreach (var key in new[] { "sinister-strike", "backstab", "mutilate", "hemorrhage", "ghostly-strike", "ambush", "eviscerate", "rogue-main-hand" })
            for (var rank = 0; rank <= 5; rank++)
                foreach (var critical in new[] { false, true }) yield return [key, rank, critical];
    }

    [Theory][MemberData(nameof(LethalityCases))]
    public void LethalityAddsOnlyTheRankedCriticalBonusForTheEnumeratedDamageAbilities(string key, int rank, bool critical)
    {
        var r = Request("lethality", rank); r.Tuning.PhysicalCritPercent = critical ? 100 : 0;
        if (key is "mutilate" or "hemorrhage" or "ghostly-strike") r.Talents.Ranks[key] = 1;
        r.StartInStealth = key == "ambush";
        var h = new Harness(r);
        if (key == "rogue-main-hand") h.Actor.AddAbility(new() { Key = key, Name = key,
            Effects = [new() { Key = "auto-hit", EffectType = AbilityEffectTypes.DirectDamage,
                TargetType = AbilityTargetTypes.Enemy, SchoolKey = "physical", ResolutionType = CombatResolutionTypes.Melee,
                CanCrit = true, CanMiss = true }] });
        var ability = h.Actor.Abilities[key].Definition;
        foreach (var effect in ability.Effects.Where(e => e.EffectType == AbilityEffectTypes.DirectDamage))
            effect.MinimumValue = effect.MaximumValue = 100;
        var damage = h.Cast(key);
        Assert.Equal(key == "mutilate" ? 2 : 1, damage.Length);
        // Independent source oracle: bonus is applied to the +100% crit portion,
        // not multiplied into the whole 200%; Ambush is absent from the source list.
        var eligible = key is "sinister-strike" or "backstab" or "mutilate" or "hemorrhage" or "ghostly-strike";
        var expected = critical ? 200m + (eligible ? 4m * rank : 0m) : 100m;
        Assert.All(damage, e => { Assert.Equal(expected, e.Amount); Assert.Equal(critical, e.IsCritical); });
        if (key == "mutilate") Assert.Contains(damage, e => e.WeaponHandKey == WeaponHandKeys.OffHand);
    }

    [Theory][InlineData("sinister-strike",235)][InlineData("ambush",200)]
    public void LethalityExplicitTuningOverrideKeepsItsMeaning(string key, int expected)
    {
        var r = Request("lethality", 5); r.Tuning.LethalityBonusCriticalDamagePerRank = .07m;
        r.Tuning.PhysicalCritPercent = 100; r.StartInStealth = key == "ambush";
        var h = new Harness(r); var e = h.Actor.Abilities[key].Definition.Effects[0];
        e.MinimumValue = e.MaximumValue = 100;
        Assert.Equal(expected, Assert.Single(h.Cast(key)).Amount);
    }

    [Theory][InlineData(0,0,0)][InlineData(1,10,5)][InlineData(2,20,10)][InlineData(3,30,15)]
    public void PuncturingWoundsRetainsBackstabAndBothMutilateCriticalBonuses(int rank, int backstab, int mutilate)
    {
        var r = Request("puncturing-wounds", rank); r.Talents.Ranks["mutilate"] = 1;
        var h = new Harness(r);
        Assert.Equal(backstab, h.Actor.Abilities["backstab"].Definition.Effects[0].BonusCriticalChancePercent);
        Assert.All(h.Actor.Abilities["mutilate"].Definition.Effects, e => Assert.Equal(mutilate, e.BonusCriticalChancePercent));
    }

    [Theory][InlineData(1,15)][InlineData(2,30)][InlineData(3,45)]
    public void PuncturingWoundsDefaultHasIndependentRankedBoundaryOracles(int rank, int threshold)
    {
        var r = Request("puncturing-wounds", rank);
        Assert.Equal(15m, r.Tuning.PuncturingWoundsExtraPointChancePerRank);
        // Deterministically select seeds immediately on either side of the literal
        // source threshold. No probability-of-success or flaky sample assertion.
        foreach (var below in new[] { true, false })
        {
            var seed = Enumerable.Range(0, 1000000).First(s => {
                var roll = (decimal)new Random(s).NextDouble() * 100m;
                return below ? roll >= threshold - .01m && roll < threshold :
                    roll >= threshold && roll < threshold + .01m;
            });
            var sampled = (decimal)new Random(seed).NextDouble() * 100m;
            Assert.InRange(sampled, threshold - .01m, threshold + .01m);
            var h = new Harness(r, seed); h.Hit("backstab");
            Assert.Equal(below ? 2 : 1, h.Combat.ComboPointsFor("target"));
        }
    }

    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)]
    public void PuncturingWoundsTwoHundredSeedOracleAndRemovalPreserveRandomConsumption(int rank)
    {
        for (var seed = 0; seed < 200; seed++)
        {
            var expectedRandom = new Random(seed);
            var expected = 1 + (rank > 0 && (decimal)expectedRandom.NextDouble() * 100m < 15m * rank ? 1 : 0);
            var r = Request("puncturing-wounds", rank); var h = new Harness(r, seed); h.Hit("backstab");
            Assert.Equal(expected, h.Combat.ComboPointsFor("target"));
            Assert.Equal(expectedRandom.NextDouble(), h.Context.Random.NextDouble());
            r.Talents.Ranks.Remove("puncturing-wounds"); var removed = new Harness(r, seed); removed.Hit("backstab");
            Assert.Equal(1, removed.Combat.ComboPointsFor("target"));
            Assert.Equal(new Random(seed).NextDouble(), removed.Context.Random.NextDouble());
        }
    }

    [Theory][InlineData(0,1)][InlineData(10,1)][InlineData(100,2)]
    public void PuncturingWoundsExplicitOverrideIncludingZeroAndSaturationIsPreserved(int chance, int expected)
    {
        var r = Request("puncturing-wounds", 3); r.Tuning.PuncturingWoundsExtraPointChancePerRank = chance;
        // Seed 1 rolls 24.8668: rank3 legacy override10 succeeds, so use rank1
        // for the10% case and rank3 for saturation beyond100%.
        if (chance == 10) r.Talents.Ranks["puncturing-wounds"] = 1;
        var h = new Harness(r); h.Hit("backstab"); Assert.Equal(expected, h.Combat.ComboPointsFor("target"));
        var roundTrip = JsonSerializer.Deserialize<RogueSimRequest>(JsonSerializer.Serialize(r))!;
        Assert.Equal(chance, roundTrip.Tuning.PuncturingWoundsExtraPointChancePerRank);
    }

    [Fact]
    public void PuncturingWoundsExactEqualityFailsAndSlightlyHigherOverrideSucceeds()
    {
        var roll = (decimal)new Random(1).NextDouble() * 100m;
        foreach (var extra in new[] { 0m, .000001m })
        {
            var r = Request("puncturing-wounds", 1); r.Tuning.PuncturingWoundsExtraPointChancePerRank = roll + extra;
            var h = new Harness(r); h.Hit("backstab"); Assert.Equal(extra == 0 ? 1 : 2, h.Combat.ComboPointsFor("target"));
        }
    }

    [Fact]
    public void PuncturingWoundsPointsAreSourceAndTargetOwnedCappedAndStackWithSealFateSeparately()
    {
        var r = Request("puncturing-wounds", 3); r.Tuning.PuncturingWoundsExtraPointChancePerRank = 100;
        var h = new Harness(r);
        h.Hit("backstab", source: "other"); h.Hit("backstab", periodic: true); h.Hit("backstab", amount: 0);
        Assert.Equal(0, h.Combat.ComboPointsFor("target"));
        h.Hit("backstab", target: "target-2"); Assert.Equal(2, h.Combat.ComboPointsFor("target-2"));
        h.Hit("backstab"); Assert.Equal(2, h.Combat.ComboPointsFor("target"));
        h.Hit("backstab"); h.Hit("backstab"); Assert.Equal(5, h.Combat.ComboPointsFor("target"));
        Assert.Equal(2, h.Combat.ComboPointsFor("target-2"));
        r.Talents.Ranks["seal-fate"] = 5; var stacked = new Harness(r);
        stacked.Hit("backstab", critical: true); Assert.Equal(3, stacked.Combat.ComboPointsFor("target"));
        stacked.Hit("backstab", critical: true); Assert.Equal(5, stacked.Combat.ComboPointsFor("target"));
    }

    [Theory][InlineData("sinister-strike",1)][InlineData("ambush",1)][InlineData("mutilate",2)]
    [InlineData("hemorrhage",1)][InlineData("ghostly-strike",1)][InlineData("rogue-main-hand",0)]
    public void PuncturingWoundsDoesNotGrantBonusToOtherAttacks(string key, int expected)
    {
        var r = Request("puncturing-wounds", 3); r.Tuning.PuncturingWoundsExtraPointChancePerRank = 100;
        var h = new Harness(r); h.Hit(key); Assert.Equal(expected, h.Combat.ComboPointsFor("target"));
        Assert.Equal(new Random(1).NextDouble(), h.Context.Random.NextDouble());
    }

    private static string StableRun(RogueSimResponse r) => JsonSerializer.Serialize(new
    {
        r.DamageDone, r.Dps, r.EndingEnergy, r.EndingComboPoints, r.FinishersUsed,
        r.DamageByAbility, r.Run.ProcessedEventCount, r.Run.CustomMetrics,
        Events = r.Run.Timeline.Select(e => new { e.TimeSeconds, e.Type, e.SourceActorKey,
            e.TargetActorKey, e.AbilityKey, e.EffectKey, e.Amount, e.RawAmount, e.ResultKey,
            e.IsCritical, e.ResourceKey, e.ResourceTotalAfter, e.EffectDeliveryType })
    });

    private static RogueSimRequest RepeatedBackstabRequest()
    {
        var r = Request("puncturing-wounds", 3);
        r.Specialization = r.Character.SpecializationKey = "subtlety";
        r.Tuning.EnergyPerSecond = 60; r.TargetMaximumHealth = 10000000;
        return r;
    }

    [Fact]
    public void PuncturingWoundsDefaultChanceAloneChangesActualRunnerAndReplaysExactly()
    {
        var r = RepeatedBackstabRequest();
        var before = JsonSerializer.Serialize(r);
        var sourced = new RogueSimRunner().Run(r);
        Assert.Equal(before, JsonSerializer.Serialize(r));
        Assert.Equal(StableRun(sourced), StableRun(new RogueSimRunner().Run(r)));
        r.Tuning.PuncturingWoundsExtraPointChancePerRank = 10;
        var legacyOverride = new RogueSimRunner().Run(r);
        Assert.True(sourced.Run.Timeline.Count(e => e.AbilityKey == "backstab" &&
            e.Type == CombatEventType.Damage) > 10);
        // Same rank, critical bonuses and seeded stream: only CP chance differs.
        Assert.NotEqual(legacyOverride.DamageDone, sourced.DamageDone);
        Assert.NotEqual(StableRun(legacyOverride), StableRun(sourced));
        r.Tuning.PuncturingWoundsExtraPointChancePerRank = 15;
        Assert.Equal(StableRun(sourced), StableRun(new RogueSimRunner().Run(r)));
    }

    [Fact]
    public void PuncturingWoundsTalentRemovalRestoresTheEntireZeroRankTimeline()
    {
        var r = RepeatedBackstabRequest(); r.Talents.Ranks.Remove("puncturing-wounds");
        var removed = new RogueSimRunner().Run(r);
        r.Talents.Ranks["puncturing-wounds"] = 0;
        Assert.Equal(StableRun(removed), StableRun(new RogueSimRunner().Run(r)));
    }

    [Theory][InlineData(false,true)][InlineData(true,false)]
    public void PuncturingWoundsCannotBypassWeaponOrPositionEligibility(bool daggers, bool behind)
    {
        var r = RepeatedBackstabRequest(); r.BothWeaponsDaggers = daggers; r.IsBehindTarget = behind;
        var sourced = new RogueSimRunner().Run(r);
        Assert.DoesNotContain(sourced.Run.Timeline, e => e.AbilityKey == "backstab");
        r.Talents.Ranks.Remove("puncturing-wounds");
        Assert.Equal(StableRun(sourced), StableRun(new RogueSimRunner().Run(r)));
    }

    [Fact]
    public void PuncturingWoundsCriticalChanceOverridesAreStillExplicit()
    {
        var r = Request("puncturing-wounds", 3); r.Talents.Ranks["mutilate"] = 1;
        r.Tuning.PuncturingWoundsBackstabCritPerRank = 7;
        r.Tuning.PuncturingWoundsMutilateCritPerRank = 2;
        var h = new Harness(r);
        Assert.Equal(21m, h.Actor.Abilities["backstab"].Definition.Effects[0].BonusCriticalChancePercent);
        Assert.All(h.Actor.Abilities["mutilate"].Definition.Effects, e => Assert.Equal(6m, e.BonusCriticalChancePercent));
    }
}
