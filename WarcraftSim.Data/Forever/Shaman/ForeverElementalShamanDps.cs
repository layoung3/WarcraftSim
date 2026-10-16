using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Data.Forever.Shaman;

/// <summary>Selected level-60 Forever Elemental talents. Unmodeled filler is represented
/// by points spent, never awarded as an unimplemented benefit.</summary>
public sealed class ForeverElementalShamanTalents
{
    public int ElementalPointsSpent { get; set; } = 41;
    public int EnhancementPointsSpent { get; set; } = 10;
    public int RestorationPointsSpent { get; set; }
    public Dictionary<string, int> Ranks { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["convection"] = 5, ["concussion"] = 5, ["call-of-flame"] = 3,
        ["call-of-thunder"] = 5, ["elemental-alacrity"] = 3,
        ["lightning-overload"] = 3, ["elemental-fury"] = 5,
        ["lava-burst"] = 1
    };
    private static readonly Dictionary<string, (string Tree, int Maximum)> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        ["convection"] = ("elemental", 5), ["concussion"] = ("elemental", 5),
        ["call-of-flame"] = ("elemental", 3), ["call-of-thunder"] = ("elemental", 5),
        ["elemental-alacrity"] = ("elemental", 3), ["lightning-overload"] = ("elemental", 3),
        ["elemental-fury"] = ("elemental", 5), ["lava-burst"] = ("elemental", 1),
        ["elemental-focus"] = ("elemental", 1), ["elemental-devastation"] = ("elemental", 3),
        ["reverberation"] = ("elemental", 5), ["elemental-precision"] = ("elemental", 3),
        ["tidal-mastery"] = ("restoration", 5)
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
            totals.GetValueOrDefault("restoration") > RestorationPointsSpent)
            throw new ArgumentException("Elemental selected ranks exceed allocated tree points.");
        if (Rank("lava-burst") > 0 && ElementalPointsSpent < 31)
            throw new ArgumentException("Lava Burst needs 31 Elemental points.");
        if (Rank("lightning-overload") > 0 && ElementalPointsSpent < 21)
            throw new ArgumentException("Lightning Overload needs at least 21 Elemental points.");
        if (Rank("elemental-fury") > 0 && Rank("call-of-thunder") == 0)
            throw new ArgumentException("Elemental Fury requires Call of Thunder.");
    }
}

/// <summary>Explicit assumptions for level-60 spell ranks until Forever combat logs confirm values.</summary>
public sealed class ForeverElementalShamanTuning
{
    public decimal LightningBoltDamage { get; set; } = 565m;
    public decimal LightningBoltMana { get; set; } = 310m;
    public decimal ChainLightningDamage { get; set; } = 590m;
    public decimal ChainLightningMana { get; set; } = 600m;
    public decimal FlameShockDirectDamage { get; set; } = 240m;
    public decimal FlameShockTickDamage { get; set; } = 92m;
    public decimal FlameShockMana { get; set; } = 400m;
    public decimal LavaBurstDamage { get; set; } = 660m;
    public decimal LavaBurstMana { get; set; } = 485m;
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
        var fire = 1m + .05m*t.Rank("call-of-flame") + .01m*t.Rank("concussion");
        var bolt = spell("lightning-bolt", "Lightning Bolt", v.LightningBoltMana * convection,
            Math.Max(1m,2.5m-t.Rank("elemental-alacrity")/6m),0m,
            impact("lightning-bolt-impact", "nature", (v.LightningBoltDamage+spellPower*.714m)*nature));
        var chain = spell("chain-lightning", "Chain Lightning (single-target)",v.ChainLightningMana*convection,
            Math.Max(1m,2.5m-t.Rank("elemental-alacrity")/6m),6m,
            impact("chain-lightning-impact","nature",(v.ChainLightningDamage+spellPower*.714m)*nature));
        var shockDot = new AbilityEffectDefinition
        {
            Key="flame-shock-tick", EffectType=AbilityEffectTypes.PeriodicDamage,
            TargetType=AbilityTargetTypes.Enemy, SchoolKey="fire", ResolutionType=CombatResolutionTypes.Spell,
            MitigationType=DamageMitigationTypes.None, CanMiss=true, CanCrit=true,
            AuraKey="flame-shock", DurationSeconds=12m, TickIntervalSeconds=3m,
            IncludeExpirationBoundaryTick=true,
            MinimumValue=(v.FlameShockTickDamage+spellPower*.10m)*fire,
            MaximumValue=(v.FlameShockTickDamage+spellPower*.10m)*fire
        };
        var flameShock = spell("flame-shock","Flame Shock",v.FlameShockMana,0m,6m,
            impact("flame-shock-impact","fire",(v.FlameShockDirectDamage+spellPower*.214m)*fire),shockDot);
        var lava = spell("lava-burst", "Lava Burst",v.LavaBurstMana*convection,
            Math.Max(1m,2.5m-t.Rank("elemental-alacrity")/6m),10m,
            impact("lava-burst-impact","fire",(v.LavaBurstDamage+spellPower*.714m)*fire));
        var extraBolt = spell("lightning-overload-bolt", "Lightning Overload: Lightning Bolt",0m,0m,0m,
            impact("overload-bolt-impact","nature",(v.LightningBoltDamage+spellPower*.714m)*nature*.5m));
        var extraChain = spell("lightning-overload-chain", "Lightning Overload: Chain Lightning",0m,0m,0m,
            impact("overload-chain-impact","nature",(v.ChainLightningDamage+spellPower*.714m)*nature*.5m));
        foreach (var extra in new[] {extraBolt,extraChain})
        { extra.IsOffGlobalCooldown=true; extra.GlobalCooldownSeconds=0m; }
        return t.Rank("lava-burst")>0 ? [bolt,chain,flameShock,lava,extraBolt,extraChain] :
            [bolt,chain,flameShock,extraBolt,extraChain];
    }
}
