namespace WarcraftSim.Data.Forever.Shaman;

/// <summary>Explicit mechanics coverage, separate from allocation legality and
/// numeric confidence. A missing entry is an error, never implicit completion.
/// "Implemented" describes the listed bounded scenario, not a universal build.</summary>
public static class ForeverShamanClassCoverage
{
    public static IReadOnlyDictionary<string,ShamanTalentCoverage> Entries {get;} =
        new Dictionary<string,ShamanTalentCoverage>(StringComparer.OrdinalIgnoreCase)
    {
        ["convection"]=Implemented("Bolt, Chain Lightning and all three Shocks Mana reduction", "A listed damage spell is cast"),
        ["concussion"]=Implemented("Bolt, Chain Lightning and Earth Shock damage", "A listed spell hits"),
        ["elemental-devastation"]=Implemented("Owned direct spell criticals grant 3/6/9 melee crit for10s; refresh and expiry", "Direct damage critical followed by melee attacks"),
        ["reverberation"]=Implemented("Shared Earth/Flame/Frost Shock cooldown reduction", "A Shock is used"),
        ["elemental-alacrity"]=Implemented("Bolt and Chain Lightning base cast time reduced .17/.33/.50s", "A listed spell is cast"),
        ["call-of-flame"]=Implemented("Searing, Magma, Flametongue Totem, Nova and Flame Shock damage; Flame Shock duration", "Fire spell or active Fire totem damages an eligible target; Flametongue Totem speed-normalized value is provisional"),
        ["elemental-focus"]=Implemented("Seeded10% Clearcasting, next damage spell free; run-local costs compose with Maelstrom", "Owned direct damage spells; no passive Overload consumption"),
        ["elemental-warding"]=Implemented("Incoming Fire/Frost/Nature damage reduced3/7/10%", "Explicit incoming nonphysical damage scenario"),
        ["trial-by-fire"]=Implemented("Nova damage/cooldown plus real Magma duration and extra ticks", "Live source-owned Fire totem; Magma target remains alive through extension"),
        ["eye-of-the-storm"]=Implemented("Seeded23/47/70% resistance to actual Bolt/Chain Lightning pushback", "Incoming damaging hits during a hard cast"),
        ["earthbound"]=Implemented("Earthbind placement roots enemies in its real radius for 5 seconds, stopping their movement", "Explicit controllable-target movement scenario; default boss immunity is tested separately"),
        ["elemental-reach"]=Implemented("Bolt, Chain Lightning and Nova range gates gain 3/6 yards", "Explicit one-dimensional target distance; Nova measures distance from the active Fire totem"),
        ["call-of-thunder"]=Implemented("Bolt/Chain Lightning critical chance +5 points", "A listed lightning spell resolves"),
        ["lightning-overload"]=Implemented("Seeded10/20/30% duplicate lightning, half damage and zero threat; no recursion", "Landed player-cast Bolt/Chain Lightning"),
        ["elemental-fury"]=Implemented("Fire/Frost/Nature spell and Searing/Magma crit damage bonus", "A supported damaging spell or Fire totem crits"),
        ["lightning-mastery"]=Implemented("15s aura: Bolt cast -.5s, Chain Lightning cooldown removed/restored", "Learned cooldown is selected by policy"),
        ["earths-grasp"]=Implemented("Stoneclaw health +25/50% and Earthbind radius +10/20%", "Actual incoming attacks deplete Stoneclaw health; controllable moving targets use the enlarged Earthbind radius"),
        ["thundering-strikes"]=Implemented("Critical chance for attacks and all supported damage/healing spells", "An eligible hit or heal resolves"),
        ["ancestral-knowledge"]=Implemented("Total Intellect2%/rank before derived stats, MD/MQ and Mana", "Character preparation; reversible stat changes"),
        ["guardian-totems"]=Implemented("Stoneskin and Windwall reduction +10/20%; Grounding cooldown reduced by 1/2 seconds", "Incoming physical melee/ranged damage or a non-area damaging spell with the corresponding active totem in range"),
        ["mental-dexterity"]=Implemented("Attack Power from33/66/100% Intellect after AK", "Character preparation and live Intellect deltas"),
        ["improved-ghost-wolf"]=Implemented("Ghost Wolf casts in 3/2/0 seconds at zero/partial/maximum rank and becomes usable indoors at either learned rank", "Explicit player travel with real form movement speed; normal spells and weapon attacks pause while transformed"),
        ["improved-lightning-shield"]=Implemented("Existing charged Shield orbs gain5% damage/rank", "Incoming hits with live Lightning Shield and available charge/ICD"),
        ["elemental-weapons"]=Implemented("Hand-local Rockbiter/Windfury AP and Flametongue/Frostbrand damage", "A real selected weapon imbue is active"),
        ["shamanistic-focus"]=Implemented("All three Shocks and Lightning Shield Mana -45%", "An eligible action is cast"),
        ["anticipation"]=Implemented("Actual incoming melee dodge +2%/rank", "Incoming physical melee scenario; can trigger Improved Stormstrike"),
        ["toughness"]=Implemented("Total Stamina2%/rank before health; reversible", "Character preparation and live stat deltas"),
        ["flurry"]=Implemented("6%/rank live attack speed, three swing charges, refresh/expiry", "Melee critical followed by scheduled weapon swings"),
        ["stormstrike"]=Implemented("100% normalized weapon hit,8s cooldown, source-owned next eligible Nature hit +20%", "Melee range, learned ability, later Bolt/Chain Lightning/Earth Shock"),
        ["spirit-weapons"]=Implemented("Incoming melee parry and conditional threat -30%/+30% with Rockbiter", "Incoming melee and outgoing damage/healing threat"),
        ["mental-quickness"]=Implemented("Spell damage and healing15/30% of Intellect", "Preparation and live Intellect deltas"),
        ["improved-stormstrike"]=Implemented("50/100% chance casting Spirit regen15s and own incoming dodge/parry cooldown resets", "Stormstrike casts; actual incoming avoidance"),
        ["maelstrom-weapon"]=Implemented("6–10PPM by rank/main-hand speed;20% cost/time per stack at every rank; reservations/expiry", "Eligible weapon damage and subsequent Bolt"),
        ["rage-of-the-farseer"]=Implemented("30% weapon haste25s with live swing rescheduling; no spell haste", "Learned cooldown selected by policy"),
        ["improved-healing-wave"]=Implemented("Healing Wave cast -.1s/rank", "A Healing Wave is cast"),
        ["tidal-focus"]=Implemented("Healing spell Mana -1%/rank plus melee and spell hit +1 point/rank", "Healing costs and damaging hit rolls"),
        ["mindfulness"]=Implemented("17/33/50% Spirit regen during the five-second rule", "Mana spent with nonzero configured Spirit regeneration"),
        ["natural-grace"]=Implemented("Spell damage and effective healing threat -5%/rank", "A damaging or healing spell generates threat"),
        ["totemic-focus"]=Implemented("All learned totem and summon/relocation Call costs discounted exactly once", "Totem placement, programmed Calls or actual Totemic Call relocation; recall uses the discounted cost paid"),
        ["improved-reincarnation"]=Implemented("Maximum health +2/4%; revival Health and Mana 20/30/40%; cooldown 60/50/40 minutes", "Explicit opt-in death/revival scenario removes owned auras, cancels the current cast, restores resources and enforces the cooldown"),
        ["ancestral-healing"]=Implemented("Healing crit applies15s armor8/17/25%, consumed by real incoming mitigation", "Healing crit followed by incoming physical damage"),
        ["healing-focus"]=Implemented("Seeded23/47/70% resistance to actual healing cast pushback", "Incoming damaging hits during Healing Wave/Lesser Healing Wave/Chain Heal"),
        ["water-shield"]=Implemented("Three charges;2% maximum Mana from incoming hits or own spell crits; mutually exclusive Shield", "Actual hit or spell crit after provisional ICD; recast when empty"),
        ["tidal-mastery"]=Implemented("Healing-only critical chance +1 point/rank", "Healing spell resolves; no lightning crit benefit"),
        ["restorative-totems"]=Implemented("Mana Spring +5%/rank and Healing Stream +10%/rank", "Real periodic water-totem pulses"),
        ["mana-tide-totem"]=Implemented("Learned12s water totem, four real flat Mana pulses, replacement and recall", "Mana missing; totem survives pulse schedule"),
        ["healing-way"]=Implemented("Healing Wave throughput8/17/25%", "Healing Wave resolves"),
        ["nature-swiftness"]=Implemented("Next eligible Nature cast instant; consumes once across healing and lightning", "Learned cooldown selected before eligible cast"),
        ["purification"]=Implemented("All supported healing throughput +2%/rank including periodic and bounces", "Healing spell or Healing Stream pulse"),
        ["riptide"]=Implemented("HoT-only15s,12s cooldown; source-owned Chain Heal primary/bounces +25%", "Learned Riptide and subsequent Chain Heal on its recipient")
    };
    private static ShamanTalentCoverage Implemented(string effect,string scenario)=>new("implemented",[effect],[],[scenario]);
}

public sealed record ShamanTalentCoverage(string Status,IReadOnlyList<string> ImplementedEffects,
    IReadOnlyList<string> MissingEffects,IReadOnlyList<string> ApplicableScenarios);
