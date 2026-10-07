using WarcraftSim.Core.Rulesets;

namespace WarcraftSim.Data.Forever.Combat;

/// <summary>
/// Spell hit/critical roll foundation for WoW: Forever.
///
/// Forever's current stat presentation exposes a 4% equal-level spell miss
/// requirement and 17% against a raid boss. The intermediate level
/// progression and 150% spell critical multiplier remain Classic fallbacks
/// until they are directly verified in the beta.
/// </summary>
public static class ForeverSpellCombatRulesetFactory
{
    public const decimal EqualLevelBaseHitChancePercent =
        96m;

    public const decimal SpellCriticalMultiplier =
        1.5m;

    public static CombatRulesetDefinition Create()
    {
        var ruleset =
            new CombatRulesetDefinition
            {
                RulesetKey =
                    "wow-forever-spell",

                Version =
                    "1.60.1-beta"
            };

        ruleset.RollRules[
            ForeverCombatResolutionTypes.PlayerSpell
        ] =
            CreatePlayerSpellRule();

        return ruleset;
    }

    public static CombatRollRuleDefinition CreatePlayerSpellRule()
    {
        return new CombatRollRuleDefinition
        {
            ResolutionType =
                ForeverCombatResolutionTypes.PlayerSpell,

            // Spell miss and spell critical are staged rolls rather than the
            // ordered one-roll physical attack table.
            UseSingleRollTable =
                false,

            BaseHitChancePercent =
                EqualLevelBaseHitChancePercent,

            HitChanceStatKey =
                ForeverCombatStatKeys.SpellHitChancePercent,

            CriticalChanceStatKey =
                ForeverCombatStatKeys.SpellCriticalChancePercent,

            CriticalMultiplier =
                SpellCriticalMultiplier
        };
    }
}
