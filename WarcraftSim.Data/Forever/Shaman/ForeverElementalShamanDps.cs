using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Data.Forever.Shaman;

/// <summary>Selected level-60 Forever Elemental talents. Unmodeled filler is represented
/// by points spent, never awarded as an unimplemented benefit.</summary>
public sealed class ForeverElementalShamanTalents
{
    public int ElementalPointsSpent { get; set; } = 31;
    public int EnhancementPointsSpent { get; set; } = 7;
    public int RestorationPointsSpent { get; set; } = 13;
    public Dictionary<string, int> Ranks { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["convection"]=5, ["concussion"]=5, ["call-of-flame"]=3,
        ["elemental-alacrity"]=3, ["lightning-overload"]=3,
        ["elemental-fury"]=5, ["lava-burst"]=1,
        ["elemental-focus"]=1, ["elemental-mastery"]=1,
        ["tidal-focus"]=5, ["mindfulness"]=3, ["thundering-strikes"]=5
    };
    private static readonly Dictionary<string, (string Tree, int Maximum)> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        ["convection"] = ("elemental", 5), ["concussion"] = ("elemental", 5),
        ["call-of-flame"] = ("elemental", 3),
        ["elemental-alacrity"] = ("elemental", 3), ["lightning-overload"] = ("elemental", 3),
        ["elemental-fury"] = ("elemental", 5), ["lava-burst"] = ("elemental", 1),
        ["elemental-mastery"] = ("elemental", 1),
        ["elemental-focus"] = ("elemental", 1), ["elemental-devastation"] = ("elemental", 3),
        ["reverberation"] = ("elemental", 5), ["elemental-precision"] = ("elemental", 3),
        ["tidal-mastery"] = ("restoration", 5),
        ["tidal-focus"] = ("restoration", 5), ["mindfulness"] = ("restoration", 3),
        ["totemic-focus"] = ("restoration", 5), ["restorative-totems"] = ("restoration", 5),
        ["natural-grace"] = ("restoration", 3), ["improved-healing-wave"] = ("restoration", 5),
        ["thundering-strikes"] = ("enhancement", 5), ["ancestral-knowledge"] = ("enhancement", 5),
        ["improved-ghost-wolf"] = ("enhancement", 2),
        ["shamanistic-focus"] = ("enhancement", 1),
        ["elemental-warding"] = ("elemental", 3), ["eye-of-the-storm"] = ("elemental", 3),
        ["elemental-reach"] = ("elemental", 2), ["trial-by-fire"] = ("elemental", 2)
    };
    public int Rank(string key) => Ranks?.GetValueOrDefault(key) ?? 0;
    public void Validate()
    {
        if (Ranks is null || ElementalPointsSpent < 0 || EnhancementPointsSpent < 0 || RestorationPointsSpent < 0 ||
            ElementalPointsSpent + EnhancementPointsSpent + RestorationPointsSpent > 51)
            throw new ArgumentException("Elemental talents require at most 51 allocated points.");
        var totals = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, rank) in Ranks)
        {
            if (!Supported.TryGetValue(key, out var allowed) || rank < 0 || rank > allowed.Maximum)
                throw new ArgumentException($"Unsupported Elemental talent rank: {key}={rank}.");
            totals[allowed.Tree] = totals.GetValueOrDefault(allowed.Tree) + rank;
        }
        if (totals.GetValueOrDefault("elemental") > ElementalPointsSpent ||
            totals.GetValueOrDefault("restoration") > RestorationPointsSpent ||
            totals.GetValueOrDefault("enhancement") > EnhancementPointsSpent)
            throw new ArgumentException("Elemental selected ranks exceed allocated tree points.");
        if (Rank("lava-burst") > 0 && ElementalPointsSpent < 21)
            throw new ArgumentException("Lava Burst needs 21 Elemental points.");
        if (Rank("lightning-overload") > 0 && Rank("elemental-alacrity") < 3)
            throw new ArgumentException("Lightning Overload requires 3/3 Elemental Alacrity.");
        if (Rank("lightning-overload") > 0 && ElementalPointsSpent < 21)
            throw new ArgumentException("Lightning Overload needs at least 21 Elemental points.");
        if (Rank("elemental-mastery") > 0 && (ElementalPointsSpent < 31 || Rank("lava-burst") == 0))
            throw new ArgumentException("Elemental Mastery requires 31 Elemental points and Lava Burst.");
        if (Rank("elemental-fury") > 0 && Rank("lightning-overload") < 3)
            throw new ArgumentException("Elemental Fury requires 3/3 Lightning Overload (Oct 16 tree).");
    }
}

