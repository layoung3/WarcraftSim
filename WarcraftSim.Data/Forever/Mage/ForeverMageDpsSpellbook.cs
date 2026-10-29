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
            Math.Max(1m, tuning.FireballCastSeconds - .1m * t.Rank("accelerant")),
            hit("fireball-impact", "fire", tuning.FireballDamage + sp * .857m, fire),
            dot("fireball-dot-tick","fireball-dot","fire",
                tuning.FireballDotTick*fire,8m,2m));
        // Level-60 rank 12: the 60-damage/8s DoT is attached only when
        // direct Fireball lands; missed spells apply neither component.
        fireball.Effects[1].DependsOnEffectKey="fireball-impact";
        fireball.Effects[1].CanMiss=false;
        // Unlike a direct Fireball impact, the legacy 2-second damage
        // component does not have independently confirmed crit eligibility.
        // Keep it non-critical pending a current-client proc-mask source.
        fireball.Effects[1].CanCrit=false;
        var frostbolt = spell("frostbolt", "Frostbolt", tuning.FrostboltMana,
            Math.Max(1m,tuning.FrostboltCastSeconds - .1m*t.Rank("improved-frostbolt")),
            hit("frostbolt-impact", "frost", tuning.FrostboltDamage + sp * .814m, frost));
        var frostfire = spell("frostfire-bolt", "Frostfire Bolt", tuning.FrostfireBoltMana,
            Math.Max(1m, 3m - t.Rank("accelerant") * .1m),
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
        // Accelerant was expanded in 70685 to Arcane Wrath as well.
        arcaneWrath.CastTimeSeconds=Math.Max(1m,
            arcaneWrath.CastTimeSeconds-.1m*t.Rank("accelerant"));
        arcaneWrath.CooldownSeconds = 6m;
        // October 2026 Forever Frost Channeling: -5% Frost spell mana/rank.
        // Frostfire Bolt is both Fire and Frost, so it qualifies as Frost.
        var frostCostMultiplier = 1m - .05m * t.Rank("frost-channeling");
        foreach (var frostSpell in new[] { frostbolt, frostfire })
            foreach (var cost in frostSpell.ResourceCosts.Where(c => c.ResourceKey == "mana"))
                cost.Amount *= frostCostMultiplier;
        // Utility slow replaces extreme Frostbolt downranking, not a default raid filler.
        var frostBlast = spell("frost-blast", "Frost Blast", tuning.FrostBlastMana,
            Math.Max(1m,1.5m-.1m*t.Rank("improved-frostbolt")),
            hit("frost-blast-impact", "frost", tuning.FrostBlastDamage + sp * tuning.FrostBlastSpellPowerCoefficient, frost),
            new AbilityEffectDefinition {
                Key="frost-blast-chill",EffectType=AbilityEffectTypes.ApplyAura,
                TargetType=AbilityTargetTypes.Enemy,SchoolKey="frost",
                AuraKey="mage-frost-blast-chill",
                DurationSeconds=9m*(1m+.11m*t.Rank("permafrost")),
                DependsOnEffectKey="frost-blast-impact",
                ResolutionType=CombatResolutionTypes.AlwaysHits,
                CanMiss=false,CanCrit=false
            });
        foreach (var cost in frostBlast.ResourceCosts.Where(c => c.ResourceKey == "mana"))
            cost.Amount *= frostCostMultiplier;
        var missilesEffect = hit("arcane-missiles-tick", "arcane",
            (tuning.ArcaneMissilesTick + sp * .19m) * arcane);
        missilesEffect.ApplyOnChannelTick = true;
        var missiles = spell("arcane-missiles", "Arcane Missiles", tuning.ArcaneMissilesMana, 0m,
            missilesEffect);
        missiles.ChannelDurationSeconds = 5m;
        missiles.ChannelTickIntervalSeconds = 1m;
        // 70630 level-60 Pyroblast rank 8: 520-646 direct and 212 over 12s
        // (53 per 3-second tick). Both components require landed direct impact.
        // Exact spell-power distribution between the two effects is provisional.
        var pyro = spell("pyroblast", "Pyroblast", tuning.PyroblastMana, 6m,
            hit("pyroblast-impact", "fire", tuning.PyroblastDamage + sp * 1.15m, fire),
            dot("pyroblast-dot-tick", "pyroblast-dot", "fire",
                (tuning.PyroblastDotTick + sp * tuning.PyroblastDotPowerPerTick) * fire,
                12m, 3m));
        pyro.Effects[1].DependsOnEffectKey = "pyroblast-impact";
        pyro.Effects[1].CanMiss = false;
        // Forever client 70630 rank 7: 166-196 Fire damage, 150 Mana, 1.5s cast.
        // The 1.5/3.5 direct-spell coefficient follows existing caster conventions.
        var scorch = spell("scorch", "Scorch", tuning.ScorchMana, 1.5m,
            hit("scorch-impact", "fire", tuning.ScorchDamage + sp * .429m, fire));
        var fireblast = spell("fire-blast", "Fire Blast", tuning.FireBlastMana, 0m,
            hit("fire-blast-impact", "fire", tuning.FireBlastDamage + sp * .429m, fire));
        // Current 70532 Improved Fire Blast: -1/-2 seconds; additional
        // range is encounter geometry, not bonus damage.
        fireblast.CooldownSeconds = 8m - t.Rank("improved-fire-blast");
        var lance = spell("ice-lance", "Ice Lance", tuning.IceLanceMana, 0m,
            hit("ice-lance-impact", "frost", tuning.IceLanceDamage + sp * .143m, frost));
        foreach (var cost in lance.ResourceCosts.Where(c => c.ResourceKey == "mana"))
            cost.Amount *= frostCostMultiplier;
        // Build 70532 Cone of Cold rank 5: 325-355 Frost, 555 Mana,
        // 10-second cooldown. Stationary solo target is assumed in the cone;
        // slow geometry and target cap remain encounter-dependent.
        var cone = spell("cone-of-cold", "Cone of Cold", 555m *
            frostCostMultiplier, 0m,
            hit("cone-of-cold-impact", "frost", 340m + sp * .129m,
                frost*(1m+(t.Rank("improved-cone-of-cold") switch {
                    1=>.12m, 2=>.23m, 3=>.35m, _=>0m }))));
        cone.CooldownSeconds = 10m;
        // Cone of Cold is a frontal AoE. Solo geometry is represented by
        // explicit configured enemies only, never invented movement/range.
        cone.Effects[0].MaxTargets=9;
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
        var blizzard = spell("blizzard", "Blizzard", 1400m * frostCostMultiplier, 0m, blizzardTick);
        blizzard.ChannelDurationSeconds = 8m;
        blizzard.ChannelTickIntervalSeconds = 1m;
        // Forever 70532 rank 6: 375-459 direct Fire and 332 over eight seconds.
        // Only one target-area ground effect is intentionally allowed in the
        // solo rotation; the engine resolves each configured enemy independently.
        var flamestrike = spell("flamestrike", "Flamestrike", 990m, 3m,
            hit("flamestrike-impact", "fire", 417m, fire),
            dot("flamestrike-ground-tick", "flamestrike", "fire",
                83m * fire, 8m, 2m));
        flamestrike.Effects[0].MaxTargets = 9;
        flamestrike.Effects[0].BonusCriticalChancePercent =
            5m * t.Rank("improved-flamestrike");
        flamestrike.Effects[1].MaxTargets = 9;
        flamestrike.Effects[1].CanMiss = false;
        flamestrike.Effects[1].DependsOnEffectKey = "flamestrike-impact";
        flamestrike.ForbiddenTargetAuraKeys.Add("flamestrike");
        // Current Forever 70630 rank 5 Blast Wave: 453-533 Fire, 545 Mana,
        // instant with a 45s cooldown and 6s movement-speed daze.
        // Distance/radius remains caller-supplied through explicit enemy sets.
        var blastWaveDamage=hit("blast-wave-impact","fire",493m+sp*.129m,fire);
        blastWaveDamage.MaxTargets=9;
        var blastWaveSlow=new AbilityEffectDefinition {
            Key="blast-wave-daze",EffectType=AbilityEffectTypes.ApplyAura,
            TargetType=AbilityTargetTypes.Enemy,AuraKey="mage-blast-wave-daze",
            DurationSeconds=6m,MaxTargets=9,
            DependsOnEffectKey="blast-wave-impact",
            ResolutionType=CombatResolutionTypes.AlwaysHits,
            CanMiss=false,CanCrit=false
        };
        var blastWave=spell("blast-wave","Blast Wave",545m,0m,
            blastWaveDamage,blastWaveSlow);
        blastWave.CooldownSeconds=45m;
        // Trainer spells belong to every Mage; medal-talent abilities must
        // only exist when their owner invested the corresponding talent point.
        var spells = new List<AbilityDefinition> {arcaneExplosion, blizzard, flamestrike,
            fireball, frostbolt, frostfire, frostBlast, missiles, scorch, fireblast, cone,
            ignite, evocation, potion};
        if (t.Rank("blast-wave")>0) spells.Add(blastWave);
        if (t.Rank("arcane-blast")>0) spells.Add(arcaneBlast);
        if (t.Rank("arcane-wrath")>0) spells.Add(arcaneWrath);
        if (t.Rank("pyroblast")>0) spells.Add(pyro);
        if (t.Rank("ice-lance")>0) spells.Add(lance);
        if (t.Rank("presence-of-mind") > 0)
        {
            var pom = spell("presence-of-mind", "Presence of Mind", 0m, 0m,
                new AbilityEffectDefinition {
                    Key = "presence-of-mind-buff", EffectType = AbilityEffectTypes.ApplyAura,
                    TargetType = AbilityTargetTypes.Self, AuraKey = "mage-presence-of-mind",
                    DurationSeconds = 1201m, ResolutionType = CombatResolutionTypes.AlwaysHits,
                    CanMiss = false, CanCrit = false
                });
            pom.CooldownSeconds = 180m;
            pom.IsOffGlobalCooldown = true;
            pom.GlobalCooldownSeconds = 0m;
            pom.ForbiddenSourceAuraKeys.Add("mage-presence-of-mind");
            spells.Add(pom);
        }
        if (t.Rank("arcane-power") > 0)
        {
            var power = spell("arcane-power", "Arcane Power", 0m, 0m,
                new AbilityEffectDefinition {
                    Key = "arcane-power-buff", EffectType = AbilityEffectTypes.ApplyAura,
                    TargetType = AbilityTargetTypes.Self, AuraKey = "mage-arcane-power",
                    DurationSeconds = 15m, ResolutionType = CombatResolutionTypes.AlwaysHits,
                    CanMiss = false, CanCrit = false
                });
            power.CooldownSeconds = 180m;
            power.IsOffGlobalCooldown = true;
            power.GlobalCooldownSeconds = 0m;
            power.ForbiddenSourceAuraKeys.Add("mage-arcane-power");
            spells.Add(power);
        }
        // Mage Armor rank 3: 490 Mana, 30 minutes, +15 all magic
        // resistances and 50% Spirit regeneration while casting.
        // Arcane Shielding strengthens only the resistance component.
        var armor=spell("mage-armor","Mage Armor",490m,0m,
            new AbilityEffectDefinition {
                Key="mage-armor-aura",EffectType=AbilityEffectTypes.ApplyAura,
                TargetType=AbilityTargetTypes.Self,AuraKey="mage-armor",
                DurationSeconds=1800m,ResolutionType=CombatResolutionTypes.AlwaysHits,
                CanMiss=false,CanCrit=false
            });
        spells.Add(armor);
        // Current 70532 Ice Block rank 1: 15 Mana, ten seconds
        // invulnerable to incoming damage and unable to cast, 5-minute CD.
        if(t.Rank("ice-block")>0)
        {
            var iceBlock=spell("ice-block","Ice Block",15m,0m,
                new AbilityEffectDefinition {
                    Key="ice-block-aura",EffectType=AbilityEffectTypes.ApplyAura,
                    TargetType=AbilityTargetTypes.Self,AuraKey="mage-ice-block",
                    DurationSeconds=10m,ResolutionType=CombatResolutionTypes.AlwaysHits,
                    CanMiss=false,CanCrit=false
                });
            iceBlock.CooldownSeconds=300m;
            spells.Add(iceBlock);
        }
        // Current 70532 Ice Barrier rank 4: 480 Mana, 811 absorbed,
        // 60s duration, 30s cooldown. The shared AbsorbManager handles
        // damage depletion and expiration; no artificial DPS contribution.
        if(t.Rank("ice-barrier")>0)
        {
            var barrier=spell("ice-barrier","Ice Barrier",480m,0m,
                new AbilityEffectDefinition {
                    Key="ice-barrier-absorb",EffectType=AbilityEffectTypes.Absorb,
                    TargetType=AbilityTargetTypes.Self,AbsorbKey="mage-ice-barrier",
                    DurationSeconds=60m,MinimumValue=811m,MaximumValue=811m,
                    ResolutionType=CombatResolutionTypes.AlwaysHits,
                    CanMiss=false,CanCrit=false
                });
            barrier.CooldownSeconds=30m;
            spells.Add(barrier);
        }
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
        // The current Ice Block aura prevents any offensive cast, recovery
        // or other spell while active. The Ice Block ability itself cannot
        // be reused until its cooldown ends.
        foreach(var learned in spells)
            if(learned.Key!="ice-block")
                learned.ForbiddenSourceAuraKeys.Add("mage-ice-block");
        return spells;
    }
}

