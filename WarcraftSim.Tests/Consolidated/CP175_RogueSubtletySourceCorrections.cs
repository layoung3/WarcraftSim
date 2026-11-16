using System.Reflection;
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Rogue;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>Historical/provisional 1cc5ae1 rogue.json damage-only corrections.
/// Does not certify defensive Ghostly Strike, Hemorrhage direct scaling or strict Subtlety.</summary>
public sealed class Cp175RogueSubtletySourceCorrections
{
    private static RogueSimRequest Request()
    {
        var r = RogueSimRequest.CreatePreset("subtlety");
        r.Talents = new() { AssassinationPointsSpent = 20, SubtletyPointsSpent = 31,
            Ranks = new() { ["ghostly-strike"] = 1, ["hemorrhage"] = 1 } };
        r.StartInStealth = false; r.Character.BaseStats = new();
        r.UseInstantPoison = false; r.MainHandPoison = r.OffHandPoison = "none";
        r.Tuning.MainHandWeaponDamage = 100; r.Tuning.OffHandWeaponDamage = 80;
        r.Tuning.MainHandSpeedSeconds = r.Tuning.OffHandSpeedSeconds = 2;
        r.Tuning.MainHandHitPercent = r.Tuning.OffHandHitPercent = 100;
        r.Tuning.PhysicalCritPercent = 0; r.Tuning.EnergyPerSecond = 60;
        r.Tuning.InstantPoisonChancePercent = 100; r.PoisonChargesPerWeapon = 100;
        r.TargetMaximumHealth = 1000000; r.CaptureTimeline = true;
        return r;
    }

    // Exercise the actual preparation path, including its independently validated hand types.
    // No new public production API or substitute damage/roll formula is needed for these tests.
    private static IReadOnlyList<AbilityDefinition> PreparedAbilities(RogueSimRequest r)
    {
        var p = typeof(RogueSimRunner).GetMethod("Prepare", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [r])!;
        return (IReadOnlyList<AbilityDefinition>)p.GetType().GetProperty("Abilities")!.GetValue(p)!;
    }

    private sealed class Harness : ICombatEventProcessor
    {
        public RogueSimRequest Request { get; }
        public SimulationContext Context { get; }
        public SimulationActorState Actor => Context.GetActor("rogue")!;
        public SimulationActorState Target => Context.GetActor("target")!;
        public RogueCombatProcessor Combat { get; }
        public RoguePoisonProcessor Poisons { get; }
        public AbilityExecutor Executor { get; }
        public AuraManager Auras { get; } = new();
        private readonly Dictionary<string, Action> _actions = new();
        public Harness(RogueSimRequest r, decimal duration = 1)
        {
            Request = r;
            Context = new(new() { Seed = 1, DurationSeconds = duration,
                CaptureTimeline = true, PrimaryActorKey = "rogue" });
            foreach (var key in new[] { "rogue", "other-rogue", "target", "target-2" })
            {
                var friendly = key.Contains("rogue", StringComparison.Ordinal);
                var actor = new SimulationActorState { Key = key, Name = key,
                    TeamKey = friendly ? "raid" : "enemy", Level = friendly ? 60 : 63,
                    MaximumHealth = 1000000 };
                actor.InitializeHealth(1000000);
                if (friendly)
                {
                    actor.AddResource(new ResourceState("energy", 100, 100, 60));
                    foreach (var ability in PreparedAbilities(r)) actor.AddAbility(ability);
                }
                Context.AddActor(actor);
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
        }
        public void At(decimal time, Action action)
        {
            var key = "test-" + _actions.Count; _actions.Add(key, action);
            Context.ScheduleEvent(new() { TimeSeconds = time, Type = CombatEventType.RotationDecision,
                SourceActorKey = "rogue", EffectKey = key, IsInternal = true });
        }
        public void Process(SimulationContext context, CombatEvent ev)
        { if (ev.EffectKey is { } key && _actions.TryGetValue(key, out var action)) action(); }
        public void Cast(string ability, string target = "target", string source = "rogue") =>
            Assert.True(Executor.TryStartAbility(Context, source, target, ability).Success,
                $"{source} could not cast {ability} at {Context.CurrentTimeSeconds}");
        public void Points(int count, decimal start = 0, string target = "target")
        { for (var i = 0; i < count; i++) At(start + i, () => Cast("sinister-strike", target)); }
        public void Mark(string source = "rogue", string target = "target") => Cast("hemorrhage", target, source);
        public CombatEvent[] Damage(string ability) => Context.Timeline.Where(e =>
            e.Type == CombatEventType.Damage && e.AbilityKey == ability).ToArray();
        public CombatEvent[] Ticks() => Damage("rupture").Where(e => e.IsPeriodic).ToArray();
        public AuraInstance MarkAura(string source = "rogue", string target = "target") =>
            Assert.Single(Context.GetActor(target)!.ActiveAuras, a =>
                a.Definition.Key == "hemorrhage-rupture" &&
                string.Equals(a.SourceActorKey, source, StringComparison.OrdinalIgnoreCase));
        public void Run() => new SimulationEngine([Executor, Auras, Combat, Poisons, this]).Run(Context);
    }

