using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Characters.Buffs;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Priest;
using Xunit;

namespace WarcraftSim.Tests;

public sealed class HealingPriestCharacterAndAiCp127Tests
{
    private static CharacterProfile Profile(decimal intellect = 120m, decimal spirit = 100m)
    {
        var profile = new CharacterProfile { ClassKey = "priest", Level = 60 };
        profile.BaseStats.Set("intellect", intellect);
        profile.BaseStats.Set("spirit", spirit);
        return profile;
    }

    private static HolyPriestPreviewRequest Scenario() => new()
    {
        EnableExtendedPriest = true, DurationSeconds = 16m,
        TankMaximumHealth = 3000m, BossSwingDamage = 360m,
        BossSwingIntervalSeconds = 2m, AdditionalRaidMembers = 0,
        PriestMaximumMana = 15000m, PriestSpiritRegenPerSecond = 0m,
        PriestMp5 = 0m, UsePrayerOfMending = false,
        UsePenance = false, UseBindingHeal = false,
        UsePrayerOfHealing = false, UseRenew = false,
        UseDivineAegis = false, LitanyRefundFraction = 0m,
        GreaterHealHealthThresholdPercent = 0m,
        FlashHealHealthThresholdPercent = 0m, CaptureTimeline = true
    };

    [Fact]
    public void CharacterPipelineMergesBaseEquipmentEnchantAndRaidBuff()
    {
        var profile = Profile(100m, 80m);
        var weapon = new CharacterEquipmentItem { SlotKey = "main-hand", ItemKey = "staff" };
        weapon.Stats.Set("intellect", 20m);
        weapon.Stats.Set("healing-power", 45m);
        var enchant = new CharacterEquipmentModifier
        {
            PlacementKey = "enchant", ModifierKey = "enchant-1", ModifierType = "enchant"
        };
        enchant.Stats.Set("healing-power", 25m);
        weapon.Modifiers.Add(enchant);
        profile.Equipment.Items.Add(weapon);
        var buff = new CharacterBuffSelection { BuffKey = "spirit-buff" };
        buff.Stats.Set("spirit", 30m);
        profile.Buffs.Selections.Add(buff);

        var result = ForeverPriestCharacterCalculator.Calculate(profile);
        Assert.Equal(120m, result.EffectiveStats["intellect"]);
        Assert.Equal(110m, result.EffectiveStats["spirit"]);
        Assert.Equal(70m, result.HealingPower);
        Assert.Equal(1376m + 20m + (120m - 20m) * 15m, result.MaximumMana);
    }

    [Fact]
    public void MentalStrengthGuidanceAndManaItemsApplyOnlyOnce()
    {
        var profile = Profile(60m, 200m);
        profile.BaseStats.Set("mana", 100m);
        profile.BaseStats.Set("mp5", 25m);
        var result = ForeverPriestCharacterCalculator.Calculate(profile,
            mentalStrengthRanks: 5, spiritualGuidanceRanks: 5);
        // Mental Strength now increases Intellect itself, not item Mana or
        // the priest's final Mana pool. The effective 60 Intellect is 66.
        Assert.Equal(66m, result.EffectiveStats["intellect"]);
        Assert.Equal(1376m + 20m + (66m - 20m) * 15m + 100m, result.MaximumMana);
        Assert.Equal(50m, result.HealingPower);
        Assert.Equal(25m, result.ManaPerFiveSeconds);
    }

    [Fact]
    public void SpiritRegenerationAssumptionIsUserConfigurable()
    {
        var profile = Profile(100m, 200m);
        var result = ForeverPriestCharacterCalculator.Calculate(profile,
            new ForeverPriestStatAssumptions
            {
                SpiritManaRegenerationPerSecondPerPoint = .10m
            });
        Assert.Equal(20m, result.SpiritRegenerationPerSecond);
    }

