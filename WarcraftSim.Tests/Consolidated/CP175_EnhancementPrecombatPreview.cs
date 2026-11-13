using System.Text.Json;
using WarcraftSim.Api.Simulation;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175EnhancementPrecombatPreview
{
    private static EnhancementShamanSimRequest Input(string mode = "observed")
    {
        var r = new EnhancementShamanSimRequest { DurationSeconds = 30m, CaptureTimeline = true };
        if (mode == "legacy") return r;
        r.Character.Race = "orc";
        if (mode == "provisional") { r.UseProvisionalLevel60Baseline = true; return r; }
        r.Level60Baseline = new() { Race = "orc", ClassKey = "shaman", Strength = 100m,
            Agility = 100m, Stamina = 100m, Intellect = 100m, Spirit = 100m,
            ObservedUnbuffedMeleeAttackPower = 700m, ObservedUnbuffedMaximumHealth = 3000m,
            ObservedUnbuffedMaximumMana = 2000m, ClientBuild = "synthetic",
            SourceReference = "controlled-test-observation", RecordedAtUtc = "2026-11-09T00:00:00Z" };
        return r;
    }

    [Theory]
    [InlineData("legacy")][InlineData("observed")][InlineData("provisional")]
    public void ExactPrecombatValuesMatchExecutionAndRemainIsolated(string mode)
    {
        var r = Input(mode);
        r.Talents.Ranks = new(r.Talents.Ranks, StringComparer.Ordinal);
        r.Character.BaseStats.Values = new(r.Character.BaseStats.Values, StringComparer.Ordinal);
        r.Character.Equipment.Items.Add(new() { SlotKey = "chest", ItemKey = "authored",
            Stats = new() { Values = new(StringComparer.OrdinalIgnoreCase) { ["INTELLECT"] = 12m, ["spell-power"] = 6m } } });
        var before = JsonSerializer.Serialize(r);
        var p = EnhancementShamanSimRunner.Preview(r);
        var result = new EnhancementShamanSimRunner().Run(r);
        Assert.Equal(result.AttackPower, p.AttackPower);
        Assert.Equal(result.SpellPower, p.SpellPower);
        Assert.Equal(result.MaximumMana, p.MaximumMana);
        Assert.Equal(result.MaximumHealth, p.MaximumHealth);
        Assert.Equal(p.AttackPower, p.EffectiveStats.Get("attack-power"));
        Assert.Equal(p.SpellPower, p.EffectiveStats.Get("spell-power"));
        Assert.Equal(p.MaximumMana, p.EffectiveStats.Get("mana"));
        Assert.Equal(p.MaximumHealth, p.EffectiveStats.Get("health"));
        Assert.Contains("not-current-client-certified", p.StatPreviewScope);
        Assert.Contains(p.ModelingNotes, n => n.Contains("before hand-local Rockbiter"));
        if (mode == "legacy") { Assert.Null(p.Baseline); Assert.Equal("explicit-user-profile-not-client-verified", p.SourceStatus); }
        else Assert.NotNull(p.Baseline);
        if (mode == "provisional") Assert.Equal("provisional-documented-proxy", p.SourceStatus);
        var expected = JsonSerializer.Serialize(p);
        p.EffectiveStats.Values.Clear();
        Assert.Equal(expected, JsonSerializer.Serialize(EnhancementShamanSimRunner.Preview(r)));
        Assert.Equal(before, JsonSerializer.Serialize(r));
        Assert.Same(StringComparer.Ordinal, r.Talents.Ranks.Comparer);
        Assert.Same(StringComparer.Ordinal, r.Character.BaseStats.Values.Comparer);
        Assert.Same(StringComparer.OrdinalIgnoreCase, r.Character.Equipment.Items[0].Stats.Values.Comparer);
    }

    [Theory][InlineData(true)][InlineData(false)]
    public void IndependentObservedOracleAndManualHealthPrecedence(bool observedHealth)
    {
        var r = Input();
        if (!observedHealth) r.Level60Baseline!.ObservedUnbuffedMaximumHealth = null;
        r.Character.Equipment.Items.Add(new() { SlotKey = "chest", ItemKey = "authored",
            Stats = new() { Values = new() { ["strength"] = 10m, ["intellect"] = 2m,
                ["stamina"] = 3m, ["mana"] = 4m, ["health"] = 5m, ["spell-power"] = 6m } } });
        r.Character.Talents.Selections.Add(new() { TalentKey = "authored",
            Stats = new() { Values = new() { ["intellect"] = 4m, ["mana"] = 5m } } });
        r.Character.Buffs.Selections.Add(new() { BuffKey = "authored",
            Stats = new() { Values = new() { ["intellect"] = 6m, ["mana"] = 7m } } });
        var p = EnhancementShamanSimRunner.Preview(r);
        Assert.Equal(832m, p.AttackPower);
        Assert.Equal(39.6m, p.SpellPower);
        Assert.Equal(2196m, p.MaximumMana);
        Assert.Equal(observedHealth ? 3035m : 3535m, p.MaximumHealth);
        r.MaximumHealth = 4800m;
        Assert.Equal(observedHealth ? 3035m : 4835m, EnhancementShamanSimRunner.Preview(r).MaximumHealth);
        r.Talents.Ranks["mental-dexterity"] = 0;
        r.Talents.Ranks["mental-quickness"] = 0;
        r.Talents.Ranks["rage-of-the-farseer"] = 0;
        var untalented = EnhancementShamanSimRunner.Preview(r);
        Assert.Equal(720m, untalented.AttackPower);
        Assert.Equal(6m, untalented.SpellPower);
    }

    [Theory][InlineData(10, 5, 2000)][InlineData(10, 15, 2075)]
    [InlineData(30, -15, 1850)][InlineData(30, -40, 1850)]
    public void ObservedManaCrossesClampedIntellectThresholdExactly(int intellect, int delta, int expected)
    {
        var r = Input(); r.Level60Baseline!.Intellect = intellect;
        r.Character.Buffs.Selections.Add(new() { BuffKey = "authored",
            Stats = new() { Values = new() { ["intellect"] = delta } } });
        var p = EnhancementShamanSimRunner.Preview(r);
        Assert.Equal(expected, p.MaximumMana);
        Assert.Equal(new EnhancementShamanSimRunner().Run(r).MaximumMana, p.MaximumMana);
    }

    [Theory][InlineData("none")][InlineData("rockbiter")][InlineData("frostbrand")]
    public void PreviewExcludesHandLocalAndScheduledBonuses(string imbue)
    {
        var r = Input(); r.DualWield = true; r.MainHandImbue = imbue;
        r.OffHandImbue = "rockbiter"; r.UseCallOfElements = true;
        var p = EnhancementShamanSimRunner.Preview(r);
        Assert.Equal(800m, p.AttackPower);
        Assert.False(p.EffectiveStats.Contains("shaman-main-hand-attack-power"));
        Assert.False(p.EffectiveStats.Contains("shaman-off-hand-attack-power"));
        Assert.Equal(p.AttackPower, new EnhancementShamanSimRunner().Run(r).AttackPower);
    }

    [Fact]
    public void InactiveWindfuryAndDisabledUppercaseCallRetainExistingAcceptance()
    {
        var r = Input(); r.MainHandImbue = "none";
        r.Tuning.WindfuryWeaponBonusAttackPower = -1m;
        r.Tuning.WindfuryWeaponInternalCooldownSeconds = -1m;
        r.UseCallOfElements = false; r.CallOfElementsTotems = ["STRENGTH-OF-EARTH-TOTEM"];
        var p = EnhancementShamanSimRunner.Preview(r);
        Assert.Equal(new EnhancementShamanSimRunner().Run(r).AttackPower, p.AttackPower);
    }

    [Theory]
    [InlineData("elements")][InlineData("ancestors")][InlineData("spirits")]
    public void InvalidNonNullTotemPresetsRejectIdentically(string call)
    {
        List<string>[] invalidPresets = [[], ["stoneskin-totem", "strength-of-earth-totem"],
            ["unknown-totem"], ["mana-tide-totem"],
            ["stoneskin-totem", "windfury-totem", "mana-spring-totem", "searing-totem", "healing-stream-totem"]];
        foreach (var preset in invalidPresets)
        {
            var r = Input();
            if (call == "elements") r.CallOfElementsTotems = preset;
            else if (call == "ancestors") r.CallOfAncestorsTotems = preset;
            else r.CallOfSpiritsTotems = preset;
            var previewError = Assert.Throws<ArgumentException>(() => EnhancementShamanSimRunner.Preview(r));
            var runError = Assert.Throws<ArgumentException>(() => new EnhancementShamanSimRunner().Run(r));
            Assert.Equal(runError.Message, previewError.Message);
        }
    }

    [Theory]
    [InlineData("item")][InlineData("enchant")][InlineData("temporary-enchant")]
    [InlineData("race")][InlineData("modes")][InlineData("health")][InlineData("mana")]
    [InlineData("duration")][InlineData("imbue")][InlineData("ranks")][InlineData("stat-map")]
    [InlineData("calls")][InlineData("combat-items")][InlineData("talent-rank")]
    [InlineData("shock-damage")][InlineData("bolt-damage")]
    [InlineData("windfury-ap")][InlineData("windfury-cooldown")][InlineData("offhand-speed")]
    [InlineData("mp5")][InlineData("uppercase-call")]
    public void RunAndPreviewRejectTheSameInvalidInputs(string kind)
    {
        var r = Input();
        switch (kind)
        {
            case "windfury-ap": r.Tuning.WindfuryWeaponBonusAttackPower = -1m; break;
            case "windfury-cooldown": r.Tuning.WindfuryWeaponInternalCooldownSeconds = -1m; break;
            case "offhand-speed": r.DualWield = false; r.OffHandWeaponSpeedSeconds = 0m; break;
            case "mp5": r.Character.BaseStats.Set("mp5", -1000m); r.Level60Baseline = null; break;
            case "uppercase-call": r.UseCallOfElements = true; r.CallOfElementsTotems = ["STRENGTH-OF-EARTH-TOTEM"]; break;
            case "shock-damage": r.Tuning.EarthShockDamage = -1m; break;
            case "bolt-damage": r.Tuning.LightningBoltDamage = -1m; break;
            case "item": r.Character.Equipment.Items.Add(new() { ItemId = 19102 }); break;
            case "enchant": r.Character.Equipment.Items.Add(new() { EnchantSpellId = 20034 }); break;
            case "temporary-enchant": r.Character.Equipment.Items.Add(new() { TemporaryEnchantSpellId = 25122 }); break;
            case "race": r.Character.Race = "tauren"; break;
            case "modes": r.Level60Baseline = null; r.UseReviewedLevel60Baseline = true; r.UseProvisionalLevel60Baseline = true; break;
            case "health": r.MaximumHealth = 0m; break;
            case "mana": r.Level60Baseline!.ObservedUnbuffedMaximumMana = 0m; break;
            case "duration": r.DurationSeconds = 0m; break;
            case "imbue": r.MainHandImbue = "invalid"; break;
            case "ranks": r.Talents.Ranks = null!; break;
            case "stat-map": r.Character.BaseStats.Values = null!; break;
            case "calls": r.CallOfElementsTotems = null!; break;
            case "combat-items": r.CombatItems.DisabledItemKeys = null!; break;
            case "talent-rank": r.Talents.Ranks["mental-dexterity"] = 4; break;
        }
        var before = JsonSerializer.Serialize(r);
        var previewError = Assert.ThrowsAny<ArgumentException>(() => EnhancementShamanSimRunner.Preview(r));
        var runError = Assert.ThrowsAny<ArgumentException>(() => new EnhancementShamanSimRunner().Run(r));
        Assert.Equal(runError.GetType(), previewError.GetType());
        Assert.Equal(runError.Message, previewError.Message);
        Assert.Equal(before, JsonSerializer.Serialize(r));
    }
}
