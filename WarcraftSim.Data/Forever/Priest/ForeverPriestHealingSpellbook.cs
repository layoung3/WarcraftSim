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
            t.ImprovedHealingRanks is < 0 or > 3 || t.TwinDisciplinesRanks is < 0 or > 5 ||
            t.MentalAgilityRanks is < 0 or > 3 || t.BonusHealingPower < 0m)
            throw new ArgumentException("Invalid Priest spell tuning or talent ranks.", nameof(t));

        decimal healScale = t.UseLegacySpiritualHealingScaling
            ? 1m + t.SpiritualHealingRanks * .05m
            : 1m + (t.SpiritualHealingRanks switch
        { 0 => 0m, 1 => .03m, 2 => .07m, 3 => .10m, _ => throw new ArgumentException("Invalid Spiritual Healing rank.") });
        decimal instantScale = 1m + t.TwinDisciplinesRanks * .01m;
        decimal improvedShieldScale = t.UseLegacyImprovedShieldScaling
            ? 1m + .05m * t.ImprovedShieldRanks
            : 1m + (t.ImprovedShieldRanks switch
                { 0 => 0m, 1 => .07m, 2 => .14m, 3 => .20m, _ => 0m });
        decimal reducedCost(decimal baseCost) => Math.Max(0m, baseCost * (1m - .05m * t.ImprovedHealingRanks));
        decimal instantCost(decimal cost) => cost * (1m - (t.MentalAgilityRanks switch
        { 0 => 0m, 1 => .03m, 2 => .07m, 3 => .10m, _ => 0m }));
        decimal amountWithPower(decimal baseAmount, decimal coefficient) =>
            (baseAmount + t.BonusHealingPower * coefficient) * healScale;
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
            healing("flash-heal-heal", amountWithPower(t.FlashHealAmount, .429m)));
        var greater = spell("greater-heal", "Greater Heal", reducedCost(t.GreaterHealCost),
            Math.Max(1.5m, 3m - t.DivineFuryRanks * .1m),
            healing("greater-heal-heal", amountWithPower(t.GreaterHealAmount, .857m)));
        var heal = spell("heal", "Heal", reducedCost(t.HealCost),
            Math.Max(1.5m, 3m - t.DivineFuryRanks * .1m),
            healing("heal-heal", amountWithPower(t.HealAmount, .857m)));
        var renewEffect = healing("renew-tick", amountWithPower(t.RenewTickAmount, .20m) *
            (1m + t.ImprovedRenewRanks * .05m) * instantScale, periodic: true);
        renewEffect.DurationSeconds = 15m;
        renewEffect.TickIntervalSeconds = 3m;
        renewEffect.IncludeExpirationBoundaryTick = true;
        renewEffect.AuraKey = "renew";
        var renew = spell("renew", "Renew", instantCost(t.RenewCost), 0m, renewEffect);
        // Weakened Soul applies to the selected target on a successful Shield.
        var shield = spell("power-word-shield", "Power Word: Shield",
            instantCost(t.ShieldCost * (t.SoulWarding ? .85m : 1m)), 0m,
            new AbilityEffectDefinition
            {
                Key = "pws-absorb", EffectType = AbilityEffectTypes.Absorb,
                TargetType = AbilityTargetTypes.Friendly, CanMiss = false, CanCrit = false,
                ResolutionType = CombatResolutionTypes.AlwaysHits, AbsorbKey = "power-word-shield",
                DurationSeconds = 30m, AbsorbStackingMode = AbsorbStackingMode.Refresh,
                MinimumValue = (t.ShieldAbsorbAmount + t.BonusHealingPower * .1m) * improvedShieldScale * instantScale,
                MaximumValue = (t.ShieldAbsorbAmount + t.BonusHealingPower * .1m) * improvedShieldScale * instantScale
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
        var bindingSelf = healing("binding-heal-self", amountWithPower(t.BindingHealAmount, .429m));
        bindingSelf.TargetType = AbilityTargetTypes.Self;
        var binding = spell("binding-heal", "Binding Heal", t.BindingHealCost, 1.5m,
            healing("binding-heal-target", amountWithPower(t.BindingHealAmount, .429m)), bindingSelf);
        var group = healing("prayer-of-healing-group", amountWithPower(t.PrayerOfHealingAmount, .286m));
        group.MaxTargets = 5; // group filter is a later roster requirement; currently team-limited
        var prayer = spell("prayer-of-healing", "Prayer of Healing", t.PrayerOfHealingCost, 3m, group);
        var penanceTick = healing("penance-tick", amountWithPower(t.PenanceTickAmount, .143m));
        penanceTick.ApplyOnChannelTick = true;
        var penance = spell("penance", "Penance", instantCost(reducedCost(t.PenanceCost)), 0m, penanceTick);
        penance.CooldownSeconds = 12m;
        // Forever client: immediate heal, then one tick each second over
        // two seconds; three effective heals, never a three-second channel.
        penance.ChannelDurationSeconds = 2m;
        penance.ChannelTickIntervalSeconds = 1m;
        penance.ChannelTickAtStart = true;
        // Proc processor resolves the actual healing when this aura is triggered.
        var mending = spell("prayer-of-mending", "Prayer of Mending", instantCost(reducedCost(t.PrayerOfMendingCost)), 0m,
            new AbilityEffectDefinition
            {
                Key = "prayer-of-mending-aura", EffectType = AbilityEffectTypes.ApplyAura,
                TargetType = AbilityTargetTypes.Friendly, CanMiss = false, CanCrit = false,
                ResolutionType = CombatResolutionTypes.AlwaysHits,
                AuraKey = "prayer-of-mending", DurationSeconds = 30m
            });
        mending.CooldownSeconds = 10m;
        var spells = new List<AbilityDefinition> { flash, greater, heal, renew, shield, prayer };
        if (!t.RestrictTalentAbilities || t.HasBindingHealTalent) spells.Add(binding);
        if (!t.RestrictTalentAbilities || t.HasPenanceTalent) spells.Add(penance);
        if (!t.RestrictTalentAbilities || t.HasPrayerOfMendingTalent) spells.Add(mending);
        return spells;
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
    public bool UseLegacySpiritualHealingScaling { get; set; } = true;
    public bool UseLegacyImprovedShieldScaling { get; set; } = true;
    public decimal BonusHealingPower { get; set; }
    public int MentalAgilityRanks { get; set; }
    public bool RestrictTalentAbilities { get; set; }
    public bool HasBindingHealTalent { get; set; }
    public bool HasPenanceTalent { get; set; }
    public bool HasPrayerOfMendingTalent { get; set; }
}
