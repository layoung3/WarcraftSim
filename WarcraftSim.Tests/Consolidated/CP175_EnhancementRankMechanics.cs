using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Shaman;

namespace WarcraftSim.Tests.Consolidated;

// Independent numeric/event oracles for the historical Shaman talent tree:
// gunba/wow-forever-sim @ 1cc5ae1e9abdbf941b7326d2d90421822dce31ba,
// ui/core/talents/trees/shaman.json. Not current-client certification.
public sealed class Cp175EnhancementRankMechanics
{
    private static EnhancementShamanSimRequest Input()
    {
        var r = new EnhancementShamanSimRequest
        {
            DurationSeconds = 24m, CaptureTimeline = true,
            UseWindfuryWeapon = false, UseWindfuryTotem = false,
            UseStormstrike = false, UseEarthShock = false,
            UseMaelstromLightningBolt = false, UseLightningBoltFiller = false,
            UseRageOfTheFarseer = false, TargetArmor = 0m,
            WeaponMinDamage = 100m, WeaponMaxDamage = 100m, WeaponSpeedSeconds = 2.8m,
            BaseMeleeHitPercent = 100m, BaseSpellHitPercent = 100m,
            CriticalChancePercent = 0m, ProvisionalGlancingChancePercent = 0m
        };
        r.Character.Race = "orc";
        r.Character.BaseStats.Values = new()
        {
            ["strength"] = 100m, ["agility"] = 100m, ["stamina"] = 100m,
            ["intellect"] = 100m, ["spirit"] = 100m, ["attack-power"] = 700m
        };
        r.Talents.Ranks.Clear();
        r.Tuning.ManaRegenPerSecond = 0m;
        return r;
    }

    public static IEnumerable<object[]> DexterityCases()
    {
        decimal[] plain = [0m, 39.6m, 80.4m, 120m];
        decimal[] knowledge = [0m, 43.56m, 88.44m, 132m];
        foreach (var mode in new[] { "legacy", "observed", "fallback" })
            for (var rank = 0; rank <= 3; rank++)
            {
                yield return [mode, rank, 0, 120m, plain[rank]];
                yield return [mode, rank, 5, 132m, knowledge[rank]];
            }
    }

    [Theory, MemberData(nameof(DexterityCases))]
    public void MentalDexterityUsesExactRanksAfterAllIntellectContributions(
        string mode, int rank, int knowledgeRank, decimal intellect, decimal bonusAp)
    {
        var r = Input();
        r.Talents.Ranks["mental-dexterity"] = rank;
        r.Talents.Ranks["ancestral-knowledge"] = knowledgeRank;
        r.Character.Equipment.Items.Add(new() { SlotKey = "chest", ItemKey = "authored",
            Stats = new() { Values = new() { ["intellect"] = 20m, ["strength"] = 10m } } });
        if (mode != "legacy")
            r.Level60Baseline = new()
            {
                Race = "orc", ClassKey = "shaman", Strength = 100m, Agility = 100m,
                Stamina = 100m, Intellect = 100m, Spirit = 100m,
                ObservedUnbuffedMeleeAttackPower = 700m,
                ObservedUnbuffedMaximumMana = mode == "observed" ? 2000m : null,
                ObservedUnbuffedMaximumHealth = mode == "observed" ? 3000m : null,
                ClientBuild = "synthetic", SourceReference = "controlled-rank-test",
                RecordedAtUtc = "2026-11-10T00:00:00Z"
            };
        var before = JsonSerializer.Serialize(r);
        var preview = EnhancementShamanSimRunner.Preview(r);
        var run = new EnhancementShamanSimRunner().Run(r);
        var expectedAp = (mode == "legacy" ? 700m : 720m) + bonusAp;
        Assert.Equal(intellect, preview.EffectiveStats.Get("intellect"));
        Assert.Equal(expectedAp, preview.AttackPower);
        Assert.Equal(expectedAp, run.AttackPower);
        var hits = run.Run.Timeline.Where(e => e.Type == CombatEventType.Damage &&
            e.AbilityKey == "main-hand-auto" && e.Amount > 0m).ToArray();
        Assert.NotEmpty(hits);
        Assert.All(hits, e => Assert.Equal(100m + expectedAp / 5m, e.Amount));
        Assert.Equal(before, JsonSerializer.Serialize(r));
    }

