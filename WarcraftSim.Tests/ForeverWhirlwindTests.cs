using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Abilities.Warrior;
using WarcraftSim.Data.Forever.Characters;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Tests;

public sealed class ForeverWhirlwindTests
{
    [Fact]
    public void NormalizedWeaponSpeedConstants_MatchClassicForeverInstantAttackRules()
    {
        Assert.Equal(1.7m, ForeverNormalizedWeaponSpeeds.Dagger);
        Assert.Equal(2.4m, ForeverNormalizedWeaponSpeeds.OneHanded);
        Assert.Equal(3.3m, ForeverNormalizedWeaponSpeeds.TwoHanded);
        Assert.Equal(2.8m, ForeverNormalizedWeaponSpeeds.Ranged);
    }

    [Fact]
    public void SingleWeaponFactory_CreatesFourTargetNormalizedMainHandAttack()
    {
        var ability = ForeverWhirlwindFactory.CreateSingleWeapon(
            100m,
            120m,
            ForeverNormalizedWeaponSpeeds.TwoHanded,
            "two-hand-sword-skill"
        );

        Assert.Equal(ForeverWhirlwindFactory.AbilityKey, ability.Key);
        Assert.Equal("Whirlwind", ability.Name);
        Assert.Equal(ForeverCharacterClassKeys.Warrior, ability.ClassKey);
        Assert.Equal(36, ability.RequiredLevel);
        Assert.Equal(10m, ability.CooldownSeconds);
        Assert.Equal(1.5m, ability.GlobalCooldownSeconds);
        Assert.Equal(25m, Assert.Single(ability.ResourceCosts).Amount);
        Assert.Equal(
            ForeverWarriorAuraKeys.BerserkerStance,
            Assert.Single(ability.RequiredSourceAuraKeys)
        );

        var effect = Assert.Single(ability.Effects);
        Assert.Equal(WeaponHandKeys.MainHand, effect.WeaponHandKey);
        Assert.Equal(4, effect.MaxTargets);
        Assert.Equal(3.3m / 14m, effect.ScalingCoefficient);
        Assert.Equal(1m, effect.DamageMultiplier);
        Assert.Null(effect.DamageMultiplierStatKey);
    }

    [Fact]
    public void DualWieldFactory_CreatesIndependentMainHandAndOffHandEffects()
    {
        var ability = ForeverWhirlwindFactory.CreateDualWield(
            100m,
            100m,
            ForeverNormalizedWeaponSpeeds.OneHanded,
            "sword-skill",
            80m,
            80m,
            ForeverNormalizedWeaponSpeeds.Dagger,
            "dagger-skill"
        );

        Assert.Equal(2, ability.Effects.Count);

        var mainHand = Assert.Single(
            ability.Effects,
            effect => effect.WeaponHandKey == WeaponHandKeys.MainHand
        );
        var offHand = Assert.Single(
            ability.Effects,
            effect => effect.WeaponHandKey == WeaponHandKeys.OffHand
        );

        Assert.Equal(2.4m / 14m, mainHand.ScalingCoefficient);
        Assert.Equal(1.7m / 14m, offHand.ScalingCoefficient);
        Assert.Equal(0.5m, offHand.DamageMultiplier);
        Assert.Equal(ForeverCombatStatKeys.OffHandDamagePercent, offHand.DamageMultiplierStatKey);
        Assert.Equal(ForeverCombatResolutionTypes.PlayerMeleeSpecial, mainHand.ResolutionType);
        Assert.Equal(ForeverCombatResolutionTypes.PlayerMeleeSpecial, offHand.ResolutionType);
    }

    [Fact]
    public void RagingBlowsStyleCostReduction_ReducesWhirlwindByThreeRage()
    {
        var ability = ForeverWhirlwindFactory.CreateSingleWeapon(
            100m,
            100m,
            ForeverNormalizedWeaponSpeeds.TwoHanded,
            "skill",
            rageCostReduction: 3m
        );

        Assert.Equal(22m, Assert.Single(ability.ResourceCosts).Amount);
    }

    [Fact]
    public void MissingBerserkerStance_PreventsWhirlwindWithoutSpendingRage()
    {
        var source = CreateActor("warrior", "raid", 100m);
        var target = CreateActor("boss", "enemy", 0m);
        source.AddAbility(
            ForeverWhirlwindFactory.CreateSingleWeapon(
                100m,
                100m,
                ForeverNormalizedWeaponSpeeds.TwoHanded,
                "skill"
            )
        );

        var context = CreateContext(source, new[] { target });
        var executor = CreateExecutor();
        var use = executor.TryStartAbility(
            context,
            source.Key,
            target.Key,
            ForeverWhirlwindFactory.AbilityKey
        );

        Assert.False(use.Success);
        Assert.Equal(100m, source.Resources["rage"].Current);
    }

