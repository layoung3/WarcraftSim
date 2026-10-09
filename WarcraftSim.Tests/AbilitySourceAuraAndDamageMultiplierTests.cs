using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Validation;

namespace WarcraftSim.Tests;

public sealed class AbilitySourceAuraAndDamageMultiplierTests
{
    [Fact]
    public void MissingRequiredSourceAura_PreventsAbilityUseWithoutSpendingResource()
    {
        var source = CreateActor("source", "raid");
        var target = CreateActor("target", "enemy");
        source.AddResource(new ResourceState("rage", 100m, 50m));
        source.AddAbility(CreateRequiredAuraAbility());

        var context = CreateContext(source, target);
        var executor = CreateExecutor();

        var result = executor.TryStartAbility(
            context,
            source.Key,
            target.Key,
            "stance-strike"
        );

        Assert.False(result.Success);
        Assert.Contains(
            "requires source aura",
            result.FailureReason ?? string.Empty,
            StringComparison.OrdinalIgnoreCase
        );
        Assert.Equal(50m, source.Resources["rage"].Current);
    }

    [Fact]
    public void ActiveRequiredSourceAura_AllowsAbilityUse()
    {
        var source = CreateActor("source", "raid");
        var target = CreateActor("target", "enemy");
        source.AddResource(new ResourceState("rage", 100m, 50m));
        source.AddAbility(CreateRequiredAuraAbility());
        AddAura(source, "required-stance", expiresAtSeconds: 10m);

        var context = CreateContext(source, target);
        var executor = CreateExecutor();

        var use = executor.TryStartAbility(
            context,
            source.Key,
            target.Key,
            "stance-strike"
        );

        Assert.True(use.Success, use.FailureReason ?? "Ability use failed.");
    }

    [Fact]
    public void ExpiredRequiredSourceAura_DoesNotSatisfyRequirement()
    {
        var source = CreateActor("source", "raid");
        var target = CreateActor("target", "enemy");
        source.AddResource(new ResourceState("rage", 100m, 50m));
        source.AddAbility(CreateRequiredAuraAbility());
        AddAura(source, "required-stance", expiresAtSeconds: 0m);

        var context = CreateContext(source, target);
        var executor = CreateExecutor();

        var use = executor.TryStartAbility(
            context,
            source.Key,
            target.Key,
            "stance-strike"
        );

        Assert.False(use.Success);
    }

