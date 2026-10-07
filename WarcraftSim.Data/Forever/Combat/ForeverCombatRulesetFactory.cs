using WarcraftSim.Core.Rulesets;

namespace WarcraftSim.Data.Forever.Combat;

/// <summary>
/// Combined Forever combat ruleset used when a simulation needs both the
/// physical attack table and spell resolution rules.
/// </summary>
public static class ForeverCombatRulesetFactory
{
    public static CombatRulesetDefinition Create()
    {
        var ruleset =
            ForeverPhysicalCombatRulesetFactory.Create();

        ruleset.RulesetKey =
            "wow-forever";

        ruleset.RollRules[
            ForeverCombatResolutionTypes.PlayerSpell
        ] =
            ForeverSpellCombatRulesetFactory
                .CreatePlayerSpellRule();

        return ruleset;
    }
}