    [Fact]
    public void IncorrectCharacterClassOrNegativeRegenerationIsRejected()
    {
        var profile = Profile();
        profile.ClassKey = "warrior";
        Assert.Throws<ArgumentException>(() => ForeverPriestCharacterCalculator.Calculate(profile));
        profile.ClassKey = "priest";
        Assert.Throws<ArgumentException>(() => ForeverPriestCharacterCalculator.Calculate(profile,
            new ForeverPriestStatAssumptions { SpiritManaRegenerationPerSecondPerPoint = -1m }));
    }

    [Fact]
    public void TalentBuildRejectsInaccessibleTierAndInvalidPointBudget()
    {
        var build = new ForeverPriestHealingTalentBuild
        {
            DisciplinePointsSpent = 19, HolyPointsSpent = 32,
            Ranks = new Dictionary<string, int> { ["penance"] = 1 }
        };
        Assert.Throws<ArgumentException>(() => build.Validate());
        build.DisciplinePointsSpent = 21;
        build.HolyPointsSpent = 30;
        build.Validate();
        build.DisciplinePointsSpent = 40;
        Assert.Throws<ArgumentException>(() => build.Validate());
    }

    [Fact]
    public void CannotSpendPenanceAndPrayerOfMendingAtLevelSixty()
    {
        var build = new ForeverPriestHealingTalentBuild
        {
            DisciplinePointsSpent = 21, HolyPointsSpent = 31,
            Ranks = new Dictionary<string, int>
            {
                ["penance"] = 1, ["prayer-of-mending"] = 1
            }
        };
        Assert.Throws<ArgumentException>(() => build.Validate());
    }

    [Fact]
    public void RealTalentRankOneTwoThreeSpiritualHealingValues()
    {
        decimal amount(int rank)
        {
            var tuning = new ForeverPriestHealingTuning
            {
                SpiritualHealingRanks = rank,
                UseLegacySpiritualHealingScaling = false
            };
            return Assert.Single(ForeverPriestHealingSpellbook.Create(tuning),
                x => x.Key == "flash-heal").Effects[0].MinimumValue;
        }
        Assert.Equal(680m * 1.03m, amount(1));
        Assert.Equal(680m * 1.07m, amount(2));
        Assert.Equal(680m * 1.10m, amount(3));
    }

    [Fact]
    public void SoulWardingReducesShieldCostAndCooldown()
    {
        var old = Assert.Single(ForeverPriestHealingSpellbook.Create(
            new ForeverPriestHealingTuning()), s => s.Key == "power-word-shield");
        var improved = Assert.Single(ForeverPriestHealingSpellbook.Create(
            new ForeverPriestHealingTuning { SoulWarding = true }),
            s => s.Key == "power-word-shield");
        Assert.Equal(old.ResourceCosts[0].Amount * .85m,
            improved.ResourceCosts[0].Amount);
        Assert.Equal(0m, improved.CooldownSeconds);
    }

    [Fact]
    public void TalentRestrictedSpellbookHasNoUnlearnedTierSpells()
    {
        var spells = ForeverPriestHealingSpellbook.Create(new ForeverPriestHealingTuning
        {
            RestrictTalentAbilities = true,
            HasBindingHealTalent = true
        });
        Assert.Contains(spells, s => s.Key == "binding-heal");
        Assert.DoesNotContain(spells, s => s.Key == "penance");
        Assert.DoesNotContain(spells, s => s.Key == "prayer-of-mending");
    }

    [Fact]
    public void BonusHealingRaisesSpellHealingWithoutChangingBaseSpellCost()
    {
        var baseline = Assert.Single(ForeverPriestHealingSpellbook.Create(
            new ForeverPriestHealingTuning()), s => s.Key == "flash-heal");
        var geared = Assert.Single(ForeverPriestHealingSpellbook.Create(
            new ForeverPriestHealingTuning { BonusHealingPower = 200m }),
            s => s.Key == "flash-heal");
        Assert.True(geared.Effects[0].MinimumValue > baseline.Effects[0].MinimumValue);
        Assert.Equal(baseline.ResourceCosts[0].Amount, geared.ResourceCosts[0].Amount);
    }

