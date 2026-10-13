using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Stats;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Batch;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Abilities.Warrior;
using WarcraftSim.Data.Forever.Characters;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// End-to-end, intentionally limited Forever Warrior DPS runner. Each iteration
/// builds new actors, processors and context; no mutable combat state leaks
/// between seeds. This is a preview runner, not a validated raid-DPS forecast.
/// </summary>
public sealed class ForeverWarriorSimRunner
{
    private const string WarriorKey = "warrior";
    private const string TargetKey = "target";
    private const string MainHandAutoKey = "main-hand-auto";
    private const string OffHandAutoKey = "off-hand-auto";
    private const string MainHandSkillKey = "main-hand-weapon-skill";
    private const string OffHandSkillKey = "off-hand-weapon-skill";

    public WarriorSimResponse Run(WarriorSimRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        Validate(request);
        // Resolve IDs once, prior to seeded iterations. All runs use the same
        // server-owned snapshot of stats, enchants and weapon characteristics.
        var resolved = request.EquipmentMode == "catalog"
            ? ForeverEquipmentCatalog.ResolveWarriorLoadout(request)
            : request;
        var configured = PrepareCharacter(resolved);
        WarriorRuntimePolicyValidator.Validate(configured);
        // Never count externally maintained static versions of effects this Warrior
        // is explicitly assigned to apply. Unassigned selections remain full-uptime.
        var supportRequest = configured.ShallowCopy();
        supportRequest.RaidBuffIds = configured.RaidBuffIds.ToList();
        supportRequest.BossDebuffIds = configured.BossDebuffIds.ToList();
        foreach (var policy in configured.MaintenancePolicies)
        {
            if (policy.AbilityKey == "battle-shout")
                supportRequest.RaidBuffIds.RemoveAll(x => x == "battle-shout");
            if (policy.AbilityKey == "sunder-armor")
                supportRequest.BossDebuffIds.RemoveAll(x => x == "sunder-armor");
        }
        var support = ForeverSupportEffectCatalog.Resolve(supportRequest);
        var calculated = WarriorGearStatCalculator.Calculate(supportRequest);
        var effectiveStats = calculated.EffectiveStats;
        if (configured.StatSourceMode == "race-base")
        {
            // Classic-derived 60 Warrior HP model; recalculated after gear and buffs.
            // Keep base 1,689 HP + Stamina (first 20 grant 1 HP each).
            var stamina = effectiveStats.Get("stamina");
            configured.MaximumHealth = 1689m + Math.Min(20m, stamina) +
                Math.Max(0m, stamina - 20m) * 10m;
        }
        var isArms = string.Equals(configured.Preset, "arms", StringComparison.OrdinalIgnoreCase);
        var batch = new SimulationBatchRunner().Run(
            new SimulationBatchOptions
            {
                Iterations = request.Iterations,
                BaseSeed = request.Seed,
                SimulationType = SimulationType.Dps
            },
            (seed, representative) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return RunOne(configured, effectiveStats, support, seed,
                    representative && configured.CaptureRepresentativeTimeline, cancellationToken);
            });

