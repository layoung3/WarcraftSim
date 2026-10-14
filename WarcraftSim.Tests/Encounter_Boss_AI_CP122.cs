using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Encounters;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests;

public sealed class EncounterBossAiCp122Tests
{
    [Fact]
    public void CastedCooldownAbility_ImpactsAtCompletionAndRepeats()
    {
        var encounter = Encounter(27m, new EncounterBossAbilityDefinition
        {
            Key = "slam", Name = "Slam", SourceActorKey = "boss",
            FirstUseSeconds = 2m, CooldownSeconds = 10m, CastTimeSeconds = 3m,
            TargetActorKey = "tank", DirectDamage = 100m
        });
        var (result, context) = Run(encounter);
        Assert.Equal(new[] { 5m, 15m, 25m }, Hits(result, "slam").Select(e => e.TimeSeconds));
        Assert.Equal(4700m, context.GetActor("tank")!.CurrentHealth);
        Assert.Equal(3, result.Timeline.Count(e => e.Type == CombatEventType.EncounterBossCastCompleted));
    }

    [Fact]
    public void HighestThreatTargeting_IsResolvedAtImpact_NotAtCastStart()
    {
        var encounter = Encounter(10m, new EncounterBossAbilityDefinition
        {
            Key = "aggro", SourceActorKey = "boss", CastTimeSeconds = 3m,
            TargetSelection = new EncounterTargetSelectionDefinition
            {
                Mode = EncounterTargetSelectionModes.HighestThreatActor,
                Relationship = SimulationActorRelationshipTypes.Enemy
            }, DirectDamage = 100m
        });
        var (result, _) = Run(encounter, start: context =>
        {
            // Threat changes during the cast: hit whoever leads at impact.
            context.GetActor("boss")!.ThreatTable.AddThreat("tank", 100m);
            context.ScheduleEvent(new CombatEvent
            {
                TimeSeconds = 2m, Type = CombatEventType.RotationDecision,
                EffectKey = "transfer-threat", IsInternal = true
            });
        }, extra: new TestEventProcessor((context, ev) =>
        {
            if (ev.EffectKey == "transfer-threat")
                context.GetActor("boss")!.ThreatTable.AddThreat("dps", 200m);
        }));
        Assert.Equal("dps", Assert.Single(Hits(result, "aggro")).TargetActorKey);
    }

    [Fact]
    public void RandomRaidTargeting_RepeatsExactlyWithSameSeed()
    {
        var encounter = Encounter(25m, new EncounterBossAbilityDefinition
        {
            Key = "bolt", SourceActorKey = "boss", CooldownSeconds = 4m,
            TargetSelection = new EncounterTargetSelectionDefinition
            {
                Mode = EncounterTargetSelectionModes.RandomMatchingActors,
                TeamKey = "raid", Relationship = SimulationActorRelationshipTypes.Enemy,
                Count = 1
            }, DirectDamage = 10m
        });
        var a = Hits(Run(encounter, seed: 83).Result, "bolt").Select(e => e.TargetActorKey).ToArray();
        var b = Hits(Run(encounter, seed: 83).Result, "bolt").Select(e => e.TargetActorKey).ToArray();
        Assert.Equal(7, a.Length);
        Assert.Equal(a, b);
    }

    [Fact]
    public void ChannelTicks_ArriveOnTheirConfiguredCadence()
    {
        var encounter = Encounter(10m, new EncounterBossAbilityDefinition
        {
            Key = "beam", SourceActorKey = "boss", FirstUseSeconds = 1m,
            CastTimeSeconds = 2m, TargetActorKey = "tank",
            ChannelTickCount = 3, ChannelTickIntervalSeconds = 1m,
            ChannelTickDamage = 40m
        });
        var (result, _) = Run(encounter);
        Assert.Equal(new[] { 4m, 5m, 6m }, Hits(result, "beam").Select(e => e.TimeSeconds));
    }

    [Fact]
    public void DispelledDot_DoesNotDeliverItsRemainingQueuedTicks()
    {
        var encounter = Encounter(16m, new EncounterBossAbilityDefinition
        {
            Key = "curse", SourceActorKey = "boss", FirstUseSeconds = 1m,
            TargetActorKey = "tank", DebuffKey = "curse-debuff",
            DebuffDurationSeconds = 10m, DispelType = "magic",
            DotTickIntervalSeconds = 2m, DotTickDamage = 45m
        });
        var (result, _) = Run(encounter, start: ctx => ctx.ScheduleEvent(new CombatEvent
        {
            TimeSeconds = 6m, Type = CombatEventType.RotationDecision,
            EffectKey = "dispel-test", IsInternal = true
        }), extra: new TestEventProcessor((ctx, ev) =>
        {
            if (ev.EffectKey == "dispel-test")
                new AuraManager().RemoveAuras(ctx, ctx.GetActor("tank")!, "curse-debuff", "boss", "dispelled");
        }));
        Assert.Equal(new[] { 3m, 5m }, Hits(result, "curse").Select(e => e.TimeSeconds));
        Assert.Contains(result.Timeline, e => e.Type == CombatEventType.AuraRemoved);
    }

