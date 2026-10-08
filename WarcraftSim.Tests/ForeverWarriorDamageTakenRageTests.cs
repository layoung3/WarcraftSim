using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Tests;

public sealed class ForeverWarriorDamageTakenRageTests
{
    [Fact]
    public void Factory_UsesProvisionalForeverDamageTakenRageRule()
    {
        var definition =
            ForeverWarriorDamageTakenRageFactory.Create();

        Assert.Equal(
            "rage",
            definition.ResourceKey
        );

        Assert.Equal(
            10m,
            definition.ResourcePerMaximumHealthOfEligibleDamage
        );

        Assert.True(definition.IgnoreArmorMitigation);
        Assert.True(definition.IgnoreAbsorbs);
        Assert.True(definition.BlockReducesEligibleDamage);
        Assert.True(definition.RequiresExternalSourceActor);
        Assert.False(
            ForeverWarriorDamageTakenRageFactory.CoefficientVerifiedByBlizzard
        );
    }

    [Theory]
    [InlineData(0, 200)]
    [InlineData(25, 150)]
    [InlineData(50, 100)]
    [InlineData(75, 50)]
    public void ArmorMitigation_DoesNotReduceForeverIncomingRage(
        double mitigationPercent,
        double healthDamage)
    {
        var target = CreateWarrior(1000m, 0m);

        var result =
            RunDamageEvent(
                target,
                new CombatEvent
                {
                    TimeSeconds = 0.1m,
                    Type = CombatEventType.Damage,
                    SourceActorKey = "enemy",
                    TargetActorKey = target.Key,
                    AbilityKey = "enemy-swing",
                    MitigationType = DamageMitigationTypes.Armor,
                    ResultKey = CombatResultTypes.Hit,
                    RawAmount = 200m,
                    MitigationPercent = (decimal)mitigationPercent,
                    MitigatedAmount =
                        200m -
                        (decimal)healthDamage,
                    Amount = (decimal)healthDamage
                }
            );

        Assert.Equal(
            2m,
            target.Resources["rage"].Current
        );

        var resourceEvent =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type == CombatEventType.ResourceChanged &&
                    combatEvent.AbilityKey ==
                        ForeverWarriorDamageTakenRageFactory.DefinitionKey
            );

