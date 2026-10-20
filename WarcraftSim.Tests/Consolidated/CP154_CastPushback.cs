using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Druid;
using Xunit;

namespace WarcraftSim.Tests;

public sealed class Cp154CastPushbackTests
{
    [Theory]
    [InlineData(0, 4.0)]
    [InlineData(100, 3.0)]
    public void IncomingDamageDelaysOneCastButNeverDuplicatesCompletion(
        int resistChance, double expectedSeconds)
    {
        var (run, processor, target) = Scenario(resistChance);
        var completed = Assert.Single(run.Timeline,
            e => e.Type == CombatEventType.AbilityCastCompleted && e.AbilityKey == "nature-heal");
        Assert.Equal((decimal)expectedSeconds, completed.TimeSeconds);
        Assert.Equal(2, processor.DamageEventsDuringCast);
        Assert.Equal(resistChance == 100 ? 2 : 0, processor.ResistedHits);
        Assert.Equal(resistChance == 0 ? 1m : 0m, processor.TotalPushbackSeconds);
        Assert.Equal(1, run.Summary.Abilities["nature-heal"].CastCompletedCount);
        Assert.True(target.CurrentHealth > 500m);
    }

    [Fact]
    public void PushbackDoesNotDelayAnUnrelatedSpellSchool()
    {
        var (run, processor, _) = Scenario(0, "fire");
        Assert.Equal(0, processor.DamageEventsDuringCast);
        Assert.Equal(0m, processor.TotalPushbackSeconds);
        Assert.Equal(3m, Assert.Single(run.Timeline,
            e => e.Type == CombatEventType.AbilityCastCompleted &&
                 e.AbilityKey == "nature-heal").TimeSeconds);
    }

    [Fact]
    public void CancellationAfterPushbackNeverCompletesTheCancelledCast()
    {
        var auras = new AuraManager();
        var executor = Executor(auras);
        var ctx = MakeContext(out var healer, out var target, out var boss);
        var pushback = new CastDamagePushbackProcessor(executor, healer.Key, 0m,
            ["nature", "arcane"]);
        var cancel = new CancelAtTwoSeconds(executor, healer.Key);
        var run = new SimulationEngine([executor, auras,
            new ScriptedEncounterDamageProcessor(new RulesetDamageMitigationResolver(
                new CombatRulesetDefinition { RulesetKey = "cp154-cancel", Version = "1" })),
            pushback, cancel]).Run(ctx, started =>
        {
            Assert.True(executor.TryStartAbility(started, healer.Key, target.Key,
                "nature-heal").Success);
            started.ScheduleEvent(ScriptedHit(1m, boss.Key, healer.Key));
            started.ScheduleEvent(new CombatEvent {
                Type = CombatEventType.RotationDecision, TimeSeconds = 2m,
                SourceActorKey = healer.Key, EffectKey = "cp154-cancel"
            });
        });
        Assert.Equal(.5m, pushback.TotalPushbackSeconds);
        Assert.DoesNotContain(run.Timeline, e =>
            e.Type == CombatEventType.AbilityCastCompleted && e.AbilityKey == "nature-heal");
        Assert.Contains(run.Timeline, e => e.Type == CombatEventType.AbilityCastCancelled &&
            e.AbilityKey == "nature-heal");
    }

    [Fact]
    public void RestorationDruidScriptedCasterPressureReportsPushback()
    {
        var noFocus = new RestorationDruidSimRequest {
            DurationSeconds = 10m, Seed = 15402, IncomingDamage = 0m,
            AdditionalAllies = 0, AllyStartingHealthPercent = 25m,
            CasterIncomingDamage = 60m, FirstCasterDamageAtSeconds = 2m,
            CasterDamageEverySeconds = 0.5m, CaptureTimeline = true
        };
        var baseline = new RestorationDruidSimRunner().Run(noFocus);
        Assert.True(baseline.Run.CustomMetrics["druid-cast-damage-events"] > 0m);
        Assert.True(baseline.Run.CustomMetrics["druid-cast-pushback-seconds"] > 0m);
        var talents = new ForeverRestorationDruidTalents();
        talents.Ranks["natures-focus"] = 5;
        talents.Validate();
        var focused = new RestorationDruidSimRunner().Run(new RestorationDruidSimRequest {
            DurationSeconds = 10m, Seed = 15402, IncomingDamage = 0m,
            AdditionalAllies = 0, AllyStartingHealthPercent = 25m,
            CasterIncomingDamage = 60m, FirstCasterDamageAtSeconds = 2m,
            CasterDamageEverySeconds = 0.5m, Talents = talents, CaptureTimeline = true
        });
        Assert.True(focused.Run.CustomMetrics.ContainsKey("druid-cast-pushback-resisted"));
        Assert.True(focused.Run.CustomMetrics["druid-cast-pushback-seconds"] >= 0m);
        Assert.True(focused.HealingDone > 0m);
    }

