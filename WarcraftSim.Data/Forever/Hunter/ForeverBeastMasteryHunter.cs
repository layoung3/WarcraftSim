using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Data.Forever.Hunter;

/// <summary>Forever 70425 Beast Mastery tree, including relevant Marksmanship off-tree ranks.</summary>
public sealed class ForeverBeastMasteryHunterTalents
{
    public Dictionary<string, int> Ranks { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["deadly-aspects"] = 5, ["endurance-training"] = 5,
        ["focused-fire"] = 2, ["improved-aspect-of-the-monkey"] = 3,
        ["pathfinding"] = 2, ["improved-revive-pet"] = 2,
        ["bestial-swiftness"] = 1, ["unleashed-fury"] = 5,
        ["improved-mend-pet"] = 2, ["ferocity"] = 5,
        ["summon-hawk"] = 1, ["spirit-bond"] = 2,
        ["intimidation"] = 1, ["bestial-discipline"] = 2,
        ["frenzy"] = 5, ["bestial-wrath"] = 1,
        ["lethal-attacks"] = 5, ["improved-concussive-shot"] = 2
    };
    private static readonly Dictionary<string, (int Cap, int Tier)> Caps =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["deadly-aspects"] = (5,0), ["endurance-training"]=(5,0),
            ["focused-fire"]=(2,5), ["improved-aspect-of-the-monkey"]=(3,5),
            ["pathfinding"]=(2,5), ["improved-revive-pet"]=(2,5),
            ["bestial-swiftness"]=(1,10), ["unleashed-fury"]=(5,10),
            ["improved-mend-pet"]=(2,15), ["ferocity"]=(5,15),
            ["summon-hawk"]=(1,15), ["spirit-bond"]=(2,20),
            ["intimidation"]=(1,20), ["bestial-discipline"]=(2,20),
            ["frenzy"]=(5,25), ["bestial-wrath"]=(1,30),
            ["lethal-attacks"]=(5,-1), ["improved-concussive-shot"]=(5,-1)
        };
    public int Rank(string key) => Ranks.GetValueOrDefault(key);
    public void Validate()
    {
        if (Ranks is null || Ranks.Values.Sum() > 51) throw new ArgumentException("Hunter talent budget exceeds 51.");
        foreach(var (key, rank) in Ranks)
            if(!Caps.TryGetValue(key,out var entry) || rank<0 || rank>entry.Cap)
                throw new ArgumentException($"Unknown or out-of-range Beast Mastery talent {key}={rank}.");
        foreach(var (key, entry) in Caps.Where(x => x.Value.Tier >= 0))
        {
            if (Rank(key)==0) continue;
            var preceding = Caps.Where(x=>x.Value.Tier >=0 && x.Value.Tier<entry.Tier)
                .Sum(x=>Rank(x.Key));
            if(preceding<entry.Tier)
                throw new ArgumentException($"Beast Mastery talent {key} requires {entry.Tier} earlier-row points.");
        }
        if(Rank("frenzy")>0 && Rank("ferocity")<5 ||
           Rank("intimidation")>0 && Rank("bestial-swiftness")==0)
            throw new ArgumentException("Beast Mastery talent prerequisites not met.");
    }
}

/// <summary>Confirmed 70450 family bonuses; family ability damage and swing speed
/// are editable preview numbers. Core Hound appears in latest client catalog,
/// but its tameability is not confirmed.</summary>
public sealed record ForeverHunterPetFamily(string Key, decimal DamageModifier,
    decimal ArmorModifier, decimal HealthModifier, string SpecialAbility,
    bool HasClaw, bool HasBite);