    [Fact]
    public void SoloPreview_PreservesHealthButScoresFullDamage()
    {
        var encounter = Encounter(5m, new EncounterBossAbilityDefinition
        {
            Key = "spike", SourceActorKey = "boss", FirstUseSeconds = 1m,
            TargetActorKey = "tank", DirectDamage = 7000m
        });
        var (result, context) = Run(encounter, preserveHealth: true);
        Assert.Equal(5000m, context.GetActor("tank")!.CurrentHealth);
        Assert.Equal(7000m, Assert.Single(Hits(result, "spike")).Amount!.Value);
    }

    [Fact]
    public void WarriorPreview_OptionalBossSpecialRaisesDtpsWithoutReducingPlayerHealth()
    {
        static WarriorSimRequest Base() => new()
        {
            Preset = "protection", EquipmentMode = "manual", TalentMode = "legacy",
            ShieldEquipped = true, TankMode = true, EnableBossAutoAttacks = true,
            IsTwoHanded = false, DualWield = false, DurationSeconds = 12m,
            Iterations = 1, Seed = 234, BossFirstSwingSeconds = 0m,
            BossSwingSpeedSeconds = 2m, BossSwingMinimumDamage = 500m,
            BossSwingMaximumDamage = 500m, PlayerDodgePercent = 0m,
            PlayerParryPercent = 0m, PlayerBlockPercent = 0m,
            BossMeleeCritPercent = 0m, UseBloodrage = false,
            StartingRage = 50m, TargetDodgePercent = 0m,
            TargetParryPercent = 0m, CaptureRepresentativeTimeline = true
        };
        var baseline = new ForeverWarriorSimRunner().Run(Base());
        var withSpecial = Base();
        withSpecial.BossAbilities = [new EncounterBossAbilityDefinition
        {
            Key = "raid-smash", Name = "Raid Smash", SourceActorKey = "target",
            FirstUseSeconds = 1m, CastTimeSeconds = 1m,
            TargetActorKey = "warrior", DirectDamage = 3000m
        }];
        var result = new ForeverWarriorSimRunner().Run(withSpecial);
        Assert.True(result.MeanDamageTakenPerSecond > baseline.MeanDamageTakenPerSecond);
        Assert.Contains(result.Batch.RepresentativeRun.Timeline, e =>
            e.Type == CombatEventType.Damage && e.EncounterEventKey == "raid-smash");
        Assert.Equal(0m, result.DeathRatePercent);
        Assert.False(result.Batch.RepresentativeRun.Summary.PrimaryActorDied);
    }

    [Fact]
    public void InvalidChannel_IsRejectedBeforeFirstMechanic()
    {
        var encounter = Encounter(10m, new EncounterBossAbilityDefinition
        {
            Key = "bad", SourceActorKey = "boss", TargetActorKey = "tank",
            ChannelTickCount = 2, ChannelTickIntervalSeconds = 0m
        });
        Assert.Throws<ArgumentException>(() => Run(encounter));
    }

    private static EncounterProfile Encounter(decimal duration, EncounterBossAbilityDefinition ability) =>
        new() { DurationSeconds = duration, BossAbilities = [ability] };

    private static IReadOnlyList<CombatEvent> Hits(SimulationRunResult result, string key) =>
        result.Timeline.Where(e => e.Type == CombatEventType.Damage &&
            e.EncounterEventKey == key).ToList();

    private static (SimulationRunResult Result, SimulationContext Context) Run(
        EncounterProfile encounter, int seed = 42, bool preserveHealth = false,
        Action<SimulationContext>? start = null, ICombatEventProcessor? extra = null)
    {
        var context = new SimulationContext(new SimulationRunOptions
        {
            DurationSeconds = encounter.DurationSeconds, Seed = seed,
            CaptureTimeline = true, PrimaryActorKey = "tank", MaximumProcessedEvents = 2000
        }, encounter);
        foreach (var (key, team, role) in new[]
        {
            ("boss", "enemy", (SimulationType?)null),
            ("tank", "raid", (SimulationType?)SimulationType.Tank),
            ("dps", "raid", (SimulationType?)SimulationType.Dps),
            ("healer", "raid", (SimulationType?)SimulationType.Healing)
        })
        {
            var actor = new SimulationActorState
            {
                Key = key, Name = key, TeamKey = team, AssignedRole = role, Level = 60
            };
            actor.InitializeHealth(5000m);
            context.AddActor(actor);
        }
        var auras = new AuraManager();
        var processors = new List<ICombatEventProcessor>
        {
            auras,
            new EncounterBossAbilityProcessor(auras),
            new ScriptedEncounterDamageProcessor(
                new RulesetDamageMitigationResolver(new CombatRulesetDefinition
                { RulesetKey = "cp122-test", Version = "1" }),
                preserveHealth: a => preserveHealth && a.Key == "tank")
        };
        if (extra is not null) processors.Add(extra);
        return (new SimulationEngine(processors).Run(context, start), context);
    }

    private sealed class TestEventProcessor(Action<SimulationContext, CombatEvent> callback) :
        ICombatEventProcessor
    {
        public void Process(SimulationContext context, CombatEvent ev) => callback(context, ev);
    }
}
