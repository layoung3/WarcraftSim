using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Data.Forever.Warlock;

/// <summary>Level-60 provisional spell definitions for the three Forever Warlock DPS
/// trees. Actual client coefficients and cast costs are supplied as tuning inputs.</summary>
public static class ForeverWarlockDpsSpellbook
{
    public static IReadOnlyList<AbilityDefinition> Create(string spec, ForeverWarlockTalentBuild talents,
        ForeverWarlockTuning tuning, decimal spellPower, decimal spirit)
    {
        ArgumentNullException.ThrowIfNull(talents);
        ArgumentNullException.ThrowIfNull(tuning);
        if (spellPower < 0 || spirit < 0 || tuning.ShadowBoltDamage < 0 || tuning.CorruptionTick < 0 ||
            tuning.ImmolateTick < 0 || tuning.IncinerateDamage < 0 || tuning.ManaRegenerationPerSecond < 0 ||
            tuning.BaneTick < 0 || tuning.ShadowBoltDamage < 0 || tuning.WrackTick < 0 ||
            tuning.SoulFireDamage < 0 || tuning.ImmolateDirect < 0 || tuning.ConflagrateDamage < 0 ||
            tuning.CorruptionMana < 0 || tuning.BaneMana < 0 || tuning.ImmolateMana < 0 ||
            tuning.ShadowBoltMana < 0 || tuning.WrackMana < 0 || tuning.SoulFireMana < 0 ||
            tuning.IncinerateMana < 0 || tuning.ConflagrateMana < 0 || tuning.LifeTapSpiritCoefficient < 0)
            throw new ArgumentException("Warlock tuning cannot contain negative damage, power or regeneration.");
        // Grim Reach replaced Malediction; range does not add periodic damage.
        const decimal periodicBonus = 1m;
        var shadowBonus = 1m + talents.Rank("shadow-mastery") * .10m / 5m;
        decimal cost(decimal mana, bool dest = false) => Math.Max(0m, mana *
            (dest ? 1m - (talents.Rank("cataclysm") switch { 1 => .03m, 2 => .07m, 3 => .10m, _ => 0m }) : 1m));
        AbilityEffectDefinition hit(string key, string school, decimal amount) => new()
        {
            Key = key, EffectType = AbilityEffectTypes.DirectDamage, SchoolKey = school,
            TargetType = AbilityTargetTypes.Enemy, ResolutionType = CombatResolutionTypes.Spell,
            MitigationType = DamageMitigationTypes.None, CanMiss = true, CanCrit = true,
            MinimumValue = Math.Max(0m, amount), MaximumValue = Math.Max(0m, amount)
        };
        AbilityEffectDefinition dot(string key, string aura, string school, decimal tick,
            decimal duration, decimal every = 3m)
        {
            var effect = hit(key, school, tick);
            effect.EffectType = AbilityEffectTypes.PeriodicDamage;
            effect.AuraKey = aura; effect.DurationSeconds = duration;
            effect.TickIntervalSeconds = every; effect.IncludeExpirationBoundaryTick = true;
            return effect;
        }
        AbilityDefinition spell(string key, string name, decimal mana, decimal cast, params AbilityEffectDefinition[] effects) => new()
        {
            Key = key, Name = name, ClassKey = "warlock", SpecializationKey = spec,
            CastTimeSeconds = cast, GlobalCooldownSeconds = 1.5m,
            ResourceCosts = mana > 0m ? [new AbilityResourceCost { ResourceKey = "mana", Amount = mana }] : [],
            Effects = effects.ToList()
        };

        var corruption = spell("corruption", "Corruption", cost(tuning.CorruptionMana),
            Math.Max(0m, 2m - .4m * talents.Rank("improved-corruption")),
            dot("corruption-tick", "corruption", "shadow",
                (tuning.CorruptionTick + spellPower * .17m) * periodicBonus * shadowBonus *
                (1m + talents.Rank("improved-corruption") * .02m), 18m));
        var bane = spell("bane-of-agony", "Bane of Agony", cost(tuning.BaneMana), 0m,
            dot("bane-tick", "bane-of-agony", "shadow",
                (tuning.BaneTick + spellPower * .10m) * periodicBonus * shadowBonus, 24m, 2m));
        var immolate = spell("immolate", "Immolate", cost(tuning.ImmolateMana, true), 2m,
            hit("immolate-impact", "fire", tuning.ImmolateDirect + spellPower * .20m),
            dot("immolate-tick", "immolate", "fire",
                (tuning.ImmolateTick + spellPower * .13m) * periodicBonus, 15m));
        var bolt = spell("shadow-bolt", "Shadow Bolt", cost(tuning.ShadowBoltMana, true),
            Math.Max(1m, 3m - .10m * talents.Rank("bane")),
            hit("shadow-bolt-impact", "shadow", (tuning.ShadowBoltDamage + spellPower * .857m) * shadowBonus));
        var spells = new List<AbilityDefinition> { corruption, bane, immolate, bolt };
        var lifeMana = Math.Max(0m, tuning.LifeTapMana + spirit * tuning.LifeTapSpiritCoefficient) *
            (1m + talents.Rank("improved-life-tap") * .10m);
        spells.Add(spell("life-tap", "Life Tap", 0m, 0m,
            new AbilityEffectDefinition { Key = "life-tap-health", EffectType = AbilityEffectTypes.HealthChange,
                TargetType = AbilityTargetTypes.Self, ResolutionType = CombatResolutionTypes.AlwaysHits,
                CanMiss = false, CanCrit = false, HealthChangeOperation = HealthChangeOperationTypes.Damage,
                MinimumValue = tuning.LifeTapHealthCost, MaximumValue = tuning.LifeTapHealthCost },
            new AbilityEffectDefinition { Key = "life-tap-mana", EffectType = AbilityEffectTypes.ResourceChange,
                TargetType = AbilityTargetTypes.Self, ResolutionType = CombatResolutionTypes.AlwaysHits,
                CanMiss = false, CanCrit = false, ResourceChangeOperation = ResourceChangeOperationTypes.Gain,
                ResourceKey = "mana", MinimumValue = lifeMana, MaximumValue = lifeMana }));
        if (spec == "affliction")
        {
            var wrackTick = hit("wrack-tick", "shadow", (tuning.WrackTick + spellPower * .12m) *
                periodicBonus * shadowBonus * (1m + talents.Rank("improved-drains") * .20m / 3m));
            wrackTick.ApplyOnChannelTick = true;
            var wrack = spell("wrack", "Wrack", cost(tuning.WrackMana), 0m, wrackTick,
                new AbilityEffectDefinition { Key = "wrack-vulnerability-effect",
                    EffectType = AbilityEffectTypes.ApplyAura,
                    AuraKey = "wrack-vulnerability", DurationSeconds = 6m,
                    TargetType = AbilityTargetTypes.Enemy,
                    CanMiss = false, CanCrit = false,
                    ResolutionType = CombatResolutionTypes.AlwaysHits });
            wrack.ChannelDurationSeconds = 6m; wrack.ChannelTickIntervalSeconds = 1m;
            if (talents.Rank("wrack") > 0) spells.Add(wrack);
        }
        if (spec == "demonology")
        {
            var soulFire = spell("soul-fire", "Soul Fire", cost(tuning.SoulFireMana),
                Math.Max(1m, 6m - .4m * talents.Rank("bane")),
                hit("soul-fire-impact", "fire", tuning.SoulFireDamage + spellPower * 1.0m));
            // Forever Soul Fire has a 60-second cooldown (before Decimation).
            // Soul Shard inventory is not yet modeled: assume a shard is available.
            soulFire.CooldownSeconds = 60m;
            spells.Add(soulFire);
        }
        if (spec == "destruction")
        {
            if (talents.Rank("incinerate") > 0)
                spells.Add(spell("incinerate", "Incinerate", cost(tuning.IncinerateMana, true),
                    Math.Max(1m, 2.5m - .1m * talents.Rank("bane")),
                    hit("incinerate-impact", "fire", tuning.IncinerateDamage + spellPower * .714m)));
            var conflagrate = spell("conflagrate", "Conflagrate", cost(tuning.ConflagrateMana, true), 0m,
                hit("conflagrate-impact", "fire", tuning.ConflagrateDamage + spellPower * .429m));
            conflagrate.CooldownSeconds = 10m;
            // Conflagrate normally consumes Immolate in this preliminary pass.
            // Selectable talents can prevent consumption in a later proc pass.
            spells.Add(conflagrate);
        }
        return spells;
    }
}

