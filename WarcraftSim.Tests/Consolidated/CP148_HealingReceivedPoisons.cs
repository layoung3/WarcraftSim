using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Rogue;
using Xunit;

namespace WarcraftSim.Tests;

/// <summary>CP148: real downstream healing received, not an inert poison aura.</summary>
public sealed class Cp148HealingReceivedPoisonsTests
{
    [Fact]
    public void HealingReceivedAuraAppliesOnlyWhileActive()
    {
        var tank = Actor("tank", "raid", 10000m, 2000m);
        tank.ActiveAuras.Add(new AuraInstance {
            Definition = new AuraDefinition { Key = "wound-poison", DurationSeconds = 4m,
                HealingReceivedMultiplier = .5m },
            SourceActorKey = "rogue", TargetActorKey = tank.Key,
            AppliedAtSeconds = 0m, ExpiresAtSeconds = 4m
        });
        Assert.Equal(500m, HealingReceivedModifiers.Apply(tank, 1m, 1000m));
        Assert.Equal(1000m, HealingReceivedModifiers.Apply(tank, 4m, 1000m));
        Assert.Equal(1000m, HealingReceivedModifiers.Apply(tank, 5m, 1000m));
    }

    [Fact]
    public void WoundPoisonAppliesConfigurableDebuffFromSuccessfulWeaponHit()
    {
        var rogue = Actor("rogue", "raid", 4000m, 4000m);
        var target = Actor("enemy", "enemy", 12000m, 12000m);
        var context = Context(rogue, target);
        var request = RogueSimRequest.CreatePreset("combat");
        request.MainHandPoison = "wound";
        request.OffHandPoison = "none";
        request.Tuning.InstantPoisonChancePercent = 100m;
        request.Tuning.WoundPoisonHealingReductionPercent = 35m;
        request.Tuning.Validate();
        var auras = new AuraManager();
        var executor = Executor(auras);
        var poison = new RoguePoisonProcessor(request, rogue, auras, executor);
        poison.Process(context, new CombatEvent {
            TimeSeconds = 0m, Type = CombatEventType.Damage,
            SourceActorKey = rogue.Key, TargetActorKey = target.Key,
            AbilityKey = "main-hand-auto", WeaponHandKey = WeaponHandKeys.MainHand,
            Amount = 100m, RawAmount = 100m
        });
        Assert.Equal(1, poison.Applications);
        Assert.Equal(650m, HealingReceivedModifiers.Apply(target, 0m, 1000m));
    }

    [Fact]
    public void WoundPoisonReducesActualResolvedHealingNotOverhealing()
    {
        var healer = Actor("shaman", "raid", 5000m, 5000m);
        var target = Actor("tank", "raid", 10000m, 2000m);
        var context = Context(healer, target);
        healer.AddAbility(new AbilityDefinition {
            Key = "test-heal", Name = "Test Heal", ClassKey = "shaman",
            Effects = [new AbilityEffectDefinition {
                Key = "heal", EffectType = AbilityEffectTypes.DirectHealing,
                TargetType = AbilityTargetTypes.Friendly,
                ResolutionType = CombatResolutionTypes.AlwaysHits,
                CanMiss = false, CanCrit = false,
                MinimumValue = 1000m, MaximumValue = 1000m
            }]
        });
        var aura = new AuraManager();
        var executor = Executor(aura);
        var run = new SimulationEngine([executor, aura]).Run(context, started => {
            aura.ApplyAura(started, target, new AuraDefinition {
                Key = "wound-poison", Name = "Wound Poison", DurationSeconds = 10m,
                HealingReceivedMultiplier = .5m
            }, "rogue", "wound-poison", "wound-debuff");
            var result = executor.TryStartAbility(started, healer.Key, target.Key, "test-heal");
            Assert.True(result.Success, result.FailureReason);
        });
        var heal = Assert.Single(run.Timeline, e => e.Type == CombatEventType.Healing &&
            e.AbilityKey == "test-heal");
        Assert.Equal(500m, heal.RawAmount);
        Assert.Equal(500m, heal.Amount);
        Assert.Equal(0m, heal.OverhealingAmount);
        Assert.Equal(2500m, target.CurrentHealth);
    }

