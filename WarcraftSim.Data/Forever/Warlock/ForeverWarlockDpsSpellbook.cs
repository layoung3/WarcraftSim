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
            tuning.CorruptionMana < 0 || tuning.BaneMana < 0 || tuning.BaneOfHavocMana < 0 || tuning.ImmolateMana < 0 ||
            tuning.ShadowBoltMana < 0 || tuning.WrackMana < 0 || tuning.SoulFireMana < 0 ||
            tuning.IncinerateMana < 0 || tuning.ConflagrateMana < 0 || tuning.LifeTapSpiritCoefficient < 0 ||
            new[] { tuning.DrainLifeTick, tuning.DrainLifeMana, tuning.DrainLifeCoefficientPerTick,
                    tuning.SiphonLifeTick, tuning.SiphonLifeMana, tuning.SiphonLifeCoefficientPerTick,
                    tuning.SearingPainDamage, tuning.SearingPainMana,
                    tuning.ShadowburnDamage, tuning.ShadowburnMana }.Any(x=>x<0m))
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
                (tuning.BaneTick + spellPower * .10m) * periodicBonus * shadowBonus *
                (1m + .05m * talents.Rank("improved-bane-of-agony")), 24m, 2m));
        var immolate = spell("immolate", "Immolate", cost(tuning.ImmolateMana, true), 2m,
            hit("immolate-impact", "fire",
                (tuning.ImmolateDirect + spellPower * .20m)*
                (1m+.10m*talents.Rank("aftermath"))),
            dot("immolate-tick", "immolate", "fire",
                (tuning.ImmolateTick + spellPower * .13m) * periodicBonus, 15m));
        // The DoT is attached only when Immolate's direct impact lands.
        immolate.Effects.Single(e => e.Key == "immolate-tick")
            .DependsOnEffectKey = "immolate-impact";
        var bolt = spell("shadow-bolt", "Shadow Bolt", cost(tuning.ShadowBoltMana, true),
            Math.Max(1m, 3m - .10m * talents.Rank("bane")),
            hit("shadow-bolt-impact", "shadow", (tuning.ShadowBoltDamage + spellPower * .857m) * shadowBonus));
        // Forever 70532 Rain of Fire rank 4: 880 total over eight 1s ticks,
        // 1185 Mana, no cooldown. No invented ground-target geometry.
        var rainTick = hit("rain-of-fire-tick", "fire", 110m);
        rainTick.ApplyOnChannelTick = true;
        rainTick.MaxTargets = 9;
        var rain = spell("rain-of-fire", "Rain of Fire", 1185m, 0m, rainTick);
        rain.ChannelDurationSeconds = 8m;
        rain.ChannelTickIntervalSeconds = 1m;
        // Forever 70532 Drain Soul rank 4: 290 Mana, 420 damage over
        // a 15s channel. Chance to gain a shard from non-trivial ticks is
        // a separate tunable encounter input; a kill while draining is guaranteed.
        var drainSoulTick=hit("drain-soul-tick","shadow",84m);
        drainSoulTick.ApplyOnChannelTick=true;
        var drainSoul=spell("drain-soul","Drain Soul",290m,0m,drainSoulTick);
        drainSoul.ChannelDurationSeconds=15m;
        drainSoul.ChannelTickIntervalSeconds=3m;

        // Forever 70532 Hellfire rank 3: each 1s channel tick deals 206 Fire
        // to nearby enemies AND 206 damage to the caster. No invented geometry.
        var hellfireEnemy=hit("hellfire-enemy","fire",206m);
        hellfireEnemy.ApplyOnChannelTick=true;
        hellfireEnemy.MaxTargets=9;
        var hellfireSelf=hit("hellfire-self","fire",206m);
        hellfireSelf.ApplyOnChannelTick=true;
        hellfireSelf.TargetType=AbilityTargetTypes.Self;
        hellfireSelf.ResolutionType=CombatResolutionTypes.AlwaysHits;
        hellfireSelf.CanMiss=false;
        hellfireSelf.CanCrit=false;
        var hellfire=spell("hellfire","Hellfire",1300m,0m,hellfireEnemy,hellfireSelf);
        hellfire.ChannelDurationSeconds=15m;
        hellfire.ChannelTickIntervalSeconds=1m;

        // Forever 70532 Death Coil rank 3: 454 Shadow, 600 Mana, instant,
        // 2-minute cooldown, three-second horror. Healing is equal to actual
        // enemy damage and is resolved by DamageToSelfHealingProcessor.
        var deathImpact=hit("death-coil-impact","shadow",454m);
        var deathFear=new AbilityEffectDefinition {
            Key="death-coil-horror",EffectType=AbilityEffectTypes.ApplyAura,
            TargetType=AbilityTargetTypes.Enemy,AuraKey="warlock-death-coil-horror",
            DurationSeconds=3m,DependsOnEffectKey="death-coil-impact",
            ResolutionType=CombatResolutionTypes.AlwaysHits,
            CanMiss=false,CanCrit=false
        };
        var deathCoil=spell("death-coil","Death Coil",600m,0m,
            deathImpact,deathFear);
        deathCoil.CooldownSeconds=120m;

        // Forever 70532 Bane of Doom: 300 Mana, instant, 60-second cooldown,
        // one 1742 Shadow damage pulse at the 60-second expiration boundary.
        // Conditional Doomguard spawn after an eligible kill is encounter-
        // dependent, and the undocumented proc chance is not invented.
        var doomApplication=new AbilityEffectDefinition {
            Key="bane-of-doom-application",
            EffectType=AbilityEffectTypes.ApplyAura,
            TargetType=AbilityTargetTypes.Enemy,SchoolKey="shadow",
            AuraKey="bane-of-doom-application",DurationSeconds=.1m,
            ResolutionType=CombatResolutionTypes.Spell,
            CanMiss=true,CanCrit=false
        };
        var doomTick=dot("bane-of-doom-tick","bane-of-doom","shadow",
            1742m*shadowBonus,60m,60m);
        doomTick.DependsOnEffectKey="bane-of-doom-application";
        doomTick.CanMiss=false;
        var doom=spell("bane-of-doom","Bane of Doom",300m,0m,
            doomApplication,doomTick);
        doom.CooldownSeconds=60m;

        // Forever 70532 level-60 Curse of the Elements rank 4: 200 Mana,
        // 300s on target, +10% all magical damage taken and -75 resistance.
        // Vulnerability resolves against live target auras; resistance reduction
        // awaits independently verified resistance/partial-resist behavior.
        var curseOfElements = spell("curse-of-the-elements", "Curse of the Elements",
            200m, 0m, new AbilityEffectDefinition {
                Key="curse-of-the-elements-application",
                EffectType=AbilityEffectTypes.ApplyAura,
                TargetType=AbilityTargetTypes.Enemy, SchoolKey="shadow",
                ResolutionType=CombatResolutionTypes.Spell,
                CanMiss=true, CanCrit=false,
                AuraKey="curse-of-the-elements", DurationSeconds=300m
            });
        // Current Forever 70532 Curse of Recklessness rank 4 (level 56):
        // 115 Mana, 120s, reduces enemy Armor by 505. It is a Curse,
        // exclusive with another Curse from this owner, not with Banes.
        var reckless=spell("curse-of-recklessness","Curse of Recklessness",
            115m,0m,new AbilityEffectDefinition {
                Key="recklessness-application",EffectType=AbilityEffectTypes.ApplyAura,
                TargetType=AbilityTargetTypes.Enemy,SchoolKey="shadow",
                ResolutionType=CombatResolutionTypes.Spell,
                AuraKey="curse-of-recklessness",DurationSeconds=120m,
                CanMiss=true,CanCrit=false
            });

        var amplifyCurse=spell("amplify-curse","Amplify Curse",0m,0m,
            new AbilityEffectDefinition {
                Key="amplify-curse-aura",EffectType=AbilityEffectTypes.ApplyAura,
                TargetType=AbilityTargetTypes.Self,AuraKey="warlock-amplify-curse",
                DurationSeconds=30m,ResolutionType=CombatResolutionTypes.AlwaysHits,
                CanMiss=false,CanCrit=false
            });
        amplifyCurse.CooldownSeconds=180m;
        var spells = new List<AbilityDefinition> { corruption, bane, immolate,
            bolt, rain, deathCoil, doom, curseOfElements, reckless, drainSoul, hellfire };
        if(talents.Rank("amplify-curse")>0)spells.Add(amplifyCurse);
        if (talents.Rank("bane-of-havoc") > 0)
        {
            // Client: 5% base Mana, instant, five-minute duration, one target.
            // Position and secondary-target selection remain encounter inputs.
            spells.Add(spell("bane-of-havoc", "Bane of Havoc",
                tuning.BaneOfHavocMana, 0m, new AbilityEffectDefinition {
                    Key="havoc-application",EffectType=AbilityEffectTypes.ApplyAura,
                    TargetType=AbilityTargetTypes.Enemy,SchoolKey="shadow",
                    ResolutionType=CombatResolutionTypes.Spell,
                    CanMiss=true,CanCrit=false,AuraKey="bane-of-havoc",
                    DurationSeconds=300m
                }));
        }
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
        // Forever 70532 Health Funnel R7: 10s channel, 1s healing ticks
        // transferring 153 HP/tick to the active demon at 79 HP/tick
        // caster cost. Conditional pet targeting and the per-tick health
        // transfer are resolved by WarlockHealthFunnelProcessor, sharing
        // AbilityExecutor cast/channel lifecycle and cancellation.
        var healthFunnel=spell("health-funnel","Health Funnel",0m,0m);
        healthFunnel.ChannelDurationSeconds=10m;
        healthFunnel.ChannelTickIntervalSeconds=1m;
        spells.Add(healthFunnel);
        // Forever Drain Life R6: 51 Shadow health transferred per second, five ticks.
        // Drain scaling is provisional pending a measured level-60 coefficient.
        var drainTick = hit("drain-life-tick", "shadow",
            (tuning.DrainLifeTick + spellPower * tuning.DrainLifeCoefficientPerTick) *
            shadowBonus * (1m + .2m * talents.Rank("improved-drains") / 3m));
        drainTick.ApplyOnChannelTick = true;
        var drain = spell("drain-life", "Drain Life", tuning.DrainLifeMana, 0m, drainTick);
        drain.ChannelDurationSeconds = 5m;
        drain.ChannelTickIntervalSeconds = 1m;
        spells.Add(drain);
        // Rank 6 Searing Pain is available to all Warlocks, including Demonology.
        var searing = spell("searing-pain", "Searing Pain", cost(tuning.SearingPainMana, true),
            1.5m, hit("searing-pain-impact", "fire",
                tuning.SearingPainDamage + spellPower * .429m));
        spells.Add(searing);
        if (spec == "affliction")
        {
            if (talents.Rank("siphon-life") > 0)
                spells.Add(spell("siphon-life", "Siphon Life", tuning.SiphonLifeMana, 0m,
                    dot("siphon-life-tick", "siphon-life", "shadow",
                        (tuning.SiphonLifeTick + spellPower * tuning.SiphonLifeCoefficientPerTick) *
                        shadowBonus, 30m, 3m)));
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
            // Current Forever Decimation: permanent Soul Fire cooldown reduction
            // by 45% / 90%; the temporary execute window is handled at runtime.
            soulFire.CooldownSeconds = 60m * (1m - .45m * talents.Rank("decimation"));
            soulFire.ResourceCosts.Add(new AbilityResourceCost {
                ResourceKey = "soul-shard", Amount = 1m
            });
            spells.Add(soulFire);
        }
        if (spec == "destruction")
        {
            if (talents.Rank("shadowburn") > 0)
            {
                var shadowburn = spell("shadowburn", "Shadowburn", tuning.ShadowburnMana, 0m,
                    hit("shadowburn-impact", "shadow",
                        (tuning.ShadowburnDamage + spellPower * .429m) * shadowBonus));
                shadowburn.CooldownSeconds = 15m;
                shadowburn.ResourceCosts.Add(new AbilityResourceCost {
                    ResourceKey = "soul-shard", Amount = 1m
                });
                spells.Add(shadowburn);
            }
            if (talents.Rank("incinerate") > 0)
                spells.Add(spell("incinerate", "Incinerate", cost(tuning.IncinerateMana, true),
                    Math.Max(1m, 2.5m - .1m * talents.Rank("bane")),
                    hit("incinerate-impact", "fire", tuning.IncinerateDamage + spellPower * .714m)));
            var conflagrate = spell("conflagrate", "Conflagrate", cost(tuning.ConflagrateMana, true), 0m,
                hit("conflagrate-impact", "fire", tuning.ConflagrateDamage + spellPower * .429m));
            conflagrate.CooldownSeconds = 10m;
            // Immolate consumption is resolved only after a landed damage event
            // by WarlockConflagrateConsumptionProcessor in the API runner.
            if (talents.Rank("conflagrate") > 0) spells.Add(conflagrate);
        }
        return spells;
    }
}