    public static IEnumerable<object[]> HandCases()
    {
        foreach (var main in new[] { "dagger", "sword", "axe", "mace", "fist", "other" })
        foreach (var off in new[] { "dagger", "sword", "axe", "mace", "fist", "other" })
        foreach (var legacy in new[] { false, true })
        foreach (var behind in new[] { false, true }) yield return [main, off, legacy, behind];
    }

    [Theory][MemberData(nameof(HandCases))]
    public void GhostlyStrikeUsesExplicitMainHandWithoutReinterpretingLegacyFlags(
        string main, string off, bool legacy, bool behind)
    {
        var r = Request(); r.MainHandWeaponType = main; r.OffHandWeaponType = off;
        r.BothWeaponsDaggers = legacy; r.IsBehindTarget = behind;
        r.Talents.AssassinationPointsSpent = 21; r.Talents.SubtletyPointsSpent = 30;
        r.Talents.Ranks["mutilate"] = 1;
        var before = JsonSerializer.Serialize(r); var h = new Harness(r);
        Assert.Equal(legacy && behind, h.Actor.Abilities.ContainsKey("backstab"));
        Assert.Equal(legacy && behind, h.Actor.Abilities.ContainsKey("ambush"));
        Assert.Equal(legacy, h.Actor.Abilities.ContainsKey("mutilate"));
        h.At(0, () => h.Cast("ghostly-strike")); h.Run();
        var hit = Assert.Single(h.Damage("ghostly-strike"));
        Assert.Equal(main == "dagger" ? 180m : 125m, hit.Amount);
        Assert.Equal(hit.Amount, hit.RawAmount); Assert.False(hit.IsCritical);
        Assert.Equal(1, h.Combat.ComboPointsFor("target"));
        Assert.Equal(0, h.Combat.ComboPointsFor("target-2"));
        Assert.Contains(h.Context.Timeline, e => e.Type == CombatEventType.ResourceChanged && e.Amount == -40);
        Assert.Equal(20m, h.Actor.Abilities["ghostly-strike"].Definition.CooldownSeconds);
        Assert.Equal(1m, h.Actor.Abilities["ghostly-strike"].Definition.GlobalCooldownSeconds);
        var aura = Assert.Single(h.Actor.ActiveAuras, a => a.Definition.Key == "ghostly-strike-dodge");
        Assert.Equal(7m, aura.ExpiresAtSeconds); // Marker only, not defensive-effect certification.
        Assert.Equal(before, JsonSerializer.Serialize(r));
        var snapshot = RogueSimulationRequestSnapshot.Create(JsonSerializer.Deserialize<RogueSimRequest>(before)!);
        Assert.Equal(main, snapshot.MainHandWeaponType); Assert.Equal(off, snapshot.OffHandWeaponType);
        Assert.Equal(legacy, snapshot.BothWeaponsDaggers); Assert.Equal(behind, snapshot.IsBehindTarget);
    }

