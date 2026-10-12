using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Tests;

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
