using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests;

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
