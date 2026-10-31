using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Data.Forever.Hunter;

/// <summary>Supported, combat-connected Marksmanship subset; unmodeled filler is not given phantom effects.</summary>
public sealed class ForeverMarksmanshipHunterTalents
{
    public int MarksmanshipPointsSpent { get; set; } = 41;
    public int BeastMasteryPointsSpent { get; set; } = 10;
    public int SurvivalPointsSpent { get; set; }
    public Dictionary<string, int> Ranks { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["lethal-attacks"] = 5, ["efficiency"] = 5, ["careful-aim"] = 5,
        ["lone-wolf"] = 1, ["improved-stings"] = 3, ["barrage"] = 3,
        ["mortal-shots"] = 5, ["trueshot-aura"] = 1, ["sniper-shot"] = 1,
        ["improved-arcane-shot"] = 5
    };

    // Rank limits for talents without current client tooltip confirmation are intentionally
    // provisional and local to this small catalog, not asserted as the final talent tree.
    private static readonly Dictionary<string, int> Caps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["lethal-attacks"] = 5, ["efficiency"] = 5, ["careful-aim"] = 5,
        ["lone-wolf"] = 1, ["improved-stings"] = 3, ["barrage"] = 3,
        ["mortal-shots"] = 5, ["trueshot-aura"] = 1, ["sniper-shot"] = 1,
        ["improved-arcane-shot"] = 5, ["rapid-killing"] = 2,
        ["rapid-recuperation"] = 2, ["ranged-weapon-specialization"] = 5,
        ["hawk-eye"] = 3, ["improved-concussive-shot"] = 5,
        ["scatter-shot"] = 1
    };
    private static readonly IReadOnlyDictionary<string,int> EarlierPoints = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase){
        ["lone-wolf"]=10,["trueshot-aura"]=20,["sniper-shot"]=30,
        ["mortal-shots"]=15,["rapid-killing"]=10,["rapid-recuperation"]=20,
        ["ranged-weapon-specialization"]=25,["scatter-shot"]=20
    };
    public static IReadOnlyList<(string Key,string Tree,int MaxRank,int RequiredEarlierPoints)> SupportedTalentMetadata =>
        Caps.Select(x=>(x.Key,"marksmanship",x.Value,EarlierPoints.GetValueOrDefault(x.Key))).ToArray();
    public int Rank(string key) => Ranks?.GetValueOrDefault(key) ?? 0;
    public void Validate()
    {
        if (Ranks is null || MarksmanshipPointsSpent < 0 || BeastMasteryPointsSpent < 0 ||
            SurvivalPointsSpent < 0 || MarksmanshipPointsSpent + BeastMasteryPointsSpent +
            SurvivalPointsSpent > 51)
            throw new ArgumentException("Marksmanship talent budget must be 0–51 points.");
        foreach (var (key, rank) in Ranks)
            if (!Caps.TryGetValue(key, out var cap) || rank < 0 || rank > cap)
                throw new ArgumentException($"Unsupported Marksmanship talent {key}={rank}.");
        if (Ranks.Values.Sum() > MarksmanshipPointsSpent ||
            Rank("lone-wolf") > 0 && MarksmanshipPointsSpent < 11 ||
            Rank("trueshot-aura") > 0 && MarksmanshipPointsSpent < 21 ||
            Rank("sniper-shot") > 0 && (MarksmanshipPointsSpent < 31 || Rank("trueshot-aura") == 0) ||
            Rank("mortal-shots") > 0 && (MarksmanshipPointsSpent < 16 || Rank("careful-aim") < 5) ||
            Rank("rapid-killing") > 0 && MarksmanshipPointsSpent < 11 ||
            Rank("rapid-recuperation") > 0 && (MarksmanshipPointsSpent < 21 || Rank("rapid-killing") < 2) ||
            Rank("ranged-weapon-specialization") > 0 && MarksmanshipPointsSpent < 26 ||
            Rank("scatter-shot") > 0 && MarksmanshipPointsSpent < 21)
            throw new ArgumentException("Marksmanship ranks exceed budget or lack required prerequisite/tier.");
    }
}

