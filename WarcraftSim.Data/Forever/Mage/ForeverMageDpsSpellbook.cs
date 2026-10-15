using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Data.Forever.Mage;

namespace WarcraftSim.Data.Forever.Mage;

/// <summary>Modeled level-60 Mage spells. All beta ranks and coefficients are explicit
/// tuning assumptions. CP132's specialization processor handles stateful procs.</summary>
public static class ForeverMageDpsSpellbook
{
    public static IReadOnlyList<AbilityDefinition> Create(ForeverMageTalentBuild t, ForeverMageTuning tuning, decimal sp)
    {
        decimal fire = 1m + t.Rank("fire-power") * .02m;
        decimal frost = 1m + t.Rank("piercing-ice") * .02m;
        decimal arcane = 1m + t.Rank("arcane-instability") * .01m;
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
        var arcaneBlast = spell("arcane-blast", "Arcane Blast", tuning.ArcaneBlastMana, 2.5m,
            hit("arcane-blast-impact", "arcane", tuning.ArcaneBlastDamage + sp * .714m, arcane));
        var missilesEffect = hit("arcane-missiles-tick", "arcane",
            (tuning.ArcaneMissilesTick + sp * .19m) * arcane);
        missilesEffect.ApplyOnChannelTick = true;
        var missiles = spell("arcane-missiles", "Arcane Missiles", tuning.ArcaneMissilesMana, 0m,
            missilesEffect);
        missiles.ChannelDurationSeconds = 5m;
        missiles.ChannelTickIntervalSeconds = 1m;
        var pyro = spell("pyroblast", "Pyroblast", tuning.PyroblastMana, 6m,
            hit("pyroblast-impact", "fire", tuning.PyroblastDamage + sp * 1.15m, fire));
        var fireblast = spell("fire-blast", "Fire Blast", tuning.FireBlastMana, 0m,
            hit("fire-blast-impact", "fire", tuning.FireBlastDamage + sp * .429m, fire));
        fireblast.CooldownSeconds = 8m;
        var lance = spell("ice-lance", "Ice Lance", tuning.IceLanceMana, 0m,
            hit("ice-lance-impact", "frost", tuning.IceLanceDamage + sp * .143m, frost));
        var igniteTick = dot("ignite-tick", "mage-ignite", "fire", 0m, 4m, 2m);
        igniteTick.CanMiss = false; igniteTick.CanCrit = false;
        var ignite = spell("mage-ignite", "Ignite (reactive)", 0m, 0m, igniteTick);
        ignite.IsOffGlobalCooldown = true; ignite.GlobalCooldownSeconds = 0m;
        return [fireball, frostbolt, frostfire, arcaneBlast, missiles, pyro, fireblast, lance, ignite];
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
    public decimal ArcaneBlastDamage { get; set; } = 440m;
    public decimal ArcaneBlastMana { get; set; } = 305m;
    public decimal ArcaneBlastBonusDamagePerStackPercent { get; set; } = 15m;
    public decimal ArcaneBlastExtraManaPerStackPercent { get; set; } = 25m;
    public decimal ArcaneMissilesTick { get; set; } = 135m;
    public decimal ArcaneMissilesMana { get; set; } = 390m;
    public decimal PyroblastDamage { get; set; } = 780m;
    public decimal PyroblastMana { get; set; } = 490m;
    public decimal FireBlastDamage { get; set; } = 380m;
    public decimal FireBlastMana { get; set; } = 280m;
    public decimal FrostfireBoltDamage { get; set; } = 490m;
    public decimal FrostfireBoltMana { get; set; } = 360m;
    public decimal FrostfireDotTick { get; set; } = 80m;
    public decimal IceLanceDamage { get; set; } = 160m;
    public decimal IceLanceMana { get; set; } = 160m;
    public decimal MissileBarrageArcaneBlastChancePercent { get; set; } = 40m;
    public decimal MissileBarrageOtherChancePercent { get; set; } = 20m;
    public decimal FingersOfFrostChancePercentPerRank { get; set; } = 7.5m;
    public decimal ManaRegenPerSecond { get; set; } = 5m;
}
