// Consolidated tests for AutoAttack NextSwing.
// Original class names are retained for xUnit test filtering.

// ==================== NextSwingAdditionalTargetTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Encounters;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Abilities.Warrior;

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
}

// ==================== NextSwingOutcomeResourceRefundTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

public sealed class NextSwingOutcomeResourceRefundTests
{
    [Theory]
    [InlineData(CombatResultTypes.Miss)]
    [InlineData(CombatResultTypes.Dodge)]
    [InlineData(CombatResultTypes.Parry)]
    public void ConfiguredAvoidedOutcome_RefundsPaidResource(
        string resultKey)
    {
        var source = CreateActor("source");
        AddResource(source, "rage", 100m, 30m);
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack();
        var replacement = CreateReplacement();
        AddRefundRule(
            replacement,
            "rage",
            80m,
            CombatResultTypes.Miss,
            CombatResultTypes.Dodge,
            CombatResultTypes.Parry
        );

        var result = Run(
            source,
            target,
            mainHand,
            replacement,
            CombatRollResult.Avoided(resultKey)
        );

        Assert.Equal(27m, source.Resources["rage"].Current);

        var damage = Assert.Single(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.Damage
        );

        Assert.Equal(resultKey, damage.ResultKey);
        Assert.Equal(0m, damage.Amount);
    }

    [Fact]
    public void DefaultReplacement_DoesNotRefundAvoidedOutcome()
    {
        var source = CreateActor("source");
        AddResource(source, "rage", 100m, 30m);
        var target = CreateActor("target", 1000m);

        Run(
            source,
            target,
            CreateAutoAttack(),
            CreateReplacement(),
            CombatRollResult.Miss()
        );

        Assert.Equal(15m, source.Resources["rage"].Current);
    }

    [Fact]
    public void UnconfiguredAvoidedOutcome_DoesNotRefund()
    {
        var source = CreateActor("source");
        AddResource(source, "rage", 100m, 30m);
        var target = CreateActor("target", 1000m);
        var replacement = CreateReplacement();
        AddRefundRule(
            replacement,
            "rage",
            80m,
            CombatResultTypes.Miss
        );

        Run(
            source,
            target,
            CreateAutoAttack(),
            replacement,
            CombatRollResult.Dodge()
        );

        Assert.Equal(15m, source.Resources["rage"].Current);
    }

    [Fact]
    public void LandedHit_DoesNotRefundAvoidanceRule()
    {
        AssertNoRefundForLandedResult(
            CombatRollResult.Hit()
        );
    }

    [Fact]
    public void CriticalHit_DoesNotRefundAvoidanceRule()
    {
        AssertNoRefundForLandedResult(
            CombatRollResult.Critical(2m)
        );
    }

    [Fact]
    public void BlockedHit_DoesNotRefundAvoidanceRule()
    {
        AssertNoRefundForLandedResult(
            CombatRollResult.Block(5m)
        );
    }

    [Fact]
    public void RefundUsesAmountActuallyPaidForPercentCost()
    {
        var source = CreateActor("source");
        AddResource(source, "energy", 120m, 120m);
        var target = CreateActor("target", 1000m);
        var replacement = CreateReplacement(
            resourceKey: "energy",
            resourceAmount: 25m,
            isPercentOfMaximum: true
        );
        AddRefundRule(
            replacement,
            "energy",
            80m,
            CombatResultTypes.Miss
        );

        Run(
            source,
            target,
            CreateAutoAttack(),
            replacement,
            CombatRollResult.Miss()
        );

        // Paid 30 (25% of 120), then refunded 24 (80% of 30).
        Assert.Equal(114m, source.Resources["energy"].Current);
    }

    [Fact]
    public void MultipleResourceCosts_CanRefundIndependently()
    {
        var source = CreateActor("source");
        AddResource(source, "rage", 100m, 30m);
        AddResource(source, "mana", 100m, 40m);
        var target = CreateActor("target", 1000m);
        var replacement = CreateReplacement();

        replacement.ResourceCosts.Add(
            new AbilityResourceCost
            {
                ResourceKey = "mana",
                Amount = 20m
            }
        );

        AddRefundRule(
            replacement,
            "rage",
            80m,
            CombatResultTypes.Miss
        );
        AddRefundRule(
            replacement,
            "mana",
            50m,
            CombatResultTypes.Miss
        );

        Run(
            source,
            target,
            CreateAutoAttack(),
            replacement,
            CombatRollResult.Miss()
        );

        Assert.Equal(27m, source.Resources["rage"].Current);
        Assert.Equal(30m, source.Resources["mana"].Current);
    }

    [Fact]
    public void RefundEvent_ReportsPositiveAmountResultAndAbilityKey()
    {
        var source = CreateActor("source");
        AddResource(source, "rage", 100m, 30m);
        var target = CreateActor("target", 1000m);
        var replacement = CreateReplacement();
        AddRefundRule(
            replacement,
            "rage",
            80m,
            CombatResultTypes.Miss
        );

        var result = Run(
            source,
            target,
            CreateAutoAttack(),
            replacement,
            CombatRollResult.Miss()
        );

        var refundEvent = Assert.Single(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.ResourceChanged &&
                combatEvent.Amount > 0m
        );

        Assert.Equal(12m, refundEvent.Amount);
        Assert.Equal(replacement.Key, refundEvent.AbilityKey);
        Assert.Equal(CombatResultTypes.Miss, refundEvent.ResultKey);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void RefundPercentOutsideZeroToHundred_IsRejected(
        int refundPercent)
    {
        var source = CreateActor("source");
        AddResource(source, "rage", 100m, 30m);
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack();
        var replacement = CreateReplacement();
        AddRefundRule(
            replacement,
            "rage",
            refundPercent,
            CombatResultTypes.Miss
        );

        var context = CreateContext();
        context.AddActor(source);
        context.AddActor(target);
        var processor = CreateProcessor(CombatRollResult.Hit());

        Assert.True(processor.Start(
            context,
            source.Key,
            target.Key,
            mainHand
        ));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            processor.QueueNextSwingReplacement(
                context,
                source.Key,
                mainHand.Key,
                replacement
            )
        );
    }

    [Fact]
    public void RefundRuleForResourceWithoutCost_IsRejected()
    {
        var source = CreateActor("source");
        AddResource(source, "rage", 100m, 30m);
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack();
        var replacement = CreateReplacement();
        AddRefundRule(
            replacement,
            "mana",
            80m,
            CombatResultTypes.Miss
        );

        var context = CreateContext();
        context.AddActor(source);
        context.AddActor(target);
        var processor = CreateProcessor(CombatRollResult.Hit());

        Assert.True(processor.Start(
            context,
            source.Key,
            target.Key,
            mainHand
        ));

        Assert.Throws<ArgumentException>(() =>
            processor.QueueNextSwingReplacement(
                context,
                source.Key,
                mainHand.Key,
                replacement
            )
        );
    }

    [Fact]
    public void BlankRefundResultKey_IsRejected()
    {
        var source = CreateActor("source");
        AddResource(source, "rage", 100m, 30m);
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack();
        var replacement = CreateReplacement();
        AddRefundRule(
            replacement,
            "rage",
            80m,
            " "
        );

        var context = CreateContext();
        context.AddActor(source);
        context.AddActor(target);
        var processor = CreateProcessor(CombatRollResult.Hit());

        Assert.True(processor.Start(
            context,
            source.Key,
            target.Key,
            mainHand
        ));

        Assert.Throws<ArgumentException>(() =>
            processor.QueueNextSwingReplacement(
                context,
                source.Key,
                mainHand.Key,
                replacement
            )
        );
    }

