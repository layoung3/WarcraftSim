using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Tests;

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
        Assert.Throws<ArgumentException>(() => WarriorRotationRules.Validate(request));
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
