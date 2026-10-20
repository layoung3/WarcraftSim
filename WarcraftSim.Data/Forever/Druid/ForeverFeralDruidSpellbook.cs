using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Data.Forever.Druid;

/// <summary>Combat-oriented, provisional level-60 Feral ranks and AP terms.</summary>
public sealed class ForeverFeralDruidTuning
{
    public decimal WeaponDps { get; set; } = 75m;
    public decimal AttackPower { get; set; } = 800m;
    public decimal MeleeHitPercent { get; set; } = 88m;
    public decimal MeleeCritPercent { get; set; } = 20m;
    public decimal ShapeshiftMana { get; set; } = 250m;
    public decimal OmenProcPercent { get; set; } = 6m;
    public decimal CatClawBonus { get; set; } = 100m;
    public decimal CatShredBonus { get; set; } = 220m;
    public decimal CatRakeDamage { get; set; } = 80m;
    public decimal CatRakeTick { get; set; } = 40m;
    public decimal RipTickPerPoint { get; set; } = 45m;
    public decimal BitePerPoint { get; set; } = 200m;
    public decimal BearMaulBonus { get; set; } = 120m;
    public decimal BearSwipeBase { get; set; } = 90m;
    public decimal LacerateTickPerStack { get; set; } = 24m;
    // Provisional level-60 coefficients: official Forever notes confirm the
    // talents/form identity, not these endgame magnitudes.
    public decimal ThickHideArmorPercentPerRank { get; set; } = 2m;
    // Forever Feral Instinct affects Swipe DAMAGE, not Bear threat.
    public decimal FeralInstinctSwipeDamagePercentPerRank { get; set; } = 10m;
    // October 15 beta: Bear/Dire Bear bonus increased to 50%.
    public decimal BearBaseThreatMultiplier { get; set; } = 1.5m;
    // Provisional bonus threat on Primal Bite; beta notes say roughly doubled.
    public decimal PrimalBiteThreatMultiplier { get; set; } = 2m;
    public decimal BearAutoRageOnHit { get; set; } = 5m;
    public decimal CatOpenersBonus { get; set; } = 260m;
    // Physical armor and reactive Nature damage for optional solo tests.
    public decimal FaerieFireArmorReduction { get; set; } = 505m;
    public decimal ThornsBaseDamage { get; set; } = 45m;
    public decimal ThornsSpellPower { get; set; } = 0m;
    // Berserk/Frenzied Regeneration values are taken from the October 2026
    // Forever beta tooltip. Keep tooltip changes localized in this tuning object.
    public decimal BerserkCooldownSeconds { get; set; } = 180m;
    public decimal BerserkDurationSeconds { get; set; } = 15m;
    public decimal FrenziedRegenerationCooldownSeconds { get; set; } = 180m;
    public decimal FrenziedRegenerationDurationSeconds { get; set; } = 10m;
    public decimal TankArmorReductionConstant { get; set; } = 10557.5m;
    public void Validate()
    {
        var values = new[] { WeaponDps, AttackPower, MeleeHitPercent, MeleeCritPercent,
            ShapeshiftMana, OmenProcPercent, CatClawBonus, CatShredBonus, CatRakeDamage,
            CatRakeTick, RipTickPerPoint, BitePerPoint, BearMaulBonus, BearSwipeBase,
            LacerateTickPerStack, ThickHideArmorPercentPerRank,
            FeralInstinctSwipeDamagePercentPerRank, BearBaseThreatMultiplier,
            TankArmorReductionConstant,PrimalBiteThreatMultiplier,BearAutoRageOnHit,CatOpenersBonus,
            FaerieFireArmorReduction, ThornsBaseDamage, ThornsSpellPower, BerserkCooldownSeconds,
            BerserkDurationSeconds,FrenziedRegenerationCooldownSeconds,
            FrenziedRegenerationDurationSeconds };
        if (values.Any(x => x < 0m) || MeleeHitPercent > 100m || MeleeCritPercent > 100m ||
            OmenProcPercent > 100m || TankArmorReductionConstant <= 0m ||
            BearBaseThreatMultiplier <= 0m || PrimalBiteThreatMultiplier <= 0m) throw new ArgumentException("Invalid Feral tuning.");
    }
}
public sealed class ForeverFeralDruidTalents
{
    // Partial implementation: unknown / disconnected ranks explicitly rejected.
    public Dictionary<string,int> Ranks { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string,int> Caps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ferocity"] = 5, ["shredding-attacks"] = 2, ["savage-fury"] = 2,
        ["sharpened-claws"] = 2, ["predatory-strikes"] = 3,
        ["furor"] = 5, ["shifting-power"] = 1, ["primal-bite"] = 1,
        ["thick-hide"] = 3, ["feral-instinct"] = 3,
        ["improved-shifting-power"] = 2, ["blood-frenzy"] = 2,
        ["feral-swiftness"] = 2, ["brutal-impact"] = 2,
        ["rend-and-tear"] = 3, ["natural-reaction"] = 3,
        ["leader-of-the-pack"] = 1, ["natural-instinct"] = 2,
        ["berserk"] = 1,
        ["feral-charge"] = 1
    };
    public int Rank(string name) => Ranks.TryGetValue(name, out var rank) ? rank : 0;
    public void Validate()
    {
        if (Ranks is null) throw new ArgumentException("Talent map required.");
        foreach (var (name,rank) in Ranks)
            if (!Caps.TryGetValue(name,out var cap) || rank < 0 || rank > cap)
                throw new ArgumentException($"Unsupported Feral talent {name}={rank}");
        if (Rank("shifting-power") > 0 && Rank("shredding-attacks") == 0 ||
            Rank("primal-bite") > 0 && Rank("savage-fury") == 0 ||
            Rank("improved-shifting-power") > 0 && Rank("shifting-power") == 0 ||
            Rank("berserk") > 0 && Rank("leader-of-the-pack") == 0)
            throw new ArgumentException("Feral talent prerequisite not met.");
    }
}
public static class ForeverFeralDruidSpellbook
{
    public static IReadOnlyList<AbilityDefinition> Create(ForeverFeralDruidTalents talents,
        ForeverFeralDruidTuning t)
    {
        talents.Validate(); t.Validate();
        var cat = ForeverDruidFormState.WeaponDamage(t.WeaponDps, ForeverDruidForm.Cat);
        var bear = ForeverDruidFormState.WeaponDamage(t.WeaponDps, ForeverDruidForm.DireBear);
        var bonus = 1m + .02m * talents.Rank("savage-fury");
        AbilityEffectDefinition hit(string key, decimal amount) => new() {
            Key = key, EffectType = AbilityEffectTypes.DirectDamage,
            TargetType = AbilityTargetTypes.Enemy, SchoolKey = "physical",
            ResolutionType = CombatResolutionTypes.Melee, CanMiss = true, CanCrit = true,
            MinimumValue = amount, MaximumValue = amount
        };
        AbilityDefinition attack(string key, string form, decimal cost, string resource,
            params AbilityEffectDefinition[] effects)
        {
            var a = new AbilityDefinition { Key = key, Name = key, ClassKey = "druid",
                SpecializationKey = "feral", GlobalCooldownSeconds = 1m,
                ResourceCosts = [new AbilityResourceCost { ResourceKey = resource, Amount = cost }],
                Effects = effects.ToList() };
            a.RequiredSourceAuraKeys.Add(ForeverDruidFormState.AuraKey(
                form == "cat" ? ForeverDruidForm.Cat : ForeverDruidForm.DireBear));
            return a;
        }
        AbilityEffectDefinition bleed(string key,string aura,decimal amount,decimal duration, decimal interval) => new() {
            Key = key, EffectType = AbilityEffectTypes.PeriodicDamage,
            TargetType = AbilityTargetTypes.Enemy, SchoolKey = "physical",
            ResolutionType = CombatResolutionTypes.Melee, CanMiss = true, CanCrit = true,
            MinimumValue = amount, MaximumValue = amount,
            AuraKey = aura, DurationSeconds = duration, TickIntervalSeconds = interval,
            IncludeExpirationBoundaryTick = true
        };
        var list = new List<AbilityDefinition> {
            attack("claw", "cat", 45m - talents.Rank("ferocity"), "energy",
                hit("claw-hit", (cat + t.CatClawBonus + t.AttackPower * .15m) * bonus)),
            attack("shred", "cat", 60m - 9m * talents.Rank("shredding-attacks"), "energy",
                hit("shred-hit", (cat * 2.25m + t.CatShredBonus + t.AttackPower * .1m) * bonus)),
            attack("rake", "cat", 40m - talents.Rank("ferocity"), "energy", hit("rake-hit", t.CatRakeDamage + t.AttackPower * .05m),
                bleed("rake-tick", "rake", t.CatRakeTick + t.AttackPower * .03m, 9m, 3m)),
            attack("rip", "cat", 30m, "energy", bleed("rip-tick", "rip", t.RipTickPerPoint, 12m, 2m)),
            attack("ferocious-bite", "cat", 35m, "energy", hit("bite-hit", t.BitePerPoint)),
            attack("maul", "bear", 15m - talents.Rank("ferocity"), "rage", hit("maul-hit", bear + t.BearMaulBonus)),
            attack("swipe", "bear", 20m - talents.Rank("ferocity"), "rage", hit("swipe-hit", (t.BearSwipeBase + t.AttackPower * .03m) *
                (1m + talents.Rank("feral-instinct") * t.FeralInstinctSwipeDamagePercentPerRank / 100m))),
            attack("lacerate", "bear", 15m - talents.Rank("shredding-attacks") * 2m, "rage",
                hit("lacerate-impact", bear * .10m),
                bleed("lacerate-tick", "lacerate", t.LacerateTickPerStack, 15m, 3m)),
            attack("enrage", "bear", 0m, "rage", new AbilityEffectDefinition {
                Key = "enrage-aura", EffectType = AbilityEffectTypes.ApplyAura,
                AuraKey = "druid-enrage", TargetType = AbilityTargetTypes.Self,
                DurationSeconds = 10m, ResolutionType = CombatResolutionTypes.AlwaysHits,
                CanMiss = false, CanCrit = false
            })
        };
        list.First(x => x.Key == "lacerate").Effects[1].AuraStackingMode = WarcraftSim.Core.Auras.AuraStackingMode.Stack;
        list.First(x => x.Key == "lacerate").Effects[1].MaxStacks = 5;
        list.Last().CooldownSeconds = 60m;
        // Cat opener state is a real source-aura gate, not simply a priority flag.
        var pounce = attack("pounce", "cat", 50m, "energy",
            bleed("pounce-bleed", "druid-pounce-bleed", t.CatRakeTick * 1.5m, 12m, 3m));
        pounce.RequiredSourceAuraKeys.Add("druid-prowl");
        var ravage = attack("ravage", "cat", 60m, "energy",
            hit("ravage-hit", (cat * 3.0m + t.CatOpenersBonus) * bonus));
        ravage.RequiredSourceAuraKeys.Add("druid-prowl");
        list.Add(pounce);
        list.Add(ravage);
        // Cower reduces the enemy threat table on cast completion in the Feral processor.
        list.Add(attack("cower", "cat", 20m, "energy",
            new AbilityEffectDefinition { Key = "cower-state", EffectType = AbilityEffectTypes.ApplyAura,
                TargetType = AbilityTargetTypes.Self, AuraKey = "druid-cower",
                ResolutionType = CombatResolutionTypes.AlwaysHits, CanMiss = false, CanCrit = false,
                DurationSeconds = 0.1m }));
        // Bear utility: these auras are also connected to threat or incoming hits
        // by the runner, rather than counting unused state as completed behavior.
        var growl = attack("growl", "bear", 0m, "rage",
            new AbilityEffectDefinition { Key = "growl-state", EffectType = AbilityEffectTypes.ApplyAura,
                TargetType = AbilityTargetTypes.Enemy, AuraKey = "druid-growl",
                DurationSeconds = 3m, ResolutionType = CombatResolutionTypes.AlwaysHits,
                CanMiss = false, CanCrit = false });
        growl.CooldownSeconds = 8m;
        list.Add(growl);
        // Forever Faerie Fire: free in Cat/Bear/Dire Bear, 6s cooldown.
        // Armor is resolved by the physical mitigation pipeline, not the aura itself.
        var faerieFire = new AbilityDefinition { Key = "faerie-fire", Name = "Faerie Fire",
            ClassKey = "druid", SpecializationKey = "feral", GlobalCooldownSeconds = 1m,
            CooldownSeconds = 6m,
            Effects = [new AbilityEffectDefinition { Key = "faerie-fire-debuff",
                EffectType = AbilityEffectTypes.ApplyAura, TargetType = AbilityTargetTypes.Enemy,
                AuraKey = "druid-faerie-fire", DurationSeconds = 40m,
                ResolutionType = CombatResolutionTypes.AlwaysHits,
                CanMiss = false, CanCrit = false }] };
        faerieFire.RequiredAnySourceAuraKeys.Add("druid-cat-form");
        faerieFire.RequiredAnySourceAuraKeys.Add("druid-dire-bear-form");
        faerieFire.RequiredAnySourceAuraKeys.Add("druid-bear-form");
        list.Add(faerieFire);
        // No artificial threat gain: Challenging Roar forces the enemy's target
        // through Core's existing ForcedTargetManager for six seconds.
        var challenging = attack("challenging-roar", "bear", 0m, "rage",
            new AbilityEffectDefinition { Key = "challenging-roar-marker",
                EffectType = AbilityEffectTypes.ApplyAura, TargetType = AbilityTargetTypes.Self,
                AuraKey = "druid-challenging-roar", DurationSeconds = 6m,
                ResolutionType = CombatResolutionTypes.AlwaysHits,
                CanMiss = false, CanCrit = false });
        challenging.CooldownSeconds = 600m; // legacy duration, pending Forever tooltip verification
        list.Add(challenging);
        if (talents.Rank("feral-charge") > 0)
        {
        var charge = attack("feral-charge", "bear", 5m, "rage",
            new AbilityEffectDefinition { Key = "feral-charge-root",
                EffectType = AbilityEffectTypes.ApplyAura, TargetType = AbilityTargetTypes.Enemy,
                AuraKey = "druid-feral-charge-root", DurationSeconds = 4m,
                ResolutionType = CombatResolutionTypes.AlwaysHits,
                CanMiss = false, CanCrit = false });
        charge.CooldownSeconds = 15m; // provisional; full distance / enemy cast interrupt deferred
        list.Add(charge);
        }
        var demo = attack("demoralizing-roar", "bear", 10m, "rage",
            new AbilityEffectDefinition { Key = "demo-state", EffectType = AbilityEffectTypes.ApplyAura,
                TargetType = AbilityTargetTypes.Enemy, AuraKey = "druid-demoralizing-roar",
                DurationSeconds = 30m, ResolutionType = CombatResolutionTypes.AlwaysHits,
                CanMiss = false, CanCrit = false });
        list.Add(demo);
        var bash = attack("bash", "bear", 10m, "rage",
            new AbilityEffectDefinition { Key = "bash-state", EffectType = AbilityEffectTypes.ApplyAura,
                TargetType = AbilityTargetTypes.Enemy, AuraKey = "druid-bash",
                DurationSeconds = 4m, ResolutionType = CombatResolutionTypes.AlwaysHits,
                CanMiss = false, CanCrit = false });
        bash.CooldownSeconds = 60m - 5m * talents.Rank("brutal-impact");
        list.Add(bash);
        if (talents.Rank("primal-bite") > 0)
        {
            var bite = attack("primal-bite", "bear", 20m - talents.Rank("ferocity"), "rage",
                hit("primal-bite-hit", bear + 180m));
            bite.CooldownSeconds = 6m; list.Add(bite);
        }
        if (talents.Rank("shifting-power") > 0)
        {
            var shifting = attack("shifting-power", "cat", 0m, "mana",
                new AbilityEffectDefinition { Key = "shifting-power-marker",
                    EffectType = AbilityEffectTypes.ApplyAura, AuraKey = "shifting-power",
                    TargetType = AbilityTargetTypes.Self, DurationSeconds = 1m,
                    ResolutionType = CombatResolutionTypes.AlwaysHits, CanMiss = false, CanCrit = false });
            shifting.ResourceCosts[0].Amount = t.ShapeshiftMana;
            shifting.CooldownSeconds = 16m - 4m * talents.Rank("improved-shifting-power");
            list.Add(shifting);
        }
        // Forever Frenzied Regeneration: 10s, converts up to 10 Rage each
        // second, 1% of maximum health per Rage. Actual ticking is handled
        // by FeralDruidCombatProcessor, not an unused marker aura.
        var regeneration = attack("frenzied-regeneration", "bear", 0m, "rage",
            new AbilityEffectDefinition { Key = "frenzied-regeneration-state",
                EffectType = AbilityEffectTypes.ApplyAura,
                TargetType = AbilityTargetTypes.Self, AuraKey = "druid-frenzied-regeneration",
                DurationSeconds = t.FrenziedRegenerationDurationSeconds,
                ResolutionType = CombatResolutionTypes.AlwaysHits,
                CanMiss = false, CanCrit = false });
        regeneration.CooldownSeconds = t.FrenziedRegenerationCooldownSeconds;
        list.Add(regeneration);
        if (talents.Rank("berserk") > 0)
        {
            // Berserk works in both Cat and Bear, so it intentionally has no
            // single-form aura requirement; the rotation gates it by form.
            var berserk = new AbilityDefinition { Key = "berserk", Name = "Berserk",
                ClassKey = "druid", SpecializationKey = "feral",
                GlobalCooldownSeconds = 0m, IsOffGlobalCooldown = true,
                CooldownSeconds = t.BerserkCooldownSeconds,
                Effects = [new AbilityEffectDefinition {
                    Key = "berserk-state", EffectType = AbilityEffectTypes.ApplyAura,
                    TargetType = AbilityTargetTypes.Self, AuraKey = "druid-berserk",
                    DurationSeconds = t.BerserkDurationSeconds,
                    ResolutionType = CombatResolutionTypes.AlwaysHits,
                    CanMiss = false, CanCrit = false }] };
            berserk.RequiredAnySourceAuraKeys.Add("druid-cat-form");
            berserk.RequiredAnySourceAuraKeys.Add("druid-bear-form");
            berserk.RequiredAnySourceAuraKeys.Add("druid-dire-bear-form");
            list.Add(berserk);
        }
        return list;
    }
}
