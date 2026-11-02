namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Conservative engine coverage inventory; 70707 source/ranks provisional.
/// Metadata is not evidence of combat-executed mechanics.
/// https://wowfdb.com/talents/mage/ (reviewed 2026-11-02).
/// </summary>
public static class Mage70707TalentCoverageCatalog
{
    public sealed record Talent(string Key,string Tree,int MaxRank,string SimulationStatus,string RemainingLimitations);
    public const string SourceBuild="1.60.1.70707";
    public const string SourceUrl="https://wowfdb.com/talents/mage/";
    public const string ReviewedDate="2026-11-02";
    public static IReadOnlyList<Talent> Talents {get;}=[
        new("wand-specialization","arcane",2,"not-modeled","Wand auto-attack bonus absent"),
        new("improved-channeling","arcane",5,"partially-modeled","Pushback mechanics present; outgoing threat/complete interrupt matrix unverified"),
        new("arcane-resilience","arcane",3,"partially-modeled","Armor scales by 25% Intellect per rank in preview and combat; incoming physical mitigation not generally certified"),
        new("arcane-subtlety","arcane",2,"partially-modeled","Arcane spell threat reduced by 15/30%; resistance reduction unmodeled"),
        new("magic-absorption","arcane",2,"partially-modeled","Resistance/Mana refund on explicit full resist; general resist distribution unresolved"),
        new("arcane-concentration","arcane",5,"partially-modeled","Clearcasting proc and consumption execute; low-rank and out-of-combat exclusions not universally certified"),
        new("missile-barrage","arcane",2,"partially-modeled","Proc executes; Arcane Missiles channel-hit trigger and modern 70707 missile cadence require reconciliation"),
        new("arcane-geometry","arcane",2,"not-modeled","Spell range geometry not modeled"),
        new("arcane-impact","arcane",3,"fully-modeled","Arcane spell critical chance rank-scaled"),
        new("arcane-blast","arcane",1,"fully-modeled","70707 provisional additive cost/SP stacks and 18-second timer; spell rank/coefficient provisional"),
        new("arcane-shielding","arcane",2,"partially-modeled","Mage Armor resistance and scripted physical Mana Shield efficiency execute; full encounter and manual recast remain unsupported"),
        new("improved-counterspell","arcane",2,"not-modeled","Enemy silence/interrupt not modeled"),
        new("arcane-meditation","arcane",3,"fully-modeled","Rank-scaled casting regeneration with corrected prerequisite"),
        new("arcane-wrath","arcane",1,"partially-modeled","Damage/Mana/cast execute; projectile and source rank validation pending"),
        new("presence-of-mind","arcane",1,"fully-modeled","Selected cooldown can make next supported spell instant"),
        new("arcane-mind","arcane",5,"fully-modeled","Intellect and Arcane crit-damage scaling execute"),
        new("arcane-instability","arcane",3,"fully-modeled","Ranked spell damage and critical chance execute"),
        new("arcane-power","arcane",1,"fully-modeled","Opt-in damage/Mana cooldown executes"),
        new("incineration","fire",5,"fully-modeled","Named spell critical chance rank-scaled"),
        new("accelerant","fire",5,"fully-modeled","Cast time for Fireball/Frostfire/Arcane Wrath rank-scaled"),
        new("ignite","fire",5,"partially-modeled","Rank-scaled owner-specific 4s rolling pool and no double-dip execute; periodic criticals cannot independently trigger it; precise live rollover remains provisional"),
        new("flame-throwing","fire",2,"not-modeled","Fire range increase not modeled"),
        new("impact","fire",3,"partially-modeled","Sourced 3/7/10% direct-Fire hit proc emits target-owned 2s stun aura; movement/AI control and low-rank suppression unavailable"),
        new("burning-soul","fire",3,"partially-modeled","Fire pushback and threat implemented; source spell eligibility still needs certification"),
        new("improved-flamestrike","fire",3,"partially-modeled","Damage/crit effects execute; real AoE geometry absent"),
        new("pyroblast","fire",1,"partially-modeled","Talent-gated direct/periodic damage; current-rank client details provisional"),
        new("improved-fire-blast","fire",2,"partially-modeled","Cooldown reduction executes; added range is unsupported"),
        new("improved-scorch","fire",3,"fully-modeled","Landed Scorch vulnerability per target and stacks execute"),
        new("improved-fire-ward","fire",2,"not-modeled","Fire Ward mechanics not modeled"),
        new("heating-up","fire",1,"partially-modeled","Eligible landed crit and landed Fire Blast extra stack now emit observable 20s aura with Pyroblast consumption; source rank/proc edge cases remain provisional"),
        new("master-of-elements","fire",3,"fully-modeled","Fire/Frost critical base mana refunds execute"),
        new("critical-mass","fire",3,"fully-modeled","Fire critical chance rank-scaled"),
        new("blast-wave","fire",1,"partially-modeled","AoE damage executes; Daze/geometry unsupported"),
        new("fire-power","fire",3,"partially-modeled","70707 self-owned Ignite/Scorch/Fireball/Pyroblast/Impact effect count and Frostfire benefit execute; external resistance/extra encounter mechanics provisional"),
        new("combustion","fire",1,"partially-modeled","Actual direct landed Fire hit/crit events raise crit chance until 3 crits remove 180s cooldown buff; resisted and periodic hits excluded; multi-target nuances provisional"),
        new("frost-warding","frost",2,"partially-modeled","Ice Barrier absorb increases; ward negation/armor contributions incomplete"),
        new("improved-frostbolt","frost",5,"fully-modeled","Frostbolt cast-time decrease executes"),
        new("precision","frost",3,"fully-modeled","Hit across every spell school rank-scaled"),
        new("ice-shards","frost",5,"fully-modeled","Frost critical damage bonus executes"),
        new("frostbite","frost",3,"partially-modeled","Rank 5/10/15% direct Chill proc applies owner-targeted 5s Frozen aura and Shatter/Ice Lance effects; enemy control and low spell-rank suppression unavailable"),
        new("improved-frost-nova","frost",2,"fully-modeled","Frost Nova actual cooldown reduced 2/4 seconds by rank; target geometry and freeze baseline are not talent effects"),
        new("permafrost","frost",3,"partially-modeled","Chill duration extension executes; movement slow cannot affect stationary target"),
        new("frost-channeling","frost",3,"fully-modeled","Frost cost and threat modifiers execute"),
        new("improved-blizzard","frost",3,"partially-modeled","Slow aura emits; moving targets and control not represented"),
        new("ice-lance","frost",1,"partially-modeled","70707 +300% vs Frozen (4x) and shared 8s Fire Blast lockout execute; external Frozen states and Arctic Reach range partial"),
        new("arctic-reach","frost",2,"not-modeled","Range geometry absent"),
        new("ice-block","frost",1,"partially-modeled","Invulnerability/lockout scripted; other control conditions not fully certified"),
        new("shatter","frost",3,"partially-modeled","Frozen critical chance executes for FoF; external Frozen sources limited"),
        new("improved-cone-of-cold","frost",3,"partially-modeled","Frost Cone damage scaling executes; geometric coverage limited"),
        new("cold-snap","frost",1,"partially-modeled","Existing cooldown resets; not all current Frost abilities included"),
        new("fingers-of-frost","frost",2,"partially-modeled","Sourced 15s aura and 1/2 rank charges are granted/consumed from actual qualifying Chill hits; Ice/Frost Armor triggers unavailable"),
        new("winters-chill","frost",5,"partially-modeled","Target-owned stacks and 70707 +20% next Cone damage per stack execute; external immunity and source rank validation provisional"),
        new("ice-barrier","frost",1,"partially-modeled","70707 819+10% Frost SP shield, +15% Frost damage and +5% while shielded execute; pushback/immunity and wider encounter partial"),
    ];
    public static Talent? Find(string key)=>Talents.FirstOrDefault(x=>
        x.Key.Equals(key,StringComparison.OrdinalIgnoreCase));
    public static string ImplementationStatus(Talent? talent)=>talent?.SimulationStatus switch {
        "fully-modeled"=>"fully-implemented",
        "partially-modeled"=>"partially-implemented",
        _=>"not-implemented"
    };
}
