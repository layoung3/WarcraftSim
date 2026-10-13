// Consolidated tests for Warrior Simulation Integration.
// Original class names are retained for xUnit test filtering.

// ==================== ForeverWarriorSimulationApiTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using Microsoft.AspNetCore.Mvc;

public sealed class ForeverWarriorSimulationApiTests
{
    private static WarriorSimRequest FastArms() => new()
    {
        Iterations = 2,
        DurationSeconds = 15m,
        ExecutePhaseDurationSeconds = 0m,
        TargetLevel = 60,
        TargetDodgePercent = 0m,
        StartingRage = 50m,
        UseBloodrage = false
    };

    [Fact]
    public void Arms_ProducesDamageAndAbilityBreakdowns()
    {
        var result = new ForeverWarriorSimRunner().Run(FastArms());
        Assert.Equal("arms", result.Preset);
        Assert.Equal(2, result.Batch.Summary.Iterations);
        Assert.True(result.MeanDps > 0m);
        Assert.True(result.Batch.Summary.AverageDamageDoneByAbility.ContainsKey("main-hand-auto"));
        Assert.True(result.Batch.Summary.AverageDamageDoneByAbility.ContainsKey("mortal-strike"));
    }

    [Fact]
    public void Fury_ProducesBloodthirstDamage()
    {
        var request = FastArms();
        request.Preset = "fury";
        var result = new ForeverWarriorSimRunner().Run(request);
        Assert.True(result.MeanDps > 0m);
        Assert.True(result.Batch.Summary.AverageDamageDoneByAbility.ContainsKey("bloodthirst"));
    }

    [Fact]
    public void Level63Boss_UsesAnExplicitGlancingMultiplier()
    {
        var request = FastArms();
        request.TargetLevel = 63;
        request.ProvisionalGlancingDamageMultiplier = 0.70m;
        var result = new ForeverWarriorSimRunner().Run(request);
        Assert.True(result.MeanDps > 0m);
    }

    [Fact]
    public void IncreasingTargetArmor_ReducesDamageInTheProvisionalModel()
    {
        var request = FastArms();
        request.TargetArmor = 0m;
        var runner = new ForeverWarriorSimRunner();
        var unarmored = runner.Run(request);
        request.TargetArmor = 6000m;
        var armored = runner.Run(request);
        Assert.True(unarmored.MeanDps > armored.MeanDps);
    }

    [Fact]
    public void SameInputAndSeed_ProduceTheSameMeanDps()
    {
        var request = FastArms();
        var runner = new ForeverWarriorSimRunner();
        var a = runner.Run(request);
        var b = runner.Run(request);
        Assert.Equal(a.MeanDps, b.MeanDps);
        Assert.Equal(a.Batch.Summary.AverageDamageDoneByAbility["main-hand-auto"],
            b.Batch.Summary.AverageDamageDoneByAbility["main-hand-auto"]);
        Assert.Equal(a.Batch.RepresentativeSeed, b.Batch.RepresentativeSeed);
    }

    [Fact]
    public void RepresentativeTimeline_CanBeDisabled()
    {
        var request = FastArms();
        request.CaptureRepresentativeTimeline = false;
        var result = new ForeverWarriorSimRunner().Run(request);
        Assert.Empty(result.Batch.RepresentativeRun.Timeline);
        Assert.True(result.Batch.RepresentativeRun.Summary.DamageDone > 0m);
    }

    [Fact]
    public void RepresentativeTimeline_CanBeCapturedOnDemand()
    {
        var request = FastArms();
        request.CaptureRepresentativeTimeline = true;
        var result = new ForeverWarriorSimRunner().Run(request);
        Assert.NotEmpty(result.Batch.RepresentativeRun.Timeline);
    }

