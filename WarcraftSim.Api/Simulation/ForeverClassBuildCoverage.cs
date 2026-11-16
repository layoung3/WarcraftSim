using WarcraftSim.Data.Forever.Rogue;
using WarcraftSim.Data.Forever.Shaman;

namespace WarcraftSim.Api.Simulation;

public sealed record ForeverTalentRuntimeCoverage(string Status,string[] Implemented,string[] Missing,string[] Conditions);

/// <summary>Coverage has an explicit row for each class and each tree. An absent
/// executable effect is never inferred from a legal node or an unrelated passing build.</summary>
public static class ForeverClassBuildCoverage
{
    public static ForeverTalentRuntimeCoverage ForTalent(string cls,string key)
    {
        if(cls=="shaman")
        {
            var coverage=ForeverShamanClassTalents.Entries.Single(x=>x.Key==key).Coverage;
            return new(coverage.Status,coverage.ImplementedEffects.ToArray(),coverage.MissingEffects.ToArray(),coverage.ApplicableScenarios.ToArray());
        }
        if(cls=="paladin")return PaladinSimRunner.TalentCoverage(key);
        if(cls=="rogue" && ForeverRogueClassTalents.CoverageStatuses.TryGetValue(key,out var status))
            return new(status,status=="unimplemented"?[]:[ForeverRogueClassTalents.CoverageDescriptions[key]],
                ForeverRogueClassTalents.CoverageLimitations.TryGetValue(key,out var missing)?[missing]:[],
                [ForeverRogueClassTalents.ScenarioConditions[key]]);
        return new("unreviewed",[],["Class-wide effect review is not recorded for this node."],[]);
    }

    public static object All() => ForeverClassBuildCatalog.Classes.Select(cls=>new {
        classKey=cls,trees=ForeverClassBuildCatalog.Trees(cls),
        treeCoverage=ForeverClassBuildCatalog.Trees(cls).Select(tree=>new {
            tree,engine=TreeEngine(cls,tree),sharedIntegration=TreeIntegration(cls,tree),
            sourceReview=cls=="paladin"?"51 current70888 nodes; November3/11 official overlays; independently pinned component values, see CP175-paladin-source ledger":
                cls is "shaman" or "rogue"?"current70888 topology reconciled; official updates applied; values remain provisional":
                "retained implementation and prior audits; current70888 all-node parity not established in this tranche",
            completionBacklog=TreeBacklog(cls,tree)}),
        classWideEditor=ForeverClassBuildCatalog.CanExecuteClassBuild(cls),
        allTreeCompletion=false,
        engine=cls switch {"paladin"=>"PaladinSimRunner: class-wide damage plus bounded healing/defensive scenarios",_=>"existing typed engines; coverage differs by scenario"},
        unifiedWorkflows=cls switch {
            "warrior"=>new[]{"arms","fury","protection"},"paladin"=>new[]{"shared-holy","shared-protection","shared-retribution"},
            "hunter"=>new[]{"beast-mastery","marksmanship","survival"},
            "rogue"=>new[]{"assassination","combat","subtlety"},"priest"=>new[]{"shadow"},
            "shaman"=>new[]{"elemental","enhancement","restoration-focused-effects"},"mage"=>new[]{"arcane","fire","frost"},
            "warlock"=>new[]{"affliction","demonology","destruction"},"druid"=>new[]{"balance","feral"},_=>[]},
        gearAndEnchants=cls switch {
            "mage" or "warlock" or "hunter"=>"existing reviewed catalog, static and supported dynamic effects",
            "warrior"=>"existing gear/support bridge and supported dynamic effects",
            "shaman"=>"published Augur belt/bracers and supported static enchants; other catalog restrictions and weapon proficiency pending",
            "rogue"=>"published leather wrists and supported static enchants; other catalog restrictions and weapon proficiency pending",
            "paladin"=>"Shared stat/equipment/enchant pipeline; narrowly reviewed weapon/shield witnesses; unknown item restrictions or proficiency remain pending",
            "druid"=>"prepared Balance runner supports published dragonhide wrists/enchants; strict UI and fixed-Cat catalog integration remain missing",
            "priest"=>"unified gear integration missing",_=>"engine and integration missing"},
        modifiers=cls switch {
            "mage" or "warlock" or "hunter" or "warrior"=>"existing supported self/target paths",
            "shaman" or "rogue" or "paladin"=>"shared static self modifiers and live Faerie Fire; other target debuffs remain gated",
            "druid"=>"Balance static self modifiers; Feral integration missing",_=>"unified integration missing"},
        combatItems=cls is "mage" or "warlock" or "hunter" or "warrior" or "shaman" or "rogue" or "paladin"?"shared processor":"partial or missing unified integration",
        rotations=cls is "shaman" or "rogue" or "paladin"?"class-wide talent/equipment-derived live priority":
            "existing typed policies; all-tree consumer/registration parity remains incomplete",
        batches=cls is "warrior" or "mage" or "warlock" or "hunter" or "shaman" or "rogue" or "paladin",
        missingCode=cls=="paladin"?new[]{"White-hit glancing outcome is not implemented; current Forever glancing damage range is pending evidence. Racial combat effects are unconnected."}:
            ForeverClassBuildCatalog.CanExecuteClassBuild(cls)?ForeverClassBuildCatalog.Talents(cls)
            .Where(x=>x.ImplementationStatus!="implemented").SelectMany(x=>x.MissingEffects.Select(m=>x.Name+": "+m)).ToArray():
            new[]{"Retained per-tree audits contain partial/unimplemented effects; reconcile current source before closing each confirmed code gap"},
        missingIntegration=cls switch {
            "priest"=>new[]{"Discipline/Holy cross-tree access in shared DPS; generic gear/modifiers"},
            "druid"=>new[]{"Complete Balance/Feral/Restoration cross-tree access; fixed-Cat AP consumers and shared inputs"},
            "paladin"=>new[]{"Racial combat effects; full raid healing, tank assignments and PvP workflows remain separate"},
            "shaman" or "rogue"=>new[]{"Other target debuffs and racial actives; full healer/tank workflows remain separate scope"},
            _=>new[]{"Current class-wide contract, action consumers and per-tree coverage reconciliation"}},
        missingEvidence=new[]{"Current client coefficients and all-node coverage remain distinct from software test results"},
        sourceConfidence="mixed reviewed official updates and historical/provisional records; see each talent and item"
    }).ToArray();