        Assert.Equal(2m, resourceEvent.Amount);
    }

    [Fact]
    public void AbsorbedDamage_DoesNotReduceForeverIncomingRage()
    {
        var target = CreateWarrior(1000m, 0m);

        RunDamageEvent(
            target,
            new CombatEvent
            {
                TimeSeconds = 0.1m,
                Type = CombatEventType.Damage,
                SourceActorKey = "enemy",
                TargetActorKey = target.Key,
                AbilityKey = "enemy-spell",
                MitigationType = DamageMitigationTypes.None,
                RawAmount = 200m,
                AbsorbedAmount = 150m,
                Amount = 50m
            }
        );

        Assert.Equal(
            2m,
            target.Resources["rage"].Current
        );
    }

    [Fact]
    public void Block_ReducesEligibleDamageAfterArmorIsIgnored()
    {
        var target = CreateWarrior(1000m, 0m);

        RunDamageEvent(
            target,
            new CombatEvent
            {
                TimeSeconds = 0.1m,
                Type = CombatEventType.Damage,
                SourceActorKey = "enemy",
                TargetActorKey = target.Key,
                AbilityKey = "enemy-swing",
                MitigationType = DamageMitigationTypes.Armor,
                RawAmount = 200m,
                MitigationPercent = 50m,
                MitigatedAmount = 100m,
                BlockedAmount = 20m,
                Amount = 80m
            }
        );

        // 20 blocked after 50% Armor corresponds to 40 pre-Armor damage.
        // Eligible pre-Armor damage is therefore 160 / 1000 * 10 Rage.
        Assert.Equal(
            1.6m,
            target.Resources["rage"].Current
        );
    }

    [Fact]
    public void NonArmorMitigation_RemainsRespected()
    {
        var target = CreateWarrior(1000m, 0m);

        RunDamageEvent(
            target,
            new CombatEvent
            {
                TimeSeconds = 0.1m,
                Type = CombatEventType.Damage,
                SourceActorKey = "enemy",
                TargetActorKey = target.Key,
                AbilityKey = "enemy-fire",
                MitigationType = DamageMitigationTypes.Resistance,
                RawAmount = 200m,
                MitigationPercent = 50m,
                MitigatedAmount = 100m,
                Amount = 100m
            }
        );

        Assert.Equal(
            1m,
            target.Resources["rage"].Current
        );
    }

    [Fact]
    public void DamageWithoutSourceActor_GeneratesNoRage()
    {
        var target = CreateWarrior(1000m, 0m);

        RunDamageEvent(
            target,
            new CombatEvent
            {
                TimeSeconds = 0.1m,
                Type = CombatEventType.Damage,
                TargetActorKey = target.Key,
                AbilityKey = "environment",
                MitigationType = DamageMitigationTypes.None,
                RawAmount = 200m,
                Amount = 200m
            },
            addEnemy: false
        );

        Assert.Equal(0m, target.Resources["rage"].Current);
    }

    [Fact]
    public void SelfDamage_GeneratesNoRage()
    {
        var target = CreateWarrior(1000m, 0m);

        RunDamageEvent(
            target,
            new CombatEvent
            {
                TimeSeconds = 0.1m,
                Type = CombatEventType.Damage,
                SourceActorKey = target.Key,
                TargetActorKey = target.Key,
                AbilityKey = "self-damage",
                MitigationType = DamageMitigationTypes.None,
                RawAmount = 200m,
                Amount = 200m
            },
            addEnemy: false
        );

        Assert.Equal(0m, target.Resources["rage"].Current);
    }

    [Fact]
    public void UnknownSourceActor_GeneratesNoRage()
    {
        var target = CreateWarrior(1000m, 0m);

        RunDamageEvent(
            target,
            new CombatEvent
            {
                TimeSeconds = 0.1m,
                Type = CombatEventType.Damage,
                SourceActorKey = "missing-enemy",
                TargetActorKey = target.Key,
                AbilityKey = "unknown-source",
                MitigationType = DamageMitigationTypes.None,
                RawAmount = 200m,
                Amount = 200m
            },
            addEnemy: false
        );

        Assert.Equal(0m, target.Resources["rage"].Current);
    }

    [Fact]
    public void IncomingRage_IsClampedAndReportsActualGain()
    {
        var target = CreateWarrior(1000m, 99.5m);

        var result =
            RunDamageEvent(
                target,
                new CombatEvent
                {
                    TimeSeconds = 0.1m,
                    Type = CombatEventType.Damage,
                    SourceActorKey = "enemy",
                    TargetActorKey = target.Key,
                    AbilityKey = "enemy-swing",
                    MitigationType = DamageMitigationTypes.Armor,
                    RawAmount = 200m,
                    Amount = 100m,
                    MitigationPercent = 50m
                }
            );

        Assert.Equal(100m, target.Resources["rage"].Current);

        var resourceEvent =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type == CombatEventType.ResourceChanged &&
                    combatEvent.AbilityKey ==
                        ForeverWarriorDamageTakenRageFactory.DefinitionKey
            );

        Assert.Equal(0.5m, resourceEvent.Amount);
    }

    [Fact]
    public void ConfiguredIncomingGenerationWithoutResource_FailsExplicitly()
    {
        var target = CreateActor("warrior", 1000m);
        ForeverWarriorDamageTakenRageFactory.Configure(target);

        Assert.Throws<InvalidOperationException>(() =>
            RunDamageEvent(
                target,
                new CombatEvent
                {
                    TimeSeconds = 0.1m,
                    Type = CombatEventType.Damage,
                    SourceActorKey = "enemy",
                    TargetActorKey = target.Key,
                    AbilityKey = "enemy-swing",
                    MitigationType = DamageMitigationTypes.None,
                    RawAmount = 100m,
                    Amount = 100m
                }
            )
        );
    }

    [Fact]
    public void ActorWithoutIncomingGenerationConfiguration_GainsNoRage()
    {
        var target = CreateActor("warrior", 1000m);
        AddRage(target, 0m);

        RunDamageEvent(
            target,
            new CombatEvent
            {
                TimeSeconds = 0.1m,
                Type = CombatEventType.Damage,
                SourceActorKey = "enemy",
                TargetActorKey = target.Key,
                AbilityKey = "enemy-swing",
                MitigationType = DamageMitigationTypes.None,
                RawAmount = 100m,
                Amount = 100m
            }
        );

        Assert.Equal(0m, target.Resources["rage"].Current);
    }

    [Fact]
    public void DuplicateIncomingGenerationConfiguration_FailsExplicitly()
    {
        var target = CreateActor("warrior", 1000m);

        ForeverWarriorDamageTakenRageFactory.Configure(target);

        Assert.Throws<InvalidOperationException>(() =>
            ForeverWarriorDamageTakenRageFactory.Configure(target)
        );
    }

    [Fact]
    public void ScriptedArmorDamage_IntegratesWithIncomingRageProcessor()
    {
        var context = CreateContext();
        var enemy = CreateActor("enemy", 1000m);
        var target = CreateWarrior(1000m, 0m);
        target.Stats.Set("armor", 400m);

        context.AddActor(enemy);
        context.AddActor(target);

        var ruleset =
            new CombatRulesetDefinition
            {
                RulesetKey = "incoming-rage-integration",
                Version = "1",
                MitigationRules =
                {
                    [DamageMitigationTypes.Armor] =
                        new DamageMitigationRuleDefinition
                        {
                            MitigationType = DamageMitigationTypes.Armor,
                            FormulaType =
                                DamageMitigationFormulaTypes.RationalLevelScaled,
                            DefenseStatKey = "armor",
                            BaseConstant = 400m,
                            PerAttackerLevelConstant = 85m,
                            MaximumReductionPercent = 75m
                        }
                }
            };

        context.ScheduleEvent(
            new CombatEvent
            {
                TimeSeconds = 0.1m,
                Type = CombatEventType.ScriptedDamage,
                SourceActorKey = enemy.Key,
                TargetActorKey = target.Key,
                EncounterEventKey = "tank-hit",
                RawAmount = 200m,
                MitigationType = DamageMitigationTypes.Armor,
                SchoolKey = "physical"
            }
        );

        var result =
            new SimulationEngine(
                [
                    new ScriptedEncounterDamageProcessor(
                        new RulesetDamageMitigationResolver(ruleset)
                    ),
                    new DamageTakenResourceGenerationProcessor()
                ]
            ).Run(context);

        Assert.Equal(
            2m,
            target.Resources["rage"].Current
        );

        Assert.Contains(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.Damage &&
                combatEvent.RawAmount == 200m &&
                combatEvent.Amount < 200m
        );
    }

    private static SimulationRunResult RunDamageEvent(
        SimulationActorState target,
        CombatEvent damageEvent,
        bool addEnemy = true)
    {
        var context = CreateContext();

        if (addEnemy)
        {
            context.AddActor(
                CreateActor("enemy", 1000m)
            );
        }

        context.AddActor(target);
        context.ScheduleEvent(damageEvent);

        return new SimulationEngine(
            [new DamageTakenResourceGenerationProcessor()]
        ).Run(context);
    }

    private static SimulationActorState CreateWarrior(
        decimal maximumHealth,
        decimal rage)
    {
        var actor = CreateActor("warrior", maximumHealth);
        AddRage(actor, rage);
        ForeverWarriorDamageTakenRageFactory.Configure(actor);
        return actor;
    }

    private static SimulationActorState CreateActor(
        string key,
        decimal health)
    {
        var actor =
            new SimulationActorState
            {
                Key = key,
                Name = key,
                TeamKey =
                    key == "enemy"
                        ? "enemies"
                        : "players",
                Level = 30
            };

        actor.InitializeHealth(health);
        return actor;
    }

    private static void AddRage(
        SimulationActorState actor,
        decimal current)
    {
        actor.AddResource(
            new ResourceState(
                "rage",
                100m,
                current
            )
        );
    }

    private static SimulationContext CreateContext()
    {
        return new SimulationContext(
            new SimulationRunOptions
            {
                Seed = 12345,
                DurationSeconds = 0.5m,
                PrimaryActorKey = "warrior",
                CaptureTimeline = true
            }
        );
    }
}
