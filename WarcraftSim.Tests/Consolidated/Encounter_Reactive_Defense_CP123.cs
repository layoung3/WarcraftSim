using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Encounters;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;
using Xunit;

namespace WarcraftSim.Tests;

/// <summary>CP123 regression coverage: reusable boss actions, interrupt tokens,
/// health/phase gates, and defense values derived from effective character stats.</summary>
public sealed class EncounterReactiveCp123Tests
{
    [Fact]
    public void PhaseGatedBossAbility_CastsOnlyWithinItsTimeWindow()
    {
        var encounter = Encounter(19m, new EncounterBossAbilityDefinition
        {
            Key = "enrage-slam", SourceActorKey = "boss", TargetActorKey = "tank",
            CooldownSeconds = 3m, DirectDamage = 100m,
            RequiredPhaseKeys = ["enrage"]
        });
        encounter.Phases.Add(new EncounterPhaseDefinition
        {
            Key = "enrage", Name = "Enrage", StartTimeSeconds = 8m, EndTimeSeconds = 15m
        });
        var (events, _) = Run(encounter);
        Assert.Equal(new[] { 9m, 12m }, Hits(events, "enrage-slam").Select(e => e.TimeSeconds));
    }

    [Fact]
    public void HealthGatedAbility_UsesCasterHealthAndNotPlayerHealth()
    {
        var encounter = Encounter(10m, new EncounterBossAbilityDefinition
        {
            Key = "low-health", SourceActorKey = "boss", TargetActorKey = "tank",
            FirstUseSeconds = 1m, CooldownSeconds = 3m, DirectDamage = 200m,
            SourceHealthPercentAtOrBelow = 40m
        });
        Assert.Empty(Hits(Run(encounter).Events, "low-health"));
        var (events, _) = Run(encounter, start: context =>
            context.GetActor("boss")!.InitializeHealth(5000m, 1750m));
        Assert.Equal(new[] { 1m, 4m, 7m }, Hits(events, "low-health").Select(e => e.TimeSeconds));
    }

    [Fact]
    public void InterruptibleBossCast_IsCancelledBeforeDamageOrAuraApplication()
    {
        var encounter = Encounter(12m, new EncounterBossAbilityDefinition
        {
            Key = "nuke", SourceActorKey = "boss", TargetActorKey = "tank",
            CastTimeSeconds = 5m, Interruptible = true, DirectDamage = 1200m,
            DebuffKey = "curse", DebuffDurationSeconds = 5m,
            DotTickDamage = 20m, DotTickIntervalSeconds = 1m
        });
        var (events, context) = Run(encounter, start: ctx => Interrupt(ctx, 2m, "dps", "nuke"));
        Assert.Single(events.Timeline, e => e.Type == CombatEventType.EncounterBossCastInterrupted);
        Assert.DoesNotContain(events.Timeline, e => e.Type == CombatEventType.EncounterBossCastCompleted);
        Assert.Empty(Hits(events, "nuke"));
        Assert.DoesNotContain(context.GetActor("tank")!.ActiveAuras, a => a.Definition.Key == "curse");
    }

    [Fact]
    public void NonInterruptibleCast_StillHitsAfterInterruptRequest()
    {
        var encounter = Encounter(12m, new EncounterBossAbilityDefinition
        {
            Key = "uninterruptible", SourceActorKey = "boss", TargetActorKey = "tank",
            CastTimeSeconds = 5m, DirectDamage = 100m, Interruptible = false
        });
        var (events, _) = Run(encounter, start: ctx => Interrupt(ctx, 2m, "dps", "uninterruptible"));
        Assert.Single(Hits(events, "uninterruptible"));
        Assert.DoesNotContain(events.Timeline, e => e.Type == CombatEventType.EncounterBossCastInterrupted);
    }

    [Fact]
    public void CompletedCastCannotBeRetroactivelyInterrupted()
    {
        var encounter = Encounter(10m, new EncounterBossAbilityDefinition
        {
            Key = "short", SourceActorKey = "boss", TargetActorKey = "tank",
            CastTimeSeconds = 1m, DirectDamage = 100m, Interruptible = true
        });
        var (events, _) = Run(encounter, start: ctx => Interrupt(ctx, 2m, "dps", "short"));
        Assert.Single(Hits(events, "short"));
        Assert.DoesNotContain(events.Timeline, e => e.Type == CombatEventType.EncounterBossCastInterrupted);
    }

