namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Conservative Warlock tree review (client 1.60.1.70753, reviewed 2026-11-03).
/// November 2 announced Wrack/Shadow-and-Flame changes may not appear in
/// the client tooltip; implemented as explicitly provisional announcements.
/// https://foreverchanges.pro/talents/warlock
/// </summary>
public static class Warlock70753TalentCoverageCatalog
{
    public sealed record Talent(string Key,string Tree,int MaxRank,int Row,
        string SimulationStatus,string Gaps,string Limitations);
    public const string SourceBuild="1.60.1.70753";
    public const string SourceUrl="https://foreverchanges.pro/talents/warlock";
    public const string ReviewedDate="2026-11-03";
    public static IReadOnlyList<Talent> Talents {get;}=[
        new("improved-life-tap","affliction",2,1,"fully-modeled","","Life Tap gain and rank-cost receipt"),
        new("suppression","affliction",5,1,"fully-modeled","","Spell hit and owner threat, not pet threat"),
        new("improved-corruption","affliction",5,1,"fully-modeled","","Corruption cast time and tick scaling"),
        new("grim-reach","affliction",2,2,"not-modeled","C","Spell range geometry absent"),
        new("soul-harvest","affliction",2,2,"partially-modeled","C,E","Kill-trigger regen executes; eligibility edge cases"),
        new("improved-drains","affliction",3,2,"fully-modeled","C,D","Ranked Drain Life/Soul damage and optional finite Drain Mana; target Mana and transfer scaling provisional"),
        new("improved-bane-of-agony","affliction",2,3,"fully-modeled","D","Agony tick damage and ramp; dynamic source provisional"),
        new("fel-concentration","affliction",3,3,"partially-modeled","B,E","Channel interruption/pushback modeled with encounter limitations"),
        new("amplify-curse","affliction",1,3,"partially-modeled","B,C","Agony amplification works, Weakness/Exhaustion absent"),
        new("pandemic","affliction",3,3,"fully-modeled","D","Ranked Affliction periodic crit bonus executes"),
        new("malevolence","affliction",5,4,"fully-modeled","","Shadow critical effect chance executes"),
        new("nightfall","affliction",2,4,"fully-modeled","","Source-specific damage procs and instant Shadow Bolt consumption"),
        new("curse-of-exhaustion","affliction",1,4,"not-modeled","C","No executable 30% movement slow; current rank selectable with warning"),
        new("siphon-life","affliction",1,5,"partially-modeled","B,E","Target-owned periodic damage and owner healing execute; edge context limited"),
        new("soul-siphon","affliction",3,5,"fully-modeled","","Target-affliction effect count, drain damage cap"),
        new("shadow-mastery","affliction",5,6,"fully-modeled","","Shadow damage and drain scaling"),
        new("wrack","affliction",1,7,"fully-modeled","D","Six channel ticks and DoT vulnerability; announced 86 + 0.286 SP per tick"),
        new("improved-health-funnel","demonology",2,1,"partially-modeled","B,E","Pet healing, owner cost/threat, incomplete emergency contexts"),
        new("improved-imp","demonology",3,1,"fully-modeled","C,D","Firebolt including SP and source-backed rank-5 Fire Shield retaliation; explicit physical incoming fixture, no raid-target selection"),
        new("demonic-embrace","demonology",5,1,"fully-modeled","D","Owner effective Stamina and health; demon inherits effective Stamina"),
        new("unholy-power","demonology",5,1,"fully-modeled","","Ranked pet damage for eligible permanent demons"),
        new("demonic-aegis","demonology",2,2,"not-modeled","B","Demon Armor/Skin effectiveness unimplemented"),
        new("improved-voidwalker","demonology",3,2,"partially-modeled","B,C","Torment/Suffering work; Consume Shadows/Sacrifice/AI incomplete"),
        new("fel-vitality","demonology",3,2,"fully-modeled","D","Owner Mana, eligible pet maximum Health/Mana rank scaling"),
        new("demonic-energies","demonology",2,2,"fully-modeled","","Actual spell-damage pet healing and Life Tap Mana"),
        new("improved-sayaad","demonology",3,3,"partially-modeled","C,E","Lash rank scales; Seduction/Soothing Kiss effects incomplete"),
        new("demonic-sacrifice","demonology",1,3,"fully-modeled","","Pre-pull family buffs and cancellation/extension"),
        new("master-summoner","demonology",2,3,"partially-modeled","C,E","Replacement summon cost/time modeled; general summoning absent"),
        new("decimation","demonology",2,4,"fully-modeled","","Execute-window Soul Fire cooldown, shard and cast effects"),
        new("fel-domination","demonology",1,4,"partially-modeled","C,E","Replacement summon discount modeled; broad summon unavailable"),
        new("demonic-brand","demonology",3,4,"partially-modeled","B,C","Source-owned Searing Pain pet attacks/threat; enemy targeting partial"),
        new("improved-felhunter","demonology",3,5,"partially-modeled","B,C","Devour healing/Spell Lock CD; Tainted Blood/Paranoia missing"),
        new("soul-link","demonology",1,5,"partially-modeled","B,C","Damage redirect and owner/pet damage; incoming scenarios limited"),
        new("demonic-knowledge","demonology",3,5,"fully-modeled","D","Active-pet owner and demon spell damage"),
        new("master-demonologist","demonology",5,6,"partially-modeled","B,C","Pet-family owner/pet offense/mitigation; full encounter partial"),
        new("demonic-pact","demonology",1,7,"fully-modeled","","Demonic Sacrifice continues after qualified replacement"),
        new("destructive-reach","destruction",2,1,"not-modeled","C","No spell range geometry"),
        new("improved-shadow-bolt","destruction",5,1,"fully-modeled","","Landed crit 12-second target-owned Shadow debuff"),
        new("bane","destruction",5,1,"fully-modeled","","Ranked cast-time reductions now include actual Immolate casts"),
        new("molten-skin","destruction",5,2,"fully-modeled","B,E","Rank-based incoming mitigation for scripted encounters"),
        new("cataclysm","destruction",3,2,"fully-modeled","","Ranked Destruction Mana reduction"),
        new("aftermath","destruction",5,2,"partially-modeled","C","Immolate impact bonus and landed Conflagrate 5s daze proc marker; no movement AI"),
        new("ruin","destruction",5,3,"fully-modeled","","Destruction critical bonus scaling"),
        new("shadowburn","destruction",1,3,"fully-modeled","","Actual Soul Shard cost and 8-second kill refund"),
        new("intensity","destruction",3,4,"partially-modeled","B,E","Pushback protection in supported incoming attacks"),
        new("agonizing-flames","destruction",3,4,"fully-modeled","","Searing Pain crit and Destruction damage"),
        new("conflagrate","destruction",1,4,"fully-modeled","","Talent gating, landed hit and target-owner Immolate consumption"),
        new("pyroclasm","destruction",2,5,"partially-modeled","C,D","Soul Fire and per-channel AoE stun proc auras execute when CC permitted; enemy interruption and movement absent"),
        new("bane-of-havoc","destruction",1,5,"fully-modeled","C","15% explicit other-target damage echo, no spatial geometry"),
        new("fire-and-brimstone","destruction",3,5,"fully-modeled","","Conflagrate crit chance per rank"),
        new("shadow-and-flame","destruction",5,6,"fully-modeled","D","Announced 4%/rank school buffs, landed aura, retention/refund"),
        new("incinerate","destruction",1,7,"fully-modeled","","Learned Incinerate and own-Immolate 25% extra damage"),
    ];
    public static Talent? Find(string key)=>Talents.FirstOrDefault(t=>
        t.Key.Equals(key,StringComparison.OrdinalIgnoreCase));
    public static string ImplementationStatus(Talent? t)=>t?.SimulationStatus switch {
        "fully-modeled"=>"fully-implemented",
        "partially-modeled"=>"partially-implemented",
        _=>"not-implemented"
    };
}