    [Fact]
    public void MindNumbingPoisonSlowsActualCastCompletionWithoutChangingInstants()
    {
        var caster = Actor("enemy-caster", "enemy", 5000m, 5000m);
        var enemy = Actor("rogue", "raid", 5000m, 5000m);
        var context = Context(caster, enemy);
        caster.AddAbility(new AbilityDefinition {
            Key = "enemy-spell", Name = "Enemy Spell", CastTimeSeconds = 2m,
            Effects = [new AbilityEffectDefinition {
                Key = "damage", EffectType = AbilityEffectTypes.DirectDamage,
                TargetType = AbilityTargetTypes.Enemy,
                ResolutionType = CombatResolutionTypes.AlwaysHits,
                CanMiss = false, CanCrit = false,
                MinimumValue = 100m, MaximumValue = 100m
            }]
        });
        var aura = new AuraManager();
        var executor = Executor(aura);
        var run = new SimulationEngine([executor, aura]).Run(context, started => {
            aura.ApplyAura(started, caster, new AuraDefinition {
                Key = "mind-numbing-poison", DurationSeconds = 10m,
                CastTimeMultiplier = 1.5m
            }, "rogue", "mind-numbing-poison", "mind-numbing-debuff");
            var result = executor.TryStartAbility(started, caster.Key, enemy.Key, "enemy-spell");
            Assert.True(result.Success, result.FailureReason);
        });
        var cast = Assert.Single(run.Timeline, e => e.Type == CombatEventType.AbilityCastCompleted &&
            e.AbilityKey == "enemy-spell");
        Assert.Equal(3m, cast.TimeSeconds);
        var instant = new AbilityDefinition { Key = "instant", CastTimeSeconds = 0m };
        var snapshot = BaseAbilityTimingProvider.Instance.Resolve(context, caster, instant);
        Assert.Equal(0m, snapshot.CastTimeSeconds);
        Assert.Equal(0m, BaseAbilityTimingProvider.Instance.Resolve(context, caster,
            new AbilityDefinition { Key = "cast", CastTimeSeconds = 2m }).CastTimeSeconds - 2m);
        // The last check is after the 10-second scenario and aura expiration.
    }

    [Fact]
    public void QuickRecoveryRefundUsesCurrentlyConfiguredFinisherCost()
    {
        var rogue = Actor("rogue", "raid", 4000m, 4000m);
        var target = Actor("target", "enemy", 10000m, 10000m);
        var context = Context(rogue, target);
        rogue.AddResource(new ResourceState("energy", 100m, 20m));
        rogue.AddAbility(new AbilityDefinition {
            Key = "eviscerate", Name = "Eviscerate",
            ResourceCosts = [new AbilityResourceCost { ResourceKey = "energy", Amount = 17m }]
        });
        var request = RogueSimRequest.CreatePreset("assassination");
        request.Talents.Ranks["quick-recovery"] = 2;
        request.Tuning.QuickRecoveryEnergyRefundPercentPerRank = 40m;
        var auras = new AuraManager();
        var executor = Executor(auras);
        var combat = new RogueCombatProcessor(request, rogue,
            new RotationProfile(), executor, auras,
            new AutoAttackProcessor(executor), []);
        combat.Process(context, new CombatEvent {
            Type = CombatEventType.Damage, TimeSeconds = 0m,
            SourceActorKey = rogue.Key, TargetActorKey = target.Key,
            AbilityKey = "eviscerate", AbilityExecutionId = Guid.NewGuid(),
            ResultKey = CombatResultTypes.Miss, RawAmount = 0m, Amount = 0m
        });
        Assert.Equal(13.6m, combat.QuickRecoveryEnergyRestored);
        Assert.Equal(33.6m, rogue.Resources["energy"].Current);
    }

    private static SimulationActorState Actor(string key, string team, decimal max, decimal hp)
    {
        var actor = new SimulationActorState {
            Key = key, Name = key, TeamKey = team, Level = 60, MaximumHealth = max
        };
        actor.InitializeHealth(max, hp);
        return actor;
    }

    private static SimulationContext Context(SimulationActorState first, SimulationActorState second)
    {
        var context = new SimulationContext(new SimulationRunOptions {
            DurationSeconds = 10m, PrimaryActorKey = first.Key,
            CaptureTimeline = true, Seed = 14801
        });
        context.AddActor(first);
        context.AddActor(second);
        return context;
    }

    private static AbilityExecutor Executor(AuraManager aura) => new(aura,
        new SimpleCombatRollResolver(hitChancePercent: 100m, criticalChancePercent: 0m),
        new RulesetDamageMitigationResolver(new CombatRulesetDefinition {
            RulesetKey = "cp148-healing", Version = "1"
        }));
}