    [Fact]
    public void RepeatingCast_CanBeInterruptedOnceAndRecoverNextCooldown()
    {
        var encounter = Encounter(18m, new EncounterBossAbilityDefinition
        {
            Key = "repeat", SourceActorKey = "boss", TargetActorKey = "tank",
            CastTimeSeconds = 3m, CooldownSeconds = 8m,
            DirectDamage = 100m, Interruptible = true
        });
        var (events, _) = Run(encounter, start: ctx => Interrupt(ctx, 1m, "healer", "repeat"));
        Assert.Single(events.Timeline, e => e.Type == CombatEventType.EncounterBossCastInterrupted);
        Assert.Equal(new[] { 11m }, Hits(events, "repeat").Select(e => e.TimeSeconds));
    }

    [Fact]
    public void InvalidPhaseReference_IsRejected()
    {
        var encounter = Encounter(10m, new EncounterBossAbilityDefinition
        {
            Key = "invalid", SourceActorKey = "boss", TargetActorKey = "tank",
            RequiredPhaseKeys = ["missing"]
        });
        Assert.Throws<ArgumentException>(() => Run(encounter));
    }

    [Fact]
    public void GearAndEnchantRatingsContributeToDerivedTankDefense()
    {
        var request = new WarriorSimRequest
        {
            EquipmentMode = "gear-bonuses", StatSourceMode = "manual",
            Gear = [new WarriorGearEntry
            {
                Slot = "head", ItemKey = "test-helm", Stats = new()
                {
                    [ForeverCombatStatKeys.Strength] = 40m,
                    [ForeverCombatStatKeys.DodgeRating] = 24m,
                    [ForeverCombatStatKeys.ParryRating] = 15m,
                    [ForeverCombatStatKeys.BlockRating] = 10m,
                    [ForeverCombatStatKeys.DefenseSkill] = 10m
                },
                EnchantName = "Test block enchant", EnchantStats = new()
                {
                    [ForeverCombatStatKeys.BlockValue] = 16m
                }
            }]
        };
        var stats = WarriorGearStatCalculator.Calculate(request).EffectiveStats;
        var tank = ForeverTankDefenseCalculator.Calculate(stats, true, 5m, 5m, 5m,
            shieldIntrinsicBlockValue: 36m, additionalParryPercent: 3m,
            additionalBlockPercent: 2m, additionalDefenseSkill: 8m);
        Assert.Equal(7m, tank.DodgePercent);
        Assert.Equal(9m, tank.ParryPercent);
        Assert.Equal(9m, tank.BlockPercent);
        Assert.Equal(54m, tank.BlockValue); // 36 shield + 16 enchant + 40/20 Strength
        Assert.Equal(18m, tank.BonusDefenseSkill); // applies once during incoming roll
    }

    [Fact]
    public void WithoutShield_BlockChanceAndBlockValueAreZero()
    {
        var stats = new WarcraftSim.Core.Stats.StatCollection();
        stats.Set(ForeverCombatStatKeys.BlockValue, 50m);
        stats.Set(ForeverCombatStatKeys.BlockChancePercent, 20m);
        var result = ForeverTankDefenseCalculator.Calculate(stats, false, 5m, 5m, 5m);
        Assert.Equal(0m, result.BlockValue);
        Assert.Equal(0m, result.BlockPercent);
    }