public static class ForeverHunterPetFamilies
{
    private static readonly IReadOnlyDictionary<string,ForeverHunterPetFamily> Catalog =
        new Dictionary<string,ForeverHunterPetFamily>(StringComparer.OrdinalIgnoreCase)
        {
            ["bat"] = new("bat",.07m,0m,0m,"sonic-blast",false,true),
            ["bear"] = new("bear",-.09m,.05m,.08m,"swipe",true,true),
            ["bird-of-prey"] = new("bird-of-prey",.07m,0m,0m,"mine",true,false),
            ["boar"] = new("boar",-.10m,.09m,.04m,"charge",false,true),
            ["carrion-bird"] = new("carrion-bird",0m,.05m,0m,"demoralizing-screech",true,true),
            ["cat"] = new("cat",.10m,0m,-.02m,"prowl",true,true),
            ["crab"] = new("crab",-.05m,.13m,-.04m,"pinch",true,false),
            ["crocolisk"] = new("crocolisk",0m,.10m,-.05m,"dismember",false,true),
            ["fox"] = new("fox",0m,.05m,0m,"tricksters-dance",false,true),
            ["gorilla"] = new("gorilla",.02m,0m,.04m,"thunderstomp",false,true),
            ["hyena"] = new("hyena",0m,.05m,0m,"tendon-rip",false,true),
            ["raptor"] = new("raptor",.10m,.03m,-.05m,"savage-rend",true,true),
            ["scorpid"] = new("scorpid",-.06m,.10m,0m,"scorpid-poison",true,false),
            ["spider"] = new("spider",.07m,0m,0m,"web",false,true),
            ["tallstrider"] = new("tallstrider",0m,0m,.05m,"dust-cloud",false,true),
            ["turtle"] = new("turtle",-.10m,.13m,0m,"shell-shield",false,true),
            ["wind-serpent"] = new("wind-serpent",.07m,0m,0m,"lightning-breath",false,true),
            ["wolf"] = new("wolf",0m,.05m,0m,"furious-howl",false,true),
            ["core-hound"] = new("core-hound",.01m,0m,.05m,"lava-breath",false,true)
        };
    public static IReadOnlyCollection<ForeverHunterPetFamily> All => Catalog.Values.ToArray();
    public static ForeverHunterPetFamily Get(string key) => Catalog.TryGetValue(key,out var value)
        ? value : throw new ArgumentException($"Unknown Forever pet family '{key}'.");
}

