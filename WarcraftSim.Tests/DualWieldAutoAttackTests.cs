using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Tests;

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