    [Theory]
    [InlineData(false, 0, 0, "150", "150")][InlineData(true, 0, 0, "216", "216")]
    [InlineData(false, 100, 0, "300", "300")][InlineData(true, 100, 0, "432", "432")]
    [InlineData(false, 0, 5500, "150", "75")][InlineData(true, 0, 5500, "216", "108")]
    [InlineData(false, 100, 5500, "300", "150")][InlineData(true, 100, 5500, "432", "216")]
    public void GhostlyStrikeHasLiteralApCriticalAndArmorOracles(bool dagger, int crit, int armor,
        string raw, string damage)
    {
        var r = Request(); r.MainHandWeaponType = dagger ? "dagger" : "sword";
        r.Character.BaseStats.Set("attack-power", 140); r.Tuning.PhysicalCritPercent = crit;
        r.TargetArmor = armor; var h = new Harness(r); h.At(0, () => h.Cast("ghostly-strike")); h.Run();
        var hit = Assert.Single(h.Damage("ghostly-strike"));
        Assert.Equal(D(raw), hit.RawAmount); Assert.Equal(D(damage), hit.Amount);
    }

    private static decimal D(string value) => decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

    [Theory][InlineData(false, "330")][InlineData(true, "475.2")]
    public void GhostlyStrikeLethalityAndSealFateKeepTheirExistingScopes(bool dagger, string damage)
    {
        var r = Request(); r.MainHandWeaponType = dagger ? "dagger" : "sword";
        r.Character.BaseStats.Set("attack-power", 140); r.Tuning.PhysicalCritPercent = 100;
        r.Talents.Ranks["lethality"] = 5; r.Talents.Ranks["seal-fate"] = 5;
        var h = new Harness(r); h.At(0, () => h.Cast("ghostly-strike")); h.Run();
        Assert.Equal(D(damage), Assert.Single(h.Damage("ghostly-strike")).RawAmount);
        Assert.Equal(2, h.Combat.ComboPointsFor("target"));
    }

    [Fact]
    public void OffHandTuningCannotChangeGhostlyStrikeAndMissingOrZeroRankOmitsIt()
    {
        var r = Request(); r.Character.BaseStats.Set("attack-power", 140);
        var expected = JsonSerializer.Serialize(PreparedAbilities(r).Single(a => a.Key == "ghostly-strike"));
        r.Tuning.OffHandWeaponDamage = 987; r.Tuning.OffHandSpeedSeconds = 4;
        Assert.Equal(expected, JsonSerializer.Serialize(PreparedAbilities(r).Single(a => a.Key == "ghostly-strike")));
        r.Talents.Ranks["ghostly-strike"] = 0;
        Assert.DoesNotContain(PreparedAbilities(r), a => a.Key == "ghostly-strike");
        r.Talents.Ranks.Remove("ghostly-strike");
        Assert.DoesNotContain(PreparedAbilities(r), a => a.Key == "ghostly-strike");
    }

    [Theory][InlineData(false, false)][InlineData(false, true)]
    [InlineData(true, false)][InlineData(true, true)]
    public void LegacyPositionalAndNamedCreateArgumentsKeepTheirMeaning(bool both, bool behind)
    {
        var r = Request(); r.BothWeaponsDaggers = both; r.IsBehindTarget = behind;
        r.MainHandWeaponType = both ? "dagger" : "sword";
        var named = ForeverRogueSpellbook.Create(r.Specialization, r.Talents, r.Tuning,
            attackPower: 0, bothDaggers: both, behindTarget: behind);
        var positional = ForeverRogueSpellbook.Create(r.Specialization, r.Talents, r.Tuning, 0, both, behind);
        Assert.Equal(JsonSerializer.Serialize(named), JsonSerializer.Serialize(positional));
        Assert.Equal(JsonSerializer.Serialize(named), JsonSerializer.Serialize(PreparedAbilities(r)));
        var noPosition = ForeverRogueSpellbook.Create(r.Specialization, r.Talents, r.Tuning, 0, both);
        var explicitBehind = ForeverRogueSpellbook.Create(r.Specialization, r.Talents, r.Tuning, 0, both, true);
        Assert.Equal(JsonSerializer.Serialize(noPosition), JsonSerializer.Serialize(explicitBehind));
    }

