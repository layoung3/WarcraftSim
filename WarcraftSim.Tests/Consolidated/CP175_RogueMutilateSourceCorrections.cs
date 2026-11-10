using System.Reflection;
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Rogue;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>Historical/provisional 1cc5ae1 rogue.json: 75% weapon +13 per hand,
/// +20% against poisoned targets, 2 CP. No current-client or multiplayer certification.</summary>
public sealed class Cp175RogueMutilateSourceCorrections
{
    private static RogueSimRequest Request()
    {
        var r = RogueSimRequest.CreatePreset("assassination");
        r.Talents = new() { AssassinationPointsSpent = 21, CombatPointsSpent = 5,
            SubtletyPointsSpent = 2, Ranks = new() { ["mutilate"] = 1 } };
        r.StartInStealth = false; r.Character.BaseStats = new();
        r.MainHandPoison = r.OffHandPoison = "none";
        r.Tuning.MainHandWeaponDamage = 100; r.Tuning.OffHandWeaponDamage = 80;
        r.Tuning.MainHandHitPercent = r.Tuning.OffHandHitPercent = 100;
        r.Tuning.PhysicalCritPercent = 0; r.Tuning.InstantPoisonChancePercent = 100;
        r.PoisonChargesPerWeapon = 20; r.CaptureTimeline = true;
        return r;
    }

    // Use the real runner adapters and executor, not a replacement damage formula.
    private sealed class Harness : ICombatEventProcessor
    {
        public SimulationContext Context { get; }
        public SimulationActorState Actor { get; }
        public SimulationActorState Target => Context.GetActor("target")!;
        public RogueCombatProcessor Combat { get; }
        public RoguePoisonProcessor Poisons { get; }
        public AbilityExecutor Executor { get; }
        public AuraManager Auras { get; } = new();
        private readonly Dictionary<string, Action> _actions = new();
        public Harness(RogueSimRequest r, decimal duration = 1, decimal attackPower = 0)
        {
            Context = new(new() { Seed = 1, DurationSeconds = duration,
                CaptureTimeline = true, PrimaryActorKey = "rogue" });
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
            Poisons = new(r, Actor, Auras, Executor);
            foreach (var ability in ForeverRogueSpellbook.Create(r.Specialization, r.Talents,
                r.Tuning, attackPower, r.BothWeaponsDaggers)) Actor.AddAbility(ability);
        }
        public void At(decimal time, Action action)
        {
            var key = "test-" + _actions.Count; _actions.Add(key, action);
            Context.ScheduleEvent(new() { TimeSeconds = time, Type = CombatEventType.RotationDecision,
                SourceActorKey = "rogue", EffectKey = key, IsInternal = true });
        }
        public void Process(SimulationContext context, CombatEvent ev)
        { if (ev.EffectKey is { } key && _actions.TryGetValue(key, out var action)) action(); }
        public void Cast(string target = "target") =>
            Assert.True(Executor.TryStartAbility(Context, "rogue", target, "mutilate").Success);
        public void Probe(string target = "target") => Executor.ExecuteBackgroundDirectDamage(
            Context, Actor, Context.GetActor(target)!, "probe-strike", "Probe strike",
            new() { Key = "probe", EffectType = AbilityEffectTypes.DirectDamage,
                TargetType = AbilityTargetTypes.Enemy, SchoolKey = "physical",
                ResolutionType = CombatResolutionTypes.Melee, CanMiss = false, CanCrit = false,
                WeaponHandKey = WeaponHandKeys.MainHand, MinimumValue = 1, MaximumValue = 1 });
        public AuraInstance Aura(string key, string source = "rogue", string target = "target",
            decimal duration = 12, bool expiration = true) => Auras.ApplyAura(Context,
                Context.GetActor(target)!, new() { Key = key, DurationSeconds = duration },
                source, key, "test-aura", expiration);
        public decimal Multiplier(string target = "target", string ability = "mutilate") =>
            Combat.DamageMultiplier(ability, CombatEffectDeliveryType.Direct, Context, Context.GetActor(target)!);
        public CombatEvent[] Run()
        {
            var run = new SimulationEngine([Executor, Auras, Combat, Poisons, this]).Run(Context);
            return run.Timeline.Where(e => e.Type == CombatEventType.Damage && e.AbilityKey == "mutilate").ToArray();
        }
    }

