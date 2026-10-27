using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Data.Forever.Priest;

/// <summary>
/// Data-driven Shadow Priest damage definitions. Spell bases/costs are
/// calibrated placeholders until the level-60 Forever client is verified.
/// The underlying damage, cast, channel, DoT and crit execution is generic Core.
/// </summary>
public static class ForeverPriestShadowSpellbook
{
    public static IReadOnlyList<AbilityDefinition> Create(ForeverPriestShadowTuning tuning,
        ForeverPriestShadowTalentBuild talents, decimal spellPower)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        ArgumentNullException.ThrowIfNull(talents);
        if (spellPower < 0 || tuning.ShadowWordPainTick < 0 ||
            tuning.DevouringPlagueTick < 0 || tuning.MindBlastDamage < 0 ||
            tuning.MindFlayTick < 0 || tuning.ShadowWordDeathDamage < 0 ||
            tuning.MindBlastCooldownSeconds <= 0 || tuning.VampiricEmbraceHealingFraction is < 0m or > 1m)
            throw new ArgumentException("Invalid Shadow spell tuning.");

        var shadowform = talents.Rank("shadowform") > 0;
        decimal instantCost(decimal amount) => Math.Max(0m, amount * (shadowform ? .5m : 1m));
        decimal scaled(decimal baseValue, decimal coefficient) => baseValue + spellPower * coefficient;
        AbilityEffectDefinition damage(string key, decimal amount, bool canCrit = true) => new()
        {
            Key = key, EffectType = AbilityEffectTypes.DirectDamage,
            SchoolKey = "shadow", TargetType = AbilityTargetTypes.Enemy,
            ResolutionType = CombatResolutionTypes.Spell, MitigationType = DamageMitigationTypes.None,
            CanMiss = true, CanCrit = canCrit, MinimumValue = amount, MaximumValue = amount,
            DamageMultiplierStatKey = "shadow-damage-bonus-percent"
        };
        AbilityDefinition spell(string key, string name, decimal mana, decimal cast, params AbilityEffectDefinition[] effects) => new()
        {
            Key = key, Name = name, ClassKey = "priest", SpecializationKey = "shadow",
            CastTimeSeconds = cast, GlobalCooldownSeconds = 1.5m,
            ResourceCosts = [new AbilityResourceCost { ResourceKey = "mana", Amount = instantCost(mana) }],
            Effects = effects.ToList()
        };

        var painTicks = 6 + talents.Rank("improved-shadow-word-pain");
        var pain = damage("swp-tick", scaled(tuning.ShadowWordPainTick, .20m));
        pain.EffectType = AbilityEffectTypes.PeriodicDamage;
        pain.DurationSeconds = painTicks * 3m;
        pain.TickIntervalSeconds = 3m;
        pain.IncludeExpirationBoundaryTick = true;
        pain.AuraKey = "shadow-word-pain";
        var swp = spell("shadow-word-pain", "Shadow Word: Pain", tuning.ShadowWordPainMana, 0m, pain);

        var plague = damage("plague-tick", scaled(tuning.DevouringPlagueTick, .10m));
        plague.EffectType = AbilityEffectTypes.PeriodicDamage;
        plague.DurationSeconds = 24m;
        plague.TickIntervalSeconds = 3m;
        plague.IncludeExpirationBoundaryTick = true;
        plague.AuraKey = "devouring-plague";
        var plagueCost = tuning.DevouringPlagueMana * (1m - .25m * talents.Rank("devouring-contagion"));
        var devouring = spell("devouring-plague", "Devouring Plague", plagueCost, 0m, plague);
        devouring.CooldownSeconds = 24m;

        var blast = spell("mind-blast", "Mind Blast", tuning.MindBlastMana, 1.5m,
            damage("mind-blast-hit", scaled(tuning.MindBlastDamage, .429m)));
        blast.CooldownSeconds = Math.Max(1m, tuning.MindBlastCooldownSeconds - .5m * talents.Rank("improved-mind-blast"));

        // Improved Mind Flay is progressive across successive channel ticks.
        // The channel processor applies the growth before each tick resolves.
        var flayTick = damage("mind-flay-tick", scaled(tuning.MindFlayTick, .167m));
        flayTick.ApplyOnChannelTick = true;
        var flay = spell("mind-flay", "Mind Flay", tuning.MindFlayMana, 0m, flayTick);
        flay.ChannelDurationSeconds = 3m;
        flay.ChannelTickIntervalSeconds = 1m;

        var death = damage("shadow-word-death-hit", scaled(tuning.ShadowWordDeathDamage, .429m));
        death.BonusCriticalChancePercent = talents.Rank("early-demise") * 15m;
        var swd = spell("shadow-word-death", "Shadow Word: Death", tuning.ShadowWordDeathMana, 0m, death);
        swd.CooldownSeconds = 15m;
        // PvE execute-only rotation; manual starts are still permitted above 20%.

        var spells = new List<AbilityDefinition> { swp, devouring, blast, swd };
        if (talents.Rank("mind-flay") > 0) spells.Add(flay);
        if (talents.Rank("vampiric-embrace") > 0)
        {
            var vamp = spell("vampiric-embrace", "Vampiric Embrace", tuning.VampiricEmbraceMana, 0m,
                new AbilityEffectDefinition
                {
                    Key = "vampiric-embrace-aura", EffectType = AbilityEffectTypes.ApplyAura,
                    TargetType = AbilityTargetTypes.Enemy, SchoolKey = "shadow",
                    ResolutionType = CombatResolutionTypes.AlwaysHits, CanMiss = false, CanCrit = false,
                    AuraKey = "vampiric-embrace", DurationSeconds = 30m
                });
            vamp.CooldownSeconds = 60m;
            spells.Add(vamp);
        }
        return spells;
    }
}

public sealed class ForeverPriestShadowTuning
{
    // Illustrative max-level spell bases; replace with client data/log calibration.
    public decimal ShadowWordPainTick { get; set; } = 127m;
    public decimal ShadowWordPainMana { get; set; } = 325m;
    public decimal DevouringPlagueTick { get; set; } = 106m;
    public decimal DevouringPlagueMana { get; set; } = 790m;
    public decimal MindBlastDamage { get; set; } = 490m;
    public decimal MindBlastMana { get; set; } = 350m;
    public decimal MindBlastCooldownSeconds { get; set; } = 8m;
    public decimal MindFlayTick { get; set; } = 130m;
    public decimal MindFlayMana { get; set; } = 205m;
    public decimal ShadowWordDeathDamage { get; set; } = 420m;
    public decimal ShadowWordDeathMana { get; set; } = 245m;
    public decimal VampiricEmbraceMana { get; set; } = 40m;
    public decimal VampiricEmbraceHealingFraction { get; set; } = .20m;
    public decimal ShadowWeavingDamagePerStackPercent { get; set; } = 2m;
    // Forever: 10% of the Priest's MAXIMUM health if the victim survives.
    // Kept as a tunable fraction for backward-compatible request shape.
    public decimal ShadowWordDeathBacklashFraction { get; set; } = .10m;
}