    [Fact]
    public void DuplicateRefundRulesForSameResource_AreRejected()
    {
        var source = CreateActor("source");
        AddResource(source, "rage", 100m, 30m);
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack();
        var replacement = CreateReplacement();
        AddRefundRule(
            replacement,
            "rage",
            80m,
            CombatResultTypes.Miss
        );
        AddRefundRule(
            replacement,
            "RAGE",
            50m,
            CombatResultTypes.Dodge
        );

        var context = CreateContext();
        context.AddActor(source);
        context.AddActor(target);
        var processor = CreateProcessor(CombatRollResult.Hit());

        Assert.True(processor.Start(
            context,
            source.Key,
            target.Key,
            mainHand
        ));

        Assert.Throws<ArgumentException>(() =>
            processor.QueueNextSwingReplacement(
                context,
                source.Key,
                mainHand.Key,
                replacement
            )
        );
    }

    private static void AssertNoRefundForLandedResult(
        CombatRollResult combatRollResult)
    {
        var source = CreateActor("source");
        AddResource(source, "rage", 100m, 30m);
        var target = CreateActor("target", 1000m);
        var replacement = CreateReplacement();
        AddRefundRule(
            replacement,
            "rage",
            80m,
            CombatResultTypes.Miss,
            CombatResultTypes.Dodge,
            CombatResultTypes.Parry
        );

        Run(
            source,
            target,
            CreateAutoAttack(),
            replacement,
            combatRollResult
        );

        Assert.Equal(15m, source.Resources["rage"].Current);
    }

    private static SimulationRunResult Run(
        SimulationActorState source,
        SimulationActorState target,
        AutoAttackDefinition mainHand,
        NextSwingReplacementDefinition replacement,
        CombatRollResult combatRollResult)
    {
        var context = CreateContext(1.1m);
        context.AddActor(source);
        context.AddActor(target);
        var processor = CreateProcessor(combatRollResult);

        return new SimulationEngine([processor])
            .Run(
                context,
                startedContext =>
                {
                    Assert.True(processor.Start(
                        startedContext,
                        source.Key,
                        target.Key,
                        mainHand
                    ));
                    Assert.True(processor.QueueNextSwingReplacement(
                        startedContext,
                        source.Key,
                        mainHand.Key,
                        replacement
                    ));
                }
            );
    }

    private static AutoAttackProcessor CreateProcessor(
        CombatRollResult combatRollResult)
    {
        return new AutoAttackProcessor(
            new AbilityExecutor(
                new AuraManager(),
                new FixedCombatRollResolver(combatRollResult),
                new RulesetDamageMitigationResolver(
                    new CombatRulesetDefinition
                    {
                        RulesetKey = "next-swing-refund-tests",
                        Version = "1"
                    }
                )
            )
        );
    }

