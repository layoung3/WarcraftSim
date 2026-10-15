using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests;

public sealed class WarriorWindfuryBloodthirstCp1302Tests
{
    [Fact]
    public void LandedBloodthirstTriggersWindfuryWhenProcChanceIsCertain()
    {
        var request = new WarriorSimRequest
        {
            Preset = "fury", TalentMode = "legacy", EquipmentMode = "manual",
            Iterations = 1, DurationSeconds = 75m, Seed = 1302001,
            CaptureRepresentativeTimeline = true,
            StartingRage = 100m, UseBloodrage = false,
            UseExecute = false, UseHeroicStrike = false,
            TargetDodgePercent = 0m, TargetParryPercent = 0m,
            BehindTarget = true, EnableBossAutoAttacks = false,
            EnableWindfuryTotem = true, WindfuryTotemProcChancePercent = 100m,
            WindfuryTotemInternalCooldownSeconds = 0m
        };
        var timeline = new ForeverWarriorSimRunner().Run(request)
            .Batch.RepresentativeRun.Timeline;

        var bloodthirstHits = timeline.Where(e =>
            e.Type == CombatEventType.Damage && e.AbilityKey == "bloodthirst" &&
            e.Amount is > 0m).ToArray();
        Assert.NotEmpty(bloodthirstHits);

        // The factory still reports Bloodthirst as an unhanded AP-scaled
        // melee special. Its Windfury eligibility is an explicit rule,
        // not a global change to its weapon classification.
        Assert.All(bloodthirstHits, hit => Assert.Null(hit.WeaponHandKey));
        Assert.All(bloodthirstHits, hit =>
            Assert.Contains(timeline, e =>
                e.Type == CombatEventType.Damage &&
                e.AbilityKey == "windfury-totem-extra" &&
                e.TimeSeconds == hit.TimeSeconds));
    }
}
