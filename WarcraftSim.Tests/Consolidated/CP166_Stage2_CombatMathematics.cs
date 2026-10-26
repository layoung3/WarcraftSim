using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp166Stage2CombatMathematicsTests
{
    [Fact]
    public void ClientRatingTablesAndLevelSixtyConversionsStayConsistent()
    {
        Assert.Equal(1m,ForeverPhysicalStatConversions.RatingToPercent(10m,
            ForeverPhysicalStatConversions.HitRatingPerPercent));
        Assert.Equal(1m,ForeverPhysicalStatConversions.RatingToPercent(14m,
            ForeverPhysicalStatConversions.CriticalStrikeRatingPerPercent));
        Assert.Equal(1m,ForeverPhysicalStatConversions.RatingToPercent(12m,
            ForeverPhysicalStatConversions.DodgeRatingPerPercent));
        Assert.Equal(1m,ForeverPhysicalStatConversions.RatingToPercent(15m,
            ForeverPhysicalStatConversions.ParryRatingPerPercent));
        Assert.Equal(1m,ForeverPhysicalStatConversions.RatingToPercent(5m,
            ForeverPhysicalStatConversions.BlockRatingPerPercent));
        Assert.Equal(1m,ForeverSpellStatConversions.RatingToPercent(10m,
            ForeverSpellStatConversions.HasteRatingPerPercent));
        Assert.Equal(1m,ForeverPhysicalStatConversions.StrengthToBlockValue(20m));
        Assert.Equal(20m,ForeverPrimaryPhysicalStatProfiles
            .GetAgilityPerCriticalPercent("warrior",60));
        Assert.Equal(60.6m,ForeverPrimarySpellStatProfiles
            .GetIntellectPerSpellCriticalPercent("warlock",60));
    }

    [Fact]
    public void PlayerWhiteDualWieldAndSpecialAttacksUseDifferentHitTables()
    {
        var rules=ForeverPhysicalCombatRulesetFactory.Create();
        Assert.Equal(95m,rules.RollRules[
            ForeverCombatResolutionTypes.PlayerMeleeAuto].BaseHitChancePercent);
        Assert.Equal(76m,rules.RollRules[
            ForeverCombatResolutionTypes.PlayerDualWieldMeleeAuto].BaseHitChancePercent);
        Assert.Equal(95m,rules.RollRules[
            ForeverCombatResolutionTypes.PlayerMeleeSpecial].BaseHitChancePercent);
        Assert.True(ForeverMeleeAttackProfiles.PlayerAuto.CanGlance);
        Assert.True(ForeverMeleeAttackProfiles.PlayerDualWieldAuto.CanGlance);
        Assert.False(ForeverMeleeAttackProfiles.PlayerSpecial.CanGlance);
        Assert.False(ForeverMeleeAttackProfiles.PlayerSpecial.CanCrush);
        Assert.True(ForeverMeleeAttackProfiles.CreatureAuto.CanCrush);
    }

    [Fact]
    public void WhiteSwingAttackPowerAndOffhandScalingDoNotChangeSpecialNormalization()
    {
        var mh=ForeverAutoAttackFactory.CreatePlayerMelee(
            "main","Main",20m,20m,2.4m,"sword-skill");
        var oh=ForeverAutoAttackFactory.CreatePlayerDualWieldOffHand(
            "off","Off",20m,20m,1.8m,"dagger-skill");
        Assert.Equal(2.4m/14m,mh.DamageEffect.ScalingCoefficient);
        Assert.Equal(1.8m/14m,oh.DamageEffect.ScalingCoefficient);
        Assert.Equal(.5m,oh.DamageMultiplier);
        Assert.Equal(ForeverNormalizedWeaponSpeeds.Dagger,1.7m);
        Assert.Equal(ForeverNormalizedWeaponSpeeds.OneHanded,2.4m);
        Assert.Equal(ForeverNormalizedWeaponSpeeds.TwoHanded,3.3m);
        Assert.Equal(ForeverNormalizedWeaponSpeeds.Ranged,2.8m);
        Assert.Equal(DamageMitigationTypes.Armor,oh.DamageEffect.MitigationType);
    }

    [Fact]
    public void IncomingArmorUsesAttackerLevelAndNeverExceedsSeventyFivePercent()
    {
        var ruleset=new CombatRulesetDefinition{RulesetKey="cp166-armor",Version="1",
            MitigationRules={
                [DamageMitigationTypes.Armor]=new DamageMitigationRuleDefinition{
                    MitigationType=DamageMitigationTypes.Armor,
                    FormulaType=DamageMitigationFormulaTypes.RationalLevelScaled,
                    DefenseStatKey=ForeverCombatStatKeys.Armor,
                    BaseConstant=400m,PerAttackerLevelConstant=85m,
                    MaximumReductionPercent=75m
                }
            }
        };
        var context=new SimulationContext(new SimulationRunOptions{
            DurationSeconds=1m,PrimaryActorKey="attacker",Seed=166020
        });
        var attacker=Actor("attacker",60),defender=Actor("defender",63);
        defender.Stats.Set(ForeverCombatStatKeys.Armor,4000m);
        var effect=new AbilityEffectDefinition{Key="damage",
            MitigationType=DamageMitigationTypes.Armor};
        var mitigator=new RulesetDamageMitigationResolver(ruleset);
        var result=mitigator.Resolve(context,attacker,defender,
            new AbilityDefinition{Key="test"},effect,100m);
        var expected=4000m/(4000m+400m+85m*60m);
        Assert.InRange(Math.Abs(result.ReductionPercent-100m*expected),
            0m,.000001m);
        Assert.InRange(Math.Abs(result.FinalAmount-100m*(1m-expected)),
            0m,.000001m);
        attacker.Level=63;
        var boss=mitigator.Resolve(context,attacker,defender,
            new AbilityDefinition{Key="test"},effect,100m);
        Assert.True(boss.ReductionPercent<result.ReductionPercent);
        defender.Stats.Set(ForeverCombatStatKeys.Armor,100000m);
        var capped=mitigator.Resolve(context,attacker,defender,
            new AbilityDefinition{Key="test"},effect,100m);
        Assert.Equal(75m,capped.ReductionPercent);
        Assert.Equal(25m,capped.FinalAmount);
        effect.MitigationType=DamageMitigationTypes.None;
        Assert.Equal(100m,mitigator.Resolve(context,attacker,defender,
            new AbilityDefinition{Key="test"},effect,100m).FinalAmount);
    }

    [Fact]
    public void OrderedTableMakesCriticalAndCrushingOutcomesMutuallyExclusive()
    {
        var a=OrderedCombatRollTable.Resolve(25m,[
            new CombatRollTableEntry{ChancePercent=30m,Result=CombatRollResult.Dodge()},
            new CombatRollTableEntry{ChancePercent=80m,Result=CombatRollResult.Critical(2m)},
            new CombatRollTableEntry{ChancePercent=60m,Result=CombatRollResult.Crushing(1.5m)}
        ]);
        Assert.Equal("dodge",a.ResultKey);
        Assert.False(a.IsCritical);
        Assert.False(a.IsCrushing);
        var b=OrderedCombatRollTable.Resolve(80m,[
            new CombatRollTableEntry{ChancePercent=30m,Result=CombatRollResult.Dodge()},
            new CombatRollTableEntry{ChancePercent=80m,Result=CombatRollResult.Critical(2m)},
            new CombatRollTableEntry{ChancePercent=60m,Result=CombatRollResult.Crushing(1.5m)}
        ]);
        Assert.True(b.IsCritical);
        Assert.False(b.IsCrushing);
    }

    [Fact]
    public void WeaponNormalizedRageDoesNotScaleWithDealtDamage()
    {
        var one=ForeverWarriorAutoAttackFactory.CreateOneHandedMainHand(
            "one","One",1m,1m,2.6m,"skill");
        var high=ForeverWarriorAutoAttackFactory.CreateOneHandedMainHand(
            "one","One",999m,999m,2.6m,"skill");
        var two=ForeverWarriorAutoAttackFactory.CreateTwoHandedMainHand(
            "two","Two",1m,1m,3.6m,"skill");
        var off=ForeverWarriorAutoAttackFactory.CreateDualWieldOffHand(
            "off","Off",1m,1m,1.8m,"skill");
        Assert.Equal(2.6m*3.46m,one.ResourceGenerations.Single().AmountPerLandedSwing);
        Assert.Equal(one.ResourceGenerations.Single().AmountPerLandedSwing,
            high.ResourceGenerations.Single().AmountPerLandedSwing);
        Assert.Equal(3.6m*4.5m,two.ResourceGenerations.Single().AmountPerLandedSwing);
        Assert.Equal(1.8m*1.73m,off.ResourceGenerations.Single().AmountPerLandedSwing);
    }

    [Theory]
    [InlineData(-1.0,0.0)]
    [InlineData(0.0,0.0)]
    [InlineData(0.421052631579,0.421052631579)]
    [InlineData(0.90,0.75)]
    [InlineData(1.00,0.75)]
    public void ArmorCeilingPreservesOrdinaryValuesButClampsHighArmor(
        double requested,double expected)
    {
        var reduced=ForeverArmorMitigationMath.ClampArmorReductionFraction(
            (decimal)requested);
        Assert.InRange(Math.Abs(reduced-(decimal)expected),0m,.00000001m);
    }

    [Fact]
    public void DefenseSkillContributesFourHundredthsPercentPerPoint()
    {
        Assert.Equal(.04m,ForeverDefenseCombatRollAdjustmentProvider
            .DefensePercentPerSkillPoint);
        Assert.Equal(5.6m,(440m-300m)*
            ForeverDefenseCombatRollAdjustmentProvider.DefensePercentPerSkillPoint);
    }

    private static SimulationActorState Actor(string key,int level)
    {
        var a=new SimulationActorState{Key=key,Name=key,Level=level,TeamKey="raid"};
        a.InitializeHealth(1000m);
        return a;
    }
}