    private static AutoAttackDefinition CreateAutoAttack()
    {
        return new AutoAttackDefinition
        {
            Key = "main-hand",
            Name = "Main Hand",
            SwingIntervalSeconds = 1m,
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

    private static NextSwingReplacementDefinition CreateReplacement(
        string resourceKey = "rage",
        decimal resourceAmount = 15m,
        bool isPercentOfMaximum = false)
    {
        var replacement =
            new NextSwingReplacementDefinition
            {
                Key = "heroic-strike",
                Name = "Heroic Strike",
                WeaponHandKey = WeaponHandKeys.MainHand,
                DamageEffect =
                    new AbilityEffectDefinition
                    {
                        Key = "damage",
                        EffectType = AbilityEffectTypes.DirectDamage,
                        TargetType = AbilityTargetTypes.Enemy,
                        WeaponHandKey = WeaponHandKeys.MainHand,
                        MinimumValue = 25m,
                        MaximumValue = 25m,
                        MitigationType = DamageMitigationTypes.None
                    }
            };

        replacement.ResourceCosts.Add(
            new AbilityResourceCost
            {
                ResourceKey = resourceKey,
                Amount = resourceAmount,
                IsPercentOfMaximum = isPercentOfMaximum
            }
        );

        return replacement;
    }

    private static void AddRefundRule(
        NextSwingReplacementDefinition replacement,
        string resourceKey,
        decimal refundPercent,
        params string[] resultKeys)
    {
        replacement.ResourceRefunds.Add(
            new NextSwingResourceRefundDefinition
            {
                ResourceKey = resourceKey,
                RefundPercent = refundPercent,
                ResultKeys = [.. resultKeys]
            }
        );
    }

    private static void AddResource(
        SimulationActorState actor,
        string key,
        decimal maximum,
        decimal current)
    {
        actor.AddResource(
            new ResourceState(
                key,
                maximum,
                current
            )
        );
    }

    private static SimulationActorState CreateActor(
        string key,
        decimal health = 100m)
    {
        var actor =
            new SimulationActorState
            {
                Key = key,
                Name = key,
                TeamKey =
                    key == "source"
                        ? "players"
                        : "enemies",
                Level = 30
            };

        actor.InitializeHealth(health);

        return actor;
    }

    private static SimulationContext CreateContext(
        decimal durationSeconds = 5m)
    {
        return new SimulationContext(
            new SimulationRunOptions
            {
                Seed = 12345,
                DurationSeconds = durationSeconds,
                PrimaryActorKey = "source",
                CaptureTimeline = true
            }
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
}

// ==================== NextSwingReplacementTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

public sealed class NextSwingReplacementTests
{
    [Fact]
    public void QueuedReplacement_ReplacesExactlyOneMatchingSwing()
    {
        var source = CreateActor("source");
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack(
            "main-hand",
            WeaponHandKeys.MainHand,
            1m,
            10m
        );
        var replacement = CreateReplacement(
            "heroic-strike",
            WeaponHandKeys.MainHand,
            25m
        );

        var result = Run(
            source,
            target,
            2.1m,
            (context, processor) =>
            {
                Assert.True(processor.Start(
                    context,
                    source.Key,
                    target.Key,
                    mainHand
                ));

                Assert.True(processor.QueueNextSwingReplacement(
                    context,
                    source.Key,
                    mainHand.Key,
                    replacement
                ));
            }
        );

        var damage = DamageEvents(result);

        Assert.Equal(2, damage.Count);
        Assert.Equal(replacement.Key, damage[0].AbilityKey);
        Assert.Equal(25m, damage[0].Amount);
        Assert.Equal(mainHand.Key, damage[1].AbilityKey);
        Assert.Equal(10m, damage[1].Amount);
    }

    [Fact]
    public void QueuedReplacement_IsReportedUnderItsOwnAbilitySummary()
    {
        var source = CreateActor("source");
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack(
            "main-hand",
            WeaponHandKeys.MainHand,
            1m,
            10m
        );
        var replacement = CreateReplacement(
            "heroic-strike",
            WeaponHandKeys.MainHand,
            25m
        );

        var result = Run(
            source,
            target,
            1.1m,
            (context, processor) =>
            {
                Assert.True(processor.Start(
                    context,
                    source.Key,
                    target.Key,
                    mainHand
                ));

                Assert.True(processor.QueueNextSwingReplacement(
                    context,
                    source.Key,
                    mainHand.Key,
                    replacement
                ));
            }
        );

        var summary =
            result.Summary.Abilities[replacement.Key];

        Assert.Equal(1, summary.DamageOccurrenceCount);
        Assert.Equal(25m, summary.DamageDone);
        Assert.False(
            result.Summary.Abilities.ContainsKey(mainHand.Key)
        );
    }

    [Fact]
    public void CancelledReplacement_AllowsNormalSwing()
    {
        var source = CreateActor("source");
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack(
            "main-hand",
            WeaponHandKeys.MainHand,
            1m,
            10m
        );
        var replacement = CreateReplacement(
            "heroic-strike",
            WeaponHandKeys.MainHand,
            25m
        );

        var result = Run(
            source,
            target,
            1.1m,
            (context, processor) =>
            {
                Assert.True(processor.Start(
                    context,
                    source.Key,
                    target.Key,
                    mainHand
                ));

                Assert.True(processor.QueueNextSwingReplacement(
                    context,
                    source.Key,
                    mainHand.Key,
                    replacement
                ));

                Assert.True(processor.CancelNextSwingReplacement(
                    context,
                    source.Key,
                    mainHand.Key
                ));
            }
        );

        var damage = Assert.Single(DamageEvents(result));

        Assert.Equal(mainHand.Key, damage.AbilityKey);
        Assert.Equal(10m, damage.Amount);
    }

    [Fact]
    public void QueueingReplacementForDifferentWeaponHandFails()
    {
        var source = CreateActor("source");
        var target = CreateActor("target");
        var mainHand = CreateAutoAttack(
            "main-hand",
            WeaponHandKeys.MainHand,
            1m,
            10m
        );
        var offHandReplacement = CreateReplacement(
            "off-hand-special",
            WeaponHandKeys.OffHand,
            25m
        );

        var context = CreateContext();
        context.AddActor(source);
        context.AddActor(target);
        var processor = CreateProcessor();

        Assert.True(processor.Start(
            context,
            source.Key,
            target.Key,
            mainHand
        ));

        Assert.Throws<InvalidOperationException>(() =>
            processor.QueueNextSwingReplacement(
                context,
                source.Key,
                mainHand.Key,
                offHandReplacement
            )
        );
    }

    [Fact]
    public void ReplacementRequiresMatchingEffectWeaponHand()
    {
        var source = CreateActor("source");
        var target = CreateActor("target");
        var mainHand = CreateAutoAttack(
            "main-hand",
            WeaponHandKeys.MainHand,
            1m,
            10m
        );
        var replacement = CreateReplacement(
            "heroic-strike",
            WeaponHandKeys.MainHand,
            25m
        );

        replacement.DamageEffect.WeaponHandKey =
            WeaponHandKeys.OffHand;

        var context = CreateContext();
        context.AddActor(source);
        context.AddActor(target);
        var processor = CreateProcessor();

        Assert.True(processor.Start(
            context,
            source.Key,
            target.Key,
            mainHand
        ));

        Assert.Throws<ArgumentException>(() =>
            processor.QueueNextSwingReplacement(
                context,
                source.Key,
                mainHand.Key,
                replacement
            )
        );
    }

    [Fact]
    public void QueueingAgain_ReplacesThePreviouslyQueuedAttack()
    {
        var source = CreateActor("source");
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack(
            "main-hand",
            WeaponHandKeys.MainHand,
            1m,
            10m
        );
        var first = CreateReplacement(
            "first-special",
            WeaponHandKeys.MainHand,
            20m
        );
        var second = CreateReplacement(
            "second-special",
            WeaponHandKeys.MainHand,
            30m
        );

        var result = Run(
            source,
            target,
            1.1m,
            (context, processor) =>
            {
                Assert.True(processor.Start(
                    context,
                    source.Key,
                    target.Key,
                    mainHand
                ));
                Assert.True(processor.QueueNextSwingReplacement(
                    context,
                    source.Key,
                    mainHand.Key,
                    first
                ));
                Assert.True(processor.QueueNextSwingReplacement(
                    context,
                    source.Key,
                    mainHand.Key,
                    second
                ));
            }
        );

        var damage = Assert.Single(DamageEvents(result));

        Assert.Equal(second.Key, damage.AbilityKey);
        Assert.DoesNotContain(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.Damage &&
                combatEvent.AbilityKey == first.Key
        );
    }

    [Fact]
    public void RestartingSwingStream_InvalidatesQueuedReplacement()
    {
        var source = CreateActor("source");
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack(
            "main-hand",
            WeaponHandKeys.MainHand,
            1m,
            10m
        );
        var replacement = CreateReplacement(
            "heroic-strike",
            WeaponHandKeys.MainHand,
            25m
        );

        var result = Run(
            source,
            target,
            1.1m,
            (context, processor) =>
            {
                Assert.True(processor.Start(
                    context,
                    source.Key,
                    target.Key,
                    mainHand
                ));
                Assert.True(processor.QueueNextSwingReplacement(
                    context,
                    source.Key,
                    mainHand.Key,
                    replacement
                ));

                Assert.True(processor.Start(
                    context,
                    source.Key,
                    target.Key,
                    mainHand
                ));
            }
        );

        var damage = Assert.Single(DamageEvents(result));

        Assert.Equal(mainHand.Key, damage.AbilityKey);
    }

    [Fact]
    public void StoppingSwingStream_ClearsQueuedReplacement()
    {
        var source = CreateActor("source");
        var target = CreateActor("target");
        var mainHand = CreateAutoAttack(
            "main-hand",
            WeaponHandKeys.MainHand,
            1m,
            10m
        );
        var replacement = CreateReplacement(
            "heroic-strike",
            WeaponHandKeys.MainHand,
            25m
        );

        var context = CreateContext();
        context.AddActor(source);
        context.AddActor(target);
        var processor = CreateProcessor();

        Assert.True(processor.Start(
            context,
            source.Key,
            target.Key,
            mainHand
        ));
        Assert.True(processor.QueueNextSwingReplacement(
            context,
            source.Key,
            mainHand.Key,
            replacement
        ));
        Assert.True(processor.Stop(
            context,
            source.Key,
            mainHand.Key
        ));

        Assert.Null(
            source.AutoAttacks[mainHand.Key]
                .QueuedNextSwingReplacement
        );
        Assert.False(processor.CancelNextSwingReplacement(
            context,
            source.Key,
            mainHand.Key
        ));
    }

    [Fact]
    public void MainHandQueue_DoesNotConsumeOffHandSwing()
    {
        var source = CreateActor("source");
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack(
            "main-hand",
            WeaponHandKeys.MainHand,
            1m,
            10m
        );
        var offHand = CreateAutoAttack(
            "off-hand",
            WeaponHandKeys.OffHand,
            1m,
            5m
        );
        var replacement = CreateReplacement(
            "heroic-strike",
            WeaponHandKeys.MainHand,
            25m
        );

        var result = Run(
            source,
            target,
            1.1m,
            (context, processor) =>
            {
                Assert.True(processor.Start(
                    context,
                    source.Key,
                    target.Key,
                    mainHand
                ));
                Assert.True(processor.Start(
                    context,
                    source.Key,
                    target.Key,
                    offHand
                ));
                Assert.True(processor.QueueNextSwingReplacement(
                    context,
                    source.Key,
                    mainHand.Key,
                    replacement
                ));
            }
        );

        var replacementDamage =
            Assert.Single(
                DamageEvents(result),
                combatEvent =>
                    combatEvent.AbilityKey == replacement.Key
            );

        var offHandDamage =
            Assert.Single(
                DamageEvents(result),
                combatEvent =>
                    combatEvent.AbilityKey == offHand.Key
            );

        Assert.Equal(25m, replacementDamage.Amount);
        Assert.Equal(5m, offHandDamage.Amount);
    }

    [Fact]
    public void MainHandReplacementCombatProfile_DoesNotUpgradeOffHandHitResult()
    {
        var source = CreateActor("source");
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack(
            "main-hand",
            WeaponHandKeys.MainHand,
            1m,
            10m,
            alwaysHits: false
        );
        var offHand = CreateAutoAttack(
            "off-hand",
            WeaponHandKeys.OffHand,
            1m,
            5m,
            alwaysHits: false
        );
        var replacement = CreateReplacement(
            "heroic-strike",
            WeaponHandKeys.MainHand,
            25m,
            alwaysHits: true
        );

        var result = Run(
            source,
            target,
            1.1m,
            (context, processor) =>
            {
                Assert.True(processor.Start(
                    context,
                    source.Key,
                    target.Key,
                    mainHand
                ));
                Assert.True(processor.Start(
                    context,
                    source.Key,
                    target.Key,
                    offHand
                ));
                Assert.True(processor.QueueNextSwingReplacement(
                    context,
                    source.Key,
                    mainHand.Key,
                    replacement
                ));
            },
            hitChancePercent: 0m
        );

        var replacementDamage =
            Assert.Single(
                DamageEvents(result),
                combatEvent =>
                    combatEvent.AbilityKey == replacement.Key
            );

        var offHandDamage =
            Assert.Single(
                DamageEvents(result),
                combatEvent =>
                    combatEvent.AbilityKey == offHand.Key
            );

        Assert.Equal(25m, replacementDamage.Amount);
        Assert.Equal(CombatResultTypes.Hit, replacementDamage.ResultKey);
        Assert.Equal(0m, offHandDamage.Amount);
        Assert.Equal(CombatResultTypes.Miss, offHandDamage.ResultKey);
    }

    private static SimulationRunResult Run(
        SimulationActorState source,
        SimulationActorState target,
        decimal durationSeconds,
        Action<SimulationContext, AutoAttackProcessor> onStarted,
        decimal hitChancePercent = 100m)
    {
        var context = CreateContext(durationSeconds);
        context.AddActor(source);
        context.AddActor(target);

        var processor = CreateProcessor(hitChancePercent);

        return new SimulationEngine(
            [
                processor
            ])
            .Run(
                context,
                startedContext =>
                    onStarted(
                        startedContext,
                        processor
                    )
            );
    }

    private static AutoAttackProcessor CreateProcessor(
        decimal hitChancePercent = 100m)
    {
        var executor =
            new AbilityExecutor(
                new AuraManager(),
                new SimpleCombatRollResolver(
                    hitChancePercent:
                        hitChancePercent
                ),
                new RulesetDamageMitigationResolver(
                    new CombatRulesetDefinition
                    {
                        RulesetKey = "next-swing-tests",
                        Version = "1"
                    }
                )
            );

        return new AutoAttackProcessor(executor);
    }

    private static AutoAttackDefinition CreateAutoAttack(
        string key,
        string weaponHandKey,
        decimal swingIntervalSeconds,
        decimal damage,
        bool alwaysHits = true)
    {
        return new AutoAttackDefinition
        {
            Key = key,
            Name = key,
            SwingIntervalSeconds = swingIntervalSeconds,
            WeaponHandKey = weaponHandKey,
            DamageEffect =
                new AbilityEffectDefinition
                {
                    Key = "damage",
                    EffectType = AbilityEffectTypes.DirectDamage,
                    TargetType = AbilityTargetTypes.Enemy,
                    WeaponHandKey = weaponHandKey,
                    ResolutionType =
                        alwaysHits
                            ? CombatResolutionTypes.AlwaysHits
                            : CombatResolutionTypes.Melee,
                    CanMiss = !alwaysHits,
                    CanCrit = false,
                    MinimumValue = damage,
                    MaximumValue = damage,
                    MitigationType = DamageMitigationTypes.None
                }
        };
    }

    private static NextSwingReplacementDefinition CreateReplacement(
        string key,
        string weaponHandKey,
        decimal damage,
        bool alwaysHits = true)
    {
        return new NextSwingReplacementDefinition
        {
            Key = key,
            Name = key,
            WeaponHandKey = weaponHandKey,
            DamageEffect =
                new AbilityEffectDefinition
                {
                    Key = "damage",
                    EffectType = AbilityEffectTypes.DirectDamage,
                    TargetType = AbilityTargetTypes.Enemy,
                    WeaponHandKey = weaponHandKey,
                    ResolutionType =
                        alwaysHits
                            ? CombatResolutionTypes.AlwaysHits
                            : CombatResolutionTypes.Melee,
                    CanMiss = !alwaysHits,
                    CanCrit = false,
                    MinimumValue = damage,
                    MaximumValue = damage,
                    MitigationType = DamageMitigationTypes.None
                }
        };
    }

    private static List<CombatEvent> DamageEvents(
        SimulationRunResult result)
    {
        return result.Timeline
            .Where(combatEvent =>
                combatEvent.Type == CombatEventType.Damage)
            .ToList();
    }

    private static SimulationActorState CreateActor(
        string key,
        decimal health = 100m)
    {
        var actor =
            new SimulationActorState
            {
                Key = key,
                Name = key,
                TeamKey =
                    key == "source"
                        ? "players"
                        : "enemies",
                Level = 30
            };

        actor.InitializeHealth(health);

        return actor;
    }

    private static SimulationContext CreateContext(
        decimal durationSeconds = 5m)
    {
        return new SimulationContext(
            new SimulationRunOptions
            {
                Seed = 12345,
                DurationSeconds = durationSeconds,
                PrimaryActorKey = "source",
                CaptureTimeline = true
            }
        );
    }
}
}

// ==================== NextSwingResourceCostTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

public sealed class NextSwingResourceCostTests
{
    [Fact]
    public void QueueingReplacement_DoesNotSpendResourceBeforeSwing()
    {
        var source = CreateActor("source");
        AddResource(source, "rage", 100m, 30m);
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack(1m, 10m);
        var replacement = CreateReplacement(25m, "rage", 15m);

        var context = CreateContext();
        context.AddActor(source);
        context.AddActor(target);
        var processor = CreateProcessor();

        Assert.True(processor.Start(
            context,
            source.Key,
            target.Key,
            mainHand
        ));
        Assert.True(processor.QueueNextSwingReplacement(
            context,
            source.Key,
            mainHand.Key,
            replacement
        ));

        Assert.Equal(30m, source.Resources["rage"].Current);
    }

    [Fact]
    public void CancellingQueuedReplacement_DoesNotSpendResource()
    {
        var source = CreateActor("source");
        AddResource(source, "rage", 100m, 30m);
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack(1m, 10m);
        var replacement = CreateReplacement(25m, "rage", 15m);

        var context = CreateContext();
        context.AddActor(source);
        context.AddActor(target);
        var processor = CreateProcessor();

        Assert.True(processor.Start(
            context,
            source.Key,
            target.Key,
            mainHand
        ));
        Assert.True(processor.QueueNextSwingReplacement(
            context,
            source.Key,
            mainHand.Key,
            replacement
        ));
        Assert.True(processor.CancelNextSwingReplacement(
            context,
            source.Key,
            mainHand.Key
        ));

        Assert.Equal(30m, source.Resources["rage"].Current);
    }

    [Fact]
    public void QueueingReplacement_FailsWhenRequiredResourceIsMissing()
    {
        var source = CreateActor("source");
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack(1m, 10m);
        var replacement = CreateReplacement(25m, "rage", 15m);

        var context = CreateContext();
        context.AddActor(source);
        context.AddActor(target);
        var processor = CreateProcessor();

        Assert.True(processor.Start(
            context,
            source.Key,
            target.Key,
            mainHand
        ));

        Assert.False(processor.QueueNextSwingReplacement(
            context,
            source.Key,
            mainHand.Key,
            replacement
        ));
        Assert.Null(
            source.AutoAttacks[mainHand.Key]
                .QueuedNextSwingReplacement
        );
    }

    [Fact]
    public void QueueingReplacement_FailsWhenResourceIsInsufficient()
    {
        var source = CreateActor("source");
        AddResource(source, "rage", 100m, 14m);
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack(1m, 10m);
        var replacement = CreateReplacement(25m, "rage", 15m);

        var context = CreateContext();
        context.AddActor(source);
        context.AddActor(target);
        var processor = CreateProcessor();

        Assert.True(processor.Start(
            context,
            source.Key,
            target.Key,
            mainHand
        ));

        Assert.False(processor.QueueNextSwingReplacement(
            context,
            source.Key,
            mainHand.Key,
            replacement
        ));
        Assert.Equal(14m, source.Resources["rage"].Current);
    }

    [Fact]
    public void ConsumedReplacement_SpendsResourceAtSwingAndReportsChange()
    {
        var source = CreateActor("source");
        AddResource(source, "rage", 100m, 30m);
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack(1m, 10m);
        var replacement = CreateReplacement(25m, "rage", 15m);

        var result = Run(
            source,
            target,
            1.1m,
            (context, processor) =>
            {
                Assert.True(processor.Start(
                    context,
                    source.Key,
                    target.Key,
                    mainHand
                ));
                Assert.True(processor.QueueNextSwingReplacement(
                    context,
                    source.Key,
                    mainHand.Key,
                    replacement
                ));
            }
        );

        Assert.Equal(15m, source.Resources["rage"].Current);

        var resourceChange = Assert.Single(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.ResourceChanged
        );

        Assert.Equal(replacement.Key, resourceChange.AbilityKey);
        Assert.Equal(-15m, resourceChange.Amount);

        var damage = Assert.Single(DamageEvents(result));
        Assert.Equal(replacement.Key, damage.AbilityKey);
        Assert.Equal(25m, damage.Amount);
    }

    [Fact]
    public void InsufficientResourceAtSwing_FallsBackToWhiteSwing()
    {
        var source = CreateActor("source");
        AddResource(source, "rage", 100m, 20m);
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack(1m, 10m);
        var replacement = CreateReplacement(25m, "rage", 15m);

        var result = Run(
            source,
            target,
            1.1m,
            (context, processor) =>
            {
                Assert.True(processor.Start(
                    context,
                    source.Key,
                    target.Key,
                    mainHand
                ));
                Assert.True(processor.QueueNextSwingReplacement(
                    context,
                    source.Key,
                    mainHand.Key,
                    replacement
                ));

                Assert.True(source.Resources["rage"].Spend(10m));
            }
        );

        Assert.Equal(10m, source.Resources["rage"].Current);
        Assert.DoesNotContain(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.ResourceChanged
        );

        var damage = Assert.Single(DamageEvents(result));
        Assert.Equal(mainHand.Key, damage.AbilityKey);
        Assert.Equal(10m, damage.Amount);
    }

    [Fact]
    public void PercentOfMaximumResourceCost_IsPaidWhenSwingExecutes()
    {
        var source = CreateActor("source");
        AddResource(source, "energy", 120m, 120m);
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack(1m, 10m);
        var replacement = CreateReplacement(
            25m,
            "energy",
            25m,
            isPercentOfMaximum: true
        );

        Run(
            source,
            target,
            1.1m,
            (context, processor) =>
            {
                Assert.True(processor.Start(
                    context,
                    source.Key,
                    target.Key,
                    mainHand
                ));
                Assert.True(processor.QueueNextSwingReplacement(
                    context,
                    source.Key,
                    mainHand.Key,
                    replacement
                ));
            }
        );

        Assert.Equal(90m, source.Resources["energy"].Current);
    }

    [Fact]
    public void MultipleResourceCosts_ArePaidTogetherWhenSwingExecutes()
    {
        var source = CreateActor("source");
        AddResource(source, "rage", 100m, 30m);
        AddResource(source, "mana", 100m, 40m);
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack(1m, 10m);
        var replacement = CreateReplacement(25m, "rage", 15m);
        replacement.ResourceCosts.Add(
            new AbilityResourceCost
            {
                ResourceKey = "mana",
                Amount = 20m
            }
        );

        Run(
            source,
            target,
            1.1m,
            (context, processor) =>
            {
                Assert.True(processor.Start(
                    context,
                    source.Key,
                    target.Key,
                    mainHand
                ));
                Assert.True(processor.QueueNextSwingReplacement(
                    context,
                    source.Key,
                    mainHand.Key,
                    replacement
                ));
            }
        );

        Assert.Equal(15m, source.Resources["rage"].Current);
        Assert.Equal(20m, source.Resources["mana"].Current);
    }

    [Fact]
    public void MultipleResourceCosts_AreAtomicWhenOneBecomesUnavailable()
    {
        var source = CreateActor("source");
        AddResource(source, "rage", 100m, 30m);
        AddResource(source, "mana", 100m, 25m);
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack(1m, 10m);
        var replacement = CreateReplacement(25m, "rage", 15m);
        replacement.ResourceCosts.Add(
            new AbilityResourceCost
            {
                ResourceKey = "mana",
                Amount = 20m
            }
        );

        var result = Run(
            source,
            target,
            1.1m,
            (context, processor) =>
            {
                Assert.True(processor.Start(
                    context,
                    source.Key,
                    target.Key,
                    mainHand
                ));
                Assert.True(processor.QueueNextSwingReplacement(
                    context,
                    source.Key,
                    mainHand.Key,
                    replacement
                ));

                Assert.True(source.Resources["mana"].Spend(10m));
            }
        );

        Assert.Equal(30m, source.Resources["rage"].Current);
        Assert.Equal(15m, source.Resources["mana"].Current);
        Assert.DoesNotContain(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.ResourceChanged
        );
        Assert.Equal(
            mainHand.Key,
            Assert.Single(DamageEvents(result)).AbilityKey
        );
    }

    [Fact]
    public void FailedRequeue_DoesNotDiscardPreviouslyQueuedReplacement()
    {
        var source = CreateActor("source");
        AddResource(source, "rage", 100m, 20m);
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack(1m, 10m);
        var affordable = CreateReplacement(25m, "rage", 10m);
        affordable.Key = "affordable";
        affordable.Name = "affordable";
        var unaffordable = CreateReplacement(40m, "rage", 30m);
        unaffordable.Key = "unaffordable";
        unaffordable.Name = "unaffordable";

        var result = Run(
            source,
            target,
            1.1m,
            (context, processor) =>
            {
                Assert.True(processor.Start(
                    context,
                    source.Key,
                    target.Key,
                    mainHand
                ));
                Assert.True(processor.QueueNextSwingReplacement(
                    context,
                    source.Key,
                    mainHand.Key,
                    affordable
                ));
                Assert.False(processor.QueueNextSwingReplacement(
                    context,
                    source.Key,
                    mainHand.Key,
                    unaffordable
                ));
            }
        );

        Assert.Equal(10m, source.Resources["rage"].Current);
        Assert.Equal(
            affordable.Key,
            Assert.Single(DamageEvents(result)).AbilityKey
        );
    }

    [Fact]
    public void OffHandSwing_DoesNotSpendQueuedMainHandResourceCost()
    {
        var source = CreateActor("source");
        AddResource(source, "rage", 100m, 30m);
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack(
            2m,
            10m,
            "main-hand",
            WeaponHandKeys.MainHand
        );
        var offHand = CreateAutoAttack(
            1m,
            5m,
            "off-hand",
            WeaponHandKeys.OffHand
        );
        var replacement = CreateReplacement(25m, "rage", 15m);

        var result = Run(
            source,
            target,
            1.1m,
            (context, processor) =>
            {
                Assert.True(processor.Start(
                    context,
                    source.Key,
                    target.Key,
                    mainHand
                ));
                Assert.True(processor.Start(
                    context,
                    source.Key,
                    target.Key,
                    offHand
                ));
                Assert.True(processor.QueueNextSwingReplacement(
                    context,
                    source.Key,
                    mainHand.Key,
                    replacement
                ));
            }
        );

        Assert.Equal(30m, source.Resources["rage"].Current);
        Assert.DoesNotContain(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.ResourceChanged
        );
        Assert.Equal(
            offHand.Key,
            Assert.Single(DamageEvents(result)).AbilityKey
        );
    }

    [Fact]
    public void NegativeReplacementResourceCost_IsRejected()
    {
        var source = CreateActor("source");
        AddResource(source, "rage", 100m, 30m);
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack(1m, 10m);
        var replacement = CreateReplacement(25m, "rage", -1m);

        var context = CreateContext();
        context.AddActor(source);
        context.AddActor(target);
        var processor = CreateProcessor();

        Assert.True(processor.Start(
            context,
            source.Key,
            target.Key,
            mainHand
        ));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            processor.QueueNextSwingReplacement(
                context,
                source.Key,
                mainHand.Key,
                replacement
            )
        );
    }

    private static SimulationRunResult Run(
        SimulationActorState source,
        SimulationActorState target,
        decimal durationSeconds,
        Action<SimulationContext, AutoAttackProcessor> onStarted)
    {
        var context = CreateContext(durationSeconds);
        context.AddActor(source);
        context.AddActor(target);
        var processor = CreateProcessor();

        return new SimulationEngine([processor])
            .Run(
                context,
                startedContext =>
                    onStarted(startedContext, processor)
            );
    }

    private static AutoAttackProcessor CreateProcessor()
    {
        return new AutoAttackProcessor(
            new AbilityExecutor(
                new AuraManager(),
                new SimpleCombatRollResolver(),
                new RulesetDamageMitigationResolver(
                    new CombatRulesetDefinition
                    {
                        RulesetKey = "next-swing-resource-tests",
                        Version = "1"
                    }
                )
            )
        );
    }

    private static AutoAttackDefinition CreateAutoAttack(
        decimal swingIntervalSeconds,
        decimal damage,
        string key = "main-hand",
        string weaponHandKey = WeaponHandKeys.MainHand)
    {
        return new AutoAttackDefinition
        {
            Key = key,
            Name = key,
            SwingIntervalSeconds = swingIntervalSeconds,
            WeaponHandKey = weaponHandKey,
            DamageEffect =
                new AbilityEffectDefinition
                {
                    Key = "damage",
                    EffectType = AbilityEffectTypes.DirectDamage,
                    TargetType = AbilityTargetTypes.Enemy,
                    WeaponHandKey = weaponHandKey,
                    ResolutionType = CombatResolutionTypes.AlwaysHits,
                    CanMiss = false,
                    CanCrit = false,
                    MinimumValue = damage,
                    MaximumValue = damage,
                    MitigationType = DamageMitigationTypes.None
                }
        };
    }

    private static NextSwingReplacementDefinition CreateReplacement(
        decimal damage,
        string resourceKey,
        decimal resourceAmount,
        bool isPercentOfMaximum = false)
    {
        var replacement =
            new NextSwingReplacementDefinition
            {
                Key = "heroic-strike",
                Name = "Heroic Strike",
                WeaponHandKey = WeaponHandKeys.MainHand,
                DamageEffect =
                    new AbilityEffectDefinition
                    {
                        Key = "damage",
                        EffectType = AbilityEffectTypes.DirectDamage,
                        TargetType = AbilityTargetTypes.Enemy,
                        WeaponHandKey = WeaponHandKeys.MainHand,
                        ResolutionType = CombatResolutionTypes.AlwaysHits,
                        CanMiss = false,
                        CanCrit = false,
                        MinimumValue = damage,
                        MaximumValue = damage,
                        MitigationType = DamageMitigationTypes.None
                    }
            };

        replacement.ResourceCosts.Add(
            new AbilityResourceCost
            {
                ResourceKey = resourceKey,
                Amount = resourceAmount,
                IsPercentOfMaximum = isPercentOfMaximum
            }
        );

        return replacement;
    }

    private static void AddResource(
        SimulationActorState actor,
        string key,
        decimal maximum,
        decimal current)
    {
        actor.AddResource(
            new ResourceState(
                key,
                maximum,
                current
            )
        );
    }

    private static List<CombatEvent> DamageEvents(
        SimulationRunResult result)
    {
        return result.Timeline
            .Where(combatEvent =>
                combatEvent.Type == CombatEventType.Damage)
            .ToList();
    }

    private static SimulationActorState CreateActor(
        string key,
        decimal health = 100m)
    {
        var actor =
            new SimulationActorState
            {
                Key = key,
                Name = key,
                TeamKey =
                    key == "source"
                        ? "players"
                        : "enemies",
                Level = 30
            };

        actor.InitializeHealth(health);

        return actor;
    }

    private static SimulationContext CreateContext(
        decimal durationSeconds = 5m)
    {
        return new SimulationContext(
            new SimulationRunOptions
            {
                Seed = 12345,
                DurationSeconds = durationSeconds,
                PrimaryActorKey = "source",
                CaptureTimeline = true
            }
        );
    }
}
}

// ==================== NextSwingRotationIntegrationTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Validation;
using WarcraftSim.Data.Forever.Abilities.Warrior;

public sealed class NextSwingRotationIntegrationTests
{
    [Fact]
    public void RotationEntry_DefaultsToNormalAbilityAction()
    {
        var entry = new RotationEntry();

        Assert.Equal(
            RotationActionTypes.Ability,
            entry.ActionType
        );
    }