/// <summary>Editable level-60 preview numbers; until client import, do not interpret as verified rank data.</summary>
public sealed class ForeverMarksmanshipHunterTuning
{
    public decimal RangedAttackPower { get; set; } = 1000m;
    public decimal Intellect { get; set; } = 180m;
    public decimal MaximumMana { get; set; } = 4200m;
    public decimal MaximumHealth { get; set; } = 3900m;
    public decimal WeaponMinDamage { get; set; } = 240m;
    public decimal WeaponMaxDamage { get; set; } = 360m;
    public decimal WeaponSpeedSeconds { get; set; } = 2.8m;
    public decimal HitChancePercent { get; set; } = 88m;
    public decimal CriticalChancePercent { get; set; } = 18m;
    public decimal SpiritRegenPerSecond { get; set; } = 10m;
    public decimal ManaPerFiveSeconds { get; set; } = 20m;
    public decimal TargetArmor { get; set; } = 3800m;
    public decimal ArcaneShotDamage { get; set; } = 217m; // Forever 70532 rank 8, level 60
    public decimal ArcaneShotMana { get; set; } = 190m; // Forever 70532 rank 8, level 60
    public decimal AimedShotBonusDamage { get; set; } = 166m;
    public decimal AimedShotMana { get; set; } = 310m;
    public decimal MultiShotBonusDamage { get; set; } = 0m; // Forever single rank: removed old flat-damage ranks
    public decimal MultiShotMana { get; set; } = 275m; // Provisional: Forever cost is 13.9% of BASE mana
    public decimal SerpentStingTickDamage { get; set; } = 111m; // Forever 70532 rank 9: 555/5 ticks
    public decimal SerpentStingMana { get; set; } = 250m; // Forever 70532 rank 9
    public decimal SniperShotBonusDamage { get; set; } = 295m; // Forever rank 3, level 58
    public decimal SniperShotMana { get; set; } = 365m; // Forever rank 3, level 58
    public decimal RapidFireHastePercent { get; set; } = 40m;
    public decimal RapidFireDurationSeconds { get; set; } = 15m;
    public decimal RapidFireCooldownSeconds { get; set; } = 300m;
    public decimal TrueshotAttackPower { get; set; } = 50m; // Forever level-60 rank (20906); introductory level-25 rank is 30 AP
    public void Validate()
    {
        var nonnegative = new[] { RangedAttackPower, Intellect, MaximumMana, MaximumHealth,
            WeaponMinDamage, WeaponMaxDamage, WeaponSpeedSeconds, HitChancePercent,
            CriticalChancePercent, SpiritRegenPerSecond, ManaPerFiveSeconds, TargetArmor,
            ArcaneShotDamage, ArcaneShotMana, AimedShotBonusDamage, AimedShotMana,
            MultiShotBonusDamage, MultiShotMana, SerpentStingTickDamage, SerpentStingMana,
            SniperShotBonusDamage, SniperShotMana, RapidFireHastePercent,
            RapidFireDurationSeconds, RapidFireCooldownSeconds, TrueshotAttackPower };
        if (nonnegative.Any(x => x < 0m) || WeaponSpeedSeconds <= 0m ||
            WeaponMaxDamage < WeaponMinDamage || MaximumMana <= 0m || MaximumHealth <= 0m ||
            HitChancePercent > 100m || CriticalChancePercent > 100m ||
            RapidFireDurationSeconds <= 0m || RapidFireCooldownSeconds <= 0m)
            throw new ArgumentException("Invalid provisional Hunter tuning.");
    }
}

