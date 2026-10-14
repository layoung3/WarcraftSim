using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Priest;
using Xunit;

namespace WarcraftSim.Tests;

/// <summary>
/// CP126 extended Priest integration. Deterministic controlled encounter,
/// NOT an assertion about live Forever max-rank spell coefficients.
/// </summary>
public sealed class HealingPriestMechanicsCp126Tests
{
    private static HolyPriestPreviewRequest Scenario() => new()
    {
        EnableExtendedPriest = true,
        DurationSeconds = 24m, TankMaximumHealth = 4000m,
        BossSwingDamage = 300m, BossSwingIntervalSeconds = 2m,
        PriestMaximumMana = 20000m, PriestMp5 = 0m,
        PriestSpiritRegenPerSecond = 0m,
        AdditionalRaidMembers = 0,
        GreaterHealHealthThresholdPercent = 0m,
        FlashHealHealthThresholdPercent = 0m,
        UsePrayerOfMending = false,
        UsePowerWordShield = false,
        UseRenew = false,
        UseBindingHeal = false,
        UsePrayerOfHealing = false,
        UsePenance = false,
        UseDivineAegis = false,
        LitanyRefundFraction = 0m,
        CaptureTimeline = true
    };

    [Fact]
    public void SpellbookUsesExistingCoreHealingAndAbsorbEffectTypes()
    {
        var spells = ForeverPriestHealingSpellbook.Create(new ForeverPriestHealingTuning());
        Assert.Equal(9, spells.Count);
        Assert.Contains(spells, s => s.Key == "renew" &&
            s.Effects.Any(e => e.EffectType == AbilityEffectTypes.PeriodicHealing && e.CanCrit));
        Assert.Contains(spells, s => s.Key == "power-word-shield" &&
            s.ForbiddenTargetAuraKeys.Contains("weakened-soul") &&
            s.Effects.Any(e => e.EffectType == AbilityEffectTypes.Absorb));
        Assert.Contains(spells, s => s.Key == "binding-heal" && s.Effects.Count == 2);
        Assert.Contains(spells, s => s.Key == "prayer-of-healing" && s.Effects[0].MaxTargets == 5);
        Assert.Contains(spells, s => s.Key == "penance" && s.IsChanneled);
    }

    [Fact]
    public void TalentRanksModifySpellValuesWithoutChangingCoreEngine()
    {
        var baseSpells = ForeverPriestHealingSpellbook.Create(new ForeverPriestHealingTuning());
        var talentSpells = ForeverPriestHealingSpellbook.Create(new ForeverPriestHealingTuning
        {
            ImprovedRenewRanks = 3, ImprovedShieldRanks = 3,
            SpiritualHealingRanks = 3, DivineFuryRanks = 5,
            ImprovedHealingRanks = 3, SoulWarding = true
        });
        AbilityDefinition get(IReadOnlyList<AbilityDefinition> list, string key) =>
            Assert.Single(list, a => a.Key == key);
        Assert.True(get(talentSpells, "renew").Effects[0].MinimumValue >
            get(baseSpells, "renew").Effects[0].MinimumValue);
        Assert.True(get(talentSpells, "power-word-shield").Effects[0].MinimumValue >
            get(baseSpells, "power-word-shield").Effects[0].MinimumValue);
        Assert.Equal(0m, get(talentSpells, "power-word-shield").CooldownSeconds);
        Assert.True(get(talentSpells, "greater-heal").CastTimeSeconds <
            get(baseSpells, "greater-heal").CastTimeSeconds);
    }

    [Fact]
    public void ExtendedModeIsOptInAndLegacyPreviewStillUsesOnlyTwoSpells()
    {
        var request = new HolyPriestPreviewRequest
        {
            DurationSeconds = 12m, TankMaximumHealth = 600m,
            BossSwingDamage = 300m, BossSwingIntervalSeconds = 2m,
            FlashHealAmount = 400m, FlashHealManaCost = 100m,
            GreaterHealHealthThresholdPercent = 0m, FlashHealHealthThresholdPercent = 90m
        };
        var response = new HolyPriestPreviewRunner().Run(request);
        Assert.True(response.HealingDone > 0m);
        Assert.DoesNotContain(response.PriestSpellCasts.Keys, k => k is "renew" or "penance");
    }

    [Fact]
    public void RenewTicksAndCanCriticalHealWhenEnabled()
    {
        var request = Scenario();
        request.UseRenew = true;
        request.EnableHealingCriticals = true;
        request.PriestHealingCritPercent = 100m;
        request.BossSwingDamage = 240m;
        var result = new HolyPriestPreviewRunner().Run(request);
        Assert.Contains(result.Run.Timeline, ev => ev.AbilityKey == "renew" &&
            ev.Type == CombatEventType.Healing && ev.IsPeriodic && ev.IsCritical);
    }

