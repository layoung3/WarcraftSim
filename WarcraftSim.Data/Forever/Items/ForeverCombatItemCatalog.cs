using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Data.Forever.Items;

/// <summary>
/// Reusable Forever level-60 active item definitions. Items are regular abilities:
/// their resource/HP effects, damage, own cooldowns and shared cooldowns are
/// resolved by the standard combat engine. Inventory and usage strategy live in
/// CombatUseItemProcessor, not in the spell definitions.
/// </summary>
public static class ForeverCombatItemCatalog
{
    public const string ManaRuby = "mana-ruby";
    public const string ManaCitrine = "mana-citrine";
    public const string ManaJade = "mana-jade";
    public const string ManaAgate = "mana-agate";
    public const string DarkRune = "dark-rune";
    public const string DemonicRune = "demonic-rune";
    public const string MightyRagePotion = "mighty-rage-potion";
    public const string MajorHealingPotion = "major-healing-potion";
    public const string DenseDynamite = "dense-dynamite";
    public const string GoblinSapperCharge = "goblin-sapper-charge";
    public const string MightyRageStrengthAura = "item-mighty-rage-strength";

    public static IReadOnlyList<AbilityDefinition> CreateAll() =>
    [
        CreateManaRuby(), CreateManaGem(ManaCitrine, "Mana Citrine", 775m, 925m),
        CreateManaGem(ManaJade, "Mana Jade", 550m, 650m),
        CreateManaGem(ManaAgate, "Mana Agate", 375m, 425m),
        CreateRune(DarkRune, "Dark Rune"),
        CreateRune(DemonicRune, "Demonic Rune"), CreateMightyRagePotion(),
        CreateMajorHealingPotion(),
        CreateDenseDynamite(), CreateGoblinSapperCharge()
    ];

    private static AbilityDefinition Item(string key, string name, decimal cooldown,
        string group, decimal groupSeconds, params AbilityEffectDefinition[] effects) => new()
    {
        Key = key, Name = name, CooldownSeconds = cooldown,
        SharedCooldownGroupKey = group, SharedCooldownSeconds = groupSeconds,
        GlobalCooldownSeconds = 0m, IsOffGlobalCooldown = true,
        Effects = effects.ToList(), Tags = ["combat-use-item"]
    };

    private static AbilityEffectDefinition Gain(string key, string resource,
        decimal minimum, decimal maximum) => new()
    {
        Key = key, EffectType = AbilityEffectTypes.ResourceChange,
        TargetType = AbilityTargetTypes.Self, ResourceKey = resource,
        ResourceChangeOperation = ResourceChangeOperationTypes.Gain,
        ResolutionType = CombatResolutionTypes.AlwaysHits, CanMiss = false, CanCrit = false,
        MinimumValue = minimum, MaximumValue = maximum
    };

    private static AbilityEffectDefinition HealthCost(string key, decimal minimum, decimal maximum) => new()
    {
        Key = key, EffectType = AbilityEffectTypes.HealthChange,
        TargetType = AbilityTargetTypes.Self,
        HealthChangeOperation = HealthChangeOperationTypes.Damage,
        ResolutionType = CombatResolutionTypes.AlwaysHits, CanMiss = false, CanCrit = false,
        MinimumValue = minimum, MaximumValue = maximum
    };

    private static AbilityEffectDefinition FireDamage(string key, decimal minimum, decimal maximum) => new()
    {
        Key = key, EffectType = AbilityEffectTypes.DirectDamage,
        TargetType = AbilityTargetTypes.Enemy, SchoolKey = "fire",
        ResolutionType = CombatResolutionTypes.AlwaysHits,
        MitigationType = DamageMitigationTypes.None,
        CanMiss = false, CanCrit = false,
        MinimumValue = minimum, MaximumValue = maximum
    };

    public static AbilityDefinition CreateManaRuby() => CreateManaGem(ManaRuby, "Mana Ruby", 1000m, 1200m);
    // Client-backed 1.60.1.70374+ conjure tooltips. Exact shared group policy
    // remains provisional pending a live Forever multi-gem cooldown observation.
    public static AbilityDefinition CreateManaGem(string key, string name, decimal min, decimal max)
    {
        if(key is not (ManaRuby or ManaCitrine or ManaJade or ManaAgate))
            throw new ArgumentException("Unknown conjured mana gem.", nameof(key));
        return Item(key,name,120m,"mage-mana-gem",120m,
            Gain(key+"-gain","mana",min,max));
    }

    public static AbilityDefinition CreateRune(string key, string name)
    {
        if (key is not (DarkRune or DemonicRune))
            throw new ArgumentException("Unknown rune item.", nameof(key));
        // Health cost precedes the mana restore; a low-health caster should never
        // receive a free restore just because the health effect would be lethal.
        return Item(key, name, 120m, "mana-rune", 120m,
            HealthCost(key + "-health", 600m, 1000m),
            Gain(key + "-mana", "mana", 900m, 1500m));
    }

    /// <summary>Forever spell 17534, item 13446: heal 1050–1750;
    /// uses the shared 120s combat-potion group (provisional server policy).
    /// </summary>
    public static AbilityDefinition CreateMajorHealingPotion() => Item(
        MajorHealingPotion,"Major Healing Potion",120m,"combat-potion",120m,
        new AbilityEffectDefinition {
            Key="major-healing-potion-heal",EffectType=AbilityEffectTypes.DirectHealing,
            TargetType=AbilityTargetTypes.Self,ResolutionType=CombatResolutionTypes.AlwaysHits,
            CanMiss=false,CanCrit=false,MinimumValue=1050m,MaximumValue=1750m
        });

    public static AbilityDefinition CreateMightyRagePotion() => Item(MightyRagePotion,
        "Mighty Rage Potion", 120m, "combat-potion", 120m,
        Gain("mighty-rage", "rage", 45m, 75m),
        new AbilityEffectDefinition
        {
            Key = "mighty-rage-strength", EffectType = AbilityEffectTypes.ApplyAura,
            TargetType = AbilityTargetTypes.Self, AuraKey = MightyRageStrengthAura,
            DurationSeconds = 20m, ResolutionType = CombatResolutionTypes.AlwaysHits,
            CanMiss = false, CanCrit = false
        });

    public static AbilityDefinition CreateDenseDynamite() => Item(DenseDynamite,
        "Dense Dynamite", 60m, "engineering-explosive", 60m,
        FireDamage("dense-dynamite-fire", 340m, 460m));

    public static AbilityDefinition CreateGoblinSapperCharge() => Item(GoblinSapperCharge,
        "Goblin Sapper Charge", 300m, "engineering-explosive", 60m,
        FireDamage("sapper-fire", 450m, 750m),
        HealthCost("sapper-self-damage", 375m, 625m));
}
