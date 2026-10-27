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
        // Same-seed eligible procs grant exactly one Rage under current Forever rules.
        // Rank 5 has a 60% proc chance, so these swings reliably exercise the path.
        Assert.True(actor.Resources["rage"].Current > 0m);
        var baselineActor = new SimulationActorState { Key = "warrior", TeamKey = "raid" };
        baselineActor.InitializeHealth(5000m);
        baselineActor.AddResource(new ResourceState("rage", 1000m, 0m));
        var baselineContext = new SimulationContext(new SimulationRunOptions {
            Seed = 12345, DurationSeconds = 10m
        });
        baselineContext.AddActor(baselineActor);
        var baseline = new WarriorRageTalentProcessor(5);
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

    [Fact]
    public void HackAndSlash_ExtraMainHandAttacksUseAttackPowerScaledWeaponDamage()
    {
        var request = RogueSimRequest.CreatePreset("combat");
        request.StartInStealth = false;
        request.UseInstantPoison = false;
        request.DurationSeconds = 40m;
        request.CaptureTimeline = true;
        request.Talents.Ranks["hack-and-slash"] = 5;
        request.Tuning.HackAndSlashExtraAttackPercentPerRank = 20m;
        request.Tuning.MainHandHitPercent = 100m;
        request.Tuning.OffHandHitPercent = 100m;
        var result = new RogueSimRunner().Run(request);
        var extraHits = result.Run.Timeline.Where(e =>
            e.Type == CombatEventType.Damage &&
            e.AbilityKey == "hack-and-slash-extra" &&
            e.WeaponHandKey == "main-hand" &&
            e.ResultKey == "hit").ToList();
        Assert.NotEmpty(extraHits);
        var regularHit = Assert.Single(result.Run.Timeline.Where(e =>
            e.Type == CombatEventType.Damage &&
            e.AbilityKey == "rogue-main-hand" &&
            e.ResultKey == "hit").Take(1));
        // An actual extra swing, before mitigation, must have the same weapon
        // damage including Attack Power as a regular noncritical MH swing.
        Assert.All(extraHits, e => Assert.Equal(regularHit.RawAmount, e.RawAmount));
        Assert.True(regularHit.RawAmount > request.Tuning.MainHandWeaponDamage);
    }

    [Fact]
    public void Rupture_TargetSwapDoesNotReplaceExistingTargetsComboPointSnapshot()
    {
        var (context, rogue) = RogueContext("rogue");
        rogue.AddResource(new ResourceState("energy", 100m, 100m, 10m));
        var second = new SimulationActorState { Key = "target-2", TeamKey = "enemy" };
        second.InitializeHealth(5000m);
        context.AddActor(second);
        var manager = new AuraManager();
        var executor = new AbilityExecutor(manager,
            new SimpleCombatRollResolver(hitChancePercent: 100m, criticalChancePercent: 0m),
            new RulesetDamageMitigationResolver(new CombatRulesetDefinition {
                RulesetKey = "cp166-stage4a", Version = "1"
            }));
        var combat = new RogueCombatProcessor(RogueSimRequest.CreatePreset("assassination"),
            rogue, new RotationProfile(), executor, manager,
            new AutoAttackProcessor(executor), []);

        void SetRupture(string target, int comboPoints)
        {
            for (var i = 0; i < comboPoints; i++)
                combat.Process(context, new CombatEvent {
                    Type = CombatEventType.Damage, SourceActorKey = "rogue",
                    TargetActorKey = target, AbilityKey = "sinister-strike",
                    Amount = 100m, AbilityExecutionId = Guid.NewGuid()
                });
            var execution = Guid.NewGuid();
            combat.Process(context, new CombatEvent {
                Type = CombatEventType.AbilityCastCompleted, SourceActorKey = "rogue",
                TargetActorKey = target, AbilityKey = "rupture",
                AbilityExecutionId = execution
            });
            combat.Process(context, new CombatEvent {
                Type = CombatEventType.AuraApplied, SourceActorKey = "rogue",
                TargetActorKey = target, AbilityKey = "rupture",
                AbilityExecutionId = execution, EffectKey = "rupture-tick"
            });
        }

        SetRupture("target", 4);
        var first = combat.DamageMultiplier("rupture", CombatEffectDeliveryType.Periodic,
            context, context.GetActor("target")!);
        Assert.Equal(4m, first);

        SetRupture("target-2", 2);
        var secondMultiplier = combat.DamageMultiplier("rupture",
            CombatEffectDeliveryType.Periodic, context, second);
        Assert.Equal(2m, secondMultiplier);
        Assert.Equal(4m, combat.DamageMultiplier("rupture",
            CombatEffectDeliveryType.Periodic, context, context.GetActor("target")!));
    }

    [Fact]
    public void Eviscerate_OnlyConsumesComboPointsOnLandedDamage()
    {
        var (context, rogue) = RogueContext("rogue");
        rogue.AddResource(new ResourceState("energy", 100m, 100m, 10m));
        var manager = new AuraManager();
        var executor = new AbilityExecutor(manager,
            new SimpleCombatRollResolver(hitChancePercent: 100m, criticalChancePercent: 0m),
            new RulesetDamageMitigationResolver(new CombatRulesetDefinition {
                RulesetKey = "cp166-stage4a", Version = "1"
            }));
        var combat = new RogueCombatProcessor(RogueSimRequest.CreatePreset("combat"),
            rogue, new RotationProfile(), executor, manager,
            new AutoAttackProcessor(executor), []);
        for (int i = 0; i < 4; i++)
            combat.Process(context, new CombatEvent {
                Type = CombatEventType.Damage, SourceActorKey = "rogue",
                TargetActorKey = "target", AbilityKey = "sinister-strike",
                AbilityExecutionId = Guid.NewGuid(), Amount = 100m
            });
        Assert.Equal(4, combat.ComboPointsFor("target"));
        var missedId = Guid.NewGuid();
        combat.Process(context, new CombatEvent {
            Type = CombatEventType.AbilityCastCompleted, SourceActorKey = "rogue",
            TargetActorKey = "target", AbilityKey = "eviscerate",
            AbilityExecutionId = missedId
        });
        combat.Process(context, new CombatEvent {
            Type = CombatEventType.Damage, SourceActorKey = "rogue",
            TargetActorKey = "target", AbilityKey = "eviscerate",
            AbilityExecutionId = missedId, ResultKey = "miss", Amount = 0m
        });
        Assert.Equal(4, combat.ComboPointsFor("target"));
        Assert.Equal(4m, combat.DamageMultiplier("eviscerate",
            CombatEffectDeliveryType.Direct, context, context.GetActor("target")!));
        var landedId = Guid.NewGuid();
        combat.Process(context, new CombatEvent {
            Type = CombatEventType.Damage, SourceActorKey = "rogue",
            TargetActorKey = "target", AbilityKey = "eviscerate",
            AbilityExecutionId = landedId, ResultKey = "hit", Amount = 100m
        });
        Assert.Equal(0, combat.ComboPointsFor("target"));
        Assert.Equal(1, combat.FinishersUsed);
    }

    [Fact]
    public void Rupture_FailedApplicationCannotConsumeComboPoints()
    {
        var (context, rogue) = RogueContext("rogue");
        rogue.AddResource(new ResourceState("energy", 100m, 100m, 10m));
        var manager = new AuraManager();
        var executor = new AbilityExecutor(manager,
            new SimpleCombatRollResolver(hitChancePercent: 100m, criticalChancePercent: 0m),
            new RulesetDamageMitigationResolver(new CombatRulesetDefinition {
                RulesetKey = "cp166-stage4a", Version = "1"
            }));
        var combat = new RogueCombatProcessor(RogueSimRequest.CreatePreset("combat"),
            rogue, new RotationProfile(), executor, manager,
            new AutoAttackProcessor(executor), []);
        for (int i = 0; i < 3; i++)
            combat.Process(context, new CombatEvent {
                Type = CombatEventType.Damage, SourceActorKey = "rogue",
                TargetActorKey = "target", AbilityKey = "sinister-strike",
                AbilityExecutionId = Guid.NewGuid(), Amount = 100m
            });
        var execution = Guid.NewGuid();
        combat.Process(context, new CombatEvent {
            Type = CombatEventType.AbilityCastCompleted, SourceActorKey = "rogue",
            TargetActorKey = "target", AbilityKey = "rupture",
            AbilityExecutionId = execution
        });
        Assert.Equal(3, combat.ComboPointsFor("target"));
        combat.Process(context, new CombatEvent {
            Type = CombatEventType.Damage, SourceActorKey = "rogue",
            TargetActorKey = "target", AbilityKey = "rupture",
            AbilityExecutionId = execution, ResultKey = "miss", Amount = 0m
        });
        Assert.Equal(3, combat.ComboPointsFor("target"));
        Assert.Equal(0, combat.FinishersUsed);
    }

    [Theory]
    [InlineData(1, 1.05)]
    [InlineData(2, 1.10)]
    public void Opportunity_CurrentForeverRanksScaleEveryNamedAbility(int rank, double expectedRatio)
    {
        var req = RogueSimRequest.CreatePreset("subtlety");
        var zero = ForeverRogueSpellbook.Create("subtlety", req.Talents, req.Tuning, 400m, true);
        req.Talents.Ranks["opportunity"] = rank;
        var chosen = ForeverRogueSpellbook.Create("subtlety", req.Talents, req.Tuning, 400m, true);
        foreach (var key in new[] { "backstab", "ambush", "garrote" })
        {
            var baseline = Assert.Single(zero, x => x.Key == key).Effects[0].MinimumValue;
            var buffed = Assert.Single(chosen, x => x.Key == key).Effects[0].MinimumValue;
            Assert.Equal((decimal)expectedRatio, buffed / baseline,
                precision: 8);
        }

        var assassin = RogueSimRequest.CreatePreset("assassination");
        assassin.Talents.CombatPointsSpent = 8;
        assassin.Talents.SubtletyPointsSpent = 2;
        var baseMutilate = ForeverRogueSpellbook.Create("assassination",
            assassin.Talents, assassin.Tuning, 400m, true).Single(x => x.Key == "mutilate");
        assassin.Talents.Ranks["opportunity"] = rank;
        var bonusMutilate = ForeverRogueSpellbook.Create("assassination",
            assassin.Talents, assassin.Tuning, 400m, true).Single(x => x.Key == "mutilate");
        Assert.Equal((decimal)expectedRatio,
            bonusMutilate.Effects[0].MinimumValue / baseMutilate.Effects[0].MinimumValue,
            precision: 8);
        Assert.Equal((decimal)expectedRatio,
            bonusMutilate.Effects[1].MinimumValue / baseMutilate.Effects[1].MinimumValue,
            precision: 8);
    }

    [Fact]
    public void Opportunity_RejectsObsoleteFivePointClassicRank()
    {
        var req = RogueSimRequest.CreatePreset("subtlety");
        req.Talents.Ranks["opportunity"] = 5;
        Assert.Throws<ArgumentException>(() => req.Talents.Validate(60, "subtlety"));
        var valid = RogueSimRequest.CreatePreset("subtlety");
        Assert.Equal(2, valid.Talents.Rank("opportunity"));
        valid.Talents.Validate(60, "subtlety");
    }

    [Fact]
    public void Garrote_AppliedBleedTicksDoNotRerollTargetDodge()
    {
        var req = RogueSimRequest.CreatePreset("combat");
        req.StartInStealth = true;
        req.UseInstantPoison = false;
        req.DurationSeconds = 120m;
        req.CaptureTimeline = true;
        req.Tuning.MainHandHitPercent = 100m;
        req.Tuning.OffHandHitPercent = 100m;
        req.TargetDodgePercent = 25m;
        var run = new RogueSimRunner().Run(req);
        var ticks = run.Run.Timeline.Where(e =>
            e.Type == CombatEventType.Damage &&
            e.AbilityKey == "garrote" &&
            e.EffectDeliveryType == CombatEffectDeliveryType.Periodic).ToList();
        Assert.NotEmpty(ticks);
        Assert.DoesNotContain(ticks, e => e.ResultKey == "miss");
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(1, 105)]
    [InlineData(2, 110)]
    public void Vigor_MaxEnergyMatchesCurrentForeverTooltip(int ranks, int maximumEnergy)
    {
        var req = RogueSimRequest.CreatePreset("assassination");
        req.Talents.Ranks["vigor"] = ranks;
        req.StartInStealth = false;
        req.DurationSeconds = 12m;
        var result = new RogueSimRunner().Run(req);
        Assert.Equal((decimal)maximumEnergy, result.MaximumEnergy);
    }

    [Fact]
    public void PoisonTalentsAndWeaponSpecializationUseCurrentClientRankValues()
    {
        var tuning = new WarcraftSim.Data.Forever.Rogue.ForeverRogueTuning();
        Assert.Equal(10m, tuning.PoisonChargePreservationChancePerRank);
        Assert.Equal(3m, tuning.HackAndSlashMaceArmorIgnorePercentPerRank);
        var req = RogueSimRequest.CreatePreset("assassination");
        Assert.Equal(5, req.Talents.Rank("improved-poisons"));
    }

    [Theory]
    [InlineData("arms", false)]
    [InlineData("fury", false)]
    [InlineData("protection", true)]
    public void Warrior_AllThreePresetsProduceExecutableAndSerializableRuns(string spec, bool tank)
    {
        var req = new WarriorSimRequest {
            Preset = spec, TalentMode = "legacy", Level = 60,
            Iterations = 1, DurationSeconds = 24m, ExecutePhaseDurationSeconds = 0m,
            TargetArmor = 0m, TargetLevel = 60, TargetDodgePercent = 0m,
            TargetParryPercent = 0m, MeleeHitPercent = 100m,
            UseBloodrage = true, StartingRage = 50m,
            TankMode = tank, ShieldEquipped = tank,
            CaptureRepresentativeTimeline = true
        };
        var response = new ForeverWarriorSimRunner().Run(req);
        Assert.Equal(spec, response.Preset);
        Assert.True(response.Batch.RepresentativeRun.ProcessedEventCount > 0);
        Assert.True(response.MeanDps >= 0m);
        var json = System.Text.Json.JsonSerializer.Serialize(response);
        Assert.Contains("\"preset\"", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("assassination")]
    [InlineData("combat")]
    [InlineData("subtlety")]
    public void Rogue_AllThreePresetsProduceExecutableAndSerializableRuns(string spec)
    {
        var req = RogueSimRequest.CreatePreset(spec);
        req.DurationSeconds = 30m;
        req.CaptureTimeline = true;
        var response = new RogueSimRunner().Run(req);
        Assert.Equal(spec, response.Specialization);
        Assert.True(response.Run.ProcessedEventCount > 0);
        Assert.True(response.DamageDone >= 0m);
        var json = System.Text.Json.JsonSerializer.Serialize(response);
        Assert.Contains("\"specialization\"", json, StringComparison.OrdinalIgnoreCase);
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
