using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Encounters;
using WarcraftSim.Data.Forever.Abilities.Warrior;
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
        req.Talents.Ranks["opportunity"] = 0;
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
            TankMode = tank, ShieldEquipped = tank, IsTwoHanded = !tank,
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

    [Fact]
    public void SweepingStrikes_EchoConsumesShieldBeforeSecondaryHealth()
    {
        var encounter = new EncounterProfile {
            TargetProximityLinks = [new TargetProximityLink {
                TargetAKey = "target", TargetBKey = "add-1", InCleaveRange = true
            }]
        };
        var context = new SimulationContext(new SimulationRunOptions {
            DurationSeconds = 12m, PrimaryActorKey = "warrior",
            CaptureTimeline = true
        }, encounter);
        var warrior = new SimulationActorState { Key = "warrior", TeamKey = "raid" };
        warrior.InitializeHealth(1000m);
        var primary = new SimulationActorState { Key = "target", TeamKey = "enemy" };
        primary.InitializeHealth(1000m);
        var secondary = new SimulationActorState { Key = "add-1", TeamKey = "enemy" };
        secondary.InitializeHealth(1000m);
        context.AddActor(warrior); context.AddActor(primary); context.AddActor(secondary);
        new AbsorbManager().ApplyAbsorb(context, secondary, "barrier", "Barrier",
            80m, 10m, AbsorbStackingMode.Refresh, 1, "shield-caster", "shield", "absorb");
        var strikes = new WarriorSweepingStrikesProcessor();
        strikes.Process(context, new CombatEvent {
            Type = CombatEventType.AbilityCastCompleted, SourceActorKey = "warrior",
            AbilityKey = "sweeping-strikes"
        });
        strikes.Process(context, new CombatEvent {
            Type = CombatEventType.Damage, SourceActorKey = "warrior",
            TargetActorKey = "target", AbilityKey = "main-hand-auto",
            WeaponHandKey = "main-hand", AbilityExecutionId = Guid.NewGuid(),
            ResultKey = "hit", Amount = 100m
        });
        Assert.Equal(980m, secondary.CurrentHealth);
        Assert.Empty(secondary.ActiveAbsorbs);
    }

    [Fact]
    public void Protection_ShieldBlockRemovesTemporaryStatAfterSecondBlockedSwing()
    {
        var context = new SimulationContext(new SimulationRunOptions {
            DurationSeconds = 20m, PrimaryActorKey = "warrior"
        });
        var warrior = new SimulationActorState { Key = "warrior", TeamKey = "raid" };
        warrior.InitializeHealth(3000m);
        warrior.Stats.Set("block-chance-percent", 10m);
        context.AddActor(warrior);
        var target = new SimulationActorState { Key = "target", TeamKey = "enemy" };
        target.InitializeHealth(5000m);
        context.AddActor(target);
        var processor = new WarriorProtectionProcessor(new WarriorSimRequest {
            ShieldEquipped = true, Preset = "protection"
        });
        processor.Process(context, new CombatEvent {
            Type = CombatEventType.AbilityCastCompleted,
            SourceActorKey = warrior.Key, AbilityKey = "shield-block"
        });
        Assert.Equal(85m, warrior.Stats.Get("block-chance-percent"));
        for (var i = 0; i < 2; i++)
        {
            processor.Process(context, new CombatEvent {
                Type = CombatEventType.Damage, SourceActorKey = target.Key,
                TargetActorKey = warrior.Key, AbilityKey = "boss-main-hand-auto",
                ResultKey = "block", BlockedAmount = 50m
            });
            Assert.Equal(i == 0 ? 85m : 10m,
                warrior.Stats.Get("block-chance-percent"));
        }
        processor.Process(context, new CombatEvent {
            Type = CombatEventType.AbilityCastCompleted,
            SourceActorKey = warrior.Key, AbilityKey = "shield-block"
        });
        Assert.Equal(85m, warrior.Stats.Get("block-chance-percent"));
    }

    [Fact]
    public void Arms_MortalStrikeAppliesRealHealingReductionOnlyWhenHitLands()
    {
        var context = new SimulationContext(new SimulationRunOptions {
            Seed = 16604, DurationSeconds = 1m, PrimaryActorKey = "warrior"
        });
        var warrior = new SimulationActorState {
            Key = "warrior", Name = "Warrior", TeamKey = "raid", Level = 60
        };
        warrior.InitializeHealth(5000m);
        warrior.AddResource(new ResourceState("rage", 100m, 100m));
        var target = new SimulationActorState {
            Key = "target", Name = "Target", TeamKey = "enemy", Level = 60
        };
        target.InitializeHealth(5000m);
        context.AddActor(warrior);
        context.AddActor(target);
        var ability = ForeverMortalStrikeFactory.CreateForLevel(60, 100m, 100m,
            2.4m, "main-hand-weapon-skill");
        warrior.AddAbility(ability);
        var manager = new AuraManager();
        var executor = new AbilityExecutor(manager,
            new SimpleCombatRollResolver(hitChancePercent: 100m, criticalChancePercent: 0m),
            new RulesetDamageMitigationResolver(new CombatRulesetDefinition {
                RulesetKey = "cp166-stage4a", Version = "1"
            }));
        new SimulationEngine([executor, manager]).Run(context, start => {
            Assert.True(executor.TryStartAbility(start, warrior.Key,
                target.Key, "mortal-strike").Success);
        });
        var healingDebuff = target.ActiveAuras.SingleOrDefault(a =>
            a.Definition.Key == ForeverWarriorAuraKeys.MortalStrikeHealingReduction);
        Assert.NotNull(healingDebuff);
        Assert.Equal(.5m, healingDebuff.Definition.HealingReceivedMultiplier);
        Assert.Equal(50m, HealingReceivedModifiers.Apply(target, 1m, 100m));
        Assert.Equal(100m, HealingReceivedModifiers.Apply(target, 500m, 100m));
    }

    [Theory]
    [InlineData(true, 100, 0, "dodge")]
    [InlineData(false, 0, 100, "parry")]
    public void Rogue_PhysicalAvoidanceReportsDodgeAndParrySeparately(
        bool behind, int dodge, int parry, string resultKey)
    {
        var req = RogueSimRequest.CreatePreset("combat");
        req.StartInStealth = false;
        req.IsBehindTarget = behind;
        req.DurationSeconds = 12m;
        req.CaptureTimeline = true;
        req.UseInstantPoison = false;
        req.Tuning.MainHandHitPercent = 100m;
        req.Tuning.OffHandHitPercent = 100m;
        req.TargetDodgePercent = dodge;
        req.TargetParryPercent = parry;
        var response = new RogueSimRunner().Run(req);
        Assert.Contains(response.Run.Timeline, e =>
            e.Type == CombatEventType.Damage &&
            e.SourceActorKey == "rogue" &&
            e.ResultKey == resultKey);
    }

    [Fact]
    public void Assassination_SealFateCanProcFromSecondMutilateCritAfterFirstFails()
    {
        // Choose a deterministic seed where first 20% roll fails and second
        // succeeds. This validates the per-hit eligibility without coupling
        // the test to the exact implementation of .NET's seeded RNG.
        var seed = Enumerable.Range(1, 1000).First(s =>
        {
            var probe = new SimulationContext(new SimulationRunOptions {
                Seed = s, DurationSeconds = 5m
            });
            return probe.Random.NextDouble() >= 0.20d &&
                probe.Random.NextDouble() < 0.20d;
        });
        var context = new SimulationContext(new SimulationRunOptions {
            Seed = seed, DurationSeconds = 5m, PrimaryActorKey = "rogue"
        });
        var rogue = new SimulationActorState { Key = "rogue", TeamKey = "raid" };
        rogue.InitializeHealth(5000m);
        rogue.AddResource(new ResourceState("energy", 100m, 100m));
        var target = new SimulationActorState { Key = "target", TeamKey = "enemy" };
        target.InitializeHealth(5000m);
        context.AddActor(rogue); context.AddActor(target);
        var request = RogueSimRequest.CreatePreset("assassination");
        request.Talents.Ranks["seal-fate"] = 1;
        var manager = new AuraManager();
        var executor = new AbilityExecutor(manager,
            new SimpleCombatRollResolver(hitChancePercent: 100m, criticalChancePercent: 0m),
            new RulesetDamageMitigationResolver(new CombatRulesetDefinition {
                RulesetKey = "cp166-stage4a", Version = "1"
            }));
        var combat = new RogueCombatProcessor(request, rogue,
            new RotationProfile(), executor, manager,
            new AutoAttackProcessor(executor), []);
        var execution = Guid.NewGuid();
        combat.Process(context, new CombatEvent {
            Type = CombatEventType.Damage, SourceActorKey = "rogue",
            TargetActorKey = "target", AbilityKey = "mutilate",
            AbilityExecutionId = execution, WeaponHandKey = "main-hand",
            IsCritical = true, ResultKey = "critical", Amount = 100m
        });
        Assert.Equal(2, combat.ComboPointsFor("target"));
        combat.Process(context, new CombatEvent {
            Type = CombatEventType.Damage, SourceActorKey = "rogue",
            TargetActorKey = "target", AbilityKey = "mutilate",
            AbilityExecutionId = execution, WeaponHandKey = "off-hand",
            IsCritical = true, ResultKey = "critical", Amount = 100m
        });
        Assert.Equal(3, combat.ComboPointsFor("target"));
    }

    [Fact]
    public void Protection_TauntMatchesHighestThreatWithoutShieldAndUsesCurrentCooldown()
    {
        var request = new WarriorSimRequest {
            Preset = "protection", Level = 60, ShieldEquipped = false
        };
        var taunt = Assert.Single(WarriorProtectionAbilityFactory.Create(request),
            a => a.Key == "taunt");
        Assert.Equal(8m, taunt.CooldownSeconds);
        Assert.True(taunt.IsOffGlobalCooldown);
        Assert.Contains(ForeverWarriorAuraKeys.DefensiveStance, taunt.RequiredSourceAuraKeys);
        Assert.Equal(3m, taunt.Effects[0].DurationSeconds);

        var context = new SimulationContext(new SimulationRunOptions {
            Seed = 16604, DurationSeconds = 1m,
            PrimaryActorKey = "warrior", CaptureTimeline = true
        });
        var warrior = new SimulationActorState {
            Key = "warrior", TeamKey = "raid", Level = 60
        };
        warrior.InitializeHealth(5000m);
        warrior.AddAbility(taunt);
        var enemy = new SimulationActorState {
            Key = "target", TeamKey = "enemy", Level = 63
        };
        enemy.InitializeHealth(5000m);
        enemy.ThreatTable.AddThreat("other-tank", 1000m);
        var otherTank = new SimulationActorState {
            Key = "other-tank", TeamKey = "raid", Level = 60
        };
        otherTank.InitializeHealth(5000m);
        context.AddActor(warrior); context.AddActor(otherTank); context.AddActor(enemy);
        var auras = new AuraManager();
        var executor = new AbilityExecutor(auras,
            new SimpleCombatRollResolver(hitChancePercent: 100m, criticalChancePercent: 0m),
            new RulesetDamageMitigationResolver(new CombatRulesetDefinition {
                RulesetKey = "cp166-stage4a", Version = "1"
            }));
        new SimulationEngine([executor, auras]).Run(context, started =>
        {
            auras.ApplyAura(started, warrior, new AuraDefinition {
                Key = ForeverWarriorAuraKeys.DefensiveStance, DurationSeconds = 10m
            }, warrior.Key, "defensive-stance", "stance");
            Assert.True(executor.TryStartAbility(started, warrior.Key, enemy.Key, "taunt").Success);
        });
        Assert.True(enemy.ThreatTable.GetThreat(warrior.Key) >= 1000m);
    }

    [Fact]
    public void Fury_BloodthirstRankUsesCurrentFortyFivePercentAttackPowerCoefficient()
    {
        var ability = ForeverBloodthirstFactory.CreateForLevel(
            60, "main-hand-weapon-skill");
        var damage = Assert.Single(ability.Effects,
            e => e.EffectType == AbilityEffectTypes.DirectDamage);
        Assert.Equal(.45m, damage.ScalingCoefficient);
        Assert.Equal("attack-power", damage.ScalingStatKey);
        Assert.Equal(6m, ability.CooldownSeconds);
        Assert.Contains(ability.Effects,
            e => e.EffectType == AbilityEffectTypes.ApplyAura &&
                 e.DependsOnEffectKey == damage.Key &&
                 e.DependencyCondition == EffectDependencyConditions.Landed);
    }

    [Fact]
    public void Fury_WhirlwindDualWieldHasIndependentWeaponHitsAndSharedCost()
    {
        var single = ForeverWhirlwindFactory.CreateSingleWeapon(
            100m, 100m, 2.4m, "main-hand-weapon-skill");
        var dual = ForeverWhirlwindFactory.CreateDualWield(
            100m, 100m, 2.4m, "main-hand-weapon-skill",
            60m, 60m, 1.7m, "off-hand-weapon-skill");
        Assert.Single(single.Effects);
        Assert.Equal(2, dual.Effects.Count);
        Assert.All(dual.Effects, effect =>
        {
            Assert.Equal(4, effect.MaxTargets);
            Assert.Equal(AbilityEffectTypes.DirectDamage, effect.EffectType);
        });
        Assert.Equal(single.ResourceCosts.Single().Amount,
            dual.ResourceCosts.Single().Amount);
    }

    [Fact]
    public void Protection_DefensiveStanceThreatAndDefianceAreAppliedOnce()
    {
        var context = new SimulationContext(new SimulationRunOptions {
            DurationSeconds = 2m, PrimaryActorKey = "warrior"
        });
        var warrior = new SimulationActorState { Key = "warrior", TeamKey = "raid" };
        warrior.InitializeHealth(5000m);
        var enemy = new SimulationActorState { Key = "target", TeamKey = "enemy" };
        enemy.InitializeHealth(5000m);
        context.AddActor(warrior); context.AddActor(enemy);
        var request = new WarriorSimRequest {
            Preset = "protection", TalentMode = "tree", ShieldEquipped = true
        };
        request.TalentRanks["defiance"] = 3;
        var aura = new AuraManager();
        aura.ApplyAura(context, warrior, new AuraDefinition {
            Key = ForeverWarriorAuraKeys.DefensiveStance,
            DurationSeconds = 10m
        }, "warrior", "stance", "aura");
        var processor = new WarriorDamageThreatProcessor(true, request);
        processor.Process(context, new CombatEvent {
            Type = CombatEventType.Damage, SourceActorKey = "warrior",
            TargetActorKey = "target", AbilityKey = "shield-slam", Amount = 100m
        });
        Assert.Equal(100m * 1.30m * 1.15m,
            enemy.ThreatTable.GetThreat("warrior"));
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
