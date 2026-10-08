using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Encounters;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Abilities.Warrior;

namespace WarcraftSim.Tests;

public sealed class NextSwingAdditionalTargetTests
{
    [Fact]
    public void QueuedCleave_HitsPrimaryAndOneLinkedSecondaryTarget()
    {
        var result = RunCleave(
            links:
            [
                Link("target-a", "target-b", true)
            ],
            secondaryActors:
            [
                CreateActor("target-b", "enemies")
            ]
        );

        var cleaveDamage = DamageEvents(result);

        Assert.Equal(2, cleaveDamage.Count);
        Assert.Contains(cleaveDamage, e => e.TargetActorKey == "target-a");
        Assert.Contains(cleaveDamage, e => e.TargetActorKey == "target-b");

        var summary = result.Summary.Abilities[ForeverCleaveFactory.AbilityKey];
        Assert.Equal(2, summary.DamageOccurrenceCount);
    }

    [Fact]
    public void QueuedCleave_PaysRageOnlyOnceAcrossBothTargets()
    {
        var result = RunCleave(
            links:
            [
                Link("target-a", "target-b", true)
            ],
            secondaryActors:
            [
                CreateActor("target-b", "enemies")
            ]
        );

        var rageSpendEvents =
            result.Timeline
                .Where(e =>
                    e.Type == CombatEventType.ResourceChanged &&
                    e.AbilityKey == ForeverCleaveFactory.AbilityKey &&
                    e.Amount < 0m)
                .ToList();

        var rageSpend = Assert.Single(rageSpendEvents);
        Assert.Equal(-20m, rageSpend.Amount);
    }

    [Fact]
    public void QueuedCleave_WithoutNearbyEnemyStillHitsPrimary()
    {
        var result = RunCleave([], []);

        var damage = Assert.Single(DamageEvents(result));

        Assert.Equal("target-a", damage.TargetActorKey);
        Assert.Equal(ForeverCleaveFactory.AbilityKey, damage.AbilityKey);
    }

    [Fact]
    public void QueuedCleave_IgnoresLinkOutsideCleaveRange()
    {
        var result = RunCleave(
            links:
            [
                Link("target-a", "target-b", false)
            ],
            secondaryActors:
            [
                CreateActor("target-b", "enemies")
            ]
        );

        var damage = Assert.Single(DamageEvents(result));
        Assert.Equal("target-a", damage.TargetActorKey);
    }

    [Fact]
    public void QueuedCleave_IgnoresFriendlyLinkedActor()
    {
        var result = RunCleave(
            links:
            [
                Link("target-a", "friendly", true)
            ],
            secondaryActors:
            [
                CreateActor("friendly", "players")
            ]
        );

        var damage = Assert.Single(DamageEvents(result));
        Assert.Equal("target-a", damage.TargetActorKey);
    }

    [Fact]
    public void QueuedCleave_IgnoresDeadLinkedEnemy()
    {
        var dead = CreateActor("target-b", "enemies", health: 0m);

        var result = RunCleave(
            links:
            [
                Link("target-a", "target-b", true)
            ],
            secondaryActors:
            [
                dead
            ]
        );

        var damage = Assert.Single(DamageEvents(result));
        Assert.Equal("target-a", damage.TargetActorKey);
    }

    [Fact]
    public void QueuedCleave_WithSeveralNearbyEnemiesUsesOnlyOneSecondaryTarget()
    {
        var result = RunCleave(
            links:
            [
                Link("target-a", "target-c", true),
                Link("target-a", "target-b", true)
            ],
            secondaryActors:
            [
                CreateActor("target-b", "enemies"),
                CreateActor("target-c", "enemies")
            ]
        );

        var damage = DamageEvents(result);

        Assert.Equal(2, damage.Count);
        Assert.Contains(damage, e => e.TargetActorKey == "target-a");
        Assert.Contains(damage, e => e.TargetActorKey == "target-b");
        Assert.DoesNotContain(damage, e => e.TargetActorKey == "target-c");
    }

    [Fact]
    public void SingleTargetReplacement_DoesNotUseNearbyTargetEvenWhenLinked()
    {
        var context = CreateContext(
            [
                Link("target-a", "target-b", true)
            ]
        );

        var source = CreateActor("source", "players");
        AddRage(source, 100m);
        var primary = CreateActor("target-a", "enemies");
        var secondary = CreateActor("target-b", "enemies");

        context.AddActor(source);
        context.AddActor(primary);
        context.AddActor(secondary);

        var processor = CreateProcessor();
        var replacement = CreateSingleTargetReplacement();
        var autoAttack = CreateMainHandAutoAttack();

        var result = new SimulationEngine([processor])
            .Run(
                context,
                started =>
                {
                    Assert.True(processor.Start(
                        started,
                        source.Key,
                        primary.Key,
                        autoAttack,
                        firstSwingDelaySeconds: 0m
                    ));
                    Assert.True(processor.QueueNextSwingReplacement(
                        started,
                        source.Key,
                        autoAttack.Key,
                        replacement
                    ));
                }
            );

        var damage = Assert.Single(
            result.Timeline,
            e => e.Type == CombatEventType.Damage &&
                 e.AbilityKey == replacement.Key
        );

        Assert.Equal(primary.Key, damage.TargetActorKey);
    }

