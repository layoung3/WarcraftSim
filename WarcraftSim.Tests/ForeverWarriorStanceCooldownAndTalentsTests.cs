using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Abilities.Warrior;

namespace WarcraftSim.Tests;

public sealed class ForeverWarriorStanceCooldownAndTalentsTests
{
    [Theory]
    [InlineData("death-wish", false, 1.5)]
    [InlineData("recklessness", false, 1.5)]
    [InlineData("blood-fury", true, 0)]
    [InlineData("berserking", true, 0)]
    public void BurstActions_RespectTheirIndividualGlobalCooldowns(
        string abilityKey, bool offGlobal, double gcdSeconds)
    {
        var request = new WarriorSimRequest
        {
            MajorCooldowns = [new WarriorMajorCooldownPolicy { AbilityKey = abilityKey }]
        };
        var ability = Assert.Single(WarriorRuntimeActionProcessor.CreateAbilities(request));
        Assert.Equal(offGlobal, ability.IsOffGlobalCooldown);
        Assert.Equal((decimal)gcdSeconds, ability.GlobalCooldownSeconds);
    }

    [Theory]
    [InlineData("sunder-armor", "improved-sunder-armor", 3, 12)]
    [InlineData("thunder-clap", "improved-thunder-clap", 3, 14)]
    [InlineData("battle-shout", "booming-voice", 5, 7.5)]
    [InlineData("demoralizing-shout", "booming-voice", 5, 7.5)]
    public void UtilityTalents_ModifyTheRealCastRageCosts(
        string abilityKey, string talent, int rank, double expected)
    {
        var request = new WarriorSimRequest
        {
            TalentMode = "tree",
            TalentRanks = new Dictionary<string, int> { [talent] = rank },
            MaintenancePolicies = [new WarriorMaintenancePolicy { AbilityKey = abilityKey }]
        };
        var ability = Assert.Single(WarriorRuntimeActionProcessor.CreateAbilities(request));
        Assert.Equal((decimal)expected, Assert.Single(ability.ResourceCosts).Amount);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(3, 19)]
    [InlineData(5, 25)]
    public void StanceSwitches_RetainBaselinePlusTacticalMasteryRage(int rank, int expected)
    {
        var ability = ForeverWarriorStanceFactory.Create("battle-stance", rank, 10m);
        Assert.Equal((decimal)expected, ability.Effects[0].MinimumValue);
        Assert.Equal((decimal)expected, ability.Effects[0].MaximumValue);
        Assert.Contains("battle-stance", ability.ForbiddenSourceAuraKeys);
        Assert.Contains(ability.Effects, e => e.Key == "remove-berserker-stance");
        Assert.Contains(ability.Effects, e => e.Key == "activate-stance");
    }

    [Fact]
    public void FuryCustomPriority_CanIncludeOverpowerOnlyWithEnabledDance()
    {
        var request = new WarriorSimRequest
        {
            Preset = "fury", RotationMode = "custom", EnableStanceDancing = true,
            CustomRotation = [new WarriorRotationRule { Action = "overpower" }]
        };
        WarriorRotationRules.Validate(request);
        request.EnableStanceDancing = false;
        Assert.Throws<ArgumentException>(() => WarriorRotationRules.Validate(request));
    }

    [Fact]
    public void InvalidStanceDanceRageLimit_IsRejected()
    {
        var request = new WarriorSimRequest { MaximumRageForOverpowerDance = 110m };
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(request));
    }

    [Fact]
    public void WeaponmasterAxe_ContributesCritWhenEquippedWeaponTypeMatches()
    {
        var request = new WarriorSimRequest
        {
            TalentMode = "tree", EquipmentMode = "manual", StatSourceMode = "manual",
            MainHandWeaponType = "axe", TalentRanks = new Dictionary<string, int>
            {
                ["weaponmaster"] = 3
            }
        };
        var axe = WarriorGearStatCalculator.Calculate(request).EffectiveStats.Get("attack-critical-chance-percent");
        request.MainHandWeaponType = "sword";
        var sword = WarriorGearStatCalculator.Calculate(request).EffectiveStats.Get("attack-critical-chance-percent");
        Assert.Equal(3m, axe - sword);
    }

    [Fact]
    public void Fury_WithStanceDancing_PreservesBloodthirstInDefaultPriority()
    {
        // In legacy mode Bloodthirst is available without a talent allocation.
        // Enabling stance dancing must add Overpower, not swap to the Arms
        // ability registration branch and remove the Fury core.
        var request = new WarriorSimRequest
        {
            Preset = "fury", TalentMode = "legacy", EquipmentMode = "manual",
            StatSourceMode = "manual", EnableStanceDancing = true,
            DurationSeconds = 6m, ExecutePhaseDurationSeconds = 0m,
            StartingRage = 100m, Iterations = 1, Seed = 12345,
            TargetLevel = 60, TargetDodgePercent = 0m,
            UseBloodrage = false, UseRend = false, UseExecute = false,
            UseHeroicStrike = false, CaptureRepresentativeTimeline = true
        };

        var result = new ForeverWarriorSimRunner().Run(request);
        Assert.Contains(result.Batch.RepresentativeRun!.Timeline, e =>
            e.Type == CombatEventType.AbilityCastStarted && e.AbilityKey == "bloodthirst");
    }

    [Fact]
    public void Fury_GrantedOverpowerOpportunity_TriggersRealStanceSwitch()
    {
        var request = new WarriorSimRequest
        {
            Preset = "fury", TalentMode = "tree", EquipmentMode = "manual",
            StatSourceMode = "manual", EnableStanceDancing = true,
            MaximumRageForOverpowerDance = 20m,
            DurationSeconds = 40m, ExecutePhaseDurationSeconds = 0m,
            StartingRage = 15m, Iterations = 1, Seed = 12345,
            TargetLevel = 60, TargetDodgePercent = 100m,
            UseBloodrage = false, UseRend = false, UseExecute = false,
            UseHeroicStrike = false, CaptureRepresentativeTimeline = true
        };
        var result = new ForeverWarriorSimRunner().Run(request);
        Assert.Contains(result.Batch.RepresentativeRun!.Timeline, e =>
            e.Type == CombatEventType.AbilityCastStarted && e.AbilityKey == "enter-battle-stance");
    }
}
