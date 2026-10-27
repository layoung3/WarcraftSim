using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Data.Forever.Shaman;

/// <summary>
/// CP143 initial Restoration subset. Declared talent points can include other
/// combat/utility talents, but only listed ranks are recognized as wired.
/// Baseline numbers are illustrative, not verified level-60 ranks.
/// </summary>
public sealed class ForeverRestorationShamanTalents
{
    public int RestorationPointsSpent { get; set; } = 41;
    public int OtherTreePointsSpent { get; set; } = 10;
    public Dictionary<string, int> Ranks { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["tidal-focus"] = 5, ["mindfulness"] = 3,
        ["tidal-mastery"] = 5, ["healing-way"] = 3,
        ["water-shield"] = 1, ["mana-tide-totem"] = 1,
        ["restorative-totems"] = 5, ["riptide"] = 1
    };
    public int Rank(string key) => Ranks.TryGetValue(key, out var v) ? v : 0;
    private static readonly Dictionary<string, int> Caps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["tidal-focus"] = 5, ["totemic-focus"] = 5, ["mindfulness"] = 3,
        ["natural-grace"] = 3, ["improved-reincarnation"] = 2,
        ["ancestral-healing"] = 3, ["purification"] = 5,
        ["healing-focus"] = 3,
        ["nature-swiftness"] = 1,
        ["tidal-mastery"] = 5, ["healing-way"] = 3,
        ["water-shield"] = 1, ["mana-tide-totem"] = 1,
        ["restorative-totems"] = 5, ["riptide"] = 1
    };
    public void Validate()
    {
        if (Ranks is null || RestorationPointsSpent is < 0 or > 51 ||
            OtherTreePointsSpent is < 0 or > 51 || RestorationPointsSpent + OtherTreePointsSpent > 51)
            throw new ArgumentException("Invalid Restoration Shaman talent budget.");
        foreach (var (key, value) in Ranks)
            if (!Caps.TryGetValue(key, out var cap) || value < 0 || value > cap)
                throw new ArgumentException($"Unsupported or invalid Restoration talent {key}={value}.");
        if (Ranks.Values.Sum() > RestorationPointsSpent ||
            Rank("water-shield") > 0 && RestorationPointsSpent < 11 ||
            Rank("mana-tide-totem") > 0 && RestorationPointsSpent < 16 ||
            Rank("nature-swiftness") > 0 && RestorationPointsSpent < 21 ||
            Rank("riptide") > 0 && RestorationPointsSpent < 31 ||
            Rank("riptide") > 0 && Rank("healing-way") == 0)
            throw new ArgumentException("Restoration talent milestone or prerequisite not met.");
    }
}