    [Fact]
    public void ExplicitProvisionalInputs_AreReportedAsLimitations()
    {
        var result = new ForeverWarriorSimRunner().Run(FastArms());
        Assert.Contains(result.ModelingNotes, note => note.Contains("Glancing", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.ModelingNotes, note => note.Contains("Armor", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.ModelingNotes, note => note.Contains("Rend", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DualWieldFury_RunsBothIndependentAutoStreams()
    {
        var request = FastArms();
        request.Preset = "fury";
        request.DualWield = true;
        request.IsTwoHanded = false;
        var result = new ForeverWarriorSimRunner().Run(request);
        Assert.True(result.Batch.Summary.AverageDamageDoneByAbility.ContainsKey("main-hand-auto"));
        Assert.True(result.Batch.Summary.AverageDamageDoneByAbility.ContainsKey("off-hand-auto"));
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("")]
    public void InvalidPreset_IsRejected(string preset)
    {
        var request = FastArms();
        request.Preset = preset;
        Assert.Throws<ArgumentException>(() => new ForeverWarriorSimRunner().Run(request));
    }

    [Fact]
    public void InvalidImpalePrerequisite_IsRejected()
    {
        var request = FastArms();
        request.DeepWoundsRank = 2;
        Assert.Throws<ArgumentException>(() => new ForeverWarriorSimRunner().Run(request));
    }

    [Fact]
    public void TwoHandWithOffhand_IsRejected()
    {
        var request = FastArms();
        request.DualWield = true;
        Assert.Throws<ArgumentException>(() => new ForeverWarriorSimRunner().Run(request));
    }

    [Fact]
    public void InvalidIterationCount_IsRejected()
    {
        var request = FastArms();
        request.Iterations = 501;
        Assert.Throws<ArgumentException>(() => new ForeverWarriorSimRunner().Run(request));
    }

    [Fact]
    public void UnverifiedArmorOrGlancingInputs_AreNeverAcceptedOutsideBounds()
    {
        var request = FastArms();
        request.ProvisionalGlancingDamageMultiplier = -0.1m;
        Assert.Throws<ArgumentException>(() => new ForeverWarriorSimRunner().Run(request));
        request.ProvisionalGlancingDamageMultiplier = 0.7m;
        request.ProvisionalArmorBaseConstant = 0m;
        request.ProvisionalArmorPerAttackerLevelConstant = 0m;
        Assert.Throws<ArgumentException>(() => new ForeverWarriorSimRunner().Run(request));
    }

    [Fact]
    public void FuryBloodthrillWithoutStanceSwitching_IsValidButDoesNotGrantOverpower()
    {
        // Cross-tree talent selections are legal. The current Fury preview
        // stays in Berserker Stance and does not create an Overpower reaction.
        // Do not reject the build or pretend that stance switching occurred.
        var request = FastArms();
        request.Preset = "fury";
        request.BloodthrillRank = 1;
        request.CaptureRepresentativeTimeline = true;

        var result = new ForeverWarriorSimRunner().Run(request);

        Assert.True(result.MeanDps > 0m);
        Assert.Contains("bloodthirst", result.Batch.Summary.AverageDamageDoneByAbility.Keys);
        Assert.DoesNotContain("overpower", result.Batch.Summary.AverageDamageDoneByAbility.Keys);
        Assert.DoesNotContain(result.Batch.RepresentativeRun.Timeline,
            e => e.Type == WarcraftSim.Core.Simulation.Engine.CombatEventType.ReactiveOpportunityGranted);
    }

    [Fact]
    public async Task ControllerReturnsBadRequestForInvalidInput()
    {
        var request = FastArms();
        request.DurationSeconds = 0m;
        var action = await new WarriorSimulationController().Run(request);
        Assert.IsType<BadRequestObjectResult>(action.Result);
    }

    [Fact]
    public async Task ControllerReturnsSimulationResponseForValidInput()
    {
        var action = await new WarriorSimulationController().Run(FastArms());
        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var result = Assert.IsType<WarriorSimResponse>(ok.Value);
        Assert.Equal(2, result.Batch.Summary.Iterations);
    }
}
}

// ==================== ForeverWarriorSupportEffectsTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Data.Forever.Combat;
using Microsoft.AspNetCore.Mvc;

public sealed class ForeverWarriorSupportEffectsTests
{
    private static WarriorSimRequest Request() => new()
    {
        Preset = "arms", Level = 60, Iterations = 1, DurationSeconds = 15m,
        TargetLevel = 60, ExecutePhaseDurationSeconds = 0m,
        TargetDodgePercent = 0m, UseBloodrage = false,
        DeepWoundsRank = 0, ImpaleRank = 0
    };

    [Fact]
    public void SupportCatalog_IsVersionedWithCategorizedEffectsAndProvenance()
    {
        var catalog = ForeverSupportEffectCatalog.GetCatalog();
        Assert.NotEmpty(catalog.CatalogVersion);
        Assert.Contains(catalog.Effects, x => x.Category == "raid-buff");
        Assert.Contains(catalog.Effects, x => x.Category == "consumable");
        Assert.Contains(catalog.Effects, x => x.Category == "boss-debuff");
        Assert.All(catalog.Effects, x => Assert.StartsWith("https://", x.SourceUrl));
        Assert.All(catalog.Effects, x => Assert.NotEmpty(x.SourceStatus));
    }

    [Fact]
    public void ZeroSelections_KeepStatsAndBossArmorUnchanged()
    {
        var request = Request();
        request.TargetArmor = 3500m;
        var output = new ForeverWarriorSimRunner().Run(request);
        Assert.Empty(output.ActiveSupportEffects);
        Assert.Equal(3500m, output.EffectiveBossArmor);
        Assert.Equal(request.AttackPower, output.EffectiveCharacterStats[ForeverCombatStatKeys.AttackPower]);
    }

    [Fact]
    public void BattleShoutAndMight_StackAsAdditiveBuffSources()
    {
        var request = Request();
        request.RaidBuffIds = ["battle-shout", "blessing-of-might"];
        var calculated = WarriorGearStatCalculator.Calculate(request);
        Assert.Equal(1200m + 139m + 133m,
            calculated.EffectiveStats.Get(ForeverCombatStatKeys.AttackPower));
        Assert.Contains(calculated.StatContributions, x => x.SourceName == "Battle Shout (rank 7)");
        Assert.Contains(calculated.StatContributions, x => x.SourceName == "Blessing of Might (highest rank)");
    }

    [Fact]
    public void AgilityAndStrengthBuffs_PassThroughDerivedConversions()
    {
        var request = Request();
        request.RaidBuffIds = ["strength-of-earth", "grace-of-air", "leader-of-the-pack"];
        var calculated = WarriorGearStatCalculator.Calculate(request);
        Assert.Equal(1200m + 106m,
            calculated.EffectiveStats.Get(ForeverCombatStatKeys.AttackPower));
        Assert.Equal(20m + 3m + 89m / 20m,
            calculated.EffectiveStats.Get(ForeverCombatStatKeys.AttackCriticalChancePercent));
    }

    [Fact]
    public void ConsumableBonuses_AffectCalculatedStats()
    {
        var request = Request();
        request.ConsumableIds = ["elixir-of-giants", "elixir-of-the-mongoose"];
        var calculated = WarriorGearStatCalculator.Calculate(request);
        Assert.Equal(1250m, calculated.EffectiveStats.Get(ForeverCombatStatKeys.AttackPower));
        Assert.Equal(20m + 2m + 25m / 20m,
            calculated.EffectiveStats.Get(ForeverCombatStatKeys.AttackCriticalChancePercent));
    }

    [Fact]
    public void MajorAndMinorArmorDebuffs_StackAndClampAtZero()
    {
        var request = Request();
        request.TargetArmor = 3500m;
        request.BossDebuffIds = ["sunder-armor", "faerie-fire", "curse-of-recklessness"];
        request.SunderArmorStacks = 5;
        var output = new ForeverWarriorSimRunner().Run(request);
        Assert.Equal(3500m - 2250m - 505m - 505m, output.EffectiveBossArmor);
        request.TargetArmor = 100m;
        Assert.Equal(0m, new ForeverWarriorSimRunner().Run(request).EffectiveBossArmor);
    }

    [Theory]
    [InlineData(1, 450)]
    [InlineData(3, 1350)]
    [InlineData(5, 2250)]
    public void SunderArmorStacks_AreExplicitAndBounded(int stacks, int reduction)
    {
        var request = Request();
        request.TargetArmor = 4000m;
        request.BossDebuffIds = ["sunder-armor"];
        request.SunderArmorStacks = stacks;
        Assert.Equal(4000m - reduction, new ForeverWarriorSimRunner().Run(request).EffectiveBossArmor);
    }

    [Fact]
    public void ExposeArmor_ReplacesSunderInTheMajorArmorGroup()
    {
        var request = Request();
        request.BossDebuffIds = ["sunder-armor", "expose-armor"];
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(request));
    }

    [Fact]
    public void ConflictingConsumables_AreRejectedRatherThanDoubleCounted()
    {
        var request = Request();
        request.ConsumableIds = ["juju-might", "winterfall-firewater"];
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(request));
    }

    [Fact]
    public void UnsupportedActiveProcAndPercentMultiplier_AreRejected()
    {
        var request = Request();
        request.RaidBuffIds = ["windfury-totem"];
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(request));
        request.RaidBuffIds = ["blessing-of-kings"];
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(request));
    }

