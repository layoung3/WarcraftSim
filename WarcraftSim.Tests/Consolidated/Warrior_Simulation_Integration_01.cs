// Consolidated tests for Warrior Simulation Integration.
// Original class names are retained for xUnit test filtering.

// ==================== ForeverWarriorCheckpoint113Tests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;

public sealed class ForeverWarriorCheckpoint113Tests
{
    private static WarriorSimRequest Request() => new()
    {
        Preset = "arms", Level = 60, Iterations = 1, DurationSeconds = 8m,
        ExecutePhaseDurationSeconds = 0m, TargetArmor = 0m, TargetLevel = 60,
        TargetDodgePercent = 0m, TargetParryPercent = 0m,
        UseBloodrage = false, UseHeroicStrike = false, StartingRage = 0m,
        MeleeHitPercent = 100m,
        CaptureRepresentativeTimeline = true, TalentMode = "tree"
    };

    [Theory]
    [InlineData(0.00)]
    [InlineData(1.25)]
    [InlineData(2.75)]
    public void OpeningAutoAttackDelay_ControlsFirstActualSwing(double delay)
    {
        var r = Request();
        r.OpeningAutoAttackSeconds = (decimal)delay;
        var result = new ForeverWarriorSimRunner().Run(r);
        Assert.Contains(result.Batch.RepresentativeRun.Timeline, e =>
            e.Type == CombatEventType.Damage && e.AbilityKey == "main-hand-auto" &&
            e.TimeSeconds == (decimal)delay);
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(8.0)]
    public void OpeningAutoAttackDelay_OutOfRangeIsRejected(double delay)
    {
        var r = Request();
        r.OpeningAutoAttackSeconds = (decimal)delay;
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(r));
    }

    [Fact]
    public void CustomTalentGatedAbility_RemainsEditableButIsSkippedWithoutTalent()
    {
        var r = Request();
        r.RotationMode = "custom";
        r.CustomRotation = [new WarriorRotationRule { Action = "mortal-strike" }];
        var profile = WarriorRotationRules.BuildCustom(r);
        Assert.Empty(profile.Entries);
        var output = new ForeverWarriorSimRunner().Run(r);
        Assert.DoesNotContain(output.Batch.Summary.AverageDamageDoneByAbility.Keys,
            x => x.Contains("mortal-strike", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AutomaticArms_WithoutMortalStrike_StillRunsAutoAttacks()
    {
        var r = Request();
        var output = new ForeverWarriorSimRunner().Run(r);
        Assert.Contains("main-hand-auto", output.Batch.Summary.AverageDamageDoneByAbility.Keys);
        Assert.DoesNotContain("mortal-strike", output.Batch.Summary.AverageDamageDoneByAbility.Keys);
    }

    [Fact]
    public void AngerManagement_AddsPeriodicRageAtThreeSecondIntervals()
    {
        var r = Request();
        r.TalentRanks["deflection"] = 5;
        r.TalentRanks["improved-tactical-mastery"] = 5;
        r.TalentRanks["anger-management"] = 1;
        var output = new ForeverWarriorSimRunner().Run(r);
        Assert.Contains(output.Batch.RepresentativeRun.Timeline, e =>
            e.Type == CombatEventType.ResourceChanged &&
            e.AbilityKey == "anger-management" && e.TimeSeconds == 3m);
        Assert.Contains(output.Batch.RepresentativeRun.Timeline, e =>
            e.Type == CombatEventType.ResourceChanged &&
            e.AbilityKey == "anger-management" && e.TimeSeconds == 6m);
    }

    [Fact]
    public void AngerManagement_WithoutRequiredPrerequisite_IsRejected()
    {
        var r = Request();
        r.TalentRanks["deflection"] = 5;
        r.TalentRanks["improved-heroic-strike"] = 3;
        r.TalentRanks["improved-rend"] = 3;
        r.TalentRanks["anger-management"] = 1;
        Assert.Throws<ArgumentException>(() => ForeverWarriorTalentCatalog.Validate(r));
    }

    [Fact]
    public void DeepWounds_NowValidatesImprovedRendPrerequisite()
    {
        var r = Request();
        r.TalentRanks["deflection"] = 5;
        r.TalentRanks["improved-tactical-mastery"] = 5;
        r.TalentRanks["deep-wounds"] = 1;
        Assert.Throws<ArgumentException>(() => ForeverWarriorTalentCatalog.Validate(r));
        r.TalentRanks["improved-rend"] = 3;
        ForeverWarriorTalentCatalog.Validate(r);
    }

    [Fact]
    public void ImprovedExecute_CanBeSelected_WithoutForcingBloodthirst()
    {
        var r = Request();
        r.Preset = "fury";
        r.TalentRanks["cruelty"] = 5;
        r.TalentRanks["booming-voice"] = 5;
        r.TalentRanks["unbridled-wrath"] = 5;
        r.TalentRanks["improved-execute"] = 2;
        ForeverWarriorSimRunner.Validate(r);
        Assert.DoesNotContain("bloodthirst", new ForeverWarriorSimRunner().Run(r).Batch.Summary.AverageDamageDoneByAbility.Keys);
    }

    [Fact]
    public void RagingBlowsAndImprovedSlam_AreNowModeledTalentDefinitions()
    {
        Assert.Equal("modeled", ForeverWarriorTalentCatalog.Talents.Single(x => x.Id == "raging-blows").SimulationStatus);
        Assert.Equal("modeled", ForeverWarriorTalentCatalog.Talents.Single(x => x.Id == "improved-slam").SimulationStatus);
        Assert.Equal("modeled", ForeverWarriorTalentCatalog.Talents.Single(x => x.Id == "improved-execute").SimulationStatus);
        Assert.Equal("modeled", ForeverWarriorTalentCatalog.Talents.Single(x => x.Id == "improved-bloodrage").SimulationStatus);
    }

    [Fact]
    public void CatalogMode_AllowsCatalogWeaponAndCustomNonWeaponStats()
    {
        var weapon = ForeverEquipmentCatalog.GetCatalog().Items.First(x =>
            x.Weapon is not null && x.AllowedSlots.Contains("main-hand"));
        var r = Request();
        r.EquipmentMode = "catalog";
        r.StatSourceMode = "race-base";
        r.Gear =
        [
            new WarriorGearEntry { Slot = "main-hand", ItemId = weapon.Id },
            new WarriorGearEntry
            {
                Slot = "neck", ItemKey = "custom-neck", Name = "Custom test necklace",
                Stats = new() { [ForeverCombatStatKeys.Strength] = 20m }
            }
        ];
        var resolved = ForeverEquipmentCatalog.ResolveWarriorLoadout(r);
        Assert.Equal("gear-bonuses", resolved.EquipmentMode);
        Assert.Contains(resolved.Gear, x => x.Slot == "neck" && x.Name == "Custom test necklace");
        var output = new ForeverWarriorSimRunner().Run(r);
        Assert.True(output.EffectiveCharacterStats[ForeverCombatStatKeys.Strength] >= 20m);
    }

    [Fact]
    public void CatalogMode_RejectsFakeCustomWeaponAndClientItemOverrides()
    {
        var r = Request();
        r.EquipmentMode = "catalog";
        r.Gear = [new WarriorGearEntry
        {
            Slot = "main-hand", ItemKey = "custom-main-hand", Name = "Unsafe sword",
            Stats = new() { [ForeverCombatStatKeys.Strength] = 500m }
        }];
        Assert.Throws<ArgumentException>(() => ForeverEquipmentCatalog.ResolveWarriorLoadout(r));
    }

    [Fact]
    public void RageTalentProc_UsesOnlyWeaponDamageNotBleedTicks()
    {
        var actor = new SimulationActorState { Key = "warrior", TeamKey = "raid" };
        actor.InitializeHealth(5000m);
        actor.AddResource(new ResourceState("rage", 100m, 0m));
        var ctx = new SimulationContext(new SimulationRunOptions { Seed = 12345, DurationSeconds = 10m });
        ctx.AddActor(actor);
        var processor = new WarriorRageTalentProcessor(5);
        for (var i = 0; i < 100; i++)
            processor.Process(ctx, new CombatEvent
            {
                Type = CombatEventType.Damage, SourceActorKey = "warrior",
                AbilityKey = "main-hand-auto", WeaponHandKey = "main-hand", Amount = 1m
            });
        Assert.True(actor.Resources["rage"].Current > 0m);
        var before = actor.Resources["rage"].Current;
        for (var i = 0; i < 100; i++)
            processor.Process(ctx, new CombatEvent
            {
                Type = CombatEventType.Damage, SourceActorKey = "warrior",
                AbilityKey = "main-hand-auto", WeaponHandKey = "main-hand", Amount = 1m,
                EffectDeliveryType = CombatEffectDeliveryType.Periodic
            });
        Assert.Equal(before, actor.Resources["rage"].Current);
    }
}
}

// ==================== ForeverWarriorConfigurableRotationTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Data.Forever.Combat;

public sealed class ForeverWarriorConfigurableRotationTests
{
    private static WarriorSimRequest BaseRequest() => new()
    {
        Preset = "fury", Iterations = 2, DurationSeconds = 20m,
        ExecutePhaseDurationSeconds = 0m,
        TargetLevel = 60, TargetDodgePercent = 0m, UseBloodrage = false,
        StartingRage = 100m, DeepWoundsRank = 0, ImpaleRank = 0
    };

    private static WarriorRotationRule Rule(string key) => new() { Action = key };

    [Fact]
    public void AutomaticMode_RemainsDefaultForExistingClients()
    {
        var request = BaseRequest();
        var result = new ForeverWarriorSimRunner().Run(request);
        Assert.Equal("automatic", request.RotationMode);
        Assert.True(result.MeanDps > 0m);
        Assert.Contains("bloodthirst", result.Batch.Summary.AverageDamageDoneByAbility.Keys);
    }

    [Fact]
    public void CustomRotation_UsesUserPriorityAndAllowsFuryAction()
    {
        var request = BaseRequest();
        request.RotationMode = "custom";
        request.CustomRotation = [Rule("bloodthirst"), Rule("whirlwind")];
        var result = new ForeverWarriorSimRunner().Run(request);
        Assert.Contains("bloodthirst", result.Batch.Summary.AverageDamageDoneByAbility.Keys);
    }

    [Fact]
    public void StatCondition_UsesActualActorAttackPower()
    {
        var request = BaseRequest();
        request.RotationMode = "custom";
        request.CustomRotation =
        [
            new WarriorRotationRule { Action = "bloodthirst", MinimumAttackPower = 1500m },
            new WarriorRotationRule { Action = "whirlwind", MaximumAttackPower = 1499m }
        ];
        var profile = WarriorRotationRules.BuildCustom(request);
        Assert.Equal(2, profile.Entries.Count);
        Assert.Contains(profile.Entries[0].Conditions,
            c => c.ConditionType == RotationConditionTypes.SourceStatValue &&
                 c.Key == ForeverCombatStatKeys.AttackPower && c.Value == 1500m);
        var result = new ForeverWarriorSimRunner().Run(request);
        Assert.DoesNotContain("bloodthirst", result.Batch.Summary.AverageDamageDoneByAbility.Keys);
        Assert.Contains("whirlwind", result.Batch.Summary.AverageDamageDoneByAbility.Keys);
    }

    [Fact]
    public void Execute_IsAlwaysRestrictedToExecutePhase()
    {
        var request = BaseRequest();
        request.RotationMode = "custom";
        request.CustomRotation = [new WarriorRotationRule
        {
            Action = "execute", TargetHealthAtOrBelowPercent = 90m
        }, Rule("bloodthirst")];
        var profile = WarriorRotationRules.BuildCustom(request);
        Assert.Contains(profile.Entries[0].Conditions,
            c => c.ConditionType == RotationConditionTypes.TargetHealthPercent && c.Value == 20m);
        var result = new ForeverWarriorSimRunner().Run(request);
        Assert.DoesNotContain("execute", result.Batch.Summary.AverageDamageDoneByAbility.Keys);
    }

    [Fact]
    public void Execute_AttackPowerConditionCanPreferBloodthirstDuringExecute()
    {
        var request = BaseRequest();
        request.ExecutePhaseDurationSeconds = request.DurationSeconds;
        request.RotationMode = "custom";
        request.CustomRotation = [new WarriorRotationRule
        {
            Action = "execute", MaximumAttackPower = 1000m
        }, Rule("bloodthirst")];
        var result = new ForeverWarriorSimRunner().Run(request);
        Assert.DoesNotContain("execute", result.Batch.Summary.AverageDamageDoneByAbility.Keys);
        Assert.Contains("bloodthirst", result.Batch.Summary.AverageDamageDoneByAbility.Keys);
    }

    [Fact]
    public void RendCustomRuleAlwaysRequiresMissingAura()
    {
        var request = BaseRequest();
        request.Preset = "arms";
        request.RotationMode = "custom";
        request.CustomRotation = [Rule("rend")];
        var profile = WarriorRotationRules.BuildCustom(request);
        Assert.Contains(profile.Entries[0].Conditions,
            c => c.ConditionType == RotationConditionTypes.TargetAuraMissing);
    }

    [Fact]
    public void ArmsSavedFuryCooldownRule_IsSkippedAndReportedWithoutFailingSimulation()
    {
        var request = BaseRequest();
        request.Preset = "arms";
        request.RotationMode = "custom";
        request.CustomRotation =
        [
            new WarriorRotationRule { Action = "whirlwind", OnlyWhenAbilityOnCooldown = "bloodthirst" },
            Rule("rend")
        ];
        var profile = WarriorRotationRules.BuildCustom(request);
        Assert.Single(profile.Entries);
        Assert.Equal("rend", profile.Entries[0].AbilityKey);
        Assert.Single(WarriorRotationRules.GetSkippedCooldownDependencies(request));
        var result = new ForeverWarriorSimRunner().Run(request);
        Assert.Contains(result.ModelingNotes, note => note.Contains("SKIPPED CUSTOM PRIORITY") && note.Contains("bloodthirst"));
    }

    [Fact]
    public void RendFirstRule_DoesNotClipActiveBleedEveryGcd()
    {
        var request = BaseRequest();
        request.Preset = "arms";
        request.StartingRage = 100m;
        request.DurationSeconds = 20m; // level-60 Rend lasts 21s
        request.CaptureRepresentativeTimeline = true;
        request.RotationMode = "custom";
        request.CustomRotation = [Rule("rend")];
        var result = new ForeverWarriorSimRunner().Run(request);
        Assert.Single(result.Batch.RepresentativeRun.Timeline, e =>
            e.Type == WarcraftSim.Core.Simulation.Engine.CombatEventType.AbilityCastStarted &&
            e.AbilityKey == "rend");
    }

    [Fact]
    public void OptionalSlamCanBeSelectedForTheRotation()
    {
        var request = BaseRequest();
        request.UseSlam = true;
        request.RotationMode = "custom";
        request.CustomRotation = [Rule("slam")];
        var result = new ForeverWarriorSimRunner().Run(request);
        Assert.Contains("slam", result.Batch.RepresentativeRun.Summary.Abilities.Keys);
    }

    [Fact]
    public void UnknownAndDisabledActions_AreRejected()
    {
        var request = BaseRequest();
        request.RotationMode = "custom";
        request.CustomRotation = [Rule("mystery-action")];
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(request));
        request.CustomRotation = [Rule("slam")];
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(request));
    }

    [Fact]
    public void PresetIncompatibleAction_IsRejected()
    {
        var request = BaseRequest();
        request.RotationMode = "custom";
        request.CustomRotation = [Rule("overpower")];
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(request));
    }

    [Fact]
    public void CustomRotationValidation_RejectsEmptyDisabledAndBadThresholds()
    {
        var request = BaseRequest();
        request.RotationMode = "custom";
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(request));
        request.CustomRotation = [new WarriorRotationRule { Action = "bloodthirst", Enabled = false }];
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(request));
        request.CustomRotation = [new WarriorRotationRule { Action = "bloodthirst", MinimumRage = 90m, MaximumRage = 30m }];
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(request));
        request.CustomRotation = [new WarriorRotationRule { Action = "bloodthirst", MinimumAttackPower = -1m }];
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(request));
    }

    [Fact]
    public void BatchOutcomeCounts_ReflectEveryIterationRatherThanOnlyRepresentative()
    {
        var result = new ForeverWarriorSimRunner().Run(BaseRequest());
        var outcomes = result.Batch.Summary.AverageCombatOutcomesByAbility;
        Assert.Contains("main-hand-auto", outcomes.Keys);
        Assert.True(outcomes["main-hand-auto"].AverageDamageOccurrences > 0m);
        Assert.True(outcomes["main-hand-auto"].AverageDirectOccurrences > 0m);
        Assert.All(outcomes.Values, o => Assert.True(o.AverageCriticalDamageOccurrences <= o.AverageDamageOccurrences));
    }

    [Fact]
    public void CustomRotationWithSameSeedsIsDeterministic()
    {
        var request = BaseRequest();
        request.RotationMode = "custom";
        request.CustomRotation = [Rule("bloodthirst"), Rule("whirlwind")];
        var runner = new ForeverWarriorSimRunner();
        Assert.Equal(runner.Run(request).MeanDps, runner.Run(request).MeanDps);
    }
}
}