    public static IEnumerable<object[]> AvoidanceCases()
    {
        foreach (var ability in new[] { "ghostly-strike", "hemorrhage" })
        foreach (var behind in new[] { false, true })
        foreach (var outcome in new[] { "miss", "dodge", "parry" }) yield return [ability, behind, outcome];
    }

    private static void Avoid(RogueSimRequest r, string outcome)
    {
        r.Tuning.MainHandHitPercent = outcome == "miss" ? 0 : 100;
        r.TargetDodgePercent = outcome == "dodge" ? 100 : 0;
        r.TargetParryPercent = outcome == "parry" ? 100 : 0;
    }

    [Theory][MemberData(nameof(AvoidanceCases))]
    public void ActualAvoidancePreservesCostsPointsAndIndependentDodgeMarker(string ability, bool behind, string outcome)
    {
        var r = Request(); r.IsBehindTarget = behind; Avoid(r, outcome);
        var h = new Harness(r); h.At(0, () => h.Cast(ability)); h.Run();
        var hit = Assert.Single(h.Damage(ability)); var lands = behind && outcome == "parry";
        Assert.Equal(lands ? CombatResultTypes.Hit : outcome, hit.ResultKey);
        Assert.Equal(lands ? 1 : 0, h.Combat.ComboPointsFor("target"));
        Assert.Equal(lands, hit.Amount > 0);
        Assert.Contains(h.Context.Timeline, e => e.Type == CombatEventType.ResourceChanged &&
            e.Amount == (ability == "ghostly-strike" ? -40m : -35m));
        if (ability == "hemorrhage") Assert.Equal(lands, h.Target.ActiveAuras.Any(a => a.Definition.Key == "hemorrhage-rupture"));
        else Assert.Single(h.Actor.ActiveAuras, a => a.Definition.Key == "ghostly-strike-dodge");
    }

    [Theory][InlineData(1, false, false, "24")][InlineData(5, false, false, "120")]
    [InlineData(1, true, false, "27.6")][InlineData(5, true, false, "138")]
    [InlineData(1, true, true, "55.2")][InlineData(5, true, true, "276")]
    public void RealRuptureTicksHaveLiteralOwnedMarkAndCriticalOracles(int points, bool mark, bool critical, string amount)
    {
        var r = Request(); r.TargetArmor = 5500; r.Tuning.PhysicalCritPercent = critical ? 100 : 0;
        var h = new Harness(r, points + 4);
        if (mark) { h.At(0, () => h.Mark()); h.Points(points - 1, 1); }
        else h.Points(points);
        h.At(points, () => h.Cast("rupture")); h.Run();
        Assert.Equal(points, h.Combat.RuptureSnapshotPoints); Assert.Equal(0, h.Combat.ComboPointsFor("target"));
        Assert.NotEmpty(h.Ticks()); Assert.All(h.Ticks(), e => { Assert.Equal(D(amount), e.Amount); Assert.Equal(e.RawAmount, e.Amount); });
        Assert.Equal(6m + 2m * points, h.Actor.Abilities["rupture"].Definition.Effects[0].DurationSeconds);
        Assert.Equal(0, h.Poisons.Applications);
    }