    [Theory]
    [InlineData(0, 260, 350.8)]
    [InlineData(1, 262.6, 358.556)]
    [InlineData(2, 265.4, 365.204)]
    [InlineData(3, 268, 372.96)]
    public void ElementalWeaponsUsesDistinctWindfuryAndUnchangedRockbiterRankTables(
        int rank, double expectedWindfury, double expectedRockbiter)
    {
        var r = Input();
        r.UseWindfuryWeapon = true;
        r.UseWindfuryTotem = true;
        r.Talents.Ranks["elemental-weapons"] = rank;
        r.Tuning.WindfuryWeaponBonusAttackPower = 100m;
        r.Tuning.WindfuryWeaponProcChancePercent = 100m;
        r.Tuning.WindfuryWeaponInternalCooldownSeconds = 0m;
        var before = JsonSerializer.Serialize(r);
        var wf = new EnhancementShamanSimRunner().Run(r);
        var extras = wf.Run.Timeline.Where(e => e.Type == CombatEventType.Damage &&
            e.AbilityKey == "windfury-weapon-extra" && e.Amount > 0m).ToArray();
        Assert.NotEmpty(extras);
        // Flat 100 weapon + 700 base AP / 5 + 100 * 1/1.13/1.27/1.40 bonus AP / 5.
        Assert.All(extras, e => Assert.Equal((decimal)expectedWindfury, e.Amount));
        var mainHits = wf.Run.Timeline.Count(e => e.Type == CombatEventType.Damage &&
            e.AbilityKey == "main-hand-auto" && e.Amount > 0m);
        Assert.Equal(mainHits, wf.WindfuryWeaponProcs);
        Assert.Equal(mainHits * 2, wf.WindfuryWeaponExtraAttacks);
        Assert.Equal(0, wf.WindfuryTotemProcs);
        Assert.Equal(before, JsonSerializer.Serialize(r));
        r.MainHandImbue = "rockbiter";
        var rockbiter = new EnhancementShamanSimRunner().Run(r);
        var rockHits = rockbiter.Run.Timeline.Where(e => e.Type == CombatEventType.Damage &&
            e.AbilityKey == "main-hand-auto" && e.TimeSeconds > 2m && e.Amount > 0m).ToArray();
        Assert.NotEmpty(rockHits);
        Assert.All(rockHits, e => Assert.Equal((decimal)expectedRockbiter, e.Amount));
    }

