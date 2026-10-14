using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Data.Forever.Priest;

/// <summary>
/// Forever Priest healing toolkit for the CP126 integration scenarios.
/// Amounts are explicit tuning inputs, NOT verified max-rank client values.
/// Talent multipliers are kept in this class, not in Core's healing engine.
/// </summary>
public static class ForeverPriestHealingSpellbook
{
    public static IReadOnlyList<AbilityDefinition> Create(ForeverPriestHealingTuning t)
    {
        ArgumentNullException.ThrowIfNull(t);
        if (t.FlashHealAmount <= 0m || t.GreaterHealAmount <= 0m ||
            t.RenewTickAmount <= 0m || t.ShieldAbsorbAmount <= 0m ||
            t.BindingHealAmount <= 0m || t.PrayerOfHealingAmount <= 0m ||
            t.PenanceTickAmount <= 0m || t.PrayerOfMendingAmount <= 0m ||
            t.ImprovedRenewRanks is < 0 or > 3 || t.ImprovedShieldRanks is < 0 or > 3 ||
            t.SpiritualHealingRanks is < 0 or > 3 || t.DivineFuryRanks is < 0 or > 5 ||
            t.ImprovedHealingRanks is < 0 or > 3 || t.TwinDisciplinesRanks is < 0 or > 5)
            throw new ArgumentException("Invalid Priest spell tuning or talent ranks.", nameof(t));

        decimal healScale = 1m + t.SpiritualHealingRanks * .05m;
        decimal instantScale = 1m + t.TwinDisciplinesRanks * .01m;
        decimal reducedCost(decimal baseCost) => Math.Max(0m, baseCost * (1m - .05m * t.ImprovedHealingRanks));
        AbilityEffectDefinition healing(string key, decimal amount, bool periodic = false) => new()
        {
            Key = key, EffectType = periodic ? AbilityEffectTypes.PeriodicHealing : AbilityEffectTypes.DirectHealing,
            TargetType = AbilityTargetTypes.Friendly, SchoolKey = "holy",
            ResolutionType = CombatResolutionTypes.Spell, CanMiss = false, CanCrit = true,
            MinimumValue = amount, MaximumValue = amount
        };
        AbilityDefinition spell(string key, string name, decimal mana, decimal cast, params AbilityEffectDefinition[] effects) => new()
        {
            Key = key, Name = name, ClassKey = "priest", SpecializationKey = "holy",
            CastTimeSeconds = cast, GlobalCooldownSeconds = 1.5m,
            ResourceCosts = [new AbilityResourceCost { ResourceKey = "mana", Amount = Math.Max(0m, mana) }],
            Effects = effects.ToList()
        };
        var flash = spell("flash-heal", "Flash Heal", t.FlashHealCost, 1.5m,
            healing("flash-heal-heal", t.FlashHealAmount * healScale));
        var greater = spell("greater-heal", "Greater Heal", reducedCost(t.GreaterHealCost),
            Math.Max(1.5m, 3m - t.DivineFuryRanks * .1m),
            healing("greater-heal-heal", t.GreaterHealAmount * healScale));
        var heal = spell("heal", "Heal", reducedCost(t.HealCost),
            Math.Max(1.5m, 3m - t.DivineFuryRanks * .1m),
            healing("heal-heal", t.HealAmount * healScale));
        var renewEffect = healing("renew-tick", t.RenewTickAmount * healScale *
            (1m + t.ImprovedRenewRanks * .05m) * instantScale, periodic: true);
        renewEffect.DurationSeconds = 15m;
        renewEffect.TickIntervalSeconds = 3m;
        renewEffect.IncludeExpirationBoundaryTick = true;
        renewEffect.AuraKey = "renew";
        var renew = spell("renew", "Renew", t.RenewCost, 0m, renewEffect);
        // Weakened Soul applies to the selected target on a successful Shield.
        var shield = spell("power-word-shield", "Power Word: Shield", t.ShieldCost, 0m,
            new AbilityEffectDefinition
            {
                Key = "pws-absorb", EffectType = AbilityEffectTypes.Absorb,
                TargetType = AbilityTargetTypes.Friendly, CanMiss = false, CanCrit = false,
                ResolutionType = CombatResolutionTypes.AlwaysHits, AbsorbKey = "power-word-shield",
                DurationSeconds = 30m, AbsorbStackingMode = AbsorbStackingMode.Refresh,
                MinimumValue = t.ShieldAbsorbAmount * (1m + .05m * t.ImprovedShieldRanks) * instantScale,
                MaximumValue = t.ShieldAbsorbAmount * (1m + .05m * t.ImprovedShieldRanks) * instantScale
            },
            new AbilityEffectDefinition
            {
                Key = "pws-weakened-soul", EffectType = AbilityEffectTypes.ApplyAura,
                TargetType = AbilityTargetTypes.Friendly, CanMiss = false, CanCrit = false,
                ResolutionType = CombatResolutionTypes.AlwaysHits, AuraKey = "weakened-soul", DurationSeconds = 15m
            });
        shield.CooldownSeconds = t.SoulWarding ? 0m : 4m;
        shield.ForbiddenTargetAuraKeys.Add("weakened-soul");
        // Binding Heal heals the chosen ally AND caster. The self heal can overheal.
        var bindingSelf = healing("binding-heal-self", t.BindingHealAmount * healScale);
        bindingSelf.TargetType = AbilityTargetTypes.Self;
        var binding = spell("binding-heal", "Binding Heal", t.BindingHealCost, 1.5m,
            healing("binding-heal-target", t.BindingHealAmount * healScale), bindingSelf);
        var group = healing("prayer-of-healing-group", t.PrayerOfHealingAmount * healScale);
        group.MaxTargets = 5; // group filter is a later roster requirement; currently team-limited
        var prayer = spell("prayer-of-healing", "Prayer of Healing", t.PrayerOfHealingCost, 3m, group);
        var penanceTick = healing("penance-tick", t.PenanceTickAmount * healScale);
        penanceTick.ApplyOnChannelTick = true;
        var penance = spell("penance", "Penance", reducedCost(t.PenanceCost), 0m, penanceTick);
        penance.CooldownSeconds = 12m;
        penance.ChannelDurationSeconds = 3m;
        penance.ChannelTickIntervalSeconds = 1m;
        // Proc processor resolves the actual healing when this aura is triggered.
        var mending = spell("prayer-of-mending", "Prayer of Mending", reducedCost(t.PrayerOfMendingCost), 0m,
            new AbilityEffectDefinition
            {
                Key = "prayer-of-mending-aura", EffectType = AbilityEffectTypes.ApplyAura,
                TargetType = AbilityTargetTypes.Friendly, CanMiss = false, CanCrit = false,
                ResolutionType = CombatResolutionTypes.AlwaysHits,
                AuraKey = "prayer-of-mending", DurationSeconds = 30m
            });
        mending.CooldownSeconds = 10m;
        return [flash, greater, heal, renew, shield, binding, prayer, penance, mending];
    }
}

