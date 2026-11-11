using WarcraftSim.Data.Forever.Rogue;
using WarcraftSim.Data.Forever.Priest;
using WarcraftSim.Data.Forever.Druid;
using WarcraftSim.Data.Forever.Shaman;

namespace WarcraftSim.Api.Simulation;

/// <summary>Restricted exact-name/tree/cap matches to the pinned historical tree.
/// No numerical effects, current-client certification or upstream simulation flags are imported.</summary>
public static class ForeverHistoricalCasterTalentCatalog
{
    public const string SourceCommit = "1cc5ae1e9abdbf941b7326d2d90421822dce31ba";
    public const string SourceStatus = "historical-provisional-not-current-client-certified";
    public sealed record Entry(string ClassKey,string Key,string Tree,int MaxRank,int Row,
        int Column,string? PrerequisiteKey,int PrerequisiteRank);
    public static IReadOnlyList<string> RogueModelingNotes { get; } = Array.AsReadOnly(new[] {
        "Restricted partial historical Assassination UI admission: twelve exact name/tree/cap matches support up to 38 allocated points with 13 legally unspent. This is not optimized, fully modeled, or current-client certified; the 36-point test variant leaves 15 unspent. Legacy direct talent allocations are unchanged and are not strict allocations; the sourced Mutilate tuning correction is described below.",
        "Pinned Rogue source is preserved as decoded tool content, not independently downloaded raw bytes or an independently verified source blob hash. Historical metadata is not current-client certification.",
        "Source prerequisite pointers identify parent nodes but not required ranks; full-parent rank gating is the existing local strict-validation convention, not independently sourced rank evidence.",
        "Mutilate uses the historical provisional default of 13 bonus damage per hand; explicit overrides including legacy 17.25 and zero remain unchanged. Its 20% bonus recognizes live Deadly, Wound, Mind-numbing and Crippling poison auras once. Instant Poison is direct damage only and creates no lingering poisoned state. Existing source-unfiltered target-state and hand-impact ordering are compatibility conventions, not multiplayer or current-client certification.",
        "Vile Poisons damage is modeled but dispel resistance has no dispel simulation. Cold Blood eligibility and broader utility retain historical differences and limitations. Weapon/ability damage, hit/crit, armor, positioning and utility modeling remain provisional; admitted metadata is not complete mechanics.",
        "Prepared Rogue catalog equipment and enchant IDs remain unsupported; explicitly authored stat budgets and baseline provenance keep their existing boundaries."
    });
    public static IReadOnlyList<string> FeralModelingNotes { get; } = Array.AsReadOnly(new[] {
        "Restricted fixed-Cat partial historical default allocation: fourteen exact name/tree/cap matches permit 0/31/5, 36 allocated and 15 legally unspent. Legal removals remain allowed; no filler, full-class or optimized-build claim.",
        "Pinned Druid source is decoded connector content, not independently downloaded upstream raw bytes or a verified blob hash; historical metadata is not current-client certification. Parent pointers name nodes, while full-parent rank gating is the existing local convention.",
        "Furor is admitted only at rank zero or five. Initial Energy is zero and there are no later form transitions; no active Furor benefit or general lower-rank correctness is claimed. Feral Swiftness movement/dodge is inactive with no movement or incoming attacks.",
        "Heart of the Wild adds only incremental Cat AP and maximum Mana to historical primary/resource inputs. Predatory Strikes adds flat level-60 AP after HOTW; white attacks and several abilities still have no AP coefficient.",
        "The inherited Omen processor executes a provisional 6% chance with a 10-second lockout, without a selectable Omen rank in this contract. Whether it should be baseline or talent-dependent is unresolved; absence from the retained talent tree does not settle eligibility.",
        "Natural Instinct retains provisional white/periodic critical eligibility and no healing model. Bear, movement, defensive, group utility and other out-of-scope effects are not certified. Manual tuning and existing automatic rotation remain provisional.",
        "Rend and Tear, Natural Reaction, unverified utilities and other legacy keys are not admitted. The partial Feral preset and read-only preparation endpoint do not add gear/modifier support, custom priorities or multi-iteration batches."
    });
    public static string FeralLimitation(string key) => key switch {
        "ferocity" => "Fixed Cat Claw/Rake costs use the existing per-rank reduction; Bear costs are outside this capability.",
        "heart-of-the-wild" => "Initial Cat incremental Strength-to-AP and Intellect-to-Mana only; ordinary primary contributions are already inclusive. Bear Health and mixed-form recalculation are outside scope.",
        "feral-swiftness" => "Inactive in fixed Cat with no movement or incoming attacks; movement speed is not simulated and dodge has no incoming events.",
        "shredding-attacks" => "Existing Cat Shred Energy reduction; Bear Lacerate is outside scope.",
        "savage-fury" => "Historically sourced 5% per rank on Claw, Shred and both Rake components; Bear Maul/Swipe cannot execute here. Existing coefficients remain provisional.",
        "sharpened-claws" => "Historically sourced 3 percentage points per rank in the existing animal-form critical roll scope; white/periodic eligibility remains provisional.",
        "shifting-power" => "Existing configured Mana-to-Energy ability; cost uses separately manual base Mana, not maximum Mana. It may act at time zero after the initial-shift preview.",
        "predatory-strikes" => "Historically sourced 30/60/90 AP at level 60 after HOTW; only existing Claw/Shred/Rake AP consumers execute here, not white attacks or invented missing coefficients.",
        "blood-frenzy" => "Existing Cat builder-critical combo-point proc; Bear Rage and broader proc eligibility are outside this fixed-Cat claim.",
        "improved-shifting-power" => "Existing Shifting Power cooldown reduction; lower cooldown does not imply unlimited Mana or an optimized rotation.",
        "leader-of-the-pack" => "Existing self animal-form critical chance; no group aura or complete utility claim.",
        "natural-instinct" => "Existing critical-bonus multiplier with provisional white/periodic eligibility; no healing model or complete source-effect certification.",
        "berserk" => "Existing toggle-enabled Cat cooldown/cost behavior only; Bear cleave and fear/utility behavior are outside scope.",
        "furor" => "Rank zero or five only. Inactive with initial zero Energy and no form transitions; lower-rank probabilistic/form behavior is unsupported.",
        _ => "Outside the admitted fixed-Cat talent surface."
    };
    private static readonly Entry[] Entries =
    [
        // Deliberately only twelve exact historical matches, not every modeled Rogue
        // talent. Full-parent gating follows the disclosed local convention above.
        new("rogue","malice","assassination",5,1,3,null,0),
        new("rogue","ruthlessness","assassination",3,2,1,null,0),
        new("rogue","improved-slice-and-dice","assassination",3,2,4,null,0),
        new("rogue","improved-expose-armor","assassination",2,3,2,null,0),
        new("rogue","lethality","assassination",5,3,3,"malice",5),
        new("rogue","vile-poisons","assassination",5,4,1,null,0),
        new("rogue","cold-blood","assassination",1,4,2,null,0),
        new("rogue","improved-poisons","assassination",5,4,3,null,0),
        new("rogue","vigor","assassination",2,5,1,null,0),
        new("rogue","mutilate","assassination",1,5,2,null,0),
        new("rogue","seal-fate","assassination",5,6,3,null,0),
        new("rogue","venom","assassination",1,7,2,"mutilate",1),
        new("druid","ferocity","feral",5,1,2,null,0),
        new("druid","heart-of-the-wild","feral",5,1,3,null,0),
        new("druid","feral-swiftness","feral",2,2,1,null,0),
        new("druid","shredding-attacks","feral",3,3,1,null,0),
        new("druid","savage-fury","feral",2,3,2,null,0),
        new("druid","sharpened-claws","feral",2,3,4,null,0),
        new("druid","shifting-power","feral",1,4,1,"shredding-attacks",3),
        new("druid","predatory-strikes","feral",3,4,3,null,0),
        new("druid","blood-frenzy","feral",2,4,4,"sharpened-claws",2),
        new("druid","improved-shifting-power","feral",2,5,1,"shifting-power",1),
        new("druid","leader-of-the-pack","feral",1,5,2,null,0),
        new("druid","natural-instinct","feral",2,5,4,null,0),
        new("druid","berserk","feral",1,7,2,"leader-of-the-pack",1),
        new("druid","improved-wrath","balance",5,1,2,null,0),
        new("druid","genesis","balance",5,1,3,null,0),
        new("druid","improved-moonfire","balance",2,2,2,null,0),
        new("druid","moonglow","balance",3,2,1,null,0),
        new("druid","natures-majesty","balance",2,2,3,null,0),
        new("druid","natures-reach","balance",2,2,4,null,0),
        new("druid","natures-splendor","balance",1,3,3,"natures-majesty",2),
        new("druid","insect-swarm","balance",1,4,1,null,0),
        new("druid","vengeance","balance",5,4,2,"improved-moonfire",2),
        new("druid","eclipse","balance",3,5,3,null,0),
        new("druid","moonfury","balance",5,6,2,null,0),
        new("druid","moonkin-form","balance",1,7,2,null,0),
        new("druid","natures-grace","balance",1,5,2,null,0),
        new("druid","improved-starfire","balance",5,4,3,null,0),
        new("druid","naturalist","restoration",5,2,1,null,0),
        new("druid","subtlety","restoration",3,2,2,null,0),
        new("druid","reflection","restoration",3,3,2,null,0),
        new("druid","natures-focus","restoration",5,1,2,null,0),
        new("druid","furor","restoration",5,1,3,null,0),
        new("shaman","convection","elemental",5,1,2,null,0),
        new("shaman","concussion","elemental",5,1,3,null,0),
        new("shaman","call-of-flame","elemental",3,2,4,null,0),
        new("shaman","elemental-alacrity","elemental",3,2,3,null,0),
        new("shaman","lightning-overload","elemental",3,5,3,"elemental-alacrity",3),
        new("shaman","elemental-fury","elemental",5,6,3,"lightning-overload",3),
        new("shaman","lava-burst","elemental",1,5,2,null,0),
        new("shaman","elemental-mastery","elemental",1,7,2,"lava-burst",1),
        new("shaman","elemental-focus","elemental",1,3,1,null,0),
        new("shaman","elemental-devastation","elemental",3,2,1,null,0),
        new("shaman","reverberation","elemental",5,2,2,null,0),
        new("shaman","tidal-mastery","restoration",5,4,1,null,0),
        new("shaman","tidal-focus","restoration",5,1,3,null,0),
        new("shaman","mindfulness","restoration",3,2,1,null,0),
        new("shaman","totemic-focus","restoration",5,2,3,null,0),
        new("shaman","restorative-totems","restoration",5,4,2,null,0),
        new("shaman","natural-grace","restoration",3,2,2,null,0),
        new("shaman","improved-healing-wave","restoration",5,1,2,null,0),
        new("shaman","thundering-strikes","enhancement",5,1,2,null,0),
        new("shaman","ancestral-knowledge","enhancement",5,1,3,null,0),
        new("shaman","improved-ghost-wolf","enhancement",2,2,3,null,0),
        new("shaman","mental-dexterity","enhancement",3,2,2,null,0),
        new("shaman","improved-lightning-shield","enhancement",3,2,4,null,0),
        new("shaman","elemental-weapons","enhancement",3,3,1,null,0),
        new("shaman","flurry","enhancement",5,4,2,"mental-dexterity",3),
        new("shaman","stormstrike","enhancement",1,4,3,null,0),
        new("shaman","mental-quickness","enhancement",2,5,2,null,0),
        new("shaman","improved-stormstrike","enhancement",2,5,3,"stormstrike",1),
        new("shaman","maelstrom-weapon","enhancement",5,6,3,null,0),
        new("shaman","rage-of-the-farseer","enhancement",1,7,2,"mental-quickness",2),
        new("shaman","shamanistic-focus","enhancement",1,3,3,null,0),
        new("shaman","elemental-warding","elemental",3,3,2,null,0),
        new("shaman","eye-of-the-storm","elemental",3,4,2,null,0),
        new("shaman","elemental-reach","elemental",2,5,1,null,0),
        new("shaman","trial-by-fire","elemental",2,4,1,null,0),
        // Shadow Magic => shadow; Improved Shadow Word: Pain => improved-shadow-word-pain.
        // Discipline nodes are validation metadata only: this restricted subset has
        // no early Discipline fillers and does not imply a Shadow Penance action.
        new("priest","inner-focus","discipline",1,3,2,null,0),
        new("priest","power-infusion","discipline",1,7,2,"penance",1),
        new("priest","penance","discipline",1,5,2,null,0),
        new("priest","shadow-focus","shadow",5,1,1,null,0),
        new("priest","spirit-tap","shadow",5,1,3,null,0),
        new("priest","improved-shadow-word-pain","shadow",2,2,3,null,0),
        new("priest","shadow-reach","shadow",2,2,4,null,0),
        new("priest","mind-flay","shadow",1,3,3,null,0),
        new("priest","improved-mind-flay","shadow",2,3,4,"mind-flay",1),
        new("priest","improved-mind-blast","shadow",5,2,1,null,0),
        new("priest","shadow-weaving","shadow",3,4,3,null,0),
        new("priest","vampiric-embrace","shadow",1,4,2,null,0),
        new("priest","devouring-contagion","shadow",2,5,3,null,0),
        new("priest","early-demise","shadow",2,6,1,null,0),
        new("priest","darkness","shadow",5,6,3,null,0),
        new("priest","shadowform","shadow",1,7,2,"vampiric-embrace",1),
    ];
    public static bool Supports(string cls,string spec) =>
        (cls=="druid" && spec is "balance" or "feral") || (cls=="shaman" && spec is "elemental" or "enhancement") ||
        (cls=="priest" && spec=="shadow") || (cls=="rogue" && spec=="assassination");
    public static string SourceUrl(string cls) =>
        $"https://github.com/gunba/wow-forever-sim/blob/{SourceCommit}/ui/core/talents/trees/{cls}.json";
    // Preserve the existing caster-only lookup contract for callers that do not
    // select a specialization. A Shaman class lookup still means Elemental.
    public static IReadOnlyList<Entry> ForClass(string cls) => ForSpecialization(cls,cls switch {
        "druid"=>"balance","shaman"=>"elemental","priest"=>"shadow",_=>""});