    [Fact]
    public void DualWieldExecution_HitsAtMostFourEnemiesWithBothWeapons()
    {
        var source = CreateActor("warrior", "raid", 100m);
        source.Stats.Set(ForeverCombatStatKeys.AttackPower, 0m);
        source.Stats.Set(ForeverCombatStatKeys.OffHandDamagePercent, 20m);
        AddBerserkerStance(source);
        source.AddAbility(
            ForeverWhirlwindFactory.CreateDualWield(
                100m,
                100m,
                ForeverNormalizedWeaponSpeeds.OneHanded,
                "skill",
                80m,
                80m,
                ForeverNormalizedWeaponSpeeds.OneHanded,
                "skill"
            )
        );

        var enemies = Enumerable.Range(1, 5)
            .Select(index => CreateActor($"enemy-{index}", "enemy", 0m))
            .ToList();

        var context = CreateContext(source, enemies);
        var executor = CreateExecutor();
        var result = new SimulationEngine([executor]).Run(
            context,
            started =>
            {
                var use = executor.TryStartAbility(
                    started,
                    source.Key,
                    enemies[0].Key,
                    ForeverWhirlwindFactory.AbilityKey
                );
                Assert.True(use.Success, use.FailureReason ?? "Ability use failed.");
            }
        );

        var whirlwindDamage = result.Timeline
            .Where(combatEvent =>
                combatEvent.Type == CombatEventType.Damage &&
                combatEvent.AbilityKey == ForeverWhirlwindFactory.AbilityKey)
            .ToList();

        Assert.Equal(8, whirlwindDamage.Count);
        Assert.Equal(4, whirlwindDamage.Select(combatEvent => combatEvent.TargetActorKey).Distinct().Count());
        Assert.DoesNotContain(whirlwindDamage, combatEvent => combatEvent.TargetActorKey == "enemy-5");

        var mainHandDamage = whirlwindDamage
            .Where(combatEvent => combatEvent.EffectKey == "main-hand-damage")
            .ToList();
        var offHandDamage = whirlwindDamage
            .Where(combatEvent => combatEvent.EffectKey == "off-hand-damage")
            .ToList();

        Assert.All(mainHandDamage, damage => Assert.Equal(100m, damage.RawAmount));
        Assert.All(offHandDamage, damage => Assert.Equal(48m, damage.RawAmount));
        Assert.Equal(75m, source.Resources["rage"].Current);
    }

    [Fact]
    public void UnsupportedFactoryInputs_FailExplicitly()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverWhirlwindFactory.CreateSingleWeapon(1m, 1m, 0m, "skill"));
        Assert.Throws<ArgumentException>(() =>
            ForeverWhirlwindFactory.CreateSingleWeapon(1m, 1m, 2.4m, ""));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverWhirlwindFactory.CreateSingleWeapon(1m, 1m, 2.4m, "skill", -1m));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverWhirlwindFactory.CreateDualWield(1m, 1m, 2.4m, "skill", 1m, 1m, 0m, "skill"));
        Assert.Throws<ArgumentException>(() =>
            ForeverWhirlwindFactory.CreateDualWield(1m, 1m, 2.4m, "skill", 1m, 1m, 2.4m, ""));
    }

    private static void AddBerserkerStance(SimulationActorState actor)
    {
        actor.ActiveAuras.Add(
            new AuraInstance
            {
                Definition = new AuraDefinition
                {
                    Key = ForeverWarriorAuraKeys.BerserkerStance,
                    Name = "Berserker Stance",
                    DurationSeconds = 3600m
                },
                SourceActorKey = actor.Key,
                TargetActorKey = actor.Key,
                AppliedAtSeconds = 0m,
                ExpiresAtSeconds = 3600m
            }
        );
    }

    private static SimulationContext CreateContext(
        SimulationActorState source,
        IEnumerable<SimulationActorState> enemies)
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
        foreach (var enemy in enemies)
        {
            context.AddActor(enemy);
        }
        return context;
    }

    private static SimulationActorState CreateActor(
        string key,
        string teamKey,
        decimal rage)
    {
        var actor = new SimulationActorState
        {
            Key = key,
            Name = key,
            TeamKey = teamKey,
            Level = 60
        };
        actor.InitializeHealth(10000m);
        actor.AddResource(new ResourceState("rage", 100m, rage));
        return actor;
    }

    private static AbilityExecutor CreateExecutor()
    {
        return new AbilityExecutor(
            new AuraManager(),
            new SimpleCombatRollResolver(100m, 0m),
            new RulesetDamageMitigationResolver(
                new CombatRulesetDefinition
                {
                    RulesetKey = "whirlwind-tests",
                    Version = "1",
                    MitigationRules =
                    {
                        [DamageMitigationTypes.Armor] = new DamageMitigationRuleDefinition
                        {
                            MitigationType = DamageMitigationTypes.Armor,
                            FormulaType = DamageMitigationFormulaTypes.RationalLevelScaled,
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
}