    [Fact]
    public void HistoricalDefaultHasIndependentPerHandAndCombinedOracles()
    {
        var r = Request(); Assert.Equal(13m, r.Tuning.MutilateBonusDamagePerHand);
        var h = new Harness(r); var effects = h.Actor.Abilities["mutilate"].Definition.Effects;
        Assert.Equal(new[] { 88m, 43m }, effects.Select(e => e.MinimumValue));
        Assert.Equal(new[] { 88m, 43m }, effects.Select(e => e.MaximumValue));
        h.At(0, () => h.Cast()); var hits = h.Run();
        Assert.Equal(new[] { 88m, 43m }, hits.Select(e => e.Amount!.Value));
        Assert.Equal(131m, hits.Sum(e => e.Amount));
        Assert.Equal(new[] { WeaponHandKeys.MainHand, WeaponHandKeys.OffHand }, hits.Select(e => e.WeaponHandKey));
        Assert.Single(hits.Select(e => e.AbilityExecutionId).Distinct());
        Assert.Equal(2, h.Combat.ComboPointsFor("target"));
        Assert.Contains(h.Context.Timeline, e => e.Type == CombatEventType.ResourceChanged && e.Amount == -60);
    }

    [Theory][InlineData("0", "75", "30")][InlineData("17.25", "92.25", "47.25")]
    [InlineData("29", "104", "59")][InlineData("13", "88", "43")]
    public void ExplicitOverridesIncludingOldSerializedValueAndZeroSurviveSnapshotAndRealImpacts(
        string bonus, string main, string off)
    {
        decimal D(string value) => decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        var r = Request(); r.Tuning.MutilateBonusDamagePerHand = D(bonus);
        var json = JsonSerializer.Serialize(r);
        var roundTrip = JsonSerializer.Deserialize<RogueSimRequest>(json)!;
        var snapshot = RogueSimulationRequestSnapshot.Create(roundTrip);
        Assert.Equal(D(bonus), snapshot.Tuning.MutilateBonusDamagePerHand);
        var h = new Harness(snapshot); h.At(0, () => h.Cast());
        Assert.Equal(new[] { D(main), D(off) }, h.Run().Select(e => e.Amount!.Value));
        var poisoned = new Harness(snapshot); poisoned.Aura("wound-poison"); poisoned.At(0, () => poisoned.Cast());
        Assert.Equal(new[] { D(main) * 1.2m, D(off) * 1.2m }, poisoned.Run().Select(e => e.Amount!.Value));
        Assert.Equal(json, JsonSerializer.Serialize(r));
    }

    [Theory][InlineData(0, "50.5")][InlineData(5, "65.5")]
    public void NonzeroAttackPowerAndDualWieldScalingLeaveTheFlatBonusOutsideTheHandPenalty(int rank, string off)
    {
        var r = Request(); r.Tuning.MainHandSpeedSeconds = r.Tuning.OffHandSpeedSeconds = 2;
        r.Talents.Ranks["dual-wield-specialization"] = rank;
        var h = new Harness(r, attackPower: 140); h.At(0, () => h.Cast());
        // Main: .75*(100 + 140*2/14)+13 = 103.
        // Off: .75*(80*(.5+.05*rank) + 140*2/14*.5)+13 = 50.5/65.5.
        Assert.Equal(new[] { 103m, decimal.Parse(off, System.Globalization.CultureInfo.InvariantCulture) },
            h.Run().Select(e => e.Amount!.Value));
    }

    [Fact]
    public void MissingSerializedTuningPropertySelectsSourcedDefaultRatherThanLegacyValue()
    {
        Assert.Equal(13m, JsonSerializer.Deserialize<ForeverRogueTuning>("{}")!.MutilateBonusDamagePerHand);
        Assert.Equal(0m, JsonSerializer.Deserialize<ForeverRogueTuning>("{\"MutilateBonusDamagePerHand\":0}")!.MutilateBonusDamagePerHand);
        var r = Request(); r.Tuning.MutilateBonusDamagePerHand = -1;
        Assert.Throws<ArgumentException>(() => r.Tuning.Validate());
    }

