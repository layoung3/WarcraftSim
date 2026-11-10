using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;
using WarcraftSim.Data.Forever.Shaman;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175EnhancementEarlyTalentMechanics
{
    private static EnhancementShamanSimRequest Input(string mode)
    {
        var r = new EnhancementShamanSimRequest
        {
            DurationSeconds = 14m, CaptureTimeline = true,
            UseWindfuryWeapon = false, UseWindfuryTotem = false,
            UseStormstrike = false, UseEarthShock = false,
            UseMaelstromLightningBolt = false, UseLightningBoltFiller = false,
            UseRageOfTheFarseer = false
        };
        r.Character.Race = "orc";
        r.Character.BaseStats.Values = new(StringComparer.Ordinal)
        {
            ["strength"] = 100m, ["agility"] = 100m, ["stamina"] = 100m,
            ["intellect"] = 100m, ["spirit"] = 100m, ["attack-power"] = 700m
        };
        if (mode != "legacy")
            r.Level60Baseline = new()
            {
                Race = "orc", ClassKey = "shaman", Strength = 100m,
                Agility = 100m, Stamina = 100m, Intellect = 100m, Spirit = 100m,
                ObservedUnbuffedMeleeAttackPower = 700m,
                ObservedUnbuffedMaximumHealth = mode == "observed" ? 3000m : null,
                ObservedUnbuffedMaximumMana = mode == "observed" ? 2000m : null,
                ClientBuild = "synthetic", SourceReference = "controlled-early-talent-test",
                RecordedAtUtc = "2026-11-10T00:00:00Z"
            };
        r.Character.Equipment.Items.Add(new()
        {
            SlotKey = "chest", ItemKey = "authored",
            Stats = new() { Values = new(StringComparer.OrdinalIgnoreCase)
            {
                ["INTELLECT"] = 2m, ["strength"] = 10m, ["stamina"] = 3m,
                ["mana"] = 4m, ["health"] = 5m, ["spell-power"] = 6m, ["mp5"] = 7m
            } },
            Modifiers = [new() { PlacementKey = "authored", ModifierKey = "budget",
                ModifierType = "custom", Stats = new() { Values = new()
                { ["intellect"] = 3m, ["mana"] = 2m, ["health"] = 1m } } }]
        });
        r.Character.Talents.Selections.Add(new() { TalentKey = "authored",
            Stats = new() { Values = new() { ["intellect"] = 4m, ["mana"] = 5m } } });
        r.Character.Buffs.Selections.Add(new() { BuffKey = "authored",
            Stats = new() { Values = new() { ["intellect"] = 6m, ["mana"] = 7m } } });
        return r;
    }

    public static IEnumerable<object[]> IntellectCases()
    {
        decimal[] expected = [115m, 117.3m, 119.6m, 121.9m, 124.2m, 126.5m];
        foreach (var mode in new[] { "legacy", "observed", "fallback" })
            for (var rank = 0; rank <= 5; rank++) yield return [mode, rank, expected[rank]];
    }

    [Theory, MemberData(nameof(IntellectCases))]
    public void AncestralKnowledgeAppliesEveryRankOnceAcrossAllContributions(
        string mode, int rank, decimal expectedIntellect)
    {
        var r = Input(mode);
        r.Talents.Ranks["ancestral-knowledge"] = rank;
        var before = JsonSerializer.Serialize(r);
        var p = EnhancementShamanSimRunner.Preview(r);
        var run = new EnhancementShamanSimRunner().Run(r);
        Assert.Equal(expectedIntellect, p.EffectiveStats.Get("intellect"));
        Assert.Equal((mode == "legacy" ? 700m : 720m) + expectedIntellect, p.AttackPower);
        Assert.Equal(6m + expectedIntellect * .30m, p.SpellPower);
        var expectedMana = mode switch
        {
            "legacy" => 1376m + (expectedIntellect - 20m) * 15m,
            "observed" => 2000m + (expectedIntellect - 100m) * 15m + 18m,
            _ => ForeverClientBaseManaCatalog.GetBaseMana("shaman", 60) +
                (expectedIntellect - 20m) * 15m + 18m
        };
        Assert.Equal(expectedMana, p.MaximumMana);
        Assert.Equal(mode == "legacy" ? 3500m : mode == "observed" ? 3036m : 3536m,
            p.MaximumHealth);
        Assert.Equal(110m, p.EffectiveStats.Get("strength"));
        Assert.Equal(100m, p.EffectiveStats.Get("agility"));
        Assert.Equal(103m, p.EffectiveStats.Get("stamina"));
        Assert.Equal(100m, p.EffectiveStats.Get("spirit"));
        Assert.Equal(7m, p.EffectiveStats.Get("mp5"));
        Assert.Equal(mode == "legacy" ? 0m : expectedIntellect /
            ForeverPrimarySpellStatProfiles.GetIntellectPerSpellCriticalPercent("shaman", 60),
            p.EffectiveStats.Get(ForeverCombatStatKeys.SpellCriticalChancePercent));
        Assert.Equal(p.AttackPower, run.AttackPower);
        Assert.Equal(p.SpellPower, run.SpellPower);
        Assert.Equal(p.MaximumMana, run.MaximumMana);
        Assert.Equal(p.MaximumHealth, run.MaximumHealth);
        Assert.Equal(run.MaximumMana, run.Run.Summary.ActorSummaries["shaman"].StartingResources["mana"]);
        if (rank > 0)
        {
            Assert.Contains(p.ModelingNotes, n => n.Contains("Historical-provisional Enhancement early talents"));
            Assert.Contains(run.ModelingNotes, n => n.Contains("NOT current-client certified"));
        }
        Assert.Equal(before, JsonSerializer.Serialize(r));
    }

    [Theory]
    [InlineData(10, 0, 11, 2000)]
    [InlineData(19, 0, 20.9, 2013.5)]
    [InlineData(20, 0, 22, 2030)]
    [InlineData(30, -11, 20.9, 1863.5)]
    [InlineData(30, -50, 0, 1850)]
    public void AncestralKnowledgePreservesClampedObservedManaDelta(
        int nakedIntellect, int authoredDelta, double expectedIntellect, double expectedMana)
    {
        var r = Input("observed");
        r.Level60Baseline!.Intellect = nakedIntellect;
        r.Character.Equipment.Items.Clear();
        r.Character.Talents.Selections.Clear();
        r.Character.Buffs.Selections.Clear();
        r.Character.Buffs.Selections.Add(new() { BuffKey = "authored-delta",
            Stats = new() { Values = new() { ["intellect"] = authoredDelta } } });
        r.Talents.Ranks["ancestral-knowledge"] = 5;
        var p = EnhancementShamanSimRunner.Preview(r);
        Assert.Equal((decimal)expectedIntellect, p.EffectiveStats.Get("intellect"));
        Assert.Equal((decimal)expectedMana, p.MaximumMana);
        Assert.Equal(p.MaximumMana, new EnhancementShamanSimRunner().Run(r).MaximumMana);
    }

    [Theory, MemberData(nameof(IntellectCases))]
    public void IncreasedIntellectFeedsActualLightningBoltDamageAndMana(
        string mode, int rank, decimal expectedIntellect)
    {
        var r = Input(mode);
        r.DurationSeconds = 3m;
        r.Talents.Ranks["ancestral-knowledge"] = rank;
        r.Talents.Ranks["maelstrom-weapon"] = 0;
        r.Talents.Ranks["thundering-strikes"] = 0;
        r.Talents.Ranks["flurry"] = 0;
        r.UseLightningBoltFiller = true;
        r.BaseSpellHitPercent = 100m;
        r.CriticalChancePercent = 0m;
        r.Character.Equipment.Items[0].Stats.Set("mp5", 0m);
        r.Tuning.ManaRegenPerSecond = 0m;
        var run = new EnhancementShamanSimRunner().Run(r);
        var bolt = Assert.Single(run.Run.Timeline, e => e.Type == CombatEventType.Damage &&
            e.AbilityKey == "lightning-bolt");
        Assert.Equal(540m + (6m + expectedIntellect * .30m) * .714m, bolt.Amount);
        Assert.False(bolt.IsCritical);
        Assert.Equal(run.MaximumMana - 320m, run.EndingMana);
    }

    public static IEnumerable<object[]> ShieldCases()
    {
        decimal[] expected = [178m, 186.9m, 195.8m, 204.7m];
        foreach (var mode in new[] { "legacy", "observed", "fallback" })
            for (var rank = 0; rank <= 3; rank++) yield return [mode, rank, expected[rank]];
    }

    [Theory, MemberData(nameof(ShieldCases))]
    public void ImprovedLightningShieldScalesOnlyOrbDamageAtEveryRank(
        string mode, int rank, decimal expectedOrb)
    {
        var r = Input(mode);
        r.UseLightningShield = true;
        r.IncomingBossHitDamage = 100m;
        r.IncomingBossSwingSeconds = 4m;
        r.LightningShieldInternalCooldownSeconds = 3m;
        var baseline = new EnhancementShamanSimRunner().Run(r);
        r.Talents.Ranks["improved-lightning-shield"] = rank;
        var before = JsonSerializer.Serialize(r);
        var p = EnhancementShamanSimRunner.Preview(r);
        var run = new EnhancementShamanSimRunner().Run(r);
        var orbs = run.Run.Timeline.Where(e => e.Type == CombatEventType.Damage &&
            e.AbilityKey == "lightning-shield" && e.SourceActorKey == "shaman").ToArray();
        Assert.Equal(new[] { 2m, 6m, 10m }, orbs.Select(e => e.TimeSeconds));
        Assert.All(orbs, e => { Assert.Equal(expectedOrb, e.Amount); Assert.False(e.IsCritical); });
        Assert.Equal(3m * expectedOrb, run.DamageByAbility["lightning-shield"]);
        Assert.Equal(3m, run.Run.CustomMetrics["shaman-lightning-shield-triggers"]);
        Assert.Contains(run.Run.Timeline, e => e.Type == CombatEventType.AuraRemoved &&
            e.AbilityKey == "lightning-shield");
        // Default Shamanistic Focus rank 1 now applies its independently tested
        // 45% reduction: 370 * .55 = 203.5. ILS itself still changes no Mana cost.
        Assert.All(run.Run.Timeline.Where(e => e.Type == CombatEventType.ResourceChanged &&
            e.AbilityKey == "lightning-shield"), e => Assert.Equal(-203.5m, e.Amount));
        Assert.Equal(baseline.MaximumMana, run.MaximumMana);
        Assert.Equal(baseline.EndingMana, run.EndingMana);
        Assert.Equal(baseline.EndingHealth, run.EndingHealth);
        Assert.Equal(baseline.AttackPower, run.AttackPower);
        Assert.Equal(baseline.SpellPower, run.SpellPower);
        Assert.Equal(p.AttackPower, run.AttackPower);
        Assert.Equal(p.SpellPower, run.SpellPower);
        Assert.Equal(p.MaximumMana, run.MaximumMana);
        Assert.Equal(p.MaximumHealth, run.MaximumHealth);
        foreach (var (ability, damage) in baseline.DamageByAbility.Where(x => x.Key != "lightning-shield"))
            Assert.Equal(damage, run.DamageByAbility[ability]);
        Assert.Equal(before, JsonSerializer.Serialize(r));
    }

    [Theory]
    [InlineData(false, 100)]
    [InlineData(true, 0)]
    public void ImprovedLightningShieldDoesNotCreateUntriggeredDamage(bool enabled, int incoming)
    {
        var r = Input("legacy");
        r.UseLightningShield = enabled;
        r.IncomingBossHitDamage = incoming;
        r.Talents.Ranks["improved-lightning-shield"] = 3;
        var run = new EnhancementShamanSimRunner().Run(r);
        Assert.Equal(0m, run.Run.CustomMetrics["shaman-lightning-shield-triggers"]);
        Assert.DoesNotContain(run.Run.Timeline, e => e.Type == CombatEventType.Damage &&
            e.AbilityKey == "lightning-shield");
    }

    [Fact]
    public void ImprovedLightningShieldStillHonorsInternalCooldown()
    {
        var r = Input("legacy");
        r.UseLightningShield = true;
        r.IncomingBossHitDamage = 10m;
        r.IncomingBossSwingSeconds = 1m;
        r.LightningShieldInternalCooldownSeconds = 3m;
        r.DurationSeconds = 9m;
        r.Talents.Ranks["improved-lightning-shield"] = 3;
        var run = new EnhancementShamanSimRunner().Run(r);
        var orbs = run.Run.Timeline.Where(e => e.Type == CombatEventType.Damage &&
            e.AbilityKey == "lightning-shield").ToArray();
        Assert.Equal(new[] { 2m, 5m, 8m }, orbs.Select(e => e.TimeSeconds));
        Assert.All(orbs, e => Assert.Equal(204.7m, e.Amount));
    }

    [Fact]
    public void MetadataAddsOnlyEightSourceBackedRanksWithoutRewritingDefaults()
    {
        var t = new ForeverEnhancementShamanTalents();
        Assert.Equal(10, t.ElementalPointsSpent);
        Assert.Equal(41, t.EnhancementPointsSpent);
        Assert.Equal(28, t.Ranks.Values.Sum());
        Assert.False(t.Ranks.ContainsKey("ancestral-knowledge"));
        Assert.False(t.Ranks.ContainsKey("improved-lightning-shield"));
        var metadata = ForeverEnhancementShamanTalents.SupportedTalentMetadata;
        Assert.Equal(12, metadata.Count);
        Assert.Equal(36, metadata.Sum(x => x.MaxRank));
        Assert.All(metadata, x => Assert.Equal("enhancement", x.Tree));
        Assert.Equal(5, metadata.Single(x => x.Key == "ancestral-knowledge").MaxRank);
        Assert.Equal(3, metadata.Single(x => x.Key == "improved-lightning-shield").MaxRank);
        t.Ranks = metadata.ToDictionary(x => x.Key, x => x.MaxRank);
        t.ElementalPointsSpent = 0;
        t.EnhancementPointsSpent = 36;
        t.Validate(60);
        t.EnhancementPointsSpent = 35;
        Assert.Throws<ArgumentException>(() => t.Validate(60));
    }

    [Theory]
    [InlineData("ancestral-knowledge", -1)]
    [InlineData("ancestral-knowledge", 6)]
    [InlineData("improved-lightning-shield", -1)]
    [InlineData("improved-lightning-shield", 4)]
    [InlineData("invented-early-filler", 1)]
    public void InvalidRanksFailBeforePreviewOrCombat(string key, int rank)
    {
        var r = Input("observed");
        r.Talents.Ranks[key] = rank;
        var before = JsonSerializer.Serialize(r);
        var p = Assert.Throws<ArgumentException>(() => EnhancementShamanSimRunner.Preview(r));
        var run = Assert.Throws<ArgumentException>(() => new EnhancementShamanSimRunner().Run(r));
        Assert.Equal(p.Message, run.Message);
        Assert.Equal(before, JsonSerializer.Serialize(r));
    }

    [Fact]
    public void SharedShieldProcessorRejectsNegativeMultiplier()
    {
        var auras = new AuraManager();
        var executor = new AbilityExecutor(auras, new SimpleCombatRollResolver(),
            new RulesetDamageMitigationResolver(new CombatRulesetDefinition
                { RulesetKey = "early-talent-test", Version = "1" }));
        var processor = new ShamanLightningShieldProcessor(new SimulationActorState(), auras,
            executor, damageMultiplier: -1m);
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => processor.Process(
            new SimulationContext(new SimulationRunOptions()), new CombatEvent()));
        Assert.Equal("damageMultiplier", error.ParamName);
    }

    [Theory]
    [InlineData("legacy")]
    [InlineData("observed")]
    [InlineData("fallback")]
    public async Task RankedInputsRemainIsolatedAndReplayWithoutCompounding(string mode)
    {
        var r = Input(mode);
        r.Talents.Ranks = new(r.Talents.Ranks, StringComparer.Ordinal);
        r.Talents.Ranks["ancestral-knowledge"] = 5;
        r.Talents.Ranks["improved-lightning-shield"] = 3;
        r.UseLightningShield = true;
        r.IncomingBossHitDamage = 10m;
        var before = JsonSerializer.Serialize(r);
        var snapshot = EnhancementShamanSimulationRequestSnapshot.Create(r);
        Assert.Same(StringComparer.Ordinal, snapshot.Talents.Ranks.Comparer);
        Assert.Same(StringComparer.Ordinal, snapshot.Character.BaseStats.Values.Comparer);
        Assert.Same(StringComparer.OrdinalIgnoreCase, snapshot.Character.Equipment.Items[0].Stats.Values.Comparer);
        snapshot.Talents.Ranks.Clear();
        snapshot.Character.Equipment.Items[0].Modifiers[0].Stats.Set("intellect", 999m);
        var preview = EnhancementShamanSimRunner.Preview(r);
        preview.EffectiveStats.Values.Clear();
        var runs = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Task.Run(() =>
            new EnhancementShamanSimRunner().Run(r))));
        Assert.All(runs, run => Assert.Equal(CombatReceipt(runs[0]), CombatReceipt(run)));
        Assert.Equal(126.5m, EnhancementShamanSimRunner.Preview(r).EffectiveStats.Get("intellect"));
        Assert.Equal(before, JsonSerializer.Serialize(r));
    }

    [Theory]
    [InlineData("legacy")]
    [InlineData("observed")]
    [InlineData("fallback")]
    public void ExplicitZeroRanksHaveExactlyTheSameCombatReceiptAsOmittedRanks(string mode)
    {
        var r = Input(mode);
        r.UseLightningShield = true;
        r.IncomingBossHitDamage = 10m;
        var omitted = CombatReceipt(new EnhancementShamanSimRunner().Run(r));
        r.Talents.Ranks["ancestral-knowledge"] = 0;
        r.Talents.Ranks["improved-lightning-shield"] = 0;
        Assert.Equal(omitted, CombatReceipt(new EnhancementShamanSimRunner().Run(r)));
    }

    private static string CombatReceipt(EnhancementShamanSimResponse result) =>
        JsonSerializer.Serialize(new { result.DamageDone, result.MaximumMana, result.EndingMana,
            result.MaximumHealth, result.EndingHealth, result.AttackPower, result.SpellPower,
            result.DamageByAbility, result.Run.CustomMetrics,
            Timeline = result.Run.Timeline.Select(e => new { e.TimeSeconds, e.Type, e.AbilityKey,
                e.Amount, e.ResultKey, e.ResourceTotalAfter }) });
}
