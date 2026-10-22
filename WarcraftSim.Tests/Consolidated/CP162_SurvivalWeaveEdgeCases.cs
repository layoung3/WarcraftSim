using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Hunter;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp162SurvivalWeaveEdgeCasesTests
{
    private static SurvivalHunterSimRequest DefaultWeave(int additionalTargets = 0) => new()
    {
        AdditionalTargets = additionalTargets,
        CombatMode = "weave",
        DurationSeconds = 120m,
        Seed = 159001,
        TargetMaximumHealth = 1000000m,
        Talents = new ForeverSurvivalHunterTalents
        {
            Ranks = new Dictionary<string, int>
            {
                ["improved-tracking"] = 4, ["deflection"] = 5,
                ["entrapment"] = 5, ["savage-strikes"] = 2,
                ["survivalist"] = 5, ["improved-wing-clip"] = 3,
                ["clever-traps"] = 2, ["surefooted"] = 3,
                ["deterrence"] = 1, ["survival-tactics"] = 2,
                ["predators-edge"] = 5, ["counterattack"] = 1,
                ["resourcefulness"] = 2, ["expose-prey"] = 2,
                ["survivalists-discipline"] = 2, ["strider-kick"] = 1,
                ["lightning-reflexes"] = 5, ["lacerating-strikes"] = 1
            }
        }
    };

    [Fact]
    public void TrapPlacedJustBeforeWeaveStillTriggers()
    {
        var request = DefaultWeave();
        var before = SimulationInputTimingScope.AdditionalDelaySeconds;
        try
        {
            SimulationInputTimingScope.AdditionalDelaySeconds = .08m;
            var result = new SurvivalHunterSimRunner().Run(request);
            var casts = result.Run.Timeline.Where(e =>
                e.Type == CombatEventType.AbilityCastCompleted &&
                e.AbilityKey == "explosive-trap").ToArray();
            Assert.Equal(4, casts.Length);
            // A trap completed during the third melee-to-ranged transition.
            Assert.Contains(casts, e => e.TimeSeconds == 67m);
            // All four placements occurred in melee and should trigger.
            Assert.Equal((decimal)casts.Length, result.Run.CustomMetrics["survival-trap-triggers"]);
            Assert.Contains(result.Run.Timeline, e =>
                e.Type == CombatEventType.Damage &&
                e.AbilityKey == "explosive-trap" && e.TimeSeconds == 67.25m);
        }
        finally
        {
            SimulationInputTimingScope.AdditionalDelaySeconds = before;
        }
    }

    [Fact]
    public void CleaveCanWeaveForMultiShotWithAimedShotDisabled()
    {
        var request = DefaultWeave(2);
        request.UseAimedShot = false;
        request.UseMultiShot = true;
        var result = new SurvivalHunterSimRunner().Run(request);
        Assert.True(result.Run.CustomMetrics["survival-weave-excursions"] > 0m);
        Assert.True(result.Run.Summary.Abilities.GetValueOrDefault("multi-shot")?.CastCompletedCount > 0);
        Assert.False(result.Run.Summary.Abilities.GetValueOrDefault("aimed-shot")?.CastCompletedCount > 0);
        Assert.Contains(result.Run.Timeline, e =>
            e.Type == CombatEventType.Damage && e.AbilityKey == "melee-auto" &&
            e.TimeSeconds > 25m);
    }
}
