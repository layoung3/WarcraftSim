// Consolidated tests for AutoAttack Foundation.
// Original class names are retained for xUnit test filtering.

// ==================== AutoAttackCastDelayTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Validation;

public sealed class AutoAttackCastDelayTests
{
    [Fact]
    public void DelayedMainHand_DoesNotSwingDuringCastAndRestartsFullInterval()
    {
        var source = CreateActor("source", "players");
        var target = CreateActor("target", "enemies");
        var ability = CreateCastAbility(delayMainHand: true);
        var mainHand = CreateAutoAttack("main", WeaponHandKeys.MainHand, 2m);

        source.AddAbility(ability);

        var result =
            Run(
                source,
                target,
                [mainHand],
                durationSeconds: 4m,
                (context, executor, autoAttacks) =>
                {
                    Assert.True(
                        autoAttacks.Start(
                            context,
                            source.Key,
                            target.Key,
                            mainHand,
                            firstSwingDelaySeconds: 1m
                        )
                    );

                    Assert.True(
                        executor.TryStartAbility(
                            context,
                            source.Key,
                            target.Key,
                            ability.Key
                        ).Success
                    );
                }
            );

        Assert.Equal(
            [3.5m],
            DamageTimes(result, mainHand.Key)
        );
    }

    [Fact]
    public void DelayedMainHand_DoesNotDelayOffHandStream()
    {
        var source = CreateActor("source", "players");
        var target = CreateActor("target", "enemies");
        var ability = CreateCastAbility(delayMainHand: true);
        var mainHand = CreateAutoAttack("main", WeaponHandKeys.MainHand, 2m);
        var offHand = CreateAutoAttack("off", WeaponHandKeys.OffHand, 1m);

        source.AddAbility(ability);

        var result =
            Run(
                source,
                target,
                [mainHand, offHand],
                durationSeconds: 4m,
                (context, executor, autoAttacks) =>
                {
                    Assert.True(autoAttacks.Start(
                        context,
                        source.Key,
                        target.Key,
                        mainHand,
                        firstSwingDelaySeconds: 1m));

                    Assert.True(autoAttacks.Start(
                        context,
                        source.Key,
                        target.Key,
                        offHand,
                        firstSwingDelaySeconds: 1m));

                    Assert.True(executor.TryStartAbility(
                        context,
                        source.Key,
                        target.Key,
                        ability.Key).Success);
                }
            );

        Assert.Equal([3.5m], DamageTimes(result, mainHand.Key));
        Assert.Equal([1m, 2m, 3m], DamageTimes(result, offHand.Key));
    }

    [Fact]
    public void QueuedReplacement_SurvivesCastDrivenSwingDelay()
    {
        var source = CreateActor("source", "players");
        var target = CreateActor("target", "enemies");
        var ability = CreateCastAbility(delayMainHand: true);
        var mainHand = CreateAutoAttack("main", WeaponHandKeys.MainHand, 2m);
        var replacement = CreateReplacement();

        source.AddAbility(ability);

        var result =
            Run(
                source,
                target,
                [mainHand],
                durationSeconds: 4m,
                (context, executor, autoAttacks) =>
                {
                    Assert.True(autoAttacks.Start(
                        context,
                        source.Key,
                        target.Key,
                        mainHand,
                        firstSwingDelaySeconds: 1m));

                    Assert.True(autoAttacks.QueueNextSwingReplacement(
                        context,
                        source.Key,
                        mainHand.Key,
                        replacement));

                    Assert.True(executor.TryStartAbility(
                        context,
                        source.Key,
                        target.Key,
                        ability.Key).Success);
                }
            );

        var replacementDamage =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type == CombatEventType.Damage &&
                    combatEvent.AbilityKey == replacement.Key
            );

