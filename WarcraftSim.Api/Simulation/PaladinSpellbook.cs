using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Data.Forever.Paladin;

namespace WarcraftSim.Api.Simulation;

/// <summary>Current rank-60 spell definitions. Values are pinned in CP175-paladin-source;
/// source confidence and unmeasured coefficients are independent of execution coverage.</summary>
public static class PaladinSpellbook
{
    internal static readonly string[] Seals=["righteousness","fury","light","wisdom","justice","crusader","command"];
    internal static readonly string[] Auras=["devotion","retribution","concentration","resistance-fire","resistance-frost","resistance-shadow"];
    internal static readonly string[] HealingActions=["holy-light","flash-of-light","holy-shock-heal","lights-vigil","oath-of-light","cleanse","purify","blessing-of-freedom","blessing-of-protection","lay-on-hands"];
    internal static readonly IReadOnlyDictionary<string,decimal> SealCosts=new Dictionary<string,decimal>
    { ["righteousness"]=200m,["fury"]=200m,["light"]=210m,["wisdom"]=200m,["justice"]=-.13m,["crusader"]=160m,["command"]=210m };
    internal static decimal SealCost(string seal,decimal baseMana)=>SealCosts[seal]<0m?-SealCosts[seal]*baseMana:SealCosts[seal];
    internal static decimal BaseHealCost(string action)=>action switch {"holy-light"=>660m,"flash-of-light"=>140m,"holy-shock-heal"=>325m,"lights-vigil" or "lights-vigil-heal"=>1340m,_=>0m};