// ==================== ForeverWarriorEquipmentAndPullTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;

public sealed class ForeverWarriorEquipmentAndPullTests
{
    private static WarriorSimRequest Request() => new()
    {
        Preset = "fury", Iterations = 1, DurationSeconds = 6m,
        ExecutePhaseDurationSeconds = 0m, TargetLevel = 60,
        TargetDodgePercent = 0m, TargetParryPercent = 0m,
        DeepWoundsRank = 0, ImpaleRank = 0, UseBloodrage = false,
        CaptureRepresentativeTimeline = true
    };

    [Fact]
    public void CurrentManualMode_StillProducesUnmodifiedCharacterSheetStats()
    {
        var request = Request();
        var result = new ForeverWarriorSimRunner().Run(request);
        Assert.Equal(request.AttackPower, result.EffectiveCharacterStats[ForeverCombatStatKeys.AttackPower]);
        Assert.Equal(request.MeleeCritPercent, result.EffectiveCharacterStats[ForeverCombatStatKeys.AttackCriticalChancePercent]);
        Assert.Equal("manual", request.EquipmentMode);
    }

    [Fact]
    public void EquipmentAndEnchantBonuses_UseSharedForeverConversions()
    {
        var request = Request();
        request.EquipmentMode = "gear-bonuses";
        request.Gear =
        [
            new WarriorGearEntry
            {
                Slot = "head", ItemKey = "custom-head", Name = "Test helm",
                Stats = new() { [ForeverCombatStatKeys.Strength] = 20m, [ForeverCombatStatKeys.CriticalStrikeRating] = 14m },
                EnchantName = "Test enchant",
                EnchantStats = new() { [ForeverCombatStatKeys.AttackPower] = 20m }
            }
        ];
        var result = new ForeverWarriorSimRunner().Run(request);
        Assert.Equal(1260m, result.EffectiveCharacterStats[ForeverCombatStatKeys.AttackPower]);
        Assert.Equal(request.MeleeCritPercent + 1m, result.EffectiveCharacterStats[ForeverCombatStatKeys.AttackCriticalChancePercent]);
        Assert.Contains(result.StatContributions, contribution => contribution.SourceName == "Test helm");
        Assert.Contains(result.StatContributions, contribution => contribution.SourceName == "Test enchant");
    }

