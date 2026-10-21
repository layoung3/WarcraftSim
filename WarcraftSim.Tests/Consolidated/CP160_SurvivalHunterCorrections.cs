using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Hunter;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp160SurvivalHunterCorrectionsTests
{
    private static SurvivalHunterSimRequest Setup(string mode = "melee", int targets = 0)
    {
        return new SurvivalHunterSimRequest {
            CombatMode = mode, DurationSeconds = 120m, SwitchAtSeconds = 60m,
            AdditionalTargets = targets, TargetMaximumHealth = 1000000m,
            UseFireTraps = false, UseFrostTraps = false,
            ScriptedAvoidanceEverySeconds = 12m,
            Tuning = new ForeverSurvivalHunterTuning {
                MaximumMana = 24000m, BaseMana = 4200m, HitChancePercent = 100m,
                TargetDodgePercent = 0m, TargetParryPercent = 0m
            }
        };
    }

    [Fact]
    public void RapidFireIsSharedWithMarksmanshipAndCostsMana()
    {
        var shared = ForeverHunterSharedAbilities.CreateRapidFire();
        var survival = ForeverSurvivalHunterSpellbook.Create(new(), new())
            .Single(x => x.Key == "rapid-fire");
        Assert.Equal(100m, shared.ResourceCosts.Single().Amount);
        Assert.Equal(shared.ResourceCosts.Single().Amount, survival.ResourceCosts.Single().Amount);
        Assert.Equal(300m, survival.CooldownSeconds);
        Assert.True(survival.IsOffGlobalCooldown);
        Assert.Equal(15m, survival.Effects.Single().DurationSeconds);
    }

    [Theory]
    [InlineData("melee")]
    [InlineData("ranged")]
    [InlineData("weave")]
    public void RapidFireIsCastOnceInBothAttackModes(string mode)
    {
        var result = new SurvivalHunterSimRunner().Run(Setup(mode));
        Assert.Equal(1m, result.Run.CustomMetrics["survival-rapid-fire-casts"]);
        Assert.Equal(1, result.Run.Summary.Abilities["rapid-fire"].CastCompletedCount);
        Assert.Contains(result.Run.Timeline, e => e.AbilityKey == "rapid-fire" &&
            e.Type == CombatEventType.ResourceChanged && e.Amount == -100m);
    }

    [Fact]
    public void AdaptiveModeReturnsToMeleeAfterEachRangedExcursion()
    {
        var request = Setup("weave");
        request.WeaveMinimumMeleeSeconds = 16m;
        var response = new SurvivalHunterSimRunner().Run(request);
        Assert.True(response.Run.CustomMetrics["survival-weave-excursions"] >= 1m);
        var shots = response.Run.Timeline.Where(e =>
            e.AbilityKey == "auto-shot" && e.Type == CombatEventType.Damage).ToArray();
        Assert.NotEmpty(shots);
        Assert.Contains(response.Run.Timeline, e => e.AbilityKey == "melee-auto" &&
            e.Type == CombatEventType.Damage && e.TimeSeconds > shots[0].TimeSeconds + 5m);
        Assert.True(response.MeleeDamage > response.RangedDamage);
    }

    [Fact]
    public void SingleTargetRangedActuallyUsesAimedShot()
    {
        var response = new SurvivalHunterSimRunner().Run(Setup("ranged"));
        Assert.True(response.Run.Summary.Abilities["aimed-shot"].CastCompletedCount > 0);
        Assert.False(response.Run.Summary.Abilities.ContainsKey("multi-shot") &&
            response.Run.Summary.Abilities["multi-shot"].CastCompletedCount > 0);
    }

    [Fact]
    public void TrapsRequireMeleeProximityUnlessRangedTriggerIsExplicit()
    {
        var request = Setup("ranged");
        request.UseFireTraps = true;
        request.UseFrostTraps = true;
        request.TriggerRangedTraps = false;
        var noProximity = new SurvivalHunterSimRunner().Run(request);
        Assert.Equal(0m, noProximity.TrapDamage);
        Assert.Equal(0m, noProximity.Run.CustomMetrics["survival-trap-triggers"]);
        request.TriggerRangedTraps = true;
        var withProximity = new SurvivalHunterSimRunner().Run(request);
        Assert.True(withProximity.TrapDamage > 0m);
    }

    [Fact]
    public void EntrapmentHonorsBossImmunityAndControlDiminishingReturns()
    {
        var request = Setup("melee", 2);
        request.UseFireTraps = true;
        request.UseFrostTraps = true;
        request.Talents.Ranks = new Dictionary<string,int> {
            ["entrapment"] = 5, ["improved-tracking"] = 5
        };
        var run = new SurvivalHunterSimRunner().Run(request);
        Assert.DoesNotContain(run.Run.Timeline, e => e.AbilityKey == "entrapment" &&
            e.TargetActorKey == "target" && e.Type == CombatEventType.AuraApplied);
        Assert.Contains(run.Run.Timeline, e => e.AbilityKey == "entrapment" &&
            e.TargetActorKey == "add-1" && e.Type == CombatEventType.AuraApplied);
    }

    [Fact]
    public void LaceratingStrikesIsNotRetrackedAfterMongooseHit()
    {
        // Full talent selection matches the live preset's prerequisites.
        var request = Setup("melee");
        request.TargetIsTracked = true;
        request.Talents.Ranks = new Dictionary<string,int> {
            ["improved-tracking"] = 5, ["deflection"] = 5,
            ["entrapment"] = 5, ["survivalist"] = 5,
            ["clever-traps"] = 2, ["surefooted"] = 3,
            ["deterrence"] = 1, ["predators-edge"] = 5,
            ["resourcefulness"] = 2, ["expose-prey"] = 2,
            ["strider-kick"] = 1, ["lightning-reflexes"] = 5,
            ["lacerating-strikes"] = 1, ["survivalists-discipline"] = 2,
            ["survival-tactics"] = 2, ["savage-strikes"] = 2,
            ["counterattack"] = 1, ["improved-wing-clip"] = 1
        };
        // Keep only 51 points while retaining all prerequisites.
        request.Talents.Ranks["entrapment"] = 2;
        request.Talents.Ranks["survivalist"] = 3;
        request.Talents.Ranks["deflection"] = 3;
        request.Talents.Validate();
        var response = new SurvivalHunterSimRunner().Run(request);
        var mongoose = response.Run.Timeline.First(e => e.AbilityKey == "mongoose-bite" &&
            e.Type == CombatEventType.Damage && e.Amount > 0m);
        var tick = response.Run.Timeline.First(e => e.AbilityKey == "lacerating-strikes" &&
            e.Type == CombatEventType.Damage && e.TimeSeconds > mongoose.TimeSeconds && !e.IsCritical);
        Assert.Equal(mongoose.Amount!.Value * .40m / 7m, tick.Amount!.Value);
    }
}
