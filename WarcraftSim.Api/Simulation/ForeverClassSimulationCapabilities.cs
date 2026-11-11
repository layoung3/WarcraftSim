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
        new("rogue",["assassination"],["energy","combo-points","health"],true,true,false,false,true,false,false,
            ["target-health","target-armor","target-avoidance","player-maximum-health","starting-energy","duration","seed",
             "dagger-weapon-budgets","weapon-poisons","stealth","positioning","expose-armor"],
            ["Assassination only: partial historical provisional 38/0/0 allocation with 13 unspent; twelve source-backed talent keys, not optimized, a complete build or current-client certification. Combat and Subtlety are unavailable.",
             "Read-only fixed configured priority from the actual BuildRotation path. Combo-point and aura gates apply during combat; auto attacks and poison procs execute separately. No custom priority.",
             "One level-63 target starts at full Health. Target Health and Armor are separate from player baseline Health. Maximum Energy includes Vigor once; starting Energy uses its separate percentage.",
             "Manual weapon damage, speed, hit, crit and ability coefficients remain provisional. Historical metadata and partial mechanics do not certify complete tooltip behavior or utility/PvP dispel effects.",
             "Observed naked Health takes precedence over the manual scenario cap; post-baseline Stamina and flat Health apply once. Without observed Health the cap plus those deltas is provisional.",
             "Catalog equipment and enchants, external modifiers, racial actives, companions, custom priority, scripts, extra targets and batch iterations are unavailable."]),
        new("priest",["shadow"],["mana","health"],true,true,false,false,true,false,false,
            ["target-health","target-initial-health","priest-maximum-health","duration","seed"],
            ["Shadow only: historical provisional starting preset 0/0/36 with 15 unspent; not a complete build or current-client certification.",
             "Fixed toggle-owned rotation only. Discipline and Penance are unavailable; spell coefficients, level-63 hit and Priest-specific regeneration remain provisional.",
             "Target initial health percent and manual Priest maximum health are distinct assumptions. Damage is unmitigated.",
             "External modifiers, combat items, catalog gear, racial actives, custom priorities, scripts, extra targets and batch iterations are unavailable."]),
        new("druid",["balance"],["mana","health"],true,true,false,true,true,true,false,
            ["target-health","duration","seed","rotation-policy"],
            ["Balance only: partial historical provisional starting preset: 35/0/0 with 16 unspent, not a complete build or current-client certification.",
             "Omen uses a 6% placeholder; Eclipse, spell coefficients and periodic behavior remain provisional. Initial rotation preview is reordered dynamically by combat state.",
             "Fixed level-63 unmitigated target. Catalog equipment eligibility is pending; only static self modifiers are supported.",
             "Racial combat effects, target debuffs, generic scripts, conditional priorities and batch iterations are unavailable."]),
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

    // Keep the class-only Shaman contract Elemental-compatible. Callers that
    // selected Enhancement must request its specialization-specific boundary.
    private static readonly ForeverClassSimulationCapability Enhancement = new(
        "shaman",["enhancement"],["mana","health"],true,true,false,true,true,false,false,
        ["target-health","target-armor","player-maximum-health","duration","seed",
         "main-hand-weapon","dual-wield","off-hand-weapon","hand-imbues","ability-toggles"],
        ["Enhancement only: partial historical provisional 0/36/0 allocation with 15 unspent; twelve source-backed talent keys, not a complete build or current-client certification.",
         "Read-only initial fixed toggle-driven priority from the actual BuildRotation path; auto attacks, Windfury and combat items execute separately. No custom priority or rotation policy.",
         "One level-63 target starts at full Health. Physical attacks use target Armor; Nature spells remain unmitigated. Weapon damage/speed, hit/crit and imbue assumptions remain manual and provisional; off-hand Windfury is unavailable.",
         "Observed naked Health takes precedence over the manual scenario cap; post-baseline Stamina and flat Health apply once. Without observed Health, the cap plus those deltas is a provisional assumption.",
         "Selected external modifiers, catalog equipment, racial actives, conditional priorities, encounter scripts, extra targets and batch iterations are unavailable."]);

    private static readonly ForeverClassSimulationCapability Feral = new(
        "druid",["feral"],["mana","energy","combo-points","health"],true,true,false,false,false,false,false,
        ["target-health","target-armor","duration","seed","manual-cat-ap","manual-weapon-hit-crit",
         "prowl","positioning","faerie-fire","feral-cooldowns"],
        ["Backend-only fixed Cat: fourteen historical source-backed keys admit a partial 0/31/5 allocation with 15 unspent; legal underallocations are allowed. No default preset or frontend admission.",
         "Historical embedded primary/resource baseline is required with explicit provisional opt-in, plus separate explicit manual total Cat AP before HOTW/Predatory. No generic Druid AP derivation or arbitrary inline observations.",
         "The simulation receipt includes the same Feral preparation, configured priority and actual input timing. Shared HTTP adds zero input latency; legacy direct HTTP defaults to 80 ms and needs explicit alignment for replay. No separate rotation-preview endpoint, latency override or custom priority is admitted here.",
         "One full-health level-63 target. No form/target transitions, seeded initial combo maps, adds, incoming attacks, tank effects, Bear utility or Thorns.",
         "No independent primary/resource overrides, catalog equipment/enchants, modifiers, racial effects, combat items, companions or batches. Tauren Endurance is omitted from scenario Health.",
         "Inherited Omen executes a provisional 6% chance and 10-second lockout without a selectable Omen rank. Baseline-versus-talent eligibility remains unresolved; absence from the retained tree does not settle it. Natural Instinct white/periodic critical eligibility remains provisional, with no healing model.",
         "Manual weapon/hit/crit/shift/base-Mana assumptions and existing coefficients remain provisional. Furor is rank zero or five only; Furor and Feral Swiftness are inactive in this scope. Full AP consumers, mixed-form correctness and complete talent effects are not certified."]);

    public static IReadOnlyList<ForeverClassSimulationCapability> SpecializationOverrides => [Enhancement,Feral];
    public static IReadOnlyList<ForeverClassSimulationCapability> All => Catalog;
    public static ForeverClassSimulationCapability Get(string? classKey, string? specialization=null)
    {
        var capability=Catalog.FirstOrDefault(x=>string.Equals(x.ClassKey,classKey,
            StringComparison.OrdinalIgnoreCase)) ??
            throw new ArgumentException($"Unsupported shared level-60 class: '{classKey}'.");
        if(string.IsNullOrWhiteSpace(specialization))return capability;
        if(string.Equals(classKey,"shaman",StringComparison.OrdinalIgnoreCase) &&
           string.Equals(specialization,"enhancement",StringComparison.OrdinalIgnoreCase))return Enhancement;
        if(string.Equals(classKey,"druid",StringComparison.OrdinalIgnoreCase) &&
           string.Equals(specialization,"feral",StringComparison.OrdinalIgnoreCase))return Feral;
        if(!capability.Specializations.Contains(specialization,StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"Unsupported {classKey} starting specialization '{specialization}'.");
        return capability;
    }
}
