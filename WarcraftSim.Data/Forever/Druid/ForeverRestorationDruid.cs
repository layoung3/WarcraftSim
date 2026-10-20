using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Data.Forever.Druid;

/// <summary>Connected Restoration subset. Unsupported talent keys are rejected.</summary>
public sealed class ForeverRestorationDruidTalents
{
    public Dictionary<string,int> Ranks { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string,int> Caps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["improved-rejuvenation"] = 3, ["gift-of-nature"] = 5,
        ["genesis"] = 5, ["living-spirit"] = 3,
        ["improved-tranquility"] = 2, ["swiftmend"] = 1,
        ["wild-growth"] = 1, ["gift-of-the-earthmother"] = 1,
        ["natures-swiftness"] = 1, ["naturalist"] = 5, ["subtlety"] = 3,
        ["improved-regrowth"] = 5, ["natures-focus"] = 5
    };
    public int Rank(string name) => Ranks?.GetValueOrDefault(name) ?? 0;
    public void Validate()
    {
        if (Ranks is null) throw new ArgumentException("Druid talent map required.");
        foreach (var (name, rank) in Ranks)
            if (!Caps.TryGetValue(name,out var cap) || rank < 0 || rank > cap)
                throw new ArgumentException($"Unsupported Restoration talent: {name}={rank}.");
    }
}
public sealed class ForeverRestorationDruidTuning
{
    public decimal MaximumHealth { get; set; } = 4200m;
    public decimal MaximumMana { get; set; } = 6100m;
    public decimal SpellHealingPower { get; set; } = 500m;
    public decimal CriticalChancePercent { get; set; } = 14m;
    public decimal ManaRegenerationPerSecond { get; set; } = 8m;
    public decimal HealingTouchBase { get; set; } = 1800m;
    public decimal HealingTouchCost { get; set; } = 610m;
    public decimal RejuvenationTick { get; set; } = 260m;
    public decimal RejuvenationCost { get; set; } = 330m;
    public decimal RegrowthDirect { get; set; } = 1150m;
    public decimal RegrowthTick { get; set; } = 140m;
    public decimal RegrowthCost { get; set; } = 520m;
    public decimal TranquilityTick { get; set; } = 600m;
    public decimal TranquilityCost { get; set; } = 1200m;
    public decimal WildGrowthInitialTick { get; set; } = 245m;
    public decimal WildGrowthCost { get; set; } = 710m;
    public decimal SwiftmendHeal { get; set; } = 1100m;
    public decimal SwiftmendCost { get; set; } = 300m;
    public decimal NaturesSwiftnessCooldownSeconds { get; set; } = 180m;
    public void Validate()
    {
        var values = new[] { MaximumHealth,MaximumMana,SpellHealingPower,CriticalChancePercent,
            ManaRegenerationPerSecond,HealingTouchBase,HealingTouchCost,RejuvenationTick,
            RejuvenationCost,RegrowthDirect,RegrowthTick,RegrowthCost,TranquilityTick,
            TranquilityCost,WildGrowthInitialTick,WildGrowthCost,SwiftmendHeal,SwiftmendCost,NaturesSwiftnessCooldownSeconds };
        if (values.Any(x=>x<0m) || MaximumHealth<=0m || MaximumMana<=0m ||
            CriticalChancePercent>100m) throw new ArgumentException("Invalid Restoration Druid tuning.");
    }
}
public static class ForeverRestorationDruidSpellbook
{
    public static IReadOnlyList<AbilityDefinition> Create(ForeverRestorationDruidTalents talents,
        ForeverRestorationDruidTuning t)
    {
        talents.Validate();t.Validate();
        var gift = 1m + talents.Rank("gift-of-nature") * .02m;
        var gen = 1m + talents.Rank("genesis") * .01m;
        AbilityEffectDefinition direct(string key,decimal value) => new() {
            Key=key, EffectType=AbilityEffectTypes.DirectHealing,
            TargetType=AbilityTargetTypes.Friendly, SchoolKey="nature",
            ResolutionType=CombatResolutionTypes.Spell, CanMiss=false,CanCrit=true,
            MinimumValue=value,MaximumValue=value
        };
        AbilityEffectDefinition hot(string key,string aura,decimal tick,decimal duration,decimal interval) => new() {
            Key=key, EffectType=AbilityEffectTypes.PeriodicHealing,
            TargetType=AbilityTargetTypes.Friendly, SchoolKey="nature",
            ResolutionType=CombatResolutionTypes.Spell,CanMiss=false,CanCrit=true,
            MinimumValue=tick*gen*gift,MaximumValue=tick*gen*gift,
            AuraKey=aura,DurationSeconds=duration,TickIntervalSeconds=interval,
            IncludeExpirationBoundaryTick=true
        };
        AbilityDefinition spell(string key,decimal mana,decimal cast,params AbilityEffectDefinition[] effects) => new() {
            Key=key,Name=key,ClassKey="druid",SpecializationKey="restoration",
            CastTimeSeconds=cast,GlobalCooldownSeconds=1.5m,
            ResourceCosts=[new AbilityResourceCost{ResourceKey="mana",Amount=mana}],
            Effects=effects.ToList()
        };
        var ht = spell("healing-touch",t.HealingTouchCost,
            Math.Max(0m,3.5m - talents.Rank("naturalist") * .1m),
            direct("healing-touch-heal",(t.HealingTouchBase+t.SpellHealingPower)*gift));
        var rejuv = spell("rejuvenation",t.RejuvenationCost,0m,
            hot("rejuvenation-tick","rejuvenation",
                (t.RejuvenationTick + t.SpellHealingPower*.2m)*
                (1m+talents.Rank("improved-rejuvenation")*.05m),12m,3m));
        var regrowth = spell("regrowth",t.RegrowthCost,2m,
            direct("regrowth-initial",(t.RegrowthDirect+t.SpellHealingPower*.43m)*gift),
            hot("regrowth-tick","regrowth",t.RegrowthTick+t.SpellHealingPower*.09m,21m,3m));
        var tranquility = spell("tranquility",t.TranquilityCost,0m,
            direct("tranquility-primary",(t.TranquilityTick+t.SpellHealingPower*.15m)*gift));
        tranquility.ChannelDurationSeconds=8m;
        tranquility.ChannelTickIntervalSeconds=2m;
        tranquility.Effects[0].ApplyOnChannelTick=true;
        tranquility.CooldownSeconds=300m*(1m-talents.Rank("improved-tranquility")*.30m);
        // Official Forever: Gift of the Earthmother reduces the GCD of these
        // three named spells by 0.5s. No speculative GCD bonus to other heals.
        if (talents.Rank("gift-of-the-earthmother") > 0)
            rejuv.GlobalCooldownSeconds = 1m;
        var list=new List<AbilityDefinition>{ht,rejuv,regrowth,tranquility};
        if (talents.Rank("natures-swiftness") > 0)
        {
            var swiftNature = spell("natures-swiftness", 0m, 0m,
                new AbilityEffectDefinition { Key = "natures-swiftness-ready-effect",
                    EffectType = AbilityEffectTypes.ApplyAura,
                    AuraKey = "druid-natures-swiftness-ready",
                    TargetType = AbilityTargetTypes.Self, DurationSeconds = 12m,
                    ResolutionType = CombatResolutionTypes.AlwaysHits,
                    CanMiss = false, CanCrit = false });
            swiftNature.CooldownSeconds = t.NaturesSwiftnessCooldownSeconds;
            swiftNature.GlobalCooldownSeconds = 0m;
            swiftNature.IsOffGlobalCooldown = true;
            list.Add(swiftNature);
        }
        if(talents.Rank("wild-growth")>0)
        {
            var wild=spell("wild-growth",t.WildGrowthCost,0m,
                new AbilityEffectDefinition {Key="wild-growth-marker",EffectType=AbilityEffectTypes.ApplyAura,
                    TargetType=AbilityTargetTypes.Friendly, AuraKey="wild-growth",
                    DurationSeconds=7m,ResolutionType=CombatResolutionTypes.AlwaysHits,
                    CanMiss=false,CanCrit=false});
            wild.CooldownSeconds=6m;
            if (talents.Rank("gift-of-the-earthmother") > 0)
                wild.GlobalCooldownSeconds = 1m;
            list.Add(wild);
        }
        if(talents.Rank("swiftmend")>0)
        {
            // Swiftmend eligibility is checked in the Restoration processor and
            // preview rotation; no HoT is consumed per Forever's redesigned effect.
            var swift=spell("swiftmend",t.SwiftmendCost,0m,
                direct("swiftmend-heal",(t.SwiftmendHeal+t.SpellHealingPower*.3m)*gift));
            swift.RequiredAnyTargetAuraKeys.Add("rejuvenation");
            swift.RequiredAnyTargetAuraKeys.Add("regrowth");
            swift.CooldownSeconds=15m;
            if (talents.Rank("gift-of-the-earthmother") > 0)
                swift.GlobalCooldownSeconds = 1m;
            list.Add(swift);
        }
        return list;
    }
}
