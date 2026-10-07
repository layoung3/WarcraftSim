using WarcraftSim.Core.Abilities;

namespace WarcraftSim.Data.Forever.Combat;

public static class ForeverSpellAttackProfiles
{
    public static ForeverSpellAttackProfileDefinition PlayerDirectSpell { get; } =
        new()
        {
            Key =
                "player-direct-spell",

            ResolutionType =
                ForeverCombatResolutionTypes.PlayerSpell,

            CanMiss =
                true,

            CanCrit =
                true,

            LevelMissProgressionVerified =
                false,

            CriticalMultiplierVerified =
                false
        };

    public static void Apply(
        AbilityEffectDefinition effect,
        ForeverSpellAttackProfileDefinition profile)
    {
        ArgumentNullException.ThrowIfNull(
            effect
        );

        ArgumentNullException.ThrowIfNull(
            profile
        );

        effect.ResolutionType =
            profile.ResolutionType;

        effect.AttackSkillStatKey =
            null;

        effect.TargetDefenseSkillStatKey =
            null;

        effect.CanMiss =
            profile.CanMiss;

        effect.CanBeDodged =
            false;

        effect.CanBeParried =
            false;

        effect.CanGlance =
            false;

        effect.CanBeBlocked =
            false;

        effect.CanCrit =
            profile.CanCrit;

        effect.CanCrush =
            false;
    }
}
