using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Data.Forever.Druid;

/// <summary>Modeled level-60 Balance talents; filler points have no phantom benefits.</summary>
public sealed class ForeverBalanceDruidTalents
{
    public int BalancePointsSpent { get; set; } = 41;
    public int FeralPointsSpent { get; set; }
    public int RestorationPointsSpent { get; set; } = 10;
    public Dictionary<string,int> Ranks { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["improved-wrath"]=5, ["genesis"]=5, ["improved-moonfire"]=2,
        ["moonglow"]=3, ["natures-majesty"]=2, ["natures-reach"]=2,
        ["natures-splendor"]=1, ["insect-swarm"]=1, ["vengeance"]=5,
        ["eclipse"]=3, ["moonfury"]=5, ["moonkin-form"]=1
    };
    private static readonly Dictionary<string, (string Tree,int Maximum)> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        ["improved-wrath"]=("balance",5), ["genesis"]=("balance",5),
        ["improved-moonfire"]=("balance",2),["moonglow"]=("balance",3),
        ["natures-majesty"]=("balance",2),["natures-reach"]=("balance",2),
        ["natures-splendor"]=("balance",1),["insect-swarm"]=("balance",1),
        ["vengeance"]=("balance",5),["eclipse"]=("balance",3),
        ["moonfury"]=("balance",5),["moonkin-form"]=("balance",1),
        ["natures-grace"]=("balance",1),["starlight-wrath"]=("balance",5),
        ["improved-starfire"]=("balance",5),["naturalist"]=("restoration",5),
        ["subtlety"]=("restoration",3), ["reflection"]=("restoration",3),
        ["natures-focus"]=("restoration",5), ["furor"]=("restoration",5)
    };
    public int Rank(string key)=>Ranks?.GetValueOrDefault(key)??0;
    public void Validate()
    {
        if (Ranks is null || BalancePointsSpent<0 || FeralPointsSpent<0 || RestorationPointsSpent<0 ||
            BalancePointsSpent+FeralPointsSpent+RestorationPointsSpent>51)
            throw new ArgumentException("Balance talents require at most 51 allocated points.");
        var balance=0; var resto=0;
        foreach(var (key,rank) in Ranks)
        {
            if(!Supported.TryGetValue(key,out var allow)||rank<0||rank>allow.Maximum)
                throw new ArgumentException($"Unsupported Balance talent rank: {key}={rank}.");
            if(allow.Tree=="balance")balance+=rank;
            if(allow.Tree=="restoration")resto+=rank;
        }
        if(balance>BalancePointsSpent||resto>RestorationPointsSpent)
            throw new ArgumentException("Balance talent ranks exceed allocated points.");
        if(Rank("eclipse")>0&&BalancePointsSpent<21)
            throw new ArgumentException("Eclipse needs 21 Balance points.");
        if(Rank("moonkin-form")>0&&BalancePointsSpent<31)
            throw new ArgumentException("Moonkin Form needs 31 Balance points.");
        if(Rank("insect-swarm")>0&&BalancePointsSpent<16)
            throw new ArgumentException("Insect Swarm needs 16 Balance points.");
        if(Rank("natures-splendor")>0&&Rank("natures-majesty")==0)
            throw new ArgumentException("Nature's Splendor requires Nature's Majesty.");
    }
}
public sealed class ForeverBalanceDruidTuning
{
    public decimal WrathDamage {get;set;}=91m;
    public decimal WrathMana {get;set;}=120m;
    public decimal StarfireDamage {get;set;}=381m;
    public decimal StarfireMana {get;set;}=340m;
    public decimal MoonfireDirectDamage {get;set;}=135m;
    public decimal MoonfireTickDamage {get;set;}=15m;
    public decimal MoonfireMana {get;set;}=375m;
    public decimal InsectSwarmTickDamage {get;set;}=31m;
    public decimal InsectSwarmMana {get;set;}=160m;
    // Provisional level-60 Hurricane rank values (not Blizzard-verified).
    public decimal HurricaneMana {get;set;}=850m;
    public decimal HurricaneTickDamage {get;set;}=155m;
    public decimal ManaRegenPerSecond {get;set;}=7m;
    public decimal EclipseCastReductionPerRank {get;set;}=0.1666666666666666666666666667m;
}
public static class ForeverBalanceDruidSpellbook
{
    public static IReadOnlyList<AbilityDefinition> Create(ForeverBalanceDruidTalents t,
        ForeverBalanceDruidTuning v, decimal spellPower)
    {
        ArgumentNullException.ThrowIfNull(t);ArgumentNullException.ThrowIfNull(v);
        if(spellPower<0m)throw new ArgumentException("Spell power must be nonnegative.");
        static AbilityEffectDefinition hit(string key,string school,decimal damage)=>new()
        {
            Key=key,EffectType=AbilityEffectTypes.DirectDamage,TargetType=AbilityTargetTypes.Enemy,
            SchoolKey=school,ResolutionType=CombatResolutionTypes.Spell,MitigationType=DamageMitigationTypes.None,
            CanMiss=true,CanCrit=true,MinimumValue=damage,MaximumValue=damage
        };
        static AbilityEffectDefinition dot(string key,string aura,string school,decimal damage,decimal duration,decimal interval=3m)=>new()
        {
            Key=key,EffectType=AbilityEffectTypes.PeriodicDamage,TargetType=AbilityTargetTypes.Enemy,
            SchoolKey=school,ResolutionType=CombatResolutionTypes.Spell,MitigationType=DamageMitigationTypes.None,
            CanMiss=true,CanCrit=true,MinimumValue=damage,MaximumValue=damage,AuraKey=aura,
            DurationSeconds=duration,TickIntervalSeconds=interval,IncludeExpirationBoundaryTick=true
        };
        static AbilityDefinition spell(string key,string name,decimal mana,decimal cast,params AbilityEffectDefinition[] effects)=>new()
        {
            Key=key,Name=name,ClassKey="druid",SpecializationKey="balance",CastTimeSeconds=cast,
            GlobalCooldownSeconds=1.5m,
            ResourceCosts=[new AbilityResourceCost{ResourceKey="mana",Amount=Math.Max(0m,mana)}],
            Effects=effects.ToList()
        };
        var damageBonus=1m+t.Rank("moonfury")*.02m+t.Rank("naturalist")*.02m;
        var dotBonus=1m+t.Rank("genesis")*.01m;
        var moonBonus=1m+t.Rank("improved-moonfire")*.05m;
        var moonglow=1m-t.Rank("moonglow")*.25m/3m;
        var durationBonus=t.Rank("natures-splendor")>0?3m:0m;
        var wrath=spell("wrath","Wrath",v.WrathMana*moonglow*(1m-t.Rank("improved-wrath")*.1m),
            Math.Max(1m,2m-t.Rank("improved-wrath")*.1m),
            hit("wrath-impact","nature",(v.WrathDamage+spellPower*.57m)*damageBonus));
        var starfire=spell("starfire","Starfire",v.StarfireMana*moonglow,
            Math.Max(1.5m,3.5m-t.Rank("improved-starfire")*.1m),
            hit("starfire-impact","arcane",(v.StarfireDamage+spellPower*1m)*damageBonus));
        var moonfire=spell("moonfire","Moonfire",v.MoonfireMana*moonglow,0m,
            hit("moonfire-impact","arcane",(v.MoonfireDirectDamage+spellPower*.15m)*damageBonus*moonBonus),
            dot("moonfire-tick","moonfire","arcane",
                (v.MoonfireTickDamage+spellPower*.13m)*damageBonus*dotBonus*moonBonus,12m+durationBonus));
        foreach(var effect in moonfire.Effects)
            effect.BonusCriticalChancePercent=t.Rank("improved-moonfire")*5m;
        var swarm=spell("insect-swarm","Insect Swarm",v.InsectSwarmMana*moonglow,0m,
            dot("insect-swarm-tick","insect-swarm","nature",
                (v.InsectSwarmTickDamage+spellPower*.158m)*damageBonus*dotBonus,12m+(durationBonus>0m?2m:0m),2m));
        var hurricane=spell("hurricane","Hurricane",v.HurricaneMana*moonglow,0m,
            hit("hurricane-tick","nature",
                (v.HurricaneTickDamage+spellPower*.13m)*damageBonus));
        hurricane.Effects[0].ApplyOnChannelTick=true;
        hurricane.ChannelDurationSeconds=10m;
        hurricane.ChannelTickIntervalSeconds=2m;
        // Forever removed Hurricane's cooldown; never set a made-up cooldown.
        var spells=new List<AbilityDefinition>{wrath,starfire,moonfire,hurricane};
        if(t.Rank("insect-swarm")>0)spells.Add(swarm);
        return spells;
    }
}