/// <summary>Explicit assumptions for level-60 spell ranks until Forever combat logs confirm values.</summary>
public sealed class ForeverElementalShamanTuning
{
    public decimal LightningBoltDamage { get; set; } = 196m; // beta-client Rank 10 midpoint (185-207)
    public decimal LightningBoltMana { get; set; } = 220m;
    public decimal Rank7LightningBoltDamage {get;set;}=147m;
    public decimal Rank7LightningBoltMana {get;set;}=135m;
    public decimal Rank7SpellPowerRetention {get;set;}=.75m; // provisional low-rank penalty
    public decimal ChainLightningDamage { get; set; } = 123m; // beta Rank 4 midpoint
    public decimal ChainLightningMana { get; set; } = 315m; // 16 Oct 70425 hotfix
    public decimal EarthShockDamage { get; set; } = 301m; // 70532 rank 7: 293-309.
    public decimal EarthShockMana { get; set; } = 450m;
    public decimal FlameShockDirectDamage { get; set; } = 166m;
    public decimal FlameShockTickDamage { get; set; } = 44m;
    public decimal FlameShockMana { get; set; } = 400m;
    public decimal LavaBurstDamage { get; set; } = 220m; // provisional Lv60 new rank; verify when indexed
    public decimal LavaBurstMana { get; set; } = 265m; // provisional new rank-4 cost
    public decimal ManaRegenPerSecond { get; set; } = 6m;
    public decimal LightningOverloadChancePercentRank1 { get; set; } = 3m;
    public decimal LightningOverloadChancePercentRank2 { get; set; } = 7m;
    public decimal LightningOverloadChancePercentRank3 { get; set; } = 10m;
    public decimal LavaBurstFlameShockBonusPercent { get; set; } = 20m;
}

