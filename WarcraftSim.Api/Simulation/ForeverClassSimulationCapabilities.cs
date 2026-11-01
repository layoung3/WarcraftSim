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
             "Major Mana Potion currently uses a Mage-specific prepared-inventory field."]),
        new("warlock",["affliction","demonology","destruction"],["mana","health"],true,true,false,true,true,true,false,
            ["target","duration","seed","racial-active","pet-type","life-tap-policy"],
            ["Life Tap spends real health; external healing is not implicit.",
             "Temporary demon and sacrifice conditions are not yet in a shared encounter schema."]),
        new("hunter",["marksmanship","survival","beast-mastery"],["mana","focus-pet"],true,true,false,true,false,true,false,
            ["target","duration","seed","racial-active","pet-family","pet-enabled","pet-ability-priority","aspect"],
            ["Rotation preview is per-runner, not yet unified.",
             "Cross-tree modeled abilities may be blocked by typed specialization runners."]),
        new("warrior",["arms","fury","protection"],["rage","health"],false,true,true,true,false,true,true,
            ["target","duration","seed","stance","starting-rage","boss-attacks","tank-mode","stance-dance","major-cooldowns"],
            ["Current Warrior runs through /api/sim/warrior, not the unified level-60 controller.",
             "Warrior uses a separate support-effect catalog and gear bridge.",
             "Warrior solo tank nonlethal analysis differs from integrated tank+healer actual death semantics."])
    ];

    public static IReadOnlyList<ForeverClassSimulationCapability> All => Catalog;
    public static ForeverClassSimulationCapability Get(string? classKey) =>
        Catalog.FirstOrDefault(x=>string.Equals(x.ClassKey,classKey,
            StringComparison.OrdinalIgnoreCase)) ??
        throw new ArgumentException($"Unsupported shared level-60 class: '{classKey}'.");
}
