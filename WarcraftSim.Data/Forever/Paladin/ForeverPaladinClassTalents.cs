namespace WarcraftSim.Data.Forever.Paladin;

/// <summary>Current client-derived allocation topology with independently dated official overlays.
/// These identities determine legality only; runtime coverage and source certainty are separate.
/// Talent IDs are not spell IDs. Empty RankSpellIds explicitly means no rank-spell mapping was pinned.</summary>
public static class ForeverPaladinClassTalents
{
    public const string ReviewedDate = "2026-11-13";
    public const string SourceBuild = "1.60.1.70888";
    public const string TreeUrl = "https://theforeverera.com/en/talents/paladin/";
    public const string TreeDocumentSha256 = "21a8fd91f3c21962ed36255e84766a742c802a22f609fcb7f33e3a00208ff31a";
    public const string DevelopmentNotesUrl = "https://us.forums.blizzard.com/en/wow/t/wow-forever-beta-development-notes-%E2%80%93-updated-november-2/2360696/9";
    public const string HotfixNotesUrl = "https://us.forums.blizzard.com/en/wow/t/wow-forever-hotfixes-november-11-2026/2403040";
    public const int UnsealedWrathTalentId = 105692;
    public const int UnsealedWrathSpellId = 1310735;
    public static IReadOnlyList<PaladinClassTalentDefinition> Entries { get; } =
    [
        new("spiritual-focus","Spiritual Focus",105335,"holy",5,1,2,null,0,"spell_arcane_blink",[],["healing-cast-pushback-resistance"]),
        new("divine-strength","Divine Strength",113630,"holy",5,1,3,null,0,"ability_golemthunderclap",[],["strength-multiplier"]),
        new("healing-light","Healing Light",105333,"holy",3,2,1,null,0,"spell_holy_holybolt",[],["holy-light-flash-holy-shock-healing"]),
        new("reverence","Reverence",110871,"holy",2,2,2,null,0,"spell_holy_divineillumination",[],["spirit-regeneration-while-casting"]),
        new("improved-seals","Improved Seals",105334,"holy",5,2,3,null,0,"ability_thunderbolt",[],["seal-damage","judgement-damage"]),
        new("purifying-power","Purifying Power",105327,"holy",2,2,4,null,0,"spell_holy_purifyingpower",[],["cleanse-purify-cost","exorcism-holy-wrath-cooldown"]),
        new("divine-favor","Divine Favor",105325,"holy",1,3,1,null,0,"spell_holy_heal",[20216],["guaranteed-next-heal-critical","holy-shock-damage-critical"]),
        new("illumination","Illumination",105329,"holy",5,3,2,"reverence",2,"spell_holy_greaterheal",[],["critical-heal-mana-refund"]),
        new("holy-conduit","Holy Conduit",105704,"holy",2,3,3,null,0,"spell_holy_devineaegis",[],["four-holy-action-mana-costs"]),
        new("divine-precision","Divine Precision",105324,"holy",2,4,1,null,0,"spell_holy_mindsooth",[],["holy-spell-hit","silence-duration"]),
        new("infusion-of-light","Infusion of Light",110873,"holy",2,4,2,null,0,"ability_paladin_infusionoflight",[],["critical-triggered-holy-light-cast-time"]),
        new("oath-of-light","Oath of Light",105330,"holy",1,4,3,null,0,"spell_holy_greaterblessingoflight",[1328548],["single-friendly-target-healing-bonus"]),
        new("consecrated-ground","Consecrated Ground",110872,"holy",2,5,1,null,0,"spell_holy_innerfire",[],["first-four-targets-holy-damage"]),
        new("holy-shock","Holy Shock",105323,"holy",1,5,2,null,0,"spell_holy_searinglight",[],["hostile-holy-damage","friendly-healing"]),
        new("boundless-light","Boundless Light",113631,"holy",2,5,3,"holy-shock",1,"ability_paladin_sacredcleansing",[],["non-oath-friendly-holy-shock-critical"]),
        new("holy-power","Holy Power",105321,"holy",5,6,3,null,0,"spell_holy_power",[],["holy-healing-critical-bonus"]),
        new("lights-vigil","Light's Vigil",105320,"holy",1,7,2,"holy-shock",1,"ability_paladin_judgementofthepure",[],["friendly-mark","holy-shock-cooldown-reset","friendly-party-healing"]),
        new("divine-intellect","Divine Intellect",105332,"protection",5,1,2,null,0,"spell_nature_sleep",[],["intellect-multiplier"]),
        new("redoubt","Redoubt",105626,"protection",5,1,3,null,0,"ability_defend",[],["incoming-melee-triggered-block-aura"]),
        new("precision","Precision",105638,"protection",3,2,1,null,0,"ability_rogue_ambush",[],["attack-hit","spell-hit"]),
        new("guardians-favor","Guardian's Favor",105637,"protection",2,2,2,null,0,"spell_holy_sealofprotection",[],["blessing-protection-cooldown","blessing-freedom-duration"]),
        new("sacred-duty","Sacred Duty",105632,"protection",2,2,4,null,0,"spell_holy_divineintervention",[],["three-defensive-cooldowns","forbearance-duration"]),
        new("templars-bulwark","Templar's Bulwark",105625,"protection",1,3,1,null,0,"ability_paladin_shieldofthetemplar",[1311015],["maximum-health-absorb","forbearance-exclusion"]),
        new("improved-hammer-of-justice","Improved Hammer of Justice",105633,"protection",3,3,2,null,0,"spell_holy_sealofmight",[],["hammer-justice-cooldown"]),
        new("shield-specialization","Shield Specialization",110874,"protection",2,3,3,"redoubt",5,"inv_shield_06",[],["shield-block-value","block-mana-return"]),
        new("toughness","Toughness",105630,"protection",5,3,4,null,0,"spell_holy_devotion",[],["equipment-armor-multiplier"]),
        new("swift-judgement","Swift Judgement",110878,"protection",1,4,1,"templars-bulwark",1,"ability_paladin_judgementred",[1310994],["independent-judgement-action"]),
        new("improved-righteous-fury","Improved Righteous Fury",105634,"protection",3,4,2,null,0,"spell_holy_sealoffury",[],["righteous-fury-incoming-damage-reduction"]),
        new("anticipation","Anticipation",105636,"protection",5,4,3,null,0,"spell_magic_lesserinvisibilty",[],["defense-skill"]),
        new("stoicism","Stoicism",105639,"protection",2,5,1,null,0,"spell_holy_stoicism",[],["stun-duration","charm-duration"]),
        new("holy-shield","Holy Shield",105628,"protection",1,5,2,null,0,"spell_holy_blessingofprotection",[],["block-chance","block-triggered-holy-damage","charges","additional-threat"]),
        new("reckoning","Reckoning",105627,"protection",5,5,3,null,0,"spell_holy_blessingofstrength",[],["block-generated-attack","incoming-critical-generated-attack","generated-attack-cap"]),
        new("iron-creed","Iron Creed",110879,"protection",3,6,3,null,0,"spell_holy_improvedresistanceauras",[],["holy-strike-threat","temporary-incoming-damage-reduction"]),
        new("shieldsworn","Shieldsworn",110875,"protection",1,7,2,"holy-shield",1,"spell_holy_greaterblessingofsanctuary",[],["holy-strike-cooldown","shield-action","free-block-triggers","temporary-spell-block"]),
        new("deflection","Deflection",105707,"retribution",5,1,2,null,0,"ability_parry",[],["parry-chance"]),
        new("benediction","Benediction",105706,"retribution",5,1,3,null,0,"spell_frost_windwalkon",[],["instant-action-mana-cost"]),
        new("improved-judgement","Improved Judgement",105705,"retribution",2,2,1,null,0,"spell_holy_righteousfury",[],["judgement-cooldown"]),
        new("conviction","Conviction",105703,"retribution",3,2,2,null,0,"spell_holy_retributionaura",[],["attack-critical","spell-critical"]),
        new("sanctified-judgement","Sanctified Judgement",105701,"retribution",3,2,3,null,0,"ability_paladin_judgementblue",[],["judgement-seal-cost-refund"]),
        new("vindication","Vindication",105702,"retribution",3,3,1,null,0,"spell_holy_vindication",[],["target-attack-power-debuff"]),
        new("unyielding-faith","Unyielding Faith",105331,"retribution",2,3,2,null,0,"spell_holy_unyieldingfaith",[],["fear-duration","disorient-duration"]),
        new("seal-of-command","Seal of Command",105696,"retribution",1,3,3,null,0,"ability_warrior_innerrage",[],["weapon-seal-proc","command-judgement","controlled-target-judgement-bonus"]),
        new("pursuit-of-justice","Pursuit of Justice",105699,"retribution",2,3,4,null,0,"spell_holy_persuitofjustice",[],["movement-speed","mounted-speed"]),
        new("eye-for-an-eye","Eye for an Eye",105698,"retribution",2,4,1,null,0,"spell_holy_eyeforaneye",[],["incoming-critical-reflection","reflection-health-cap"]),
        new("sacred-arbiter","Sacred Arbiter",105700,"retribution",1,4,2,null,0,"inv_sword_08",[],["holy-strike-damage","judgement-refresh"]),
        new("two-handed-weapon-specialization","Two-Handed Weapon Specialization",105697,"retribution",3,4,4,null,0,"inv_hammer_04",[],["two-hand-physical-holy-damage","two-hand-threat-reduction"]),
        new("turn-evil","Turn Evil",113629,"retribution",2,5,1,null,0,"spell_holy_turnundead",[],["turn-undead-cast-time","turn-undead-cooldown","demon-eligibility"]),
        new("repentance","Repentance",105694,"retribution",1,5,2,null,0,"spell_holy_prayerofhealing",[],["humanoid-incapacitate","break-on-damage"]),
        new("vengeance","Vengeance",105693,"retribution",3,5,3,"seal-of-command",1,"ability_racial_avatar",[],["nonperiodic-critical-stacking-physical-holy-damage"]),
        new("champion-of-the-light","Champion of the Light",110882,"retribution",5,6,3,null,0,"ability_paladin_enlightenedjudgements",[],["intellect-to-spell-damage","hammer-wrath-cast-time"]),
        new("unsealed-wrath","Unsealed Wrath",105692,"retribution",1,7,2,"repentance",1,"spell_holy_blessedresillience",[1310735],["seal-storage","seal-switch-echo","windfury-exclusion","proc-recursion-boundary"]),
    ];
    public static IReadOnlyList<(string Key,string Tree,int MaxRank)> SupportedTalentMetadata =>
        Entries.Select(x=>(x.Key,x.Tree,x.MaxRank)).ToArray();
    public static PaladinClassTalentDefinition? Find(string key) =>
        Entries.FirstOrDefault(x=>x.Key.Equals(key,StringComparison.OrdinalIgnoreCase));