        Assert.Equal(3.5m, replacementDamage.TimeSeconds);
        Assert.Empty(DamageTimes(result, mainHand.Key));
    }

    [Fact]
    public void AbilityWithoutDelayedHands_DoesNotChangeBackgroundSwingTimer()
    {
        var source = CreateActor("source", "players");
        var target = CreateActor("target", "enemies");
        var ability = CreateCastAbility(delayMainHand: false);
        var mainHand = CreateAutoAttack("main", WeaponHandKeys.MainHand, 2m);

        source.AddAbility(ability);

        var result =
            Run(
                source,
                target,
                [mainHand],
                durationSeconds: 3.1m,
                (context, executor, autoAttacks) =>
                {
                    Assert.True(autoAttacks.Start(
                        context,
                        source.Key,
                        target.Key,
                        mainHand,
                        firstSwingDelaySeconds: 1m));

                    Assert.True(executor.TryStartAbility(
                        context,
                        source.Key,
                        target.Key,
                        ability.Key).Success);
                }
            );

        Assert.Equal([1m, 3m], DamageTimes(result, mainHand.Key));
    }

    [Fact]
    public void InvalidDelayedWeaponHand_FailsDefinitionValidation()
    {
        var context = CreateContext(2m);
        var source = CreateActor("source", "players");

        source.AddAbility(
            new AbilityDefinition
            {
                Key = "invalid-delay",
                Name = "Invalid Delay",
                DelayedAutoAttackWeaponHandKeys = ["third-hand"]
            }
        );

        context.AddActor(source);

        var exception =
            Assert.Throws<SimulationDefinitionValidationException>(() =>
                new SimulationEngine().Run(context)
            );

        Assert.Contains(
            exception.Errors,
            error => error.Contains(
                "unsupported delayed auto-attack weapon hand 'third-hand'",
                StringComparison.OrdinalIgnoreCase)
        );
    }

    private static SimulationRunResult Run(
        SimulationActorState source,
        SimulationActorState target,
        IReadOnlyCollection<AutoAttackDefinition> definitions,
        decimal durationSeconds,
        Action<SimulationContext, AbilityExecutor, AutoAttackProcessor> start)
    {
        var context = CreateContext(durationSeconds);
        context.AddActor(source);
        context.AddActor(target);

        var executor = CreateExecutor();
        var autoAttacks = new AutoAttackProcessor(executor);

        return new SimulationEngine(
            [
                executor,
                autoAttacks
            ])
            .Run(
                context,
                startedContext => start(
                    startedContext,
                    executor,
                    autoAttacks
                )
            );
    }

    private static AbilityExecutor CreateExecutor()
    {
        return new AbilityExecutor(
            new AuraManager(),
            new SimpleCombatRollResolver(),
            new RulesetDamageMitigationResolver(
                new CombatRulesetDefinition
                {
                    RulesetKey = "auto-attack-cast-delay-tests",
                    Version = "1"
                }
            )
        );
    }

    private static AbilityDefinition CreateCastAbility(
        bool delayMainHand)
    {
        return new AbilityDefinition
        {
            Key = "test-cast",
            Name = "Test Cast",
            CastTimeSeconds = 1.5m,
            DelayedAutoAttackWeaponHandKeys =
                delayMainHand
                    ? [WeaponHandKeys.MainHand]
                    : []
        };
    }

    private static AutoAttackDefinition CreateAutoAttack(
        string key,
        string handKey,
        decimal swingIntervalSeconds)
    {
        return new AutoAttackDefinition
        {
            Key = key,
            Name = key,
            WeaponHandKey = handKey,
            SwingIntervalSeconds = swingIntervalSeconds,
            DamageEffect =
                new AbilityEffectDefinition
                {
                    Key = "damage",
                    EffectType = AbilityEffectTypes.DirectDamage,
                    TargetType = AbilityTargetTypes.Enemy,
                    WeaponHandKey = handKey,
                    ResolutionType = CombatResolutionTypes.AlwaysHits,
                    CanMiss = false,
                    CanCrit = false,
                    MinimumValue = 1m,
                    MaximumValue = 1m,
                    MitigationType = DamageMitigationTypes.None
                }
        };
    }

    private static NextSwingReplacementDefinition CreateReplacement()
    {
        return new NextSwingReplacementDefinition
        {
            Key = "queued-special",
            Name = "Queued Special",
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
                    MinimumValue = 2m,
                    MaximumValue = 2m,
                    MitigationType = DamageMitigationTypes.None
                }
        };
    }

    private static decimal[] DamageTimes(
        SimulationRunResult result,
        string abilityKey)
    {
        return result.Timeline
            .Where(combatEvent =>
                combatEvent.Type == CombatEventType.Damage &&
                string.Equals(
                    combatEvent.AbilityKey,
                    abilityKey,
                    StringComparison.OrdinalIgnoreCase))
            .Select(combatEvent => combatEvent.TimeSeconds)
            .ToArray();
    }

    private static SimulationActorState CreateActor(
        string key,
        string teamKey)
    {
        var actor =
            new SimulationActorState
            {
                Key = key,
                Name = key,
                TeamKey = teamKey,
                Level = 30
            };

        actor.InitializeHealth(1000m);
        actor.ConfigureActionTiming(0m, 0m);

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

// ==================== AutoAttackFoundationTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;

public sealed class AutoAttackFoundationTests
{
    [Fact]
    public void AutoAttack_SwingsOnIndependentBackgroundTimer()
    {
        var source = CreateActor("source", 100m);
        var target = CreateActor("target", 100m);
        var definition = CreateAutoAttack(2m, 10m);

        var result = Run(
            source,
            [target],
            5m,
            (context, processor) =>
            {
                Assert.True(
                    processor.Start(
                        context,
                        source.Key,
                        target.Key,
                        definition
                    )
                );
            }
        );

        var damageEvents =
            result.Timeline
                .Where(combatEvent =>
                    combatEvent.Type == CombatEventType.Damage &&
                    string.Equals(
                        combatEvent.AbilityKey,
                        definition.Key,
                        StringComparison.OrdinalIgnoreCase
                    ))
                .ToList();

        Assert.Equal(
            [2m, 4m],
            damageEvents
                .Select(combatEvent => combatEvent.TimeSeconds)
                .ToArray()
        );

        Assert.All(
            damageEvents,
            combatEvent =>
                Assert.Equal(
                    10m,
                    combatEvent.Amount
                )
        );

        var summary =
            result.Summary.Abilities[
                definition.Key
            ];

        Assert.Equal(2, summary.DamageOccurrenceCount);
        Assert.Equal(20m, summary.DamageDone);
        Assert.Equal(0, summary.CastStartedCount);
    }

    [Fact]
    public void AutoAttack_DoesNotWaitForCastGlobalCooldownOrInputReadiness()
    {
        var source = CreateActor("source", 100m);
        var target = CreateActor("target", 100m);
        var definition = CreateAutoAttack(1m, 7m);

        source.ConfigureActionTiming(
            initialActionDelaySeconds: 50m,
            inputDelaySeconds: 0m
        );

        source.StartGlobalCooldown(
            currentTimeSeconds: 0m,
            durationSeconds: 50m
        );

        source.StartCast(
            currentTimeSeconds: 0m,
            castTimeSeconds: 50m
        );

        var result = Run(
            source,
            [target],
            3m,
            (context, processor) =>
            {
                Assert.True(
                    processor.Start(
                        context,
                        source.Key,
                        target.Key,
                        definition
                    )
                );
            }
        );

        var damageEvents =
            result.Timeline
                .Where(combatEvent =>
                    combatEvent.Type == CombatEventType.Damage &&
                    combatEvent.AbilityKey == definition.Key)
                .ToList();

        Assert.Equal(2, damageEvents.Count);
        Assert.Equal(1m, damageEvents[0].TimeSeconds);
        Assert.Equal(2m, damageEvents[1].TimeSeconds);
    }

    [Fact]
    public void AutoAttack_CanExplicitlyStartWithImmediateFirstSwing()
    {
        var source = CreateActor("source", 100m);
        var target = CreateActor("target", 100m);
        var definition = CreateAutoAttack(2m, 10m);

        var result = Run(
            source,
            [target],
            1m,
            (context, processor) =>
            {
                Assert.True(
                    processor.Start(
                        context,
                        source.Key,
                        target.Key,
                        definition,
                        firstSwingDelaySeconds: 0m
                    )
                );
            }
        );

        var damage =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type == CombatEventType.Damage &&
                    combatEvent.AbilityKey == definition.Key
            );

        Assert.Equal(0m, damage.TimeSeconds);
    }

    [Fact]
    public void AutoAttack_StopInvalidatesAlreadyQueuedSwing()
    {
        var source = CreateActor("source", 100m);
        var target = CreateActor("target", 100m);
        var definition = CreateAutoAttack(1m, 10m);

        var result = Run(
            source,
            [target],
            3m,
            (context, processor) =>
            {
                Assert.True(
                    processor.Start(
                        context,
                        source.Key,
                        target.Key,
                        definition
                    )
                );

                Assert.True(
                    processor.Stop(
                        context,
                        source.Key,
                        definition.Key
                    )
                );
            }
        );

        Assert.DoesNotContain(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.Damage &&
                combatEvent.AbilityKey == definition.Key
        );

        Assert.Contains(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.AutoAttackStopped &&
                combatEvent.AbilityKey == definition.Key
        );
    }

    [Fact]
    public void AutoAttack_RestartRetargetsAndInvalidatesOldSwingInstance()
    {
        var source = CreateActor("source", 100m);
        var firstTarget = CreateActor("first-target", 100m);
        var secondTarget = CreateActor("second-target", 100m);
        var definition = CreateAutoAttack(2m, 10m);

        var result = Run(
            source,
            [firstTarget, secondTarget],
            3m,
            (context, processor) =>
            {
                Assert.True(
                    processor.Start(
                        context,
                        source.Key,
                        firstTarget.Key,
                        definition,
                        firstSwingDelaySeconds: 2m
                    )
                );

                Assert.True(
                    processor.Start(
                        context,
                        source.Key,
                        secondTarget.Key,
                        definition,
                        firstSwingDelaySeconds: 1m
                    )
                );
            }
        );

        var damage =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type == CombatEventType.Damage &&
                    combatEvent.AbilityKey == definition.Key
            );

        Assert.Equal(secondTarget.Key, damage.TargetActorKey);
        Assert.Equal(1m, damage.TimeSeconds);
        Assert.Equal(100m, firstTarget.CurrentHealth);
        Assert.Equal(90m, secondTarget.CurrentHealth);
    }

    [Fact]
    public void AutoAttack_StopsAfterKillingTarget()
    {
        var source = CreateActor("source", 100m);
        var target = CreateActor("target", 5m);
        var definition = CreateAutoAttack(1m, 10m);

        var result = Run(
            source,
            [target],
            5m,
            (context, processor) =>
            {
                Assert.True(
                    processor.Start(
                        context,
                        source.Key,
                        target.Key,
                        definition
                    )
                );
            }
        );

        Assert.Single(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.Damage &&
                combatEvent.AbilityKey == definition.Key
        );

        Assert.Contains(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.AutoAttackStopped &&
                combatEvent.AbilityKey == definition.Key
        );

        Assert.False(
            source.AutoAttacks[
                definition.Key
            ].IsActive
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AutoAttack_RejectsNonPositiveSwingInterval(
        int swingIntervalSeconds)
    {
        var source = CreateActor("source", 100m);
        var target = CreateActor("target", 100m);
        var definition =
            CreateAutoAttack(
                swingIntervalSeconds,
                10m
            );

        var context = CreateContext(source.Key, 3m);
        context.AddActor(source);
        context.AddActor(target);

        var processor = CreateProcessor();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            processor.Start(
                context,
                source.Key,
                target.Key,
                definition
            )
        );
    }

    [Fact]
    public void AutoAttackEventTypes_AreAppendedWithoutShiftingExistingContract()
    {
        Assert.Equal(31, (int)CombatEventType.AbilityChannelCancelled);
        Assert.Equal(32, (int)CombatEventType.AutoAttackStarted);
        Assert.Equal(33, (int)CombatEventType.AutoAttackSwing);
        Assert.Equal(34, (int)CombatEventType.AutoAttackStopped);
    }

    [Fact]
    public void ForeverPlayerMeleeFactory_UsesWeaponSpeedForAttackPowerDamage()
    {
        var definition =
            ForeverAutoAttackFactory.CreatePlayerMelee(
                key: "main-hand",
                name: "Main Hand",
                minimumWeaponDamage: 18m,
                maximumWeaponDamage: 12m,
                weaponSpeedSeconds: 2.6m,
                attackSkillStatKey: "sword-skill"
            );

        Assert.Equal(2.6m, definition.SwingIntervalSeconds);
        Assert.Equal(12m, definition.DamageEffect.MinimumValue);
        Assert.Equal(18m, definition.DamageEffect.MaximumValue);
        Assert.Equal(
            ForeverCombatStatKeys.AttackPower,
            definition.DamageEffect.ScalingStatKey
        );
        Assert.Equal(
            2.6m / 14m,
            definition.DamageEffect.ScalingCoefficient
        );
        Assert.Equal(
            ForeverCombatResolutionTypes.PlayerMeleeAuto,
            definition.DamageEffect.ResolutionType
        );
        Assert.Equal(
            "sword-skill",
            definition.DamageEffect.AttackSkillStatKey
        );
        Assert.Equal(
            DamageMitigationTypes.Armor,
            definition.DamageEffect.MitigationType
        );
        Assert.True(definition.DamageEffect.CanGlance);
        Assert.True(definition.DamageEffect.CanCrit);
    }

    [Fact]
    public void ForeverPlayerMeleeFactory_CanUseDualWieldAutoAttackTable()
    {
        var definition =
            ForeverAutoAttackFactory.CreatePlayerMelee(
                key: "dual-wield-main-hand",
                name: "Dual Wield Main Hand",
                minimumWeaponDamage: 10m,
                maximumWeaponDamage: 20m,
                weaponSpeedSeconds: 2m,
                attackSkillStatKey: "sword-skill",
                usesDualWieldHitTable: true
            );

        Assert.Equal(
            ForeverCombatResolutionTypes.PlayerDualWieldMeleeAuto,
            definition.DamageEffect.ResolutionType
        );
    }

    [Fact]
    public void ForeverPlayerMeleeFactory_RequiresWeaponSpeedAndAttackSkill()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverAutoAttackFactory.CreatePlayerMelee(
                "main-hand",
                "Main Hand",
                10m,
                20m,
                0m,
                "sword-skill"
            )
        );

        Assert.Throws<ArgumentException>(() =>
            ForeverAutoAttackFactory.CreatePlayerMelee(
                "main-hand",
                "Main Hand",
                10m,
                20m,
                2m,
                ""
            )
        );
    }

    private static SimulationRunResult Run(
        SimulationActorState source,
        IReadOnlyCollection<SimulationActorState> targets,
        decimal durationSeconds,
        Action<SimulationContext, AutoAttackProcessor> onStarted)
    {
        var context =
            CreateContext(
                source.Key,
                durationSeconds
            );

        context.AddActor(source);

        foreach (var target in targets)
        {
            context.AddActor(target);
        }

        var processor =
            CreateProcessor();

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

    private static AutoAttackProcessor CreateProcessor()
    {
        var executor =
            new AbilityExecutor(
                new AuraManager(),
                new SimpleCombatRollResolver(),
                new RulesetDamageMitigationResolver(
                    new CombatRulesetDefinition
                    {
                        RulesetKey = "auto-attack-tests",
                        Version = "1"
                    }
                )
            );

        return new AutoAttackProcessor(
            executor
        );
    }

    private static AutoAttackDefinition CreateAutoAttack(
        decimal swingIntervalSeconds,
        decimal damage)
    {
        return new AutoAttackDefinition
        {
            Key = "main-hand-auto",
            Name = "Main Hand Auto Attack",
            SwingIntervalSeconds = swingIntervalSeconds,
            DamageEffect =
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
        };
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
                    key == "source"
                        ? "players"
                        : "enemies",
                Level = 30
            };

        actor.InitializeHealth(
            health
        );

        return actor;
    }

    private static SimulationContext CreateContext(
        string primaryActorKey,
        decimal durationSeconds)
    {
        return new SimulationContext(
            new SimulationRunOptions
            {
                Seed = 12345,
                DurationSeconds = durationSeconds,
                PrimaryActorKey = primaryActorKey,
                CaptureTimeline = true
            }
        );
    }
}
}

