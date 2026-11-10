namespace WarcraftSim.Api.Simulation;

/// <summary>
/// CP175 Stage A. These are capabilities confirmed in existing running adapters,
/// not promises that an adapter is already compatible with the unified UI.
/// The frontend must use these flags to avoid rendering non-executable controls.
/// </summary>
public sealed record ForeverClassSimulationCapability(
    string ClassKey,
    string[] Specializations,
    string[] Resources,
    bool ExecutesThroughCurrentUnifiedEndpoint,
    bool HasClassTalentValidation,
    bool ExecutesArbitraryCrossTreeModeledTalents,
    bool HasSharedCombatItemProcessor,
    bool HasRotationPreview,
    bool HasExecutableCustomRotation,
    bool HasSeededBatchResults,
    string[] EncounterControls,
    string[] KnownRestrictions);

public static class ForeverClassSimulationCapabilities
{
    private static readonly ForeverClassSimulationCapability[] Catalog =
    [
        new("mage",["arcane","fire","frost"],["mana"],true,true,false,true,true,true,false,
            ["target","duration","seed","racial-active","spell-cooldown-policy","mana-thresholds"],
            ["Off-tree learned cooldowns run where connected; arbitrary cross-tree executable talent parity is unverified.",
             "Major Mana Potion is scheduled by the shared combat item processor; legacy explicit Mage fields are adapted."]),
        new("warlock",["affliction","demonology","destruction"],["mana","health"],true,true,false,true,true,true,false,
            ["target","duration","seed","racial-active","pet-type","life-tap-policy"],
            ["Life Tap spends real health; external healing is not implicit.",
             "Temporary demon and sacrifice conditions are not yet in a shared encounter schema."]),
        new("hunter",["marksmanship","survival","beast-mastery"],["mana","focus-pet"],true,true,false,true,true,true,false,
            ["target","duration","seed","racial-active","pet-family","pet-enabled","pet-ability-priority","aspect"],
            ["Hunter rotation preview delegates to actual typed runner priority and learned-action validation.",
             "Deep cross-tree passives and encounter utility remain partly modeled; arbitrary legal mixed-tree parity is not yet certified."]),
        new("shaman",["elemental"],["mana","health"],true,true,false,true,true,true,false,
            ["target-health","duration","seed","rotation-policy"],
            ["Elemental only: historical provisional 51-rank allocation, not current-client certification.",
             "Fixed level-63 unmitigated target; static self modifiers only. Catalog equipment eligibility remains pending evidence.",
             "Generic encounter scripts, target debuffs, racial actives, conditional priorities and batch iterations are unavailable.",
             "Health is a manual scenario cap plus provisional baseline deltas unless observed. Legacy and shared potion inventories start empty."]),
        new("warrior",["arms","fury","protection"],["rage","health"],true,true,false,true,true,true,true,
            ["target","duration","seed","stance","starting-rage","boss-attacks","tank-mode","stance-dance","major-cooldowns"],
            ["Warrior uses the shared preparation adapter and the existing seeded Warrior engine; it is not a Mage/Hunter typed runner.",
             "Warrior uses a separate support-effect catalog and gear bridge.",
             "Warrior solo tank nonlethal analysis differs from integrated tank+healer actual death semantics."])
    ];

    public static IReadOnlyList<ForeverClassSimulationCapability> All => Catalog;
    public static ForeverClassSimulationCapability Get(string? classKey) =>
        Catalog.FirstOrDefault(x=>string.Equals(x.ClassKey,classKey,
            StringComparison.OrdinalIgnoreCase)) ??
        throw new ArgumentException($"Unsupported shared level-60 class: '{classKey}'.");
}