public sealed class ForeverWarlockTuning
{
    // Explicit, provisional level-60 beta values (not guaranteed client ranks).
    public decimal CorruptionTick { get; set; } = 130m;
    public decimal CorruptionMana { get; set; } = 320m;
    // Current Forever 70532 rank 6: 552 base Shadow over 24s/12 ticks,
    // or 46 unmodified average per tick. It ramps instead of hitting flat.
    public decimal BaneTick { get; set; } = 46m;
    public decimal BaneMana { get; set; } = 215m;
    // Client tooltip: 5% base Mana (approximately 43 at level 60);
    // kept tunable because derived base Mana differs from equipped maximum.
    public decimal BaneOfHavocMana { get; set; } = 43m;
    public decimal ImmolateDirect { get; set; } = 230m;
    public decimal ImmolateTick { get; set; } = 78m;
    public decimal ImmolateMana { get; set; } = 380m;
    public decimal ShadowBoltDamage { get; set; } = 268m;
    public decimal ShadowBoltMana { get; set; } = 360m;
    public decimal DrainLifeTick { get; set; } = 51m; // Forever level-54 rank 6.
    public decimal DrainLifeMana { get; set; } = 300m;
    public decimal DrainLifeCoefficientPerTick { get; set; } = .1m; // provisional
    public decimal SiphonLifeTick { get; set; } = 41m; // Forever level-58 rank 4.
    public decimal SiphonLifeMana { get; set; } = 365m;
    public decimal SiphonLifeCoefficientPerTick { get; set; } = .1m; // provisional
    public decimal SearingPainDamage { get; set; } = 114m; // Forever rank 6, 105-123
    public decimal SearingPainMana { get; set; } = 168m;
    public decimal ShadowburnDamage { get; set; } = 266m; // Forever rank 6, 251-281
    public decimal ShadowburnMana { get; set; } = 365m;
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