    public static IReadOnlyList<Entry> ForSpecialization(string cls,string spec)
    {
        IReadOnlyList<(string Key,string Tree,int MaxRank)> modeled=(cls,spec) switch {
            ("rogue","assassination")=>ForeverRogueTalentBuild.SupportedTalentMetadata,
            ("druid","balance")=>ForeverBalanceDruidTalents.SupportedTalentMetadata,
            ("druid","feral")=>ForeverFeralDruidTalents.SupportedTalentMetadata,
            ("shaman","elemental")=>ForeverElementalShamanTalents.SupportedTalentMetadata,
            ("shaman","enhancement")=>ForeverEnhancementShamanTalents.SupportedTalentMetadata,
            ("priest","shadow")=>ForeverPriestShadowTalentBuild.SupportedTalentMetadata,
            _=>Array.Empty<(string Key,string Tree,int MaxRank)>()
        };
        return Entries.Where(x=>x.ClassKey==cls && modeled.Any(m=>
            m.Key==x.Key && m.Tree==x.Tree && m.MaxRank==x.MaxRank)).ToArray();
    }
    public static Entry? Find(string cls,string key) =>
        ForClass(cls).FirstOrDefault(x=>x.Key==key);
    public static Entry? Find(string cls,string spec,string key) =>
        ForSpecialization(cls,spec).FirstOrDefault(x=>x.Key==key);
}