    public static IEnumerable<object[]> PoisonCompositionCases()
    {
        foreach (var poison in new[] { "deadly", "wound", "mind-numbing", "crippling" })
            foreach (var critical in new[] { false, true })
                foreach (var armor in new[] { false, true })
                    foreach (var opportunity in new[] { 0, 2 })
                        yield return [poison, critical, armor, opportunity];
    }

    [Theory][MemberData(nameof(PoisonCompositionCases))]
    public void ActualPoisonApplicationsComposeOnceWithOpportunityCriticalAndArmor(
        string poison, bool critical, bool armor, int opportunity)
    {
        var r = Request(); r.MainHandPoison = poison;
        r.Tuning.PhysicalCritPercent = critical ? 100 : 0;
        r.TargetArmor = armor ? r.Tuning.ArmorDamageReductionConstant : 0;
        r.Talents.Ranks["opportunity"] = opportunity;
        var h = new Harness(r); h.At(0, () => h.Probe()); h.At(.1m, () => h.Cast());
        var hits = h.Run();
        var scale = (opportunity == 2 ? 1.10m : 1m) * (critical ? 2m : 1m);
        Assert.Equal(new[] { 105.6m * scale, 51.6m * scale }, hits.Select(e => e.RawAmount!.Value));
        Assert.Equal(new[] { 105.6m * scale, 51.6m * scale }.Select(x => x * (armor ? .5m : 1m)),
            hits.Select(e => e.Amount!.Value));
        Assert.All(hits, e => Assert.Equal(critical, e.IsCritical));
        Assert.Equal(2, h.Combat.ComboPointsFor("target"));
        Assert.Equal(2, h.Poisons.Applications); Assert.Equal(18, h.Poisons.ChargesRemaining(WeaponHandKeys.MainHand));
        Assert.Equal(20, h.Poisons.ChargesRemaining(WeaponHandKeys.OffHand));
        Assert.Contains(h.Context.Timeline, e => e.Type == CombatEventType.AuraApplied &&
            e.AbilityKey == poison + "-poison" && e.TargetActorKey == "target" && e.SourceActorKey == "rogue");
        Assert.DoesNotContain(h.Context.Timeline, e => e.Type == CombatEventType.Damage && e.AbilityKey == poison + "-poison");
        var aura = Assert.Single(h.Target.ActiveAuras, a => a.Definition.Key == poison + "-poison");
        Assert.Equal(12.1m, aura.ExpiresAtSeconds);
        Assert.Equal(poison == "deadly" ? 2 : 1, aura.Stacks);
        Assert.Equal(poison == "wound" ? .5m : 1m, aura.Definition.HealingReceivedMultiplier);
        Assert.Equal(poison == "mind-numbing" ? 1.3m : 1m, aura.Definition.CastTimeMultiplier);
    }

    [Theory][InlineData("deadly-poison", true)][InlineData("wound-poison", true)]
    [InlineData("mind-numbing-poison", true)][InlineData("crippling-poison", true)]
    [InlineData("instant-poison", false)][InlineData("unrelated", false)]
    [InlineData("deadly-poison-extra", false)][InlineData("not-wound-poison", false)]
    public void OnlyExactRecognizedAuraKeysQualifyAndForeignSourcePreservesExistingCompatibility(
        string key, bool qualifies)
    {
        // Foreign-source inclusion preserves the old target-state predicate only;
        // this synthetic fixture does not certify party poison rules in the client.
        var h = new Harness(Request()); h.Aura(key, "another-rogue"); h.At(0, () => h.Cast());
        Assert.Equal(new[] { qualifies ? 105.6m : 88m, qualifies ? 51.6m : 43m }, h.Run().Select(e => e.Amount!.Value));
    }

    [Theory][InlineData("deadly-poison")][InlineData("wound-poison")]
    [InlineData("mind-numbing-poison")][InlineData("crippling-poison")]
    public void AnotherTargetsPoisonOrActorsOwnAuraDoesNotQualify(string key)
    {
        var h = new Harness(Request()); h.Aura(key, target: "target-2"); h.Aura(key, target: "rogue");
        h.At(0, () => h.Cast()); Assert.Equal(131m, h.Run().Sum(e => e.Amount));
    }

