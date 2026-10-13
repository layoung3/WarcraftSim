// Consolidated tests for Forever Abilities.
// Original class names are retained for xUnit test filtering.

// ==================== ForeverExecuteTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Abilities.Warrior;
using WarcraftSim.Data.Forever.Characters;
using WarcraftSim.Data.Forever.Combat;

public sealed class ForeverExecuteTests
{
    [Theory]
    [InlineData(1, 5308, 24, 125, 3)]
    [InlineData(2, 20658, 32, 200, 6)]
    [InlineData(3, 20660, 40, 325, 9)]
    [InlineData(4, 20661, 48, 450, 12)]
    [InlineData(5, 20662, 56, 600, 15)]
    public void RankTable_UsesForeverClientValues(
        int rank,
        int spellId,
        int requiredLevel,
        decimal baseDamage,
        decimal damagePerExtraRage)
    {
        var definition = ForeverExecuteRanks.GetByRank(rank);

        Assert.Equal(rank, definition.Rank);
        Assert.Equal(spellId, definition.SpellId);
        Assert.Equal(requiredLevel, definition.RequiredLevel);
        Assert.Equal(baseDamage, definition.BaseDamage);
        Assert.Equal(damagePerExtraRage, definition.DamagePerExtraRage);
        Assert.Equal(15m, definition.RageCost);
        Assert.Equal(1.5m, definition.GlobalCooldownSeconds);
    }

    [Theory]
    [InlineData(24, 1)]
    [InlineData(31, 1)]
    [InlineData(32, 2)]
    [InlineData(60, 5)]
    public void HighestAvailableRank_UsesCharacterLevel(
        int characterLevel,
        int expectedRank)
    {
        var rank = ForeverExecuteRanks.GetHighestAvailable(characterLevel);
        Assert.Equal(expectedRank, rank.Rank);
    }

    [Fact]
    public void LevelThirtyFactory_CreatesRankOneExecute()
    {
        var execute = ForeverExecuteFactory.CreateForLevel(
            30,
            "sword-skill"
        );

        Assert.Equal(ForeverExecuteFactory.AbilityKey, execute.Key);
        Assert.Equal("Execute (Rank 1)", execute.Name);
        Assert.Equal(ForeverCharacterClassKeys.Warrior, execute.ClassKey);
        Assert.Equal(24, execute.RequiredLevel);
        Assert.Equal(20m, execute.MaximumTargetHealthPercent);
        Assert.Equal(1.5m, execute.GlobalCooldownSeconds);
        Assert.False(execute.IsOffGlobalCooldown);

        var cost = Assert.Single(execute.ResourceCosts);
        Assert.Equal(ForeverExecuteFactory.RageResourceKey, cost.ResourceKey);
        Assert.Equal(15m, cost.Amount);

        var additional = Assert.Single(execute.AdditionalResourceConsumptions);
        Assert.Equal(ForeverExecuteFactory.RageResourceKey, additional.ResourceKey);
        Assert.Null(additional.MaximumAmount);

        var effect = Assert.Single(execute.Effects);
        Assert.Equal(WeaponHandKeys.MainHand, effect.WeaponHandKey);
        Assert.Equal(125m, effect.MinimumValue);
        Assert.Equal(125m, effect.MaximumValue);
        Assert.Equal("rage", effect.ConsumedResourceScalingKey);
        Assert.Equal(3m, effect.ConsumedResourceScalingCoefficient);
        Assert.Equal(ForeverCombatResolutionTypes.PlayerMeleeSpecial, effect.ResolutionType);
        Assert.Equal("sword-skill", effect.AttackSkillStatKey);
        Assert.True(effect.CanMiss);
        Assert.True(effect.CanBeDodged);
        Assert.True(effect.CanBeParried);
        Assert.True(effect.CanBeBlocked);
        Assert.True(effect.CanCrit);
        Assert.False(effect.CanGlance);
        Assert.Equal(DamageMitigationTypes.Armor, effect.MitigationType);
    }

    [Fact]
    public void RageCostReduction_ChangesBaseCostWithoutChangingExtraRageConversion()
    {
        var execute = ForeverExecuteFactory.CreateForLevel(
            30,
            "sword-skill",
            rageCostReduction: 3m
        );

        Assert.Equal(12m, Assert.Single(execute.ResourceCosts).Amount);
        Assert.Equal(3m, Assert.Single(execute.Effects).ConsumedResourceScalingCoefficient);
    }

