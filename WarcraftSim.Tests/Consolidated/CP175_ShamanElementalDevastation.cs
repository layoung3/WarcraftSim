using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Shaman;

namespace WarcraftSim.Tests.Consolidated;

// Independent historical-source expectations: 3/6/9 melee crit points, ten
// seconds, direct harmful spell criticals, no passive/proxy/periodic triggers.
public sealed class Cp175ShamanElementalDevastation
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 3)]
    [InlineData(2, 6)]
    [InlineData(3, 9)]
    public void EveryRankUsesAnOwnedTemporaryBonusWithAnExactHalfOpenEnd(int rank, int expected)
    {
        var h = new Harness(rank, 12m);
        var stats = JsonSerializer.Serialize(h.Owner.Stats);
        h.At(.999m, () => Assert.Equal(0m, h.Bonus));
        h.Critical(1m);
        h.At(1m, () => Assert.Equal((decimal)expected, h.Bonus));
        h.At(10.999m, () => Assert.Equal((decimal)expected, h.Bonus));
        // This probe was queued before the expiry event. The roll still sees
        // the aura as inactive at the exact end, regardless of queue position.
        h.At(11m, () => Assert.Equal(0m, h.Bonus));
        h.At(11.001m, () => Assert.Equal(0m, h.Bonus));
        var run = h.Run();
        Assert.Equal(rank == 0 ? 0 : 1, h.Processor.Procs);
        Assert.Equal(rank == 0 ? 0 : 1, run.Timeline.Count(e =>
            e.Type == CombatEventType.AuraApplied && e.AbilityKey == "elemental-devastation"));
        if (rank > 0)
            Assert.Contains(run.Timeline, e => e.Type == CombatEventType.AuraRemoved &&
                e.AbilityKey == "elemental-devastation" && e.TimeSeconds == 11m);
        Assert.Equal(stats, JsonSerializer.Serialize(h.Owner.Stats));
        Assert.Empty(h.Owner.ActiveAuras);
        Assert.Empty(h.Other.ActiveAuras);
    }

    [Theory]
    [InlineData(1, 3)]
    [InlineData(2, 6)]
    [InlineData(3, 9)]
    public void RefreshNeverStacksAndTheOldExpirationCannotRemoveTheNewProc(int rank, int expected)
    {
        var h = new Harness(rank, 17m);
        h.Critical(1m);
        Guid? first = null;
        h.At(2m, () => first = Assert.Single(h.Owner.ActiveAuras).InstanceId);
        h.Critical(5m, "lightning-bolt");
        h.At(6m, () =>
        {
            var refreshed = Assert.Single(h.Owner.ActiveAuras);
            Assert.NotEqual(first, refreshed.InstanceId);
            Assert.Equal(1, refreshed.Stacks);
            Assert.Equal(15m, refreshed.ExpiresAtSeconds);
        });
        foreach (var at in new[] { 10.999m, 11m, 11.001m, 14.999m })
            h.At(at, () => Assert.Equal((decimal)expected, h.Bonus));
        h.At(15m, () => Assert.Equal(0m, h.Bonus));
        var run = h.Run();
        Assert.Equal(2, h.Processor.Procs);
        Assert.Equal(15m, Assert.Single(run.Timeline,
            e => e.Type == CombatEventType.AuraRemoved && e.AbilityKey == "elemental-devastation").TimeSeconds);
    }

    [Fact]
    public void ForeignCriticalsAndForeignCopiesCannotBuffTheOwnerOrAnotherActor()
    {
        var h = new Harness(3, 15m);
        h.Critical(1m, source: "other");
        h.At(2m, () =>
        {
            Assert.Equal(0, h.Processor.Procs);
            Assert.Equal(0m, h.Bonus);
            h.Auras.ApplyAura(h.Context, h.Owner, new AuraDefinition
            {
                Key = ShamanElementalDevastationProcessor.AuraKey,
                Name = "Foreign Elemental Devastation",
                DurationSeconds = 10m
            }, h.Other.Key, "elemental-devastation", "foreign");
            Assert.Equal(0m, h.Bonus);
        });
        h.Critical(3m);
        h.At(4m, () =>
        {
            Assert.Equal(9m, h.Bonus);
            Assert.Equal(0m, h.Processor.MeleeCriticalChanceBonus(h.Other, h.Context.CurrentTimeSeconds));
            h.Auras.RemoveAuras(h.Context, h.Owner,
                ShamanElementalDevastationProcessor.AuraKey, h.Owner.Key);
            Assert.Equal(0m, h.Bonus);
        });
        h.Critical(5m);
        h.At(6m, () => Assert.Equal(9m, h.Bonus));
        h.Run();
        Assert.Equal(2, h.Processor.Procs);
    }

    [Theory]
    [InlineData("lightning-bolt")]
    [InlineData("lightning-bolt-rank7")]
    [InlineData("maelstrom-lightning-bolt")]
    [InlineData("chain-lightning")]
    [InlineData("earth-shock")]
    [InlineData("flame-shock")]
    [InlineData("lava-burst")]
    public void ExistingDirectPlayerSpellCriticalsQualify(string ability)
    {
        var h = new Harness(2, 3m);
        h.Critical(1m, ability);
        h.At(2m, () => Assert.Equal(6m, h.Bonus));
        h.Run();
        Assert.Equal(1, h.Processor.Procs);
    }

    [Theory]
    [InlineData("lightning-overload-bolt")]
    [InlineData("lightning-overload-chain")]
    [InlineData("fire-nova")]
    [InlineData("searing-totem")]
    [InlineData("call-of-elements")]
    [InlineData("call-of-ancestors")]
    [InlineData("call-of-spirits")]
    [InlineData("lightning-shield")]
    [InlineData("flametongue-proc-main-hand")]
    [InlineData("frostbrand-proc-main-hand")]
    [InlineData("windfury-weapon-extra")]
    [InlineData("windfury-totem-extra")]
    [InlineData("stormstrike")]
    [InlineData("main-hand-auto")]
    [InlineData("off-hand-auto")]
    [InlineData("unknown-spell")]
    public void PassiveProxyAndOtherCriticalsDoNotGenerateOrRefreshTheProc(string ability)
    {
        var h = new Harness(3, 12m);
        h.Critical(0m);
        h.Critical(8m, ability);
        h.At(10m, () => Assert.Equal(0m, h.Bonus));
        h.Run();
        Assert.Equal(1, h.Processor.Procs);
    }

    [Theory]
    [InlineData("periodic")]
    [InlineData("channel")]
    [InlineData("melee-resolution")]
    [InlineData("main-hand")]
    [InlineData("off-hand")]
    [InlineData("not-critical")]
    [InlineData("miss")]
    [InlineData("cast-only")]
    public void CorrectSpellNameDoesNotBypassTheCriticalDirectSpellContract(string invalid)
    {
        var h = new Harness(3, 3m);
        var e = CriticalEvent(1m, "lightning-bolt");
        switch (invalid)
        {
            case "periodic": e.EffectDeliveryType = CombatEffectDeliveryType.Periodic; break;
            case "channel": e.EffectDeliveryType = CombatEffectDeliveryType.ChannelTick; break;
            case "melee-resolution": e.ResolutionType = CombatResolutionTypes.Melee; break;
            case "main-hand": e.WeaponHandKey = "main-hand"; break;
            case "off-hand": e.WeaponHandKey = "off-hand"; break;
            case "not-critical": e.IsCritical = false; break;
            case "miss": e.ResultKey = "miss"; break;
            case "cast-only": e.Type = CombatEventType.AbilityCastCompleted; break;
        }
        h.Context.ScheduleEvent(e);
        h.Run();
        Assert.Equal(0, h.Processor.Procs);
        Assert.Empty(h.Owner.ActiveAuras);
    }

    [Fact]
    public void AResolvedCriticalWithZeroDamageStillQualifies()
    {
        var h = new Harness(1, 3m);
        var e = CriticalEvent(1m, "earth-shock");
        e.Amount = 0m;
        h.Context.ScheduleEvent(e);
        h.At(2m, () => Assert.Equal(3m, h.Bonus));
        h.Run();
        Assert.Equal(1, h.Processor.Procs);
    }

    [Theory]
    [InlineData("earth-shock", 0)]
    [InlineData("lightning-bolt", 2.5)]
    [InlineData("maelstrom-lightning-bolt", 0)]
    public void RealEnhancementSpellExecutionProducesTheOwnedProc(string key, double impact)
    {
        var h = new Harness(3, 14m);
        var talents = new ForeverEnhancementShamanTalents { Ranks = new() { ["maelstrom-weapon"] = 5 } };
        h.Owner.AddAbility(ForeverEnhancementShamanSpellbook.Create(new(), talents, 0m, 3m, 1m, 1m)
            .Single(a => a.Key == key));
        h.Owner.AddResource(new("mana", 10000m, 10000m));
        var executor = new AbilityExecutor(h.Auras,
            new SimpleCombatRollResolver(criticalChancePercent: 100m), new Unmitigated());
        h.At(0m, () =>
        {
            if (key == "maelstrom-lightning-bolt")
                h.Auras.ApplyAura(h.Context, h.Owner, new AuraDefinition
                { Key = "maelstrom-ready", Name = "Maelstrom", DurationSeconds = 30m }, h.Owner.Key, key, "ready");
            Assert.True(executor.TryStartAbility(h.Context, h.Owner.Key, "target", key).Success);
        });
        h.At((decimal)impact + .001m, () => Assert.Equal(9m, h.Bonus));
        h.At((decimal)impact + 10m, () => Assert.Equal(0m, h.Bonus));
        var run = h.Run(executor);
        Assert.Equal(1, h.Processor.Procs);
        Assert.Equal((decimal)impact, Assert.Single(run.Timeline,
            e => e.Type == CombatEventType.Damage && e.AbilityKey == key).TimeSeconds);
        Assert.Equal((decimal)impact, Assert.Single(run.Timeline,
            e => e.Type == CombatEventType.AuraApplied && e.AbilityKey == "elemental-devastation").TimeSeconds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActualRunnerGainsMeleeCriticalsWithoutChangingItsSpellCriticalsOrStaticStats(bool dualWield)
    {
        var r = RunnerRequest(dualWield);
        var runner = new EnhancementShamanSimRunner();
        var baseline = runner.Run(r);
        var baselinePreview = EnhancementShamanSimRunner.Preview(r);
        r.Talents.Ranks["elemental-devastation"] = 3;
        var unchanged = JsonSerializer.Serialize(r);
        var candidate = runner.Run(r);
        var replay = runner.Run(r);
        var candidatePreview = EnhancementShamanSimRunner.Preview(r);
        Assert.Equal(unchanged, JsonSerializer.Serialize(r));
        foreach (var key in new[] { "earth-shock", "lightning-bolt" })
        {
            Assert.Contains(Damage(candidate), e => e.AbilityKey == key && e.IsCritical);
            Assert.Equal(Projection(Damage(baseline).Where(e => e.AbilityKey == key)),
                Projection(Damage(candidate).Where(e => e.AbilityKey == key)));
        }
        foreach (var key in dualWield ? new[] { "main-hand-auto", "off-hand-auto" } : new[] { "main-hand-auto" })
        {
            var before = Damage(baseline).Where(e => e.AbilityKey == key).ToArray();
            var after = Damage(candidate).Where(e => e.AbilityKey == key).ToArray();
            Assert.Equal(before.Length, after.Length);
            Assert.True(after.Count(e => e.IsCritical) > before.Count(e => e.IsCritical),
                $"The deterministic {key} fixture must include an additional melee critical.");
            Assert.True(after.Sum(e => e.Amount) > before.Sum(e => e.Amount));
        }
        Assert.Equal(baselinePreview.AttackPower, candidatePreview.AttackPower);
        Assert.Equal(baselinePreview.SpellPower, candidatePreview.SpellPower);
        Assert.Equal(baselinePreview.MaximumMana, candidatePreview.MaximumMana);
        Assert.Equal(baselinePreview.MaximumHealth, candidatePreview.MaximumHealth);
        Assert.Equal(JsonSerializer.Serialize(baselinePreview.EffectiveStats),
            JsonSerializer.Serialize(candidatePreview.EffectiveStats));
        Assert.Equal(candidate.DamageDone, replay.DamageDone);
        Assert.Equal(Projection(candidate.Run.Timeline), Projection(replay.Run.Timeline));
        Assert.Equal(Damage(candidate).Count(e => e.IsCritical && e.WeaponHandKey is null),
            candidate.Run.CustomMetrics["shaman-elemental-devastation-procs"]);
        r.Talents.Ranks.Remove("elemental-devastation");
        var restored = runner.Run(r);
        Assert.Equal(Projection(baseline.Run.Timeline), Projection(restored.Run.Timeline));
        Assert.Equal(baseline.DamageDone, restored.DamageDone);
        Assert.DoesNotContain("shaman-elemental-devastation-procs", restored.Run.CustomMetrics.Keys);
    }

    [Fact]
    public void ElementalCasterRecordsTheProcWithoutInventingAMeleeOrSpellDamageBenefit()
    {
        var r = new ElementalShamanSimRequest
        {
            DurationSeconds = 25m, BaseSpellHitPercent = 100m, SpellCritPercent = 100m,
            MajorManaPotionCharges = 0, CustomPriority = ["earth-shock", "lightning-bolt"],
            Talents = new() { ElementalPointsSpent = 3, EnhancementPointsSpent = 0,
                RestorationPointsSpent = 0, Ranks = new() },
            Tuning = new() { EarthShockMana = 0m, LightningBoltMana = 0m }
        };
        var baseline = new ElementalShamanSimRunner().Run(r);
        r.Talents.Ranks["elemental-devastation"] = 3;
        var before = JsonSerializer.Serialize(r);
        var candidate = new ElementalShamanSimRunner().Run(r);
        Assert.Equal(before, JsonSerializer.Serialize(r));
        Assert.Equal(baseline.DamageDone, candidate.DamageDone);
        Assert.Equal(baseline.EndingMana, candidate.EndingMana);
        Assert.Equal(Projection(baseline.Run.Timeline.Where(e => e.Type == CombatEventType.Damage)),
            Projection(candidate.Run.Timeline.Where(e => e.Type == CombatEventType.Damage)));
        Assert.DoesNotContain(candidate.Run.Timeline, e => e.WeaponHandKey is not null);
        var criticals = candidate.Run.Timeline.Count(e => e.Type == CombatEventType.Damage && e.IsCritical);
        Assert.True(criticals > 0);
        Assert.Equal((decimal)criticals, candidate.Run.CustomMetrics["shaman-elemental-devastation-procs"]);
        Assert.Equal(criticals, candidate.Run.Timeline.Count(e =>
            e.Type == CombatEventType.AuraApplied && e.AbilityKey == "elemental-devastation"));
    }

    [Fact]
    public void ActualFireNovaAndCriticalSearingTicksNeverProcElementalDevastation()
    {
        var r = new ElementalShamanSimRequest
        {
            DurationSeconds = 30m, BaseSpellHitPercent = 100m, SpellCritPercent = 100m,
            MajorManaPotionCharges = 0, CustomPriority = ["searing-totem", "fire-nova"],
            Talents = new() { ElementalPointsSpent = 3, EnhancementPointsSpent = 0,
                RestorationPointsSpent = 0, Ranks = new() { ["elemental-devastation"] = 3 } }
        };
        var result = new ElementalShamanSimRunner().Run(r);
        foreach (var key in new[] { "searing-totem", "fire-nova" })
            Assert.Contains(result.Run.Timeline, e => e.Type == CombatEventType.Damage &&
                e.AbilityKey == key && e.IsCritical);
        Assert.Equal(0m, result.Run.CustomMetrics["shaman-elemental-devastation-procs"]);
        Assert.DoesNotContain(result.Run.Timeline, e =>
            e.Type == CombatEventType.AuraApplied && e.AbilityKey == "elemental-devastation");
    }

    [Theory]
    [InlineData("flametongue")]
    [InlineData("frostbrand")]
    public void RealWeaponProcsAndShieldPreserveTheirNoCriticalContractAndCannotTriggerTheBuff(string imbue)
    {
        var r = RunnerRequest(false);
        r.DurationSeconds = 40m;
        r.CriticalChancePercent = 100m;
        r.Talents.Ranks["elemental-devastation"] = 3;
        r.MainHandImbue = imbue;
        r.FrostbrandProcChancePercent = 100m;
        r.UseEarthShock = false;
        r.UseLightningBoltFiller = false;
        r.UseLightningShield = true;
        r.IncomingBossHitDamage = 10m;
        var result = new EnhancementShamanSimRunner().Run(r);
        var procHits = Damage(result).Where(e =>
            e.AbilityKey!.Contains(imbue, StringComparison.Ordinal) || e.AbilityKey == "lightning-shield").ToArray();
        Assert.Contains(procHits, e => e.AbilityKey!.Contains(imbue, StringComparison.Ordinal));
        Assert.Contains(procHits, e => e.AbilityKey == "lightning-shield");
        Assert.All(procHits, e => Assert.False(e.IsCritical));
        Assert.Equal(0m, result.Run.CustomMetrics["shaman-elemental-devastation-procs"]);
        Assert.DoesNotContain(result.Run.Timeline, e =>
            e.Type == CombatEventType.AuraApplied && e.AbilityKey == "elemental-devastation");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void InvalidProcessorRanksAreRejected(int rank) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Harness(rank, 1m));

    private static EnhancementShamanSimRequest RunnerRequest(bool dualWield) => new()
    {
        Seed = 175813, DurationSeconds = 180m, TargetMaximumHealth = 10000000m,
        TargetArmor = 0m, BaseMeleeHitPercent = 100m, BaseSpellHitPercent = 100m,
        CriticalChancePercent = 25m, ProvisionalGlancingChancePercent = 0m,
        WeaponMinDamage = 100m, WeaponMaxDamage = 100m, WeaponSpeedSeconds = 1.25m,
        DualWield = dualWield, OffHandWeaponMinDamage = 80m, OffHandWeaponMaxDamage = 80m,
        OffHandWeaponSpeedSeconds = 1.5m, MainHandImbue = "none", OffHandImbue = "none",
        UseWindfuryWeapon = false, UseWindfuryTotem = false, UseStormstrike = false,
        UseRageOfTheFarseer = false, UseMaelstromLightningBolt = false,
        UseEarthShock = true, UseLightningBoltFiller = true,
        Talents = new() { ElementalPointsSpent = 3, EnhancementPointsSpent = 0, Ranks = new() },
        Tuning = new() { EarthShockMana = 0m, LightningBoltMana = 0m }
    };

    private static IEnumerable<CombatEvent> Damage(EnhancementShamanSimResponse r) =>
        r.Run.Timeline.Where(e => e.Type == CombatEventType.Damage && e.SourceActorKey == "shaman");

    private static string Projection(IEnumerable<CombatEvent> events) => JsonSerializer.Serialize(
        events.Select(e => new { e.TimeSeconds, e.Type, e.SourceActorKey, e.TargetActorKey,
            e.AbilityKey, e.EffectKey, e.ResultKey, e.Amount, e.IsCritical, e.EffectDeliveryType,
            e.WeaponHandKey, e.ResourceTotalAfter }));

    private static CombatEvent CriticalEvent(decimal time, string key, string source = "shaman") => new()
    {
        TimeSeconds = time, Type = CombatEventType.Damage, SourceActorKey = source,
        TargetActorKey = "target", AbilityKey = key, EffectKey = "test-impact",
        SchoolKey = "nature", ResolutionType = CombatResolutionTypes.Spell,
        ResultKey = "critical", IsCritical = true, Amount = 100m
    };

    private sealed class Harness
    {
        public SimulationContext Context { get; }
        public SimulationActorState Owner { get; } = new() { Key = "shaman", TeamKey = "raid", Level = 60 };
        public SimulationActorState Other { get; } = new() { Key = "other", TeamKey = "raid", Level = 60 };
        public AuraManager Auras { get; } = new();
        public ShamanElementalDevastationProcessor Processor { get; }
        public decimal Bonus => Processor.MeleeCriticalChanceBonus(Owner, Context.CurrentTimeSeconds);
        private readonly Dictionary<string, Action> _steps = [];
        public Harness(int rank, decimal duration)
        {
            Context = new(new() { DurationSeconds = duration, CaptureTimeline = true, PrimaryActorKey = "shaman" });
            Owner.InitializeHealth(1000m);
            Other.InitializeHealth(1000m);
            var target = new SimulationActorState { Key = "target", TeamKey = "enemy", Level = 63 };
            target.InitializeHealth(100000m);
            Context.AddActor(Owner);
            Context.AddActor(Other);
            Context.AddActor(target);
            Processor = new(Owner, Auras, rank);
        }
        public void Critical(decimal time, string ability = "earth-shock", string source = "shaman") =>
            Context.ScheduleEvent(CriticalEvent(time, ability, source));
        public void At(decimal time, Action action)
        {
            var key = $"probe-{_steps.Count}";
            _steps.Add(key, action);
            Context.ScheduleEvent(new() { TimeSeconds = time, Type = CombatEventType.RotationDecision,
                EffectKey = key, IsInternal = true });
        }
        public SimulationRunResult Run(params ICombatEventProcessor[] before) =>
            new SimulationEngine([..before, Auras, Processor, new Steps(_steps)]).Run(Context);
    }

    private sealed class Steps(Dictionary<string, Action> actions) : ICombatEventProcessor
    {
        public void Process(SimulationContext context, CombatEvent e)
        {
            if (e.EffectKey is not null && actions.TryGetValue(e.EffectKey, out var action)) action();
        }
    }
    private sealed class Unmitigated : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext context, SimulationActorState source,
            SimulationActorState target, AbilityDefinition ability, AbilityEffectDefinition effect,
            decimal rawAmount) => DamageMitigationResult.Unmitigated(rawAmount);
    }
}