    [Fact]
    public void UnknownDuplicateAndWrongCategoryIds_FailValidation()
    {
        var request = Request();
        request.RaidBuffIds = ["nonexistent"];
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(request));
        request.RaidBuffIds = ["battle-shout", "battle-shout"];
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(request));
        request.RaidBuffIds = ["elixir-of-giants"];
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(request));
    }

    [Fact]
    public void BuffedAndDebuffedSim_ProducesMoreDamageWithSameRngSeed()
    {
        var request = Request();
        request.TargetArmor = 4000m;
        var runner = new ForeverWarriorSimRunner();
        var baseline = runner.Run(request);
        request.RaidBuffIds = ["battle-shout", "blessing-of-might"];
        request.BossDebuffIds = ["sunder-armor"];
        var supported = runner.Run(request);
        Assert.True(supported.MeanDps > baseline.MeanDps);
        Assert.Equal(1750m, supported.EffectiveBossArmor);
    }

    [Fact]
    public void ApplyingSupportDoesNotMutateSavedRequest()
    {
        var request = Request();
        request.RaidBuffIds = ["battle-shout"];
        request.BossDebuffIds = ["sunder-armor"];
        request.TargetArmor = 4000m;
        _ = new ForeverWarriorSimRunner().Run(request);
        Assert.Equal(4000m, request.TargetArmor);
        Assert.Equal(1200m, request.AttackPower);
        Assert.Single(request.RaidBuffIds, x => x == "battle-shout");
    }

    [Fact]
    public void CatalogEquipmentAndSupportBuffs_CombineWithoutChangingItemData()
    {
        var request = Request();
        request.EquipmentMode = "catalog";
        request.Gear = [new WarriorGearEntry { Slot = "main-hand", ItemId = 279259 }];
        request.RaidBuffIds = ["battle-shout"];
        var result = new ForeverWarriorSimRunner().Run(request);
        // Ironwood Blade contributes 8 strength => 16 AP; shout adds another 139.
        Assert.Equal(1200m + 16m + 139m, result.EffectiveCharacterStats[ForeverCombatStatKeys.AttackPower]);
        Assert.Contains(result.ActiveSupportEffects, x => x.Contains("Battle Shout"));
        Assert.Equal("catalog", request.EquipmentMode);
        Assert.Equal(1200m, request.AttackPower);
    }

    [Fact]
    public void SupportCatalogEndpoint_ReturnsDataWithoutRunningSim()
    {
        var controller = new WarriorSimulationController();
        var response = controller.SupportCatalog();
        var result = Assert.IsType<OkObjectResult>(response.Result);
        var catalog = Assert.IsType<ForeverSupportCatalogDocument>(result.Value);
        Assert.NotEmpty(catalog.Effects);
    }
}
}

