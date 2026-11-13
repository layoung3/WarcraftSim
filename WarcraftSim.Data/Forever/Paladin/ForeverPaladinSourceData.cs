namespace WarcraftSim.Data.Forever.Paladin;

/// <summary>
/// Values consumed by the level-60 Paladin adapter. These are source facts or
/// explicitly identified provisional models, not a declaration of live-client
/// certification. Each group is reconciled in CP175-paladin-source-ledger.md.
/// </summary>
public static class ForeverPaladinSourceData
{
    public const string LedgerPath = "docs/audits/CP175-paladin-source-ledger.md";
    public const string CurrentModelCommit = "8c7b5a5f5ae85b6222c16d1780c342992cd74f71";
    public const string CurrentModelUrl = "https://github.com/wowsims/forever/blob/" + CurrentModelCommit;
    public const string TalentSpellSourceUrl = CurrentModelUrl + "/ui/sim/talents/trees/paladin.json";
    public const string ClientEffectSourceUrl = CurrentModelUrl + "/sim/core/spelldata/spells_auto_gen.go";
    public const string ProcMeasurementSourceUrl = CurrentModelUrl + "/tools/database/overrides/spell_overrides.go";
    public const string SpellDataSha256 = "aae81a86f6902adf6f287720140f6becbea6ef936a9ddbd035c1de9fad5afceb";

    // Official dated changes, independently corroborated by current data.
    public const decimal ConsecrationFirstFourSpellPowerPerTick = .042m;
    public const decimal RetributionAuraSpellPower = .06m;
    public const bool JudgementUsesRangedHit = true;
    public const bool JudgementTriggersMeleeOnlyProcs = false;
    public const bool WindfuryGeneratesUnsealedEchoes = false;

    // Current client-derived spell/effect rows. Rounded decimal coefficients
    // preserve the intended values of the extracted float32 representations.
    public const decimal DirectHolySpellPower = .429m;
    public const decimal HolyLightSpellPower = .714m;
    public const decimal HolyShieldSpellPower = .08m;
    public const decimal LightsVigilSpellPower = .143m;
    public const decimal HolyWrathSpellPower = .19m;
    public const decimal HolyShockDamageRangeYards = 20m;
    public const decimal HolyShockHealingRangeYards = 40m;
    public const decimal HolyWrathRadiusYards = 20m;
    public const decimal HolyWrathStunSeconds = 2m;
    public const int BlessingOfSacrificeSpellId = 20729;
    public const decimal BlessingOfSacrificeTransferDamage = 55m;
    public const decimal BlessingOfSacrificeManaCost = 100m;
    public const decimal BlessingOfSacrificeDurationSeconds = 30m;
    public const decimal FuryProcSpellPower = .10m;
    public const decimal FlashOfLightMinimumAt60 = 291.2m;
    public const decimal FlashOfLightMaximumAt60 = 325.2m;
    public const decimal JudgementOfRighteousnessMinimumAt60 = 170.2m;
    public const decimal JudgementOfRighteousnessMaximumAt60 = 186.2m;
    public const decimal JudgementOfFuryMinimumAt60 = 153.38m;
    public const decimal JudgementOfFuryMaximumAt60 = 167.38m;
    public const decimal OathHolyLightAt60 = 400m;
    public const decimal OathFlashOfLightAt60 = 115m;
    // The client float32 coefficient is 3.833329916; its non-damage aura
    // amount floors to 229 at 60. The mirror's rounded 230 is not consumed.
    public const decimal OathHolyShockAt60 = 229m;
    public const decimal SealCrusaderAttackPowerAt60 = 325m;
    public const decimal SealCrusaderHasteFraction = .40m;
    public const decimal FuryAbsorbFraction = .50m;
    public const decimal UnsealedEchoLifetimeSeconds = 15m;
    public const int UnsealedMaximumStacks = 3;
    public const int ReckoningMaximumAttacks = 4;
    public const decimal ReckoningBlockCooldownSeconds = 1.5m;
    public const decimal VindicationAttackPowerPerRankAt60 = 68m;