    public static IEnumerable<object[]> BoundaryCases()
    {
        foreach (var poison in new[] { "deadly-poison", "wound-poison", "mind-numbing-poison", "crippling-poison" })
            foreach (var time in new[] { .5m, 1m, 1.999m, 2m }) yield return [poison, time];
    }
    [Theory][MemberData(nameof(BoundaryCases))]
    public void RealImpactsUseHalfOpenAuraLifetimeEvenWhenExpiredObjectRemains(string key, decimal time)
    {
        var h = new Harness(Request(), 3);
        var aura = h.Aura(key, expiration: false); aura.AppliedAtSeconds = 1; aura.ExpiresAtSeconds = 2;
        h.At(time, () => h.Cast()); var hits = h.Run();
        Assert.Equal(time >= 1 && time < 2 ? 157.2m : 131m, hits.Sum(e => e.Amount));
        Assert.Contains(aura, h.Target.ActiveAuras);
    }

    [Theory][InlineData("deadly")][InlineData("wound")][InlineData("mind-numbing")][InlineData("crippling")]
    public void ActualRefreshSurvivesStaleExpiryThenExpiresExactlyWithoutChangingDurations(string poison)
    {
        var r = Request(); r.MainHandPoison = poison;
        var h = new Harness(r, 18); h.At(0, () => h.Probe()); h.At(5, () => h.Probe());
        h.At(12, () => Assert.Equal(1.2m, h.Multiplier()));
        h.At(16.999m, () => Assert.Equal(1.2m, h.Multiplier()));
        h.At(17, () => { Assert.Equal(1m, h.Multiplier()); r.MainHandPoison = "none"; h.Cast(); });
        Assert.Equal(131m, h.Run().Sum(e => e.Amount));
        Assert.Equal(2, h.Poisons.Applications); Assert.Equal(18, h.Poisons.ChargesRemaining(WeaponHandKeys.MainHand));
        Assert.DoesNotContain(h.Target.ActiveAuras, a => a.Definition.Key == poison + "-poison");
        if (poison == "deadly")
        {
            Assert.Equal(5, h.Poisons.DeadlyTicks);
            var ticks = h.Context.Timeline.Where(e => e.Type == CombatEventType.Damage && e.AbilityKey == "deadly-poison").ToArray();
            Assert.Equal(new[] { 3m, 6m, 9m, 12m, 15m }, ticks.Select(e => e.TimeSeconds));
            Assert.All(ticks, e => Assert.True(e.IsPeriodic));
            Assert.Equal(r.Tuning.DeadlyPoisonDamagePerStack, ticks[0].Amount);
            Assert.All(ticks.Skip(1), e => Assert.Equal(2 * r.Tuning.DeadlyPoisonDamagePerStack, e.Amount));
        }
        else Assert.DoesNotContain(h.Context.Timeline, e => e.Type == CombatEventType.Damage && e.AbilityKey == poison + "-poison");
    }

    [Theory][InlineData("deadly")][InlineData("wound")][InlineData("mind-numbing")][InlineData("crippling")]
    public void ExplicitRemovalEndsEligibility(string poison)
    {
        var r = Request(); r.MainHandPoison = poison; var h = new Harness(r);
        h.At(0, () => h.Probe());
        h.At(.1m, () => { Assert.Equal(1.2m, h.Multiplier());
            h.Auras.RemoveAuras(h.Context, h.Target, poison + "-poison", "rogue");
            Assert.Equal(1m, h.Multiplier()); r.MainHandPoison = "none"; h.Cast(); });
        Assert.Equal(131m, h.Run().Sum(e => e.Amount));
    }

