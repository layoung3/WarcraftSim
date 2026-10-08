using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests;

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
