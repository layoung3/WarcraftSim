using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Data.Forever.Shaman;

/// <summary>CP130 Forever Enhancement combat spells with explicit, adjustable
/// provisional max-level tuning. Weapon procs are handled by shared Core.</summary>
public static class ForeverEnhancementShamanSpellbook
{
    public static IReadOnlyList<AbilityDefinition> Create(ForeverEnhancementShamanTuning tuning,
        ForeverEnhancementShamanTalents talents, decimal spellPower, decimal weaponSpeed,
        decimal minimumWeaponDamage, decimal maximumWeaponDamage, bool dualWield = false)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        ArgumentNullException.ThrowIfNull(talents);
        if (spellPower < 0m || weaponSpeed <= 0m || minimumWeaponDamage < 0m ||
            maximumWeaponDamage < minimumWeaponDamage || tuning.EarthShockDamage < 0m ||
            tuning.LightningBoltDamage < 0m)
            throw new ArgumentException("Invalid Enhancement spellbook values.");
        AbilityDefinition spell(string key, string name, decimal mana, decimal cast, decimal cd,
            AbilityEffectDefinition effect) => new()
        {
            Key = key, Name = name, ClassKey = "shaman", SpecializationKey = "enhancement",
            CastTimeSeconds = cast, GlobalCooldownSeconds = 1.5m, CooldownSeconds = cd,
            ResourceCosts = [new AbilityResourceCost { ResourceKey = "mana", Amount = Math.Max(0m, mana) }],
            Effects = [effect]
        };
        AbilityEffectDefinition nature(string key, decimal amount) => new()
        {
            Key = key, EffectType = AbilityEffectTypes.DirectDamage,
            TargetType = AbilityTargetTypes.Enemy,
            SchoolKey = "nature", ResolutionType = CombatResolutionTypes.Spell,
            MitigationType = DamageMitigationTypes.None, CanMiss = true, CanCrit = true,
            MinimumValue = amount, MaximumValue = amount,
            DamageMultiplierStatKey = "shaman-stormstrike-nature-percent"
        };
        var focus = talents.Rank("shamanistic-focus") > 0 ? .55m : 1m;
        var shock = spell("earth-shock", "Earth Shock", tuning.EarthShockMana * focus,
            0m, 6m, nature("earth-shock-hit", tuning.EarthShockDamage + spellPower * .429m));
        var lightningDamage = tuning.LightningBoltDamage + spellPower * .714m;
        var lightning = spell("lightning-bolt", "Lightning Bolt", tuning.LightningBoltMana,
            2.5m, 0m, nature("lightning-bolt-hit", lightningDamage));
        var instant = spell("maelstrom-lightning-bolt", "Maelstrom Lightning Bolt", 0m,
            0m, 0m, nature("maelstrom-bolt-hit", lightningDamage));
        instant.RequiredSourceAuraKeys.Add("maelstrom-ready");
        var result = new List<AbilityDefinition> { shock, lightning };
        if (talents.Rank("maelstrom-weapon") > 0) result.Add(instant);
        if (talents.Rank("stormstrike") > 0)
        {
            var melee = new AbilityEffectDefinition
            {
                Key = "stormstrike-hit", EffectType = AbilityEffectTypes.DirectDamage,
                TargetType = AbilityTargetTypes.Enemy, SchoolKey = "physical",
                WeaponHandKey = WeaponHandKeys.MainHand,
                ResolutionType = ForeverCombatResolutionTypes.PlayerMeleeSpecial,
                AttackSkillStatKey = "main-hand-weapon-skill",
                MitigationType = DamageMitigationTypes.Armor,
                CanMiss = true, CanCrit = true, CanBeDodged = true, CanBeParried = true,
                MinimumValue = minimumWeaponDamage, MaximumValue = maximumWeaponDamage,
                ScalingStatKey = "attack-power",
                // Physical instant attacks use normalized AP contribution,
                // not live auto-attack weapon speed (Stage 2 contract).
                ScalingCoefficient = (dualWield
                    ? ForeverNormalizedWeaponSpeeds.OneHanded
                    : ForeverNormalizedWeaponSpeeds.TwoHanded) / 14m
            };
            result.Add(spell("stormstrike", "Stormstrike", 125m, 0m, 8m, melee));
        }
        return result;
    }
}

public sealed class ForeverEnhancementShamanTuning
{
    public decimal EarthShockDamage { get; set; } = 410m;
    public decimal EarthShockMana { get; set; } = 430m;
    public decimal LightningBoltDamage { get; set; } = 540m;
    public decimal LightningBoltMana { get; set; } = 320m;
    public decimal WindfuryWeaponProcChancePercent { get; set; } = 20m;
    public decimal WindfuryWeaponBonusAttackPower { get; set; } = 333m;
    public decimal WindfuryWeaponInternalCooldownSeconds { get; set; } = 1.5m;
    public decimal MaelstromChancePerMeleePercentPerRank { get; set; } = 4m;
    public decimal ManaRegenPerSecond { get; set; } = 8m;
}