public sealed class ForeverRestorationShamanTuning
{
    public decimal MaximumMana { get; set; } = 5400m;
    public decimal MaximumHealth { get; set; } = 4200m;
    public decimal HealingPower { get; set; } = 400m;
    // Level-60 values not published. Rank coefficient is deliberately configurable.
    public decimal PurificationHealingPercentPerRank { get; set; } = 2m;
    public decimal HealingCritPercent { get; set; } = 14m;
    public decimal SpiritRegenPerSecond { get; set; } = 15m;
    public decimal Mp5 { get; set; } = 18m;
    public decimal HealingWaveBase { get; set; } = 1750m;
    public decimal HealingWaveMana { get; set; } = 580m;
    public decimal LesserHealingWaveBase { get; set; } = 930m;
    public decimal LesserHealingWaveMana { get; set; } = 390m;
    public decimal ChainHealBase { get; set; } = 1060m;
    public decimal ChainHealMana { get; set; } = 620m;
    public decimal ChainHealBounceMultiplier { get; set; } = .5m;
    public decimal RiptideDirectBase { get; set; } = 740m;
    public decimal RiptidePeriodicTick { get; set; } = 175m;
    public decimal RiptideMana { get; set; } = 360m;
    public decimal HealingStreamTick { get; set; } = 95m;
    public decimal HealingStreamMana { get; set; } = 110m;
    public decimal WaterShieldManaPerCharge { get; set; } = 140m;
    // Illustration-only rank effect (armor increase, not flat damage reduction).
    // 8%/rank is an explicit preview assumption pending client data verification.
    public decimal AncestralHealingArmorPercentPerRank { get; set; } = 8m;
    public decimal ArmorReductionConstant { get; set; } = 10557.5m;
    public decimal ManaTideTickPercent { get; set; } = 6m;
    public void Validate()
    {
        if (MaximumMana <= 0m || MaximumHealth <= 0m || HealingPower < 0m ||
            HealingCritPercent is < 0m or > 100m || PurificationHealingPercentPerRank < 0m ||
            SpiritRegenPerSecond < 0m || Mp5 < 0m ||
            HealingWaveBase <= 0m || LesserHealingWaveBase <= 0m || ChainHealBase <= 0m ||
            HealingWaveMana < 0m || LesserHealingWaveMana < 0m || ChainHealMana < 0m ||
            ChainHealBounceMultiplier is < 0m or > 1m || RiptideDirectBase <= 0m ||
            RiptidePeriodicTick <= 0m || RiptideMana < 0m || HealingStreamTick < 0m ||
            HealingStreamMana < 0m || WaterShieldManaPerCharge < 0m ||
            ManaTideTickPercent is < 0m or > 100m ||
            AncestralHealingArmorPercentPerRank < 0m || ArmorReductionConstant <= 0m)
            throw new ArgumentException("Invalid provisional Restoration Shaman tuning.");
    }
}