public sealed class ForeverBeastMasteryHunterTuning
{
    public decimal HunterHealth { get; set; }=3900m;
    public decimal HunterMana { get; set; }=4200m;
    public decimal HunterStamina { get; set; }=180m;
    public decimal HunterArmor { get; set; }=2400m;
    public decimal RangedAttackPower { get; set; }=1000m;
    public decimal MeleeAttackPower { get; set; }=950m;
    public decimal HunterHitPercent { get; set; }=88m;
    public decimal HunterCriticalPercent { get; set; }=18m;
    public decimal HunterMeleeCriticalPercent { get; set; }=18m;
    public decimal HunterSpiritRegenPerSecond { get; set; }=10m;
    public decimal HunterMp5 { get; set; }=20m;
    public decimal TargetArmor { get; set; }=3800m;
    public decimal HunterWeaponMin { get; set; }=240m;
    public decimal HunterWeaponMax { get; set; }=360m;
    public decimal HunterWeaponSpeed { get; set; }=2.8m;
    public decimal PetBaseHealth { get; set; }=1800m;
    public decimal PetBaseArmor { get; set; }=1200m;
    public decimal PetBaseAttackPower { get; set; }=310m;
    public decimal PetDamageMin { get; set; }=72m;
    public decimal PetDamageMax { get; set; }=108m;
    public decimal PetSwingSeconds { get; set; }=2m;
    public decimal PetHitPercent { get; set; }=89m;
    public decimal PetAvoidancePercent { get; set; }=5m;
    public decimal PetCriticalBonusPercent { get; set; }=0m;
    public decimal PetFocusPerSecond { get; set; }=5m;
    public decimal PetOwnerAttackPowerInheritance { get; set; }=.10m;
    public decimal PetOwnerArmorInheritance { get; set; }=.30m;
    public decimal PetHealthPerOwnerStamina { get; set; }=2m;
    public decimal PetBiteMin { get; set; }=81m;
    public decimal PetBiteMax { get; set; }=99m;
    public decimal PetClawMin { get; set; }=43m;
    public decimal PetClawMax { get; set; }=59m;
    // Provisional coefficient for family abilities without verified rank-60 damage.
    public decimal FamilySpecialDamage { get; set; }=125m;
    // Rank 4 level-60 Furious Howl currently lists +82 melee AP. Remain tunable
    // while WoW Forever client coefficients and rank availability evolve.
    public decimal WolfHowlMeleeAttackPowerBonus { get; set; }=82m;
    public decimal FamilySpecialFocusCost { get; set; }=30m;
    public decimal FamilySpecialCooldown { get; set; }=15m;
    // Provisional beta rank coefficients; configurable until independently verified.
    public decimal PetBleedTickDamage { get; set; }=30m;
    public decimal PetPoisonTickDamage { get; set; }=12m;
    public decimal HawkAttackDamage { get; set; }=75m;
    public decimal HawkAttackInterval { get; set; }=2m;
    public decimal BestialWrathCooldownSeconds { get; set; }=120m;
    public void Validate()
    {
        if (HunterHealth<=0 || HunterMana<=0 || HunterWeaponSpeed<=0 || PetSwingSeconds<=0 ||
            PetFocusPerSecond<0 || PetBaseHealth<=0 || PetDamageMax<PetDamageMin ||
            HunterWeaponMax<HunterWeaponMin || PetHitPercent is <0 or >100 ||
            HunterHitPercent is <0 or >100 || HunterCriticalPercent is <0 or >100 ||
            HunterMeleeCriticalPercent is <0 or >100 ||
            PetAvoidancePercent is <0 or >100 || FamilySpecialCooldown<=0 ||
            HawkAttackInterval<=0 || BestialWrathCooldownSeconds<=0 ||
            GetType().GetProperties().Where(x=>x.PropertyType==typeof(decimal))
                .Any(x=>((decimal)x.GetValue(this)!) < 0m))
            throw new ArgumentException("Invalid Beast Mastery combat tuning values.");
    }
}