    [Fact]
    public void GearInput_IsIgnoredInManualMode()
    {
        var request = Request();
        request.Gear = [new WarriorGearEntry
        {
            Slot = "head", ItemKey = "test", Name = "Excluded",
            Stats = new() { [ForeverCombatStatKeys.Strength] = 100m }
        }];
        var result = new ForeverWarriorSimRunner().Run(request);
        Assert.Equal(request.AttackPower, result.EffectiveCharacterStats[ForeverCombatStatKeys.AttackPower]);
    }

    [Fact]
    public void GearValidation_RejectsDuplicateSlotsAndUnknownStats()
    {
        var request = Request();
        request.EquipmentMode = "gear-bonuses";
        request.Gear =
        [
            new WarriorGearEntry { Slot = "head", ItemKey = "a" },
            new WarriorGearEntry { Slot = "head", ItemKey = "b" }
        ];
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(request));
        request.Gear = [new WarriorGearEntry { Slot = "head", ItemKey = "a", Stats = new() { ["unknown-stat"] = 20m } }];
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(request));
    }

    [Fact]
    public void CustomPriority_CanRequirePreviousAbilityToBeOnCooldown()
    {
        var request = Request();
        request.RotationMode = "custom";
        request.CustomRotation =
        [
            new WarriorRotationRule { Action = "bloodthirst" },
            new WarriorRotationRule { Action = "whirlwind", OnlyWhenAbilityOnCooldown = "bloodthirst" }
        ];
        var profile = WarriorRotationRules.BuildCustom(request);
        Assert.Contains(profile.Entries[1].Conditions, c =>
            c.ConditionType == RotationConditionTypes.SourceAbilityOnCooldown && c.Key == "bloodthirst");
        Assert.True(new ForeverWarriorSimRunner().Run(request).MeanDps > 0m);
    }

    [Fact]
    public void CooldownCondition_IsFalseBeforeCast_AndTrueWhileRecovering()
    {
        var actor = new SimulationActorState { Key = "player", TeamKey = "raid" };
        actor.AddAbility(new AbilityDefinition { Key = "bloodthirst", Name = "Bloodthirst", CooldownSeconds = 6m });
        var target = new SimulationActorState { Key = "target", TeamKey = "enemy" };
        var context = new SimulationContext(new SimulationRunOptions { DurationSeconds = 20m });
        context.AddActor(actor);
        context.AddActor(target);
        var condition = new RotationConditionDefinition { ConditionType = RotationConditionTypes.SourceAbilityOnCooldown, Key = "bloodthirst" };
        Assert.False(RotationConditionEvaluator.AreSatisfied(context, actor, target, [condition]));
        Assert.True(actor.Abilities["bloodthirst"].ConsumeCharge(0m));
        Assert.True(RotationConditionEvaluator.AreSatisfied(context, actor, target, [condition]));
        Assert.Equal(6m, RotationConditionEvaluator.GetNextKnownEvaluationTime(context, actor, target, [condition]));
    }

    [Fact]
    public void CooldownDependency_RejectsUnavailableOrQueueOnlyAbilities()
    {
        var request = Request();
        request.RotationMode = "custom";
        request.CustomRotation = [new WarriorRotationRule { Action = "whirlwind", OnlyWhenAbilityOnCooldown = "mortal-strike" }];
        // A valid but unavailable dependency from a saved build is kept and
        // skipped instead of blocking the entire encounter.
        WarriorRotationRules.Validate(request);
        Assert.Empty(WarriorRotationRules.BuildCustom(request).Entries);
        Assert.Single(WarriorRotationRules.GetSkippedCooldownDependencies(request));
        request.CustomRotation[0].OnlyWhenAbilityOnCooldown = "heroic-strike";
        Assert.Throws<ArgumentException>(() => WarriorRotationRules.Validate(request));
    }

    [Fact]
    public void BehindTarget_DefaultsToTrue_AndRetainsDodgeStat()
    {
        var request = Request();
        Assert.True(request.BehindTarget);
        Assert.True(request.ImmediateOpeningSwing);
        request.TargetDodgePercent = 5m;
        var result = new ForeverWarriorSimRunner().Run(request);
        Assert.True(result.MeanDps >= 0m);
    }

    [Fact]
    public void OpeningAutoAttack_LandsAtZero_OrWaitsWhenOptedOut()
    {
        var request = Request();
        request.DurationSeconds = 1m;
        request.MeleeHitPercent = 100m;
        request.StartingRage = 0m;
        request.UseHeroicStrike = false;
        var first = new ForeverWarriorSimRunner().Run(request);
        Assert.Contains(first.Batch.RepresentativeRun.Timeline, e =>
            e.Type == CombatEventType.Damage && e.AbilityKey == "main-hand-auto" && e.TimeSeconds == 0m);

        request.ImmediateOpeningSwing = false;
        var delayed = new ForeverWarriorSimRunner().Run(request);
        Assert.DoesNotContain(delayed.Batch.RepresentativeRun.Timeline, e =>
            e.Type == CombatEventType.Damage && e.AbilityKey == "main-hand-auto");
    }

    [Fact]
    public void BehindTarget_RemovesParryEvenAgainstHighFrontalParryChance()
    {
        var request = Request();
        request.Iterations = 12;
        request.TargetParryPercent = 100m;
        request.TargetDodgePercent = 0m;
        var rear = new ForeverWarriorSimRunner().Run(request);
        Assert.DoesNotContain("parry", rear.Batch.Summary.AverageCombatOutcomesByAbility["main-hand-auto"].AverageResultCounts.Keys);
        request.BehindTarget = false;
        var front = new ForeverWarriorSimRunner().Run(request);
        Assert.Contains("parry", front.Batch.Summary.AverageCombatOutcomesByAbility["main-hand-auto"].AverageResultCounts.Keys);
    }
}
}