public static class ForeverMarksmanshipHunterSpellbook
{
    public static IReadOnlyList<AbilityDefinition> Create(ForeverMarksmanshipHunterTalents talent,
        ForeverMarksmanshipHunterTuning t, decimal ap, bool hasPet = false)
    {
        talent.Validate(); t.Validate();
        if (ap < 0m) throw new ArgumentOutOfRangeException(nameof(ap));
        var lone = talent.Rank("lone-wolf") > 0 && !hasPet ? 1.20m : 1m;
        // Forever's 14 AP / weapon DPS normalization. Non-verified coefficients stay editable.
        var weaponBase = (t.WeaponMinDamage + t.WeaponMaxDamage) / 2m;
        var efficiency = 1m - talent.Rank("efficiency") * .03m; // Forever 70532: 3/6/9/12/15%
        var barrage = 1m + (talent.Rank("barrage") switch { 1 => .03m, 2 => .07m, 3 => .10m, _ => 0m });
        var stings = 1m + (talent.Rank("improved-stings") switch { 1 => .07m, 2 => .14m, 3 => .20m, _ => 0m });
        static AbilityEffectDefinition damage(string key, string school, decimal amount, bool canCrit = true) => new()
        {
            Key = key, EffectType = AbilityEffectTypes.DirectDamage,
            TargetType = AbilityTargetTypes.Enemy, SchoolKey = school,
            MitigationType = school == "physical" ? DamageMitigationTypes.Armor : DamageMitigationTypes.None,
            ResolutionType = CombatResolutionTypes.Spell, CanMiss = true, CanCrit = canCrit,
            MinimumValue = amount, MaximumValue = amount
        };
        static AbilityDefinition shot(string key, decimal mana, decimal cast, decimal cooldown,
            params AbilityEffectDefinition[] effects) => new()
        {
            Key = key, Name = key, ClassKey = "hunter", SpecializationKey = "marksmanship",
            ResourceCosts = [new AbilityResourceCost { ResourceKey = "mana", Amount = mana }],
            CastTimeSeconds = cast, CooldownSeconds = cooldown, GlobalCooldownSeconds = 1.5m,
            Effects = effects.ToList()
        };
        var serpentHit = new AbilityEffectDefinition {
            Key = "serpent-sting-impact", EffectType = AbilityEffectTypes.ApplyAura,
            TargetType = AbilityTargetTypes.Enemy, SchoolKey = "nature",
            AuraKey = "hunter-serpent-impact", DurationSeconds = .1m,
            ResolutionType = CombatResolutionTypes.Spell, CanMiss = true, CanCrit = false
        };
        var serpentTick = new AbilityEffectDefinition {
            Key = "serpent-sting-tick", EffectType = AbilityEffectTypes.PeriodicDamage,
            TargetType = AbilityTargetTypes.Enemy, SchoolKey = "nature",
            ResolutionType = CombatResolutionTypes.Spell, CanMiss = false, CanCrit = true,
            AuraKey = "serpent-sting", DurationSeconds = 15m, TickIntervalSeconds = 3m,
            IncludeExpirationBoundaryTick = true, DependsOnEffectKey = "serpent-sting-impact",
            MinimumValue = t.SerpentStingTickDamage * lone * stings,
            MaximumValue = t.SerpentStingTickDamage * lone * stings
        };
        var serpent = shot("serpent-sting", t.SerpentStingMana * efficiency, 0m, 0m,
            serpentHit, serpentTick);
        var arcane = shot("arcane-shot", t.ArcaneShotMana * efficiency, 0m,
            6m - .30m * talent.Rank("improved-arcane-shot"),
            damage("arcane-shot-impact", "arcane", t.ArcaneShotDamage * lone));
        var aimed = shot("aimed-shot", t.AimedShotMana * efficiency, 2m, 6m,
            damage("aimed-shot-impact", "physical", (weaponBase + t.AimedShotBonusDamage) * lone * barrage));
        aimed.Effects[0].ScalingStatKey = "attack-power";
        aimed.Effects[0].ScalingCoefficient = t.WeaponSpeedSeconds / 14m * lone * barrage;
        aimed.SharedCooldownGroupKey = "hunter-aimed-multi";
        aimed.SharedCooldownSeconds = 6m;
        var multiEffect = damage("multi-shot-impact", "physical",
            (weaponBase + t.MultiShotBonusDamage) * lone * barrage);
        multiEffect.ScalingStatKey = "attack-power";
        multiEffect.ScalingCoefficient = t.WeaponSpeedSeconds / 14m * lone * barrage;
        multiEffect.MaxTargets = 3;
        var multi = shot("multi-shot", t.MultiShotMana * efficiency, 0m, 6m, multiEffect);
        multi.SharedCooldownGroupKey = "hunter-aimed-multi";
        multi.SharedCooldownSeconds = 6m;
        var rapid = ForeverHunterSharedAbilities.CreateRapidFire(
            Math.Max(0m, t.RapidFireCooldownSeconds - 60m * talent.Rank("rapid-killing")),
            t.RapidFireDurationSeconds);
        var list = new List<AbilityDefinition> { serpent, arcane, aimed, multi, rapid,
            ForeverHunterSharedAbilities.CreateAspectOfTheHawk(),
            ForeverHunterSharedAbilities.CreateAspectOfTheBeast(),
            ForeverHunterSharedAbilities.CreateVolley(barrage) };
        if (talent.Rank("sniper-shot") > 0)
        {
            var sniper = shot("sniper-shot", t.SniperShotMana * efficiency, 4m, 15m,
                damage("sniper-shot-impact", "physical", (weaponBase + t.SniperShotBonusDamage) * lone * barrage));
            sniper.Effects[0].ScalingStatKey = "attack-power";
            sniper.Effects[0].ScalingCoefficient = t.WeaponSpeedSeconds / 14m * lone * barrage;
            list.Add(sniper);
        }
        return list;
    }
}
