using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp161SharedLatencyAndSurvivalDefaultsTests
{
    [Fact]
    public void SurvivalDefaultsToMeleeFirstWeavingWithTravelAndNoControlOnlyTraps()
    {
        var setup = new SurvivalHunterSimRequest();
        Assert.Equal("weave", setup.CombatMode);
        Assert.True(setup.WeaveTravelSeconds > 0m);
        Assert.False(setup.UseFrostTraps);
        Assert.False(setup.UseWingClip);
        Assert.True(setup.UseRapidFire);
    }

    [Fact]
    public void ScopedInputDelayIsAdditiveAndLeavesEnemyTimingUnchanged()
    {
        var options = new SimulationRunOptions
        {
            Seed = 1, DurationSeconds = 1m, PrimaryActorKey = "hunter"
        };
        var context = new SimulationContext(options);
        var hunter = new SimulationActorState
        {
            Key = "hunter", TeamKey = "raid", MaximumHealth = 100m
        };
        hunter.InitializeHealth(100m);
        hunter.ConfigureActionTiming(0m, .04m);
        var enemy = new SimulationActorState
        {
            Key = "boss", TeamKey = "enemy", MaximumHealth = 100m
        };
        enemy.InitializeHealth(100m);
        enemy.ConfigureActionTiming(0m, .025m);
        context.AddActor(hunter);
        context.AddActor(enemy);

        var before = SimulationInputTimingScope.AdditionalDelaySeconds;
        try
        {
            SimulationInputTimingScope.AdditionalDelaySeconds = .08m;
            new SimulationEngine().Run(context);
        }
        finally
        {
            SimulationInputTimingScope.AdditionalDelaySeconds = before;
        }
        Assert.Equal(.12m, hunter.InputDelaySeconds);
        Assert.Equal(.025m, enemy.InputDelaySeconds);
    }
}
