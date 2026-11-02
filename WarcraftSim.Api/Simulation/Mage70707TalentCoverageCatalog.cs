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
        new("arcane-subtlety","arcane",2,"not-modeled","Arcane threat/resistance reduction not modeled"),
        new("magic-absorption","arcane",2,"partially-modeled","Resistance/Mana refund on explicit full resist; general resist distribution unresolved"),
        new("arcane-concentration","arcane",5,"partially-modeled","Clearcasting proc and consumption execute; low-rank and out-of-combat exclusions not universally certified"),
        new("missile-barrage","arcane",2,"partially-modeled","Proc executes; Arcane Missiles channel-hit trigger and modern 70707 missile cadence require reconciliation"),
        new("arcane-geometry","arcane",2,"not-modeled","Spell range geometry not modeled"),
        new("arcane-impact","arcane",3,"fully-modeled","Arcane spell critical chance rank-scaled"),
        new("arcane-blast","arcane",1,"fully-modeled","70707 provisional additive cost/SP stacks and 18-second timer; spell rank/coefficient provisional"),
        new("arcane-shielding","arcane",2,"partially-modeled","Mage Armor resistance increases; Mana Shield efficiency not modeled"),
        new("improved-counterspell","arcane",2,"not-modeled","Enemy silence/interrupt not modeled"),
        new("arcane-meditation","arcane",3,"fully-modeled","Rank-scaled casting regeneration with corrected prerequisite"),
        new("arcane-wrath","arcane",1,"partially-modeled","Damage/Mana/cast execute; projectile and source rank validation pending"),
        new("presence-of-mind","arcane",1,"fully-modeled","Selected cooldown can make next supported spell instant"),
        new("arcane-mind","arcane",5,"fully-modeled","Intellect and Arcane crit-damage scaling execute"),
        new("arcane-instability","arcane",3,"fully-modeled","Ranked spell damage and critical chance execute"),
        new("arcane-power","arcane",1,"fully-modeled","Opt-in damage/Mana cooldown executes"),
        new("incineration","fire",5,"fully-modeled","Named spell critical chance rank-scaled"),
        new("accelerant","fire",5,"fully-modeled","Cast time for Fireball/Frostfire/Arcane Wrath rank-scaled"),
        new("ignite","fire",5,"partially-modeled","Source-owned rolling fire critical DoT executes; precise rollover still provisional"),
        new("flame-throwing","fire",2,"not-modeled","Fire range increase not modeled"),
        new("impact","fire",3,"not-modeled","Stun and low-rank proc penalties not modeled"),
        new("burning-soul","fire",3,"partially-modeled","Fire pushback and threat implemented; source spell eligibility still needs certification"),
        new("improved-flamestrike","fire",3,"partially-modeled","Damage/crit effects execute; real AoE geometry absent"),
        new("pyroblast","fire",1,"partially-modeled","Talent-gated direct/periodic damage; current-rank client details provisional"),
        new("improved-fire-blast","fire",2,"partially-modeled","Cooldown reduction executes; added range is unsupported"),
        new("improved-scorch","fire",3,"fully-modeled","Landed Scorch vulnerability per target and stacks execute"),
        new("improved-fire-ward","fire",2,"not-modeled","Fire Ward mechanics not modeled"),
        new("heating-up","fire",1,"partially-modeled","Crit stacks/Pyro cast execute; precise Fire Blast automatic stack rules not certified"),
        new("master-of-elements","fire",3,"fully-modeled","Fire/Frost critical base mana refunds execute"),
        new("critical-mass","fire",3,"fully-modeled","Fire critical chance rank-scaled"),
        new("blast-wave","fire",1,"partially-modeled","AoE damage executes; Daze/geometry unsupported"),
        new("fire-power","fire",3,"partially-modeled","Fire effect count/damage executes; published eligible-effect list needs correction"),
        new("combustion","fire",1,"partially-modeled","Critical stack/consumption executes; rank/proc edge cases remain"),
        new("frost-warding","frost",2,"partially-modeled","Ice Barrier absorb increases; ward negation/armor contributions incomplete"),
        new("improved-frostbolt","frost",5,"fully-modeled","Frostbolt cast-time decrease executes"),
        new("precision","frost",3,"fully-modeled","Hit across every spell school rank-scaled"),
        new("ice-shards","frost",5,"fully-modeled","Frost critical damage bonus executes"),
        new("frostbite","frost",3,"not-modeled","Freeze proc and low-rank penalties not modeled"),
        new("improved-frost-nova","frost",2,"not-modeled","Nova cooldown/range mechanic not modeled"),
        new("permafrost","frost",3,"partially-modeled","Chill duration extension executes; movement slow cannot affect stationary target"),
        new("frost-channeling","frost",3,"fully-modeled","Frost cost and threat modifiers execute"),
        new("improved-blizzard","frost",3,"partially-modeled","Slow aura emits; moving targets and control not represented"),
        new("ice-lance","frost",1,"partially-modeled","FoF Frozen multiplier exists; 70707 300% increased bonus and shared Fire Blast cooldown not corrected yet"),
        new("arctic-reach","frost",2,"not-modeled","Range geometry absent"),
        new("ice-block","frost",1,"partially-modeled","Invulnerability/lockout scripted; other control conditions not fully certified"),
        new("shatter","frost",3,"partially-modeled","Frozen critical chance executes for FoF; external Frozen sources limited"),
        new("improved-cone-of-cold","frost",3,"partially-modeled","Frost Cone damage scaling executes; geometric coverage limited"),
        new("cold-snap","frost",1,"partially-modeled","Existing cooldown resets; not all current Frost abilities included"),
        new("fingers-of-frost","frost",2,"partially-modeled","Chill-triggered charges execute; Armor-triggered procs unavailable"),
        new("winters-chill","frost",5,"partially-modeled","Target-specific stack/Cone interaction executes; 70707 20% damage-taken per stack not reconciled"),
        new("ice-barrier","frost",1,"partially-modeled","Absorb and Frost bonus execute; 70707 revised Frost damage values pending"),
    ];
    public static Talent? Find(string key)=>Talents.FirstOrDefault(x=>
        x.Key.Equals(key,StringComparison.OrdinalIgnoreCase));
    public static string ImplementationStatus(Talent? talent)=>talent?.SimulationStatus switch {
        "fully-modeled"=>"fully-implemented",
        "partially-modeled"=>"partially-implemented",
        _=>"not-implemented"
    };
}