// ==================== AutoAttackTimingTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;

public sealed class AutoAttackTimingTests
{
    [Fact]
    public void BaseProvider_PreservesDefinitionSwingInterval()
    {
        var context = CreateContext(5m);
        var source = CreateActor("source");
        var definition = CreateAutoAttack(2.6m);

        context.AddActor(source);

        var timing =
            BaseAutoAttackTimingProvider.Instance.Resolve(
                context,
                source,
                definition
            );

        Assert.Equal(2.6m, timing.SwingIntervalSeconds);
    }

    [Theory]
    [InlineData(0, 2.5)]
    [InlineData(25, 2.0)]
    [InlineData(100, 1.25)]
    public void ForeverProvider_AppliesHasteToSwingInterval(
        int hastePercent,
        double expectedSwingIntervalSeconds)
    {
        var context = CreateContext(5m);
        var source = CreateActor("source");
        var definition = CreateAutoAttack(2.5m);

        source.Stats.Set(
            ForeverCombatStatKeys.HastePercent,
            hastePercent
        );

        context.AddActor(source);

        var timing =
            new ForeverAutoAttackTimingProvider()
                .Resolve(
                    context,
                    source,
                    definition
                );

        Assert.Equal(
            (decimal)expectedSwingIntervalSeconds,
            timing.SwingIntervalSeconds
        );
    }

