using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Data.Forever.Rogue;

/// <summary>Active Rogue DPS subset backed by executable Core ability definitions.
/// This does not claim full baseline or talent-tree completion.</summary>
public static class ForeverRogueSpellbook
{
    public static IReadOnlyList<AbilityDefinition> Create(string spec, ForeverRogueTalentBuild talents,
        ForeverRogueTuning t, decimal attackPower, bool bothDaggers, bool behindTarget = true)
        // Compatibility callers have no independent hand identity. Preserve their
        // positional/named arguments and infer the main hand from the old flag.
        => Create(spec, talents, t, attackPower, bothDaggers, behindTarget,
            mainHandIsDagger: bothDaggers);

    public static IReadOnlyList<AbilityDefinition> Create(string spec, ForeverRogueTalentBuild talents,
        ForeverRogueTuning t, decimal attackPower, bool bothDaggers, bool behindTarget, bool mainHandIsDagger, bool useClassWideTalents = false)
    {
        if (useClassWideTalents) ForeverRogueClassTalents.Validate(talents);
        else talents.Validate(60, spec);
        t.Validate();
        if (attackPower < 0m) throw new ArgumentOutOfRangeException(nameof(attackPower));
        decimal mh = t.MainHandWeaponDamage + (useClassWideTalents ? 0m : attackPower * t.MainHandSpeedSeconds / 14m);
        var offMultiplier = .5m * (1m + .25m * talents.Rank("dual-wield-specialization"));
        var opportunity = (useClassWideTalents ? .04m : .05m) * talents.Rank("opportunity");
        var aggression = (useClassWideTalents ? .05m : .02m) * talents.Rank("aggression");
        var efficient = useClassWideTalents ? ForeverRogueClassTalents.EfficientStrikesReduction(talents.Rank("efficient-strikes")) : 0m;
        var mutilateBonus = useClassWideTalents ? t.ClassWideMutilateBonusDamagePerHand ?? 79m : t.MutilateBonusDamagePerHand;
        decimal oh = useClassWideTalents ? t.OffHandWeaponDamage * offMultiplier :
            t.OffHandWeaponDamage * (0.5m + .05m * talents.Rank("dual-wield-specialization")) +
            attackPower * t.OffHandSpeedSeconds / 14m * 0.5m;
        AbilityEffectDefinition hit(string key, decimal damage, string hand = WeaponHandKeys.MainHand) => new()
        {
            Key = key, EffectType = AbilityEffectTypes.DirectDamage,
            TargetType = AbilityTargetTypes.Enemy, ResolutionType = CombatResolutionTypes.Melee,
            SchoolKey = "physical", WeaponHandKey = hand, CanMiss = true, CanCrit = true,
            MinimumValue = Math.Max(0m, damage), MaximumValue = Math.Max(0m, damage)
        };
        AbilityDefinition spell(string key, string name, decimal energy, params AbilityEffectDefinition[] effects) => new()
        {
            Key = key, Name = name, ClassKey = "rogue", SpecializationKey = spec,
            GlobalCooldownSeconds = 1m,
            ResourceCosts = energy > 0m ? [new AbilityResourceCost { ResourceKey = "energy", Amount = energy }] : [],
            Effects = effects.ToList()
        };
        // Historical pinned rogue.json has an exact 7/13/20% table, not thirds.
        // Provenance and limits: docs/audits/CP175-rogue-sourced-ranks.md.
        var improvedEviscerate = talents.Rank(useClassWideTalents ? "coup-de-grace" : "improved-eviscerate") switch
        { 1 => .07m, 2 => .13m, 3 => .20m, _ => 0m };
        var outList = new List<AbilityDefinition>
        {
            spell("sinister-strike", "Sinister Strike", 45m - efficient,
                hit("sinister-strike-hit", (mh + t.SinisterStrikeBonusDamage) *
                    (1m + aggression))),
            spell("backstab", "Backstab", 60m,
                hit("backstab-hit", (mh * 1.5m + t.BackstabBonusDamage) *
                    (1m + opportunity + (useClassWideTalents ? 0m : aggression)))),
            spell("eviscerate", "Eviscerate", 35m,
                hit("eviscerate-hit", t.EviscerateDamagePerPoint *
                    (1m + improvedEviscerate + aggression)))
        };
        var rupture = spell("rupture", "Rupture", 25m, new AbilityEffectDefinition
        {
            Key = "rupture-tick", EffectType = AbilityEffectTypes.PeriodicDamage,
            TargetType = AbilityTargetTypes.Enemy, ResolutionType = CombatResolutionTypes.Melee,
            SchoolKey = "physical", AuraKey = "rupture", CanMiss = true, CanCrit = true,
            DurationSeconds = 8m, TickIntervalSeconds = 2m, IncludeExpirationBoundaryTick = true,
            MinimumValue = t.RuptureTickDamagePerPoint, MaximumValue = t.RuptureTickDamagePerPoint
        });
        // CP143: Stealth is an engine-enforced source aura, not a free opener flag.
        var ambush = spell("ambush", "Ambush", 60m,
            hit("ambush-hit", (mh * 2.5m + t.AmbushBonusDamage) *
                (1m + opportunity)));
        if ((useClassWideTalents ? mainHandIsDagger : bothDaggers) && behindTarget)
        {
            ambush.RequiredAnySourceAuraKeys.Add("rogue-stealth");
            ambush.RequiredAnySourceAuraKeys.Add(useClassWideTalents ? "rogue-ghostly-opener" : "rogue-cutthroat");
            ambush.Effects[0].BonusCriticalChancePercent =
                talents.Rank("improved-ambush") * 15m;
            outList.Add(ambush);
        }
        var dirtyDeedsEnergyReduction = 10m * talents.Rank("dirty-deeds");
        var garrote = spell("garrote", "Garrote", 50m - dirtyDeedsEnergyReduction, new AbilityEffectDefinition
        {
            Key = "garrote-tick", EffectType = AbilityEffectTypes.PeriodicDamage,
            TargetType = AbilityTargetTypes.Enemy, ResolutionType = CombatResolutionTypes.Melee,
            SchoolKey = "physical", AuraKey = "garrote", CanMiss = true, CanCrit = true,
            DurationSeconds = 18m + (useClassWideTalents ? 9m * talents.Rank("blood-splatter") : 0m), TickIntervalSeconds = 3m, IncludeExpirationBoundaryTick = true,
            MinimumValue = t.GarroteTickDamage * (1m + opportunity),
            MaximumValue = t.GarroteTickDamage * (1m + opportunity)
        });
        if (behindTarget || talents.Rank("dirty-deeds") > 0)
        {
            if (useClassWideTalents)
            {
                garrote.RequiredAnySourceAuraKeys.Add("rogue-stealth");
                garrote.RequiredAnySourceAuraKeys.Add("rogue-ghostly-opener");
            }
            else garrote.RequiredSourceAuraKeys.Add("rogue-stealth");
            outList.Add(garrote);
        }
        var cheap = spell("cheap-shot", "Cheap Shot", 60m - dirtyDeedsEnergyReduction,
            new AbilityEffectDefinition { Key = "cheap-shot-control", EffectType = AbilityEffectTypes.ApplyAura,
                TargetType = AbilityTargetTypes.Enemy, AuraKey = "cheap-shot-stun",
                DurationSeconds = 4m, ResolutionType = CombatResolutionTypes.AlwaysHits,
                CanMiss = false, CanCrit = false });
        cheap.RequiredSourceAuraKeys.Add("rogue-stealth");
        outList.Add(cheap);
        var vanish = spell("vanish", "Vanish", 0m,
            new AbilityEffectDefinition { Key = "vanish-stealth", EffectType = AbilityEffectTypes.ApplyAura,
                TargetType = AbilityTargetTypes.Self, AuraKey = "rogue-stealth",
                DurationSeconds = 3600m, ResolutionType = CombatResolutionTypes.AlwaysHits,
                CanMiss = false, CanCrit = false });
        vanish.CooldownSeconds = 300m - talents.Rank("elusiveness") * 45m;
        vanish.IsOffGlobalCooldown = true;
        outList.Add(vanish);
        var expose = spell("expose-armor", "Expose Armor",
            Math.Max(0m, 25m - talents.Rank("improved-expose-armor") *
                t.ImprovedExposeArmorEnergyDiscountPerRank),
            new AbilityEffectDefinition { Key = "expose-armor-debuff", EffectType = AbilityEffectTypes.ApplyAura,
                TargetType = AbilityTargetTypes.Enemy, AuraKey = "expose-armor",
                DurationSeconds = 30m, ResolutionType = CombatResolutionTypes.Melee,
                CanMiss = true, CanCrit = false });
        outList.Add(expose);
        outList.Add(spell("kidney-shot", "Kidney Shot", 25m,
            new AbilityEffectDefinition { Key = "kidney-shot-control", EffectType = AbilityEffectTypes.ApplyAura,
                TargetType = AbilityTargetTypes.Enemy, AuraKey = "kidney-shot-stun",
                DurationSeconds = 4m, ResolutionType = CombatResolutionTypes.AlwaysHits,
                CanMiss = false, CanCrit = false }));
        if (talents.Rank("riposte") > 0)
        {
            var riposte = spell("riposte", "Riposte", 10m,
                hit("riposte-hit", mh * 1.5m));
            if(useClassWideTalents)
                riposte.Effects.Add(new AbilityEffectDefinition { Key="riposte-disarm",EffectType=AbilityEffectTypes.ApplyAura,
                    TargetType=AbilityTargetTypes.Enemy,AuraKey="rogue-riposte-disarm",DurationSeconds=6m,
                    DependsOnEffectKey="riposte-hit",DependencyCondition=EffectDependencyConditions.Landed,
                    ResolutionType=CombatResolutionTypes.AlwaysHits,CanMiss=false,CanCrit=false });
            riposte.CooldownSeconds = 6m;
            riposte.RequiredSourceAuraKeys.Add("riposte-ready");
            outList.Add(riposte);
        }
        if (talents.Rank("premeditation") > 0)
        {
            var premed = spell("premeditation", "Premeditation", 0m,
                new AbilityEffectDefinition { Key = "premeditation-marker", EffectType = AbilityEffectTypes.ApplyAura,
                    AuraKey = "premeditation-mark", DurationSeconds = 20m,
                    TargetType = AbilityTargetTypes.Self, ResolutionType = CombatResolutionTypes.AlwaysHits,
                    CanMiss = false, CanCrit = false });
            premed.RequiredSourceAuraKeys.Add("rogue-stealth");
            premed.CooldownSeconds = 120m; premed.IsOffGlobalCooldown = true;
            outList.Add(premed);
        }
        if (talents.Rank("ghostly-strike") > 0)
        {
            var ghost = spell("ghostly-strike", "Ghostly Strike", 40m,
                // Historical pinned rogue.json explicitly identifies the main hand.
                hit("ghostly-strike-hit", mh * (mainHandIsDagger ? 1.80m : 1.25m)),
                new AbilityEffectDefinition { Key = "ghostly-strike-evasion", EffectType = AbilityEffectTypes.ApplyAura,
                    AuraKey = "ghostly-strike-dodge", DurationSeconds = 7m,
                    TargetType = AbilityTargetTypes.Self, ResolutionType = CombatResolutionTypes.AlwaysHits,
                    CanMiss = false, CanCrit = false });
            if(useClassWideTalents)
            {
                ghost.Effects[1].DependsOnEffectKey="ghostly-strike-hit";
                ghost.Effects[1].DependencyCondition=EffectDependencyConditions.Landed;
            }
            ghost.CooldownSeconds = 20m; outList.Add(ghost);
        }
        if (talents.Rank("cold-blood") > 0)
        {
            var cold = spell("cold-blood", "Cold Blood", 0m,
                new AbilityEffectDefinition { Key = "cold-blood-aura", EffectType = AbilityEffectTypes.ApplyAura,
                    AuraKey = "cold-blood", DurationSeconds = 30m, TargetType = AbilityTargetTypes.Self,
                    ResolutionType = CombatResolutionTypes.AlwaysHits, CanMiss = false, CanCrit = false });
            cold.CooldownSeconds = 180m; cold.IsOffGlobalCooldown = true; outList.Add(cold);
        }
        if (useClassWideTalents ? mainHandIsDagger : bothDaggers)
            outList.First(a => a.Key == "backstab").Effects[0].BonusCriticalChancePercent =
                talents.Rank("puncturing-wounds") * t.PuncturingWoundsBackstabCritPerRank;
        if (!(useClassWideTalents ? mainHandIsDagger : bothDaggers) || !behindTarget) outList.RemoveAll(a => a.Key == "backstab");
        outList.Add(rupture);
        outList.Add(spell("slice-and-dice", "Slice and Dice", 25m, new AbilityEffectDefinition
        {
            Key = "slice-and-dice-aura", EffectType = AbilityEffectTypes.ApplyAura,
            AuraKey = "slice-and-dice", DurationSeconds = 9m, TargetType = AbilityTargetTypes.Self,
            ResolutionType = CombatResolutionTypes.AlwaysHits, CanCrit = false, CanMiss = false
        }));
        if (talents.Rank("mutilate") > 0 && bothDaggers)
            outList.Add(spell("mutilate", "Mutilate", 60m,
                hit("mutilate-main", ((useClassWideTalents ? .9m : .75m) * mh + mutilateBonus) *
                    (1m + opportunity)),
                hit("mutilate-off", ((useClassWideTalents ? .9m : .75m) * oh + mutilateBonus * (useClassWideTalents ? offMultiplier : 1m)) *
                    (1m + opportunity), WeaponHandKeys.OffHand)));
        if (outList.FirstOrDefault(a => a.Key == "mutilate") is { } mutilate)
            foreach (var effect in mutilate.Effects)
                effect.BonusCriticalChancePercent =
                    talents.Rank("puncturing-wounds") * t.PuncturingWoundsMutilateCritPerRank;
        if (!useClassWideTalents && talents.Rank("venom") > 0)
            outList.Add(spell("venom", "Venom", 25m, new AbilityEffectDefinition
            {
                Key = "venom-aura", EffectType = AbilityEffectTypes.ApplyAura,
                AuraKey = "venom", DurationSeconds = 9m, TargetType = AbilityTargetTypes.Self,
                ResolutionType = CombatResolutionTypes.AlwaysHits, CanMiss = false, CanCrit = false
            }));
        if (talents.Rank("hemorrhage") > 0)
            outList.Add(spell("hemorrhage", "Hemorrhage", 35m - efficient,
                hit("hemorrhage-hit", useClassWideTalents ? mh * (mainHandIsDagger ? 1.45m : 1m) : mh + t.HemorrhageBonusDamage),
                new AbilityEffectDefinition { Key = "hemorrhage-mark", EffectType = AbilityEffectTypes.ApplyAura,
                    AuraKey = "hemorrhage-rupture", TargetType = AbilityTargetTypes.Enemy,
                    DependsOnEffectKey = "hemorrhage-hit", DependencyCondition = EffectDependencyConditions.Landed,
                    ResolutionType = CombatResolutionTypes.AlwaysHits, DurationSeconds = 15m,
                    CanMiss = false, CanCrit = false }));
        if (talents.Rank("adrenaline-rush") > 0)
        {
            var rush = spell("adrenaline-rush", "Adrenaline Rush", 0m,
                new AbilityEffectDefinition { Key = "rush-aura", EffectType = AbilityEffectTypes.ApplyAura,
                    AuraKey = "adrenaline-rush", TargetType = AbilityTargetTypes.Self,
                    ResolutionType = CombatResolutionTypes.AlwaysHits,
                    DurationSeconds = t.AdrenalineRushDurationSeconds, CanMiss = false, CanCrit = false });
            rush.CooldownSeconds = 300m;
            rush.IsOffGlobalCooldown = true;
            outList.Add(rush);
        }
        if (talents.Rank("blade-flurry") > 0)
        {
            var flurry = spell("blade-flurry", "Blade Flurry", 25m,
                new AbilityEffectDefinition { Key = "blade-flurry-aura", EffectType = AbilityEffectTypes.ApplyAura,
                    AuraKey = "blade-flurry", TargetType = AbilityTargetTypes.Self,
                    ResolutionType = CombatResolutionTypes.AlwaysHits, DurationSeconds = 15m,
                    CanMiss = false, CanCrit = false });
            flurry.CooldownSeconds = 120m;
            outList.Add(flurry);
        }
        // Utility is executable and cooldown/energy gated. The stationary boss model
        // does not fake interrupt, blind, snare or dodge outcomes without enemy AI.
        AbilityDefinition utility(string key, decimal energy, decimal cooldown,
            string auraKey, decimal seconds, bool self = false)
        {
            var skill = spell(key, key, energy, new AbilityEffectDefinition {
                Key = key + "-effect", EffectType = AbilityEffectTypes.ApplyAura,
                TargetType = self ? AbilityTargetTypes.Self : AbilityTargetTypes.Enemy,
                AuraKey = auraKey, DurationSeconds = seconds,
                ResolutionType = CombatResolutionTypes.AlwaysHits, CanMiss = false, CanCrit = false
            });
            skill.CooldownSeconds = cooldown;
            return skill;
        }
        outList.Add(utility("kick", 25m, 10m, "rogue-interrupt-attempt", 1m));
        if(!useClassWideTalents)outList.Add(utility("gouge",45m,10m,"rogue-gouge-control",4m));
        else if(!behindTarget)
        {
            // Current client spell 11286, rank 5: 75 damage; target faces the Rogue.
            var gouge=utility("gouge",45m,10m,"rogue-gouge-control",4m+.5m*talents.Rank("improved-gouge"));
            gouge.Effects[0].DependsOnEffectKey="gouge-hit";
            gouge.Effects[0].DependencyCondition=EffectDependencyConditions.Landed;
            gouge.Effects.Insert(0,hit("gouge-hit",75m));
            outList.Add(gouge);
        }
        outList.Add(utility("blind", useClassWideTalents ? 30m * (1m - .25m * talents.Rank("dirty-tricks")) : Math.Max(0m, 30m - talents.Rank("dirty-tricks") * 5m),
            180m - talents.Rank("elusiveness") * 45m, "rogue-blind-control", 10m));
        outList.Add(utility("feint", 20m, 10m, "rogue-feint-threat", 1m, self: true));
        outList.Add(utility("evasion", 0m,
            300m * (1m - talents.Rank("endurance") * (useClassWideTalents ? .30m : .10m)),
            "rogue-evasion", 15m, self: true));
        outList.Add(utility("sprint", 0m,
            300m * (1m - talents.Rank("endurance") * (useClassWideTalents ? .30m : .10m)),
            "rogue-sprint", 15m, self: true));
        outList.Add(utility("distract", 0m, 30m, "rogue-distract-detection", 10m));
        if (talents.Rank("preparation") > 0)
        {
            var prep = utility("preparation", 0m, 600m,
                "rogue-preparation-activated", 1m, self: true);
            prep.IsOffGlobalCooldown = true;
            outList.Add(prep);
        }
        var sap = utility("sap", useClassWideTalents ? 65m * (1m - .25m * talents.Rank("dirty-tricks")) : Math.Max(0m, 65m - talents.Rank("dirty-tricks") * 10m),
            0m, "rogue-sap-control", 25m);
        sap.RequiredSourceAuraKeys.Add("rogue-stealth");
        outList.Add(sap);
        if (useClassWideTalents)
        {
            // Current Shank: off-hand, cannot Miss (dodge/parry remain separate), rank 2 at 44.
            var shank=spell("shank","Shank",5m,hit("shank-hit",.5m*oh,WeaponHandKeys.OffHand));
            shank.Effects[0].CanMiss=false;
            shank.CooldownSeconds=10m-.4m*talents.Rank("flash-of-steel");
            outList.Add(shank);
            if(talents.Rank("death-mark")>0)
                outList.Add(utility("death-mark",25m,10m,"rogue-death-mark",6m));
            // Base values contain no AP in this mode. Every weapon effect consumes live
            // AP exactly once, including temporary gear/modifier changes and expiration.
            foreach(var ability in outList)
            foreach(var effect in ability.Effects.Where(e=>e.EffectType==AbilityEffectTypes.DirectDamage))
            {
                var off=effect.WeaponHandKey==WeaponHandKeys.OffHand;
                var coefficient=ability.Key switch {
                    "sinister-strike"=>1m+aggression,
                    "backstab"=>1.5m*(1m+opportunity),
                    "ambush"=>2.5m*(1m+opportunity),
                    "mutilate"=>.9m*(1m+opportunity)*(off?offMultiplier:1m),
                    "ghostly-strike"=>mainHandIsDagger?1.8m:1.25m,
                    "hemorrhage"=>mainHandIsDagger?1.45m:1m,
                    "riposte"=>1.5m,
                    "shank"=>.5m*offMultiplier,
                    _=>0m
                };
                if(coefficient>0m)
                {
                    effect.ScalingStatKey="attack-power";
                    effect.ScalingCoefficient=coefficient*(off?t.OffHandSpeedSeconds:t.MainHandSpeedSeconds)/14m;
                }
            }
            if(outList.FirstOrDefault(a=>a.Key=="eviscerate") is {} evis)
                evis.ResourceCosts[0].Amount=talents.Rank("flawless-execution")>0?20m:35m;
        }
        return outList;
    }
}