// ==================== ForeverWarriorEventBudgetTests.cs ====================
namespace WarcraftSim.Tests
{
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;

/// <summary>Regression scenarios based on the two 180s/100-iteration setups
/// that exposed CP116's excessive stance wake-up scheduling.</summary>
public sealed class ForeverWarriorEventBudgetTests
{
    [Theory]
    [InlineData("cp116-slow-arms.json")]
    [InlineData("cp116-slow-fury.json")]
    public void UploadedWarriorPreset_FinishesWithBoundedEvents(string fixtureName)
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureName));
        var request = JsonSerializer.Deserialize<WarriorSimRequest>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(request);

        // Keep all of the user's priorities, talents, gear, and cooldowns,
        // but run only one seeded encounter plus the representative run.
        request.Iterations = 1;
        var result = new ForeverWarriorSimRunner().Run(request);

        Assert.True(result.Batch.RepresentativeRun.ProcessedEventCount > 0);
        Assert.True(result.Batch.RepresentativeRun.ProcessedEventCount < 20_000,
            $"The {fixtureName} encounter processed an unexpectedly large number of events: " +
            result.Batch.RepresentativeRun.ProcessedEventCount);
    }

    [Fact]
    public void InfiniteSameTimestampEvents_FailWithEventBudgetException()
    {
        var context = new SimulationContext(new SimulationRunOptions
        {
            DurationSeconds = 10m, MaximumProcessedEvents = 100
        });
        var engine = new SimulationEngine([new RepeatingDecisionProcessor()]);
        var error = Assert.Throws<SimulationEventLimitExceededException>(() => engine.Run(context));
        Assert.Equal(100L, error.Limit);
        Assert.Equal(0m, error.ElapsedSeconds);
    }

    [Fact]
    public void CancellationBeforeSimulation_IsObserved()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var request = new WarriorSimRequest { Iterations = 1 };
        Assert.Throws<OperationCanceledException>(() =>
            new ForeverWarriorSimRunner().Run(request, source.Token));
    }

    private sealed class RepeatingDecisionProcessor : ICombatEventProcessor
    {
        public void Process(SimulationContext context, CombatEvent combatEvent)
        {
            if (combatEvent.Type is not (CombatEventType.SimulationStarted or CombatEventType.RotationDecision))
                return;
            context.ScheduleEvent(new CombatEvent
            {
                Type = CombatEventType.RotationDecision,
                TimeSeconds = context.CurrentTimeSeconds,
                IsInternal = true
            });
        }
    }
}
}