    [Fact]
    public void ForeverProvider_NegativeHasteDoesNotSlowSwingBelowBaseSpeed()
    {
        var context = CreateContext(5m);
        var source = CreateActor("source");
        var definition = CreateAutoAttack(2.5m);

        source.Stats.Set(
            ForeverCombatStatKeys.HastePercent,
            -50m
        );

        context.AddActor(source);

        var timing =
            new ForeverAutoAttackTimingProvider()
                .Resolve(
                    context,
                    source,
                    definition
                );

        Assert.Equal(2.5m, timing.SwingIntervalSeconds);
    }

    [Fact]
    public void Processor_UsesForeverHasteForFirstAndFollowingSwings()
    {
        var source = CreateActor("source");
        var target = CreateActor("target");
        var definition = CreateAutoAttack(2.5m);

        source.Stats.Set(
            ForeverCombatStatKeys.HastePercent,
            25m
        );

        var result =
            Run(
                source,
                target,
                definition,
                durationSeconds: 4.1m,
                new ForeverAutoAttackTimingProvider()
            );

        Assert.Equal(
            [2m, 4m],
            DamageTimes(
                result,
                definition.Key
            )
        );
    }

    [Fact]
    public void ExplicitFirstSwingDelay_OverridesOnlyInitialSwing()
    {
        var source = CreateActor("source");
        var target = CreateActor("target");
        var definition = CreateAutoAttack(2.5m);

        source.Stats.Set(
            ForeverCombatStatKeys.HastePercent,
            25m
        );

        var result =
            Run(
                source,
                target,
                definition,
                durationSeconds: 4.1m,
                new ForeverAutoAttackTimingProvider(),
                firstSwingDelaySeconds: 0m
            );

        Assert.Equal(
            [0m, 2m, 4m],
            DamageTimes(
                result,
                definition.Key
            )
        );
    }

