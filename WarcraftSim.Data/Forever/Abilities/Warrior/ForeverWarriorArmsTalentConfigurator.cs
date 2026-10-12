using WarcraftSim.Core.Abilities;
using WarcraftSim.Data.Forever.Characters;

namespace WarcraftSim.Data.Forever.Abilities.Warrior;

/// <summary>
/// Apply Forever Warrior Impale to known critical direct-damage abilities.
/// Call this when assembling the warrior spellbook based on selected talents.
/// It deliberately does not alter auto-attacks or periodic bleed effects.
/// </summary>
public static class ForeverWarriorArmsTalentConfigurator
{
    public static void ApplyImpale(AbilityDefinition ability, int impaleRank, int deepWoundsRank = 0)
    {
        ArgumentNullException.ThrowIfNull(ability);
        if (impaleRank is < 0 or > 2)
            throw new ArgumentOutOfRangeException(nameof(impaleRank));
        if (deepWoundsRank is < 0 or > 3)
            throw new ArgumentOutOfRangeException(nameof(deepWoundsRank));
        if (impaleRank > 0 && deepWoundsRank < 3)
            throw new InvalidOperationException("Forever Impale requires Deep Wounds rank 3.");
        if (!string.Equals(ability.ClassKey, ForeverCharacterClassKeys.Warrior,
                StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Impale requires a Warrior ability.", nameof(ability));

        foreach (var effect in ability.Effects.Where(effect =>
            effect.EffectType == AbilityEffectTypes.DirectDamage && effect.CanCrit))
        {
            effect.BonusCriticalDamagePercent = impaleRank * 10m;
            effect.CriticalDamageBonusRequiredSourceAuraKeys.Clear();
            if (impaleRank == 0) continue;
            effect.CriticalDamageBonusRequiredSourceAuraKeys.Add(ForeverWarriorAuraKeys.BattleStance);
            effect.CriticalDamageBonusRequiredSourceAuraKeys.Add(ForeverWarriorAuraKeys.DefensiveStance);
            effect.CriticalDamageBonusRequiredSourceAuraKeys.Add(ForeverWarriorAuraKeys.BerserkerStance);
        }
    }
}