    [Fact]
    public void Rotation_QueuesNextSwingReplacementAndConsumesItOnSwing()
    {
        var source = CreateActor("source");
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack("main-hand", 1m, 10m);
        var replacement = CreateReplacement("heroic-strike", 25m);

        source.AddNextSwingReplacement(replacement);

        var result = Run(
            source,
            target,
            CreateQueueRotation(mainHand.Key, replacement.Key),
            durationSeconds: 1.1m,
            mainHand
        );

        var damage = Assert.Single(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.Damage &&
                combatEvent.AbilityKey == replacement.Key
        );

        Assert.Equal(25m, damage.Amount);
        Assert.DoesNotContain(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.Damage &&
                combatEvent.AbilityKey == mainHand.Key
        );
    }

    [Fact]
    public void QueuedAction_DoesNotPreventForegroundAbilityInSameDecision()
    {
        var source = CreateActor("source");
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack("main-hand", 1m, 10m);
        var replacement = CreateReplacement("heroic-strike", 25m);
        var foreground = CreateDirectAbility("slam", 5m);

        source.AddNextSwingReplacement(replacement);
        source.AddAbility(foreground);

        var rotation = CreateQueueRotation(mainHand.Key, replacement.Key);
        rotation.Entries.Add(
            new RotationEntry
            {
                ActionType = RotationActionTypes.Ability,
                AbilityKey = foreground.Key,
                Priority = 2
            }
        );

        var result = Run(
            source,
            target,
            rotation,
            durationSeconds: 1.1m,
            mainHand
        );

        Assert.Contains(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.AbilityCastStarted &&
                combatEvent.AbilityKey == foreground.Key &&
                combatEvent.TimeSeconds == 0m
        );

        Assert.Contains(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.Damage &&
                combatEvent.AbilityKey == replacement.Key
        );
    }