    [Theory][InlineData(0, 0, "100")][InlineData(45, 0, "145")][InlineData(73, 0, "173")]
    [InlineData(45, 140, "165")]
    public void HemorrhageDirectTuningAndSerializedOverridesRemainUnchanged(int bonus, int ap, string amount)
    {
        var r = Request(); r.Tuning.HemorrhageBonusDamage = bonus; r.Character.BaseStats.Set("attack-power", ap);
        var json = JsonSerializer.Serialize(r); var snapshot = RogueSimulationRequestSnapshot.Create(JsonSerializer.Deserialize<RogueSimRequest>(json)!);
        var h = new Harness(snapshot); h.At(0, () => h.Mark()); h.Run();
        Assert.Equal(D(amount), Assert.Single(h.Damage("hemorrhage")).Amount);
        Assert.Equal(1, h.Combat.ComboPointsFor("target"));
        var mark = h.MarkAura(); Assert.Equal("rogue", mark.SourceActorKey); Assert.Equal("target", mark.TargetActorKey);
        Assert.Equal(15m, mark.ExpiresAtSeconds); Assert.Equal(0m, mark.AppliedAtSeconds);
        Assert.Equal(bonus, snapshot.Tuning.HemorrhageBonusDamage); Assert.Equal(json, JsonSerializer.Serialize(r));
        Assert.Equal(45m, JsonSerializer.Deserialize<ForeverRogueTuning>("{}")!.HemorrhageBonusDamage);
    }

