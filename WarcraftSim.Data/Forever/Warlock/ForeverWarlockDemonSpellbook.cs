using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Data.Forever.Warlock;

/// <summary>
/// CP164 companion archetypes. The first five are permanent; Infernal and
/// Doomguard require explicit encounter assumptions, not normal pet selection.
/// Numeric combat coefficients not confirmed in the Forever client remain tunable.
/// </summary>
public sealed record ForeverWarlockDemonProfile(
    string Key, bool IsTemporary, bool HasMeleeAuto, decimal HealthMultiplier,
    decimal ArmorMultiplier, decimal AttackPowerMultiplier, decimal ManaMultiplier,
    decimal SwingMultiplier, string PrimaryAbility, string School);

public static class ForeverWarlockDemons
{
    private static readonly IReadOnlyDictionary<string, ForeverWarlockDemonProfile> Entries =
        new Dictionary<string, ForeverWarlockDemonProfile>(StringComparer.OrdinalIgnoreCase)
        {
            ["imp"] = new("imp",false,false,.65m,.5m,.75m,1.3m,1m,"firebolt","fire"),
            ["voidwalker"] = new("voidwalker",false,true,1.50m,1.5m,.8m,1m,1.15m,"torment","shadow"),
            ["succubus"] = new("succubus",false,true,.9m,.8m,1.15m,1m,.9m,"lash-of-pain","shadow"),
            ["incubus"] = new("incubus",false,true,.9m,.8m,1.15m,1m,.9m,"lash-of-pain","shadow"),
            ["felhunter"] = new("felhunter",false,true,1m,1.1m,1m,1.2m,1m,"shadow-bite","shadow"),
            ["infernal"] = new("infernal",true,true,1.8m,1.7m,1.2m,1m,1.25m,"immolation","fire"),
            ["doomguard"] = new("doomguard",true,true,1.6m,1.4m,1.2m,1.4m,1.25m,"doom-bolt","shadow")
        };
    public static IReadOnlyCollection<ForeverWarlockDemonProfile> All => Entries.Values.ToArray();
    public static ForeverWarlockDemonProfile Get(string key) =>
        Entries.TryGetValue(key ?? "",out var entry) ? entry :
            throw new ArgumentException($"Unsupported Warlock demon: '{key}'.");
}

/// <summary>Explicit provisional demon coefficients, independent of Hunter pet tuning.</summary>
public sealed class ForeverWarlockDemonTuning
{
    public decimal BaseHealth { get; set; } = 2500m;
    public decimal BaseArmor { get; set; } = 1000m;
    public decimal BaseMana { get; set; } = 1800m;
    public decimal ManaRegenPerSecond { get; set; } = 12m;
    public decimal OwnerHealthInheritance { get; set; } = 0m;
    public decimal OwnerArmorInheritance { get; set; } = .20m;
    public decimal OwnerSpellPowerInheritance { get; set; } = .15m;
    public decimal SpellBaseDamage { get; set; } = 165m;
    public decimal FelhunterSpellDamage { get; set; } = 120m;
    public decimal InfernalTickDamage { get; set; } = 50m;
    public decimal VoidwalkerTormentThreat { get; set; } = 500m;
    public decimal VoidwalkerSufferingThreat { get; set; } = 350m;
    public decimal HealthFunnelHealing { get; set; } = 125m;
    // Null inherits current owner spell crit; set explicitly for controlled pet-only tests.
    public decimal? DemonSpellCritPercent { get; set; } = null;
    // Explicit deterministic FINAL spell hit override (legacy). Null uses
    // owner spell hit bonus on the target-level spell miss table.
    public decimal? DemonSpellHitPercent { get; set; }
    public decimal DemonMagicResistancePercent { get; set; } = 0m;
    public decimal DemonEngagementDelaySeconds { get; set; } = .75m;
    public decimal TemporaryDemonDurationSeconds { get; set; } = 60m;
    public void Validate()
    {
        if (BaseHealth<=0m || BaseArmor<0m || BaseMana<0m ||
            ManaRegenPerSecond<0m || OwnerHealthInheritance<0m ||
            OwnerArmorInheritance<0m || OwnerSpellPowerInheritance<0m ||
            SpellBaseDamage<0m || FelhunterSpellDamage<0m ||
            InfernalTickDamage<0m || VoidwalkerTormentThreat<0m ||
            VoidwalkerSufferingThreat<0m || HealthFunnelHealing<0m ||
            DemonEngagementDelaySeconds<0m || TemporaryDemonDurationSeconds<=0m ||
            DemonSpellCritPercent is <0m or >100m ||
            DemonSpellHitPercent is <0m or >100m ||
            DemonMagicResistancePercent is <0m or >100m)
            throw new ArgumentException("Invalid provisional Warlock demon tuning.");
    }
}

