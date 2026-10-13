using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Encounters;

namespace WarcraftSim.Tests;

/// <summary>Multi-target encounter regression coverage in the consolidated suite.</summary>
public sealed class WarriorMultiTargetTests
{
    private static WarriorSimRequest Request(params WarriorAdditionalTarget[] adds) => new()
    {
        Preset = "fury", TalentMode = "legacy", RotationMode = "custom", Iterations = 1,
        DurationSeconds = 25m, ExecutePhaseDurationSeconds = 0m,
        CaptureRepresentativeTimeline = true, StartingRage = 100m,
        TargetLevel = 60, TargetDodgePercent = 0m, TargetParryPercent = 0m,
        MeleeHitPercent = 100m, MeleeCritPercent = 0m, TargetArmor = 0m,
        DeepWoundsRank = 0, ImpaleRank = 0, UseBloodrage = false,
        UseHeroicStrike = false, UseRend = false, UseExecute = false,
        AdditionalTargets = [..adds],
        CustomRotation = [new WarriorRotationRule { Action = "whirlwind" }]
    };

    private static WarriorAdditionalTarget Add(string key = "add-1", decimal from = 0m, decimal until = 25m) =>
        new() { Key = key, Name = key, SpawnTimeSeconds = from, DespawnTimeSeconds = until,
            Armor = 0m, DodgePercent = 0m, Level = 60 };

    private static List<CombatEvent> Damage(WarriorSimResponse response, string ability) =>
        response.Batch.RepresentativeRun.Timeline.Where(e =>
            e.Type == CombatEventType.Damage && e.AbilityKey == ability && (e.Amount ?? 0m) > 0m).ToList();

    [Fact]
    public void SingleTarget_WhirlwindCannotInventExtraEnemies()
    {
        var result = new ForeverWarriorSimRunner().Run(Request());
        var hits = Damage(result, "whirlwind");
        Assert.NotEmpty(hits);
        Assert.All(hits, hit => Assert.Equal("target", hit.TargetActorKey));
    }

    [Fact]
    public void WhirlwindHitsBossAndTwoScriptedAdds_WithoutGoingOverTargetLimit()
    {
        var result = new ForeverWarriorSimRunner().Run(Request(Add("add-1"), Add("add-2")));
        var hits = Damage(result, "whirlwind");
        Assert.Contains(hits, e => e.TargetActorKey == "target");
        Assert.Contains(hits, e => e.TargetActorKey == "add-1");
        Assert.Contains(hits, e => e.TargetActorKey == "add-2");
        Assert.True(result.Batch.RepresentativeRun.ProcessedEventCount < 20_000);
    }

    [Fact]
    public void AddSpawnAndDespawnLimitItsDamageWindow()
    {
        var result = new ForeverWarriorSimRunner().Run(Request(Add("add-1", 6m, 16m)));
        var damage = Damage(result, "whirlwind").Where(e => e.TargetActorKey == "add-1").ToList();
        Assert.NotEmpty(damage);
        Assert.All(damage, e => Assert.InRange(e.TimeSeconds, 6m, 16m));
        Assert.DoesNotContain(damage, e => e.TimeSeconds >= 16m);
    }

    [Fact]
    public void CleaveHitsNearbySecondTarget_NotUnlimitedAdds()
    {
        var request = Request(Add("add-1"), Add("add-2"));
        request.UseCleave = true;
        request.CustomRotation = [new WarriorRotationRule { Action = "cleave", MinimumActiveEnemies = 2 }];
        var result = new ForeverWarriorSimRunner().Run(request);
        var hits = Damage(result, "cleave");
        Assert.NotEmpty(hits);
        Assert.Contains(hits, e => e.TargetActorKey == "target");
        Assert.Contains(hits, e => e.TargetActorKey == "add-1");
        Assert.DoesNotContain(hits, e => e.TargetActorKey == "add-2");
    }

    [Fact]
    public void MinimumActiveEnemiesConditionDisablesWhirlwindWhenNoAddsExist()
    {
        var request = Request();
        request.CustomRotation = [new WarriorRotationRule { Action = "whirlwind", MinimumActiveEnemies = 2 }];
        var result = new ForeverWarriorSimRunner().Run(request);
        Assert.Empty(Damage(result, "whirlwind"));
    }

    [Fact]
    public void MultiTargetConfigurationRejectsInvalidWindowsAndDuplicateKeys()
    {
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(
            Request(Add("add-1", 15m, 10m))));
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(
            Request(Add("add-1"), Add("add-1"))));
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(
            Request(Add("add-1"), Add("add-2"), Add("add-3"), Add("add-4"))));
    }

    [Fact]
    public void SweepingStrikes_EchoesAtMostFiveEligibleHitsToOneNearbyEnemy()
    {
        var encounter = new EncounterProfile
        {
            TargetProximityLinks = [new TargetProximityLink
            {
                TargetAKey = "target", TargetBKey = "add-1", InCleaveRange = true
            }]
        };
        var context = new SimulationContext(new SimulationRunOptions
        {
            DurationSeconds = 30m, PrimaryActorKey = "warrior"
        }, encounter);
        var warrior = new SimulationActorState { Key = "warrior", TeamKey = "raid" };
        warrior.InitializeHealth(1000m);
        var boss = new SimulationActorState { Key = "target", TeamKey = "enemy" };
        boss.InitializeHealth(1000m);
        var add = new SimulationActorState { Key = "add-1", TeamKey = "enemy" };
        add.InitializeHealth(1000m);
        context.AddActor(warrior); context.AddActor(boss); context.AddActor(add);
        var processor = new WarriorSweepingStrikesProcessor();
        processor.Process(context, new CombatEvent
        {
            Type = CombatEventType.AbilityCastCompleted,
            AbilityKey = "sweeping-strikes", SourceActorKey = "warrior"
        });
        for (var i = 0; i < 6; i++)
            processor.Process(context, new CombatEvent
            {
                Type = CombatEventType.Damage,
                AbilityKey = "main-hand-auto", SourceActorKey = "warrior",
                TargetActorKey = "target", WeaponHandKey = "main-hand",
                AbilityExecutionId = Guid.NewGuid(), Amount = 10m
            });
        Assert.Equal(950m, add.CurrentHealth);
    }

    [Fact]
    public void MultiTargetDamageIsIncludedInDpsSummary()
    {
        var result = new ForeverWarriorSimRunner().Run(Request(Add()));
        var run = result.Batch.RepresentativeRun;
        Assert.True(run.Summary.ActorSummaries["add-1"].DamageTaken > 0m);
        Assert.True(run.Summary.ActorSummaries["target"].DamageTaken > 0m);
        // The separately accumulated actor summaries and total damage can differ
        // at the last decimal place because decimal addition rounds after each step.
        // Preserve a strict, sub-nanodamage tolerance without requiring bit-for-bit
        // equality of differently grouped accumulations.
        var damageAcrossTargets =
            run.Summary.ActorSummaries["target"].DamageTaken +
            run.Summary.ActorSummaries["add-1"].DamageTaken;
        Assert.InRange(
            Math.Abs(run.Summary.DamageDone - damageAcrossTargets),
            0m,
            0.000000001m);
    }
}