    // Original Forever measurement analysis, carried to higher unmeasured ranks.
    // This is extrapolation from measured ranks, not remembered Classic rates.
    public const decimal LightWisdomSealPpm = 15m;
    public const decimal LightWisdomJudgementChance = .50m;
    public const decimal SealCommandPpm = 7m;
    public const decimal SealCommandCooldownSeconds = 1m;
    public const decimal VindicationPpm = 5m;
    public const decimal ResistanceLevelFactor = 9m;

    // Source-backed simulation choices where current server behavior is not
    // independently established. The runner must disclose their provisional use.
    public const decimal FuryAbsorbSeconds = 30m;
    public const decimal SealCrusaderAutoDamageMultiplier = 1m / 1.4m;
    public const decimal RighteousnessOneHandSpellPower = .10m;
    public const decimal RighteousnessTwoHandSpellPower = .12m;
    public const decimal DirectHealingThreatFraction = .25m;
    public const decimal VigilHealingThreatFraction = .50m;

    // Spell ID of the talent definition (one ID with a rank curve). This is
    // deliberately distinct from the Talent ID and from trained ability ranks.
    public static IReadOnlyDictionary<string, int> TalentSpellIds { get; } =
        new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["spiritual-focus"] = 20205,
        ["divine-strength"] = 20262,
        ["healing-light"] = 20237,
        ["reverence"] = 1310899,
        ["improved-seals"] = 20224,
        ["purifying-power"] = 429144,
        ["divine-favor"] = 20216,
        ["illumination"] = 20210,
        ["holy-conduit"] = 1237268,
        ["divine-precision"] = 1310904,
        ["infusion-of-light"] = 426065,
        ["oath-of-light"] = 1328548,
        ["consecrated-ground"] = 1310905,
        ["holy-shock"] = 1311606,
        ["boundless-light"] = 1328550,
        ["holy-power"] = 5923,
        ["lights-vigil"] = 1310911,
        ["divine-intellect"] = 20257,
        ["redoubt"] = 20127,
        ["precision"] = 20189,
        ["guardians-favor"] = 20174,
        ["sacred-duty"] = 1224697,
        ["templars-bulwark"] = 1311015,
        ["improved-hammer-of-justice"] = 20487,
        ["shield-specialization"] = 20150,
        ["toughness"] = 20143,
        ["swift-judgement"] = 1310994,
        ["improved-righteous-fury"] = 20468,
        ["anticipation"] = 20096,
        ["stoicism"] = 1328546,
        ["holy-shield"] = 20925,
        ["reckoning"] = 20177,
        ["iron-creed"] = 1311034,
        ["shieldsworn"] = 1328544,
        ["deflection"] = 20060,
        ["benediction"] = 20101,
        ["improved-judgement"] = 25956,
        ["conviction"] = 20117,
        ["sanctified-judgement"] = 1311074,
        ["vindication"] = 9452,
        ["unyielding-faith"] = 9453,
        ["seal-of-command"] = 20375,
        ["pursuit-of-justice"] = 26022,
        ["eye-for-an-eye"] = 9799,
        ["sacred-arbiter"] = 1311087,
        ["two-handed-weapon-specialization"] = 20111,
        ["turn-evil"] = 1328370,
        ["repentance"] = 20066,
        ["vengeance"] = 20049,
        ["champion-of-the-light"] = 1311084,
        ["unsealed-wrath"] = 1310735,
    };

    // IDs from the current per-seal aura rows. Crusader has no stored-hit aura.
    public static IReadOnlyDictionary<string, int> EchoSpellIds { get; } =
        new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["command"] = 1311703, ["fury"] = 1311701,
        ["righteousness"] = 1311704, ["justice"] = 1311705,
        ["light"] = 1328198, ["wisdom"] = 1328206
    };
}