    [Fact]
    public void RankOneExecution_ConsumesRemainingRageAndAddsThreeDamagePerPoint()
    {
        var source = CreateActor("warrior", 1000m, 30m);
        var target = CreateActor("target", 1000m, 0m, 200m);
        source.AddAbility(ForeverExecuteFactory.CreateForLevel(30, "sword-skill"));

        var result = RunExecute(source, target);

        Assert.Equal(0m, source.Resources["rage"].Current);

        var damage = Assert.Single(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.Damage &&
                combatEvent.AbilityKey == ForeverExecuteFactory.AbilityKey
        );

        // 30 starting Rage - 15 base cost = 15 extra Rage.
        Assert.Equal(170m, damage.RawAmount);
        Assert.Equal(170m, damage.Amount);
    }

    [Fact]
    public void RankOneExecution_WithOnlyBaseCostDealsBaseDamage()
    {
        var source = CreateActor("warrior", 1000m, 15m);
        var target = CreateActor("target", 1000m, 0m, 200m);
        source.AddAbility(ForeverExecuteFactory.CreateForLevel(30, "sword-skill"));

        var result = RunExecute(source, target);

        var damage = Assert.Single(
            result.Timeline,
            combatEvent => combatEvent.Type == CombatEventType.Damage
        );

        Assert.Equal(125m, damage.RawAmount);
        Assert.Equal(0m, source.Resources["rage"].Current);
    }

    [Fact]
    public void ExecuteAboveTwentyPercent_FailsWithoutSpendingRage()
    {
        var source = CreateActor("warrior", 1000m, 30m);
        var target = CreateActor("target", 1000m, 0m, 201m);
        source.AddAbility(ForeverExecuteFactory.CreateForLevel(30, "sword-skill"));

        var context = CreateContext(source, target);
        var executor = CreateExecutor();
        var result = executor.TryStartAbility(
            context,
            source.Key,
            target.Key,
            ForeverExecuteFactory.AbilityKey
        );

        Assert.False(result.Success);
        Assert.Equal(30m, source.Resources["rage"].Current);
    }

    [Fact]
    public void UnsupportedFactoryInputs_FailExplicitly()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverExecuteFactory.CreateForLevel(23, "sword-skill"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverExecuteFactory.CreateForLevel(61, "sword-skill"));
        Assert.Throws<ArgumentException>(() =>
            ForeverExecuteFactory.CreateForLevel(30, ""));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverExecuteFactory.CreateForLevel(30, "sword-skill", -1m));
    }

    private static SimulationRunResult RunExecute(
        SimulationActorState source,
        SimulationActorState target)
    {
        var context = CreateContext(source, target);
        var executor = CreateExecutor();

        return new SimulationEngine([executor]).Run(
            context,
            started =>
            {
                var use = executor.TryStartAbility(
                    started,
                    source.Key,
                    target.Key,
                    ForeverExecuteFactory.AbilityKey
                );
                Assert.True(use.Success, use.FailureReason ?? "Ability use failed.");
            }
        );
    }

    private static SimulationContext CreateContext(
        SimulationActorState source,
        SimulationActorState target)
    {
        var context = new SimulationContext(new SimulationRunOptions
        {
            DurationSeconds = 0.1m,
            CaptureTimeline = true,
            PrimaryActorKey = source.Key,
            Seed = 1
        });
        context.AddActor(source);
        context.AddActor(target);
        return context;
    }

    private static AbilityExecutor CreateExecutor()
    {
        var ruleset = new CombatRulesetDefinition
        {
            RulesetKey = "execute-tests",
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
        };

        return new AbilityExecutor(
            new AuraManager(),
            new SimpleCombatRollResolver(
                hitChancePercent: 100m,
                criticalChancePercent: 0m
            ),
            new RulesetDamageMitigationResolver(ruleset)
        );
    }

    private static SimulationActorState CreateActor(
        string key,
        decimal maximumHealth,
        decimal rage,
        decimal? startingHealth = null)
    {
        var actor = new SimulationActorState
        {
            Key = key,
            Name = key,
            TeamKey = key,
            Level = 30
        };
        actor.InitializeHealth(maximumHealth, startingHealth);
        actor.AddResource(new ResourceState("rage", 100m, rage));
        return actor;
    }
}
}

// ==================== ForeverHeroicStrikeTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Data.Forever.Abilities.Warrior;
using WarcraftSim.Data.Forever.Combat;

public sealed class ForeverHeroicStrikeTests
{
    [Theory]
    [InlineData(1, 78, 1, 11)]
    [InlineData(2, 284, 8, 21)]
    [InlineData(3, 285, 16, 32)]
    [InlineData(4, 1608, 24, 44)]
    [InlineData(5, 11564, 32, 58)]
    [InlineData(6, 11565, 40, 80)]
    [InlineData(7, 11566, 48, 111)]
    [InlineData(8, 11567, 56, 138)]
    [InlineData(9, 25286, 60, 157)]
    public void RankTable_UsesForeverClientValues(
        int rank,
        int spellId,
        int requiredLevel,
        decimal bonusWeaponDamage)
    {
        var definition =
            ForeverHeroicStrikeRanks.GetByRank(rank);

        Assert.Equal(rank, definition.Rank);
        Assert.Equal(spellId, definition.SpellId);
        Assert.Equal(requiredLevel, definition.RequiredLevel);
        Assert.Equal(
            bonusWeaponDamage,
            definition.BonusWeaponDamage
        );
        Assert.Equal(15m, definition.RageCost);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(8, 2)]
    [InlineData(30, 4)]
    [InlineData(60, 9)]
    public void HighestAvailableRank_UsesCharacterLevel(
        int characterLevel,
        int expectedRank)
    {
        var rank =
            ForeverHeroicStrikeRanks.GetHighestAvailable(
                characterLevel
            );

        Assert.Equal(expectedRank, rank.Rank);
    }

    [Fact]
    public void LevelThirtyFactory_CreatesRankFourMainHandSpecial()
    {
        var heroicStrike =
            ForeverHeroicStrikeFactory.CreateForLevel(
                characterLevel: 30,
                minimumWeaponDamage: 100m,
                maximumWeaponDamage: 120m,
                weaponSpeedSeconds: 2.8m,
                attackSkillStatKey: "sword-skill"
            );

        Assert.Equal(
            ForeverHeroicStrikeFactory.AbilityKey,
            heroicStrike.Key
        );
        Assert.Equal(
            "Heroic Strike (Rank 4)",
            heroicStrike.Name
        );
        Assert.Equal(
            WeaponHandKeys.MainHand,
            heroicStrike.WeaponHandKey
        );

        var effect =
            heroicStrike.DamageEffect;

        Assert.Equal(
            WeaponHandKeys.MainHand,
            effect.WeaponHandKey
        );
        Assert.Equal(144m, effect.MinimumValue);
        Assert.Equal(164m, effect.MaximumValue);
        Assert.Equal(
            ForeverCombatStatKeys.AttackPower,
            effect.ScalingStatKey
        );
        Assert.Equal(0.2m, effect.ScalingCoefficient);
        Assert.Equal(
            ForeverCombatResolutionTypes.PlayerMeleeSpecial,
            effect.ResolutionType
        );
        Assert.Equal("sword-skill", effect.AttackSkillStatKey);
        Assert.True(effect.CanMiss);
        Assert.True(effect.CanBeDodged);
        Assert.True(effect.CanBeParried);
        Assert.True(effect.CanBeBlocked);
        Assert.True(effect.CanCrit);
        Assert.False(effect.CanGlance);
        Assert.False(effect.CanCrush);
        Assert.Equal(
            DamageMitigationTypes.Armor,
            effect.MitigationType
        );

        var cost =
            Assert.Single(heroicStrike.ResourceCosts);

        Assert.Equal(
            ForeverHeroicStrikeFactory.RageResourceKey,
            cost.ResourceKey
        );
        Assert.Equal(15m, cost.Amount);
        Assert.False(cost.IsPercentOfMaximum);

        // Refund behavior remains server-side/unverified and must be opted in
        // separately once confirmed rather than inherited from Classic.
        Assert.Empty(heroicStrike.ResourceRefunds);
    }

