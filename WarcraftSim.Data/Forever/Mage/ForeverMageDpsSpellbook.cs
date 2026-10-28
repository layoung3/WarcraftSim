using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Data.Forever.Mage;

namespace WarcraftSim.Data.Forever.Mage;

/// <summary>Modeled level-60 Mage spells. All beta ranks and coefficients are explicit
/// tuning assumptions. CP132's specialization processor handles stateful procs.</summary>
public static class ForeverMageDpsSpellbook
{
    public static IReadOnlyList<AbilityDefinition> Create(ForeverMageTalentBuild t, ForeverMageTuning tuning, decimal sp, decimal spirit = 0m, decimal maximumMana = 0m)
    {
        // Forever Arcane Instability increases ALL spell damage, not Arcane
        // damage alone. Fire Power remains a separate dynamic target modifier.
        decimal globalSpellDamage = 1m + t.Rank("arcane-instability") * .01m;
        decimal fire = globalSpellDamage;
        // Piercing Ice is removed in current Forever (build 70532).
        // Do not apply the obsolete Classic Frost-damage passive.
        decimal frost = globalSpellDamage;
        decimal arcane = globalSpellDamage;
        static AbilityEffectDefinition hit(string key, string school, decimal raw, decimal multiplier = 1m) => new()
        {
            Key = key, EffectType = AbilityEffectTypes.DirectDamage,
            TargetType = AbilityTargetTypes.Enemy, SchoolKey = school,
            ResolutionType = CombatResolutionTypes.Spell,
            MitigationType = DamageMitigationTypes.None,
            CanMiss = true, CanCrit = true, MinimumValue = raw, MaximumValue = raw,
            DamageMultiplier = multiplier
        };
        static AbilityEffectDefinition dot(string key, string aura, string school, decimal tick,
            decimal duration, decimal interval) => new()
        {
            Key = key, EffectType = AbilityEffectTypes.PeriodicDamage,
            TargetType = AbilityTargetTypes.Enemy, SchoolKey = school,
            ResolutionType = CombatResolutionTypes.Spell,
            MitigationType = DamageMitigationTypes.None, CanMiss = true, CanCrit = true,
            MinimumValue = tick, MaximumValue = tick, AuraKey = aura,
            DurationSeconds = duration, TickIntervalSeconds = interval,
            IncludeExpirationBoundaryTick = true
        };
        static AbilityDefinition spell(string key, string name, decimal mana, decimal cast, params AbilityEffectDefinition[] effects) => new()
        {
            Key = key, Name = name, ClassKey = "mage", CastTimeSeconds = cast,
            GlobalCooldownSeconds = 1.5m,
            ResourceCosts = mana > 0 ? [new AbilityResourceCost { ResourceKey = "mana", Amount = mana }] : [],
            Effects = effects.ToList()
        };
        decimal frostfireMult = Math.Max(fire, frost); // Provisional dual-school talent benefit.
        var fireball = spell("fireball", "Fireball", tuning.FireballMana,
            Math.Max(1m, tuning.FireballCastSeconds - .1m * t.Rank("improved-fireball")),
            hit("fireball-impact", "fire", tuning.FireballDamage + sp * .857m, fire));
        var frostbolt = spell("frostbolt", "Frostbolt", tuning.FrostboltMana,
            tuning.FrostboltCastSeconds,
            hit("frostbolt-impact", "frost", tuning.FrostboltDamage + sp * .814m, frost));
        var frostfire = spell("frostfire-bolt", "Frostfire Bolt", tuning.FrostfireBoltMana,
            Math.Max(1m, 3m - t.Rank("improved-fireball") * .1m),
            hit("frostfire-impact", "fire", tuning.FrostfireBoltDamage + sp * .857m, frostfireMult),
            dot("frostfire-tick", "frostfire-dot", "fire",
                (tuning.FrostfireDotTick + sp * .08m) * frostfireMult, 9m, 3m));
        frostfire.Effects.Single(e => e.Key == "frostfire-tick")
            .DependsOnEffectKey = "frostfire-impact";
        // 70425: 1.5s, 40% less damage and coefficient, granting timed Int-based Spell Damage stacks.
        var arcaneBlast = spell("arcane-blast", "Arcane Blast", tuning.ArcaneBlastMana, 1.5m,
            hit("arcane-blast-impact", "arcane", tuning.ArcaneBlastDamage * .6m + sp * .4284m, arcane));
        var arcaneWrath = spell("arcane-wrath", "Arcane Wrath", tuning.ArcaneWrathMana, 3.5m,
            hit("arcane-wrath-impact", "arcane",
                tuning.ArcaneWrathDamage + (sp + maximumMana * .05m) * .857m, arcane));
        arcaneWrath.CooldownSeconds = 6m;
        // October 2026 Forever Frost Channeling: -5% Frost spell mana/rank.
        // Frostfire Bolt is both Fire and Frost, so it qualifies as Frost.
        var frostCostMultiplier = 1m - .05m * t.Rank("frost-channeling");
        foreach (var frostSpell in new[] { frostbolt, frostfire })
            foreach (var cost in frostSpell.ResourceCosts.Where(c => c.ResourceKey == "mana"))
                cost.Amount *= frostCostMultiplier;
        // Utility slow replaces extreme Frostbolt downranking, not a default raid filler.
        var frostBlast = spell("frost-blast", "Frost Blast", tuning.FrostBlastMana, 1.5m,
            hit("frost-blast-impact", "frost", tuning.FrostBlastDamage + sp * .429m, frost));
        foreach (var cost in frostBlast.ResourceCosts.Where(c => c.ResourceKey == "mana"))
            cost.Amount *= frostCostMultiplier;
        var missilesEffect = hit("arcane-missiles-tick", "arcane",
            (tuning.ArcaneMissilesTick + sp * .19m) * arcane);
        missilesEffect.ApplyOnChannelTick = true;
        var missiles = spell("arcane-missiles", "Arcane Missiles", tuning.ArcaneMissilesMana, 0m,
            missilesEffect);
        missiles.ChannelDurationSeconds = 5m;
        missiles.ChannelTickIntervalSeconds = 1m;
        var pyro = spell("pyroblast", "Pyroblast", tuning.PyroblastMana, 6m,
            hit("pyroblast-impact", "fire", tuning.PyroblastDamage + sp * 1.15m, fire));
        // Forever client 70630 rank 7: 166-196 Fire damage, 150 Mana, 1.5s cast.
        // The 1.5/3.5 direct-spell coefficient follows existing caster conventions.
        var scorch = spell("scorch", "Scorch", tuning.ScorchMana, 1.5m,
            hit("scorch-impact", "fire", tuning.ScorchDamage + sp * .429m, fire));
        var fireblast = spell("fire-blast", "Fire Blast", tuning.FireBlastMana, 0m,
            hit("fire-blast-impact", "fire", tuning.FireBlastDamage + sp * .429m, fire));
        fireblast.CooldownSeconds = 8m;
        var lance = spell("ice-lance", "Ice Lance", tuning.IceLanceMana, 0m,
            hit("ice-lance-impact", "frost", tuning.IceLanceDamage + sp * .143m, frost));
        foreach (var cost in lance.ResourceCosts.Where(c => c.ResourceKey == "mana"))
            cost.Amount *= frostCostMultiplier;
        // Build 70532 Cone of Cold rank 5: 325-355 Frost, 555 Mana,
        // 10-second cooldown. Stationary solo target is assumed in the cone;
        // slow geometry and target cap remain encounter-dependent.
        var cone = spell("cone-of-cold", "Cone of Cold", 555m *
            frostCostMultiplier, 0m,
            hit("cone-of-cold-impact", "frost", 340m + sp * .129m, frost));
        cone.CooldownSeconds = 10m;
        var igniteTick = dot("ignite-tick", "mage-ignite", "fire", 0m, 4m, 2m);
        igniteTick.CanMiss = false; igniteTick.CanCrit = false;
        var ignite = spell("mage-ignite", "Ignite (reactive)", 0m, 0m, igniteTick);
        ignite.IsOffGlobalCooldown = true; ignite.GlobalCooldownSeconds = 0m;
        // Classic-era Evocation retained in the Forever beta: 8 second channel,
        // 8 minute cooldown, +1500% spirit regeneration. Four 2-second ticks
        // are used for the existing discrete resource-effect engine.
        // 12.5 + Spirit/4 is the Classic-era Spirit regen per 2 seconds;
        // 16x includes normal regen plus the 1500% bonus.
        var evocationTick = Math.Max(0m, (12.5m + Math.Max(0m, spirit) / 4m) * 16m);
        var evocationEffect = new AbilityEffectDefinition
        {
            Key = "evocation-mana", EffectType = AbilityEffectTypes.ResourceChange,
            TargetType = AbilityTargetTypes.Self,
            ResourceChangeOperation = ResourceChangeOperationTypes.Gain,
            ResourceKey = "mana", ResolutionType = CombatResolutionTypes.AlwaysHits,
            MinimumValue = evocationTick, MaximumValue = evocationTick,
            CanMiss = false, CanCrit = false, ApplyOnChannelTick = true
        };
        var evocation = spell("evocation", "Evocation", 0m, 0m, evocationEffect);
        evocation.ChannelDurationSeconds = 8m;
        evocation.ChannelTickIntervalSeconds = 2m;
        evocation.CooldownSeconds = 480m;

        // This is a first shared-cooldown resource item, not a full consumes catalog.
        // 1350-2250 is the client-facing Forever Major Mana Potion range.
        var potionEffect = new AbilityEffectDefinition
        {
            Key = "major-mana-potion-gain", EffectType = AbilityEffectTypes.ResourceChange,
            TargetType = AbilityTargetTypes.Self,
            ResourceChangeOperation = ResourceChangeOperationTypes.Gain,
            ResourceKey = "mana", ResolutionType = CombatResolutionTypes.AlwaysHits,
            MinimumValue = 1350m, MaximumValue = 2250m, CanMiss = false, CanCrit = false
        };
        var potion = spell("major-mana-potion", "Major Mana Potion", 0m, 0m, potionEffect);
        potion.CooldownSeconds = 120m;
        potion.SharedCooldownGroupKey = "combat-potion";
        potion.SharedCooldownSeconds = 120m;
        potion.GlobalCooldownSeconds = 0m;
        potion.IsOffGlobalCooldown = true;
        // Current 70532 client rank data: Arcane Explosion R6 232-252, 390 Mana;
        // Blizzard R6 1168 damage over eight ticks, 1400 Mana. Ground
        // geometry is an explicit target-set approximation (up to nine enemies).
        // AoE spell-power coefficients remain unverified: no invented scaling.
        var arcaneExplosion = spell("arcane-explosion", "Arcane Explosion", 390m, 0m,
            hit("arcane-explosion-impact", "arcane", 242m, arcane));
        arcaneExplosion.Effects[0].MaxTargets = 9;
        var blizzardTick = hit("blizzard-tick", "frost", 146m, frost);
        blizzardTick.MaxTargets = 9;
        blizzardTick.ApplyOnChannelTick = true;
        var blizzard = spell("blizzard", "Blizzard", 1400m, 0m, blizzardTick);
        blizzard.ChannelDurationSeconds = 8m;
        blizzard.ChannelTickIntervalSeconds = 1m;
        var spells = new List<AbilityDefinition> {arcaneExplosion, blizzard,
            fireball, frostbolt, frostfire, arcaneBlast,
            arcaneWrath, frostBlast, missiles, pyro, scorch, fireblast, lance, cone,
            ignite, evocation, potion};
        // Forever 70532: no GCD or Mana, 10-minute Cold Snap resets other Frost cooldowns.
        if (t.Rank("cold-snap") > 0)
            spells.Add(new AbilityDefinition {
                Key = "cold-snap", Name = "Cold Snap", ClassKey = "mage",
                CooldownSeconds = 600m, IsOffGlobalCooldown = true,
                GlobalCooldownSeconds = 0m
            });
        // Forever 70532: each landed direct Fire hit grants +10% subsequent Fire crit
        // until the third direct Fire critical strike. No fixed aura duration.
        if (t.Rank("combustion") > 0) {
            var combustion = spell("combustion", "Combustion", 0m, 0m,
                new AbilityEffectDefinition {
                    Key = "combustion-buff", EffectType = AbilityEffectTypes.ApplyAura,
                    TargetType = AbilityTargetTypes.Self, AuraKey = "mage-combustion",
                    DurationSeconds = 1201m, ResolutionType = CombatResolutionTypes.AlwaysHits,
                    CanMiss = false, CanCrit = false
                });
            combustion.CooldownSeconds = 180m;
            combustion.GlobalCooldownSeconds = 0m;
            combustion.IsOffGlobalCooldown = true;
            combustion.ForbiddenSourceAuraKeys.Add("mage-combustion");
            spells.Add(combustion);
        }
        return spells;
    }
}