    private static (SimulationRunResult run, CastDamagePushbackProcessor processor,
        SimulationActorState target) Scenario(int resistChance, string school = "nature")
    {
        var ctx = MakeContext(out var healer, out var target, out var boss, school);
        var auras = new AuraManager();
        var executor = Executor(auras);
        var processor = new CastDamagePushbackProcessor(executor, healer.Key,
            resistChance, ["nature", "arcane"]);
        var mit = new RulesetDamageMitigationResolver(new CombatRulesetDefinition {
            RulesetKey = "cp154-encounter", Version = "1"
        });
        var run = new SimulationEngine([executor, auras,
            new ScriptedEncounterDamageProcessor(mit), processor]).Run(ctx, started =>
        {
            var use = executor.TryStartAbility(started, healer.Key, target.Key, "nature-heal");
            Assert.True(use.Success, use.FailureReason);
            started.ScheduleEvent(ScriptedHit(1m, boss.Key, healer.Key));
            started.ScheduleEvent(ScriptedHit(1.5m, boss.Key, healer.Key));
        });
        return (run, processor, target);
    }

    private static CombatEvent ScriptedHit(decimal when, string source, string target) => new() {
        TimeSeconds = when, Type = CombatEventType.ScriptedDamage,
        SourceActorKey = source, TargetActorKey = target,
        EncounterEventKey = "cp154-caster-hit", SchoolKey = "physical",
        MitigationType = DamageMitigationTypes.None, RawAmount = 60m
    };

    private static SimulationContext MakeContext(out SimulationActorState healer,
        out SimulationActorState target, out SimulationActorState boss, string school = "nature")
    {
        var ctx = new SimulationContext(new SimulationRunOptions {
            DurationSeconds = 8m, Seed = 15401, CaptureTimeline = true,
            PrimaryActorKey = "druid"
        });
        healer = new SimulationActorState { Key = "druid", Name = "Druid",
            TeamKey = "raid", Level = 60 };
        healer.InitializeHealth(5000m);
        healer.AddResource(new ResourceState("mana", 5000m, 5000m));
        healer.AddAbility(new AbilityDefinition {
            Key = "nature-heal", Name = "Nature Heal", CastTimeSeconds = 3m,
            ResourceCosts = [new AbilityResourceCost { ResourceKey = "mana", Amount = 100m }],
            Effects = [new AbilityEffectDefinition {
                Key = "heal", EffectType = AbilityEffectTypes.DirectHealing,
                TargetType = AbilityTargetTypes.Friendly, SchoolKey = school,
                ResolutionType = CombatResolutionTypes.AlwaysHits,
                CanMiss = false, CanCrit = false,
                MinimumValue = 200m, MaximumValue = 200m
            }]
        });
        target = new SimulationActorState { Key = "tank", Name = "Tank",
            TeamKey = "raid", Level = 60 };
        target.InitializeHealth(5000m, 500m);
        boss = new SimulationActorState { Key = "boss", Name = "Boss",
            TeamKey = "enemy", Level = 60 };
        boss.InitializeHealth(100000m);
        ctx.AddActor(healer); ctx.AddActor(target); ctx.AddActor(boss);
        return ctx;
    }

    private static AbilityExecutor Executor(AuraManager auras) => new(auras,
        new SimpleCombatRollResolver(hitChancePercent: 100m, criticalChancePercent: 0m),
        new RulesetDamageMitigationResolver(new CombatRulesetDefinition {
            RulesetKey = "cp154-rolls", Version = "1"
        }));

    private sealed class CancelAtTwoSeconds(AbilityExecutor executor, string actorKey)
        : ICombatEventProcessor
    {
        public void Process(SimulationContext context, CombatEvent combatEvent)
        {
            if (combatEvent.Type == CombatEventType.RotationDecision &&
                combatEvent.EffectKey == "cp154-cancel")
                executor.TryCancelCurrentCast(context, actorKey);
        }
    }
}