    [Fact]
    public void RageCostReduction_ChangesOnlyTheConfiguredCost()
    {
        var heroicStrike =
            ForeverHeroicStrikeFactory.CreateForLevel(
                characterLevel: 30,
                minimumWeaponDamage: 100m,
                maximumWeaponDamage: 100m,
                weaponSpeedSeconds: 2.8m,
                attackSkillStatKey: "sword-skill",
                rageCostReduction: 3m
            );

        var cost =
            Assert.Single(heroicStrike.ResourceCosts);

        Assert.Equal(12m, cost.Amount);
        Assert.Equal(144m, heroicStrike.DamageEffect.MinimumValue);
        Assert.Equal(144m, heroicStrike.DamageEffect.MaximumValue);
    }

    [Fact]
    public void UnsupportedFactoryInputs_FailExplicitly()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverHeroicStrikeFactory.CreateForLevel(
                0,
                100m,
                100m,
                2.8m,
                "sword-skill"
            )
        );

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverHeroicStrikeFactory.CreateForLevel(
                61,
                100m,
                100m,
                2.8m,
                "sword-skill"
            )
        );

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverHeroicStrikeFactory.CreateForLevel(
                30,
                100m,
                100m,
                0m,
                "sword-skill"
            )
        );

        Assert.Throws<ArgumentException>(() =>
            ForeverHeroicStrikeFactory.CreateForLevel(
                30,
                100m,
                100m,
                2.8m,
                ""
            )
        );

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverHeroicStrikeFactory.CreateForLevel(
                30,
                100m,
                100m,
                2.8m,
                "sword-skill",
                -1m
            )
        );
    }
}
}

// ==================== ForeverMortalStrikeTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Abilities.Warrior;
using WarcraftSim.Data.Forever.Characters;
using WarcraftSim.Data.Forever.Combat;

public sealed class ForeverMortalStrikeTests
{
    [Theory]
    [InlineData(1, 12294, 40, 85)]
    [InlineData(2, 21551, 48, 110)]
    [InlineData(3, 21552, 54, 135)]
    [InlineData(4, 21553, 60, 160)]
    public void RankTable_UsesForeverValues(
        int rank,
        int spellId,
        int requiredLevel,
        decimal bonusWeaponDamage)
    {
        var definition = ForeverMortalStrikeRanks.GetByRank(rank);

        Assert.Equal(rank, definition.Rank);
        Assert.Equal(spellId, definition.SpellId);
        Assert.Equal(requiredLevel, definition.RequiredLevel);
        Assert.Equal(bonusWeaponDamage, definition.BonusWeaponDamage);
        Assert.Equal(30m, definition.RageCost);
        Assert.Equal(6m, definition.CooldownSeconds);
        Assert.Equal(1.5m, definition.GlobalCooldownSeconds);
        Assert.Equal(10m, definition.HealingReductionDurationSeconds);
    }

    [Theory]
    [InlineData(40, 1)]
    [InlineData(47, 1)]
    [InlineData(48, 2)]
    [InlineData(60, 4)]
    public void HighestAvailableRank_UsesCharacterLevel(
        int characterLevel,
        int expectedRank)
    {
        Assert.Equal(
            expectedRank,
            ForeverMortalStrikeRanks.GetHighestAvailable(characterLevel).Rank
        );
    }

    [Fact]
    public void LevelSixtyFactory_CreatesRankFourNormalizedMainHandSpecial()
    {
        var ability = ForeverMortalStrikeFactory.CreateForLevel(
            60,
            100m,
            120m,
            ForeverNormalizedWeaponSpeeds.TwoHanded,
            "two-hand-sword-skill"
        );

        Assert.Equal(ForeverMortalStrikeFactory.AbilityKey, ability.Key);
        Assert.Equal("Mortal Strike (Rank 4)", ability.Name);
        Assert.Equal(ForeverCharacterClassKeys.Warrior, ability.ClassKey);
        Assert.Equal(60, ability.RequiredLevel);
        Assert.Equal(6m, ability.CooldownSeconds);
        Assert.Equal(1.5m, ability.GlobalCooldownSeconds);
        Assert.Equal(30m, Assert.Single(ability.ResourceCosts).Amount);

        var damage = Assert.Single(
            ability.Effects,
            effect => effect.Key == "damage"
        );
        Assert.Equal(WeaponHandKeys.MainHand, damage.WeaponHandKey);
        Assert.Equal(260m, damage.MinimumValue);
        Assert.Equal(280m, damage.MaximumValue);
        Assert.Equal(ForeverCombatStatKeys.AttackPower, damage.ScalingStatKey);
        Assert.Equal(3.3m / 14m, damage.ScalingCoefficient);
        Assert.Equal(ForeverCombatResolutionTypes.PlayerMeleeSpecial, damage.ResolutionType);
        Assert.Equal("two-hand-sword-skill", damage.AttackSkillStatKey);

        var marker = Assert.Single(
            ability.Effects,
            effect => effect.Key == "healing-reduction"
        );
        Assert.Equal(ForeverWarriorAuraKeys.MortalStrikeHealingReduction, marker.AuraKey);
        Assert.Equal(10m, marker.DurationSeconds);
        Assert.Equal("damage", marker.DependsOnEffectKey);
    }