public sealed class ForeverWarlockTuning
{
    // Explicit, provisional level-60 beta values (not guaranteed client ranks).
    public decimal CorruptionTick { get; set; } = 130m;
    public decimal CorruptionMana { get; set; } = 320m;
    public decimal BaneTick { get; set; } = 90m;
    public decimal BaneMana { get; set; } = 290m;
    public decimal ImmolateDirect { get; set; } = 230m;
    public decimal ImmolateTick { get; set; } = 78m;
    public decimal ImmolateMana { get; set; } = 380m;
    public decimal ShadowBoltDamage { get; set; } = 268m;
    public decimal ShadowBoltMana { get; set; } = 360m;
    public decimal WrackTick { get; set; } = 115m;
    public decimal WrackMana { get; set; } = 240m;
    public decimal SoulFireDamage { get; set; } = 431m;
    public decimal SoulFireMana { get; set; } = 335m;
    public decimal IncinerateDamage { get; set; } = 320m;
    public decimal IncinerateMana { get; set; } = 365m;
    public decimal ConflagrateDamage { get; set; } = 420m;
    public decimal ConflagrateMana { get; set; } = 310m;
    public decimal LifeTapMana { get; set; } = 550m;
    public decimal LifeTapHealthCost { get; set; } = 350m;
    public decimal LifeTapSpiritCoefficient { get; set; } = 1m;
    public decimal ManaRegenerationPerSecond { get; set; } = 5m;
    public decimal PetBaseDamage { get; set; } = 65m;
    public decimal PetAttackPowerFromIntellect { get; set; } = 1m;
    public decimal PetSwingSeconds { get; set; } = 2m;
}