public static class ForeverWarlockDemonSpellbook
{
    private static AbilityDefinition Cast(string key,decimal cost,decimal cooldown,
        decimal castSeconds=0m, params AbilityEffectDefinition[] effects) => new()
    {
        Key="demon-"+key,Name=key,ClassKey="warlock-demon",
        CooldownSeconds=cooldown,CastTimeSeconds=castSeconds,
        GlobalCooldownSeconds=1.5m,
        ResourceCosts=cost>0m ?
            [new AbilityResourceCost{ResourceKey="mana",Amount=cost}] : [],
        Effects=effects.ToList(),
        RequiredSourceAuraKeys=["warlock-demon-engaged"]
    };
    private static AbilityEffectDefinition Damage(string key,string school,decimal baseDamage) => new()
    {
        Key=key,EffectType=AbilityEffectTypes.DirectDamage,
        TargetType=AbilityTargetTypes.Enemy,SchoolKey=school,
        ResolutionType=CombatResolutionTypes.Spell,
        MitigationType=DamageMitigationTypes.None,
        CanMiss=true,CanCrit=true,MinimumValue=baseDamage,MaximumValue=baseDamage,
        ScalingStatKey="spell-power",ScalingCoefficient=.15m
    };

    public static IReadOnlyList<AbilityDefinition> Create(ForeverWarlockDemonProfile profile,
        ForeverWarlockDemonTuning tuning,int improvedFelhunterRanks=0,
        int improvedImpRanks=0,int improvedSayaadRanks=0)
    {
        var list=new List<AbilityDefinition>();
        switch(profile.Key)
        {
            case "imp":
                list.Add(Cast("firebolt",180m,0m,2m,Damage("firebolt-hit","fire",tuning.SpellBaseDamage*(1m+.10m*improvedImpRanks))));
                break;
            case "voidwalker":
                list.Add(Cast("torment",60m,5m));
                list.Add(Cast("suffering",100m,120m));
                break;
            case "succubus":
            case "incubus":
                // Level-60 Forever Lash of Pain: 160 Mana / 12-second cooldown.
                list.Add(Cast("lash-of-pain",160m,12m,0m,Damage("lash-hit","shadow",55m*(1m+.10m*improvedSayaadRanks))));
                // Seduction requires a susceptible encounter target and is
                // enabled by the runner only when explicitly configured.
                list.Add(Cast("seduction",120m,30m,1.5m));
                break;
            case "felhunter":
                list.Add(Cast("shadow-bite",90m,6m,0m,
                    Damage("shadow-bite-hit","shadow",tuning.FelhunterSpellDamage)));
                list.Add(Cast("spell-lock",120m,Math.Max(0m,24m-2m*improvedFelhunterRanks)));
                // Devour Magic may only fire if there is an actual magic aura
                // available in the encounter model.
                list.Add(Cast("devour-magic",215m,8m));
                break;
            case "infernal":
                var immolate=Damage("infernal-immolation-tick","fire",tuning.InfernalTickDamage);
                immolate.EffectType=AbilityEffectTypes.PeriodicDamage;
                immolate.AuraKey="infernal-immolation";
                immolate.DurationSeconds=8m;immolate.TickIntervalSeconds=2m;
                immolate.IncludeExpirationBoundaryTick=true;
                list.Add(Cast("immolation",0m,8m,0m,immolate));
                break;
            case "doomguard":
                list.Add(Cast("doom-bolt",220m,3m,2.5m,
                    Damage("doom-bolt-hit","shadow",tuning.SpellBaseDamage*1.4m)));
                break;
        }
        return list;
    }
}