    [Fact]
    public void RageCostReduction_ChangesCostOnly()
    {
        var ability = ForeverMortalStrikeFactory.CreateForLevel(
            60,
            100m,
            100m,
            ForeverNormalizedWeaponSpeeds.TwoHanded,
            "skill",
            rageCostReduction: 3m
        );

        Assert.Equal(27m, Assert.Single(ability.ResourceCosts).Amount);
        Assert.Equal(260m, Assert.Single(ability.Effects, effect => effect.Key == "damage").MinimumValue);
    }

    [Fact]
    public void LevelSixtyExecution_UsesNormalizedAttackPowerAndAppliesMarkerAura()
    {
        var source = CreateActor("warrior", "raid", 100m);
        var target = CreateActor("boss", "enemy", 0m);
        source.Stats.Set(ForeverCombatStatKeys.AttackPower, 140m);
        source.AddAbility(
            ForeverMortalStrikeFactory.CreateForLevel(
                60,
                100m,
                100m,
                ForeverNormalizedWeaponSpeeds.TwoHanded,
                "skill"
            )
        );

        var result = RunAbility(source, target, ForeverMortalStrikeFactory.AbilityKey);

        var damage = Assert.Single(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.Damage &&
                combatEvent.AbilityKey == ForeverMortalStrikeFactory.AbilityKey
        );
        Assert.Equal(293m, damage.RawAmount);
        Assert.Equal(70m, source.Resources["rage"].Current);
        Assert.Contains(
            target.ActiveAuras,
            aura => aura.Definition.Key == ForeverWarriorAuraKeys.MortalStrikeHealingReduction
        );
    }

    [Fact]
    public void UnsupportedFactoryInputs_FailExplicitly()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverMortalStrikeFactory.CreateForLevel(39, 1m, 1m, 3.3m, "skill"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverMortalStrikeFactory.CreateForLevel(61, 1m, 1m, 3.3m, "skill"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverMortalStrikeFactory.CreateForLevel(60, 1m, 1m, 0m, "skill"));
        Assert.Throws<ArgumentException>(() =>
            ForeverMortalStrikeFactory.CreateForLevel(60, 1m, 1m, 3.3m, ""));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverMortalStrikeFactory.CreateForLevel(60, 1m, 1m, 3.3m, "skill", -1m));
    }

    private static SimulationRunResult RunAbility(
        SimulationActorState source,
        SimulationActorState target,
        string abilityKey)
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
        var executor = CreateExecutor();

        return new SimulationEngine([executor]).Run(
            context,
            started =>
            {
                var use = executor.TryStartAbility(started, source.Key, target.Key, abilityKey);
                Assert.True(use.Success, use.FailureReason ?? "Ability use failed.");
            }
        );
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
                    RulesetKey = "mortal-strike-tests",
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
}

// ==================== ForeverSlamTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Data.Forever.Abilities.Warrior;
using WarcraftSim.Data.Forever.Characters;
using WarcraftSim.Data.Forever.Combat;

public sealed class ForeverSlamTests
{
    [Theory]
    [InlineData(1, 1240193, 20, 16)]
    [InlineData(2, 1464, 30, 32)]
    [InlineData(3, 8820, 38, 43)]
    [InlineData(4, 11604, 46, 68)]
    [InlineData(5, 11605, 54, 87)]
    public void RankTable_UsesForeverClientValues(
        int rank,
        int spellId,
        int requiredLevel,
        decimal bonusWeaponDamage)
    {
        var definition =
            ForeverSlamRanks.GetByRank(rank);

        Assert.Equal(rank, definition.Rank);
        Assert.Equal(spellId, definition.SpellId);
        Assert.Equal(requiredLevel, definition.RequiredLevel);
        Assert.Equal(bonusWeaponDamage, definition.BonusWeaponDamage);
        Assert.Equal(15m, definition.RageCost);
        Assert.Equal(1.5m, definition.CastTimeSeconds);
        Assert.Equal(18m, definition.CooldownSeconds);
        Assert.Equal(1.5m, definition.GlobalCooldownSeconds);
    }