    [Fact]
    public void DefaultLegacySettingsDoNotActivateCharacterOrAiOptIns()
    {
        var request = Scenario();
        Assert.False(request.UseAdaptiveHealingPolicy);
        Assert.False(request.UseCharacterDerivedPriestStats);
        Assert.False(request.UsePriestTalentBuild);
        var result = new HolyPriestPreviewRunner().Run(request);
        Assert.Equal(request.PriestMaximumMana, result.PriestMaximumManaUsed);
        Assert.Null(result.DerivedPriestStats);
    }

    [Fact]
    public void AdaptivePolicyUsesShieldBeforeLegacyLowHealthThreshold()
    {
        var standard = Scenario();
        standard.UsePowerWordShield = true;
        standard.FlashHealHealthThresholdPercent = 0m;
        standard.GreaterHealHealthThresholdPercent = 0m;
        var adaptive = Scenario();
        adaptive.UsePowerWordShield = true;
        adaptive.UseAdaptiveHealingPolicy = true;
        var legacyResult = new HolyPriestPreviewRunner().Run(standard);
        var adaptiveResult = new HolyPriestPreviewRunner().Run(adaptive);
        var legacyShield = Assert.Single(legacyResult.Run.Timeline,
            e => e.Type == CombatEventType.AbsorbApplied && e.AbilityKey == "power-word-shield");
        var adaptiveShield = Assert.Single(adaptiveResult.Run.Timeline,
            e => e.Type == CombatEventType.AbsorbApplied && e.AbilityKey == "power-word-shield");
        Assert.True(adaptiveShield.TimeSeconds < legacyShield.TimeSeconds);
    }

    [Fact]
    public void DerivedCharacterFeedsPreviewManaAndHealingPower()
    {
        var plain = Scenario();
        plain.UsePowerWordShield = false;
        plain.FlashHealHealthThresholdPercent = 96m;
        var geared = Scenario();
        geared.UsePowerWordShield = false;
        geared.FlashHealHealthThresholdPercent = 96m;
        geared.UseCharacterDerivedPriestStats = true;
        geared.PriestCharacter = Profile(150m, 200m);
        geared.PriestCharacter.BaseStats.Set("healing-power", 300m);
        geared.PriestStatAssumptions.SpiritManaRegenerationPerSecondPerPoint = 0m;
        var plainResult = new HolyPriestPreviewRunner().Run(plain);
        var gearedResult = new HolyPriestPreviewRunner().Run(geared);
        var stats = Assert.IsType<ForeverPriestCalculatedStats>(gearedResult.DerivedPriestStats);
        Assert.Equal(stats.MaximumMana, gearedResult.PriestMaximumManaUsed);
        Assert.Contains(plainResult.Run.Timeline,
            e => e.AbilityKey == "flash-heal" && e.Type == CombatEventType.Healing);
        Assert.Contains(gearedResult.Run.Timeline,
            e => e.AbilityKey == "flash-heal" && e.Type == CombatEventType.Healing);
        var plainHeal = plainResult.Run.Timeline.First(e =>
            e.AbilityKey == "flash-heal" && e.Type == CombatEventType.Healing);
        var gearedHeal = gearedResult.Run.Timeline.First(e =>
            e.AbilityKey == "flash-heal" && e.Type == CombatEventType.Healing);
        Assert.True(gearedHeal.RawAmount.GetValueOrDefault() > plainHeal.RawAmount.GetValueOrDefault());
    }

    [Fact]
    public void ActiveTalentModeDoesNotExposePenanceWhenUnselected()
    {
        var request = Scenario();
        request.UsePriestTalentBuild = true;
        request.UsePenance = true;
        request.UsePrayerOfMending = true;
        request.PriestTalents.DisciplinePointsSpent = 18;
        request.PriestTalents.HolyPointsSpent = 33;
        request.PriestTalents.Ranks["prayer-of-mending"] = 1;
        var result = new HolyPriestPreviewRunner().Run(request);
        Assert.False(result.PriestSpellCasts.ContainsKey("penance"));
        Assert.True(result.PriestSpellCasts.ContainsKey("prayer-of-mending"));
    }
}