    [Fact]
    public void Processor_ReResolvesTimingAfterEveryCompletedSwing()
    {
        var source = CreateActor("source");
        var target = CreateActor("target");
        var definition = CreateAutoAttack(2m);

        var timingProvider =
            new SequenceAutoAttackTimingProvider(
                2m,
                1m,
                1m
            );

        var result =
            Run(
                source,
                target,
                definition,
                durationSeconds: 3.1m,
                timingProvider
            );

        Assert.Equal(
            [2m, 3m],
            DamageTimes(
                result,
                definition.Key
            )
        );

        Assert.True(timingProvider.ResolveCount >= 3);
    }

    [Fact]
    public void Processor_RejectsNonPositiveResolvedSwingInterval()
    {
        var source = CreateActor("source");
        var target = CreateActor("target");
        var definition = CreateAutoAttack(2m);
        var context = CreateContext(5m);

        context.AddActor(source);
        context.AddActor(target);

        var processor =
            CreateProcessor(
                new ConstantAutoAttackTimingProvider(0m)
            );

        Assert.Throws<InvalidOperationException>(() =>
            processor.Start(
                context,
                source.Key,
                target.Key,
                definition
            )
        );
    }

    [Fact]
    public void ForeverHaste_ChangesFrequencyWithoutChangingBaseWeaponApCoefficient()
    {
        var definition =
            ForeverAutoAttackFactory.CreatePlayerMelee(
                key: "main-hand",
                name: "Main Hand",
                minimumWeaponDamage: 20m,
                maximumWeaponDamage: 30m,
                weaponSpeedSeconds: 2.8m,
                attackSkillStatKey: "sword-skill"
            );

        var source = CreateActor("source");
        source.Stats.Set(
            ForeverCombatStatKeys.HastePercent,
            40m
        );

        var context = CreateContext(5m);
        context.AddActor(source);

        var timing =
            new ForeverAutoAttackTimingProvider()
                .Resolve(
                    context,
                    source,
                    definition
                );

        Assert.Equal(2m, timing.SwingIntervalSeconds);
        Assert.Equal(
            2.8m / 14m,
            definition.DamageEffect.ScalingCoefficient
        );
    }