        return new WarriorSimResponse
        {
            Preset = isArms ? "arms" : "fury",
            EquipmentCatalogVersion = request.EquipmentMode == "catalog" ? ForeverEquipmentCatalog.GetCatalog().CatalogVersion : null,
            ResolvedMainHandWeapon = request.EquipmentMode == "catalog"
                ? $"{configured.Gear.Single(x => string.Equals(x.Slot, "main-hand", StringComparison.OrdinalIgnoreCase)).Name}: {configured.MainHandMinimumDamage}–{configured.MainHandMaximumDamage} at {configured.MainHandSpeedSeconds:0.00}s"
                : null,
            ResolvedOffHandWeapon = request.EquipmentMode == "catalog" && configured.DualWield
                ? $"{configured.Gear.Single(x => string.Equals(x.Slot, "off-hand", StringComparison.OrdinalIgnoreCase)).Name}: {configured.OffHandMinimumDamage}–{configured.OffHandMaximumDamage} at {configured.OffHandSpeedSeconds:0.00}s"
                : null,
            SupportCatalogVersion = ForeverSupportEffectCatalog.GetCatalog().CatalogVersion,
            ActiveSupportEffects = support.SelectedEffects.Select(x => x.Name)
                .Concat(configured.MaintenancePolicies.Select(x => x.AbilityKey + " (assigned upkeep)"))
                .ToList(),
            EffectiveBossArmor = EffectiveArmor(configured, support),
            MeanDps = batch.Summary.DamagePerSecond.Mean,
            MedianDps = batch.Summary.DamagePerSecond.Median,
            EffectiveCharacterStats = new Dictionary<string, decimal>(effectiveStats.Values),
            StatContributions = calculated.StatContributions,
            Batch = batch,
            ModelingNotes =
            [
                configured.TalentMode == "tree"
                    ? "TALENT CALCULATOR: Tree ranks and prerequisites are enforced. 'Not modeled' selections do not affect damage, while 'partial' selections only apply the behavior described in their tooltip."
                    : "LEGACY talent ranks: existing individual rank inputs are retained for backwards compatibility.",
                configured.StatSourceMode == "race-base"
                    ? $"RACIAL BASE PREVIEW: {configured.Race} uses Classic Era level-60 primary stats as a temporary Forever proxy. Innate class crit, passive racial effects and exact Forever level-60 values are NOT verified; selected Orc/Troll active cooldowns are modeled separately."
                    : "MANUAL BASE: attack power and crit inputs are treated as unbuffed baseline values, excluding gear and raid support.",
                configured.MaintenancePolicies.Count > 0
                    ? "MAINTENANCE: Assigned shouts/debuffs use actual Rage, GCDs, ramp-up and refresh timers. Sunder effects on boss armor and Battle Shout effects on AP are modeled. Demo/Thunder defensive mitigation, Thunder Clap direct damage/AP coefficient, cast-miss chance, Sunder threat, party distribution and enemy attacks are not modeled."
                    : "SUPPORT ALPHA: Unassigned selected buffs/debuffs are pre-applied at full uptime; select a maintenance assignment to model their cast costs.",
                request.EnableStanceDancing
                    ? $"STANCE DANCING: Fury can use its optional Overpower dance. Arms automatically prefers Berserker (+3% crit), changing to Battle for eligible Battle-only priority skills. Stances use a separate {request.StanceSwitchGlobalCooldownSeconds:0.##}s cooldown and 10 + 3/rank Rage retention."
                    : "STANCE DANCING: Arms automatically prefers Berserker (+3% crit) and changes to Battle for eligible Battle-only priority skills; optional Fury Overpower dancing is off.",
                "MAJOR COOLDOWN GCD: Death Wish and Recklessness use a 1.5s GCD. Blood Fury and Berserking are currently modeled as off-GCD.",
                "MAJOR COOLDOWNS: Explicit use schedules and end-of-fight holds are supported. Haste only affects subsequent swing scheduling; incoming-damage/Fear side effects require a tank/encounter model.",
                "WEAPONMASTER: Matching Axe/Polearm weapons grant crit, single Mace/Staff armor ignore, and Sword weapons can trigger seeded extra attacks. Mixed-weapon per-hit armor/crit still needs refinement; sword extra-attacks use the same normalized weapon Rage generation as basic attacks.",
                "ARMS STANCE POLICY: Whirlwind is baseline and Berserker-only; the automatic Arms rotation prefers Berserker when no eligible Battle-only action is due. Battle-only actions are evaluated against current Rage and conditions. Berserker crit is applied dynamically. +10% incoming damage in Berserker requires a future attacking-boss model.",
                "FLURRY: Melee crits grant three faster swings (5% per rank); active MH/OH timers are proportionally rescheduled. Flurry and Berserking haste combine additively in this beta preview.",
                "INCOMING TALENTS: Enrage, Blood Craze, and Shield Specialization react to incoming damage/blocks, but the current stationary DPS dummy never attacks. Their effects are not a tank simulation.",
                "TWO-HANDED AND BASTION: Static damage contributions now affect actual resolved damage without corrupting the base weapon snapshot. Two-handed damage currently uses a generic physical modifier, and shield recognition is a preview; exact per-attack behavior remains partial.",
                "GORE DRINKER: Its next three landed melee attacks heal the Warrior after eligible activations. In a DPS encounter without boss attacks, a full-health Warrior receives no effective healing.",
                "Only the modeled static bonuses listed in the support catalog are counted; unknown and proc-based effects are rejected.",
                request.EquipmentMode == "catalog"
                    ? "CATALOG: Item IDs resolve to a curated Forever beta snapshot. MH/OH damage and speed come from equipment; recognized static item/enchant/set bonuses flow into effective stats. Other item effects, procs, and unlisted items are NOT modeled. Stamina-to-HP is included only in race-base preview mode. AP/crit/hit/haste inputs exclude gear."
                    : request.EquipmentMode == "gear-bonuses"
                        ? "ALPHA gear editor: custom named item/enchant stat bonuses; baseline AP/crit/hit/haste inputs must EXCLUDE gear. Weapons use manual damage/speed."
                        : "ALPHA: Manual character-sheet stats and weapon values; enable Catalog mode to select item IDs.",
                "Glancing damage multiplier is an explicit, unverified calibration input.",
                "Armor reduction uses explicit provisional rational-formula constants, not a confirmed Forever curve.",
                "Rend's total Attack Power coefficient defaults to zero until verified; it can be supplied explicitly.",
                request.BehindTarget
                    ? "Rear positioning disables target parry and block but retains dodge. No movement or frontal mechanics are simulated."
                    : "Frontal attacks allow parry/block; there is no boss parry-haste model.",
                request.RotationMode == "custom"
                    ? "CUSTOM priority: ordered rules are user-configured and not optimized by the simulator."
                    : "AUTOMATIC priority is an introductory build-aware baseline, not yet an optimized rotation.",
                ..WarriorRotationRules.GetSkippedCooldownDependencies(configured).Select(
                    rule => $"SKIPPED CUSTOM PRIORITY: {rule}; retained in saved setup for other builds."),
                "UNBRIDLED WRATH: Modeled only from successful swing-timer attacks (MH/OH autos and queued Heroic Strike/Cleave), not instant specials such as Whirlwind. Forever-specific proc mask remains subject to beta validation.",
                "The target health percent is scripted by time for this single-character sim. Default execute is the final configured seconds; custom milestones replace that schedule.",
                "Deep Wounds currently uses the configurable 2-second tick cadence assumption.",
                $"Opening auto attack: {(request.ImmediateOpeningSwing ? request.OpeningAutoAttackSeconds.ToString("0.00") + "s" : "full weapon-speed delay (legacy)")}.",
                "TALENT-GATED ACTIONS: Unlearned talent abilities are not created, and custom rules referencing them are skipped; the default preset only adds learned actions.",
                ..ForeverWarriorTalentCatalog.UnmodeledSelections(configured),
                "Optional Slam delays the main-hand swing; Cleave is allowed but a one-target encounter provides no secondary target.",
                "The training target has an effectively inexhaustible actual HP pool and the simulation always runs for the selected duration."
            ]
        };
    }

    public static void Validate(WarriorSimRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ForeverWarriorTalentCatalog.Validate(request);
        if (request.MaximumRageForOverpowerDance is < 0m or > 100m ||
            request.StanceSwitchGlobalCooldownSeconds is < 0m or > 2m)
            throw new ArgumentException("Stance dance Rage and GCD settings are out of range.");
        ForeverWarriorRaceProfiles.Validate(request);
        if (request.EquipmentMode == "catalog")
        {
            Validate(ForeverEquipmentCatalog.ResolveWarriorLoadout(request));
            return;
        }
        var arms = string.Equals(request.Preset, "arms", StringComparison.OrdinalIgnoreCase);
        var fury = string.Equals(request.Preset, "fury", StringComparison.OrdinalIgnoreCase);
        if (!arms && !fury) throw new ArgumentException("Preset must be 'arms' or 'fury'.");
        if (request.Level != 60) throw new ArgumentException("The first Warrior preset is level 60 only.");
        if (request.Iterations is < 1 or > 500)
            throw new ArgumentException("Iterations must be from 1 through 500.");
        if (request.OpeningAutoAttackSeconds < 0m || request.OpeningAutoAttackSeconds >= request.DurationSeconds)
            throw new ArgumentException("OpeningAutoAttackSeconds must be >= 0 and earlier than the end of the encounter.");
        if (request.DurationSeconds is < 1m or > 600m)
            throw new ArgumentException("DurationSeconds must be from 1 through 600.");
        if (request.TargetCreatureType is not ("humanoid" or "giant" or "dragonkin" or "mounted"))
            throw new ArgumentException("TargetCreatureType must be humanoid, giant, dragonkin or mounted.");
        if (request.TargetLevel is < 1 or > 100)
            throw new ArgumentException("TargetLevel must be from 1 through 100.");
        if (request.DualWield && request.IsTwoHanded)
            throw new ArgumentException("A two-handed weapon cannot be combined with an off-hand weapon.");
        CheckWeapon(request.MainHandMinimumDamage, request.MainHandMaximumDamage,
            request.MainHandSpeedSeconds, "Main hand");
        if (request.DualWield)
            CheckWeapon(request.OffHandMinimumDamage, request.OffHandMaximumDamage,
                request.OffHandSpeedSeconds, "Off hand");
        if (request.AttackPower < 0m || request.MainHandWeaponSkill < 0m ||
            request.OffHandWeaponSkill < 0m || request.MaximumHealth <= 0m ||
            request.TargetArmor < 0m)
            throw new ArgumentException("AP, weapon skills and target armor must be nonnegative; player health must be positive.");
        if (request.StartingRage is < 0m or > 100m ||
            request.HeroicStrikeMinimumRage is < 0m or > 100m)
            throw new ArgumentException("Rage values must be within 0–100.");
        CheckPercent(request.MeleeCritPercent, "MeleeCritPercent");
        CheckPercent(request.MeleeHitPercent, "MeleeHitPercent");
        CheckPercent(request.MeleeHastePercent, "MeleeHastePercent");
        CheckPercent(request.OffHandHitPercent, "OffHandHitPercent");
        CheckPercent(request.OffHandDamagePercent, "OffHandDamagePercent");
        CheckPercent(request.TargetDodgePercent, "TargetDodgePercent");
        CheckPercent(request.TargetParryPercent, "TargetParryPercent");
        CheckPercent(request.TargetBlockPercent, "TargetBlockPercent");
        if (request.ProvisionalGlancingDamageMultiplier is < 0m or > 1m)
            throw new ArgumentException("ProvisionalGlancingDamageMultiplier must be within 0–1.");
        if (request.ProvisionalArmorBaseConstant < 0m ||
            request.ProvisionalArmorPerAttackerLevelConstant < 0m ||
            request.ProvisionalArmorBaseConstant + request.ProvisionalArmorPerAttackerLevelConstant * request.Level <= 0m)
            throw new ArgumentException("The provisional armor denominator constants must produce a positive denominator.");
        if (request.RendTotalAttackPowerCoefficient is < 0m or > 5m)
            throw new ArgumentException("RendTotalAttackPowerCoefficient must be from 0 through 5.");
        if (request.DeepWoundsRank is < 0 or > 3 || request.ImprovedOverpowerRank is < 0 or > 2 ||
            request.ImprovedRendRank is < 0 or > 3 || request.ImpaleRank is < 0 or > 2 ||
            request.BloodthrillRank is < 0 or > 5)
            throw new ArgumentException("One or more talent ranks are outside their supported ranges.");
        var deepRank = request.TalentMode == "tree"
            ? ForeverWarriorTalentCatalog.Rank(request, "deep-wounds") : request.DeepWoundsRank;
        var impaleRank = request.TalentMode == "tree"
            ? ForeverWarriorTalentCatalog.Rank(request, "impale") : request.ImpaleRank;
        var bloodthrillRank = request.TalentMode == "tree"
            ? ForeverWarriorTalentCatalog.Rank(request, "bloodthrill") : request.BloodthrillRank;
        if (impaleRank > 0 && deepRank != 3)
            throw new ArgumentException("Impale requires Deep Wounds rank 3.");
        // Cross-tree allocations are valid. Stance-dependent attacks remain
        // gated by their real aura, including optional Fury stance dancing.
        // A rotation preset is not a restriction on the talent allocation.
        // Validate the effective scripted phase schedule up front, before batch iteration.
        _ = BuildTargetHealthTimeline(request);
        WarriorGearStatCalculator.Validate(request);
        _ = ForeverSupportEffectCatalog.Resolve(request);
        WarriorRuntimePolicyValidator.Validate(request);
        WarriorRotationRules.Validate(request);
    }

    private static WarriorSimRequest PrepareCharacter(WarriorSimRequest source)
    {
        var result = source.ShallowCopy();
        if (result.TalentMode != "tree") return result;
        static int R(WarriorSimRequest request, string id) =>
            ForeverWarriorTalentCatalog.Rank(request, id);
        result.DeepWoundsRank = R(result, "deep-wounds");
        result.ImpaleRank = R(result, "impale");
        result.ImprovedRendRank = R(result, "improved-rend");
        result.ImprovedOverpowerRank = R(result, "improved-overpower");
        result.BloodthrillRank = R(result, "bloodthrill");
        // Only the statically understood components of these talents are modeled.
        result.OffHandHitPercent += R(result, "furious-precision") switch
        {
            1 => 4m, 2 => 7m, 3 => 10m, _ => 0m
        };
        result.OffHandDamagePercent += 5m * R(result, "dual-wield-specialization");
        return result;
    }

    private static ScriptedTargetHealthTimeline BuildTargetHealthTimeline(WarriorSimRequest request)
    {
        if (request.ExecutePhaseDurationSeconds is < 0m or > 600m)
            throw new ArgumentException("ExecutePhaseDurationSeconds must be from 0 through 600 seconds.");
        // Shorter quick-test durations remain valid with the default 30s setting.
        var executeDuration = Math.Min(request.ExecutePhaseDurationSeconds, request.DurationSeconds);

        var milestones = request.TargetHealthMilestones ??
            throw new ArgumentException("TargetHealthMilestones cannot be null.");
        if (milestones.Count > 30)
            throw new ArgumentException("At most 30 scripted health milestones are supported.");

        List<ScriptedTargetHealthCheckpoint> checkpoints = [new(0m, 100m)];
        if (milestones.Count != 0)
        {
            foreach (var point in milestones)
            {
                if (point is null || point.TimeSeconds <= 0m ||
                    point.TimeSeconds >= request.DurationSeconds)
                    throw new ArgumentException("Health milestone times must be after 0 and before encounter end.");
                checkpoints.Add(new ScriptedTargetHealthCheckpoint(point.TimeSeconds, point.HealthPercent));
            }
        }
        else if (executeDuration > 0m)
        {
            var executeStart = request.DurationSeconds - executeDuration;
            if (executeStart == 0m)
                checkpoints[0] = new ScriptedTargetHealthCheckpoint(0m, 20m);
            else
                checkpoints.Add(new ScriptedTargetHealthCheckpoint(executeStart, 20m));
        }
        return new ScriptedTargetHealthTimeline(checkpoints);
    }

    private static void CheckWeapon(decimal minimum, decimal maximum, decimal speed, string name)
    {
        if (minimum < 0m || maximum < minimum || speed < 0.5m || speed > 10m)
            throw new ArgumentException($"{name} requires ordered nonnegative damage and a weapon speed from 0.5–10 seconds.");
    }

    private static void CheckPercent(decimal value, string name)
    {
        if (value is < 0m or > 100m)
            throw new ArgumentException($"{name} must be between 0 and 100.");
    }

    private static decimal EffectiveArmor(WarriorSimRequest request, ForeverSupportPlan support)
    {
        var armor = Math.Max(0m, request.TargetArmor - support.TotalArmorReduction);
        // Weaponmaster maces/staves ignore a portion of post-debuff armor.
        // Mixed-hand weapon setups need a per-hit mitigation model instead.
        if (request.TalentMode == "tree" && !request.DualWield &&
            request.MainHandWeaponType is "mace" or "staff")
            armor *= 1m - 0.03m * ForeverWarriorTalentCatalog.Rank(request, "weaponmaster");
        return armor;
    }

    private static SimulationRunResult RunOne(WarriorSimRequest request, StatCollection effectiveStats, ForeverSupportPlan support, int seed, bool captureTimeline, CancellationToken cancellationToken)
    {
        var arms = string.Equals(request.Preset, "arms", StringComparison.OrdinalIgnoreCase);
        var context = new SimulationContext(new SimulationRunOptions
        {
            Seed = seed, DurationSeconds = request.DurationSeconds,
            PrimaryActorKey = WarriorKey, CaptureTimeline = captureTimeline,
            // Guard a single-target DPS preview against runaway event queues.
            // Multi-actor raid runners can configure their own higher budget.
            MaximumProcessedEvents = 20_000, CancellationToken = cancellationToken
        });
        var warrior = CreateWarrior(request, arms, effectiveStats);
        var target = new SimulationActorState
        {
            Key = TargetKey, Name = "Single-target training boss", TeamKey = "enemy",
            Level = request.TargetLevel
        };
        // The DPS target must not die in a timed run: actual damage is tracked,
        // while the health percentage used by execute/rotations is a scripted schedule.
        target.InitializeHealth(1_000_000_000_000_000_000m);
        var healthTimeline = BuildTargetHealthTimeline(request);
        // Debuffs are applied to the encounter's real armor input, before
        // the ruleset's existing mitigation calculation. Never reduce below 0.
        target.Stats.Set(ForeverCombatStatKeys.Armor,
            EffectiveArmor(request, support));
        target.Stats.Set(ForeverCombatStatKeys.DodgeChancePercent, request.TargetDodgePercent);
        target.Stats.Set(ForeverCombatStatKeys.ParryChancePercent, request.TargetParryPercent);
        target.Stats.Set(ForeverCombatStatKeys.BlockChancePercent, request.TargetBlockPercent);
        context.AddActor(warrior);
        context.AddActor(target);
        context.SetScriptedTargetHealth(TargetKey, healthTimeline);

        var ruleset = ForeverCombatRulesetFactory.Create();
        foreach (var resolution in new[]
        {
            ForeverCombatResolutionTypes.PlayerMeleeAuto,
            ForeverCombatResolutionTypes.PlayerDualWieldMeleeAuto
        })
        {
            var rule = ruleset.RollRules[resolution];
            rule.MinimumGlancingDamageMultiplier = request.ProvisionalGlancingDamageMultiplier;
            rule.MaximumGlancingDamageMultiplier = request.ProvisionalGlancingDamageMultiplier;
        }
        ruleset.MitigationRules[DamageMitigationTypes.Armor] =
            new DamageMitigationRuleDefinition
            {
                MitigationType = DamageMitigationTypes.Armor,
                FormulaType = DamageMitigationFormulaTypes.RationalLevelScaled,
                DefenseStatKey = ForeverCombatStatKeys.Armor,
                BaseConstant = request.ProvisionalArmorBaseConstant,
                PerAttackerLevelConstant = request.ProvisionalArmorPerAttackerLevelConstant,
                MaximumReductionPercent = 75m
            };

        var auraManager = new AuraManager();
        var executor = new AbilityExecutor(
            auraManager,
            new RulesetCombatRollResolver(ruleset, new ForeverCombatRollContextAdjustmentProvider()),
            new RulesetDamageMitigationResolver(ruleset),
            abilityTimingProvider: new ForeverAbilityTimingProvider());
        var autoProcessor = new AutoAttackProcessor(executor, new ForeverAutoAttackTimingProvider());
        var mainHand = CreateMainHandAuto(request);
        AutoAttackDefinition? offHand = request.DualWield ? CreateOffHandAuto(request) : null;
        var weaponTalents = new WarriorWeaponTalentProcessor(request, autoProcessor, executor, mainHand, offHand);
        var rotation = BuildRotation(request, arms);
        var runtimeProcessor = new WarriorRuntimeActionProcessor(
            executor, auraManager, request, warrior, target, autoProcessor);
        var retainedRage = 10m + (request.TalentMode == "tree"
            ? ForeverWarriorTalentCatalog.Rank(request, "improved-tactical-mastery") * 3m : 0m);
        var furyDance = !arms && request.EnableStanceDancing &&
            (request.RotationMode != "custom" || request.CustomRotation.Any(rule =>
                rule.Enabled && string.Equals(rule.Action, "overpower", StringComparison.OrdinalIgnoreCase)));
        ICombatEventProcessor[] danceProcessors = arms && request.Level >= 30
            ? [new WarriorArmsStancePriorityProcessor(executor, rotation, retainedRage)]
            : furyDance
                ? [new WarriorStanceDanceProcessor(executor,
                    request.MaximumRageForOverpowerDance, retainedRage)]
                : [];
        var rotationProcessor = new PriorityRotationExecutor(rotation,
            WarriorKey, TargetKey, executor, autoAttackProcessor: autoProcessor);
        var engine = new SimulationEngine(
        [
            auraManager, executor, autoProcessor,
            weaponTalents,
            new WarriorIncomingTalentProcessor(request),
            new CriticalStrikeRollingDamageProcessor(auraManager),
            new ReactiveAbilityOpportunityProcessor(),
            ..danceProcessors,
            runtimeProcessor,
            new WarriorRageTalentProcessor(
                configuredRageTalentRank: request.TalentMode == "tree"
                    ? ForeverWarriorTalentCatalog.Rank(request, "unbridled-wrath") : 0),
            rotationProcessor
        ]);

        return engine.Run(context, started =>
        {
            var startingBerserker = request.Level >= 30;
            auraManager.ApplyAura(started, warrior,
                new AuraDefinition
                {
                    Key = startingBerserker ? ForeverWarriorAuraKeys.BerserkerStance : ForeverWarriorAuraKeys.BattleStance,
                    Name = startingBerserker ? "Berserker Stance" : "Battle Stance",
                    DurationSeconds = request.DurationSeconds + 1m
                }, WarriorKey, null, null);
            runtimeProcessor.RefreshDynamicStats(started);
            if (!autoProcessor.Start(started, WarriorKey, TargetKey, mainHand,
                    request.ImmediateOpeningSwing ? request.OpeningAutoAttackSeconds : null))
                throw new InvalidOperationException("Could not start the main-hand attack stream.");
            if (offHand is not null && !autoProcessor.Start(started, WarriorKey, TargetKey, offHand,
                    request.ImmediateOpeningSwing ? request.OpeningAutoAttackSeconds : null))
                throw new InvalidOperationException("Could not start the off-hand attack stream.");
            if (request.TalentMode == "tree" &&
                ForeverWarriorTalentCatalog.Rank(request, "anger-management") > 0)
            {
                for (var tick = 3m; tick < request.DurationSeconds; tick += 3m)
                    started.ScheduleEvent(new CombatEvent
                    {
                        TimeSeconds = tick, Type = CombatEventType.RotationDecision,
                        SourceActorKey = WarriorKey, TargetActorKey = TargetKey,
                        EffectKey = WarriorRageTalentProcessor.AngerManagementEffectKey,
                        IsInternal = true,
                        Description = "Anger Management: gain 1 Rage."
                    });
            }
            runtimeProcessor.ScheduleInitialDecisions(started);
            // Reconsider priority exactly when scripted health changes, including
            // if the character was idle immediately before the execute phase.
            foreach (var point in healthTimeline.Checkpoints.Where(p => p.TimeSeconds > 0m))
                started.ScheduleEvent(new CombatEvent
                {
                    TimeSeconds = point.TimeSeconds,
                    Type = CombatEventType.RotationDecision,
                    SourceActorKey = WarriorKey,
                    TargetActorKey = TargetKey,
                    IsInternal = true,
                    Description = "Scripted target-health breakpoint: reevaluate rotation."
                });
        });
    }

    private static SimulationActorState CreateWarrior(WarriorSimRequest request, bool arms, StatCollection effectiveStats)
    {
        var warrior = new SimulationActorState
        {
            Key = WarriorKey, Name = "Level 60 Warrior", TeamKey = "raid",
            AssignedRole = SimulationType.Dps, Level = request.Level
        };
        warrior.InitializeHealth(request.MaximumHealth);
        warrior.AddResource(new ResourceState("rage", 100m, request.StartingRage));
        warrior.Stats.Set(ForeverCombatStatKeys.AttackPower, effectiveStats.Get(ForeverCombatStatKeys.AttackPower));
        warrior.Stats.Set(CombatPositionStatKeys.AttackingFromBehind, request.BehindTarget ? 1m : 0m);
        warrior.Stats.Set(ForeverCombatStatKeys.AttackCriticalChancePercent, effectiveStats.Get(ForeverCombatStatKeys.AttackCriticalChancePercent));
        warrior.Stats.Set(ForeverCombatStatKeys.HitChancePercent, effectiveStats.Get(ForeverCombatStatKeys.HitChancePercent));
        warrior.Stats.Set(ForeverCombatStatKeys.HastePercent, effectiveStats.Get(ForeverCombatStatKeys.HastePercent));
        warrior.Stats.Set(ForeverCombatStatKeys.OffHandHitChancePercent, request.OffHandHitPercent);
        warrior.Stats.Set(ForeverCombatStatKeys.OffHandDamagePercent, request.OffHandDamagePercent);
        foreach (var (key, value) in effectiveStats.Values)
            warrior.Stats.Set(key, value);
        if (request.TalentMode == "tree")
        {
            var twoHandedRank = request.IsTwoHanded ? ForeverWarriorTalentCatalog.Rank(request, "two-handed-weapon-specialization") : 0;
            var hasShield = !request.DualWield && !request.IsTwoHanded &&
                request.Gear.Any(item => item.Slot == "off-hand" && item.ItemKey != "" &&
                    item.Name.Contains("shield", StringComparison.OrdinalIgnoreCase));
            var bastionRank = hasShield ? ForeverWarriorTalentCatalog.Rank(request, "bastion") : 0;
            warrior.Stats.Add("physical-damage-bonus-percent", twoHandedRank + 2m * bastionRank);
        }
        warrior.Stats.Set(MainHandSkillKey, request.MainHandWeaponSkill);
        warrior.Stats.Set(OffHandSkillKey, request.OffHandWeaponSkill);

        if (request.DeepWoundsRank > 0)
        {
            warrior.AddCriticalStrikeRollingDamageProc(
                ForeverDeepWoundsFactory.Create(request.DeepWoundsRank,
                    request.MainHandMinimumDamage, request.MainHandMaximumDamage,
                    request.DualWield ? request.OffHandMinimumDamage : null,
                    request.DualWield ? request.OffHandMaximumDamage : null));
        }
        foreach (var ability in WarriorRuntimeActionProcessor.CreateAbilities(request))
            warrior.AddAbility(ability);
        if (request.UseBloodrage)
            warrior.AddAbility(ForeverBloodrageFactory.Create(request.MaximumHealth,
                rageGenerationMultiplier: request.TalentMode == "tree"
                    ? 1m + 0.25m * ForeverWarriorTalentCatalog.Rank(request, "improved-bloodrage") : 1m));
        // Arms uses Battle-only attacks (Rend, Overpower, Spearing Strike)
        // even in its automatic rotation. Its preferred Berserker opener
        // therefore requires both stance actions regardless of custom settings.
        // Fury continues to opt into stance dancing explicitly.
        if (arms || request.EnableStanceDancing)
        {
            var tacticalRank = request.TalentMode == "tree" ?
                ForeverWarriorTalentCatalog.Rank(request, "improved-tactical-mastery") : 0;
            warrior.AddAbility(ForeverWarriorStanceFactory.Create(
                ForeverWarriorAuraKeys.BattleStance, tacticalRank,
                baseRetainedRage: 10m,
                globalCooldownSeconds: request.StanceSwitchGlobalCooldownSeconds));
            warrior.AddAbility(ForeverWarriorStanceFactory.Create(
                ForeverWarriorAuraKeys.BerserkerStance, tacticalRank,
                baseRetainedRage: 10m,
                globalCooldownSeconds: request.StanceSwitchGlobalCooldownSeconds));
        }
        if (arms || request.EnableStanceDancing)
        {
            var overpower = ForeverOverpowerFactory.CreateForLevel(request.Level,
                request.MainHandMinimumDamage, request.MainHandMaximumDamage,
                NormalizedSpeed(request), MainHandSkillKey,
                improvedOverpowerRank: request.ImprovedOverpowerRank,
                impaleRank: request.ImpaleRank, deepWoundsRank: request.DeepWoundsRank);
            warrior.AddAbility(overpower);
            warrior.AddReactiveOpportunityDefinition(
                ForeverOverpowerFactory.CreateOpportunity(request.BloodthrillRank));
        }
        // Stance dancing unlocks Overpower for Fury; it must never replace the
        // Fury ability set with the Arms ability set.
        if (arms)
        {
            if (request.TalentMode != "tree" || ForeverWarriorTalentCatalog.Rank(request, "mortal-strike") > 0)
            {
                var mortalStrike = ForeverMortalStrikeFactory.CreateForLevel(request.Level,
                    request.MainHandMinimumDamage, request.MainHandMaximumDamage,
                    NormalizedSpeed(request), MainHandSkillKey);
                ForeverWarriorArmsTalentConfigurator.ApplyImpale(
                    mortalStrike, request.ImpaleRank, request.DeepWoundsRank);
                warrior.AddAbility(mortalStrike);
            }
            if (request.TalentMode == "tree" &&
                ForeverWarriorTalentCatalog.Rank(request, "spearing-strike") > 0)
                warrior.AddAbility(ForeverSpearingStrikeFactory.Create(
                    request.MainHandMinimumDamage, request.MainHandMaximumDamage,
                    NormalizedSpeed(request), MainHandSkillKey,
                    request.TargetCreatureType is "giant" or "dragonkin" or "mounted"));
            if (request.UseRend)
            {
                var rend = ForeverRendFactory.CreateForLevel(request.Level,
                    request.RendTotalAttackPowerCoefficient, request.ImprovedRendRank);
                warrior.AddAbility(rend);
            }
        }
        else
        {
            if (request.TalentMode != "tree" || ForeverWarriorTalentCatalog.Rank(request, "bloodthirst") > 0)
            {
                var bloodthirst = ForeverBloodthirstFactory.CreateForLevel(request.Level, MainHandSkillKey);
                ForeverWarriorArmsTalentConfigurator.ApplyImpale(
                    bloodthirst, request.ImpaleRank, request.DeepWoundsRank);
                warrior.AddAbility(bloodthirst);
            }

        }

        // Whirlwind is a baseline level-36 Warrior action in every stance/spec.
        // Fury/Arms selection must not determine its availability.
        var whirlwind = request.DualWield
            ? ForeverWhirlwindFactory.CreateDualWield(
                request.MainHandMinimumDamage, request.MainHandMaximumDamage,
                NormalizedSpeed(request), MainHandSkillKey,
                request.OffHandMinimumDamage, request.OffHandMaximumDamage,
                ForeverNormalizedWeaponSpeeds.OneHanded, OffHandSkillKey,
                rageCostReduction: request.TalentMode == "tree" &&
                    ForeverWarriorTalentCatalog.Rank(request, "raging-blows") > 0 ? 3m : 0m)
            : ForeverWhirlwindFactory.CreateSingleWeapon(
                request.MainHandMinimumDamage, request.MainHandMaximumDamage,
                NormalizedSpeed(request), MainHandSkillKey,
                rageCostReduction: request.TalentMode == "tree" &&
                    ForeverWarriorTalentCatalog.Rank(request, "raging-blows") > 0 ? 3m : 0m);
        ForeverWarriorArmsTalentConfigurator.ApplyImpale(
            whirlwind, request.ImpaleRank, request.DeepWoundsRank);
        warrior.AddAbility(whirlwind);
        if (request.UseSlam)
        {
            var slam = ForeverSlamFactory.CreateForLevel(request.Level,
                request.MainHandMinimumDamage, request.MainHandMaximumDamage,
                request.MainHandSpeedSeconds, MainHandSkillKey);
            var improvedSlam = request.TalentMode == "tree" ?
                ForeverWarriorTalentCatalog.Rank(request, "improved-slam") : 0;
            if (improvedSlam > 0)
            {
                slam.CastTimeSeconds = Math.Max(0m, slam.CastTimeSeconds - 0.25m * improvedSlam);
                slam.GlobalCooldownSeconds = Math.Max(0m, slam.GlobalCooldownSeconds - 0.25m * improvedSlam);
                slam.CooldownSeconds = Math.Max(0m, slam.CooldownSeconds - 3m);
                slam.DelayedAutoAttackWeaponHandKeys.Clear();
            }
            warrior.AddAbility(slam);
        }
        if (request.UseCleave)
            warrior.AddNextSwingReplacement(ForeverCleaveFactory.CreateForLevel(
                request.Level, request.MainHandMinimumDamage,
                request.MainHandMaximumDamage, request.MainHandSpeedSeconds,
                MainHandSkillKey, rageCostReduction: request.TalentMode == "tree" &&
                    ForeverWarriorTalentCatalog.Rank(request, "raging-blows") > 0 ? 3m : 0m));
        if (request.UseExecute)
        {
            var execute = ForeverExecuteFactory.CreateForLevel(request.Level, MainHandSkillKey,
                rageCostReduction: request.TalentMode == "tree"
                    ? ForeverWarriorTalentCatalog.Rank(request, "improved-execute") switch
                    { 1 => 3m, 2 => 5m, _ => 0m } : 0m);
            ForeverWarriorArmsTalentConfigurator.ApplyImpale(
                execute, request.ImpaleRank, request.DeepWoundsRank);
            warrior.AddAbility(execute);
        }
        if (request.UseHeroicStrike)
            warrior.AddNextSwingReplacement(ForeverHeroicStrikeFactory.CreateForLevel(
                request.Level, request.MainHandMinimumDamage,
                request.MainHandMaximumDamage, request.MainHandSpeedSeconds,
                MainHandSkillKey,
                rageCostReduction: request.TalentMode == "tree"
                    ? ForeverWarriorTalentCatalog.Rank(request, "improved-heroic-strike") : 0m));
        if (request.TalentMode == "tree")
        {
            var costReduction = ForeverWarriorTalentCatalog.Rank(request, "focused-rage");
            if (costReduction > 0)
            {
                var offense = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "overpower", "execute", "mortal-strike", "bloodthirst", "whirlwind",
                    "rend", "slam", "spearing-strike", "sunder-armor", "thunder-clap"
                };
                foreach (var (key, ability) in warrior.Abilities)
                    if (offense.Contains(key))
                        foreach (var cost in ability.Definition.ResourceCosts)
                            if (cost.ResourceKey == "rage")
                                cost.Amount = Math.Max(0m, cost.Amount - costReduction);
                foreach (var (key, replacement) in warrior.NextSwingReplacements)
                    if (key is "heroic-strike" or "cleave")
                        foreach (var cost in replacement.ResourceCosts)
                            if (cost.ResourceKey == "rage")
                                cost.Amount = Math.Max(0m, cost.Amount - costReduction);
            }
        }
        return warrior;
    }

    private static decimal NormalizedSpeed(WarriorSimRequest request) =>
        request.IsTwoHanded ? ForeverNormalizedWeaponSpeeds.TwoHanded :
            ForeverNormalizedWeaponSpeeds.OneHanded;

    private static AutoAttackDefinition CreateMainHandAuto(WarriorSimRequest request)
    {
        if (request.DualWield)
            return ForeverWarriorAutoAttackFactory.CreateDualWieldMainHand(
                MainHandAutoKey, "Main-hand auto attack",
                request.MainHandMinimumDamage, request.MainHandMaximumDamage,
                request.MainHandSpeedSeconds, MainHandSkillKey);
        return request.IsTwoHanded
            ? ForeverWarriorAutoAttackFactory.CreateTwoHandedMainHand(
                MainHandAutoKey, "Two-handed auto attack",
                request.MainHandMinimumDamage, request.MainHandMaximumDamage,
                request.MainHandSpeedSeconds, MainHandSkillKey)
            : ForeverWarriorAutoAttackFactory.CreateOneHandedMainHand(
                MainHandAutoKey, "One-handed auto attack",
                request.MainHandMinimumDamage, request.MainHandMaximumDamage,
                request.MainHandSpeedSeconds, MainHandSkillKey);
    }

    private static AutoAttackDefinition CreateOffHandAuto(WarriorSimRequest request)
    {
        var definition = ForeverWarriorAutoAttackFactory.CreateDualWieldOffHand(
            OffHandAutoKey, "Off-hand auto attack",
            request.OffHandMinimumDamage, request.OffHandMaximumDamage,
            request.OffHandSpeedSeconds, OffHandSkillKey);
        if (request.TalentMode == "tree")
        {
            var rank = ForeverWarriorTalentCatalog.Rank(request, "dual-wield-specialization");
            var multiplier = 1m + rank * 0.10m;
            foreach (var generation in definition.ResourceGenerations)
            {
                generation.AmountPerLandedSwing *= multiplier;
                generation.CriticalBonusAmountPerLandedSwing *= multiplier;
            }
        }
        return definition;
    }

    private static RotationProfile BuildRotation(WarriorSimRequest request, bool arms)
    {
        if (request.RotationMode == "custom")
            return WarriorRotationRules.BuildCustom(request);

        var rotation = new RotationProfile
        {
            Name = arms ? "Warrior Arms Preview" : "Warrior Fury Preview",
            RulesetKey = "wow-forever", ClassKey = ForeverCharacterClassKeys.Warrior,
            SimulationType = SimulationType.Dps
        };
        var priority = 0;
        if (request.UseBloodrage)
            rotation.Entries.Add(new RotationEntry
            {
                Priority = ++priority, AbilityKey = ForeverBloodrageFactory.AbilityKey,
                Conditions =
                [
                    new RotationConditionDefinition
                    {
                        ConditionType = RotationConditionTypes.SourceResourceCurrent,
                        Key = "rage", ComparisonOperator = RotationComparisonOperators.LessThanOrEqual,
                        Value = 30m
                    }
                ]
            });
        if (arms)
            rotation.Entries.Add(new RotationEntry
            {
                Priority = ++priority, AbilityKey = ForeverOverpowerFactory.AbilityKey
            });
        if (request.UseExecute)
            rotation.Entries.Add(new RotationEntry
            {
                Priority = ++priority, AbilityKey = ForeverExecuteFactory.AbilityKey,
                Conditions =
                [
                    new RotationConditionDefinition
                    {
                        ConditionType = RotationConditionTypes.TargetHealthPercent,
                        ComparisonOperator = RotationComparisonOperators.LessThanOrEqual,
                        Value = 20m
                    }
                ]
            });
        if (request.TalentMode != "tree" || (arms
            ? ForeverWarriorTalentCatalog.Rank(request, "mortal-strike") > 0
            : ForeverWarriorTalentCatalog.Rank(request, "bloodthirst") > 0))
            rotation.Entries.Add(new RotationEntry
            {
                Priority = ++priority,
                AbilityKey = arms ? ForeverMortalStrikeFactory.AbilityKey : ForeverBloodthirstFactory.AbilityKey
            });
        if (!arms)
            rotation.Entries.Add(new RotationEntry
            {
                Priority = ++priority, AbilityKey = ForeverWhirlwindFactory.AbilityKey
            });
        if (arms && request.TalentMode == "tree" &&
            ForeverWarriorTalentCatalog.Rank(request, "spearing-strike") > 0)
            rotation.Entries.Add(new RotationEntry
            {
                Priority = ++priority,
                AbilityKey = ForeverSpearingStrikeFactory.AbilityKey
            });
        if (arms && request.UseRend)
            rotation.Entries.Add(new RotationEntry
            {
                Priority = ++priority, AbilityKey = ForeverRendFactory.AbilityKey,
                Conditions =
                [
                    new RotationConditionDefinition
                    {
                        ConditionType = RotationConditionTypes.TargetAuraMissing,
                        Key = ForeverRendFactory.AuraKey
                    }
                ]
            });
        if (request.UseHeroicStrike)
            rotation.Entries.Add(new RotationEntry
            {
                Priority = ++priority,
                ActionType = RotationActionTypes.QueueNextSwingReplacement,
                NextSwingReplacementKey = ForeverHeroicStrikeFactory.AbilityKey,
                AutoAttackKey = MainHandAutoKey,
                Conditions =
                [
                    new RotationConditionDefinition
                    {
                        ConditionType = RotationConditionTypes.SourceResourceCurrent,
                        Key = "rage", ComparisonOperator = RotationComparisonOperators.GreaterThanOrEqual,
                        Value = request.HeroicStrikeMinimumRage
                    }
                ]
            });
        return rotation;
    }
}