    [Fact]
    public void PowerWordShieldAbsorbsDamageAndAppliesWeakenedSoul()
    {
        var request = Scenario();
        request.UsePowerWordShield = true;
        request.TankMaximumHealth = 3000m;
        request.BossSwingDamage = 500m;
        var result = new HolyPriestPreviewRunner().Run(request);
        Assert.Contains(result.Run.Timeline, ev => ev.Type == CombatEventType.AbsorbApplied &&
            ev.AbilityKey == "power-word-shield");
        Assert.Contains(result.Run.Timeline, ev => ev.Type == CombatEventType.AbsorbConsumed);
        Assert.Contains(result.Run.Timeline, ev => ev.Type == CombatEventType.AuraApplied &&
            ev.Description?.Contains("weakened-soul", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void PrayerOfMendingActivatesAfterTankTakesDamage()
    {
        var request = Scenario();
        request.UsePrayerOfMending = true;
        request.TankMaximumHealth = 3000m;
        request.BossSwingDamage = 600m;
        var result = new HolyPriestPreviewRunner().Run(request);
        Assert.Contains(result.Run.Timeline, ev => ev.AbilityKey == "prayer-of-mending" &&
            ev.Type == CombatEventType.Healing && ev.Amount > 0m);
    }

    [Fact]
    public void BindingHealAffectsBothCasterAndAlly()
    {
        var request = Scenario();
        request.UseBindingHeal = true;
        request.PriestInitialHealthPercent = 80m;
        request.TankMaximumHealth = 3000m;
        request.BossSwingDamage = 1300m;
        request.DurationSeconds = 6m;
        var result = new HolyPriestPreviewRunner().Run(request);
        Assert.Contains(result.Run.Timeline, ev => ev.Type == CombatEventType.Healing &&
            ev.AbilityKey == "binding-heal" && ev.TargetActorKey == "priest");
        Assert.Contains(result.Run.Timeline, ev => ev.Type == CombatEventType.Healing &&
            ev.AbilityKey == "binding-heal" && ev.TargetActorKey == "tank");
    }

    [Fact]
    public void PrayerOfHealingAffectsSeveralInjuredRaidMembers()
    {
        var request = Scenario();
        request.UsePrayerOfHealing = true;
        request.AdditionalRaidMembers = 3;
        request.RaidMemberInitialHealthPercent = 50m;
        request.DurationSeconds = 5m;
        var result = new HolyPriestPreviewRunner().Run(request);
        var targets = result.Run.Timeline.Where(e => e.Type == CombatEventType.Healing &&
            e.AbilityKey == "prayer-of-healing" && e.Amount > 0m)
            .Select(e => e.TargetActorKey).Distinct().ToList();
        Assert.True(targets.Count >= 3);
    }

    [Fact]
    public void PenanceIsAThreeTickHealingChannel()
    {
        var request = Scenario();
        request.UsePenance = true;
        request.TankMaximumHealth = 3000m;
        request.BossSwingDamage = 600m;
        request.DurationSeconds = 9m;
        var result = new HolyPriestPreviewRunner().Run(request);
        Assert.Contains(result.Run.Timeline, ev => ev.Type == CombatEventType.AbilityChannelStarted &&
            ev.AbilityKey == "penance");
        Assert.True(result.Run.Timeline.Count(ev => ev.Type == CombatEventType.Healing &&
            ev.AbilityKey == "penance" && ev.IsChannelTick) >= 3);
    }

    [Fact]
    public void CriticalDirectHealCreatesDivineAegisAbsorb()
    {
        var request = Scenario();
        request.UseDivineAegis = true;
        request.PriestHealingCritPercent = 100m;
        request.FlashHealHealthThresholdPercent = 95m;
        request.BossSwingDamage = 500m;
        var result = new HolyPriestPreviewRunner().Run(request);
        Assert.Contains(result.Run.Timeline, ev => ev.Type == CombatEventType.AbsorbApplied &&
            ev.AbilityKey == "divine-aegis");
    }

    [Fact]
    public void HealingCreatesConfigurableThreatWithoutWarriorDependencies()
    {
        var request = Scenario();
        request.FlashHealHealthThresholdPercent = 95m;
        request.HealingThreatCoefficient = 0.5m;
        var result = new HolyPriestPreviewRunner().Run(request);
        Assert.True(result.HealingDone > 0m);
        Assert.True(result.HealingThreatOnBoss > 0m);
    }

    [Fact]
    public void FiveSecondRuleChangesManaConservationComparedToAlwaysOnSpirit()
    {
        var request = Scenario();
        request.FlashHealHealthThresholdPercent = 95m;
        request.BossSwingDamage = 400m;
        request.PriestSpiritRegenPerSecond = 15m;
        request.MeditationCastingSpiritFraction = 0m;
        request.LitanyRefundFraction = 0m;
        var noCastingSpirit = new HolyPriestPreviewRunner().Run(request);
        request.MeditationCastingSpiritFraction = 1m;
        var fullCastingSpirit = new HolyPriestPreviewRunner().Run(request);
        Assert.True(fullCastingSpirit.PriestEndingMana >= noCastingSpirit.PriestEndingMana);
    }

    [Fact]
    public void ExtendedSettingsAreValidated()
    {
        var request = Scenario();
        request.AdditionalRaidMembers = 100;
        Assert.Throws<ArgumentException>(() => new HolyPriestPreviewRunner().Run(request));
        Assert.Throws<ArgumentException>(() =>
            ForeverPriestHealingSpellbook.Create(new ForeverPriestHealingTuning
            { ImprovedRenewRanks = 40 }));
    }
}
