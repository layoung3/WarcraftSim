using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp168AfflictionNightfallRemoval
{
    [Fact]
    public void EarlyShadowTranceRemovalRevokesInstantShadowBolt()
    {
        var context = new SimulationContext(new SimulationRunOptions { DurationSeconds = 30m });
        var owner = new SimulationActorState { Key = "warlock", TeamKey = "player", Name = "Warlock" };
        var enemy = new SimulationActorState { Key = "boss", TeamKey = "enemy", Name = "Boss" };
        context.AddActor(owner);
        context.AddActor(enemy);
        var auras = new AuraManager();
        var decimation = new WarlockDecimationProcessor(owner, auras, 0);
        var nightfall = new WarlockNightfallProcessor(owner, auras, decimation, 2, testChance: 100m);
        var bolt = new AbilityDefinition { Key = "shadow-bolt", CastTimeSeconds = 3m };

        Assert.Equal(3m, nightfall.Resolve(context, owner, bolt).CastTimeSeconds);
        nightfall.Process(context, new CombatEvent {
            Type = CombatEventType.Damage,
            SourceActorKey = "warlock", TargetActorKey = "boss",
            AbilityKey = "corruption", ResultKey = "hit", Amount = 100m
        });
        Assert.Equal(1, nightfall.Procs);
        Assert.Equal(0m, nightfall.Resolve(context, owner, bolt).CastTimeSeconds);

        // Aura manager emits a real removal event for dispels/forced removals,
        // before the nominal ten-second proc expiry.
        Assert.Equal(1, auras.RemoveAuras(context, owner,
            "warlock-shadow-trance", "warlock", "test dispel"));
        nightfall.Process(context, new CombatEvent {
            Type = CombatEventType.AuraRemoved,
            SourceActorKey = "warlock", TargetActorKey = "warlock",
            AbilityKey = "nightfall"
        });
        Assert.Equal(3m, nightfall.Resolve(context, owner, bolt).CastTimeSeconds);
        Assert.Equal(0, nightfall.Consumed);
    }
}