    private static SimulationRunResult Run(
        SimulationActorState source,
        SimulationActorState target,
        AutoAttackDefinition definition,
        decimal durationSeconds,
        IAutoAttackTimingProvider timingProvider,
        decimal? firstSwingDelaySeconds = null)
    {
        var context =
            CreateContext(
                durationSeconds
            );

        context.AddActor(source);
        context.AddActor(target);

        var processor =
            CreateProcessor(
                timingProvider
            );

        return new SimulationEngine(
            [
                processor
            ])
            .Run(
                context,
                startedContext =>
                {
                    Assert.True(
                        processor.Start(
                            startedContext,
                            source.Key,
                            target.Key,
                            definition,
                            firstSwingDelaySeconds
                        )
                    );
                }
            );
    }

    private static decimal[] DamageTimes(
        SimulationRunResult result,
        string abilityKey)
    {
        return result.Timeline
            .Where(combatEvent =>
                combatEvent.Type == CombatEventType.Damage &&
                string.Equals(
                    combatEvent.AbilityKey,
                    abilityKey,
                    StringComparison.OrdinalIgnoreCase
                ))
            .Select(combatEvent =>
                combatEvent.TimeSeconds
            )
            .ToArray();
    }

    private static AutoAttackProcessor CreateProcessor(
        IAutoAttackTimingProvider timingProvider)
    {
        var executor =
            new AbilityExecutor(
                new AuraManager(),
                new SimpleCombatRollResolver(),
                new RulesetDamageMitigationResolver(
                    new CombatRulesetDefinition
                    {
                        RulesetKey = "auto-attack-timing-tests",
                        Version = "1"
                    }
                )
            );

        return new AutoAttackProcessor(
            executor,
            timingProvider
        );
    }

    private static AutoAttackDefinition CreateAutoAttack(
        decimal swingIntervalSeconds)
    {
        return new AutoAttackDefinition
        {
            Key = "main-hand-auto",
            Name = "Main Hand Auto Attack",
            SwingIntervalSeconds = swingIntervalSeconds,
            DamageEffect =
                new AbilityEffectDefinition
                {
                    Key = "damage",
                    EffectType = AbilityEffectTypes.DirectDamage,
                    TargetType = AbilityTargetTypes.Enemy,
                    ResolutionType = CombatResolutionTypes.AlwaysHits,
                    CanMiss = false,
                    CanCrit = false,
                    MinimumValue = 1m,
                    MaximumValue = 1m,
                    MitigationType = DamageMitigationTypes.None
                }
        };
    }

    private static SimulationActorState CreateActor(
        string key)
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

        actor.InitializeHealth(100m);

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

    private sealed class ConstantAutoAttackTimingProvider :
        IAutoAttackTimingProvider
    {
        private readonly decimal _swingIntervalSeconds;

        public ConstantAutoAttackTimingProvider(
            decimal swingIntervalSeconds)
        {
            _swingIntervalSeconds =
                swingIntervalSeconds;
        }

        public AutoAttackTimingSnapshot Resolve(
            SimulationContext context,
            SimulationActorState source,
            AutoAttackDefinition definition)
        {
            return new AutoAttackTimingSnapshot
            {
                SwingIntervalSeconds =
                    _swingIntervalSeconds
            };
        }
    }

    private sealed class SequenceAutoAttackTimingProvider :
        IAutoAttackTimingProvider
    {
        private readonly Queue<decimal>
            _intervals;

        private decimal _lastInterval;

        public int ResolveCount { get; private set; }

        public SequenceAutoAttackTimingProvider(
            params decimal[] intervals)
        {
            if (intervals.Length == 0)
            {
                throw new ArgumentException(
                    "At least one interval is required.",
                    nameof(intervals)
                );
            }

            _intervals =
                new Queue<decimal>(
                    intervals
                );

            _lastInterval =
                intervals[^1];
        }

        public AutoAttackTimingSnapshot Resolve(
            SimulationContext context,
            SimulationActorState source,
            AutoAttackDefinition definition)
        {
            ResolveCount++;

            if (_intervals.Count > 0)
            {
                _lastInterval =
                    _intervals.Dequeue();
            }

            return new AutoAttackTimingSnapshot
            {
                SwingIntervalSeconds =
                    _lastInterval
            };
        }
    }
}
}

// ==================== DualWieldAutoAttackTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;

public sealed class DualWieldAutoAttackTests
{
    [Fact]
    public void SingleWeaponFactory_DefaultsToMainHandAtFullDamage()
    {
        var definition =
            ForeverAutoAttackFactory.CreatePlayerMelee(
                "main-hand",
                "Main Hand",
                20m,
                30m,
                2.5m,
                "sword-skill"
            );

        Assert.Equal(
            WeaponHandKeys.MainHand,
            definition.WeaponHandKey
        );
        Assert.Equal(1m, definition.DamageMultiplier);
        Assert.Null(definition.DamageMultiplierStatKey);
        Assert.Equal(
            WeaponHandKeys.MainHand,
            definition.DamageEffect.WeaponHandKey
        );
        Assert.Equal(
            ForeverCombatResolutionTypes.PlayerMeleeAuto,
            definition.DamageEffect.ResolutionType
        );
    }