// ==================== ForeverWarriorRuntimePolicyTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;

public sealed class ForeverWarriorRuntimePolicyTests
{
    private static WarriorSimRequest Base() => new()
    {
        Preset = "arms", TalentMode = "tree", StatSourceMode = "manual",
        DurationSeconds = 20m, ExecutePhaseDurationSeconds = 0m, Iterations = 1,
        StartingRage = 100m, CaptureRepresentativeTimeline = true,
        UseBloodrage = false, UseRend = false, UseExecute = false, UseHeroicStrike = false,
        TargetLevel = 60, TargetDodgePercent = 0m,
    };

    [Theory]
    [InlineData("sunder-armor", 5)]
    [InlineData("battle-shout", 1)]
    [InlineData("demoralizing-shout", 1)]
    [InlineData("thunder-clap", 1)]
    public void UtilityAssignments_CastActualAbilities(string action, int stacks)
    {
        var request = Base();
        if (action == "thunder-clap")
        {
            // A level-60 DPS Arms preview starts in Berserker Stance, which
            // correctly disallows Thunder Clap. Exercise this upkeep cast in
            // Defensive Stance, where it is legal and used by tanks.
            request.Preset = "protection";
            request.TankMode = true;
            request.EnableBossAutoAttacks = true;
            request.ShieldEquipped = true;
            request.IsTwoHanded = false;
        }
        request.MaintenancePolicies = [new WarriorMaintenancePolicy
        {
            AbilityKey = action, DesiredStacks = stacks, RefreshWhenRemainingSeconds = 2m
        }];
        var result = new ForeverWarriorSimRunner().Run(request);
        Assert.Contains(result.Batch.RepresentativeRun!.Timeline,
            item => item.Type == CombatEventType.AbilityCastCompleted && item.AbilityKey == action);
    }