public static class ForeverRestorationShamanSpellbook
{
    public static IReadOnlyList<AbilityDefinition> Create(ForeverRestorationShamanTalents talents,
        ForeverRestorationShamanTuning t)
    {
        talents.Validate(); t.Validate();
        decimal tidalFocus = 1m - talents.Rank("tidal-focus") * .01m;
        decimal healingWay = 1m + talents.Rank("healing-way") * .02m;
        decimal purification = 1m + talents.Rank("purification") *
            t.PurificationHealingPercentPerRank / 100m;
        AbilityEffectDefinition heal(string key, decimal amount, bool periodic = false) => new()
        {
            Key = key, EffectType = periodic ? AbilityEffectTypes.PeriodicHealing : AbilityEffectTypes.DirectHealing,
            TargetType = AbilityTargetTypes.Friendly, SchoolKey = "nature",
            ResolutionType = CombatResolutionTypes.Spell, CanMiss = false, CanCrit = true,
            MinimumValue = amount * purification, MaximumValue = amount * purification
        };
        AbilityDefinition spell(string key, string name, decimal mana, decimal cast, params AbilityEffectDefinition[] effects) => new()
        {
            Key = key, Name = name, ClassKey = "shaman", SpecializationKey = "restoration",
            CastTimeSeconds = cast, GlobalCooldownSeconds = 1.5m,
            ResourceCosts = [new AbilityResourceCost { ResourceKey = "mana", Amount = mana }],
            Effects = effects.ToList()
        };
        var hw = spell("healing-wave", "Healing Wave", t.HealingWaveMana * tidalFocus, 3m,
            heal("healing-wave-heal", (t.HealingWaveBase + t.HealingPower * .857m) * healingWay));
        var lhw = spell("lesser-healing-wave", "Lesser Healing Wave", t.LesserHealingWaveMana * tidalFocus, 1.5m,
            heal("lesser-healing-wave-heal", t.LesserHealingWaveBase + t.HealingPower * .429m));
        // The direct primary heal uses the core pipeline. Additional jumps are resolved
        // by the dedicated processor, using live health and per-bounce falloff.
        var chain = spell("chain-heal", "Chain Heal", t.ChainHealMana * tidalFocus, 2.5m,
            heal("chain-heal-primary", t.ChainHealBase + t.HealingPower * .714m));
        var spells = new List<AbilityDefinition> { hw, lhw, chain };
        if (talents.Rank("riptide") > 0)
        {
            var hot = heal("riptide-tick", t.RiptidePeriodicTick + t.HealingPower * .10m, true);
            hot.AuraKey = "riptide"; hot.DurationSeconds = 15m;
            hot.TickIntervalSeconds = 3m; hot.IncludeExpirationBoundaryTick = true;
            var riptide = spell("riptide", "Riptide", t.RiptideMana * tidalFocus, 0m,
                heal("riptide-initial", t.RiptideDirectBase + t.HealingPower * .20m), hot);
            riptide.CooldownSeconds = 6m;
            spells.Add(riptide);
        }
        if (talents.Rank("water-shield") > 0)
        {
            var shield = spell("water-shield", "Water Shield", 0m, 0m,
                new AbilityEffectDefinition { Key = "water-shield-aura",
                    EffectType = AbilityEffectTypes.ApplyAura, AuraKey = "water-shield",
                    TargetType = AbilityTargetTypes.Self, ResolutionType = CombatResolutionTypes.AlwaysHits,
                    CanMiss = false, CanCrit = false, DurationSeconds = 600m });
            shield.ForbiddenSourceAuraKeys.Add("water-shield");
            spells.Add(shield);
        }
        if (talents.Rank("nature-swiftness") > 0)
        {
            var swift = spell("nature-swiftness", "Nature's Swiftness", 0m, 0m,
                new AbilityEffectDefinition { Key = "nature-swiftness-aura",
                    EffectType = AbilityEffectTypes.ApplyAura, AuraKey = "nature-swiftness-ready",
                    TargetType = AbilityTargetTypes.Self, DurationSeconds = 30m,
                    ResolutionType = CombatResolutionTypes.AlwaysHits, CanMiss = false, CanCrit = false });
            swift.CooldownSeconds = 180m;
            swift.IsOffGlobalCooldown = true;
            swift.ForbiddenSourceAuraKeys.Add("nature-swiftness-ready");
            spells.Add(swift);
        }
        if (talents.Rank("mana-tide-totem") > 0)
        {
            var tide = spell("mana-tide-totem", "Mana Tide Totem", 0m, 0m,
                new AbilityEffectDefinition { Key = "mana-tide-aura",
                    EffectType = AbilityEffectTypes.ApplyAura, AuraKey = "mana-tide-active",
                    TargetType = AbilityTargetTypes.Self, DurationSeconds = 12m,
                    ResolutionType = CombatResolutionTypes.AlwaysHits, CanMiss = false, CanCrit = false });
            tide.CooldownSeconds = 300m; spells.Add(tide);
        }
        // October 16: Restorative Totems now modifies the entire heal per tick;
        // provisional rank scaling centralized here until endgame client import.
        // Self-owned totem state; the processor chooses an injured ally on each
        // pulse instead of sticking a 120-second healing aura on the cast target.
        var stream = spell("healing-stream-totem", "Healing Stream Totem",
            t.HealingStreamMana * (1m - talents.Rank("totemic-focus") * .05m), 0m,
            new AbilityEffectDefinition {
                Key = "healing-stream-aura", EffectType = AbilityEffectTypes.ApplyAura,
                TargetType = AbilityTargetTypes.Self, AuraKey = "healing-stream-totem",
                DurationSeconds = 300m, ResolutionType = CombatResolutionTypes.AlwaysHits,
                CanMiss = false, CanCrit = false
            });
        stream.GlobalCooldownSeconds = 1m;
        stream.ForbiddenSourceAuraKeys.Add("healing-stream-totem");
        stream.ForbiddenSourceAuraKeys.Add("mana-tide-active");
        spells.Add(stream);
        return spells;
    }
}
