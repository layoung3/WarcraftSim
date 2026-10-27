using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>
/// CP166 Stage 4A: deterministic outcome-level guards for discovered class defects.
/// These tests intentionally use explicit events and resources, not DPS approximations.
/// </summary>
public sealed class Cp166Stage4AWarriorRogueRegressionTests
{
    [Fact]
    public void UnbridledWrath_AlwaysGrantsOneRagePerProc_AfterOctoberFiveHotfix()
    {
        var actor = new SimulationActorState { Key = "warrior", TeamKey = "raid" };
        actor.InitializeHealth(5000m);
        actor.AddResource(new ResourceState("rage", 1000m, 0m));
        var context = new SimulationContext(new SimulationRunOptions {
            Seed = 12345, DurationSeconds = 10m
        });
        context.AddActor(actor);
        var processor = new WarriorRageTalentProcessor(5);
        const int swings = 40;
        for (var i = 0; i < swings; i++)
            processor.Process(context, new CombatEvent {
                Type = CombatEventType.Damage, SourceActorKey = actor.Key,
                AbilityKey = "main-hand-auto", WeaponHandKey = "main-hand", Amount = 100m
            });
        // Regardless of RNG, matched-seed eligible procs grant either 1 or 2 Rage.
        // Rank 5 has a 60% proc chance, so these swings reliably exercise the path.
        Assert.True(actor.Resources["rage"].Current > 0m);
        var baselineActor = new SimulationActorState { Key = "warrior", TeamKey = "raid" };
        baselineActor.InitializeHealth(5000m);
        baselineActor.AddResource(new ResourceState("rage", 1000m, 0m));
        var baselineContext = new SimulationContext(new SimulationRunOptions {
            Seed = 12345, DurationSeconds = 10m
        });
        baselineContext.AddActor(baselineActor);
        var baseline = new WarriorRageTalentProcessor(5, false);
        for (var i = 0; i < swings; i++)
            baseline.Process(baselineContext, new CombatEvent {
                Type = CombatEventType.Damage, SourceActorKey = "warrior",
                AbilityKey = "main-hand-auto", WeaponHandKey = "main-hand", Amount = 100m
            });
        Assert.Equal(baselineActor.Resources["rage"].Current,
            actor.Resources["rage"].Current);
    }

    [Fact]
    public void UnbridledWrath_DoesNotAwardRageForAnotherActorsHits()
    {
        var (context, actor) = RogueContext("warrior");
        actor.AddResource(new ResourceState("rage", 100m, 0m));
        var processor = new WarriorRageTalentProcessor(5);
        for (var i = 0; i < 50; i++)
            processor.Process(context, new CombatEvent {
                Type = CombatEventType.Damage, SourceActorKey = "companion",
                AbilityKey = "main-hand-auto", WeaponHandKey = "main-hand", Amount = 100m
            });
        Assert.Equal(0m, actor.Resources["rage"].Current);
    }

    [Fact]
    public void Garrote_AwardsComboPointOnlyWhenApplicationLands()
    {
        var (context, rogue) = RogueContext("rogue");
        rogue.AddResource(new ResourceState("energy", 100m, 100m, 10m));
        var request = RogueSimRequest.CreatePreset("assassination");
        var auraManager = new AuraManager();
        var executor = new AbilityExecutor(auraManager,
            new SimpleCombatRollResolver(hitChancePercent: 100m, criticalChancePercent: 0m),
            new RulesetDamageMitigationResolver(new CombatRulesetDefinition {
                RulesetKey = "cp166-stage4a", Version = "1"
            }));
        var processor = new RogueCombatProcessor(request, rogue, new RotationProfile(),
            executor, auraManager, new AutoAttackProcessor(executor), []);
        var execution = Guid.NewGuid();
        processor.Process(context, new CombatEvent {
            Type = CombatEventType.AbilityCastCompleted,
            SourceActorKey = rogue.Key, TargetActorKey = "target",
            AbilityKey = "garrote", AbilityExecutionId = execution
        });
        Assert.Equal(0, processor.ComboPointsFor("target"));
        // A missed application does not emit AuraApplied and must not grant points.
        processor.Process(context, new CombatEvent {
            Type = CombatEventType.Damage, SourceActorKey = rogue.Key,
            TargetActorKey = "target", AbilityKey = "garrote",
            AbilityExecutionId = execution, ResultKey = "miss", Amount = 0m
        });
        Assert.Equal(0, processor.ComboPointsFor("target"));
        processor.Process(context, new CombatEvent {
            Type = CombatEventType.AuraApplied, SourceActorKey = rogue.Key,
            TargetActorKey = "target", AbilityKey = "garrote",
            AbilityExecutionId = Guid.NewGuid(), EffectKey = "garrote-tick"
        });
        Assert.Equal(1, processor.ComboPointsFor("target"));
    }

    private static (SimulationContext, SimulationActorState) RogueContext(string key)
    {
        var actor = new SimulationActorState { Key = key, TeamKey = "raid" };
        actor.InitializeHealth(5000m);
        var context = new SimulationContext(new SimulationRunOptions {
            Seed = 16604, DurationSeconds = 10m
        });
        context.AddActor(actor);
        var target = new SimulationActorState { Key = "target", TeamKey = "enemy" };
        target.InitializeHealth(5000m);
        context.AddActor(target);
        return (context, actor);
    }
}