public static class ForeverBeastMasteryHunterSpellbook
{
    private static AbilityEffectDefinition Damage(string key, string school, decimal min, decimal max,
        int targets = 1) => new() {
        Key = key, EffectType=AbilityEffectTypes.DirectDamage, TargetType=AbilityTargetTypes.Enemy,
        SchoolKey=school, MitigationType=school=="physical" ? DamageMitigationTypes.Armor : DamageMitigationTypes.None,
        ResolutionType=CombatResolutionTypes.Melee, CanMiss=true, CanCrit=true,
        MinimumValue=min,MaximumValue=max,MaxTargets=targets
    };
    private static AbilityEffectDefinition Periodic(string key,string school,decimal perTick,decimal duration,decimal interval) => new() {
        Key=key,EffectType=AbilityEffectTypes.PeriodicDamage,TargetType=AbilityTargetTypes.Enemy,
        SchoolKey=school,MitigationType=DamageMitigationTypes.None,
        // Stage 3 resolves harmful DoT applications exactly once. A missed
        // Rend/Tendon Rip/Scorpid Poison/Web must not apply guaranteed ticks.
        // Existing periodic ticks inherit a landed application, never reroll hit.
        ResolutionType=school=="physical" ? CombatResolutionTypes.Melee : CombatResolutionTypes.Spell,
        CanMiss=true,CanCrit=false,
        AuraKey=key,DurationSeconds=duration,TickIntervalSeconds=interval,IncludeExpirationBoundaryTick=true,
        MinimumValue=perTick,MaximumValue=perTick
    };
    private static AbilityDefinition Action(string key, string owner, string resource,
        decimal cost, decimal cooldown, params AbilityEffectDefinition[] effects) => new() {
        Key=key,Name=key,ClassKey=owner,CooldownSeconds=cooldown,
        ResourceCosts=[new AbilityResourceCost {ResourceKey=resource,Amount=cost}],
        Effects=effects.ToList()
    };
    public static IReadOnlyList<AbilityDefinition> Hunter(ForeverBeastMasteryHunterTalents talent,
        ForeverBeastMasteryHunterTuning tuning)
    {
        talent.Validate();tuning.Validate();
        // Retain the weapon's actual damage roll rather than collapsing each
        // shot to a constant average. Both special shots inherit the same AP.
        decimal weaponAp=tuning.RangedAttackPower*tuning.HunterWeaponSpeed/14m;
        var aimed=Action("aimed-shot","hunter","mana",310m,6m,
            Damage("aimed-impact","physical",
                tuning.HunterWeaponMin+weaponAp+166m,
                tuning.HunterWeaponMax+weaponAp+166m));
        aimed.CastTimeSeconds=2m;
        aimed.SharedCooldownGroupKey="hunter-aimed-multi";
        aimed.SharedCooldownSeconds=6m;
        var multi=Action("multi-shot","hunter","mana",275m,6m,
            Damage("multi-impact","physical",
                tuning.HunterWeaponMin+weaponAp+110m,
                tuning.HunterWeaponMax+weaponAp+110m,3));
        multi.SharedCooldownGroupKey="hunter-aimed-multi";
        multi.SharedCooldownSeconds=6m;
        var arcane=Action("arcane-shot","hunter","mana",230m,6m,
            Damage("arcane-impact","arcane",205m,205m));
        arcane.SharedCooldownGroupKey="hunter-arcane-hawk";
        arcane.SharedCooldownSeconds=6m;
        var list=new List<AbilityDefinition>{ForeverHunterSharedAbilities.CreateRapidFire(),aimed,multi,arcane,
            Action("serpent-sting","hunter","mana",220m,0m,new AbilityEffectDefinition{
                Key="serpent-tick",EffectType=AbilityEffectTypes.PeriodicDamage,
                TargetType=AbilityTargetTypes.Enemy,SchoolKey="nature",
                AuraKey="serpent-sting",DurationSeconds=15m,TickIntervalSeconds=3m,
                IncludeExpirationBoundaryTick=true,MinimumValue=72m,MaximumValue=72m,
                CanCrit=true,CanMiss=false})};
        if(talent.Rank("summon-hawk")>0) {
            var hawk=Action("summon-hawk","hunter","mana",190m,6m);
            hawk.SharedCooldownGroupKey="hunter-arcane-hawk";
            hawk.SharedCooldownSeconds=6m;
            list.Add(hawk);
        }
        if(talent.Rank("bestial-wrath")>0){
            var wrath=Action("bestial-wrath","hunter","mana",0m,tuning.BestialWrathCooldownSeconds);
            wrath.RequiredSourceAuraKeys.Add("hunter-pet-active");
            list.Add(wrath);
        }
        if(talent.Rank("intimidation")>0){
            var stun=Action("intimidation","hunter","mana",0m,60m);
            stun.RequiredSourceAuraKeys.Add("hunter-pet-active");
            list.Add(stun);
        }
        var mend=Action("mend-pet","hunter","mana",
            210m*(1m-.10m*talent.Rank("improved-mend-pet")),10m);
        mend.RequiredSourceAuraKeys.Add("pet-needs-mend");
        list.Add(mend);
        var revive=Action("revive-pet","hunter","mana",
            500m*(1m-.20m*talent.Rank("improved-revive-pet")),0m);
        revive.CastTimeSeconds=Math.Max(0m,10m-3m*talent.Rank("improved-revive-pet"));
        revive.RequiredSourceAuraKeys.Add("pet-needs-revive");
        list.Add(revive);
        return list;
    }
    public static IReadOnlyList<AbilityDefinition> Pet(ForeverHunterPetFamily family,
        ForeverBeastMasteryHunterTuning tuning)
    {
        var list=new List<AbilityDefinition>();
        if(family.HasBite)list.Add(Action("pet-bite","pet","focus",35m,10m,
            Damage("pet-bite-impact","physical",tuning.PetBiteMin,tuning.PetBiteMax)));
        if(family.HasClaw)list.Add(Action("pet-claw","pet","focus",25m,0m,
            Damage("pet-claw-impact","physical",tuning.PetClawMin,tuning.PetClawMax)));
        var school=family.SpecialAbility is "lightning-breath" or "sonic-blast" or "thunderstomp"
            ? "nature" : family.SpecialAbility=="lava-breath" ? "fire" : "physical";
        var targets=family.SpecialAbility=="thunderstomp" ? 9 : family.SpecialAbility=="swipe" ? 3 : 1;
        // Pure utility families still cast the signature ability; only offensive
        // signatures have initial damage. Their effects are handled by the pet processor.
        var utility=family.SpecialAbility is "prowl" or "furious-howl" or "shell-shield" or
            "dust-cloud" or "tricksters-dance" or "demoralizing-screech";
        // Forever-confirmed signature mechanics override the provisional shared
        // cost/cooldown. Other abilities stay editable until verified at rank 60.
        var focusCost=family.SpecialAbility switch {
            "tricksters-dance" => 10m, "sonic-blast" => 80m,
            "charge" => 35m, "swipe" => 20m,
            "furious-howl" => 60m, "lightning-breath" => 50m,
            "prowl" => 40m, "thunderstomp" => 60m,
            _ => tuning.FamilySpecialFocusCost
        };
        var cooldown=family.SpecialAbility switch {
            "tricksters-dance" => 180m, "sonic-blast" => 60m,
            "mine" => 90m, "dismember" => 15m, "charge" => 25m,
            "swipe" => 5m, "furious-howl" => 30m,
            "lightning-breath" => 0m,
            "prowl" => 10m, "thunderstomp" => 60m,
            _ => tuning.FamilySpecialCooldown
        };
        // Rank-60 Forever client values: Bear Swipe 20-22 physical per target;
        // Wind Serpent Lightning Breath 86-98 Nature to one target. Unknown
        // family coefficients intentionally continue using configurable tuning.
        var (specialMin,specialMax)=family.SpecialAbility switch {
            "swipe" => (20m,22m),
            "lightning-breath" => (86m,98m),
            "thunderstomp" => (122m,142m),
            _ => (tuning.FamilySpecialDamage,tuning.FamilySpecialDamage)
        };
        AbilityEffectDefinition[] effects=family.SpecialAbility switch {
            "savage-rend" => [Periodic("pet-rend-tick","physical",tuning.PetBleedTickDamage,8m,2m)],
            "tendon-rip" => [Periodic("pet-tendon-tick","physical",tuning.PetBleedTickDamage,8m,2m)],
            "scorpid-poison" => [Periodic("pet-scorpid-tick","nature",tuning.PetPoisonTickDamage,8m,2m)],
            "web" => [Periodic("pet-corrosive-web-tick","nature",26m,4m,2m)],
            _ when utility => [],
            _ => [Damage("pet-family-impact",school,specialMin,specialMax,targets)]
        };
        var special=Action("pet-"+family.SpecialAbility,"pet","focus",focusCost,cooldown,effects);
        if (family.SpecialAbility == "furious-howl") {
            // Avoid spending Focus to refresh a still-active one-minute howl.
            special.ForbiddenSourceAuraKeys.Add("wolf-furious-howl");
        }
        if (family.SpecialAbility == "prowl") {
            // Cat stealth cannot be activated after the pet enters combat.
            // It is a pre-pull instant action, not a repeating DPS cooldown.
            special.IsOffGlobalCooldown = true;
            special.GlobalCooldownSeconds = 0m;
            special.ForbiddenSourceAuraKeys.Add("pet-engaged");
        }
        list.Add(special);
        return list;
    }
}
