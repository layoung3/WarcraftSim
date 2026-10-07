using WarcraftSim.Core.Rulesets;

namespace WarcraftSim.Data.Forever.Combat;

public static class ForeverPhysicalCombatRulesetFactory
{
    public const decimal EqualLevelSingleWeaponBaseHitChancePercent =
        95m;

    public const decimal EqualLevelDualWieldBaseHitChancePercent =
        76m;

    public const decimal HigherTargetLevelHitPenaltyPercent =
        0.8m;

    public const decimal CreatureBaseCriticalChancePercent =
        5m;

    public const decimal PhysicalCriticalMultiplier =
        2m;

    public const decimal CreatureCrushingDamageMultiplier =
        1.5m;

    public static CombatRulesetDefinition Create()
    {
        var ruleset =
            new CombatRulesetDefinition
            {
                RulesetKey =
                    "wow-forever-physical",

                Version =
                    "1.60.1-beta"
            };

        ruleset.RollRules[
            ForeverCombatResolutionTypes.PlayerMeleeAuto
        ] =
            CreatePlayerRule(
                ForeverCombatResolutionTypes.PlayerMeleeAuto,
                EqualLevelSingleWeaponBaseHitChancePercent
            );

        ruleset.RollRules[
            ForeverCombatResolutionTypes.PlayerDualWieldMeleeAuto
        ] =
            CreatePlayerRule(
                ForeverCombatResolutionTypes.PlayerDualWieldMeleeAuto,
                EqualLevelDualWieldBaseHitChancePercent
            );

        ruleset.RollRules[
            ForeverCombatResolutionTypes.PlayerMeleeSpecial
        ] =
            CreatePlayerRule(
                ForeverCombatResolutionTypes.PlayerMeleeSpecial,
                EqualLevelSingleWeaponBaseHitChancePercent
            );

        ruleset.RollRules[
            ForeverCombatResolutionTypes.CreatureMeleeAuto
        ] =
            new CombatRollRuleDefinition
            {
                ResolutionType =
                    ForeverCombatResolutionTypes.CreatureMeleeAuto,

                UseSingleRollTable =
                    true,

                BaseHitChancePercent =
                    EqualLevelSingleWeaponBaseHitChancePercent,

                TargetDodgeChanceStatKey =
                    ForeverCombatStatKeys.DodgeChancePercent,

                TargetParryChanceStatKey =
                    ForeverCombatStatKeys.ParryChancePercent,

                TargetBlockChanceStatKey =
                    ForeverCombatStatKeys.BlockChancePercent,

                TargetBlockValueStatKey =
                    ForeverCombatStatKeys.BlockValue,

                BaseCriticalChancePercent =
                    CreatureBaseCriticalChancePercent,

                CriticalMultiplier =
                    PhysicalCriticalMultiplier,

                // The current Forever character sheet confirms that
                // +3-level creatures can crush for 150% damage, but the
                // current beta sources do not expose a verified chance.
                BaseCrushingChancePercent =
                    null,

                CrushingDamageMultiplier =
                    CreatureCrushingDamageMultiplier
            };

        return ruleset;
    }

    private static CombatRollRuleDefinition CreatePlayerRule(
        string resolutionType,
        decimal baseHitChancePercent)
    {
        return new CombatRollRuleDefinition
        {
            ResolutionType =
                resolutionType,

            UseSingleRollTable =
                true,

            BaseHitChancePercent =
                baseHitChancePercent,

            HitChanceStatKey =
                ForeverCombatStatKeys.HitChancePercent,

            HitPenaltyPerHigherTargetLevelPercent =
                HigherTargetLevelHitPenaltyPercent,

            TargetDodgeChanceStatKey =
                ForeverCombatStatKeys.DodgeChancePercent,

            SourceDodgeReductionStatKey =
                ForeverCombatStatKeys.DodgeReductionPercent,

            TargetParryChanceStatKey =
                ForeverCombatStatKeys.ParryChancePercent,

            SourceParryReductionStatKey =
                ForeverCombatStatKeys.ParryReductionPercent,

            TargetBlockChanceStatKey =
                ForeverCombatStatKeys.BlockChancePercent,

            SourceBlockReductionStatKey =
                ForeverCombatStatKeys.BlockReductionPercent,

            TargetBlockValueStatKey =
                ForeverCombatStatKeys.BlockValue,

            CriticalChanceStatKey =
                ForeverCombatStatKeys.AttackCriticalChancePercent,

            CriticalMultiplier =
                PhysicalCriticalMultiplier

            // Intentionally no glancing damage multiplier here. The current
            // Forever beta known-issues list says the melee penalty against
            // higher-level targets is still incorrect. A glancing roll will
            // fail loudly until a verified value is supplied.
        };
    }
}