    [Fact]
    public void NegativeAdditionalTargetCount_FailsValidationWhenQueued()
    {
        var context = CreateContext([]);
        var source = CreateActor("source", "players");
        AddRage(source, 100m);
        var target = CreateActor("target-a", "enemies");
        context.AddActor(source);
        context.AddActor(target);

        var processor = CreateProcessor();
        var autoAttack = CreateMainHandAutoAttack();
        var replacement = CreateSingleTargetReplacement();
        replacement.MaximumAdditionalTargets = -1;

        Assert.True(processor.Start(
            context,
            source.Key,
            target.Key,
            autoAttack
        ));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            processor.QueueNextSwingReplacement(
                context,
                source.Key,
                autoAttack.Key,
                replacement
            )
        );
    }

    private static SimulationRunResult RunCleave(
        IReadOnlyList<TargetProximityLink> links,
        IReadOnlyList<SimulationActorState> secondaryActors)
    {
        var context = CreateContext(links);
        var source = CreateActor("source", "players");
        AddRage(source, 100m);
        var primary = CreateActor("target-a", "enemies");

        context.AddActor(source);
        context.AddActor(primary);

        foreach (var secondary in secondaryActors)
        {
            context.AddActor(secondary);
        }

        var processor = CreateProcessor();
        var autoAttack = CreateMainHandAutoAttack();
        var cleave =
            ForeverCleaveFactory.CreateForLevel(
                30,
                10m,
                10m,
                2m,
                "sword-skill"
            );

        return new SimulationEngine([processor])
            .Run(
                context,
                started =>
                {
                    Assert.True(processor.Start(
                        started,
                        source.Key,
                        primary.Key,
                        autoAttack,
                        firstSwingDelaySeconds: 0m
                    ));
                    Assert.True(processor.QueueNextSwingReplacement(
                        started,
                        source.Key,
                        autoAttack.Key,
                        cleave
                    ));
                }
            );
    }

    private static IReadOnlyList<CombatEvent> DamageEvents(
        SimulationRunResult result)
    {
        return result.Timeline
            .Where(e =>
                e.Type == CombatEventType.Damage &&
                e.AbilityKey == ForeverCleaveFactory.AbilityKey)
            .ToList();
    }

    private static SimulationContext CreateContext(
        IReadOnlyList<TargetProximityLink> links)
    {
        return new SimulationContext(
            new SimulationRunOptions
            {
                Seed = 12345,
                DurationSeconds = 0.5m,
                PrimaryActorKey = "source",
                CaptureTimeline = true
            },
            new EncounterProfile
            {
                Name = "Cleave Targeting",
                TargetProximityLinks = links.ToList()
            }
        );
    }

    private static TargetProximityLink Link(
        string a,
        string b,
        bool inCleaveRange)
    {
        return new TargetProximityLink
        {
            TargetAKey = a,
            TargetBKey = b,
            InCleaveRange = inCleaveRange
        };
    }

    private static SimulationActorState CreateActor(
        string key,
        string teamKey,
        decimal health = 1000m)
    {
        var actor =
            new SimulationActorState
            {
                Key = key,
                Name = key,
                TeamKey = teamKey,
                Level = 30
            };

        actor.InitializeHealth(
            Math.Max(1m, health),
            Math.Max(0m, health)
        );

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

    private static AutoAttackDefinition CreateMainHandAutoAttack()
    {
        return new AutoAttackDefinition
        {
            Key = "main-hand",
            Name = "Main Hand",
            SwingIntervalSeconds = 2m,
            WeaponHandKey = WeaponHandKeys.MainHand,
            DamageEffect =
                new AbilityEffectDefinition
                {
                    Key = "damage",
                    EffectType = AbilityEffectTypes.DirectDamage,
                    TargetType = AbilityTargetTypes.Enemy,
                    WeaponHandKey = WeaponHandKeys.MainHand,
                    MinimumValue = 1m,
                    MaximumValue = 1m,
                    MitigationType = DamageMitigationTypes.None
                }
        };
    }

    private static NextSwingReplacementDefinition CreateSingleTargetReplacement()
    {
        return new NextSwingReplacementDefinition
        {
            Key = "single-target-special",
            Name = "Single Target Special",
            WeaponHandKey = WeaponHandKeys.MainHand,
            DamageEffect =
                new AbilityEffectDefinition
                {
                    Key = "damage",
                    EffectType = AbilityEffectTypes.DirectDamage,
                    TargetType = AbilityTargetTypes.Enemy,
                    WeaponHandKey = WeaponHandKeys.MainHand,
                    MinimumValue = 10m,
                    MaximumValue = 10m,
                    MitigationType = DamageMitigationTypes.None
                }
        };
    }

    private static AutoAttackProcessor CreateProcessor()
    {
        return new AutoAttackProcessor(
            new AbilityExecutor(
                new AuraManager(),
                new FixedCombatRollResolver(CombatRollResult.Hit()),
                new RulesetDamageMitigationResolver(
                    new CombatRulesetDefinition
                    {
                        RulesetKey = "cleave-targeting-tests",
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
                    }
                )
            )
        );
    }

    private sealed class FixedCombatRollResolver(
        CombatRollResult result) : ICombatRollResolver
    {
        public CombatRollResult Resolve(
            SimulationContext context,
            SimulationActorState source,
            SimulationActorState target,
            AbilityDefinition ability,
            AbilityEffectDefinition effect)
        {
            return result;
        }
    }
}