public sealed class ForeverPriestHealingTuning
{
    public decimal FlashHealAmount { get; set; } = 680m;
    public decimal FlashHealCost { get; set; } = 180m;
    public decimal GreaterHealAmount { get; set; } = 1550m;
    public decimal GreaterHealCost { get; set; } = 410m;
    public decimal HealAmount { get; set; } = 920m;
    public decimal HealCost { get; set; } = 230m;
    public decimal RenewTickAmount { get; set; } = 145m;
    public decimal RenewCost { get; set; } = 230m;
    public decimal ShieldAbsorbAmount { get; set; } = 650m;
    public decimal ShieldCost { get; set; } = 270m;
    public decimal BindingHealAmount { get; set; } = 590m;
    public decimal BindingHealCost { get; set; } = 290m;
    public decimal PrayerOfHealingAmount { get; set; } = 540m;
    public decimal PrayerOfHealingCost { get; set; } = 540m;
    public decimal PenanceTickAmount { get; set; } = 280m;
    public decimal PenanceCost { get; set; } = 240m;
    public decimal PrayerOfMendingAmount { get; set; } = 250m;
    public decimal PrayerOfMendingCost { get; set; } = 240m;
    public int ImprovedRenewRanks { get; set; }
    public int ImprovedShieldRanks { get; set; }
    public int SpiritualHealingRanks { get; set; }
    public int DivineFuryRanks { get; set; }
    public int ImprovedHealingRanks { get; set; }
    public int TwinDisciplinesRanks { get; set; }
    public bool SoulWarding { get; set; }
}