    public static List<AbilityDefinition> Create(PaladinSimRequest r,string weaponKind,bool shield,decimal baseMana,decimal maxHealth)
    {
        int rank(string key)=>r.Talents.Rank(key);
        var book=new List<AbilityDefinition>();
        AbilityDefinition action(string key,decimal cost=0m,decimal cooldown=0m,decimal cast=0m,List<AbilityEffectDefinition>? effects=null)
        {
            var a=new AbilityDefinition {Key=key,Name=key.Replace('-',' '),ClassKey="paladin",RequiredLevel=60,
                CooldownSeconds=cooldown,CastTimeSeconds=cast,GlobalCooldownSeconds=1.5m,
                ResourceCosts=cost>0m?[new(){ResourceKey="mana",Amount=cost}]:[],Effects=effects??[]};
            book.Add(a);return a;
        }
        foreach(var seal in Seals.Where(x=>x!="command"||rank("seal-of-command")>0))
            action("seal-of-"+seal,SealCost(seal,baseMana),effects:[AuraEffect("paladin-seal-"+seal,30m)]);
        foreach(var aura in Auras)
            action("aura-"+aura,effects:[AuraEffect("paladin-aura-"+aura,3600m)]);
        action("righteous-fury",.30m*baseMana,effects:[AuraEffect("paladin-righteous-fury",1800m)]);
        var judgement=action("judgement",.06m*baseMana,10m-rank("improved-judgement"));
        judgement.IsOffGlobalCooldown=true;judgement.GlobalCooldownSeconds=0m;
        judgement.RequiredAnySourceAuraKeys=Seals.Select(s=>"paladin-seal-"+s).ToList();
        if(rank("swift-judgement")>0)
        {
            var swift=action("swift-judgement",cooldown:60m);swift.RequiredAnySourceAuraKeys=[..judgement.RequiredAnySourceAuraKeys];
            swift.IsOffGlobalCooldown=true;swift.GlobalCooldownSeconds=0m;
        }
        if(weaponKind!="none")
        {
            var strike=Damage("holy-strike-impact", .5m*(81m+r.WeaponMinDamage),.5m*(105m+r.WeaponMaxDamage),0m);
            strike.ResolutionType=CombatResolutionTypes.Melee;strike.WeaponHandKey="main-hand";strike.CanBeDodged=true;
            strike.ScalingStatKey="paladin-holy-strike-power";strike.ScalingCoefficient=.5m;
            // The Holy strike is a weapon attack even though its damage school is Holy.
            strike.DamageMultiplier=rank("sacred-arbiter")>0?1.2m:1m;
            action("holy-strike",20m,shield&&rank("shieldsworn")>0?6m:10m,effects:[strike]);
        }
        action("consecration",565m,8m); // Its ground scheduler resolves one current hit/crit roll per enemy per tick.
        var purifying=rank("purifying-power") switch {1=>.17m,2=>.33m,_=>0m};
        if(r.TargetCreatureType is "undead" or "demon")
        {
            action("exorcism",345m,15m*(1m-purifying),effects:[Damage("exorcism-impact",475m,529m,ForeverPaladinSourceData.DirectHolySpellPower)]);
            var wrath=Damage("holy-wrath-impact",490m,576m,ForeverPaladinSourceData.HolyWrathSpellPower);wrath.MaxTargets=9;
            action("holy-wrath",805m,60m*(1m-purifying),2m,[wrath]);
        }
        action("hammer-of-wrath",425m,6m,Math.Max(0m,1m-.2m*rank("champion-of-the-light")),
            [Damage("hammer-of-wrath-impact",474m,522m,ForeverPaladinSourceData.DirectHolySpellPower)]).MaximumTargetHealthPercent=20m;
        var light=1m+.04m*rank("healing-light");
        action("holy-light",660m,cast:2.5m,effects:[Heal("holy-light-heal",1495m*light,1665m*light,ForeverPaladinSourceData.HolyLightSpellPower*light)]);
        action("flash-of-light",140m,cast:1.5m,effects:[Heal("flash-of-light-heal",ForeverPaladinSourceData.FlashOfLightMinimumAt60*light,ForeverPaladinSourceData.FlashOfLightMaximumAt60*light,ForeverPaladinSourceData.DirectHolySpellPower*light)]);
        if(rank("divine-favor")>0)
        {
            var favor=action("divine-favor",.04m*baseMana,120m,effects:[AuraEffect("paladin-divine-favor",600m)]);
            favor.IsOffGlobalCooldown=true;
        }
        if(rank("oath-of-light")>0)
            action("oath-of-light",.08m*baseMana,effects:[AuraEffect("paladin-oath-of-light",300m,AbilityTargetTypes.Friendly)]);
        if(rank("holy-shock")>0)
        {
            foreach(var key in new[]{"holy-shock","holy-shock-heal"})
            {
                var shock=action(key,325m,10m,effects:[key=="holy-shock"?Damage("holy-shock-damage",334m,362m,ForeverPaladinSourceData.DirectHolySpellPower):
                    Heal("holy-shock-heal",307m*light,333m*light,ForeverPaladinSourceData.DirectHolySpellPower*light)]);
                shock.SharedCooldownGroupKey="paladin-holy-shock";shock.SharedCooldownSeconds=10m;
            }
        }
        if(rank("lights-vigil")>0&&r.FriendlyTargetEnabled)
            action("lights-vigil",1340m,6m,1.5m,[AuraEffect("paladin-lights-vigil",30m,AbilityTargetTypes.Friendly)]);
        if(rank("holy-shield")>0&&shield)
            action("holy-shield",240m,10m,effects:[AuraEffect("paladin-holy-shield",10m)]);
        var sacred=30m*rank("sacred-duty");
        if(rank("templars-bulwark")>0)
        {
            var bulwark=action("templars-bulwark",110m,300m-sacred,effects:[new() {
                Key="templars-bulwark-shield",EffectType=AbilityEffectTypes.Absorb,TargetType=AbilityTargetTypes.Self,
                AbsorbKey="paladin-templars-bulwark",DurationSeconds=8m,MinimumValue=maxHealth,MaximumValue=maxHealth,
                CanMiss=false,CanCrit=false,ResolutionType=CombatResolutionTypes.AlwaysHits
            }]);
            bulwark.ForbiddenSourceAuraKeys.Add("paladin-forbearance");bulwark.IsOffGlobalCooldown=true;bulwark.GlobalCooldownSeconds=0m;
        }
        action("divine-shield",110m,300m-sacred,effects:[AuraEffect("paladin-divine-shield",12m)])
            .ForbiddenSourceAuraKeys.Add("paladin-forbearance");
        action("divine-protection",35m,300m-sacred,effects:[AuraEffect("paladin-divine-protection",8m)])
            .ForbiddenSourceAuraKeys.Add("paladin-forbearance");
        action("blessing-of-protection",.07m*baseMana,300m-60m*rank("guardians-favor"),
            effects:[AuraEffect("paladin-blessing-of-protection",10m,AbilityTargetTypes.Friendly)])
            .ForbiddenTargetAuraKeys.Add("paladin-forbearance");
        action("blessing-of-freedom",.10m*baseMana,20m,effects:[AuraEffect("paladin-blessing-of-freedom",
            10m+3m*rank("guardians-favor"),AbilityTargetTypes.Friendly)]);
        action("cleanse",.08m*baseMana*(1m-.1m*rank("purifying-power")));
        action("purify",.08m*baseMana*(1m-.1m*rank("purifying-power")));
        // Base-kit single-friendly-target blessing scope uses the same one-blessing ownership as Protection/Freedom.
        action("blessing-of-might",130m,effects:[AuraEffect("paladin-blessing-of-might",3600m,AbilityTargetTypes.Friendly)]);
        action("blessing-of-wisdom",125m,effects:[AuraEffect("paladin-blessing-of-wisdom",3600m,AbilityTargetTypes.Friendly)]);
        action("blessing-of-kings",.08m*baseMana,effects:[AuraEffect("paladin-blessing-of-kings",3600m,AbilityTargetTypes.Friendly)]);
        if(r.FriendlyTargetEnabled)
            action("blessing-of-sacrifice",ForeverPaladinSourceData.BlessingOfSacrificeManaCost,
                effects:[AuraEffect("paladin-blessing-of-sacrifice",ForeverPaladinSourceData.BlessingOfSacrificeDurationSeconds,AbilityTargetTypes.Friendly)]);
        action("lay-on-hands",cooldown:1200m);
        if(!r.TargetControlImmune)
        {
            action("hammer-of-justice",100m,60m-5m*rank("improved-hammer-of-justice"),
                effects:[AuraEffect("paladin-stunned",6m,AbilityTargetTypes.Enemy)]);
            if(rank("repentance")>0&&r.TargetCreatureType=="humanoid")
                action("repentance",60m,60m,effects:[AuraEffect("paladin-incapacitated",6m,AbilityTargetTypes.Enemy)]);
            if(r.TargetCreatureType=="undead"||r.TargetCreatureType=="demon"&&rank("turn-evil")>0)
                action("turn-undead",75m,30m-5m*rank("turn-evil"),1.5m-.5m*rank("turn-evil"),
                    [AuraEffect("paladin-turned",20m,AbilityTargetTypes.Enemy)]);
        }
        foreach(var a in book)
        {
            if(a.Key is "consecration" or "holy-wrath" or "exorcism" or "hammer-of-wrath")
                foreach(var c in a.ResourceCosts)c.Amount*=1m-.2m*rank("holy-conduit");
            if(a.CastTimeSeconds==0m)foreach(var c in a.ResourceCosts)c.Amount*=1m-.02m*rank("benediction");
            // Physical abilities are restricted during physical protection; every spell observes control/silence.
            a.ForbiddenSourceAuraKeys.Add("paladin-mounted");
            a.ForbiddenSourceAuraKeys.AddRange(["paladin-control-stun","paladin-control-charm","paladin-control-fear","paladin-control-disorient"]);
            if(a.Key!="holy-strike")a.ForbiddenSourceAuraKeys.Add("paladin-control-silence");
            else a.ForbiddenSourceAuraKeys.AddRange(["paladin-divine-protection","paladin-blessing-of-protection"]);
        }
        return book;
    }
    internal static AbilityEffectDefinition Damage(string key,decimal min,decimal max,decimal coefficient,
        string school="holy",string resolution=CombatResolutionTypes.Spell)=>new() {
        Key=key,EffectType=AbilityEffectTypes.DirectDamage,TargetType=AbilityTargetTypes.Enemy,
        SchoolKey=school,ResolutionType=resolution,CanMiss=true,CanCrit=true,MinimumValue=min,MaximumValue=max,
        ScalingStatKey=school=="holy"?"paladin-holy-spell-power":"spell-power",ScalingCoefficient=coefficient,
        MitigationType=school=="physical"?DamageMitigationTypes.Armor:DamageMitigationTypes.None
    };
    internal static AbilityEffectDefinition Heal(string key,decimal min,decimal max,decimal coefficient)=>new() {
        Key=key,EffectType=AbilityEffectTypes.DirectHealing,TargetType=AbilityTargetTypes.Friendly,SchoolKey="holy",
        ResolutionType=CombatResolutionTypes.Healing,CanMiss=false,CanCrit=true,MinimumValue=min,MaximumValue=max,
        ScalingStatKey="healing-power",ScalingCoefficient=coefficient
    };
    internal static AbilityEffectDefinition AuraEffect(string key,decimal duration,string target=AbilityTargetTypes.Self)=>new() {
        Key=key+"-effect",EffectType=AbilityEffectTypes.ApplyAura,TargetType=target,AuraKey=key,
        DurationSeconds=duration,CanMiss=false,CanCrit=false,ResolutionType=CombatResolutionTypes.AlwaysHits
    };
}