    public static void Validate(IReadOnlyDictionary<string,int> ranks,int level=60)
    {
        ArgumentNullException.ThrowIfNull(ranks);
        if(level is <10 or >60)throw new ArgumentException("Paladin talent level must be 10–60.");
        var canonical=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        foreach(var (key,rank) in ranks)
        {
            var node=Find(key);
            if(node is null || rank<0 || rank>node.MaxRank || !canonical.TryAdd(node.Key,rank))
                throw new ArgumentException($"Unknown, duplicate or invalid Paladin talent: {key}={rank}.");
        }
        if(canonical.Values.Sum()>level-9)
            throw new ArgumentException("Paladin talent allocation exceeds the level point budget.");
        foreach(var node in Entries.Where(x=>canonical.GetValueOrDefault(x.Key)>0))
        {
            var earlier=Entries.Where(x=>x.Tree==node.Tree && x.Row<node.Row)
                .Sum(x=>canonical.GetValueOrDefault(x.Key));
            if(earlier<(node.Row-1)*5)
                throw new ArgumentException($"{node.Key} needs {(node.Row-1)*5} points in earlier {node.Tree} rows, found {earlier}.");
            if(node.ParentKey is not null && canonical.GetValueOrDefault(node.ParentKey)<node.ParentRank)
                throw new ArgumentException($"{node.Key} requires {node.ParentKey} rank {node.ParentRank}.");
        }
    }
    public static ForeverPaladinTalents FromRanks(IReadOnlyDictionary<string,int> ranks)
    {
        Validate(ranks);
        return new() {Ranks=new(ranks,StringComparer.OrdinalIgnoreCase)};
    }
    public static Dictionary<string,int> Starter(string tree)
    {
        // Reviewed examples assembled from the sourced topology, not popularity/optimality claims.
        Dictionary<string,int> ranks=tree switch
        {
            "retribution"=>new(StringComparer.Ordinal) {
                ["deflection"]=5,["benediction"]=5,["improved-judgement"]=2,["conviction"]=3,
                ["sanctified-judgement"]=3,["seal-of-command"]=1,["pursuit-of-justice"]=1,
                ["sacred-arbiter"]=1,["two-handed-weapon-specialization"]=3,["repentance"]=1,
                ["vengeance"]=3,["champion-of-the-light"]=2,["unsealed-wrath"]=1,
                ["divine-strength"]=5,["improved-seals"]=5,["divine-intellect"]=5,
                ["precision"]=3,["guardians-favor"]=2},
            "holy"=>new(StringComparer.Ordinal) {
                ["spiritual-focus"]=5,["healing-light"]=3,["reverence"]=2,["divine-favor"]=1,
                ["illumination"]=5,["holy-conduit"]=2,["infusion-of-light"]=2,["oath-of-light"]=1,
                ["holy-shock"]=1,["boundless-light"]=2,["consecrated-ground"]=1,["holy-power"]=5,
                ["lights-vigil"]=1,["divine-intellect"]=5,["benediction"]=5,["conviction"]=3},
            "protection"=>new(StringComparer.Ordinal) {
                ["divine-intellect"]=5,["redoubt"]=5,["precision"]=3,["sacred-duty"]=2,
                ["templars-bulwark"]=1,["shield-specialization"]=2,["toughness"]=2,
                ["swift-judgement"]=1,["improved-righteous-fury"]=3,["anticipation"]=1,
                ["holy-shield"]=1,["reckoning"]=1,["iron-creed"]=3,["shieldsworn"]=1,
                ["divine-strength"]=5,["improved-seals"]=5,["holy-conduit"]=2,
                ["benediction"]=5,["conviction"]=3},
            _=>throw new ArgumentException("Choose a current Paladin tree for a reviewed example.")
        };
        Validate(ranks);
        return ranks;
    }
}

public sealed class ForeverPaladinTalents
{
    public Dictionary<string,int> Ranks { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    // Deserialization may supply an ordinary case-sensitive dictionary. Legality
    // accepts canonical keys irrespective of case, so execution must do so too.
    public int Rank(string key) => Ranks?.FirstOrDefault(
        pair=>pair.Key.Equals(key,StringComparison.OrdinalIgnoreCase)).Value ?? 0;
    public void Validate(int level=60) => ForeverPaladinClassTalents.Validate(Ranks,level);
}

public sealed record PaladinClassTalentDefinition(string Key,string Name,int TalentId,string Tree,
    int MaxRank,int Row,int Column,string? ParentKey,int ParentRank,string IconKey,
    IReadOnlyList<int> RankSpellIds,IReadOnlyList<string> EffectComponents)
{
    public int SpellId => ForeverPaladinSourceData.TalentSpellIds[Key];
    public string SpellIdSourceUrl => ForeverPaladinSourceData.TalentSpellSourceUrl;
    public string SourceUrl => ForeverPaladinClassTalents.TreeUrl;
    public string SourceConfidence => "client-70888-mirror-with-dated-official-overrides-provisional";
}
