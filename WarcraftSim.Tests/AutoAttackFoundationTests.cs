using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Tests;

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