    [Theory]
    [InlineData(0, 370, 430, 450, 400)]
    [InlineData(1, 203.5, 236.5, 247.5, 220)]
    public void ShamanisticFocusDiscountsOnlyShocksAndShieldInBothSpellbooks(
        int rank, double shieldCost, double enhancementShock, double elementalShock, double flameShock)
    {
        var enhancement = new ForeverEnhancementShamanTalents { Ranks = new() { ["shamanistic-focus"] = rank } };
        var elemental = new ForeverElementalShamanTalents { Ranks = new() { ["shamanistic-focus"] = rank } };
        var enhance = ForeverEnhancementShamanSpellbook.Create(new(), enhancement, 0m, 2.8m, 100m, 100m);
        var ele = ForeverElementalShamanSpellbook.Create(elemental, new(), 0m);
        decimal Cost(IReadOnlyList<AbilityDefinition> spells, string key) =>
            spells.Single(s => s.Key == key).ResourceCosts.Single(c => c.ResourceKey == "mana").Amount;
        Assert.Equal((decimal)shieldCost, Cost(enhance, "lightning-shield"));
        Assert.Equal((decimal)shieldCost, Cost(ele, "lightning-shield"));
        Assert.Equal((decimal)enhancementShock, Cost(enhance, "earth-shock"));
        Assert.Equal((decimal)elementalShock, Cost(ele, "earth-shock"));
        Assert.Equal((decimal)flameShock, Cost(ele, "flame-shock"));
        Assert.Equal(320m, Cost(enhance, "lightning-bolt"));
        Assert.Equal(220m, Cost(ele, "lightning-bolt"));
        Assert.Equal(250m, Cost(enhance, "windfury-totem"));
        Assert.Equal(250m, Cost(ele, "windfury-totem"));
        // The shared source definition is still 370; neither spellbook mutates it globally.
        Assert.Equal(370m, ForeverShamanSharedAbilities.CreateLightningShield().ResourceCosts.Single().Amount);
        foreach (var spells in new[] { enhance, ele })
        {
            var shield = spells.Single(s => s.Key == "lightning-shield");
            Assert.Equal(600m, shield.Effects.Single().DurationSeconds);
            Assert.Equal(1.5m, shield.GlobalCooldownSeconds);
            Assert.Contains("shaman-lightning-shield", shield.ForbiddenSourceAuraKeys);
        }
        var r = Input();
        r.UseLightningShield = true;
        r.Talents.Ranks["shamanistic-focus"] = rank;
        var run = new EnhancementShamanSimRunner().Run(r);
        Assert.Equal(-(decimal)shieldCost, Assert.Single(run.Run.Timeline, e =>
            e.Type == CombatEventType.ResourceChanged && e.AbilityKey == "lightning-shield").Amount);
        Assert.Equal(run.MaximumMana - (decimal)shieldCost, run.EndingMana);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void OnlyRankFiveLearnsTheFreeInstantAlias(int rank)
    {
        var talents = new ForeverEnhancementShamanTalents { Ranks = new() { ["maelstrom-weapon"] = rank } };
        var spells = ForeverEnhancementShamanSpellbook.Create(new(), talents, 0m, 2.8m, 100m, 100m);
        Assert.Equal(rank == 5, spells.Any(s => s.Key == "maelstrom-lightning-bolt"));
        Assert.Single(spells, s => s.Key == "lightning-bolt");
        if (rank == 5)
        {
            var instant = spells.Single(s => s.Key == "maelstrom-lightning-bolt");
            Assert.Equal(0m, instant.CastTimeSeconds);
            Assert.Equal(0m, instant.ResourceCosts.Single().Amount);
            Assert.Contains("maelstrom-ready", instant.RequiredSourceAuraKeys);
        }
    }

    [Theory]
    [InlineData(1, 2, 256)] [InlineData(2, 1.5, 192)]
    [InlineData(3, 1, 128)] [InlineData(4, .5, 64)] [InlineData(5, 0, 0)]
    public void FiveStackRotationUsesRankSpecificRealCastTimeAndMana(int rank, double seconds, int mana)
    {
        var r = Input();
        r.DurationSeconds = 60m;
        r.UseMaelstromLightningBolt = true;
        r.Talents.Ranks["maelstrom-weapon"] = rank;
        r.Tuning.MaelstromChancePerMeleePercentPerRank = 100m / rank;
        var before = JsonSerializer.Serialize(r);
        var run = new EnhancementShamanSimRunner().Run(r);
        var key = rank == 5 ? "maelstrom-lightning-bolt" : "lightning-bolt";
        var starts = run.Run.Timeline.Where(e => e.Type == CombatEventType.AbilityCastStarted &&
            e.AbilityKey == key).ToArray();
        Assert.NotEmpty(starts);
        Assert.Equal(5, run.MaxMaelstromStacks);
        Assert.Equal(starts.Length, run.Run.Timeline.Count(e =>
            e.Type == CombatEventType.AuraApplied && e.AbilityKey == "maelstrom-weapon"));
        Assert.All(starts, start =>
        {
            // Rotation only acts after an actual five-stack aura, not at rank-dependent lower stacks.
            Assert.Contains(run.Run.Timeline, e => e.Type == CombatEventType.AuraApplied &&
                e.AbilityKey == "maelstrom-weapon" && e.TimeSeconds <= start.TimeSeconds);
            if (start.TimeSeconds + (decimal)seconds >= r.DurationSeconds)
            {
                // The encounter is [0, duration): an in-flight final cast cannot complete or spend.
                Assert.DoesNotContain(run.Run.Timeline, e => e.Type == CombatEventType.AbilityCastCompleted &&
                    e.AbilityExecutionId == start.AbilityExecutionId);
                Assert.DoesNotContain(run.Run.Timeline, e => e.Type == CombatEventType.Damage &&
                    e.AbilityExecutionId == start.AbilityExecutionId);
                return;
            }
            var completion = Assert.Single(run.Run.Timeline, e => e.Type == CombatEventType.AbilityCastCompleted &&
                e.AbilityExecutionId == start.AbilityExecutionId);
            Assert.Equal((decimal)seconds, completion.TimeSeconds - start.TimeSeconds);
            Assert.Equal(540m, Assert.Single(run.Run.Timeline, e => e.Type == CombatEventType.Damage &&
                e.AbilityExecutionId == start.AbilityExecutionId && e.AbilityKey == key).Amount);
        });
        var completed = run.Run.Timeline.Count(e => e.Type == CombatEventType.AbilityCastCompleted && e.AbilityKey == key);
        Assert.True(completed > 0);
        var spent = run.Run.Timeline.Where(e => e.Type == CombatEventType.ResourceChanged &&
            e.AbilityKey == key && e.Amount < 0m).ToArray();
        if (rank == 5)
        {
            Assert.Empty(spent);
            Assert.Equal(completed, run.MaelstromInstantBolts);
        }
        else
        {
            Assert.Equal(completed, spent.Length);
            if (spent.Any(e => e.Amount != -(decimal)mana))
                Assert.Fail("Five-stack Mana discount diverged. Timeline: " + JsonSerializer.Serialize(
                    run.Run.Timeline.Select(e => new { e.TimeSeconds, e.Type, e.AbilityKey,
                        e.EffectKey, e.AbilityExecutionId, e.Amount })));
            Assert.All(spent, e => Assert.Equal(-(decimal)mana, e.Amount));
            Assert.Equal(0, run.MaelstromInstantBolts);
            Assert.DoesNotContain(run.Run.Timeline, e => e.AbilityKey == "maelstrom-lightning-bolt");
        }
        Assert.Equal(run.MaximumMana - completed * mana, run.EndingMana);
        Assert.Equal(before, JsonSerializer.Serialize(r));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void FiveStackToggleOffDoesNotEnableUnrequestedFiller(int rank)
    {
        var r = Input();
        r.Talents.Ranks["maelstrom-weapon"] = rank;
        r.Tuning.MaelstromChancePerMeleePercentPerRank = 100m;
        var run = new EnhancementShamanSimRunner().Run(r);
        Assert.DoesNotContain(run.Run.Timeline, e => e.AbilityKey is "lightning-bolt" or "maelstrom-lightning-bolt");
        Assert.Equal(0, run.MaelstromInstantBolts);
    }

    [Fact]
    public void ZeroRankFiveStackToggleDoesNotGrantOrCastLightningBolt()
    {
        var r = Input();
        r.UseMaelstromLightningBolt = true;
        r.Tuning.MaelstromChancePerMeleePercentPerRank = 100m;
        var run = new EnhancementShamanSimRunner().Run(r);
        Assert.Equal(0, run.MaxMaelstromStacks);
        Assert.Equal(0, run.MaelstromInstantBolts);
        Assert.DoesNotContain(run.Run.Timeline, e => e.AbilityKey is "lightning-bolt" or "maelstrom-lightning-bolt");
    }

    [Fact]
    public void NormalBoltSnapshotsTimingAndCostAndConsumesOnlyReservedStacks()
    {
        var h = new Harness(2, 5m);
        h.Hits(0m, 3);
        h.At(1m, () => Assert.True(h.Executor.TryStartAbility(h.Context, "shaman", "target", "lightning-bolt").Success));
        h.Hits(1.5m, 2);
        h.At(2m, () => { Assert.Equal(5, h.Reactive.CurrentMaelstromStacks); Assert.Equal(243.2m, h.BoltCost); });
        h.At(3m, () => { Assert.Equal(2, h.Reactive.CurrentMaelstromStacks); Assert.Equal(268.8m, h.BoltCost); });
        var run = h.Run();
        var start = Assert.Single(run.Timeline, e => e.Type == CombatEventType.AbilityCastStarted);
        var end = Assert.Single(run.Timeline, e => e.Type == CombatEventType.AbilityCastCompleted);
        Assert.Equal(1.9m, end.TimeSeconds - start.TimeSeconds);
        Assert.Equal(756.8m, h.Actor.Resources["mana"].Current);
        Assert.Equal(-243.2m, Assert.Single(run.Timeline, e => e.Type == CombatEventType.ResourceChanged).Amount);
        Assert.Equal(2, h.Reactive.CurrentMaelstromStacks); // Nature damage itself did not generate another stack.
    }

    [Fact]
    public void MeleeHitQueuedBetweenSynchronousStartAndStartedEventCannotChangeSnapshot()
    {
        var h = new Harness(1, 5m);
        h.Hits(0m, 3);
        h.At(1m, () => Assert.True(h.Executor.TryStartAbility(h.Context, "shaman", "target", "lightning-bolt").Success));
        // This event is already queued when TryStartAbility emits its later Started event.
        h.Hits(1m, 1);
        var run = h.Run();
        var start = Assert.Single(run.Timeline, e => e.Type == CombatEventType.AbilityCastStarted);
        var end = Assert.Single(run.Timeline, e => e.Type == CombatEventType.AbilityCastCompleted);
        Assert.Equal(2.2m, end.TimeSeconds - start.TimeSeconds);
        Assert.Equal(-281.6m, Assert.Single(run.Timeline, e => e.Type == CombatEventType.ResourceChanged).Amount);
        Assert.Equal(1, h.Reactive.CurrentMaelstromStacks);
        Assert.Equal(307.2m, h.BoltCost);
    }

    [Theory]
    [InlineData(0, 0, 2.5, 320, 0)]
    [InlineData(1, 0, 2.5, 320, 1)]
    [InlineData(5, 5, 0, 0, 0)]
    public void SameTimestampLatchHandlesNoStacksZeroRankAndInstantNormalBolt(
        int rank, int stacks, double seconds, int mana, int remaining)
    {
        var h = new Harness(rank, 5m);
        h.Hits(0m, stacks);
        h.At(1m, () => Assert.True(h.Executor.TryStartAbility(h.Context, "shaman", "target", "lightning-bolt").Success));
        h.Hits(1m, 1);
        var run = h.Run();
        var start = Assert.Single(run.Timeline, e => e.Type == CombatEventType.AbilityCastStarted);
        var end = Assert.Single(run.Timeline, e => e.Type == CombatEventType.AbilityCastCompleted);
        Assert.Equal((decimal)seconds, end.TimeSeconds - start.TimeSeconds);
        Assert.Equal(1000m - mana, h.Actor.Resources["mana"].Current);
        Assert.Equal(remaining, h.Reactive.CurrentMaelstromStacks);
    }

    [Fact]
    public void CancelBeforeQueuedStartedDoesNotResurrectTheCancelledReservation()
    {
        var h = new Harness(1, 5m);
        h.Hits(0m, 3);
        h.At(1m, () => Assert.True(h.Executor.TryStartAbility(h.Context, "shaman", "target", "lightning-bolt").Success));
        h.At(1m, () => Assert.True(h.Executor.TryCancelCurrentCast(h.Context, "shaman")));
        h.Hits(1m, 1);
        var run = h.Run();
        Assert.DoesNotContain(run.Timeline, e => e.Type == CombatEventType.AbilityCastCompleted);
        Assert.Equal(1000m, h.Actor.Resources["mana"].Current);
        Assert.Equal(4, h.Reactive.CurrentMaelstromStacks);
        Assert.Equal(268.8m, h.BoltCost);
    }

    [Fact]
    public void StaleStartedForCompletedCastCannotOverwriteTheCurrentReservation()
    {
        var h = new Harness(1, 7m);
        h.Hits(0m, 1);
        h.At(1m, () => Assert.True(h.Executor.TryStartAbility(h.Context, "shaman", "target", "lightning-bolt").Success));
        h.Hits(3.5m, 3);
        h.At(4m, () => Assert.True(h.Executor.TryStartAbility(h.Context, "shaman", "target", "lightning-bolt").Success));
        h.At(4.1m, () => h.Reactive.Process(h.Context, new()
        {
            TimeSeconds = 4.1m, Type = CombatEventType.AbilityCastStarted,
            SourceActorKey = "shaman", AbilityKey = "lightning-bolt",
            AbilityExecutionId = h.Context.AbilityExecutions.Values.First(e => e.CastCompletionProcessed).Id
        }));
        h.Hits(4.2m, 1);
        var run = h.Run();
        Assert.Equal(new[] { -307.2m, -281.6m }, run.Timeline.Where(e => e.Type == CombatEventType.ResourceChanged).Select(e => e.Amount!.Value));
        Assert.Equal(1, h.Reactive.CurrentMaelstromStacks);
        Assert.Equal(307.2m, h.BoltCost);
    }

    [Fact]
    public void CastBoundaryWaitsForCompletionBeforeReusingItsStacksButCanStartAfterDispatch()
    {
        var h = new Harness(1, 7m);
        h.Hits(0m, 3);
        h.At(1m, () => Assert.True(h.Executor.TryStartAbility(h.Context, "shaman", "target", "lightning-bolt").Success));
        // Prequeued wake runs before the 3.2s completion scheduled during the 1s start.
        h.At(3.2m, () =>
        {
            var early = h.Executor.TryStartAbility(h.Context, "shaman", "target", "lightning-bolt");
            Assert.False(early.Success);
            Assert.Contains("already casting", early.FailureReason);
            // This wake is queued behind completion and may start at the SAME timestamp.
            h.At(3.2m, () => Assert.True(h.Executor.TryStartAbility(h.Context, "shaman", "target", "lightning-bolt").Success));
        });
        h.Hits(3.3m, 1);
        var run = h.Run();
        Assert.Equal(new[] { -281.6m, -320m }, run.Timeline.Where(e => e.Type == CombatEventType.ResourceChanged).Select(e => e.Amount!.Value));
        var starts = run.Timeline.Where(e => e.Type == CombatEventType.AbilityCastStarted).ToArray();
        Assert.Equal(new[] { 1m, 3.2m }, starts.Select(e => e.TimeSeconds));
        Assert.Equal(new[] { 3.2m, 5.7m }, run.Timeline.Where(e => e.Type == CombatEventType.AbilityCastCompleted).Select(e => e.TimeSeconds));
        Assert.Equal(1, h.Reactive.CurrentMaelstromStacks);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void DefaultOrUnselectedOrderingPreservesLegacyBoundaryStart(bool useDefault)
    {
        var h = new Harness(0, 7m, new HashSet<string> { "unrelated-ability" }, useDefault);
        h.At(1m, () => Assert.True(h.Executor.TryStartAbility(h.Context, "shaman", "target", "lightning-bolt").Success));
        h.At(3.5m, () => Assert.True(h.Executor.TryStartAbility(h.Context, "shaman", "target", "lightning-bolt").Success));
        var run = h.Run();
        Assert.Equal(new[] { 1m, 3.5m }, run.Timeline.Where(e => e.Type == CombatEventType.AbilityCastStarted).Select(e => e.TimeSeconds));
    }

    [Fact]
    public void CompletionOrderingCopiesTheCallerOwnedSet()
    {
        var keys = new HashSet<string> { "lightning-bolt" };
        var h = new Harness(0, 5m, keys);
        keys.Clear();
        h.At(1m, () => Assert.True(h.Executor.TryStartAbility(h.Context, "shaman", "target", "lightning-bolt").Success));
        h.At(3.5m, () => Assert.False(h.Executor.TryStartAbility(h.Context, "shaman", "target", "lightning-bolt").Success));
        h.Run();
    }

    [Fact]
    public void InstantOffGlobalCooldownActionsRemainAllowedBeforeQueuedCompletions()
    {
        var h = new Harness(0, 3m);
        foreach (var key in new[] { "instant-a", "instant-b" })
            h.Actor.AddAbility(new() { Key = key, Name = key, IsOffGlobalCooldown = true,
                CastTimeSeconds = 0m, GlobalCooldownSeconds = 0m });
        h.At(1m, () =>
        {
            Assert.True(h.Executor.TryStartAbility(h.Context, "shaman", "target", "instant-a").Success);
            Assert.True(h.Executor.TryStartAbility(h.Context, "shaman", "target", "instant-b").Success);
        });
        var run = h.Run();
        var completions = run.Timeline.Where(e => e.Type == CombatEventType.AbilityCastCompleted).ToArray();
        Assert.Equal(2, completions.Length);
        Assert.All(completions, e => Assert.Equal(1m, e.TimeSeconds));
    }

    [Theory]
    [InlineData("dead-target")]
    [InlineData("missing-target")]
    [InlineData("missing-ability")]
    public void InvalidBoltCompletionReleasesOwnershipWithoutConsumingStacksAndCanRetarget(string failure)
    {
        var h = new Harness(1, 7m);
        var replacement = new SimulationActorState { Key = "replacement", TeamKey = "enemy", Level = 60 };
        replacement.InitializeHealth(100000m);
        h.Context.AddActor(replacement);
        var bolt = h.Actor.Abilities["lightning-bolt"].Definition;
        Guid? firstId = null;
        h.Hits(0m, 3);
        h.At(1m, () =>
        {
            Assert.True(h.Executor.TryStartAbility(h.Context, "shaman", "target", "lightning-bolt").Success);
            firstId = h.Actor.CurrentCastExecutionId;
        });
        h.At(2m, () =>
        {
            if (failure == "dead-target") h.Context.GetActor("target")!.TakeDamage(100000m);
            else if (failure == "missing-target") h.Context.Actors.Remove("target");
            else h.Actor.Abilities.Remove("lightning-bolt");
        });
        h.At(3.25m, () =>
        {
            if (failure == "missing-ability") h.Actor.AddAbility(bolt);
        });
        h.Hits(3.3m, 1);
        h.At(3.5m, () =>
        {
            var retry = h.Executor.TryStartAbility(h.Context, "shaman", "replacement", "lightning-bolt");
            Assert.True(retry.Success, retry.FailureReason);
            Assert.True(h.Context.GetAbilityExecution(firstId!.Value)!.IsCancelled);
            Assert.Equal(4, h.Reactive.CurrentMaelstromStacks);
            Assert.Equal(1000m, h.Actor.Resources["mana"].Current);
        });
        h.Hits(6m, 1);
        var run = h.Run();
        Assert.Equal(-268.8m, Assert.Single(run.Timeline, e => e.Type == CombatEventType.ResourceChanged).Amount);
        Assert.Equal(540m, Assert.Single(run.Timeline, e => e.Type == CombatEventType.Damage && e.AbilityKey == "lightning-bolt").Amount);
        Assert.Equal(1, h.Reactive.CurrentMaelstromStacks);
        Assert.Equal(307.2m, h.BoltCost); // No stale execution re-latches after the unsuccessful cast.
    }

    [Fact]
    public void CancelledNormalBoltDoesNotSpendManaOrConsumeStacksAndCanRetry()
    {
        var h = new Harness(2, 5m);
        h.Hits(0m, 3);
        h.At(1m, () => Assert.True(h.Executor.TryStartAbility(h.Context, "shaman", "target", "lightning-bolt").Success));
        h.Hits(1.1m, 1);
        h.At(1.2m, () => Assert.True(h.Executor.TryCancelCurrentCast(h.Context, "shaman")));
        h.At(1.3m, () =>
        {
            Assert.Equal(4, h.Reactive.CurrentMaelstromStacks);
            Assert.Equal(217.6m, h.BoltCost);
            Assert.Equal(1000m, h.Actor.Resources["mana"].Current);
        });
        h.At(2.5m, () => Assert.True(h.Executor.TryStartAbility(h.Context, "shaman", "target", "lightning-bolt").Success));
        var run = h.Run();
        Assert.Single(run.Timeline, e => e.Type == CombatEventType.AbilityCastCancelled);
        Assert.Single(run.Timeline, e => e.Type == CombatEventType.AbilityCastCompleted);
        Assert.Equal(-217.6m, Assert.Single(run.Timeline, e => e.Type == CombatEventType.ResourceChanged).Amount);
        Assert.Equal(0, h.Reactive.CurrentMaelstromStacks);
        Assert.Equal(320m, h.BoltCost);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ExpiryDuringNormalBoltPreservesSnapshotAndDoesNotConsumeFreshStacks(bool newStack)
    {
        var h = new Harness(1, 34m);
        h.Hits(0m, 3);
        h.At(29.9m, () => Assert.True(h.Executor.TryStartAbility(h.Context, "shaman", "target", "lightning-bolt").Success));
        h.At(30m, () =>
        {
            Assert.Equal(0, h.Reactive.CurrentMaelstromStacks);
            Assert.Equal(281.6m, h.BoltCost); // In-flight 12% reduction remains frozen.
        });
        if (newStack) h.Hits(30.1m, 1);
        h.At(32.2m, () =>
        {
            Assert.Equal(newStack ? 1 : 0, h.Reactive.CurrentMaelstromStacks);
            Assert.Equal(newStack ? 307.2m : 320m, h.BoltCost);
        });
        var run = h.Run();
        var start = Assert.Single(run.Timeline, e => e.Type == CombatEventType.AbilityCastStarted);
        var end = Assert.Single(run.Timeline, e => e.Type == CombatEventType.AbilityCastCompleted);
        Assert.Equal(2.2m, end.TimeSeconds - start.TimeSeconds);
        Assert.Equal(-281.6m, Assert.Single(run.Timeline, e => e.Type == CombatEventType.ResourceChanged).Amount);
        Assert.Equal(newStack ? 1 : 0, h.Reactive.CurrentMaelstromStacks);
    }

    [Fact]
    public void CancellationAfterExpiryKeepsFreshStackForRetry()
    {
        var h = new Harness(1, 35m);
        h.Hits(0m, 3);
        h.At(29.9m, () => Assert.True(h.Executor.TryStartAbility(h.Context, "shaman", "target", "lightning-bolt").Success));
        h.Hits(30.1m, 1);
        h.At(30.2m, () => Assert.True(h.Executor.TryCancelCurrentCast(h.Context, "shaman")));
        h.At(30.3m, () =>
        {
            Assert.Equal(1, h.Reactive.CurrentMaelstromStacks);
            Assert.Equal(307.2m, h.BoltCost);
            Assert.Equal(1000m, h.Actor.Resources["mana"].Current);
        });
        h.At(31.5m, () => Assert.True(h.Executor.TryStartAbility(h.Context, "shaman", "target", "lightning-bolt").Success));
        var run = h.Run();
        Assert.Single(run.Timeline, e => e.Type == CombatEventType.AbilityCastCancelled);
        var end = Assert.Single(run.Timeline, e => e.Type == CombatEventType.AbilityCastCompleted);
        Assert.Equal(33.9m, end.TimeSeconds);
        Assert.Equal(-307.2m, Assert.Single(run.Timeline, e => e.Type == CombatEventType.ResourceChanged).Amount);
        Assert.Equal(0, h.Reactive.CurrentMaelstromStacks);
    }

    [Theory]
    [InlineData(1, 3)] [InlineData(1, 5)]
    [InlineData(4, 3)] [InlineData(4, 5)] [InlineData(5, 5)]
    public void PartialAndFullStacksExpireAtThirtySecondsAndCanRebuild(int rank, int count)
    {
        var h = new Harness(rank, 32m);
        h.Hits(0m, count);
        h.At(29.999m, () => Assert.Equal(count, h.Reactive.CurrentMaelstromStacks));
        h.At(30m, () =>
        {
            Assert.Equal(0, h.Reactive.CurrentMaelstromStacks);
            Assert.Equal(320m, h.BoltCost);
            Assert.DoesNotContain(h.Actor.ActiveAuras, a => a.Definition.Key == "maelstrom-ready" && a.IsActiveAt(30m));
        });
        h.Hits(31m, 1);
        h.Run();
        Assert.Equal(1, h.Reactive.CurrentMaelstromStacks);
        Assert.Equal(320m * (1m - .04m * rank), h.BoltCost);
    }

    private sealed class Harness
    {
        public SimulationContext Context { get; }
        public SimulationActorState Actor { get; }
        public AbilityExecutor Executor { get; }
        public ForeverEnhancementShamanReactiveProcessor Reactive { get; }
        private readonly AuraManager _auras = new();
        private readonly Dictionary<string, Action> _steps = new();
        public decimal BoltCost => Actor.Abilities["lightning-bolt"].Definition.ResourceCosts.Single().Amount;
        public Harness(int rank, decimal duration, IReadOnlySet<string>? completionOrderedKeys = null,
            bool useDefaultOrdering = false)
        {
            Context = new(new() { DurationSeconds = duration, CaptureTimeline = true, PrimaryActorKey = "shaman" });
            Actor = new() { Key = "shaman", TeamKey = "raid", Level = 60 };
            Actor.InitializeHealth(1000m);
            Actor.AddResource(new("mana", 1000m, 1000m));
            var target = new SimulationActorState { Key = "target", TeamKey = "enemy", Level = 60 };
            target.InitializeHealth(100000m);
            Context.AddActor(Actor);
            Context.AddActor(target);
            var talents = new ForeverEnhancementShamanTalents { Ranks = new() { ["maelstrom-weapon"] = rank } };
            var tuning = new ForeverEnhancementShamanTuning { MaelstromChancePerMeleePercentPerRank = 100m };
            foreach (var spell in ForeverEnhancementShamanSpellbook.Create(tuning, talents, 0m, 2.8m, 100m, 100m))
                Actor.AddAbility(spell);
            ForeverEnhancementShamanReactiveProcessor? state = null;
            Executor = new(_auras, new SimpleCombatRollResolver(), new NoMitigation(),
                abilityTimingProvider: new EnhancementMaelstromTimingProvider(() => state?.CurrentMaelstromStacks ?? 0, rank),
                completionOrderedAbilityKeys: useDefaultOrdering ? null :
                    completionOrderedKeys ?? new HashSet<string> { "lightning-bolt" });
            Reactive = new("shaman", new AutoAttackProcessor(Executor), _auras, talents, tuning);
            state = Reactive;
        }
        public void Hits(decimal time, int count)
        {
            for (var i = 0; i < count; i++)
                Context.ScheduleEvent(new() { TimeSeconds = time, Type = CombatEventType.Damage,
                    SourceActorKey = "shaman", TargetActorKey = "target", AbilityKey = "main-hand-auto",
                    SchoolKey = "physical", WeaponHandKey = "main-hand", Amount = 1m });
        }
        public void At(decimal time, Action action)
        {
            var key = $"step-{_steps.Count}";
            _steps.Add(key, action);
            Context.ScheduleEvent(new() { TimeSeconds = time, Type = CombatEventType.RotationDecision,
                EffectKey = key, IsInternal = true });
        }
        public SimulationRunResult Run() => new SimulationEngine([
            Executor, _auras, Reactive, new Steps(_steps)]).Run(Context);
    }
    private sealed class Steps(Dictionary<string, Action> actions) : ICombatEventProcessor
    {
        public void Process(SimulationContext context, CombatEvent e)
        {
            if (e.EffectKey is not null && actions.TryGetValue(e.EffectKey, out var action)) action();
        }
    }
    private sealed class NoMitigation : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext context, SimulationActorState source,
            SimulationActorState target, AbilityDefinition ability, AbilityEffectDefinition effect, decimal amount) =>
            DamageMitigationResult.Unmitigated(amount);
    }
}
