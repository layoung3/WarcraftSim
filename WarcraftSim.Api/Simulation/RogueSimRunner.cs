using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Building;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Combat;
using WarcraftSim.Data.Forever.Rogue;
using WarcraftSim.Data.Forever.Simulation;

namespace WarcraftSim.Api.Simulation;

/// <summary>CP142 first real Rogue execution path using Core ability/auto-attack/
/// resource, aura, reporting and seeded combat-roll pipelines.</summary>
public sealed class RogueSimRunner
{
    public RogueSimResponse Run(RogueSimRequest request, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Character is null || request.Talents is null || request.Tuning is null ||
            request.DurationSeconds is <= 0m or > 1200m ||
            request.TargetMaximumHealth <= 0m || request.RogueMaximumHealth <= 0m ||
            request.StartingEnergyPercent is < 0m or > 100m || request.TargetArmor < 0m || request.AdditionalTargets is < 0 or > 8 ||
            request.BossSwingSeconds <= 0m || request.BossHitDamage < 0m ||
            request.TargetDodgePercent is < 0m or > 100m ||
            request.TargetParryPercent is < 0m or > 100m ||
            !ValidWeapon(request.MainHandWeaponType) ||
            !ValidWeapon(request.OffHandWeaponType) || request.Character.Level != 60 ||
            !string.Equals(request.Character.ClassKey, "rogue", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(request.Character.SpecializationKey, request.Specialization, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Invalid level-60 Rogue simulation request.");
        if (request.PoisonChargesPerWeapon < 0 ||
            !RoguePoisonProcessor.Supported.Contains(request.MainHandPoison) ||
            !RoguePoisonProcessor.Supported.Contains(request.OffHandPoison))
            throw new ArgumentException("Unsupported Rogue poison configuration.");
        // CP142 compatibility: disabling poisons disables both weapon coatings.
        if (!request.UseInstantPoison)
        {
            request.MainHandPoison = "none";
            request.OffHandPoison = "none";
        }
        request.Talents.Validate(60, request.Specialization);
        request.Tuning.Validate();
        var stats = ForeverCharacterSimulationRuntimeCalculatorFactory
            .CreateStandard(_ => request.RogueMaximumHealth)
            .Calculate(request.Character).EffectiveStats;
        var ap = Math.Max(0m, stats.Get("attack-power"));
        var energyMax = 100m + request.Talents.Rank("vigor") * 5m;
        var context = new SimulationContext(new SimulationRunOptions
        {
            Seed = request.Seed, DurationSeconds = request.DurationSeconds,
            PrimaryActorKey = "rogue", CaptureTimeline = request.CaptureTimeline,
            MaximumProcessedEvents = 100000, CancellationToken = token
        });
        var rogue = new SimulationActorState
        {
            Key = "rogue", Name = request.Character.Name, TeamKey = "raid", Level = 60,
            MaximumHealth = request.RogueMaximumHealth, Stats = stats
        };
        rogue.InitializeHealth(rogue.MaximumHealth);
        rogue.AddResource(new ResourceState("energy", energyMax,
            energyMax * request.StartingEnergyPercent / 100m, request.Tuning.EnergyPerSecond));
        var boss = new SimulationActorState
        {
            Key = "target", Name = "Level 63 Rogue training boss", TeamKey = "enemy",
            Level = 63, MaximumHealth = request.TargetMaximumHealth
        };
        boss.InitializeHealth(boss.MaximumHealth);
        context.AddActor(rogue); context.AddActor(boss);
        for (var i = 1; i <= request.AdditionalTargets; i++)
        {
            var add = new SimulationActorState { Key = $"target-{i}", Name = $"Additional target {i}",
                TeamKey = "enemy", Level = 63, MaximumHealth = request.TargetMaximumHealth };
            add.InitializeHealth(add.MaximumHealth);
            context.AddActor(add);
        }
        foreach (var ability in ForeverRogueSpellbook.Create(request.Specialization,
            request.Talents, request.Tuning, ap, request.BothWeaponsDaggers,
            request.IsBehindTarget)) rogue.AddAbility(ability);
        var rotation = BuildRotation(request);
        var aura = new AuraManager();
        RogueCombatProcessor? combat = null;
        var roll = new RogueRollResolver(request, () => combat);
        var executor = new AbilityExecutor(aura, roll,
            new RogueArmorMitigation(request, () => combat));
        var auto = new AutoAttackProcessor(executor, new RogueAutoAttackTimingProvider(request.Tuning));
        var weapon = CreateWeapons(request.Tuning, ap, request.Talents);
        combat = new RogueCombatProcessor(request, rogue, rotation, executor, aura, auto, weapon);
        var poisons = new RoguePoisonProcessor(request, rogue, aura, executor);
        var threatDefense = new RogueThreatAndDefenseProcessor(rogue.Key, request, aura);
        var bossAttacks = new ScriptedEncounterDamageProcessor(new RogueNoMitigation(),
            preserveHealth: actor => request.EnableBossAttacks && actor.Key == rogue.Key,
            modifyRawDamage: threatDefense.ResolveIncomingPhysicalDamage);
        var priority = new PriorityRotationExecutor(rotation, "rogue", "target", executor,
            reactToTargetStateChanges: true);
        var engine = new SimulationEngine([executor, aura, auto, bossAttacks,
            combat, poisons, threatDefense, priority]);
        var run = engine.Run(context, started =>
        {
            if (request.EnableBossAttacks)
                for (var at = request.BossSwingSeconds; at < request.DurationSeconds;
                     at += request.BossSwingSeconds)
                    started.ScheduleEvent(new CombatEvent {
                        Type = CombatEventType.ScriptedDamage, TimeSeconds = at,
                        SourceActorKey = boss.Key, TargetActorKey = rogue.Key,
                        EncounterEventKey = "rogue-boss-swing", SchoolKey = "physical",
                        MitigationType = DamageMitigationTypes.None,
                        RawAmount = request.BossHitDamage
                    });
            if (!request.StartInStealth &&
                (!auto.Start(started, "rogue", "target", weapon[0]) ||
                 !auto.Start(started, "rogue", "target", weapon[1])))
                throw new InvalidOperationException("Rogue weapon attacks failed to start.");
        });
        var report = run.Summary.ActorSummaries["rogue"];
        run.CustomMetrics["rogue-expose-armor-points"] = combat.ExposeArmorPoints;
        run.CustomMetrics["rogue-stealth-openers"] = combat.StealthOpeners;
        run.CustomMetrics["rogue-finisher-count"] = combat.FinishersUsed;
        run.CustomMetrics["rogue-poison-procs"] = poisons.Applications;
        run.CustomMetrics["rogue-deadly-poison-ticks"] = poisons.DeadlyTicks;
        run.CustomMetrics["rogue-main-hand-poison-charges"] = poisons.ChargesRemaining(WeaponHandKeys.MainHand);
        run.CustomMetrics["rogue-off-hand-poison-charges"] = poisons.ChargesRemaining(WeaponHandKeys.OffHand);
        run.CustomMetrics["rogue-preparation-cooldowns-reset"] = combat.PreparationResets;
        run.CustomMetrics["rogue-hack-and-slash-attacks"] = combat.HackAndSlashAttacks;
        run.CustomMetrics["rogue-quick-recovery-energy"] = combat.QuickRecoveryEnergyRestored;
        run.CustomMetrics["rogue-ending-combo-points"] = combat.ComboPointsFor("target");
        run.CustomMetrics["rogue-boss-threat"] = boss.ThreatTable.GetThreat(rogue.Key);
        run.CustomMetrics["rogue-feint-threat-reduced"] = threatDefense.ThreatReducedByFeint;
        run.CustomMetrics["rogue-boss-attacks-dodged"] = threatDefense.DodgedIncomingSwings;
        run.CustomMetrics["rogue-boss-attacks-parried"] = threatDefense.ParriedIncomingSwings;
        return new RogueSimResponse
        {
            Specialization = request.Specialization,
            DamageDone = report.DamageDone, Dps = report.DamageDone / request.DurationSeconds,
            EndingEnergy = rogue.Resources["energy"].Current, MaximumEnergy = energyMax,
            EndingComboPoints = combat.ComboPointsFor("target"), PoisonProcs = poisons.Applications,
            FinishersUsed = combat.FinishersUsed, StealthOpeners = combat.StealthOpeners,
            DamageByAbility = new Dictionary<string, decimal>(report.DamageDoneByAbility,
                StringComparer.OrdinalIgnoreCase), Run = run,
            ModelingNotes =
            [
                "CP142: Rogue has functional energy, target-owned combo points, dual auto-attacks, generator and finisher damage, Slice and Dice, Rupture, Venom, and Instant Poison triggers.",
                "Assassination, Combat and Subtlety are PARTIALLY implemented. Unsupported active and passive talents are not silently treated as complete.",
                "Level-60 weapon damage, hit, cooldowns, poison, resource and ability coefficients are provisional tuning values, not verified endgame client data.",
                "Mutilate requires two daggers; Hemorrhage and Subtlety use simplified positional and execute behavior. PvP control, target-switching and most utilities remain unmodeled.",
                "CP143: stealth, vanish and opener restrictions use source auras. Vanish is a modeled 300s cooldown; positional checks and PvP detection are deferred.",
                "CP143: optional second target Blade Flurry damage and additional finishers; remaining utility spells and exact armor debuff magnitudes are provisional.",
                "CP144: configurable target armor and Expose Armor now reduce physical non-bleed damage using a provisional armor formula. Default target armor is zero to retain existing comparison fixtures.",
                "CP144: weapon-specific poison charges and Deadly stacking ticks are executable; healing-reduction / movement and cast-speed utility debuffs remain state-only.",
                "Per-hit dodge/parry/glancing and full raid debuffs remain future mechanics.",
                "CP146: Removed the obsolete Deadliness talent per Blizzard's Forever Rogue deep dive; retained supported talent calculations with provisional coefficients.",
                "CP146: Feint now reduces actual shared enemy threat and Evasion modifies scripted incoming physical attack avoidance; all exact threat/dodge values are provisional.",
                "Kick, Blind, Sprint and Sap still require enemy cast, CC, or positioning models for full functionality.",
                "Default rotation is only for exercising abilities, not a finalized optimal damage rotation."
            ]
        };
    }

    private static bool ValidWeapon(string type) =>
        type is "dagger" or "sword" or "axe" or "mace" or "fist" or "other";

    private static RotationProfile BuildRotation(RogueSimRequest r)
    {
        var keys = r.Specialization switch
        {
            "assassination" => new[] { "premeditation", "ambush", "garrote", "vanish", "cold-blood", "expose-armor", "slice-and-dice", "venom", "rupture", "eviscerate", "mutilate", "sinister-strike" },
            "combat" => new[] { "ambush", "garrote", "vanish", "adrenaline-rush", "blade-flurry", "riposte", "expose-armor", "slice-and-dice", "rupture", "eviscerate", "sinister-strike" },
            "subtlety" => new[] { "premeditation", "ambush", "garrote", "vanish", "expose-armor", "slice-and-dice", "rupture", "eviscerate", "ghostly-strike", "hemorrhage", "backstab", "sinister-strike" },
            _ => throw new ArgumentException("Unknown Rogue specialization.")
        };
        var profile = new RotationProfile
        {
            Name = $"CP142 Rogue {r.Specialization}", ClassKey = "rogue",
            RulesetKey = "wow-forever", SimulationType = SimulationType.Dps
        };
        var rank = 0;
        if (r.UseUtilityAbilities)
        {
            foreach (var utility in new[] { "evasion", "feint", "kick", "blind" })
                profile.Entries.Add(new RotationEntry { Priority = rank++, AbilityKey = utility });
            if (r.Talents.Rank("preparation") > 0)
                profile.Entries.Add(new RotationEntry { Priority = rank++, AbilityKey = "preparation" });
        }
        // The priority executor casts any affordable lower-priority action while
        // waiting for Energy. Assassination must pool 60 Energy for Mutilate;
        // keeping the cheaper 45-Energy Sinister Strike fallback permanently
        // prevents Mutilate from being affordable in its default rotation.
        // Retain Sinister Strike for builds without the talent or two daggers.
        var hasUsableMutilate = r.Specialization == "assassination" &&
            r.Talents.Rank("mutilate") > 0 && r.BothWeaponsDaggers;
        foreach (var key in keys)
        {
            if (key == "expose-armor" && !r.UseExposeArmor)
                continue;
            if (key == "sinister-strike" && hasUsableMutilate)
                continue;
            if (key is ("sinister-strike" or "eviscerate" or "rupture" or "slice-and-dice" or "expose-armor") ||
                key == "backstab" && r.BothWeaponsDaggers && r.IsBehindTarget ||
                key == "mutilate" && r.Talents.Rank("mutilate") > 0 && r.BothWeaponsDaggers ||
                key == "venom" && r.Talents.Rank("venom") > 0 ||
                key == "hemorrhage" && r.Talents.Rank("hemorrhage") > 0 ||
                key == "adrenaline-rush" && r.Talents.Rank("adrenaline-rush") > 0 ||
                key == "blade-flurry" && r.Talents.Rank("blade-flurry") > 0 ||
                key == "ambush" && r.BothWeaponsDaggers && r.IsBehindTarget ||
                key == "garrote" && (r.IsBehindTarget || r.Talents.Rank("dirty-deeds") > 0) || key == "vanish" ||
                key == "premeditation" && r.Talents.Rank("premeditation") > 0 ||
                key == "cold-blood" && r.Talents.Rank("cold-blood") > 0 ||
                key == "ghostly-strike" && r.Talents.Rank("ghostly-strike") > 0 ||
                key == "riposte" && r.Talents.Rank("riposte") > 0)
                profile.Entries.Add(new RotationEntry { Priority = rank++, AbilityKey = key });
        }
        return profile;
    }

    private static AutoAttackDefinition[] CreateWeapons(ForeverRogueTuning t, decimal ap,
        ForeverRogueTalentBuild talents)
    {
        AutoAttackDefinition weapon(string key, string hand, decimal speed, decimal baseDamage, decimal multiplier) => new()
        {
            Key = key, Name = key, WeaponHandKey = hand, SwingIntervalSeconds = speed,
            DamageEffect = new AbilityEffectDefinition
            {
                Key = "weapon-hit", EffectType = AbilityEffectTypes.DirectDamage,
                SchoolKey = "physical", TargetType = AbilityTargetTypes.Enemy,
                WeaponHandKey = hand, ResolutionType = CombatResolutionTypes.Melee,
                CanMiss = true, CanCrit = true,
                MinimumValue = (baseDamage + ap * speed / 14m) * multiplier,
                MaximumValue = (baseDamage + ap * speed / 14m) * multiplier
            }
        };
        return
        [
            weapon("rogue-main-hand", WeaponHandKeys.MainHand, t.MainHandSpeedSeconds, t.MainHandWeaponDamage, 1m),
            weapon("rogue-off-hand", WeaponHandKeys.OffHand, t.OffHandSpeedSeconds, t.OffHandWeaponDamage,
                .5m + talents.Rank("dual-wield-specialization") * .05m)
        ];
    }

    private sealed class RogueAutoAttackTimingProvider(ForeverRogueTuning t) : IAutoAttackTimingProvider
    {
        public AutoAttackTimingSnapshot Resolve(SimulationContext context, SimulationActorState actor,
            AutoAttackDefinition def)
        {
            var haste = Math.Max(0m, actor.Stats.Get("haste-percent"));
            if (actor.ActiveAuras.Any(a => a.Definition.Key == "slice-and-dice" &&
                a.IsActiveAt(context.CurrentTimeSeconds))) haste += t.SliceAndDiceHastePercent;
            if (actor.ActiveAuras.Any(a => a.Definition.Key == "blade-flurry" &&
                a.IsActiveAt(context.CurrentTimeSeconds))) haste += 20m;
            return new AutoAttackTimingSnapshot { SwingIntervalSeconds =
                def.SwingIntervalSeconds / (1m + haste / 100m) };
        }
    }

    private sealed class RogueRollResolver(RogueSimRequest r, Func<RogueCombatProcessor?> combat) : ICombatRollResolver
    {
        public CombatRollResult Resolve(SimulationContext ctx, SimulationActorState src,
            SimulationActorState dest, AbilityDefinition ability, AbilityEffectDefinition effect) =>
            Resolve(ctx, src, dest, ability, effect, CombatEffectDeliveryType.Direct);
        public CombatRollResult Resolve(SimulationContext ctx, SimulationActorState src,
            SimulationActorState dest, AbilityDefinition ability, AbilityEffectDefinition effect,
            CombatEffectDeliveryType delivery)
        {
            if (effect.ResolutionType == CombatResolutionTypes.AlwaysHits) return CombatRollResult.Hit();
            var hit = effect.WeaponHandKey == WeaponHandKeys.OffHand ? r.Tuning.OffHandHitPercent :
                effect.ResolutionType == CombatResolutionTypes.Spell ? r.Tuning.MainHandHitPercent :
                    r.Tuning.MainHandHitPercent;
            hit = Math.Clamp(hit + r.Talents.Rank("precision") + src.Stats.Get("hit-chance-percent"), 0m, 100m);
            if (effect.CanMiss && (decimal)ctx.Random.NextDouble() * 100m >= hit)
                return CombatRollResult.Miss();
            // The encounter's frontal dodge/parry are explicit inputs, not
            // hidden in the hit percentage. Weapon Expertise reduces them.
            if (delivery != CombatEffectDeliveryType.Periodic &&
                effect.ResolutionType == CombatResolutionTypes.Melee &&
                dest.TeamKey != src.TeamKey)
            {
                var expertise = r.Talents.Rank("weapon-expertise") *
                    r.Tuning.WeaponExpertiseAvoidanceReductionPerRank;
                var dodge = Math.Max(0m, r.TargetDodgePercent - expertise);
                var parry = r.IsBehindTarget ? 0m :
                    Math.Max(0m, r.TargetParryPercent - expertise);
                if ((decimal)ctx.Random.NextDouble() * 100m < dodge + parry)
                    return CombatRollResult.Miss(); // avoid result pending core combat tables
            }
            // Consume after the first successfully landed special hit, even in
            // a multi-hit Mutilate. October 16 beta specifically corrected this.
            var coldBlood = combat()?.ConsumeColdBlood(ctx, ability.Key) ?? false;
            var crit = effect.CanCrit && (coldBlood || (decimal)ctx.Random.NextDouble() * 100m <
                Math.Clamp(r.Tuning.PhysicalCritPercent + r.Talents.Rank("malice") +
                    src.Stats.Get("attack-critical-chance-percent") + effect.BonusCriticalChancePercent +
                    (effect.WeaponHandKey == WeaponHandKeys.OffHand ?
                        (r.OffHandWeaponType == "dagger" ? r.Tuning.HackAndSlashDaggerCritPercentPerRank :
                         r.OffHandWeaponType == "fist" ? r.Tuning.HackAndSlashFistCritPercentPerRank : 0m) :
                        (r.MainHandWeaponType == "dagger" ? r.Tuning.HackAndSlashDaggerCritPercentPerRank :
                         r.MainHandWeaponType == "fist" ? r.Tuning.HackAndSlashFistCritPercentPerRank : 0m)) *
                    (effect.ResolutionType == CombatResolutionTypes.Melee ?
                        r.Talents.Rank("hack-and-slash") : 0), 0m, 100m));
            var bonus = combat()?.DamageMultiplier(ability.Key, delivery, ctx, dest) ?? 1m;
            var lethality = crit && ability.Key is
                ("sinister-strike" or "backstab" or "ambush" or "mutilate" or
                 "hemorrhage" or "ghostly-strike")
                ? r.Talents.Rank("lethality") *
                    r.Tuning.LethalityBonusCriticalDamagePerRank : 0m;
            return new CombatRollResult { Landed = true, IsCritical = crit,
                ResultKey = crit ? CombatResultTypes.Critical : CombatResultTypes.Hit,
                AmountMultiplier = bonus * (crit ? 2m + lethality : 1m) };
        }
    }
    private sealed class RogueNoMitigation : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext context, SimulationActorState source,
            SimulationActorState target, AbilityDefinition ability,
            AbilityEffectDefinition effect, decimal rawAmount) =>
            DamageMitigationResult.Unmitigated(rawAmount);
    }
    private sealed class RogueArmorMitigation(RogueSimRequest request,
        Func<RogueCombatProcessor?> mechanics) : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext context, SimulationActorState source,
            SimulationActorState target, AbilityDefinition ability, AbilityEffectDefinition effect,
            decimal rawAmount)
        {
            if (request.TargetArmor <= 0m || effect.SchoolKey != "physical" ||
                effect.EffectType == AbilityEffectTypes.PeriodicDamage ||
                ability.Key is "garrote" or "rupture")
                return DamageMitigationResult.Unmitigated(rawAmount);
            var exposed = target.ActiveAuras.Any(a => a.Definition.Key == "expose-armor" &&
                a.SourceActorKey == "rogue" && a.IsActiveAt(context.CurrentTimeSeconds));
            var armor = Math.Max(0m, request.TargetArmor - (exposed ?
                (mechanics()?.ExposeArmorPoints ?? 0) * request.Tuning.ArmorReductionPerExposeComboPoint : 0m));
            var handType = effect.WeaponHandKey == WeaponHandKeys.OffHand
                ? request.OffHandWeaponType : request.MainHandWeaponType;
            if (handType == "mace")
                armor *= 1m - Math.Min(100m, request.Talents.Rank("hack-and-slash") *
                    request.Tuning.HackAndSlashMaceArmorIgnorePercentPerRank) / 100m;
            var pct = ForeverArmorMitigationMath.ClampArmorReductionFraction(
                armor / (armor + request.Tuning.ArmorDamageReductionConstant));
            var mitigated = Math.Max(0m,rawAmount) * pct;
            return new DamageMitigationResult {
                RawAmount = Math.Max(0m,rawAmount), FinalAmount = Math.Max(0m,rawAmount) - mitigated,
                MitigatedAmount = mitigated, ReductionPercent = pct * 100m,
                DefenseValue = armor
            };
        }
    }
}