    [Fact]
    public void DamageMultiplierAndLiveStatPercent_AreAppliedBeforeMitigation()
    {
        var source = CreateActor("source", "raid");
        var target = CreateActor("target", "enemy");
        source.Stats.Set("off-hand-damage-percent", 20m);
        source.AddAbility(
            new AbilityDefinition
            {
                Key = "multiplier-test",
                Name = "Multiplier Test",
                IsOffGlobalCooldown = true,
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
                        MinimumValue = 100m,
                        MaximumValue = 100m,
                        DamageMultiplier = 0.5m,
                        DamageMultiplierStatKey = "off-hand-damage-percent"
                    }
                ]
            }
        );

        var context = CreateContext(source, target);
        var executor = CreateExecutor();
        var result = new SimulationEngine([executor]).Run(
            context,
            started =>
            {
                var use = executor.TryStartAbility(
                    started,
                    source.Key,
                    target.Key,
                    "multiplier-test"
                );
                Assert.True(use.Success, use.FailureReason ?? "Ability use failed.");
            }
        );

        var damage = Assert.Single(
            result.Timeline,
            combatEvent => combatEvent.Type == CombatEventType.Damage
        );

        Assert.Equal(60m, damage.RawAmount);
        Assert.Equal(60m, damage.Amount);
    }

    [Fact]
    public void Validator_RejectsNegativeEffectDamageMultiplier()
    {
        var source = CreateActor("source", "raid");
        source.AddAbility(
            new AbilityDefinition
            {
                Key = "bad-multiplier",
                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key = "damage",
                        EffectType = AbilityEffectTypes.DirectDamage,
                        DamageMultiplier = -1m
                    }
                ]
            }
        );

        var context = new SimulationContext(
            new SimulationRunOptions
            {
                DurationSeconds = 0.1m,
                PrimaryActorKey = source.Key
            }
        );
        context.AddActor(source);

        var exception = Assert.Throws<SimulationDefinitionValidationException>(
            () => new SimulationEngine().Run(context)
        );

        Assert.Contains(
            exception.Errors,
            error => error.Contains("negative damage multiplier", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void Validator_RejectsDuplicateRequiredSourceAuraKeys()
    {
        var source = CreateActor("source", "raid");
        source.AddAbility(
            new AbilityDefinition
            {
                Key = "duplicate-aura-requirement",
                RequiredSourceAuraKeys =
                [
                    "berserker-stance",
                    "BERSERKER-STANCE"
                ]
            }
        );

        var context = new SimulationContext(
            new SimulationRunOptions
            {
                DurationSeconds = 0.1m,
                PrimaryActorKey = source.Key
            }
        );
        context.AddActor(source);

        var exception = Assert.Throws<SimulationDefinitionValidationException>(
            () => new SimulationEngine().Run(context)
        );

        Assert.Contains(
            exception.Errors,
            error => error.Contains("duplicate required source aura key", StringComparison.OrdinalIgnoreCase)
        );
    }

    private static AbilityDefinition CreateRequiredAuraAbility()
    {
        return new AbilityDefinition
        {
            Key = "stance-strike",
            Name = "Stance Strike",
            RequiredSourceAuraKeys = ["required-stance"],
            ResourceCosts =
            [
                new AbilityResourceCost
                {
                    ResourceKey = "rage",
                    Amount = 10m
                }
            ],
            Effects =
            [
                new AbilityEffectDefinition
                {
                    Key = "damage",
                    EffectType = AbilityEffectTypes.DirectDamage,
                    ResolutionType = CombatResolutionTypes.AlwaysHits,
                    CanMiss = false,
                    CanCrit = false,
                    MinimumValue = 1m,
                    MaximumValue = 1m
                }
            ]
        };
    }

    private static void AddAura(
        SimulationActorState actor,
        string key,
        decimal expiresAtSeconds)
    {
        actor.ActiveAuras.Add(
            new AuraInstance
            {
                Definition = new AuraDefinition
                {
                    Key = key,
                    Name = key,
                    DurationSeconds = Math.Max(0m, expiresAtSeconds)
                },
                SourceActorKey = actor.Key,
                TargetActorKey = actor.Key,
                AppliedAtSeconds = 0m,
                ExpiresAtSeconds = expiresAtSeconds
            }
        );
    }

    private static SimulationContext CreateContext(
        SimulationActorState source,
        SimulationActorState target)
    {
        var context = new SimulationContext(
            new SimulationRunOptions
            {
                DurationSeconds = 0.1m,
                CaptureTimeline = true,
                PrimaryActorKey = source.Key,
                Seed = 1
            }
        );
        context.AddActor(source);
        context.AddActor(target);
        return context;
    }

    private static SimulationActorState CreateActor(
        string key,
        string teamKey)
    {
        var actor = new SimulationActorState
        {
            Key = key,
            Name = key,
            TeamKey = teamKey,
            Level = 60
        };
        actor.InitializeHealth(5000m);
        return actor;
    }

    private static AbilityExecutor CreateExecutor()
    {
        return new AbilityExecutor(
            new AuraManager(),
            new SimpleCombatRollResolver(
                hitChancePercent: 100m,
                criticalChancePercent: 0m
            ),
            new RulesetDamageMitigationResolver(
                new CombatRulesetDefinition
                {
                    RulesetKey = "ability-state-tests",
                    Version = "1"
                }
            )
        );
    }
}