    [Fact]
    public void Sunder_RefreshAndStackingAreNotFreePreAppliedDebuffs()
    {
        var request = Base();
        request.DurationSeconds = 34m;
        request.BossDebuffIds = ["sunder-armor"];
        request.MaintenancePolicies = [new WarriorMaintenancePolicy
        {
            AbilityKey = "sunder-armor", DesiredStacks = 5, RefreshWhenRemainingSeconds = 2m
        }];
        var result = new ForeverWarriorSimRunner().Run(request);
        Assert.Equal(request.TargetArmor, result.EffectiveBossArmor); // static pre-pull reduction excluded
        Assert.Contains(result.Batch.RepresentativeRun!.Timeline,
            item => item.AbilityKey == "sunder-armor" && item.Type == CombatEventType.AuraApplied);
    }

    [Fact]
    public void AssignedBattleShout_RemovesExternalStaticSource()
    {
        var request = Base();
        request.RaidBuffIds = ["battle-shout"];
        request.MaintenancePolicies = [new WarriorMaintenancePolicy { AbilityKey = "battle-shout" }];
        var result = new ForeverWarriorSimRunner().Run(request);
        Assert.True(result.EffectiveCharacterStats["attack-power"] < 1400m);
        Assert.Contains(result.Batch.RepresentativeRun!.Timeline,
            item => item.AbilityKey == "battle-shout" && item.Type == CombatEventType.AbilityCastCompleted);
    }