    [Theory]
    [InlineData(20, 1)]
    [InlineData(29, 1)]
    [InlineData(30, 2)]
    [InlineData(60, 5)]
    public void HighestAvailableRank_UsesCharacterLevel(
        int characterLevel,
        int expectedRank)
    {
        var rank =
            ForeverSlamRanks.GetHighestAvailable(characterLevel);

        Assert.Equal(expectedRank, rank.Rank);
    }

    [Fact]
    public void LevelThirtyFactory_CreatesRankTwoForegroundWeaponSpecial()
    {
        var slam =
            ForeverSlamFactory.CreateForLevel(
                characterLevel: 30,
                minimumWeaponDamage: 100m,
                maximumWeaponDamage: 120m,
                weaponSpeedSeconds: 2.8m,
                attackSkillStatKey: "sword-skill"
            );

        Assert.Equal(ForeverSlamFactory.AbilityKey, slam.Key);
        Assert.Equal("Slam (Rank 2)", slam.Name);
        Assert.Equal(ForeverCharacterClassKeys.Warrior, slam.ClassKey);
        Assert.Equal(30, slam.RequiredLevel);
        Assert.Equal(1.5m, slam.CastTimeSeconds);
        Assert.Equal(18m, slam.CooldownSeconds);
        Assert.Equal(1.5m, slam.GlobalCooldownSeconds);
        Assert.False(slam.IsOffGlobalCooldown);

        var delayedHand =
            Assert.Single(slam.DelayedAutoAttackWeaponHandKeys);

        Assert.Equal(WeaponHandKeys.MainHand, delayedHand);

        var cost =
            Assert.Single(slam.ResourceCosts);

        Assert.Equal(ForeverSlamFactory.RageResourceKey, cost.ResourceKey);
        Assert.Equal(15m, cost.Amount);
        Assert.False(cost.IsPercentOfMaximum);

        var effect =
            Assert.Single(slam.Effects);

        Assert.Equal(WeaponHandKeys.MainHand, effect.WeaponHandKey);
        Assert.Equal(132m, effect.MinimumValue);
        Assert.Equal(152m, effect.MaximumValue);
        Assert.Equal(ForeverCombatStatKeys.AttackPower, effect.ScalingStatKey);
        Assert.Equal(0.2m, effect.ScalingCoefficient);
        Assert.Equal(
            ForeverCombatResolutionTypes.PlayerMeleeSpecial,
            effect.ResolutionType
        );
        Assert.Equal("sword-skill", effect.AttackSkillStatKey);
        Assert.True(effect.CanMiss);
        Assert.True(effect.CanBeDodged);
        Assert.True(effect.CanBeParried);
        Assert.True(effect.CanBeBlocked);
        Assert.True(effect.CanCrit);
        Assert.False(effect.CanGlance);
        Assert.False(effect.CanCrush);
        Assert.Equal(DamageMitigationTypes.Armor, effect.MitigationType);
    }

    [Fact]
    public void UnsupportedFactoryInputs_FailExplicitly()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverSlamFactory.CreateForLevel(
                19,
                100m,
                100m,
                2.8m,
                "sword-skill"
            )
        );

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverSlamFactory.CreateForLevel(
                61,
                100m,
                100m,
                2.8m,
                "sword-skill"
            )
        );

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverSlamFactory.CreateForLevel(
                30,
                100m,
                100m,
                0m,
                "sword-skill"
            )
        );

        Assert.Throws<ArgumentException>(() =>
            ForeverSlamFactory.CreateForLevel(
                30,
                100m,
                100m,
                2.8m,
                ""
            )
        );
    }
}
}

// ==================== ForeverWhirlwindTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Abilities.Warrior;
using WarcraftSim.Data.Forever.Characters;
using WarcraftSim.Data.Forever.Combat;

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
}
