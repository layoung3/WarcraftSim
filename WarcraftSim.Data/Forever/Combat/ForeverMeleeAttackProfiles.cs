using WarcraftSim.Core.Abilities;

namespace WarcraftSim.Data.Forever.Combat;

public static class ForeverMeleeAttackProfiles
{
    public static ForeverMeleeAttackProfileDefinition PlayerAuto { get; } =
        new()
        {
            Key =
                "player-auto",

            ResolutionType =
                ForeverCombatResolutionTypes.PlayerMeleeAuto,

            UsesWeaponSkill =
                true,

            CanMiss =
                true,

            CanBeDodged =
                true,

            CanBeParried =
                true,

            CanGlance =
                true,

            CanBeBlocked =
                true,

            CanCrit =
                true,

            CanCrush =
                false,

            // Blizzard's current beta known-issues list says the melee
            // higher-level glancing damage penalty is still incorrect.
            GlancingDamageModelVerified =
                false,

            CrushingChanceModelVerified =
                true
        };

    public static ForeverMeleeAttackProfileDefinition PlayerDualWieldAuto { get; } =
        new()
        {
            Key =
                "player-dual-wield-auto",

            ResolutionType =
                ForeverCombatResolutionTypes.PlayerDualWieldMeleeAuto,

            UsesWeaponSkill =
                true,

            CanMiss =
                true,

            CanBeDodged =
                true,

            CanBeParried =
                true,

            CanGlance =
                true,

            CanBeBlocked =
                true,

            CanCrit =
                true,

            CanCrush =
                false,

            GlancingDamageModelVerified =
                false,

            CrushingChanceModelVerified =
                true
        };

    public static ForeverMeleeAttackProfileDefinition PlayerSpecial { get; } =
        new()
        {
            Key =
                "player-special",

            ResolutionType =
                ForeverCombatResolutionTypes.PlayerMeleeSpecial,

            UsesWeaponSkill =
                true,

            CanMiss =
                true,

            CanBeDodged =
                true,

            CanBeParried =
                true,

            CanGlance =
                false,

            CanBeBlocked =
                true,

            CanCrit =
                true,

            CanCrush =
                false,

            GlancingDamageModelVerified =
                true,

            CrushingChanceModelVerified =
                true
        };

    public static ForeverMeleeAttackProfileDefinition CreatureAuto { get; } =
        new()
        {
            Key =
                "creature-auto",

            ResolutionType =
                ForeverCombatResolutionTypes.CreatureMeleeAuto,

            UsesTargetDefenseSkill =
                true,

            CanMiss =
                true,

            CanBeDodged =
                true,

            CanBeParried =
                true,

            CanGlance =
                false,

            CanBeBlocked =
                true,

            CanCrit =
                true,

            CanCrush =
                true,

            GlancingDamageModelVerified =
                true,

            CrushingChanceModelVerified =
                false
        };

    public static void Apply(
        AbilityEffectDefinition effect,
        ForeverMeleeAttackProfileDefinition profile,
        string? attackSkillStatKey = null)
    {
        ArgumentNullException.ThrowIfNull(
            effect
        );

        ArgumentNullException.ThrowIfNull(
            profile
        );

        if (
            profile.UsesWeaponSkill &&
            string.IsNullOrWhiteSpace(
                attackSkillStatKey))
        {
            throw new ArgumentException(
                $"Forever melee profile '{profile.Key}' requires an attack skill stat key.",
                nameof(attackSkillStatKey)
            );
        }

        effect.ResolutionType =
            profile.ResolutionType;

        effect.AttackSkillStatKey =
            profile.UsesWeaponSkill
                ? attackSkillStatKey
                : null;

        effect.TargetDefenseSkillStatKey =
            profile.UsesTargetDefenseSkill
                ? ForeverCombatStatKeys.DefenseSkill
                : null;

        effect.CanMiss =
            profile.CanMiss;

        effect.CanBeDodged =
            profile.CanBeDodged;

        effect.CanBeParried =
            profile.CanBeParried;

        effect.CanGlance =
            profile.CanGlance;

        effect.CanBeBlocked =
            profile.CanBeBlocked;

        effect.CanCrit =
            profile.CanCrit;

        effect.CanCrush =
            profile.CanCrush;
    }
}