    [Fact]
    public void OrcBloodFury_FollowsPlannedTime()
    {
        var request = Base();
        request.Race = "orc";
        request.MajorCooldowns = [new WarriorMajorCooldownPolicy
        {
            AbilityKey = "blood-fury", Mode = "at-times", PlannedUseSeconds = [5m]
        }];
        var result = new ForeverWarriorSimRunner().Run(request);
        Assert.Contains(result.Batch.RepresentativeRun!.Timeline,
            item => item.AbilityKey == "blood-fury" && item.Type == CombatEventType.AbilityCastStarted && item.TimeSeconds == 5m);
    }

    [Fact]
    public void OrcBloodFury_HoldsSecondUseForFinalExecuteWindow()
    {
        var request = Base();
        request.Race = "orc";
        request.DurationSeconds = 180m;
        request.MajorCooldowns = [new WarriorMajorCooldownPolicy
        {
            AbilityKey = "blood-fury", OpeningDelaySeconds = 5m,
            HoldLastUse = true, FinalWindowSeconds = 35m
        }];
        var result = new ForeverWarriorSimRunner().Run(request);
        var casts = result.Batch.RepresentativeRun!.Timeline
            .Where(item => item.AbilityKey == "blood-fury" && item.Type == CombatEventType.AbilityCastStarted)
            .Select(item => item.TimeSeconds).ToList();
        Assert.Contains(5m, casts);
        Assert.Contains(145m, casts);
        Assert.DoesNotContain(125m, casts);
    }