    [Theory][InlineData("foreign", "120")][InlineData("wrong-target", "120")]
    [InlineData("own", "138")][InlineData("both", "138")][InlineData("case-owner", "138")]
    [InlineData("lookalike-key", "120")][InlineData("uppercase-key", "120")]
    public void RealApplicationsSelectOnlyTheActualTargetsOwnMarkOnce(string mode, string amount)
    {
        var h = new Harness(Request(), 10); h.Points(5);
        if (mode is "foreign" or "both" or "wrong-target")
            h.At(0, () => h.Mark("other-rogue", mode == "wrong-target" ? "target-2" : "target"));
        if (mode is "own" or "both" or "case-owner" or "lookalike-key" or "uppercase-key")
            h.At(5, () => h.Mark());
        h.At(6, () =>
        {
            if (mode == "case-owner") h.MarkAura().SourceActorKey = "RoGuE";
            if (mode == "lookalike-key") h.MarkAura().Definition.Key = "hemorrhage-rupture-extra";
            if (mode == "uppercase-key") h.MarkAura().Definition.Key = "HEMORRHAGE-RUPTURE";
            h.Cast("rupture");
        });
        h.Run(); Assert.NotEmpty(h.Ticks()); Assert.All(h.Ticks(), e => Assert.Equal(D(amount), e.Amount));
        Assert.Equal(5, h.Combat.RuptureSnapshotPoints); Assert.Equal(0, h.Combat.ComboPointsFor("target-2"));
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void MarkExpiresAtFifteenDuringActualFivePointBleedEvenIfObjectIsRetained(bool retainExpired)
    {
        var h = new Harness(Request(), 22); h.At(0, () => h.Mark()); h.Points(4, 1);
        h.At(5, () => h.Cast("rupture"));
        if (retainExpired)
            h.At(1, () => h.MarkAura().InstanceId = Guid.NewGuid()); // Leave its stale expiry event unable to remove the object.
        h.Run(); var ticks = h.Ticks();
        Assert.Equal(new[] { 7m, 9m, 11m, 13m, 15m, 17m, 19m, 21m }, ticks.Select(e => e.TimeSeconds));
        Assert.Equal(new[] { 138m, 138m, 138m, 138m, 120m, 120m, 120m, 120m }, ticks.Select(e => e.Amount!.Value));
        Assert.Equal(retainExpired, h.Target.ActiveAuras.Any(a => a.Definition.Key == "hemorrhage-rupture"));
        Assert.Equal(5, h.Combat.RuptureSnapshotPoints);
    }

    [Fact]
    public void ApplyingAndRemovingMarkAfterBleedStartsChangesOnlyLaterLiveTicks()
    {
        var h = new Harness(Request(), 22); h.Points(5); h.At(5, () => h.Cast("rupture"));
        h.At(8, () => h.Mark()); h.At(12, () => Assert.Equal(1,
            h.Auras.RemoveAuras(h.Context, h.Target, "hemorrhage-rupture", "rogue")));
        h.Run(); var ticks = h.Ticks();
        Assert.Equal(new[] { 7m, 9m, 11m, 13m, 15m, 17m, 19m, 21m }, ticks.Select(e => e.TimeSeconds));
        Assert.Equal(new[] { 120m, 138m, 138m, 120m, 120m, 120m, 120m, 120m }, ticks.Select(e => e.Amount!.Value));
        Assert.Equal(5, h.Combat.RuptureSnapshotPoints); Assert.Equal(1, h.Combat.ComboPointsFor("target"));
    }

    [Fact]
    public void SuccessfulRefreshAtFiveSurvivesOldExpiryAndEndsAtTwenty()
    {
        var h = new Harness(Request(), 23); Guid original = default; Guid refreshed = default;
        h.At(0, () => h.Mark()); h.Points(4, 1);
        h.At(4.5m, () => original = h.MarkAura().InstanceId);
        h.At(5, () => h.Mark()); h.At(6, () => { refreshed = h.MarkAura().InstanceId; h.Cast("rupture"); });
        h.At(15, () => { Assert.Equal(refreshed, h.MarkAura().InstanceId); Assert.Equal(20m, h.MarkAura().ExpiresAtSeconds); });
        h.Run(); Assert.NotEqual(original, refreshed);
        Assert.Equal(new[] { 8m, 10m, 12m, 14m, 16m, 18m, 20m, 22m }, h.Ticks().Select(e => e.TimeSeconds));
        Assert.Equal(new[] { 138m, 138m, 138m, 138m, 138m, 138m, 120m, 120m }, h.Ticks().Select(e => e.Amount!.Value));
        Assert.DoesNotContain(h.Target.ActiveAuras, a => a.Definition.Key == "hemorrhage-rupture");
    }

    [Theory][InlineData("miss")][InlineData("dodge")][InlineData("parry")]
    public void FailedRefreshPreservesOriginalMarkIdentityAndExpiry(string outcome)
    {
        var r = Request(); r.IsBehindTarget = false; var h = new Harness(r, 23); Guid original = default;
        h.At(0, () => h.Mark()); h.Points(4, 1);
        h.At(4.5m, () => original = h.MarkAura().InstanceId);
        h.At(5, () => { Avoid(r, outcome); h.Mark(); });
        h.At(6, () => { Avoid(r, "none"); Assert.Equal(original, h.MarkAura().InstanceId);
            Assert.Equal(15m, h.MarkAura().ExpiresAtSeconds); h.Cast("rupture"); });
        h.Run(); Assert.Equal(outcome, h.Damage("hemorrhage")[1].ResultKey);
        Assert.Equal(new[] { 138m, 138m, 138m, 138m, 120m, 120m, 120m, 120m }, h.Ticks().Select(e => e.Amount!.Value));
        Assert.Equal(1, h.Context.Timeline.Count(e => e.Type == CombatEventType.AuraApplied && e.AbilityKey == "hemorrhage"));
        Assert.DoesNotContain(h.Target.ActiveAuras, a => a.Definition.Key == "hemorrhage-rupture");
    }

    [Theory][InlineData("before", "120")][InlineData("active", "138")]
    [InlineData("just-before-expiry", "138")][InlineData("at-expiry", "120")]
    [InlineData("after-expiry", "120")]
    public void ActualTickHonorsHalfOpenMarkTimes(string boundary, string amount)
    {
        var h = new Harness(Request(), 8); h.At(0, () => h.Mark()); h.Points(4, 1);
        h.At(5, () => h.Cast("rupture"));
        h.At(6, () => { var aura = h.MarkAura();
            aura.AppliedAtSeconds = boundary == "before" ? 7.001m : 0;
            aura.ExpiresAtSeconds = boundary switch { "just-before-expiry" => 7.001m, "at-expiry" => 7m,
                "after-expiry" => 6.999m, _ => 15m }; });
        h.Run(); Assert.Equal(D(amount), Assert.Single(h.Ticks()).Amount);
    }

    [Fact]
    public void LandedZeroDamageCanApplyMarkWithoutPositiveDamageComboPoint()
    {
        var r = Request(); r.Tuning.MainHandWeaponDamage = 0; r.Tuning.HemorrhageBonusDamage = 0;
        var h = new Harness(r); h.At(0, () => h.Mark()); h.Run();
        Assert.Equal(0m, Assert.Single(h.Damage("hemorrhage")).Amount);
        Assert.Equal(CombatResultTypes.Hit, h.Damage("hemorrhage")[0].ResultKey);
        Assert.Equal(0, h.Combat.ComboPointsFor("target")); Assert.Equal(15m, h.MarkAura().ExpiresAtSeconds);
    }

    [Fact]
    public void PeriodicTicksKeepCritAndThousandCutsButDoNotRerollAvoidanceOrProcPoison()
    {
        var r = Request(); r.Talents.Ranks["thousand-cuts"] = 1;
        r.MainHandPoison = "instant"; r.UseInstantPoison = true;
        var h = new Harness(r, 22); h.At(0, () => h.Mark()); h.Points(4, 1);
        h.At(5, () => h.Cast("rupture")); var applications = -1;
        h.At(6, () => { applications = h.Poisons.Applications; r.Tuning.MainHandHitPercent = 0;
            r.TargetDodgePercent = r.TargetParryPercent = 100; r.IsBehindTarget = false;
            r.Tuning.PhysicalCritPercent = 100; });
        h.Run(); Assert.Equal(8, h.Ticks().Length); Assert.Equal(5, h.Combat.ThousandCutsStacks);
        Assert.Equal(applications, h.Poisons.Applications);
        Assert.Equal(new[] { 276m, 276m, 276m, 276m, 240m, 240m, 240m, 240m }, h.Ticks().Select(e => e.Amount!.Value));
        Assert.All(h.Ticks(), e => Assert.True(e.IsCritical));
    }

    [Fact]
    public void MarkDoesNotBuffHemorrhageGarroteEviscerateOrAutos()
    {
        var r = Request(); var h = new Harness(r, 8); h.At(0, () => h.Mark());
        h.At(1, () => h.Mark()); h.At(2, () => h.Cast("eviscerate"));
        h.At(3, () => { h.Auras.ApplyAura(h.Context, h.Actor,
            new() { Key = "rogue-stealth", DurationSeconds = 20 }, "rogue", "test", "test"); h.Cast("garrote"); });
        h.At(4, () => h.Executor.ExecuteBackgroundDirectDamage(h.Context, h.Actor, h.Target,
            "rogue-main-hand", "Auto attack", new() { Key = "weapon-hit", EffectType = AbilityEffectTypes.DirectDamage,
                TargetType = AbilityTargetTypes.Enemy, SchoolKey = "physical", ResolutionType = CombatResolutionTypes.Melee,
                CanMiss = true, CanCrit = true, WeaponHandKey = WeaponHandKeys.MainHand, MinimumValue = 100, MaximumValue = 100 }));
        h.Run(); Assert.Equal(new[] { 145m, 145m }, h.Damage("hemorrhage").Select(e => e.Amount!.Value));
        Assert.Equal(320m, Assert.Single(h.Damage("eviscerate")).Amount);
        Assert.Equal(65m, Assert.Single(h.Damage("garrote")).Amount);
        Assert.Equal(100m, Assert.Single(h.Damage("rogue-main-hand")).Amount);
    }

    [Fact]
    public void OwnedMarkComposesWithExistingConditionalBonusesWithoutChangingTheirArithmetic()
    {
        var r = Request(); r.Talents.Ranks["master-of-subtlety"] = 2;
        r.Talents.Ranks["improved-kidney-shot"] = 2;
        var h = new Harness(r, 8); h.At(0, () => h.Mark()); h.Points(4, 1);
        h.At(5, () => h.Cast("rupture"));
        h.At(6, () => {
            h.Auras.ApplyAura(h.Context, h.Actor, new() { Key = "master-of-subtlety-window", DurationSeconds = 3 },
                "rogue", "test", "test");
            h.Auras.ApplyAura(h.Context, h.Target, new() { Key = "kidney-shot-stun", DurationSeconds = 3 },
                "rogue", "test", "test");
        });
        h.Run(); // Existing (1 + .06 + .06), then own mark: 120 * 1.12 * 1.15.
        Assert.Equal(154.56m, Assert.Single(h.Ticks()).Amount);
    }

    [Fact]
    public void ForeignGeneratorsAndTicksCannotGiveActingRoguePointsOrThousandCutsStacks()
    {
        var r = Request(); r.Talents.Ranks["thousand-cuts"] = 1;
        var h = new Harness(r, 5); h.At(0, () => h.Mark("other-rogue"));
        h.At(1, () => h.Cast("rupture", source: "other-rogue")); h.Run();
        Assert.NotEmpty(h.Ticks()); Assert.All(h.Ticks(), e => Assert.Equal("other-rogue", e.SourceActorKey));
        Assert.Equal(0, h.Combat.ComboPointsFor("target")); Assert.Equal(0, h.Combat.FinishersUsed);
        Assert.Equal(0, h.Combat.ThousandCutsStacks); Assert.Equal(0, h.Combat.RuptureSnapshotPoints);
    }

    [Fact]
    public void EachTargetsBleedKeepsItsOwnComboPointSnapshotAndLiveMark()
    {
        var h = new Harness(Request(), 14); h.Points(5); h.Points(1, 5, "target-2");
        h.At(6, () => h.Cast("rupture")); h.At(7, () => h.Cast("rupture", "target-2"));
        h.At(9.5m, () => h.Mark()); h.Run();
        var first = h.Ticks().Where(e => e.TargetActorKey == "target").ToArray();
        var second = h.Ticks().Where(e => e.TargetActorKey == "target-2").ToArray();
        Assert.Equal(new[] { 8m, 10m, 12m }, first.Select(e => e.TimeSeconds));
        Assert.Equal(new[] { 120m, 138m, 138m }, first.Select(e => e.Amount!.Value));
        Assert.Equal(new[] { 9m, 11m, 13m }, second.Select(e => e.TimeSeconds));
        Assert.All(second, e => Assert.Equal(24m, e.Amount));
        Assert.Equal(1, h.Combat.ComboPointsFor("target")); Assert.Equal(0, h.Combat.ComboPointsFor("target-2"));
    }

    [Theory][InlineData("assassination", "dagger", 180)][InlineData("assassination", "sword", 125)]
    [InlineData("combat", "dagger", 180)][InlineData("combat", "sword", 125)]
    public void ExplicitCrossTreeCastsUseCorrectedDefinitionsDespiteFixedPriorityExclusion(
        string spec, string weapon, int ghostlyDamage)
    {
        var r = Request(); r.Specialization = r.Character.SpecializationKey = spec;
        r.MainHandWeaponType = weapon; r.BothWeaponsDaggers = false;
        var preview = RogueSimRunner.Preview(r);
        Assert.DoesNotContain(preview.Rotation.Entries, e => e.AbilityKey is "ghostly-strike" or "hemorrhage");
        var h = new Harness(r, 5); h.At(0, () => h.Cast("ghostly-strike")); h.At(1, () => h.Mark());
        h.At(2, () => h.Cast("rupture")); h.Run();
        Assert.Equal(ghostlyDamage, Assert.Single(h.Damage("ghostly-strike")).Amount);
        Assert.Equal(145m, Assert.Single(h.Damage("hemorrhage")).Amount);
        Assert.Equal(2, h.Combat.RuptureSnapshotPoints); Assert.Equal(55.2m, Assert.Single(h.Ticks()).Amount);
    }
}
