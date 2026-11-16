namespace WarcraftSim.Data.Forever.Rogue;

/// <summary>November class-wide allocation, independent of presets and runtime coverage.
/// The complete topology is read from build 1.60.1.70888, not inferred from the obsolete
/// October tree. Official November 2 changes corroborate the current names and ranks.
/// Legacy ForeverRogueTalentBuild validation intentionally retains its old contract.</summary>
public static class ForeverRogueClassTalents
{
    public const string ReviewedDate = "2026-11-12";
    public const string SourceBuild = "1.60.1.70888";
    public const string TreeUrl = "https://theforeverera.com/en/talents/rogue/";
    public const string TreeDocumentSha256 = "09fe460693c5b48ab708e229c38deb8078950f9021af0a658e4c617e66350ead";
    public const string DevelopmentNotesUrl = "https://us.forums.blizzard.com/en/wow/t/wow-forever-beta-development-notes-%E2%80%93-updated-november-2/2360696";
    public const string DeathMarkSourceUrl = "https://www.wowhead.com/forever/spell=1325758/death-mark";
    public const string MutilateSourceUrl = "https://foreverdb.net/spell/1241584";
    public static IReadOnlyList<RogueClassTalentDefinition> Entries { get; } =
    [
        new("improved-gouge","assassination",3,1,1,null,0),
        new("remorseless-attacks","assassination",2,1,2,null,0),
        new("malice","assassination",5,1,3,null,0),
        new("ruthlessness","assassination",3,2,1,null,0),
        new("improved-expose-armor","assassination",2,2,2,null,0),
        new("improved-slice-and-dice","assassination",3,2,4,null,0),
        new("vigor","assassination",2,3,1,null,0),
        new("blood-splatter","assassination",1,3,2,null,0),
        new("lethality","assassination",2,3,3,"malice",5),
        new("vile-poisons","assassination",5,4,1,null,0),
        new("cold-blood","assassination",1,4,2,null,0),
        new("improved-poisons","assassination",5,4,3,null,0),
        new("relentless-strikes","assassination",5,5,1,null,0),
        new("venom","assassination",1,5,2,null,0),
        new("improved-kidney-shot","assassination",2,5,3,null,0),
        new("seal-fate","assassination",3,6,3,null,0),
        new("mutilate","assassination",1,7,2,"venom",1),
        new("coup-de-grace","combat",3,1,1,null,0),
        new("efficient-strikes","combat",2,1,2,null,0),
        new("lightning-reflexes","combat",5,1,3,null,0),
        new("puncturing-wounds","combat",3,2,1,null,0),
        new("deflection","combat",3,2,2,null,0),
        new("precision","combat",3,2,3,null,0),
        new("improved-sprint","combat",2,2,4,null,0),
        new("endurance","combat",2,3,1,null,0),
        new("riposte","combat",1,3,2,"deflection",3),
        new("composure","combat",2,3,4,null,0),
        new("improved-kick","combat",2,4,1,null,0),
        new("flawless-execution","combat",1,4,2,null,0),
        new("weapon-expertise","combat",2,4,3,"precision",3),
        new("aggression","combat",3,4,4,null,0),
        new("dual-wield-specialization","combat",2,5,1,null,0),
        new("blade-flurry","combat",1,5,2,null,0),
        new("hack-and-slash","combat",5,5,3,null,0),
        new("flash-of-steel","combat",5,6,3,null,0),
        new("adrenaline-rush","combat",1,7,2,"blade-flurry",1),
        new("setup","subtlety",3,1,1,null,0),
        new("master-of-deception","subtlety",3,1,2,null,0),
        new("opportunity","subtlety",5,1,3,null,0),
        new("dirty-deeds","subtlety",2,2,1,null,0),
        new("camouflage","subtlety",3,2,2,null,0),
        new("improved-ambush","subtlety",3,2,3,null,0),
        new("dirty-tricks","subtlety",2,2,4,null,0),
        new("initiative","subtlety",3,3,1,null,0),
        new("premeditation","subtlety",1,3,2,null,0),
        new("serrated-blades","subtlety",3,3,3,null,0),
        new("elusiveness","subtlety",2,4,1,null,0),
        new("ghostly-strike","subtlety",1,4,2,null,0),
        new("quick-getaway","subtlety",2,4,4,"dirty-tricks",2),
        new("heightened-senses","subtlety",2,5,1,null,0),
        new("preparation","subtlety",1,5,2,null,0),
        new("hemorrhage","subtlety",1,5,3,"serrated-blades",3),
        new("boot-knife","subtlety",5,6,3,null,0),
        new("death-mark","subtlety",1,7,2,"preparation",1),
    ];
    public static IReadOnlyDictionary<string,string> IconKeys { get; } =
        new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
        {
            ["improved-gouge"]="ability_gouge",
            ["remorseless-attacks"]="ability_fiegndead",
            ["malice"]="ability_racial_bloodrage",
            ["ruthlessness"]="ability_druid_disembowel",
            ["improved-expose-armor"]="ability_warrior_riposte",
            ["improved-slice-and-dice"]="ability_rogue_slicedice",
            ["vigor"]="spell_nature_earthbindtotem",
            ["blood-splatter"]="ability_rogue_rupture",
            ["lethality"]="ability_criticalstrike",
            ["vile-poisons"]="ability_rogue_feigndeath",
            ["cold-blood"]="spell_ice_lament",
            ["improved-poisons"]="ability_poisons",
            ["relentless-strikes"]="ability_warrior_decisivestrike",
            ["venom"]="inv_sword_31",
            ["improved-kidney-shot"]="ability_rogue_kidneyshot",
            ["seal-fate"]="spell_shadow_chilltouch",
            ["mutilate"]="ability_rogue_deadlybrew",
            ["coup-de-grace"]="ability_rogue_eviscerate",
            ["efficient-strikes"]="spell_shadow_ritualofsacrifice",
            ["lightning-reflexes"]="spell_nature_invisibilty",
            ["puncturing-wounds"]="ability_backstab",
            ["deflection"]="ability_parry",
            ["precision"]="ability_marksmanship",
            ["improved-sprint"]="ability_rogue_sprint",
            ["endurance"]="spell_shadow_shadowward",
            ["riposte"]="ability_warrior_challange",
            ["composure"]="ability_rogue_fleetfooted",
            ["improved-kick"]="ability_kick",
            ["flawless-execution"]="inv_sword_35",
            ["weapon-expertise"]="spell_holy_blessingofstrength",
            ["aggression"]="ability_racial_avatar",
            ["dual-wield-specialization"]="ability_dualwield",
            ["blade-flurry"]="ability_warrior_punishingblow",
            ["hack-and-slash"]="inv_sword_27",
            ["flash-of-steel"]="ability_rogue_restlessblades",
            ["adrenaline-rush"]="spell_shadow_shadowworddominate",
            ["setup"]="spell_nature_mirrorimage",
            ["master-of-deception"]="spell_shadow_charm",
            ["opportunity"]="ability_warrior_warcry",
            ["dirty-deeds"]="spell_shadow_summonsuccubus",
            ["camouflage"]="ability_stealth",
            ["improved-ambush"]="ability_rogue_ambush",
            ["dirty-tricks"]="ability_sap",
            ["initiative"]="spell_shadow_fumble",
            ["premeditation"]="spell_shadow_possession",
            ["serrated-blades"]="inv_sword_17",
            ["elusiveness"]="spell_magic_lesserinvisibilty",
            ["ghostly-strike"]="spell_shadow_curse",
            ["quick-getaway"]="ability_rogue_blackjack",
            ["heightened-senses"]="ability_ambush",
            ["preparation"]="spell_shadow_antishadow",
            ["hemorrhage"]="spell_shadow_lifedrain",
            ["boot-knife"]="inv_sword_32",
            ["death-mark"]="ability_creature_cursed_02",
        };
    public static IReadOnlyList<(string Key,string Tree,int MaxRank)> SupportedTalentMetadata =>
        Entries.Select(x => (x.Key,x.Tree,x.MaxRank)).ToArray();
    public static RogueClassTalentDefinition? Find(string key) =>
        Entries.FirstOrDefault(x => x.Key.Equals(key,StringComparison.OrdinalIgnoreCase));
    public static void Validate(ForeverRogueTalentBuild build,int level=60) =>
        Validate(build.Ranks,build.AssassinationPointsSpent,build.CombatPointsSpent,build.SubtletyPointsSpent,level);
    public static void Validate(IReadOnlyDictionary<string,int> ranks,int assassination,
        int combat,int subtlety,int level=60)
    {
        ArgumentNullException.ThrowIfNull(ranks);
        if(level is <10 or >60 || assassination<0 || combat<0 || subtlety<0 ||
            assassination+combat+subtlety>level-9)
            throw new ArgumentException("Class-wide Rogue talents exceed the level budget.");
        var canonical=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        var totals=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        foreach(var (key,rank) in ranks)
        {
            var node=Find(key);
            if(node is null || rank<0 || rank>node.MaxRank || !canonical.TryAdd(node.Key,rank))
                throw new ArgumentException($"Unknown, duplicate or invalid class-wide Rogue talent: {key}={rank}.");
            totals[node.Tree]=totals.GetValueOrDefault(node.Tree)+rank;
        }
        if(totals.GetValueOrDefault("assassination")!=assassination ||
            totals.GetValueOrDefault("combat")!=combat || totals.GetValueOrDefault("subtlety")!=subtlety)
            throw new ArgumentException("Class-wide Rogue tree totals must equal their named ranks.");
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
    public static ForeverRogueTalentBuild FromRanks(IReadOnlyDictionary<string,int> ranks)
    {
        var build=new ForeverRogueTalentBuild { Ranks=new(ranks,StringComparer.OrdinalIgnoreCase) };
        foreach(var (key,rank) in ranks)
        {
            switch(Find(key)?.Tree)
            {
                case "assassination":build.AssassinationPointsSpent+=rank;break;
                case "combat":build.CombatPointsSpent+=rank;break;
                case "subtlety":build.SubtletyPointsSpent+=rank;break;
                default:throw new ArgumentException($"Unknown class-wide Rogue talent {key}.");
            }
        }
        Validate(build);
        return build;
    }
    // Explicit per-node execution inventory. New source nodes default to unimplemented
    // unless reviewed into this table; rank legality never uses this status.
    public static IReadOnlyDictionary<string,string> CoverageLimitations { get; } =
        new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
        {
            ["master-of-deception"]="Stealth detection geometry and detection-level resolution are unimplemented.",
            ["camouflage"]="Stealth movement and baseline Stealth re-entry cooldown are unimplemented.",
            ["quick-getaway"]="Simultaneous Sap target limit executes (one extra target at either rank). Post-Sap movement speed is unimplemented.",
            ["heightened-senses"]="Spell/ranged avoidance executes in the incoming-damage resolver. Stealth detection-level bonus is unimplemented."
        };
    public static IReadOnlyDictionary<string,string> CoverageStatuses { get; } =
        new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
        {
            ["improved-gouge"]="implemented",["remorseless-attacks"]="implemented",["malice"]="implemented",
            ["ruthlessness"]="implemented",["improved-expose-armor"]="implemented",["improved-slice-and-dice"]="implemented",
            ["vigor"]="implemented",["blood-splatter"]="implemented",["lethality"]="implemented",
            ["vile-poisons"]="implemented",["cold-blood"]="implemented",["improved-poisons"]="implemented",
            ["relentless-strikes"]="implemented",["venom"]="implemented",["improved-kidney-shot"]="implemented",
            ["seal-fate"]="implemented",["mutilate"]="implemented",["coup-de-grace"]="implemented",
            ["efficient-strikes"]="implemented",["lightning-reflexes"]="implemented",["puncturing-wounds"]="implemented",
            ["deflection"]="implemented",["precision"]="implemented",["improved-sprint"]="implemented",
            ["endurance"]="implemented",["riposte"]="implemented",["composure"]="implemented",
            ["improved-kick"]="implemented",["flawless-execution"]="implemented",["weapon-expertise"]="implemented",
            ["aggression"]="implemented",["dual-wield-specialization"]="implemented",["blade-flurry"]="implemented",
            ["hack-and-slash"]="implemented",["flash-of-steel"]="implemented",["adrenaline-rush"]="implemented",
            ["setup"]="implemented",["master-of-deception"]="unimplemented",["opportunity"]="implemented",
            ["dirty-deeds"]="implemented",["camouflage"]="unimplemented",["improved-ambush"]="implemented",
            ["dirty-tricks"]="implemented",["initiative"]="implemented",["premeditation"]="implemented",
            ["serrated-blades"]="implemented",["elusiveness"]="implemented",["ghostly-strike"]="implemented",
            ["quick-getaway"]="partial",["heightened-senses"]="partial",["preparation"]="implemented",
            ["hemorrhage"]="implemented",["boot-knife"]="implemented",["death-mark"]="implemented"
        };

    public static IReadOnlyDictionary<string,string> CoverageDescriptions { get; } =
        new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
        {
            ["improved-gouge"]="Gouge control lasts 0.5 seconds longer per rank.",
            ["remorseless-attacks"]="Eligible kill grants 20/40 percentage points of crit to the next listed attack for 20 seconds.",
            ["malice"]="Adds 1 percentage point of attack and poison crit per rank.",
            ["ruthlessness"]="Finisher adds one combo point with 20% probability per rank.",
            ["improved-expose-armor"]="Expose costs 5 less Energy per rank and refunds 1/2 points after spending five.",
            ["improved-slice-and-dice"]="Slice and Dice duration increases 15% per rank.",
            ["vigor"]="Maximum Energy increases 5 per rank.",
            ["blood-splatter"]="Rupture lasts six seconds longer and Garrote nine seconds longer.",
            ["lethality"]="Adds 10/20% to critical damage bonus for the six listed strikes, including Gouge.",
            ["vile-poisons"]="Poison damage increases 4% per rank; an enemy poison dispel has 8% extra resist chance per rank.",
            ["cold-blood"]="An activated cooldown makes the next eligible landed special hit critical.",
            ["improved-poisons"]="Poison application chance increases 2 percentage points and charge preservation 10% per rank.",
            ["relentless-strikes"]="Finisher has a 4%-per-rank chance per spent combo point to restore 25 Energy.",
            ["venom"]="Landed Shank grants six seconds of 30% poison damage and 20 percentage points of poison application chance.",
            ["improved-kidney-shot"]="Own Kidney Shot stun increases attack and poison damage by 5/10%.",
            ["seal-fate"]="A generator critical hit has 33/66/100% chance for an extra combo point.",
            ["mutilate"]="Unlocks two-hand dagger generator using current 90% weapon coefficient and level-60 +79 per hand.",
            ["coup-de-grace"]="Eviscerate damage and Death Mark storage/payout increase by 7/13/20%.",
            ["efficient-strikes"]="Sinister Strike and Hemorrhage cost 3/5 less Energy.",
            ["lightning-reflexes"]="Adds one percentage point of physical dodge per rank.",
            ["puncturing-wounds"]="Backstab gains 10% crit and 15% extra-point chance per rank; Mutilate gains 5% crit per rank.",
            ["deflection"]="Adds two percentage points of parry per rank.",
            ["precision"]="Adds one percentage point of hit per rank.",
            ["improved-sprint"]="Sprint has 50/100% chance to remove all live root/snare movement-impairing auras.",
            ["endurance"]="Sprint and Evasion cooldowns reduce by 30/60%.",
            ["riposte"]="Parry opens a five-second opportunity; Riposte deals 150% weapon damage and disarms for six seconds.",
            ["composure"]="Poised reduces the next Energy cost by an additional 5/10.",
            ["improved-kick"]="Kick has 50/100% chance to silence for two seconds, canceling actual casts.",
            ["flawless-execution"]="Eviscerate costs 15 less Energy.",
            ["weapon-expertise"]="Reduces target dodge/parry by one percentage point per rank.",
            ["aggression"]="Sinister Strike and Eviscerate damage increase by 5% per rank.",
            ["dual-wield-specialization"]="Off-hand weapon damage increases 25% per rank.",
            ["blade-flurry"]="Grants 20% melee attack speed for 15 seconds and cleaves an actual additional enemy.",
            ["hack-and-slash"]="Actual hand type selects extra attacks, crit, or personal armor ignore.",
            ["flash-of-steel"]="Shank gains 3 Energy and loses 0.4 seconds of cooldown per rank.",
            ["adrenaline-rush"]="Doubles Energy regeneration for the active 15-second window.",
            ["setup"]="Dodge or full spell resist gives a 15%-per-rank chance of one point on that attacker.",
            ["master-of-deception"]="No executable stealth detection model yet.",
            ["opportunity"]="Backstab, Garrote, Ambush and Mutilate damage increase 4% per rank.",
            ["dirty-deeds"]="Cheap Shot/Garrote cost 10 less per rank; Garrote’s behind-target requirement is removed.",
            ["camouflage"]="No executable stealth movement or re-entry cooldown model yet.",
            ["improved-ambush"]="Ambush critical chance increases 15 percentage points per rank.",
            ["dirty-tricks"]="Sap and Blind Energy costs reduce 25/50%.",
            ["initiative"]="Ambush, Garrote and Cheap Shot grant an extra point with 33/67/100% probability.",
            ["premeditation"]="Grants two points that expire after 20 seconds unless added to or consumed.",
            ["serrated-blades"]="Personal armor ignore increases 3% and Rupture damage 10% per rank.",
            ["elusiveness"]="Vanish and Blind cooldowns reduce 45 seconds per rank.",
            ["ghostly-strike"]="Uses main-hand weapon damage, grants 15% dodge and one non-Stealth Ambush/Garrote within seven seconds.",
            ["quick-getaway"]="Allows one additional simultaneous Sap target at either rank. Post-Sap movement remains missing.",
            ["heightened-senses"]="Spell/ranged avoidance increases 2 percentage points per rank; stealth detection is missing.",
            ["preparation"]="Resets other learned Rogue ability cooldowns without resetting GCD, resources or itself.",
            ["hemorrhage"]="Deals sourced main-hand weapon damage and applies a 15% own Garrote/Rupture bonus for 15 seconds.",
            ["boot-knife"]="Shank has 20% chance per rank to grant two combo points.",
            ["death-mark"]="Stores 12% of own damage per spent point, doubles bleed storage, then releases after six seconds up to the sourced AP/crit cap.",
        };
    public static IReadOnlyDictionary<string,string> ScenarioConditions { get; } =
        new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
        {
            ["improved-gouge"]="Frontal, susceptible target; Gouge hit must land; later damage breaks control.",
            ["remorseless-attacks"]="Rogue kills an enemy within five levels in the explicit encounter convention; eligible follow-up strike.",
            ["malice"]="A modeled attack or poison with a critical roll.",
            ["ruthlessness"]="A landed finisher actually consumes target-owned combo points.",
            ["improved-expose-armor"]="A landed five-point Expose application for refunds; all Expose casts for cost.",
            ["improved-slice-and-dice"]="A landed Slice and Dice finisher; duration scales with spent points.",
            ["vigor"]="Precombat preparation and starting Energy percentage, once per isolated request.",
            ["blood-splatter"]="Successful bleed application; regular ticks include the extended duration.",
            ["lethality"]="A critical Sinister Strike, Gouge, Backstab, Mutilate, Ghostly Strike or Hemorrhage.",
            ["vile-poisons"]="Poison damage or a real TryDispel attempt against this Rogue’s live poison.",
            ["cold-blood"]="Sinister Strike, Backstab, Ambush, Eviscerate or Mutilate; one landed hit consumes it.",
            ["improved-poisons"]="Successful weapon strikes with nonempty prepared poison charges.",
            ["relentless-strikes"]="Successful finisher with spent points and an actual Energy deficit.",
            ["venom"]="Shank with Venom learned; live aura expiration governs subsequent poisons.",
            ["improved-kidney-shot"]="A susceptible target with this Rogue’s active Kidney Shot stun.",
            ["seal-fate"]="One attempt per generator execution; at most one extra point for two-hand Mutilate.",
            ["mutilate"]="Both actual hands are daggers; each hand rolls damage, while generator grants two points once.",
            ["coup-de-grace"]="Eviscerate or Death Mark is cast with real combo points.",
            ["efficient-strikes"]="Either learned, weapon-eligible action, combined with one-use Poised reduction.",
            ["lightning-reflexes"]="Incoming modeled physical attacks; no fabricated defensive events in a damage-only fight.",
            ["puncturing-wounds"]="Eligible Backstab or Mutilate; Backstab bonus point requires a landed hit.",
            ["deflection"]="A modeled incoming physical swing, after dodge resolution.",
            ["precision"]="Missable attacks and modeled poison spell rolls.",
            ["improved-sprint"]="Sprint activates while an aura tagged root, snare or movement-impairing is present.",
            ["endurance"]="Either cooldown is available in the learned action set.",
            ["riposte"]="A real incoming parry; landed Riposte; disarm affects modeled main-hand weapon attacks.",
            ["composure"]="Poised granted out of combat at full Energy; next Energy ability consumes it.",
            ["improved-kick"]="Kick against an enemy cast or subsequent spell cast during the live silence window.",
            ["flawless-execution"]="An Eviscerate finisher, combining with Poised if still available.",
            ["weapon-expertise"]="Explicit target avoidance; behind-target attacks already suppress parry.",
            ["aggression"]="Those two actions only; Backstab is excluded by current source.",
            ["dual-wield-specialization"]="Off-hand autos and special strikes; main-hand damage remains independent.",
            ["blade-flurry"]="Cooldown active; secondary damage requires another living enemy.",
            ["hack-and-slash"]="Sword/axe extra attack, dagger/fist crit, mace armor ignore; independently by hand.",
            ["flash-of-steel"]="A landed Shank and its subsequent cooldown/priority readiness.",
            ["adrenaline-rush"]="Cooldown active; old regeneration settles before changing rate on aura boundaries.",
            ["setup"]="A real incoming dodge or fully resisted spell; target must be a living enemy.",
            ["master-of-deception"]="Unimplemented; never labeled inactive solely because a stationary fight omits detection.",
            ["opportunity"]="Those eligible weapon/positional actions or their bleed ticks.",
            ["dirty-deeds"]="Either opener still needs Stealth or a sourced Ghostly permission for Garrote.",
            ["camouflage"]="Unimplemented secondary scenario; talent remains legal for editing/export.",
            ["improved-ambush"]="Main-hand dagger, behind target, Stealth or a live Ghostly opener permission.",
            ["dirty-tricks"]="Prepared utility actions; Sap retains its Stealth requirement.",
            ["initiative"]="Correlated successful opener execution/application; each cast attempts once.",
            ["premeditation"]="Stealth, learned ability and own target points; other-target gains do not preserve them.",
            ["serrated-blades"]="Physical non-bleed armor mitigation or own Rupture ticks.",
            ["elusiveness"]="Either cooldown; rank removal restores the original cooldown in a fresh request.",
            ["ghostly-strike"]="Landed strike; Ambush still requires a main-hand dagger and positioning.",
            ["quick-getaway"]="Successful Sap applications enforce the own-source target limit. Movement is a visible partial component.",
            ["heightened-senses"]="Actual incoming spell/ranged resolver for avoidance; detection remains partial.",
            ["preparation"]="An activated Preparation; per-actor abilities only, excluding consumables.",
            ["hemorrhage"]="Landed strike; dagger coefficient 145%, other hand types 100%; target-owned live mark.",
            ["boot-knife"]="One landed Shank execution, even if weapon damage is zero.",
            ["death-mark"]="Live target mark; removal cancels release; foreign damage is excluded; source composition remains provisional.",
        };

    /// <summary>Locally reviewed legal starter allocations, not popularity or optimality claims.
    /// Ranks are authored against the linked current tree; applying one changes talents only.</summary>
    public static ForeverRogueTalentBuild Starter(string tree) => FromRanks(tree switch
    {
        "assassination" => new Dictionary<string,int>
        {
            ["malice"]=5,["improved-gouge"]=3,["remorseless-attacks"]=2,["ruthlessness"]=3,
            ["improved-slice-and-dice"]=3,["vigor"]=2,["blood-splatter"]=1,["lethality"]=2,
            ["vile-poisons"]=5,["improved-poisons"]=2,["venom"]=1,["seal-fate"]=1,["mutilate"]=1,
            ["coup-de-grace"]=3,["efficient-strikes"]=2,["puncturing-wounds"]=3,["precision"]=3,
            ["endurance"]=2,["composure"]=2,["flawless-execution"]=1,["weapon-expertise"]=2,["aggression"]=2
        },
        "combat" => new Dictionary<string,int>
        {
            ["coup-de-grace"]=3,["efficient-strikes"]=2,["precision"]=3,["puncturing-wounds"]=3,
            ["composure"]=2,["endurance"]=2,["flawless-execution"]=1,["weapon-expertise"]=2,
            ["aggression"]=3,["dual-wield-specialization"]=2,["blade-flurry"]=1,["hack-and-slash"]=5,
            ["flash-of-steel"]=1,["adrenaline-rush"]=1,["malice"]=5,["ruthlessness"]=3,["improved-slice-and-dice"]=2,
            ["opportunity"]=5,["dirty-deeds"]=2,["improved-ambush"]=3
        },
        "subtlety" => new Dictionary<string,int>
        {
            ["opportunity"]=5,["dirty-deeds"]=2,["improved-ambush"]=3,["initiative"]=3,
            ["premeditation"]=1,["serrated-blades"]=3,["ghostly-strike"]=1,["elusiveness"]=2,
            ["preparation"]=1,["hemorrhage"]=1,["setup"]=3,["boot-knife"]=5,["death-mark"]=1,
            ["malice"]=5,["ruthlessness"]=3,["improved-slice-and-dice"]=2,
            ["coup-de-grace"]=3,["efficient-strikes"]=2,["puncturing-wounds"]=3,["precision"]=2
        },
        _ => throw new ArgumentException("Choose assassination, combat or subtlety starter talents.")
    });
    public static decimal CoupDeGraceFraction(int rank) => rank switch {1=>.07m,2=>.13m,3=>.20m,_=>0m};
    public static decimal EfficientStrikesReduction(int rank) => rank switch {1=>3m,2=>5m,_=>0m};
    public static decimal SealFatePercent(int rank) => rank switch {1=>33m,2=>66m,3=>100m,_=>0m};
}

public sealed record RogueClassTalentDefinition(string Key,string Tree,int MaxRank,
    int Row,int Column,string? ParentKey,int ParentRank)
{
    public string SourceConfidence => "reviewed-client-extraction-provisional-not-server-certified";
    public string SourceBuild => ForeverRogueClassTalents.SourceBuild;
    public string IconKey => ForeverRogueClassTalents.IconKeys[Key];
    public string SourceUrl => $"https://theforeverera.com/en/talents/rogue/{Key}/";
}
