using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Data.Forever.Hunter;

/// <summary>Level-60 Forever Survival tree, client 1.60.1.70450 (Oct 2026).</summary>
public sealed class ForeverSurvivalHunterTalents
{
    public Dictionary<string, int> Ranks { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    private static readonly (string Key, int Cap, int Row)[] Tree = [
        ("improved-tracking",5,0),("deflection",5,0),
        ("entrapment",5,1),("savage-strikes",2,1),("survivalist",5,1),("improved-wing-clip",3,1),
        ("clever-traps",2,2),("surefooted",3,2),("deterrence",1,2),
        ("survival-tactics",2,3),("predators-edge",5,3),("counterattack",1,3),
        ("resourcefulness",2,4),("expose-prey",2,4),("survivalists-discipline",2,4),("strider-kick",1,4),
        ("lightning-reflexes",5,5),("lacerating-strikes",1,6)
    ];
    public int Rank(string key) => Ranks?.GetValueOrDefault(key) ?? 0;
    public void Validate()
    {
        if (Ranks is null || Ranks.Values.Sum() > 51)
            throw new ArgumentException("Survival talents must have at most 51 allocated points.");
        foreach (var (key, value) in Ranks)
            if (!Tree.Any(t => t.Key == key && value >= 0 && value <= t.Cap))
                throw new ArgumentException($"Unsupported Survival talent or rank: {key}={value}.");
        foreach (var (_, _, row) in Tree.DistinctBy(t => t.Row)) {
            if (row == 0) continue;
            var threshold = row * 5;
            if (Tree.Where(t => t.Row == row).Any(t => Rank(t.Key) > 0) &&
                Tree.Where(t => t.Row < row).Sum(t => Rank(t.Key)) < threshold)
                throw new ArgumentException($"Survival tier {row + 1} requires {threshold} points in earlier rows.");
        }
        if (Rank("counterattack") > 0 && Rank("deterrence") != 1 ||
            Rank("lacerating-strikes") > 0 && Rank("expose-prey") != 2)
            throw new ArgumentException("Survival capstone/Counterattack prerequisites not met.");
    }
}

/// <summary>Editable equipment/stat and experimental encounter inputs; spell-rank constants are level 60.</summary>
public sealed class ForeverSurvivalHunterTuning
{
    public decimal BaseMana { get; set; } = 4200m; // provisional, distinct from gear-increased max Mana
    public decimal MaximumMana { get; set; } = 4200m;
    public decimal MaximumHealth { get; set; } = 3900m;
    public decimal Agility { get; set; } = 220m;
    public decimal MeleeAttackPower { get; set; } = 950m;
    public decimal RangedAttackPower { get; set; } = 1000m;
    public decimal MeleeWeaponMinDamage { get; set; } = 205m;
    public decimal MeleeWeaponMaxDamage { get; set; } = 310m;
    public decimal MeleeWeaponSpeedSeconds { get; set; } = 2.7m;
    public decimal OffhandWeaponMinDamage { get; set; } = 75m;
    public decimal OffhandWeaponMaxDamage { get; set; } = 120m;
    public decimal OffhandWeaponSpeedSeconds { get; set; } = 1.8m;
    public decimal RangedWeaponMinDamage { get; set; } = 240m;
    public decimal RangedWeaponMaxDamage { get; set; } = 360m;
    public decimal RangedWeaponSpeedSeconds { get; set; } = 2.8m;
    public decimal HitChancePercent { get; set; } = 88m;
    public decimal CriticalChancePercent { get; set; } = 18m;
    public decimal TargetDodgePercent { get; set; } = 5m;
    public decimal TargetParryPercent { get; set; } = 5m;
    public decimal SpiritRegenPerSecond { get; set; } = 10m;
    public decimal ManaPerFiveSeconds { get; set; } = 20m;
    public decimal TargetArmor { get; set; } = 3800m;
    public decimal ArcaneShotDamage { get; set; } = 205m;
    public decimal SerpentStingTickDamage { get; set; } = 72m;
    public decimal RaptorStrikeBonusDamage { get; set; } = 70m; // verified rank 8, level 56
    public decimal MongooseBiteBonusDamage { get; set; } = 57m; // rank 4 at level 58
    public decimal WingClipDamage { get; set; } = 50m;
    public decimal ImmolationTrapTotalDamage { get; set; } = 690m; // level 56 rank, editable
    public decimal ExplosiveTrapMinDamage { get; set; } = 201m; // level 54 rank 3
    public decimal ExplosiveTrapMaxDamage { get; set; } = 257m;
    public decimal ExplosiveTrapDotTotalDamage { get; set; } = 330m;
    public decimal TrapAttackPowerCoefficient { get; set; } = .10m; // provisional: client confirms AP scaling, not coefficient
    public void Validate()
    {
        var all = new[] { BaseMana, MaximumMana, MaximumHealth, Agility, MeleeAttackPower,
            RangedAttackPower, MeleeWeaponMinDamage, MeleeWeaponMaxDamage, MeleeWeaponSpeedSeconds,
            OffhandWeaponMinDamage, OffhandWeaponMaxDamage, OffhandWeaponSpeedSeconds,
            RangedWeaponMinDamage, RangedWeaponMaxDamage, RangedWeaponSpeedSeconds, HitChancePercent,
            CriticalChancePercent, TargetDodgePercent, TargetParryPercent, SpiritRegenPerSecond,
            ManaPerFiveSeconds, TargetArmor, ArcaneShotDamage, SerpentStingTickDamage,
            RaptorStrikeBonusDamage, MongooseBiteBonusDamage, WingClipDamage,
            ImmolationTrapTotalDamage, ExplosiveTrapMinDamage, ExplosiveTrapMaxDamage,
            ExplosiveTrapDotTotalDamage, TrapAttackPowerCoefficient };
        if (all.Any(v => v < 0m) || BaseMana <= 0m || MaximumMana <= 0m || MaximumHealth <= 0m ||
            MeleeWeaponSpeedSeconds <= 0m || OffhandWeaponSpeedSeconds <= 0m ||
            RangedWeaponSpeedSeconds <= 0m || OffhandWeaponMinDamage > OffhandWeaponMaxDamage ||
            MeleeWeaponMinDamage > MeleeWeaponMaxDamage || RangedWeaponMinDamage > RangedWeaponMaxDamage ||
            ExplosiveTrapMinDamage > ExplosiveTrapMaxDamage || HitChancePercent > 100m ||
            CriticalChancePercent > 100m || TargetDodgePercent + TargetParryPercent > 100m)
            throw new ArgumentException("Invalid Survival Hunter tuning.");
    }
}

/// <summary>Ability definitions are executed by Core AbilityExecutor and AutoAttackProcessor.</summary>
public static class ForeverSurvivalHunterSpellbook
{
    private static AbilityEffectDefinition Damage(string key, decimal min, decimal max,
        string school = "physical", bool melee = false) => new() {
        Key = key, EffectType = AbilityEffectTypes.DirectDamage,
        TargetType = AbilityTargetTypes.Enemy, SchoolKey = school,
        ResolutionType = melee ? CombatResolutionTypes.Melee : CombatResolutionTypes.Spell,
        MitigationType = school == "physical" ? DamageMitigationTypes.Armor : DamageMitigationTypes.None,
        CanMiss = true, CanCrit = true, CanBeDodged = melee, CanBeParried = melee,
        MinimumValue = min, MaximumValue = max
    };
    private static AbilityDefinition Spell(string key, decimal mana, decimal cooldown,
        params AbilityEffectDefinition[] effects) => new() {
        Key = key, Name = key, ClassKey = "hunter", SpecializationKey = "survival",
        ResourceCosts = [new AbilityResourceCost { ResourceKey = "mana", Amount = mana }],
        CooldownSeconds = cooldown, GlobalCooldownSeconds = 1.5m, Effects = effects.ToList()
    };
    public static IReadOnlyList<AbilityDefinition> Create(ForeverSurvivalHunterTalents talents,
        ForeverSurvivalHunterTuning t)
    {
        talents.Validate(); t.Validate();
        var r = (string k) => talents.Rank(k);
        decimal cost = 1m - .30m * r("resourcefulness");
        decimal weapon = (t.MeleeWeaponMinDamage + t.MeleeWeaponMaxDamage) / 2m +
            (t.MeleeAttackPower + t.Agility * (r("lightning-reflexes") * .02m)) / 14m * t.MeleeWeaponSpeedSeconds;
        decimal trapBoost = 1m + .15m * r("clever-traps");
        decimal trapCooldown = 30m * (1m - .20m * r("survivalists-discipline"));
        var list = new List<AbilityDefinition> {
            // Raptor Strike is a queued next-melee-swing replacement, NOT an instant hit.
            // The reactive runner queues and pays it when the next swing resolves.
            Spell("raptor-strike", 0m, 6m),
            Spell("lacerate", 95m * cost, 0m,
                new AbilityEffectDefinition {
                    Key="lacerate-impact", EffectType=AbilityEffectTypes.ApplyAura,
                    TargetType=AbilityTargetTypes.Enemy, AuraKey="hunter-lacerate-impact",
                    DurationSeconds=.1m, ResolutionType=CombatResolutionTypes.Melee,
                    CanMiss=true,CanCrit=false,CanBeDodged=true,CanBeParried=true
                },
                new AbilityEffectDefinition {
                    Key="lacerate-tick", EffectType=AbilityEffectTypes.PeriodicDamage,
                    TargetType=AbilityTargetTypes.Enemy, AuraKey="hunter-lacerate",
                    DurationSeconds=21m, TickIntervalSeconds=3m, IncludeExpirationBoundaryTick=true,
                    ResolutionType=CombatResolutionTypes.Melee, SchoolKey="physical",
                    MitigationType=DamageMitigationTypes.None, CanMiss=false, CanCrit=true,
                    DependsOnEffectKey="lacerate-impact",
                    MinimumValue=406m/7m, MaximumValue=406m/7m
                }),
            Spell("mongoose-bite", 65m * cost, 5m,
                Damage("mongoose-impact", weapon + t.MongooseBiteBonusDamage,
                    weapon + t.MongooseBiteBonusDamage, melee:true)),
            Spell("wing-clip", 80m * cost, 0m,
                Damage("wing-clip-impact", t.WingClipDamage, t.WingClipDamage, melee:true)),
            Spell("arcane-shot", 230m, 6m,
                Damage("arcane-shot-impact", t.ArcaneShotDamage, t.ArcaneShotDamage, "arcane")),
            Spell("serpent-sting", 220m, 0m,
                new AbilityEffectDefinition {
                    Key="serpent-impact", EffectType=AbilityEffectTypes.ApplyAura,
                    TargetType=AbilityTargetTypes.Enemy, AuraKey="hunter-serpent-impact",
                    DurationSeconds=.1m, ResolutionType=CombatResolutionTypes.Spell,
                    CanMiss=true,CanCrit=false
                },
                new AbilityEffectDefinition {
                    Key="serpent-tick", EffectType=AbilityEffectTypes.PeriodicDamage,
                    TargetType=AbilityTargetTypes.Enemy, AuraKey="serpent-sting",
                    DurationSeconds=15m, TickIntervalSeconds=3m, IncludeExpirationBoundaryTick=true,
                    ResolutionType=CombatResolutionTypes.Spell, SchoolKey="nature", CanMiss=false,
                    CanCrit=true, DependsOnEffectKey="serpent-impact",
                    MinimumValue=t.SerpentStingTickDamage, MaximumValue=t.SerpentStingTickDamage
                }),
            Spell("aimed-shot", 310m, 6m,
                Damage("aimed-impact", t.RangedWeaponMaxDamage + t.RangedAttackPower / 14m *
                    t.RangedWeaponSpeedSeconds + 166m, t.RangedWeaponMaxDamage +
                    (t.RangedAttackPower + t.Agility * .02m*r("lightning-reflexes")) /
                    14m * t.RangedWeaponSpeedSeconds + 166m)),
            Spell("multi-shot", 275m, 6m,
                Damage("multi-impact", t.RangedWeaponMaxDamage + t.RangedAttackPower / 14m *
                    t.RangedWeaponSpeedSeconds + 110m, t.RangedWeaponMaxDamage +
                    t.RangedAttackPower / 14m * t.RangedWeaponSpeedSeconds + 110m)),
            Spell("immolation-trap", 245m * cost, trapCooldown,
                new AbilityEffectDefinition { Key="immolation-arming", EffectType=AbilityEffectTypes.ApplyAura,
                    TargetType=AbilityTargetTypes.Self, AuraKey="hunter-fire-trap-armed",
                    DurationSeconds=60m, CanMiss=false, CanCrit=false,
                    ResolutionType=CombatResolutionTypes.AlwaysHits }),
            Spell("explosive-trap", 520m * cost, trapCooldown,
                new AbilityEffectDefinition { Key="explosive-arming", EffectType=AbilityEffectTypes.ApplyAura,
                    TargetType=AbilityTargetTypes.Self, AuraKey="hunter-fire-trap-armed",
                    DurationSeconds=60m, CanMiss=false, CanCrit=false,
                    ResolutionType=CombatResolutionTypes.AlwaysHits }),
            Spell("frost-trap", 60m * cost, trapCooldown,
                new AbilityEffectDefinition { Key="frost-arming", EffectType=AbilityEffectTypes.ApplyAura,
                    TargetType=AbilityTargetTypes.Self, AuraKey="hunter-frost-trap-armed",
                    DurationSeconds=60m, CanMiss=false, CanCrit=false,
                    ResolutionType=CombatResolutionTypes.AlwaysHits }),
            Spell("freezing-trap", 100m * cost, trapCooldown,
                new AbilityEffectDefinition { Key="freezing-arming", EffectType=AbilityEffectTypes.ApplyAura,
                    TargetType=AbilityTargetTypes.Self, AuraKey="hunter-frost-trap-armed",
                    DurationSeconds=60m, CanMiss=false, CanCrit=false,
                    ResolutionType=CombatResolutionTypes.AlwaysHits }),
            Spell("disengage", 0m, 5m),
        };
        var rangedBonus = t.Agility * .02m * r("lightning-reflexes") / 14m * t.RangedWeaponSpeedSeconds;
        foreach (var key in new[]{ "aimed-shot", "multi-shot" }) {
            var effect = list.Single(a => a.Key == key).Effects.Single();
            effect.MinimumValue += rangedBonus;
            effect.MaximumValue += rangedBonus;
        }
        var aimed = list.Single(a => a.Key == "aimed-shot");
        var multi = list.Single(a => a.Key == "multi-shot");
        aimed.CastTimeSeconds = 2m;
        aimed.SharedCooldownGroupKey = multi.SharedCooldownGroupKey = "hunter-aimed-multi";
        aimed.SharedCooldownSeconds = multi.SharedCooldownSeconds = 6m;
        multi.Effects.Single().MaxTargets = 3;
        foreach (var ability in list.Where(a => a.Key is "immolation-trap" or "explosive-trap")) {
            ability.SharedCooldownGroupKey = "hunter-fire-traps";
            ability.SharedCooldownSeconds = trapCooldown;
        }
        foreach (var ability in list.Where(a => a.Key is "frost-trap" or "freezing-trap")) {
            ability.SharedCooldownGroupKey = "hunter-frost-traps";
            ability.SharedCooldownSeconds = trapCooldown;
        }
        list.Single(a => a.Key == "mongoose-bite").RequiredTargetOpportunityKey = "hunter-mongoose-ready";
        if (r("strider-kick") > 0)
            list.Add(Spell("strider-kick", t.BaseMana * .0581m * cost, 8m,
                Damage("strider-impact", weapon, weapon, melee:true)));
        if (r("deterrence") > 0)
            list.Add(Spell("deterrence", 0m, 300m * (1m - .20m*r("survivalists-discipline")),
                new AbilityEffectDefinition { Key="deterrence-aura", EffectType=AbilityEffectTypes.ApplyAura,
                    TargetType=AbilityTargetTypes.Self, AuraKey="hunter-deterrence", DurationSeconds=10m,
                    ResolutionType=CombatResolutionTypes.AlwaysHits, CanMiss=false, CanCrit=false }));
        if (r("counterattack") > 0) {
            var counter = Spell("counterattack", 30m*cost,5m,
                Damage("counter-impact", weapon*.5m+110m,weapon*.5m+110m, melee:true));
            counter.RequiredTargetOpportunityKey="hunter-counterattack-ready";
            counter.Effects.Single().CanBeDodged=false;
            counter.Effects.Single().CanBeParried=false;
            list.Add(counter);
        }
        return list;
    }
}