public sealed class ForeverMageTuning
{
    // Illustrative level-60 values. Not verified against final game spell ranks.
    // Forever 1.60.1.70532 rank 12, also confirmed in 70634:
    // 425-541 direct (483 average), 60 Fire DoT over 8s, 410 Mana.
    public decimal FireballDamage { get; set; } = 483m;
    public decimal FireballDotTick { get; set; } = 15m;
    public decimal FireballMana { get; set; } = 410m;
    public decimal FireballCastSeconds { get; set; } = 3.5m;
    public decimal FrostboltDamage { get; set; } = 410m;
    public decimal FrostboltMana { get; set; } = 330m;
    public decimal FrostboltCastSeconds { get; set; } = 3m;
    public decimal ArcaneBlastDamage { get; set; } = 394m; // Pre-70425 rank-5 avg; spellbook applies 40% reduction.
    // Forever 70532 rank 5: 15% of base Mana. The solo level-60
    // baseline currently uses 1376 Mana, separate from Intellect/gear.
    // The base-Mana baseline remains provisional, not the 15% rule.
    public decimal ArcaneBlastMana { get; set; } = 1376m * .15m;
    // Retained for backwards-compatible old JSON; no longer used as a release bonus.
    public decimal ArcaneBlastBonusDamagePerStackPercent { get; set; } = 10m;
    public decimal ArcaneWrathDamage { get; set; } = 451m; // 70425 rank-4 406-496 average.
    public decimal ArcaneWrathMana { get; set; } = 395m; // 70425 rank-4.
    public decimal FrostBlastDamage { get; set; } = 77m; // 70425 rank-3 73-81 average.
    public decimal FrostBlastMana { get; set; } = 90m; // Rank 3, spell 1326055.
    // Numerically unresolved across 70532/70630/70634 derived exports.
    // 70630 reports 27.1%, others 34.3%. The prior simulator's 42.9%
    // remains a clearly provisional DEFAULT, not a 70685 measurement.
    public decimal FrostBlastSpellPowerCoefficient { get; set; } = .429m;
    public decimal MissileBarrageArcaneMissileChancePercentPerRank { get; set; } = 5m;
    public decimal ArcaneBlastExtraManaPerStackPercent { get; set; } = 175m;
    public decimal ArcaneMissilesTick { get; set; } = 135m;
    public decimal ArcaneMissilesMana { get; set; } = 390m;
    public decimal PyroblastDamage { get; set; } = 583m; // 70630 rank 8: (520+646)/2.
    public decimal PyroblastMana { get; set; } = 440m; // 70630 rank 8.
    public decimal PyroblastDotTick { get; set; } = 53m; // 212/4 over 12 sec.
    public decimal PyroblastDotPowerPerTick { get; set; } = .15m; // 70630 guide; provisional server value.
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
    // Forever 70532: fixed 15% for both talent ranks; rank governs charges.
    // Retain the serialized property name for compatibility with existing exports.
    public decimal FingersOfFrostChancePercentPerRank { get; set; } = 15m;
    public decimal ManaRegenPerSecond { get; set; } = 5m;
}