public static class ForeverElementalShamanSpellbook
{
    public static IReadOnlyList<AbilityDefinition> Create(ForeverElementalShamanTalents t,
        ForeverElementalShamanTuning v, decimal spellPower)
    {
        ArgumentNullException.ThrowIfNull(t); ArgumentNullException.ThrowIfNull(v);
        if (spellPower < 0m) throw new ArgumentException("Spell power cannot be negative.");
        static AbilityEffectDefinition impact(string key, string school, decimal damage) => new()
        {
            Key=key, EffectType=AbilityEffectTypes.DirectDamage, TargetType=AbilityTargetTypes.Enemy,
            SchoolKey=school, ResolutionType=CombatResolutionTypes.Spell, MitigationType=DamageMitigationTypes.None,
            CanMiss=true, CanCrit=true, MinimumValue=damage, MaximumValue=damage
        };
        static AbilityDefinition spell(string key, string name, decimal mana, decimal cast, decimal cd,
            params AbilityEffectDefinition[] effects) => new()
        {
            Key=key, Name=name, ClassKey="shaman", SpecializationKey="elemental",
            CastTimeSeconds=cast, GlobalCooldownSeconds=1.5m, CooldownSeconds=cd,
            ResourceCosts=[new AbilityResourceCost {ResourceKey="mana", Amount=Math.Max(0m,mana)}],
            Effects=effects.ToList()
        };
        var convection = 1m - .02m*t.Rank("convection");
        var nature = 1m + .01m*t.Rank("concussion");
        var fire = 1m + .05m*t.Rank("call-of-flame");
        var bolt = spell("lightning-bolt", "Lightning Bolt", v.LightningBoltMana * convection,
            Math.Max(1m,2.5m-t.Rank("elemental-alacrity")/6m),0m,
            impact("lightning-bolt-impact", "nature", (v.LightningBoltDamage+spellPower*.714m)*nature));
        var chainEffect=impact("chain-lightning-impact","nature",(v.ChainLightningDamage+spellPower*.571m)*nature);
        chainEffect.MaxTargets=3; // Engine multi-target; per-jump 30% decay not yet represented.
        var chain = spell("chain-lightning", "Chain Lightning",v.ChainLightningMana*convection,
            Math.Max(1m,2m-t.Rank("elemental-alacrity")/6m),6m,chainEffect);
        var rank7=spell("lightning-bolt-rank7","Lightning Bolt Rank 7",v.Rank7LightningBoltMana*convection,
            Math.Max(1m,2.5m-t.Rank("elemental-alacrity")/6m),0m,
            impact("rank7-impact","nature",(v.Rank7LightningBoltDamage+spellPower*.714m*v.Rank7SpellPowerRetention)*nature));
        var shockDot = new AbilityEffectDefinition
        {
            Key="flame-shock-tick", EffectType=AbilityEffectTypes.PeriodicDamage,
            TargetType=AbilityTargetTypes.Enemy, SchoolKey="fire", ResolutionType=CombatResolutionTypes.Spell,
            MitigationType=DamageMitigationTypes.None, CanMiss=false, CanCrit=true,
            AuraKey="flame-shock", DurationSeconds=12m+3m*t.Rank("call-of-flame"), TickIntervalSeconds=3m,
            IncludeExpirationBoundaryTick=true,
            // Flame Shock's DoT follows the landed direct Shock hit.
            DependsOnEffectKey="flame-shock-impact",
            MinimumValue=(v.FlameShockTickDamage+spellPower*.10m)*fire,
            MaximumValue=(v.FlameShockTickDamage+spellPower*.10m)*fire
        };
        // Forever Enhancement Shamanistic Focus: Shock spells cost 45% less Mana.
        // In this spellbook Flame Shock is the only implemented Shock.
        var shockFocusMultiplier=t.Rank("shamanistic-focus")>0?.55m:1m;
        var flameShock = spell("flame-shock","Flame Shock",v.FlameShockMana*convection*shockFocusMultiplier,0m,6m-.2m*t.Rank("reverberation"),
            impact("flame-shock-impact","fire",(v.FlameShockDirectDamage+spellPower*.214m)*fire),shockDot);
        var earthShock = spell("earth-shock", "Earth Shock",
            v.EarthShockMana * convection * shockFocusMultiplier, 0m,
            6m - .2m * t.Rank("reverberation"),
            impact("earth-shock-impact", "nature", (v.EarthShockDamage + spellPower * .386m) * nature));
        earthShock.SharedCooldownGroupKey = "shaman-shocks";
        earthShock.SharedCooldownSeconds = earthShock.CooldownSeconds;
        flameShock.SharedCooldownGroupKey = "shaman-shocks";
        flameShock.SharedCooldownSeconds = flameShock.CooldownSeconds;
        var lava = spell("lava-burst", "Lava Burst",v.LavaBurstMana*convection,
            Math.Max(1m,2.5m-t.Rank("elemental-alacrity")/6m),10m,
            impact("lava-burst-impact","fire",(v.LavaBurstDamage+spellPower*.714m)*fire));
        var searing=spell("searing-totem","Searing Totem Rank 6",170m*(1m-.05m*t.Rank("totemic-focus")),0m,0m,
            new AbilityEffectDefinition {
                Key="searing-totem-tick",EffectType=AbilityEffectTypes.PeriodicDamage,
                TargetType=AbilityTargetTypes.Enemy,SchoolKey="fire",ResolutionType=CombatResolutionTypes.Spell,
                MitigationType=DamageMitigationTypes.None,CanMiss=false,CanCrit=true,
                AuraKey="searing-totem",DurationSeconds=55m,TickIntervalSeconds=2m,
                MinimumValue=(47m+spellPower*.017m)*(1m+.05m*t.Rank("call-of-flame")),
                MaximumValue=(47m+spellPower*.017m)*(1m+.05m*t.Rank("call-of-flame"))
            });
        // Elemental Mastery uses a source aura; a separate event processor applies the cast-time
        // adjustment during its 15-second active window without touching shared timing logic.
        var mastery=spell("elemental-mastery","Elemental Mastery",0m,0m,180m,
            new AbilityEffectDefinition{Key="elemental-mastery-buff",EffectType=AbilityEffectTypes.ApplyAura,
                TargetType=AbilityTargetTypes.Self,ResolutionType=CombatResolutionTypes.AlwaysHits,
                AuraKey="elemental-mastery",DurationSeconds=15m,CanMiss=false,CanCrit=false});
        var extraBolt = spell("lightning-overload-bolt", "Lightning Overload: Lightning Bolt",0m,0m,0m,
            impact("overload-bolt-impact","nature",(v.LightningBoltDamage+spellPower*.714m)*nature*.5m));
        var extraChain = spell("lightning-overload-chain", "Lightning Overload: Chain Lightning",0m,0m,0m,
            impact("overload-chain-impact","nature",(v.ChainLightningDamage+spellPower*.714m)*nature*.5m));
        // Self-targeted party totem approximation (solo actor only). 10 Mana/2 sec,
        // 300-second duration, 100 Mana placement cost; no phantom MP5 multiplier.
        var manaSpring = spell("mana-spring-totem", "Mana Spring Totem", 100m, 0m, 0m,
            new AbilityEffectDefinition
            {
                Key="mana-spring-totem-tick", EffectType=AbilityEffectTypes.PeriodicResourceChange,
                TargetType=AbilityTargetTypes.Self, ResourceChangeOperation=ResourceChangeOperationTypes.Gain,
                ResourceKey="mana", ResolutionType=CombatResolutionTypes.AlwaysHits,
                AuraKey="mana-spring-totem", DurationSeconds=300m, TickIntervalSeconds=2m,
                MinimumValue=10m, MaximumValue=10m, CanMiss=false, CanCrit=false
            });
        foreach (var extra in new[] {extraBolt,extraChain})
        { extra.IsOffGlobalCooldown=true; extra.GlobalCooldownSeconds=0m; }
        return t.Rank("lava-burst")>0 ? [bolt,rank7,chain,flameShock,earthShock,lava,searing,mastery,extraBolt,extraChain,manaSpring] :
            [bolt,rank7,chain,flameShock,earthShock,searing,extraBolt,extraChain,manaSpring];
    }
}