    [Fact]
    public void Rotation_RequeuesReplacementAfterEachMatchingSwing()
    {
        var source = CreateActor("source");
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack("main-hand", 1m, 10m);
        var replacement = CreateReplacement("heroic-strike", 25m);

        source.AddNextSwingReplacement(replacement);

        var result = Run(
            source,
            target,
            CreateQueueRotation(mainHand.Key, replacement.Key),
            durationSeconds: 2.1m,
            mainHand
        );

        Assert.Equal(
            2,
            result.Timeline.Count(combatEvent =>
                combatEvent.Type == CombatEventType.Damage &&
                combatEvent.AbilityKey == replacement.Key)
        );

        Assert.DoesNotContain(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.Damage &&
                combatEvent.AbilityKey == mainHand.Key
        );
    }

    [Fact]
    public void Rotation_DoesNotQueueReplacementWhenResourceIsUnavailable()
    {
        var source = CreateActor("source");
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack("main-hand", 1m, 10m);
        var replacement = CreateReplacement(
            "heroic-strike",
            25m,
            rageCost: 15m
        );

        source.AddResource(
            new ResourceState(
                "rage",
                100m,
                0m,
                0m
            )
        );
        source.AddNextSwingReplacement(replacement);

        var result = Run(
            source,
            target,
            CreateQueueRotation(mainHand.Key, replacement.Key),
            durationSeconds: 1.1m,
            mainHand
        );

        var whiteSwing = Assert.Single(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.Damage &&
                combatEvent.AbilityKey == mainHand.Key
        );

        Assert.Equal(10m, whiteSwing.Amount);
        Assert.DoesNotContain(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.Damage &&
                combatEvent.AbilityKey == replacement.Key
        );
    }