    private static string TreeEngine(string cls,string tree)=>cls switch {
        "paladin"=>"PaladinSimRunner; one class-wide spellbook and bounded friendly-healing/incoming-hit consumers",
        "shaman"=>"ShamanClassWideSimRunner; shared three-tree spellbook and focused defensive/healing/utility scenarios",
        "rogue"=>"RogueSimRunner class-wide mode; one spellbook, combo/poison/defense pipeline",
        "mage"=>"MageSimRunner", "warlock"=>"WarlockSimRunner",
        "hunter"=>tree switch {"beast-mastery"=>"BeastMasteryHunterSimRunner","survival"=>"SurvivalHunterSimRunner",_=>"MarksmanshipHunterSimRunner"},
        "warrior"=>"ForeverWarriorSimRunner with stance/weapon/protection processors",
        "druid"=>tree switch {"balance"=>"BalanceDruidSimRunner","feral"=>"FeralDruidSimRunner",_=>"RestorationDruidSimRunner (separate healing workflow)"},
        "priest"=>tree=="shadow"?"ShadowPriestSimRunner":"Existing Priest healing/cooldown integration; separate from the Shadow adapter",
        _=>"No executable class engine"};

    private static string TreeIntegration(string cls,string tree)=>cls switch {
        "shaman" or "rogue" or "paladin"=>"Current class-wide editor, shared preparation, live priority, reports/replay and bounded comparisons",
        "mage" or "warlock" or "hunter" or "warrior"=>"Existing unified typed workflow; cross-tree contracts retain prior restrictions",
        "druid" when tree=="restoration"=>"Missing shared DPS allocation and cross-form/healing-tree integration",
        "druid"=>"Restricted existing unified workflow; all-tree action/stat integration incomplete",
        "priest" when tree=="shadow"=>"Restricted existing unified Shadow workflow",
        "priest"=>"Missing shared DPS allocation and off-tree active-action integration",
        _=>"Missing shared execution adapter; follows Shaman/Rogue contract"};

    private static string[] TreeBacklog(string cls,string tree)=>cls switch {
        "shaman"=>["Expand applicable combinations/encounters beyond bounded witnesses; retain source uncertainty separately from implemented mechanics"],
        "rogue" when tree=="subtlety"=>["Stealth detection, stealth movement/re-entry cooldown and Quick Getaway movement"],
        "rogue"=>["Extend interaction evidence across legal weapon/poison/rank combinations; keep current coefficient uncertainty explicit"],
        "paladin"=>["Extend interaction evidence beyond bounded rank/weapon/target witnesses; retain per-component source uncertainty, racial omissions and full raid/PvP scenario boundaries"],
        "druid" when tree=="restoration"=>["Connect Restoration stat/resource/healing/defensive effects to legal shared Balance/Feral allocations"],
        "druid" when tree=="feral"=>["Complete Cat AP consumers and shared equipment; reconcile Bear/form-transition prerequisites and off-tree effects"],
        "druid"=>["Move beyond restricted Balance allocations; connect all applicable Restoration/Feral effects and shared UI inputs"],
        "priest" when tree=="shadow"=>["Extend strict shared allocation beyond retained Shadow keys and connect gear/modifiers/items"],
        "priest"=>[$"Reuse existing {tree} healing/defensive code and connect applicable cross-tree costs, cooldowns and actions to shared DPS"],
        _=>[$"Reconcile every current {tree} node with the retained effect audit; distinguish missing mechanics, adapter gaps and missing scenario evidence"]};
}
