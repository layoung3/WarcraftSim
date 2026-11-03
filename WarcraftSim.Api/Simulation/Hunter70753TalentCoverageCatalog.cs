namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Implementation-based Hunter 1.60.1.70753 talent census. SOURCE CONFIDENCE
/// (datamined/provisional) is deliberately separate from mechanical status.
/// The historical visual/reference file is not a source of runtime rules.
/// </summary>
public static class Hunter70753TalentCoverageCatalog
{
    public sealed record Talent(string Key,string Tree,int MaxRank,int Row,
        string SimulationStatus,string Gaps,string Limitations);
    public const string SourceBuild="1.60.1.70753";
    public const string SourceUrl="https://foreverchanges.pro/talents/hunter";
    public const string ReviewedDate="2026-11-03";
    public static IReadOnlyList<Talent> Talents {get;}=[
        new("deadly-aspects","beast-mastery",5,1,"fully-modeled","D","2%/rank chance of 30% auto-attack haste from eligible Aspect; cast/expiry implemented"),
        new("endurance-training","beast-mastery",5,1,"fully-modeled","D","3%/rank pet Health and Armor through shared actor factory"),
        new("focused-fire","beast-mastery",2,2,"fully-modeled","D","1%/rank owner and active-pet damage, conditional on active pet"),
        new("improved-aspect-of-the-monkey","beast-mastery",3,2,"partially-modeled","B,E","Hunter/pet dodge bonuses; constrained incoming-defense scenarios"),
        new("improved-revive-pet","beast-mastery",2,2,"partially-modeled","B,E","Mana/cast-time and revive execution; more pet-scaling recovery scenarios outstanding"),
        new("pathfinding","beast-mastery",2,3,"not-modeled","C","Cheetah/Pack travel movement not supported; moved to tier 3 in 70753"),
        new("bestial-swiftness","beast-mastery",1,3,"partially-modeled","C","30% pet movement represented by bounded engage time, not pathing"),
        new("unleashed-fury","beast-mastery",5,3,"fully-modeled","D","3%/rank damage on pet and eligible hawk actions"),
        new("improved-mend-pet","beast-mastery",2,4,"partially-modeled","B","10%/rank Mend Pet Mana reduction; 15%/rank dispel chance absent"),
        new("ferocity","beast-mastery",5,4,"fully-modeled","D","2%/rank pet and hawk critical-strike chance"),
        new("summon-hawk","beast-mastery",1,4,"fully-modeled","C,D","Owned hawk summons, damage, 18-second lifetime and shared Arcane cooldown"),
        new("spirit-bond","beast-mastery",2,5,"fully-modeled","D","Periodic owner/pet Health regeneration with active companion"),
        new("intimidation","beast-mastery",1,5,"partially-modeled","C","Pet next successful attack crit/threat; actual enemy AI stun absent"),
        new("bestial-discipline","beast-mastery",2,5,"fully-modeled","D","Pet Focus regeneration and owner casting Mana regeneration"),
        new("frenzy","beast-mastery",5,6,"fully-modeled","D","On-critical pet attack speed proc and timed expiration"),
        new("bestial-wrath","beast-mastery",1,7,"fully-modeled","C","Pet +50% damage for 18 sec; enemy fear/control assumptions excluded"),
        new("hawk-eye","marksmanship",3,1,"not-modeled","C","2 yards/rank ranged reach absent without spatial geometry"),
        new("improved-concussive-shot","marksmanship",5,1,"not-modeled","C","4%/rank Concussive Shot stun and enemy interruption absent"),
        new("lethal-attacks","marksmanship",5,1,"fully-modeled","","1%/rank all owner attack critical chance; pet crit inherits owner"),
        new("improved-stings","marksmanship",3,2,"partially-modeled","C","Serpent Sting modifier active; Viper/Scorpid utility not complete"),
        new("efficiency","marksmanship",5,2,"fully-modeled","D","3%/rank Shot/Sting Mana cost scaling; some cross-tree melee costs pending"),
        new("careful-aim","marksmanship",5,2,"fully-modeled","D","20%/rank Intellect contribution to owner Attack Power"),
        new("rapid-killing","marksmanship",2,3,"partially-modeled","C,E","Cooldown reduction and valid kill-triggered next-shot buff; raid kill attribution limited"),
        new("improved-arcane-shot","marksmanship",5,3,"fully-modeled","","0.3 sec/rank Arcane Shot independent cooldown reduction"),
        new("lone-wolf","marksmanship",1,3,"fully-modeled","C","20% owner attack damage only without active pet; includes before engage and after loss"),
        new("trueshot-aura","marksmanship",1,4,"partially-modeled","C,D","Rank-5 +100 owner live ranged AP through aura apply/remove and shared pet inheritance; party/raid distribution unmodeled"),
        new("mortal-shots","marksmanship",5,4,"fully-modeled","D","6%/rank ranged ability critical bonus"),
        new("rapid-recuperation","marksmanship",2,5,"fully-modeled","C","Conditional 15-second casting Mana regeneration after Serpent Sting"),
        new("barrage","marksmanship",3,5,"fully-modeled","","5/10/15% Aimed/Multi/Volley damage; excludes Sniper Shot"),
        new("scatter-shot","marksmanship",1,5,"partially-modeled","C","19503 50% ranged weapon damage, 8% base Mana, 30s cooldown, 4s damage-break disorient and Auto Shot stop execute; 15-yard geometry and enemy AI absent"),
        new("ranged-weapon-specialization","marksmanship",5,6,"fully-modeled","","1%/rank physical ranged weapon damage"),
        new("sniper-shot","marksmanship",1,7,"partially-modeled","C,D","4sec cast/15sec cooldown and rank-3 +590 damage execute; range charges not modeled"),
        new("improved-tracking","survival",5,1,"fully-modeled","C","1%/rank damage vs explicit tracked creature type"),
        new("deflection","survival",5,1,"partially-modeled","B","1%/rank parry bonus lacks complete incoming physical attacks"),
        new("entrapment","survival",5,2,"partially-modeled","C","Trap control marker, no enemy root/pathing"),
        new("savage-strikes","survival",2,2,"fully-modeled","","2%/rank melee ability crit chance"),
        new("survivalist","survival",5,2,"fully-modeled","","2%/rank maximum Health"),
        new("improved-wing-clip","survival",3,2,"partially-modeled","C","Wing Clip damage and root proc; no movement simulation"),
        new("clever-traps","survival",2,3,"fully-modeled","D","15%/rank Fire trap damage and Frost duration"),
        new("surefooted","survival",3,3,"partially-modeled","B,C","Hit chance modeled; movement-impair reduction absent"),
        new("deterrence","survival",1,3,"partially-modeled","B","Defensive aura and cooldown; enemy attacks constrained"),
        new("survival-tactics","survival",2,4,"partially-modeled","C","Trap hit effects modeled; Feign Death enemy threat/AI absent"),
        new("predators-edge","survival",5,4,"fully-modeled","D","6%/rank melee critical bonus and offhand damage"),
        new("counterattack","survival",1,4,"partially-modeled","C","Parry-reactive attack and damage execute; root enemy-control absent"),
        new("resourcefulness","survival",2,5,"fully-modeled","","30%/rank melee/trap Mana reduction and crit-gated regen"),
        new("expose-prey","survival",2,5,"fully-modeled","C","5%/rank marked-target proc opens Mongoose Bite"),
        new("survivalists-discipline","survival",2,5,"fully-modeled","","20%/rank trap and Deterrence cooldown reduction"),
        new("strider-kick","survival",1,5,"partially-modeled","C","Real melee strike; 30% movement speed buff not spatially modeled"),
        new("lightning-reflexes","survival",5,6,"partially-modeled","A,B,D","3/6/9/12/15% effective Agility reached AP; crit/dodge conversion needs client-derived coefficients"),
        new("lacerating-strikes","survival",1,7,"partially-modeled","A,D","Mongoose Bite bleed executes; verify weaker overwrite and current refresh priorities"),
    ];
    public static Talent? Find(string key)=>Talents.FirstOrDefault(x=>
        x.Key.Equals(key,StringComparison.OrdinalIgnoreCase));
    public static string ImplementationStatus(Talent? talent)=>talent?.SimulationStatus switch {
        "fully-modeled"=>"fully-implemented",
        "partially-modeled"=>"partially-implemented",
        _=>"not-implemented"
    };
}