    [Fact]
    public void ResourceChange_ReevaluatesQueuedActionAffordability()
    {
        var source = CreateActor("source");
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack("main-hand", 1m, 10m);
        var replacement = CreateReplacement(
            "heroic-strike",
            25m,
            rageCost: 15m
        );
        var rageGenerator = CreateRageGenerator("bloodrage-test", 20m);

        source.AddResource(
            new ResourceState(
                "rage",
                100m,
                0m,
                0m
            )
        );
        source.AddNextSwingReplacement(replacement);
        source.AddAbility(rageGenerator);

        var rotation = CreateQueueRotation(mainHand.Key, replacement.Key);
        rotation.Entries.Add(
            new RotationEntry
            {
                AbilityKey = rageGenerator.Key,
                Priority = 2
            }
        );

        var result = Run(
            source,
            target,
            rotation,
            durationSeconds: 1.1m,
            mainHand
        );

        Assert.Contains(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.ResourceChanged &&
                combatEvent.AbilityKey == rageGenerator.Key &&
                combatEvent.Amount == 20m
        );

        Assert.Contains(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.Damage &&
                combatEvent.AbilityKey == replacement.Key
        );
    }

    [Fact]
    public void QueueAction_RequiresItsSelectedTargetToMatchSwingTarget()
    {
        var source = CreateActor("source");
        var swingTarget = CreateActor("target", 1000m);
        var otherTarget = CreateActor("other-target", 1000m);
        var mainHand = CreateAutoAttack("main-hand", 1m, 10m);
        var replacement = CreateReplacement("heroic-strike", 25m);

        source.AddNextSwingReplacement(replacement);

        var rotation = CreateQueueRotation(mainHand.Key, replacement.Key);
        rotation.Entries[0].Target =
            new RotationTargetDefinition
            {
                Mode = RotationTargetSelectionModes.Fixed,
                ActorKey = otherTarget.Key
            };

        var result = Run(
            source,
            swingTarget,
            rotation,
            durationSeconds: 1.1m,
            mainHand,
            additionalActors: [otherTarget]
        );

        Assert.Contains(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.Damage &&
                combatEvent.AbilityKey == mainHand.Key
        );

        Assert.DoesNotContain(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.Damage &&
                combatEvent.AbilityKey == replacement.Key
        );
    }

