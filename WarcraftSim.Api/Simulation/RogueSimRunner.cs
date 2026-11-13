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
    // Both consumers own a fresh snapshot and use the exact same preparation.
    // No combat context, processor or scheduled event is needed for the preview.
    private static RoguePrecombatPreparation Prepare(RogueSimRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request=RogueSimulationRequestSnapshot.Create(request);
        if (request.Character is null || request.Talents is null || request.Tuning is null || request.Modifiers is null || request.CombatItems is null ||
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
        if(request.UseClassWideTalents)
        {
            ForeverRogueClassTalents.Validate(request.Talents);
            request.Talents.Ranks=new(request.Talents.Ranks,StringComparer.OrdinalIgnoreCase);
        }
        else request.Talents.Validate(60, request.Specialization);
        request.CombatItems.Validate("rogue");
        request.Tuning.Validate();
        var baseline=ForeverLevel60Baseline.Resolve(request.Level60Baseline,
            request.UseReviewedLevel60Baseline,request.Character.Race??"","rogue",
            request.UseProvisionalLevel60Baseline);
        IReadOnlyList<string> inputNotes=[];
        if(request.UseClassWideTalents)
        {
            var shared=ForeverSharedCharacterInputs.Prepare(request.Character,baseline,request.Modifiers,request.DurationSeconds,supportedTargetDebuffs:["faerie-fire-rank4"]);
            request.Character=shared.Character;
            inputNotes=shared.Notes;
            foreach(var (hand,weapon) in shared.Weapons)
            {
                if(hand is not ("main-hand" or "off-hand"))continue;
                if(!ValidWeapon(weapon.Type) || weapon.Hand=="two-hand")
                    throw new ArgumentException($"The Rogue {hand} requires an eligible one-handed weapon.");
                if(hand=="main-hand")
                {
                    request.MainHandWeaponType=weapon.Type;
                    request.Tuning.MainHandWeaponDamage=(weapon.MinimumDamage+weapon.MaximumDamage)/2m;
                    request.Tuning.MainHandSpeedSeconds=weapon.SpeedSeconds;
                }
                else
                {
                    request.OffHandWeaponType=weapon.Type;
                    request.Tuning.OffHandWeaponDamage=(weapon.MinimumDamage+weapon.MaximumDamage)/2m;
                    request.Tuning.OffHandSpeedSeconds=weapon.SpeedSeconds;
                }
            }
            request.BothWeaponsDaggers=request.MainHandWeaponType=="dagger" && request.OffHandWeaponType=="dagger";
        }
        else if(baseline is not null)
        {
            if(request.Character.Equipment?.Items is null ||
                request.Character.Equipment.Items.Any(item => item is null))
                throw new ArgumentException("Prepared Rogue equipment must contain a non-null item list and entries.");
            if(request.Character.Equipment.Items.Any(item =>
                (item.ItemId.HasValue || item.EnchantSpellId.HasValue || item.TemporaryEnchantSpellId.HasValue)))
                throw new ArgumentException("Prepared Rogue catalog equipment and enchants are not enabled; use explicitly authored stat budgets.");
            request.Character=CasterSimulationRequestSnapshot.GenerateBaseline(request.Character,baseline);
        }
        var stats = ForeverCharacterSimulationRuntimeCalculatorFactory
            .CreateStandard(_ => request.RogueMaximumHealth)
            .Calculate(request.Character).EffectiveStats;
        if(request.UseClassWideTalents)
            stats.Set("attack-power",stats.Get("attack-power")+stats.Get("melee-attack-power"));
        var maximumHealth=request.RogueMaximumHealth;
        if(request.UseClassWideTalents && baseline is null)
            maximumHealth+=10m*(stats.Get("stamina")-request.Character.BaseStats.Get("stamina"))+stats.Get("health");
        if(baseline is not null)
        {
            maximumHealth=(baseline.ObservedUnbuffedMaximumHealth??maximumHealth)+
                10m*(stats.Get("stamina")-baseline.Stamina)+stats.Get("health");
            if(maximumHealth<=0m)
                throw new ArgumentException("Prepared Rogue maximum Health must be positive.");
        }
        if(maximumHealth<=0m)throw new ArgumentException("Prepared Rogue maximum Health must be positive.");
        var ap = Math.Max(0m, stats.Get("attack-power"));
        var energyMax = 100m + request.Talents.Rank("vigor") * 5m;
        var startingEnergy = energyMax * request.StartingEnergyPercent / 100m;
        var abilities = ForeverRogueSpellbook.Create(request.Specialization,
            request.Talents, request.Tuning, ap, request.BothWeaponsDaggers,
            request.IsBehindTarget, mainHandIsDagger: request.MainHandWeaponType == "dagger", useClassWideTalents:request.UseClassWideTalents);
        var rotation = request.UseClassWideTalents ? BuildClassWideRotation(request,abilities) : BuildRotation(request);
        var weapons = CreateWeaponDefinitions(request.Tuning, ap, request.Talents,request.UseClassWideTalents);
        return new(request, stats, ap, energyMax, startingEnergy, maximumHealth,
            baseline, abilities, rotation, weapons,inputNotes);
    }

    /// <summary>Exact display-only precombat inputs and configured priority, without executing combat.</summary>
    public static RoguePrecombatPreview Preview(RogueSimRequest request)
    {
        var prepared = Prepare(request);
        // Only the display copy projects resource caps and the clamped spellbook AP.
        // Keep the actor's contributed raw stats unchanged for combat processing.
        var stats = new StatCollection { Values = new(prepared.Stats.Values, prepared.Stats.Values.Comparer) };
        stats.Set("attack-power", prepared.AttackPower);
        stats.Set("energy", prepared.MaximumEnergy);
        stats.Set("health", prepared.MaximumHealth);
        return new(stats, prepared.AttackPower, prepared.MaximumEnergy, prepared.StartingEnergy,
            prepared.MaximumHealth,
            prepared.Baseline is null ? null : ForeverLevel60Baseline.Summary(prepared.Baseline),
            prepared.Baseline?.VerificationStatus ?? "explicit-user-profile-not-client-verified",
            [..(prepared.Request.UseClassWideTalents ? ClassWideModelingNotes(prepared) : ModelingNotes(prepared.Baseline)),
                "Display-only exact Rogue precombat snapshot from the execution preparation path. Attack Power is the nonnegative spellbook and auto-attack input. Energy maximum includes the existing Vigor calculation once; starting Energy applies the separate requested percentage. Health is the actual prepared actor cap.",
                "EffectiveStats contains contributed raw hit, crit and haste stats, not final combat roll chances. Manual tuning, Precision, Malice, hand-specific and ability-specific modifiers, clamping and temporary effects are applied separately during combat; no new combat formulas are implied by this display.",
                prepared.Request.UseClassWideTalents ? "Class-wide configured priority supports a validated custom order and live combat gates." :
                    "Read-only fixed configured priority from the execution BuildRotation path. The dynamic combat processor applies combo-point, aura and eligibility gates and changes some costs and durations later. Entries are not a list of immediately executable actions; auto attacks and poison procs are scheduled separately. No custom rotation is supported.",
                prepared.Request.UseClassWideTalents ? "Coverage and source confidence are separate from legal allocations. Pending effects are listed in the class-wide catalog." :
                    "Preview validates the existing legacy runner preparation, not strict talent allocation or a complete source-backed build. Prepared catalog equipment and enchants are not enabled; legacy authored budget handling is unchanged. User-supplied provenance is not server attestation. Historical proxy values are NOT verified for Forever; talent mechanics, weapon budgets and combat coefficients retain their existing provisional assumptions."],
            new(prepared.Rotation.Entries.AsReadOnly())) { ClassWide=prepared.Request.UseClassWideTalents };
    }

    private sealed record RoguePrecombatPreparation(RogueSimRequest Request, StatCollection Stats,
        decimal AttackPower, decimal MaximumEnergy, decimal StartingEnergy, decimal MaximumHealth,
        ForeverLevel60BaselineObservation? Baseline, IReadOnlyList<AbilityDefinition> Abilities,
        RotationProfile Rotation, AutoAttackDefinition[] Weapons,IReadOnlyList<string> InputNotes);

    private static List<string> ModelingNotes(ForeverLevel60BaselineObservation? baseline) =>
        [
            baseline is null ? "Legacy authored Rogue inputs; no naked baseline selected." :
                $"Prepared Rogue baseline: {baseline.VerificationStatus}; build {baseline.ClientBuild}; source {baseline.SourceReference}. " +
                (baseline.ObservedUnbuffedMaximumHealth.HasValue ? "Supplied naked Health plus authored contributions (subject to baseline provenance)." :
                "Health uses the manual scenario cap plus provisional post-baseline Stamina and flat Health deltas; not observed naked Health.") +
                (baseline.VerificationStatus=="provisional-documented-proxy" ?
                " Historical proxy values are NOT verified for Forever." : " User-supplied provenance is not server attestation.") +
                " Weapon tuning and combat coefficients remain provisional; catalog equipment is not enabled.",
            "CP142: Rogue has functional energy, target-owned combo points, dual auto-attacks, generator and finisher damage, Slice and Dice, Rupture, Venom, and Instant Poison triggers.",
            "Assassination, Combat and Subtlety are PARTIALLY implemented. Unsupported active and passive talents are not silently treated as complete.",
            "Level-60 weapon damage, hit, cooldowns, poison, resource and ability coefficients are provisional tuning values, not verified endgame client data.",
            "Mutilate requires two daggers; Hemorrhage and Subtlety use simplified positional and execute behavior. PvP control, target-switching and most utilities remain unmodeled.",
            "Mutilate's historical provisional default is 13 bonus damage per hand; explicit overrides including legacy 17.25 and zero are preserved. The 20% poisoned-target bonus recognizes live Deadly, Wound, Mind-numbing or Crippling auras once, with existing source-unfiltered target-state and impact ordering. Instant Poison has no lingering aura. This is not multiplayer or current-client certification.",
            "CP143: stealth, vanish and opener restrictions use source auras. Vanish has a provisional 300s base cooldown reduced by Elusiveness. Explicit behind-target eligibility is modeled; movement and PvP detection are deferred.",
            "CP143: optional second target Blade Flurry damage and additional finishers; remaining utility spells and exact armor debuff magnitudes are provisional.",
            "CP144: configurable target armor and Expose Armor now reduce physical non-bleed damage using a provisional armor formula. Default target armor is zero to retain existing comparison fixtures.",
            "Improved Expose Armor uses historical pinned rank 1/2 refunds of 1/2 Combo Points only after five points are spent. Missing/null ImprovedExposeArmorFivePointRefund selects that provisional source default; an explicit 0..5 remains an absolute override, including 5 in older exported requests. This is not current-client verification.",
            "CP144: weapon-specific poison charges and Deadly stacking ticks are executable. Wound and Mind-numbing apply provisional healing-received and cast-time multipliers; utility poison auras do not add damage to the stationary training target. Movement and PvP effects remain limited.",
            "Explicit target dodge/parry and Weapon Expertise are modeled with provisional avoidance assumptions; glancing and full raid debuffs remain future mechanics.",
            "CP146: Removed the obsolete Deadliness talent per Blizzard's Forever Rogue deep dive; retained supported talent calculations with provisional coefficients.",
            "CP146: Feint now reduces actual shared enemy threat and Evasion modifies scripted incoming physical attack avoidance; all exact threat/dodge values are provisional.",
            "Kick, Blind, Sprint and Sap still require enemy cast, CC, or positioning models for full functionality.",
            "Default rotation is only for exercising abilities, not a finalized optimal damage rotation."
        ];

    public RogueSimResponse Run(RogueSimRequest request, CancellationToken token = default)
    {
        var prepared = Prepare(request);
        request = prepared.Request;
        var stats = prepared.Stats;
        var maximumHealth = prepared.MaximumHealth;
        var energyMax = prepared.MaximumEnergy;
        var context = new SimulationContext(new SimulationRunOptions
        {
            Seed = request.Seed, DurationSeconds = request.DurationSeconds,
            PrimaryActorKey = "rogue", CaptureTimeline = request.CaptureTimeline,
            MaximumProcessedEvents = 100000, CancellationToken = token
        });
        var rogue = new SimulationActorState
        {
            Key = "rogue", Name = request.Character.Name, TeamKey = "raid", Level = 60,
            MaximumHealth = maximumHealth, Stats = stats
        };
        rogue.InitializeHealth(rogue.MaximumHealth);
        rogue.AddResource(new ResourceState("energy", energyMax,
            prepared.StartingEnergy, request.Tuning.EnergyPerSecond));
        var boss = new SimulationActorState
        {
            Key = "target", Name = "Level 63 Rogue training boss", TeamKey = "enemy",
            Level = 63, MaximumHealth = request.TargetMaximumHealth
        };
        boss.InitializeHealth(boss.MaximumHealth);
        boss.Stats.Set("armor",request.TargetArmor);
        context.AddActor(rogue); context.AddActor(boss);
        for (var i = 1; i <= request.AdditionalTargets; i++)
        {
            var add = new SimulationActorState { Key = $"target-{i}", Name = $"Additional target {i}",
                TeamKey = "enemy", Level = 63, MaximumHealth = request.TargetMaximumHealth };
            add.InitializeHealth(add.MaximumHealth);
            add.Stats.Set("armor",request.TargetArmor);
            context.AddActor(add);
        }
        foreach (var ability in prepared.Abilities) rogue.AddAbility(ability);
        var rotation = prepared.Rotation;
        var aura = new AuraManager();
        RogueCombatProcessor? combat = null;
        var roll = new RogueRollResolver(request, () => combat);
        var executor = new AbilityExecutor(aura, roll,
            new RogueArmorMitigation(request, () => combat), resourceCostPolicy: () => combat);
        var auto = new AutoAttackProcessor(executor, new RogueAutoAttackTimingProvider(request.Tuning));
        var weapon = prepared.Weapons;
        combat = new RogueCombatProcessor(request, rogue, rotation, executor, aura, auto, weapon);
        var poisons = new RoguePoisonProcessor(request, rogue, aura, executor);
        var threatDefense = new RogueThreatAndDefenseProcessor(rogue.Key, request, aura,
            (ctx,target)=>combat.ClassWide?.TrySetup(ctx,target));
        var externalDebuffs=request.UseClassWideTalents ? new ForeverExternalTargetDebuffProcessor(request.Modifiers,60,aura) : null;
        var items=request.UseClassWideTalents ? new CombatUseItemProcessor(request.CombatItems,executor,rogue,"target","rogue") : null;
        var equipment=request.UseClassWideTalents ? new ForeverEquipmentEffectProcessor(request.Character.Equipment,
            "rogue","target",aura,executor,request.Seed,auto,classKey:"rogue",onStatDelta:ApplyEquipmentDelta) : null;
        var bossAttacks = new ScriptedEncounterDamageProcessor(new RogueNoMitigation(),
            preserveHealth: actor => !request.UseClassWideTalents && request.EnableBossAttacks && actor.Key == rogue.Key,
            modifyRawDamage: threatDefense.ResolveIncomingPhysicalDamage);
        var priority = new PriorityRotationExecutor(rotation, "rogue", "target", executor,
            reactToTargetStateChanges: true);
        var processors=new List<ICombatEventProcessor> { executor,aura,auto,bossAttacks,combat,poisons,threatDefense };
        if(externalDebuffs is not null)processors.Add(externalDebuffs);
        if(equipment is not null)processors.Add(equipment);
        if(items is not null)processors.Add(items);
        processors.Add(priority);
        var engine = new SimulationEngine(processors);
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
                        RawAmount = request.BossHitDamage,
                        WeaponHandKey=request.BossAttacksUseWeapon ? WeaponHandKeys.MainHand : null
                    });
            if (!request.StartInStealth &&
                (!auto.Start(started, "rogue", "target", weapon[0]) ||
                 !auto.Start(started, "rogue", "target", weapon[1])))
                throw new InvalidOperationException("Rogue weapon attacks failed to start.");
        });
        equipment?.WriteMetrics(run);
        if(externalDebuffs is not null)run.CustomMetrics["rogue-external-debuff-applications"]=externalDebuffs.Applications;
        combat.ClassWide?.WriteMetrics(run);
        if(items is not null)
            foreach(var (key,uses) in items.Uses)run.CustomMetrics["combat-item-uses-"+key]=uses;
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
        if(request.UseClassWideTalents)
        {
            run.CustomMetrics["rogue-controlled-incoming-attacks"]=threatDefense.PreventedByControl;
            run.CustomMetrics["rogue-heightened-senses-avoided"]=threatDefense.HeightenedSensesAvoided;
        }
        return new RogueSimResponse
        {
            Specialization = request.Specialization,
            DamageDone = report.DamageDone, Dps = report.DamageDone / request.DurationSeconds,
            EndingEnergy = rogue.Resources["energy"].Current, MaximumEnergy = energyMax,
            MaximumHealth=rogue.MaximumHealth, EndingHealth=rogue.CurrentHealth,
            EndingComboPoints = combat.ComboPointsFor("target"), PoisonProcs = poisons.Applications,
            FinishersUsed = combat.FinishersUsed, StealthOpeners = combat.StealthOpeners,
            DamageByAbility = new Dictionary<string, decimal>(report.DamageDoneByAbility,
                StringComparer.OrdinalIgnoreCase), Run = run,
            ModelingNotes = request.UseClassWideTalents ? ClassWideModelingNotes(prepared) : ModelingNotes(prepared.Baseline)
        };
    }

    private static void ApplyEquipmentDelta(SimulationContext context,SimulationActorState actor,IReadOnlyDictionary<string,decimal> delta)
    {
        actor.Stats.Set("attack-power",actor.Stats.Get("attack-power")+delta.GetValueOrDefault("melee-attack-power"));
        ForeverEquipmentDynamicResourceCaps.Apply(context,actor,delta);
    }

    private static List<string> ClassWideModelingNotes(RoguePrecombatPreparation prepared) =>
        [..prepared.InputNotes,
            $"Current class-wide Rogue allocation: {ForeverRogueClassTalents.SourceBuild}, reviewed {ForeverRogueClassTalents.ReviewedDate}; {ForeverRogueClassTalents.TreeUrl}. All 54 nodes have sourced caps, tiers and parent ranks. Preset origin does not select mechanics or policy.",
            "Shared raw character, gear, enchants and static modifiers enter the existing calculator once. Attack Power combines general and melee Attack Power once; weapon abilities/autos and current poison coefficients consume live AP. Catalog weapon identities replace manual weapon budgets only for selected catalog weapon slots.",
            "Poised/Composure, Shank/Flash of Steel/Boot Knife/Venom, current rank tables, cross-tree cooldowns, Ghostly Strike opener/dodge, Blood Splatter, Relentless Strikes and Death Mark execute through the existing Rogue event engine. Automatic priority derives from learned actions and actual hand types; custom order is validated then uses live resource/aura/CP gates.",
            "Death Mark uses the level-60 client rank 4 tooltip: 12% stored per combo point, double for Garrote/Rupture, six-second release; cap (156+245*CP+.04*AP*CP)*(1+crit/100). AP/crit are snapshotted on application; Coup de Grace modifies storage and payout. These composition/snapshot conventions are provisional pending server logs. " + ForeverRogueClassTalents.DeathMarkSourceUrl,
            "Mutilate uses current rank 3: 90% weapon damage plus 79 per hand unless an explicit class-wide override is supplied. Off-hand penalty composition, remaining baseline damage tuning, armor and combat-table assumptions are provisional. " + ForeverRogueClassTalents.MutilateSourceUrl,
            "External Faerie Fire modifies live primary-target armor with its configured expiry/maintenance. Additional Blade Flurry targets retain their own armor and do not inherit that primary-target debuff.",
            "Control witnesses are synthetic susceptible-target scenarios, not certification of raid boss immunities. Non-trivial Remorseless targets are explicitly modeled as enemies within five levels. Full tank/healer workflows remain separate.",
            ..ForeverRogueClassTalents.CoverageLimitations.Select(x=>$"Talent coverage {x.Key}: {x.Value}")];

    private static RotationProfile BuildClassWideRotation(RogueSimRequest r,IReadOnlyList<AbilityDefinition> abilities)
    {
        var available=abilities.Select(x=>x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        IEnumerable<string> keys;
        if(r.CustomPriority is not null)
        {
            if(r.CustomPriority.Count>64 || r.CustomPriority.Any(k=>string.IsNullOrWhiteSpace(k) || !available.Contains(k)) ||
                r.CustomPriority.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=r.CustomPriority.Count)
                throw new ArgumentException("Custom Rogue priority must contain at most 64 distinct learned, weapon-eligible actions.");
            keys=r.CustomPriority.Select(x=>x.ToLowerInvariant());
        }
        else
        {
            var generator=available.Contains("mutilate")?"mutilate":available.Contains("hemorrhage")?"hemorrhage":
                available.Contains("backstab")?"backstab":"sinister-strike";
            var autoKeys=new List<string>();
            if(r.UseUtilityAbilities)autoKeys.AddRange(["evasion","feint","kick","preparation"]);
            autoKeys.AddRange(["premeditation","ambush","garrote","vanish","cold-blood","adrenaline-rush",
                "blade-flurry","riposte","shank"]);
            if(r.UseExposeArmor)autoKeys.Add("expose-armor");
            autoKeys.AddRange(["slice-and-dice","death-mark","rupture","eviscerate","ghostly-strike",generator]);
            keys=autoKeys.Where(available.Contains);
        }
        var result=new RotationProfile { Name="Rogue class-wide priority",ClassKey="rogue",RulesetKey="wow-forever",SimulationType=SimulationType.Dps };
        foreach(var key in keys)result.Entries.Add(new RotationEntry { Priority=result.Entries.Count,AbilityKey=key });
        return result;
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

    // Retain the legacy three-argument helper used by existing regression harnesses.
    private static AutoAttackDefinition[] CreateWeapons(ForeverRogueTuning t,decimal ap,ForeverRogueTalentBuild talents) =>
        CreateWeaponDefinitions(t,ap,talents,false);

    private static AutoAttackDefinition[] CreateWeaponDefinitions(ForeverRogueTuning t, decimal ap,
        ForeverRogueTalentBuild talents,bool classWide)
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
                MinimumValue = (baseDamage + (classWide?0m:ap*speed/14m)) * multiplier,
                MaximumValue = (baseDamage + (classWide?0m:ap*speed/14m)) * multiplier,
                ScalingStatKey=classWide?"attack-power":null,
                ScalingCoefficient=classWide?speed/14m*multiplier:0m
            }
        };
        return
        [
            weapon("rogue-main-hand", WeaponHandKeys.MainHand, t.MainHandSpeedSeconds, t.MainHandWeaponDamage, 1m),
            weapon("rogue-off-hand", WeaponHandKeys.OffHand, t.OffHandSpeedSeconds, t.OffHandWeaponDamage,
                .5m * (classWide ? 1m+.25m*talents.Rank("dual-wield-specialization") : 1m+.1m*talents.Rank("dual-wield-specialization")))
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
                var avoidanceRoll = (decimal)ctx.Random.NextDouble() * 100m;
                if (avoidanceRoll < dodge)
                    return CombatRollResult.Dodge();
                if (avoidanceRoll < dodge + parry)
                    return CombatRollResult.Parry();
            }
            // Consume after the first successfully landed special hit, even in
            // a multi-hit Mutilate. October 16 beta specifically corrected this.
            var coldBlood = combat()?.ConsumeColdBlood(ctx, ability.Key) ?? false;
            var remorseless=r.UseClassWideTalents && effect.CanCrit ? combat()?.ClassWide?.ConsumeRemorseless(ctx,ability.Key)??0m : 0m;
            var crit = effect.CanCrit && (coldBlood || (decimal)ctx.Random.NextDouble() * 100m <
                Math.Clamp(r.Tuning.PhysicalCritPercent + r.Talents.Rank("malice") +
                    src.Stats.Get("attack-critical-chance-percent") + effect.BonusCriticalChancePercent + remorseless +
                    (effect.WeaponHandKey == WeaponHandKeys.OffHand ?
                        (r.OffHandWeaponType == "dagger" ? r.Tuning.HackAndSlashDaggerCritPercentPerRank :
                         r.OffHandWeaponType == "fist" ? r.Tuning.HackAndSlashFistCritPercentPerRank : 0m) :
                        (r.MainHandWeaponType == "dagger" ? r.Tuning.HackAndSlashDaggerCritPercentPerRank :
                         r.MainHandWeaponType == "fist" ? r.Tuning.HackAndSlashFistCritPercentPerRank : 0m)) *
                    (effect.ResolutionType == CombatResolutionTypes.Melee ?
                        r.Talents.Rank("hack-and-slash") : 0), 0m, 100m));
            var bonus = combat()?.DamageMultiplier(ability.Key, delivery, ctx, dest) ?? 1m;
            // Historical pinned Lethality list excludes Ambush. Gouge remains
            // control-only here; this does not add a new damaging ability.
            var lethality = crit && ability.Key is
                ("sinister-strike" or "backstab" or "mutilate" or
                 "hemorrhage" or "ghostly-strike") ||
                crit && r.UseClassWideTalents && ability.Key=="gouge"
                ? r.Talents.Rank("lethality") *
                    (r.UseClassWideTalents ? .10m : r.Tuning.LethalityBonusCriticalDamagePerRank) : 0m;
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
            var targetArmor=request.UseClassWideTalents ? target.Stats.Get("armor") : request.TargetArmor;
            if (targetArmor <= 0m || effect.SchoolKey != "physical" ||
                effect.EffectType == AbilityEffectTypes.PeriodicDamage ||
                ability.Key is "garrote" or "rupture" or "death-mark-damage")
                return DamageMitigationResult.Unmitigated(rawAmount);
            var exposed = target.ActiveAuras.Any(a => a.Definition.Key == "expose-armor" &&
                a.SourceActorKey == "rogue" && a.IsActiveAt(context.CurrentTimeSeconds));
            var armor = Math.Max(0m, targetArmor - (exposed ?
                (mechanics()?.ExposeArmorPoints ?? 0) * request.Tuning.ArmorReductionPerExposeComboPoint : 0m));
            var handType = effect.WeaponHandKey == WeaponHandKeys.OffHand
                ? request.OffHandWeaponType : request.MainHandWeaponType;
            if (handType == "mace")
                armor *= 1m - Math.Min(100m, request.Talents.Rank("hack-and-slash") *
                    request.Tuning.HackAndSlashMaceArmorIgnorePercentPerRank) / 100m;
            // Historical 3/6/9% personal ignore. Multiplication after the existing
            // Expose/mace factors is a provisional local composition convention.
            var serrated = request.Talents.Rank("serrated-blades");
            if (serrated > 0) armor *= 1m - .03m * serrated;
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

public sealed record RoguePrecombatPreview(StatCollection EffectiveStats, decimal AttackPower,
    decimal MaximumEnergy, decimal StartingEnergy, decimal MaximumHealth, object? Baseline,
    string SourceStatus, IReadOnlyList<string> ModelingNotes, RogueRotationPreview Rotation)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool ClassWide { get; init; }
    public string StatPreviewScope => ClassWide ? "rogue-class-wide-shared-precombat-provisional" : "rogue-exact-precombat-display-only-not-current-client-certified";
    public string RotationPreviewScope => ClassWide ? "talent-and-equipment-derived-priority-with-validated-custom-order" : "fixed-configured-priority-no-custom-rotation";
}

/// <summary>Deterministic configured priority without the runtime profile's random identity.
/// Each preview owns its entries and nested conditions; combat gating has not run.</summary>
public sealed record RogueRotationPreview(IReadOnlyList<RotationEntry> Entries);