    [Fact]
    public void BossCastPolicy_CanActivateShieldWallBeforeCastedSpecial()
    {
        static WarriorSimRequest Setup() => new()
        {
            Preset = "protection", ShieldEquipped = true,
            IsTwoHanded = false, TankMode = true, EnableBossAutoAttacks = true,
            TalentMode = "legacy", DurationSeconds = 12m,
            BossFirstSwingSeconds = 10m, BossSwingMinimumDamage = 0m,
            BossSwingMaximumDamage = 0m, BossMeleeCritPercent = 0m,
            Iterations = 1, Seed = 313, CaptureRepresentativeTimeline = true,
            BossAbilities = [new EncounterBossAbilityDefinition
            {
                Key = "blast", SourceActorKey = "target", TargetActorKey = "warrior",
                FirstUseSeconds = 2m, CastTimeSeconds = 3m, DirectDamage = 2000m,
                SchoolKey = "arcane", MitigationType = "none"
            }]
        };
        var without = new ForeverWarriorSimRunner().Run(Setup());
        var withPolicy = Setup();
        withPolicy.MajorCooldowns = [new WarriorMajorCooldownPolicy
        {
            AbilityKey = "shield-wall", Mode = "on-boss-cast",
            TriggerBossAbilityKeys = ["blast"],
            TriggerMinimumRawDamagePercentOfMaxHealth = 35m
        }];
        var result = new ForeverWarriorSimRunner().Run(withPolicy);
        Assert.True(result.MeanDamageTakenPerSecond < without.MeanDamageTakenPerSecond);
        Assert.Contains(result.Batch.RepresentativeRun.Timeline, e =>
            e.Type == CombatEventType.AbilityCastCompleted && e.AbilityKey == "shield-wall");
    }

    [Fact]
    public void CatalogShield_ProvidesItsOwnBlockValueInDerivedMode()
    {
        var request = new WarriorSimRequest
        {
            EquipmentMode = "catalog", TankStatMode = "derived",
            Gear = [
                new WarriorGearEntry { Slot = "main-hand", ItemId = 279259 },
                new WarriorGearEntry { Slot = "off-hand", ItemId = 12602 }
            ]
        };
        // This regression only depends on the existing CP122 catalog fixture IDs.
        var resolved = ForeverEquipmentCatalog.ResolveWarriorLoadout(request);
        Assert.True(resolved.ShieldEquipped);
        Assert.True(resolved.ShieldIntrinsicBlockValue > 0m);
        Assert.Equal(ForeverEquipmentCatalog.FindItem(12602).ShieldBlockValue,
            resolved.ShieldIntrinsicBlockValue);
    }

    [Fact]
    public void LegacyTankStatMode_RemainsDefaultForExistingSavedConfigs()
    {
        Assert.Equal("manual", new WarriorSimRequest().TankStatMode);
    }

    private static EncounterProfile Encounter(decimal duration, EncounterBossAbilityDefinition ability) =>
        new() { DurationSeconds = duration, BossAbilities = [ability] };

    private static IEnumerable<CombatEvent> Hits(SimulationRunResult result, string key) =>
        result.Timeline.Where(e => e.Type == CombatEventType.Damage && e.EncounterEventKey == key);

    private static void Interrupt(SimulationContext context, decimal time, string actor, string key) =>
        context.ScheduleEvent(new CombatEvent
        {
            TimeSeconds = time, Type = CombatEventType.EncounterBossInterruptRequested,
            SourceActorKey = actor, TargetActorKey = "boss", EncounterEventKey = key,
            IsInternal = true
        });

    private static (SimulationRunResult Events, SimulationContext Context) Run(EncounterProfile encounter,
        Action<SimulationContext>? start = null)
    {
        var context = new SimulationContext(new SimulationRunOptions
        {
            DurationSeconds = encounter.DurationSeconds, Seed = 111,
            CaptureTimeline = true, PrimaryActorKey = "tank", MaximumProcessedEvents = 5000
        }, encounter);
        foreach (var (key, team) in new[]
        {
            ("boss", "enemy"), ("tank", "raid"), ("dps", "raid"), ("healer", "raid")
        })
        {
            var actor = new SimulationActorState { Key = key, Name = key, TeamKey = team, Level = 60 };
            actor.InitializeHealth(5000m);
            context.AddActor(actor);
        }
        var auras = new AuraManager();
        var result = new SimulationEngine([
            auras,
            new EncounterTimelineProcessor(),
            new EncounterBossAbilityProcessor(auras),
            new ScriptedEncounterDamageProcessor(new RulesetDamageMitigationResolver(new CombatRulesetDefinition
            {
                RulesetKey = "cp123-test", Version = "1"
            }))
        ]).Run(context, start);
        return (result, context);
    }
}