    [Fact]
    public void ForeverHeroicStrike_CanBeSelectedByRotation()
    {
        var source = CreateActor("source");
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack("main-hand", 1m, 10m);

        source.AddResource(
            new ResourceState(
                ForeverHeroicStrikeFactory.RageResourceKey,
                100m,
                100m,
                0m
            )
        );

        var heroicStrike =
            ForeverHeroicStrikeFactory.CreateForLevel(
                characterLevel: 30,
                minimumWeaponDamage: 10m,
                maximumWeaponDamage: 10m,
                weaponSpeedSeconds: 1m,
                attackSkillStatKey: "sword-skill"
            );

        source.AddNextSwingReplacement(heroicStrike);

        var result = Run(
            source,
            target,
            CreateQueueRotation(mainHand.Key, heroicStrike.Key),
            durationSeconds: 1.1m,
            mainHand
        );

        Assert.Contains(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.Damage &&
                combatEvent.AbilityKey == ForeverHeroicStrikeFactory.AbilityKey
        );

        Assert.Equal(
            85m,
            source.Resources[ForeverHeroicStrikeFactory.RageResourceKey].Current
        );
    }

    [Fact]
    public void Validation_RejectsUnknownRotationActionType()
    {
        var source = CreateActor("source");
        var target = CreateActor("target");
        var rotation = new RotationProfile
        {
            Name = "Invalid Action",
            Entries =
            [
                new RotationEntry
                {
                    ActionType = "invented-action",
                    Priority = 1
                }
            ]
        };

        var exception = RunExpectingValidationFailure(
            source,
            target,
            rotation,
            includeAutoAttackProcessor: true
        );

        Assert.Contains(
            exception.Errors,
            error => error.Contains(
                "unsupported action type",
                StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void Validation_RejectsUnknownNextSwingReplacement()
    {
        var source = CreateActor("source");
        var target = CreateActor("target");
        var rotation = CreateQueueRotation(
            "main-hand",
            "missing-special"
        );

        var exception = RunExpectingValidationFailure(
            source,
            target,
            rotation,
            includeAutoAttackProcessor: true
        );

        Assert.Contains(
            exception.Errors,
            error =>
                error.Contains("missing-special", StringComparison.OrdinalIgnoreCase) &&
                error.Contains("does not have", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void Validation_RejectsQueuedActionWithoutAutoAttackProcessor()
    {
        var source = CreateActor("source");
        var target = CreateActor("target");
        var replacement = CreateReplacement("heroic-strike", 25m);
        source.AddNextSwingReplacement(replacement);

        var exception = RunExpectingValidationFailure(
            source,
            target,
            CreateQueueRotation("main-hand", replacement.Key),
            includeAutoAttackProcessor: false
        );

        Assert.Contains(
            exception.Errors,
            error => error.Contains(
                "no auto-attack processor",
                StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void Validation_RejectsInterruptFlagOnQueuedAction()
    {
        var source = CreateActor("source");
        var target = CreateActor("target");
        var replacement = CreateReplacement("heroic-strike", 25m);
        source.AddNextSwingReplacement(replacement);

        var rotation = CreateQueueRotation("main-hand", replacement.Key);
        rotation.Entries[0].InterruptCurrentCast = true;

        var exception = RunExpectingValidationFailure(
            source,
            target,
            rotation,
            includeAutoAttackProcessor: true
        );

        Assert.Contains(
            exception.Errors,
            error => error.Contains(
                "cannot interrupt",
                StringComparison.OrdinalIgnoreCase)
        );
    }

    private static SimulationRunResult Run(
        SimulationActorState source,
        SimulationActorState target,
        RotationProfile rotation,
        decimal durationSeconds,
        AutoAttackDefinition mainHand,
        IReadOnlyCollection<SimulationActorState>? additionalActors = null)
    {
        var context = CreateContext(durationSeconds);
        context.AddActor(source);
        context.AddActor(target);

        if (additionalActors is not null)
        {
            foreach (var actor in additionalActors)
            {
                context.AddActor(actor);
            }
        }

        var auraManager = new AuraManager();
        var abilityExecutor = CreateAbilityExecutor(auraManager);
        var autoAttackProcessor = new AutoAttackProcessor(abilityExecutor);
        var rotationExecutor =
            new PriorityRotationExecutor(
                rotation,
                source.Key,
                target.Key,
                abilityExecutor,
                autoAttackProcessor: autoAttackProcessor
            );

        var engine = new SimulationEngine(
            [
                abilityExecutor,
                autoAttackProcessor,
                rotationExecutor
            ]
        );

        return engine.Run(
            context,
            startedContext =>
                Assert.True(
                    autoAttackProcessor.Start(
                        startedContext,
                        source.Key,
                        target.Key,
                        mainHand
                    )
                )
        );
    }

    private static SimulationDefinitionValidationException
        RunExpectingValidationFailure(
            SimulationActorState source,
            SimulationActorState target,
            RotationProfile rotation,
            bool includeAutoAttackProcessor)
    {
        var context = CreateContext(1m);
        context.AddActor(source);
        context.AddActor(target);

        var auraManager = new AuraManager();
        var abilityExecutor = CreateAbilityExecutor(auraManager);
        var autoAttackProcessor = new AutoAttackProcessor(abilityExecutor);

        var rotationExecutor =
            new PriorityRotationExecutor(
                rotation,
                source.Key,
                target.Key,
                abilityExecutor,
                autoAttackProcessor:
                    includeAutoAttackProcessor
                        ? autoAttackProcessor
                        : null
            );

        var processors =
            new List<ICombatEventProcessor>
            {
                abilityExecutor
            };

        if (includeAutoAttackProcessor)
        {
            processors.Add(
                autoAttackProcessor
            );
        }

        processors.Add(
            rotationExecutor
        );

        return Assert.Throws<SimulationDefinitionValidationException>(() =>
            new SimulationEngine(
                processors
            ).Run(context)
        );
    }

    private static AbilityExecutor CreateAbilityExecutor(
        AuraManager auraManager)
    {
        return new AbilityExecutor(
            auraManager,
            new SimpleCombatRollResolver(
                hitChancePercent: 100m,
                dodgeChancePercent: 0m,
                parryChancePercent: 0m,
                blockChancePercent: 0m,
                criticalChancePercent: 0m
            ),
            new RulesetDamageMitigationResolver(
                new CombatRulesetDefinition
                {
                    RulesetKey = "next-swing-rotation-tests",
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
        );
    }

    private static RotationProfile CreateQueueRotation(
        string autoAttackKey,
        string replacementKey)
    {
        return new RotationProfile
        {
            Name = "Next Swing Rotation",
            Entries =
            [
                new RotationEntry
                {
                    ActionType =
                        RotationActionTypes.QueueNextSwingReplacement,
                    NextSwingReplacementKey = replacementKey,
                    AutoAttackKey = autoAttackKey,
                    Priority = 1
                }
            ]
        };
    }

    private static AutoAttackDefinition CreateAutoAttack(
        string key,
        decimal swingIntervalSeconds,
        decimal damage)
    {
        return new AutoAttackDefinition
        {
            Key = key,
            Name = key,
            SwingIntervalSeconds = swingIntervalSeconds,
            WeaponHandKey = WeaponHandKeys.MainHand,
            DamageEffect =
                new AbilityEffectDefinition
                {
                    Key = "damage",
                    EffectType = AbilityEffectTypes.DirectDamage,
                    TargetType = AbilityTargetTypes.Enemy,
                    WeaponHandKey = WeaponHandKeys.MainHand,
                    ResolutionType = CombatResolutionTypes.AlwaysHits,
                    CanMiss = false,
                    CanCrit = false,
                    MinimumValue = damage,
                    MaximumValue = damage,
                    MitigationType = DamageMitigationTypes.None
                }
        };
    }

    private static NextSwingReplacementDefinition CreateReplacement(
        string key,
        decimal damage,
        decimal rageCost = 0m)
    {
        var replacement = new NextSwingReplacementDefinition
        {
            Key = key,
            Name = key,
            WeaponHandKey = WeaponHandKeys.MainHand,
            DamageEffect =
                new AbilityEffectDefinition
                {
                    Key = "damage",
                    EffectType = AbilityEffectTypes.DirectDamage,
                    TargetType = AbilityTargetTypes.Enemy,
                    WeaponHandKey = WeaponHandKeys.MainHand,
                    ResolutionType = CombatResolutionTypes.AlwaysHits,
                    CanMiss = false,
                    CanCrit = false,
                    MinimumValue = damage,
                    MaximumValue = damage,
                    MitigationType = DamageMitigationTypes.None
                }
        };

        if (rageCost > 0m)
        {
            replacement.ResourceCosts.Add(
                new AbilityResourceCost
                {
                    ResourceKey = "rage",
                    Amount = rageCost
                }
            );
        }

        return replacement;
    }

    private static AbilityDefinition CreateDirectAbility(
        string key,
        decimal damage)
    {
        return new AbilityDefinition
        {
            Key = key,
            Name = key,
            GlobalCooldownSeconds = 1.5m,
            Effects =
            [
                new AbilityEffectDefinition
                {
                    Key = "damage",
                    EffectType = AbilityEffectTypes.DirectDamage,
                    TargetType = AbilityTargetTypes.Enemy,
                    ResolutionType = CombatResolutionTypes.AlwaysHits,
                    CanMiss = false,
                    CanCrit = false,
                    MinimumValue = damage,
                    MaximumValue = damage,
                    MitigationType = DamageMitigationTypes.None
                }
            ]
        };
    }

    private static AbilityDefinition CreateRageGenerator(
        string key,
        decimal amount)
    {
        return new AbilityDefinition
        {
            Key = key,
            Name = key,
            GlobalCooldownSeconds = 1.5m,
            Effects =
            [
                new AbilityEffectDefinition
                {
                    Key = "rage",
                    EffectType = AbilityEffectTypes.ResourceChange,
                    TargetType = AbilityTargetTypes.Self,
                    ResourceKey = "rage",
                    ResourceChangeOperation = ResourceChangeOperationTypes.Gain,
                    MinimumValue = amount,
                    MaximumValue = amount
                }
            ]
        };
    }

    private static SimulationActorState CreateActor(
        string key,
        decimal health = 100m)
    {
        var actor = new SimulationActorState
        {
            Key = key,
            Name = key,
            TeamKey =
                key == "source"
                    ? "players"
                    : "enemies",
            Level = 30
        };

        actor.InitializeHealth(health);
        return actor;
    }

    private static SimulationContext CreateContext(
        decimal durationSeconds)
    {
        return new SimulationContext(
            new SimulationRunOptions
            {
                Seed = 12345,
                DurationSeconds = durationSeconds,
                PrimaryActorKey = "source",
                CaptureTimeline = true
            }
        );
    }
}
}