// ==================== ForeverWarriorTalentRaceBuilderTests.cs ====================
namespace WarcraftSim.Tests
{
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Data.Forever.Combat;

public sealed class ForeverWarriorTalentRaceBuilderTests
{
    private static WarriorSimRequest Request() => new()
    {
        Level = 60, Preset = "arms", Iterations = 1, DurationSeconds = 12m,
        ExecutePhaseDurationSeconds = 0m, TargetLevel = 60,
        TargetDodgePercent = 0m, TargetParryPercent = 0m, TargetArmor = 0m,
        UseBloodrage = false, DeepWoundsRank = 0, ImpaleRank = 0
    };

    [Fact]
    public void Catalog_CoversCurrentFiftyTwoTalentThreeTreeLayout()
    {
        var all = ForeverWarriorTalentCatalog.Talents;
        Assert.Equal(52, all.Count);
        Assert.Equal(17, all.Count(x => x.Tree == "arms"));
        Assert.Equal(17, all.Count(x => x.Tree == "fury"));
        Assert.Equal(18, all.Count(x => x.Tree == "protection"));
        Assert.Equal(all.Count, all.Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains(all, x => x.Id == "spearing-strike" && x.Row == 4);
        Assert.Contains(all, x => x.Id == "raging-blows" && x.Row == 4);
        Assert.Contains(all, x => x.Id == "vanguard" && x.Row == 4);
        Assert.DoesNotContain(all, x => x.Id == "improved-cleave");
        Assert.DoesNotContain(all, x => x.Id == "toughness");
    }

    [Theory]
    [InlineData("arms", 17)]
    [InlineData("fury", 17)]
    [InlineData("protection", 18)]
    public void TalentDefinitions_HaveValidRowsColumnsAndRankCaps(string tree, int count)
    {
        var definitions = ForeverWarriorTalentCatalog.Talents.Where(x => x.Tree == tree).ToList();
        Assert.Equal(count, definitions.Count);
        Assert.All(definitions, d =>
        {
            Assert.InRange(d.Row, 1, 7);
            Assert.InRange(d.Column, 1, 4);
            Assert.InRange(d.MaxRank, 1, 5);
            Assert.NotEmpty(d.Description);
        });
    }

    [Fact]
    public void TreeMode_EnforcesPrerequisitesAndTierUnlocks()
    {
        var r = Request();
        r.TalentMode = "tree";
        r.TalentRanks["impale"] = 1;
        Assert.Throws<ArgumentException>(() => ForeverWarriorTalentCatalog.Validate(r));
        r.TalentRanks.Clear();
        r.TalentRanks["improved-rend"] = 3;
        r.TalentRanks["deflection"] = 5;
        r.TalentRanks["improved-heroic-strike"] = 3;
        r.TalentRanks["improved-tactical-mastery"] = 5;
        r.TalentRanks["deep-wounds"] = 3;
        r.TalentRanks["impale"] = 2;
        ForeverWarriorTalentCatalog.Validate(r);
        r.TalentRanks["deep-wounds"] = 2;
        Assert.Throws<ArgumentException>(() => ForeverWarriorTalentCatalog.Validate(r));
    }

    [Fact]
    public void UnrecognizedAndOverCapTalentRanks_RejectWithoutChangingRequest()
    {
        var r = Request();
        r.TalentMode = "tree";
        r.TalentRanks["cruelty"] = 6;
        Assert.Throws<ArgumentException>(() => ForeverWarriorTalentCatalog.Validate(r));
        r.TalentRanks.Clear();
        r.TalentRanks["invented-talent"] = 1;
        Assert.Throws<ArgumentException>(() => ForeverWarriorTalentCatalog.Validate(r));
        Assert.Equal(1, r.TalentRanks["invented-talent"]);
    }

    [Fact]
    public void BaseRaces_AreAvailableAndProvisional()
    {
        Assert.Equal(9, ForeverWarriorRaceProfiles.Races.Count);
        Assert.All(ForeverWarriorRaceProfiles.Races, x => Assert.False(x.IsVerifiedForever));
        Assert.Equal(120m, ForeverWarriorRaceProfiles.Get("human").Strength);
        Assert.Equal(123m, ForeverWarriorRaceProfiles.Get("orc").Strength);
    }

    [Fact]
    public void RaceBaseMode_IgnoresManualSheetValuesAndCalculatesStrengthAndAgility()
    {
        var r = Request();
        r.StatSourceMode = "race-base";
        r.Race = "human";
        r.AttackPower = 123456m;
        r.MeleeCritPercent = 99m;
        var stats = WarriorGearStatCalculator.Calculate(r).EffectiveStats;
        Assert.Equal(120m, stats.Get(ForeverCombatStatKeys.Strength));
        Assert.Equal(80m, stats.Get(ForeverCombatStatKeys.Agility));
        Assert.Equal(240m, stats.Get(ForeverCombatStatKeys.AttackPower));
        Assert.Equal(4m, stats.Get(ForeverCombatStatKeys.AttackCriticalChancePercent));
        Assert.Equal(110m, stats.Get("stamina"));
    }

    [Fact]
    public void RaceMode_WithCruelty_StacksTalentAndRaceBonuses()
    {
        var r = Request();
        r.StatSourceMode = "race-base";
        r.Race = "orc";
        r.TalentMode = "tree";
        r.TalentRanks["cruelty"] = 5;
        var stats = WarriorGearStatCalculator.Calculate(r);
        Assert.Equal(246m, stats.EffectiveStats.Get(ForeverCombatStatKeys.AttackPower));
        Assert.Equal(77m / 20m + 5m,
            stats.EffectiveStats.Get(ForeverCombatStatKeys.AttackCriticalChancePercent));
        Assert.Contains(stats.StatContributions, x => x.SourceName == "Cruelty");
    }

    [Fact]
    public void RaceMode_ChangesAnActualSimulationWithoutMutatingInput()
    {
        var r = Request();
        r.StatSourceMode = "race-base";
        r.Race = "human";
        var baselineAP = r.AttackPower;
        var output = new ForeverWarriorSimRunner().Run(r);
        Assert.Equal(240m, output.EffectiveCharacterStats[ForeverCombatStatKeys.AttackPower]);
        Assert.Equal(baselineAP, r.AttackPower);
        Assert.Contains(output.ModelingNotes, x => x.Contains("RACIAL BASE PREVIEW"));
    }

    [Fact]
    public void TreeMode_WithoutMortalStrike_DoesNotReportMortalStrikeDamage()
    {
        var r = Request();
        r.TalentMode = "tree";
        var result = new ForeverWarriorSimRunner().Run(r);
        Assert.DoesNotContain(result.Batch.Summary.AverageDamageDoneByAbility.Keys,
            x => x.Contains("Mortal Strike", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TreeMode_NotesPlannerOnlyTalentSelections()
    {
        var r = Request();
        r.TalentMode = "tree";
        r.TalentRanks["deflection"] = 5;
        var result = new ForeverWarriorSimRunner().Run(r);
        Assert.Contains(result.ModelingNotes, x =>
            x.Contains("TALENT NOT YET SIMULATED: Deflection"));
    }

    [Fact]
    public void PartiallyModeledSpecializations_AreReportedWithoutCallingThemUnsupported()
    {
        var twoHanded = ForeverWarriorTalentCatalog.Talents.Single(x => x.Id == "two-handed-weapon-specialization");
        var dualWield = ForeverWarriorTalentCatalog.Talents.Single(x => x.Id == "dual-wield-specialization");
        Assert.Equal("partial", twoHanded.SimulationStatus);
        Assert.Equal("partial", dualWield.SimulationStatus);
        var request = Request();
        request.TalentMode = "tree";
        request.TalentRanks["two-handed-weapon-specialization"] = 1;
        Assert.Contains(ForeverWarriorTalentCatalog.UnmodeledSelections(request),
            x => x.Contains("TALENT PARTIALLY SIMULATED: Two-Handed Weapon Specialization"));
    }

    [Fact]
    public void SpearingStrike_DamageScalesWithExplicitCreatureCategory()
    {
        var normal = WarcraftSim.Data.Forever.Abilities.Warrior.ForeverSpearingStrikeFactory.Create(
            100m, 200m, 3.3m, "main-hand-weapon-skill", false);
        var bonus = WarcraftSim.Data.Forever.Abilities.Warrior.ForeverSpearingStrikeFactory.Create(
            100m, 200m, 3.3m, "main-hand-weapon-skill", true);
        Assert.Equal(40m, normal.Effects.Single().MinimumValue);
        Assert.Equal(120m, bonus.Effects.Single().MinimumValue);
        Assert.Equal(20m, bonus.CooldownSeconds);
        Assert.Equal(15m, bonus.ResourceCosts.Single().Amount);
    }

    [Fact]
    public void SpearingStrike_RequiresSelectionAndSupportedCreatureType()
    {
        var r = Request();
        r.TalentMode = "tree";
        r.TalentRanks["improved-heroic-strike"] = 3;
        r.TalentRanks["deflection"] = 5;
        r.TalentRanks["improved-rend"] = 3;
        r.TalentRanks["improved-overpower"] = 2;
        r.TalentRanks["improved-tactical-mastery"] = 2;
        r.TalentRanks["spearing-strike"] = 1;
        ForeverWarriorSimRunner.Validate(r);
        r.TargetCreatureType = "invented";
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(r));
    }

    [Fact]
    public void CatalogController_ExposesTalentsAndRaces()
    {
        var controller = new WarriorSimulationController();
        Assert.IsType<OkObjectResult>(controller.TalentCatalog());
        Assert.IsType<OkObjectResult>(controller.RaceCatalog());
    }
}
}