public sealed class ForeverMageTuning
{
    // Illustrative level-60 values. Not verified against final game spell ranks.
    public decimal FireballDamage { get; set; } = 500m;
    public decimal FireballMana { get; set; } = 395m;
    public decimal FireballCastSeconds { get; set; } = 3.5m;
    public decimal FrostboltDamage { get; set; } = 410m;
    public decimal FrostboltMana { get; set; } = 330m;
    public decimal FrostboltCastSeconds { get; set; } = 3m;
    public decimal ArcaneBlastDamage { get; set; } = 394m; // Pre-70425 rank-5 avg; spellbook applies 40% reduction.
    public decimal ArcaneBlastMana { get; set; } = 305m;
    // Retained for backwards-compatible old JSON; no longer used as a release bonus.
    public decimal ArcaneBlastBonusDamagePerStackPercent { get; set; } = 10m;
    public decimal ArcaneWrathDamage { get; set; } = 451m; // 70425 rank-4 406-496 average.
    public decimal ArcaneWrathMana { get; set; } = 395m; // 70425 rank-4.
    public decimal FrostBlastDamage { get; set; } = 77m; // 70425 rank-3 73-81 average.
    public decimal FrostBlastMana { get; set; } = 90m; // 70425 rank-3.
    public decimal MissileBarrageArcaneMissileChancePercentPerRank { get; set; } = 5m;
    public decimal ArcaneBlastExtraManaPerStackPercent { get; set; } = 175m;
    public decimal ArcaneMissilesTick { get; set; } = 135m;
    public decimal ArcaneMissilesMana { get; set; } = 390m;
    public decimal PyroblastDamage { get; set; } = 780m;
    public decimal PyroblastMana { get; set; } = 490m;
    public decimal ScorchDamage { get; set; } = 181m; // 70630 rank 7: (166+196)/2.
    public decimal ScorchMana { get; set; } = 150m; // 70532 rank 7.
    public decimal FireBlastDamage { get; set; } = 380m;
    public decimal FireBlastMana { get; set; } = 280m;
    public decimal FrostfireBoltDamage { get; set; } = 490m;
    public decimal FrostfireBoltMana { get; set; } = 360m;
    public decimal FrostfireDotTick { get; set; } = 80m;
    public decimal IceLanceDamage { get; set; } = 160m;
    public decimal IceLanceMana { get; set; } = 160m;
    public decimal MissileBarrageArcaneBlastChancePercent { get; set; } = 40m; // Rank 2; rank 1 is half.
    public decimal MissileBarrageOtherChancePercent { get; set; } = 20m;
    public decimal FingersOfFrostChancePercentPerRank { get; set; } = 7.5m;
    public decimal ManaRegenPerSecond { get; set; } = 5m;
}