    [Fact]
    public void RacialCooldown_RejectsOtherRace()
    {
        var request = Base();
        request.Race = "human";
        request.MajorCooldowns = [new WarriorMajorCooldownPolicy { AbilityKey = "blood-fury" }];
        Assert.Throws<ArgumentException>(() => new ForeverWarriorSimRunner().Run(request));
    }

    [Fact]
    public void DeathWish_RequiresSelectedTalent()
    {
        var request = Base();
        request.MajorCooldowns = [new WarriorMajorCooldownPolicy { AbilityKey = "death-wish" }];
        Assert.Throws<ArgumentException>(() => new ForeverWarriorSimRunner().Run(request));
    }

    [Fact]
    public void SunderMaintenance_RejectsExposeArmor()
    {
        var request = Base();
        request.BossDebuffIds = ["expose-armor"];
        request.MaintenancePolicies = [new WarriorMaintenancePolicy { AbilityKey = "sunder-armor" }];
        Assert.Throws<ArgumentException>(() => new ForeverWarriorSimRunner().Run(request));
    }

    [Fact]
    public void InvalidRefreshWindow_IsRejected()
    {
        var request = Base();
        request.MaintenancePolicies = [new WarriorMaintenancePolicy
        { AbilityKey = "sunder-armor", RefreshWhenRemainingSeconds = 30m }];
        Assert.Throws<ArgumentException>(() => new ForeverWarriorSimRunner().Run(request));
    }
}
}
