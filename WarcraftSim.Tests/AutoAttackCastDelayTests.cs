using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Validation;

namespace WarcraftSim.Tests;

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