    [Fact]
    public void DualWieldMainHand_UsesDualWieldTableWithoutDamagePenalty()
    {
        var definition =
            ForeverAutoAttackFactory.CreatePlayerDualWieldMainHand(
                "main-hand",
                "Main Hand",
                20m,
                30m,
                2.5m,
                "sword-skill"
            );

        Assert.Equal(
            WeaponHandKeys.MainHand,
            definition.WeaponHandKey
        );
        Assert.Equal(1m, definition.DamageMultiplier);
        Assert.Equal(
            ForeverCombatResolutionTypes.PlayerDualWieldMeleeAuto,
            definition.DamageEffect.ResolutionType
        );
    }

    [Fact]
    public void DualWieldOffHand_UsesHalfDamageAndDualWieldTable()
    {
        var definition =
            ForeverAutoAttackFactory.CreatePlayerDualWieldOffHand(
                "off-hand",
                "Off Hand",
                20m,
                30m,
                1.8m,
                "sword-skill"
            );

        Assert.Equal(
            WeaponHandKeys.OffHand,
            definition.WeaponHandKey
        );
        Assert.Equal(
            WeaponHandKeys.OffHand,
            definition.DamageEffect.WeaponHandKey
        );
        Assert.Equal(
            ForeverAutoAttackFactory.BaseOffHandDamageMultiplier,
            definition.DamageMultiplier
        );
        Assert.Equal(
            ForeverCombatStatKeys.OffHandDamagePercent,
            definition.DamageMultiplierStatKey
        );
        Assert.Equal(
            ForeverCombatResolutionTypes.PlayerDualWieldMeleeAuto,
            definition.DamageEffect.ResolutionType
        );
    }

    [Fact]
    public void DualWieldOffHand_PreservesBaseWeaponSpeedForAttackPowerScaling()
    {
        var definition =
            ForeverAutoAttackFactory.CreatePlayerDualWieldOffHand(
                "off-hand",
                "Off Hand",
                20m,
                30m,
                1.8m,
                "sword-skill"
            );

        Assert.Equal(
            1.8m / 14m,
            definition.DamageEffect.ScalingCoefficient
        );
    }

    [Fact]
    public void OffHandHitAdjustment_AppliesOnlyToOffHandEffects()
    {
        var source = CreateActor("source");
        var target = CreateActor("target");

        source.Stats.Set(
            ForeverCombatStatKeys.OffHandHitChancePercent,
            10m
        );

        var provider =
            new ForeverWeaponHandCombatRollAdjustmentProvider();

        var offHand =
            provider.GetAdjustment(
                CreateContext(),
                source,
                target,
                CreateAbility(),
                CreateEffect(WeaponHandKeys.OffHand),
                CreateRule()
            );

        var mainHand =
            provider.GetAdjustment(
                CreateContext(),
                source,
                target,
                CreateAbility(),
                CreateEffect(WeaponHandKeys.MainHand),
                CreateRule()
            );

        Assert.Equal(10m, offHand.HitChancePercentDelta);
        Assert.Same(
            CombatRollContextAdjustment.None,
            mainHand
        );
    }


    [Fact]
    public void ForeverComposite_IncludesOffHandHitAdjustment()
    {
        var source = CreateActor("source");
        var target = CreateActor("target");

        source.Stats.Set(
            ForeverCombatStatKeys.OffHandHitChancePercent,
            7m
        );

        var adjustment =
            new ForeverCombatRollContextAdjustmentProvider()
                .GetAdjustment(
                    CreateContext(),
                    source,
                    target,
                    CreateAbility(),
                    CreateEffect(WeaponHandKeys.OffHand),
                    CreateRule()
                );

        Assert.Equal(
            7m,
            adjustment.HitChancePercentDelta
        );
    }

    [Fact]
    public void AutoAttackDamageMultiplier_ScalesTheWholeResolvedWeaponAmount()
    {
        var source = CreateActor("source");
        var target = CreateActor("target", 1000m);
        var definition = CreateAutoAttack(
            "off-hand",
            1m,
            100m,
            0.5m
        );

        var result =
            Run(
                source,
                target,
                1.1m,
                (context, processor) =>
                    Assert.True(
                        processor.Start(
                            context,
                            source.Key,
                            target.Key,
                            definition
                        )
                    )
            );

        var damage =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type == CombatEventType.Damage &&
                    combatEvent.AbilityKey == definition.Key
            );