    [Fact]
    public void MultipleActuallyAppliedPoisonsStillGrantExactlyOneTwentyPercentBonus()
    {
        var r = Request(); var h = new Harness(r);
        var poisons = new[] { "deadly", "wound", "mind-numbing", "crippling" };
        for (var i = 0; i < poisons.Length; i++)
        { var poison = poisons[i]; h.At(i * .1m, () => { r.MainHandPoison = poison; h.Probe(); }); }
        h.At(.5m, () => { r.MainHandPoison = "none"; h.Cast(); });
        Assert.Equal(new[] { 105.6m, 51.6m }, h.Run().Select(e => e.Amount!.Value));
        Assert.Equal(4, h.Target.ActiveAuras.Count); Assert.Equal(4, h.Poisons.Applications);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void InstantOnlyDirectDamageDoesNotCreatePersistentPoisonEligibility(bool delayedOffHand)
    {
        var r = Request(); r.MainHandPoison = "instant"; var h = new Harness(r);
        if (delayedOffHand) h.Actor.Abilities["mutilate"].Definition.Effects[1].TravelTimeSeconds = .1m;
        h.At(0, () => h.Probe()); h.At(.1m, () => h.Cast());
        Assert.Equal(new[] { 88m, 43m }, h.Run().Select(e => e.Amount!.Value));
        Assert.Contains(h.Context.Timeline, e => e.Type == CombatEventType.Damage && e.AbilityKey == "instant-poison" && e.Amount > 0);
        Assert.Empty(h.Target.ActiveAuras); Assert.Equal(1m, h.Multiplier());
    }

    [Theory][InlineData("deadly", false)][InlineData("wound", false)]
    [InlineData("mind-numbing", false)][InlineData("crippling", false)]
    [InlineData("deadly", true)][InlineData("wound", true)]
    [InlineData("mind-numbing", true)][InlineData("crippling", true)]
    public void ExistingHandOrderingIsPreservedAndOnlyAPoisonBeforeAnImpactCanBoostIt(string poison, bool delayedOffHand)
    {
        var r = Request(); r.MainHandPoison = poison; var h = new Harness(r);
        // Default effects are both immediate. A synthetic delay tests the real
        // impact pipeline; it is not a proposed new Mutilate timing rule.
        if (delayedOffHand) h.Actor.Abilities["mutilate"].Definition.Effects[1].TravelTimeSeconds = .1m;
        h.At(0, () => h.Cast()); var hits = h.Run();
        Assert.Equal(new[] { 88m, delayedOffHand ? 51.6m : 43m }, hits.Select(e => e.Amount!.Value));
        Assert.Equal(new[] { 0m, delayedOffHand ? .1m : 0m }, hits.Select(e => e.TimeSeconds));
        Assert.Equal(1, h.Poisons.Applications); Assert.Equal(2, h.Combat.ComboPointsFor("target"));
        Assert.Equal(1.2m, h.Multiplier());
    }

    [Theory][InlineData("none")][InlineData("no-charges")][InlineData("zero-chance")][InlineData("foreign-hit")]
    public void EquippedPoisonOrAttemptWithoutApplicationCannotQualify(string mode)
    {
        var r = Request(); r.MainHandPoison = mode == "none" ? "none" : "wound";
        if (mode == "no-charges") r.PoisonChargesPerWeapon = 0;
        if (mode == "zero-chance") r.Tuning.InstantPoisonChancePercent = 0;
        var h = new Harness(r);
        if (mode == "foreign-hit") { h.Poisons.Process(h.Context, new() { Type = CombatEventType.Damage,
            SourceActorKey = "another-rogue", TargetActorKey = "target", AbilityKey = "probe-strike",
            WeaponHandKey = WeaponHandKeys.MainHand, Amount = 1 }); r.MainHandPoison = "none"; }
        else h.At(0, () => h.Probe());
        h.At(.1m, () => h.Cast()); Assert.Equal(131m, h.Run().Sum(e => e.Amount));
        Assert.Equal(0, h.Poisons.Applications); Assert.Empty(h.Target.ActiveAuras);
    }

    [Fact]
    public void ZeroRankAndNonMutilateSpellDefinitionsAreUnaffectedByBonusDefault()
    {
        var r = Request();
        var sourced = ForeverRogueSpellbook.Create("assassination", r.Talents, r.Tuning, 0, true);
        r.Tuning.MutilateBonusDamagePerHand = 17.25m;
        var legacy = ForeverRogueSpellbook.Create("assassination", r.Talents, r.Tuning, 0, true);
        Assert.Equal(JsonSerializer.Serialize(legacy.Where(a => a.Key != "mutilate")),
            JsonSerializer.Serialize(sourced.Where(a => a.Key != "mutilate")));
        r.Talents.Ranks["mutilate"] = 0;
        Assert.DoesNotContain(ForeverRogueSpellbook.Create("assassination", r.Talents, r.Tuning, 0, true), a => a.Key == "mutilate");
        var h = new Harness(Request()); h.Aura("wound-poison");
        Assert.Equal(1m, h.Multiplier(ability: "sinister-strike"));
    }
}