        Assert.Equal(50m, damage.RawAmount);
        Assert.Equal(50m, damage.Amount);
    }

    [Fact]
    public void OffHandDamageStat_ModifiesTheBaselinePenaltyAtSwingTime()
    {
        var source = CreateActor("source");
        var target = CreateActor("target", 1000m);
        var definition = CreateAutoAttack(
            "off-hand",
            1m,
            100m,
            0.5m,
            ForeverCombatStatKeys.OffHandDamagePercent
        );

        source.Stats.Set(
            ForeverCombatStatKeys.OffHandDamagePercent,
            50m
        );

        var result =
            Run(
                source,
                target,
                1.1m,
                (context, processor) =>
                    Assert.True(
                        processor.Start(
                            context,
                            source.Key,
                            target.Key,
                            definition
                        )
                    )
            );

        var damage =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type == CombatEventType.Damage &&
                    combatEvent.AbilityKey == definition.Key
            );

        Assert.Equal(75m, damage.RawAmount);
    }

    [Fact]
    public void OffHandDamageStat_CannotDriveDamageBelowZero()
    {
        var source = CreateActor("source");
        var target = CreateActor("target", 1000m);
        var definition = CreateAutoAttack(
            "off-hand",
            1m,
            100m,
            0.5m,
            ForeverCombatStatKeys.OffHandDamagePercent
        );

        source.Stats.Set(
            ForeverCombatStatKeys.OffHandDamagePercent,
            -200m
        );

        var result =
            Run(
                source,
                target,
                1.1m,
                (context, processor) =>
                    Assert.True(
                        processor.Start(
                            context,
                            source.Key,
                            target.Key,
                            definition
                        )
                    )
            );

        var damage =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type == CombatEventType.Damage &&
                    combatEvent.AbilityKey == definition.Key
            );

        Assert.Equal(0m, damage.RawAmount);
        Assert.Equal(0m, damage.Amount);
    }

    [Fact]
    public void MainHandAndOffHand_SwingOnIndependentTimers()
    {
        var source = CreateActor("source");
        var target = CreateActor("target", 10000m);

        var mainHand = CreateAutoAttack(
            "main-hand",
            2m,
            10m,
            1m
        );

        var offHand = CreateAutoAttack(
            "off-hand",
            1.5m,
            5m,
            1m
        );

        var result =
            Run(
                source,
                target,
                4.1m,
                (context, processor) =>
                {
                    Assert.True(
                        processor.Start(
                            context,
                            source.Key,
                            target.Key,
                            mainHand
                        )
                    );

                    Assert.True(
                        processor.Start(
                            context,
                            source.Key,
                            target.Key,
                            offHand
                        )
                    );
                }
            );

        Assert.Equal(
            [2m, 4m],
            DamageTimes(result, mainHand.Key)
        );

        Assert.Equal(
            [1.5m, 3m],
            DamageTimes(result, offHand.Key)
        );
    }

    [Fact]
    public void AutoAttack_RejectsNegativeDamageMultiplier()
    {
        var source = CreateActor("source");
        var target = CreateActor("target");
        var context = CreateContext();

        context.AddActor(source);
        context.AddActor(target);

        var definition = CreateAutoAttack(
            "off-hand",
            2m,
            10m,
            -0.5m
        );

        var processor = CreateProcessor();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            processor.Start(
                context,
                source.Key,
                target.Key,
                definition
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

    private static AutoAttackProcessor CreateProcessor()
    {
        var executor =
            new AbilityExecutor(
                new AuraManager(),
                new SimpleCombatRollResolver(),
                new RulesetDamageMitigationResolver(
                    new CombatRulesetDefinition
                    {
                        RulesetKey = "dual-wield-tests",
                        Version = "1"
                    }
                )
            );

        return new AutoAttackProcessor(executor);
    }

    private static AutoAttackDefinition CreateAutoAttack(
        string key,
        decimal swingIntervalSeconds,
        decimal damage,
        decimal damageMultiplier,
        string? damageMultiplierStatKey = null)
    {
        var weaponHand =
            key.Contains(
                "off-hand",
                StringComparison.OrdinalIgnoreCase)
                ? WeaponHandKeys.OffHand
                : WeaponHandKeys.MainHand;

        return new AutoAttackDefinition
        {
            Key = key,
            Name = key,
            SwingIntervalSeconds = swingIntervalSeconds,
            WeaponHandKey = weaponHand,
            DamageMultiplier = damageMultiplier,
            DamageMultiplierStatKey = damageMultiplierStatKey,
            DamageEffect =
                new AbilityEffectDefinition
                {
                    Key = "damage",
                    EffectType = AbilityEffectTypes.DirectDamage,
                    TargetType = AbilityTargetTypes.Enemy,
                    WeaponHandKey = weaponHand,
                    ResolutionType = CombatResolutionTypes.AlwaysHits,
                    CanMiss = false,
                    CanCrit = false,
                    MinimumValue = damage,
                    MaximumValue = damage,
                    MitigationType = DamageMitigationTypes.None
                }
        };
    }

    private static AbilityDefinition CreateAbility()
    {
        return new AbilityDefinition
        {
            Key = "test-ability",
            Name = "Test Ability"
        };
    }

    private static AbilityEffectDefinition CreateEffect(
        string weaponHandKey)
    {
        return new AbilityEffectDefinition
        {
            Key = "damage",
            EffectType = AbilityEffectTypes.DirectDamage,
            WeaponHandKey = weaponHandKey
        };
    }

    private static CombatRollRuleDefinition CreateRule()
    {
        return new CombatRollRuleDefinition
        {
            ResolutionType = "test",
            BaseHitChancePercent = 76m
        };
    }

    private static decimal[] DamageTimes(
        SimulationRunResult result,
        string abilityKey)
    {
        return result.Timeline
            .Where(combatEvent =>
                combatEvent.Type == CombatEventType.Damage &&
                string.Equals(
                    combatEvent.AbilityKey,
                    abilityKey,
                    StringComparison.OrdinalIgnoreCase
                ))
            .Select(combatEvent =>
                combatEvent.TimeSeconds
            )
            .ToArray();
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
