using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Encounters;
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
/// End-to-end Forever Warrior DPS and first Protection tank runner. Each iteration
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
        (request.CombatItems ?? throw new ArgumentException("CombatItems cannot be null.")).Validate("warrior");
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
        var windowMaxima = new List<decimal>(request.Iterations);
        var threatRates = new List<decimal>(request.Iterations);
        var batch = new SimulationBatchRunner().Run(
            new SimulationBatchOptions
            {
                Iterations = request.Iterations,
                BaseSeed = request.Seed,
                SimulationType = request.TankMode ? SimulationType.Tank : SimulationType.Dps
            },
            (seed, representative) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var outcome = RunOne(configured, effectiveStats, support, seed,
                    representative && configured.CaptureRepresentativeTimeline, cancellationToken);
                if (!representative)
                {
                    windowMaxima.Add(outcome.CustomMetrics.GetValueOrDefault("tank-max-window-percent"));
                    threatRates.Add(outcome.CustomMetrics.GetValueOrDefault("boss-threat") /
                        Math.Max(1m, configured.DurationSeconds));
                }
                return outcome;
            });

        var sortedWindowMaxima = windowMaxima.OrderBy(x => x).ToArray();
        var p95Window = sortedWindowMaxima[Math.Max(0, (int)Math.Ceiling(sortedWindowMaxima.Length * 0.95d) - 1)];
        var orderedThreatRates = threatRates.OrderBy(x => x).ToArray();
        var p95Threat = orderedThreatRates[Math.Max(0,
            (int)Math.Ceiling(orderedThreatRates.Length * 0.95d) - 1)];
        return new WarriorSimResponse
        {
            MeanMaximumWindowPercentOfHealth = windowMaxima.Average(),
            P95MaximumWindowPercentOfHealth = p95Window,
            MeanDamageTakenPerSecond = batch.Summary.DamageTakenPerSecond.Mean,
            MeanThreatPerSecond = threatRates.Average(),
            P95ThreatPerSecond = p95Threat,
            DeathRatePercent = batch.Summary.PrimaryActorDeathRatePercent,
            RepresentativeMaximumWindowDamage = batch.RepresentativeRun.CustomMetrics.GetValueOrDefault("tank-max-window-damage"),
            RepresentativeMaximumWindowPercentOfHealth = batch.RepresentativeRun.CustomMetrics.GetValueOrDefault("tank-max-window-percent"),
            RepresentativeWindowThresholdExceeded = batch.RepresentativeRun.CustomMetrics.GetValueOrDefault("tank-window-warnings"),
            RepresentativeBossThreat = batch.RepresentativeRun.CustomMetrics.GetValueOrDefault("boss-threat"),
            RepresentativeThreatPerSecond = batch.RepresentativeRun.CustomMetrics.GetValueOrDefault("boss-threat") /
                Math.Max(1m, configured.DurationSeconds),
            Preset = configured.Preset,
            Mode = request.TankMode ? "Level-60 Warrior incoming-combat / tank preview" : "Level-60 Warrior DPS alpha",
            EquipmentCatalogVersion = request.EquipmentMode == "catalog" ? ForeverEquipmentCatalog.GetCatalog().CatalogVersion : null,
            ResolvedMainHandWeapon = request.EquipmentMode == "catalog"
                ? $"{configured.Gear.Single(x => string.Equals(x.Slot, "main-hand", StringComparison.OrdinalIgnoreCase)).Name}: {configured.MainHandMinimumDamage}–{configured.MainHandMaximumDamage} at {configured.MainHandSpeedSeconds:0.00}s"
                : null,
            ResolvedOffHandWeapon = request.EquipmentMode == "catalog" && configured.DualWield
                ? $"{configured.Gear.Single(x => string.Equals(x.Slot, "off-hand", StringComparison.OrdinalIgnoreCase)).Name}: {configured.OffHandMinimumDamage}–{configured.OffHandMaximumDamage} at {configured.OffHandSpeedSeconds:0.00}s"
                : null,
            SupportCatalogVersion = ForeverSupportEffectCatalog.GetCatalog().CatalogVersion,
            ActiveSupportEffects = support.SelectedEffects.Select(x => x.Name)
                .Concat(configured.EnableWindfuryTotem ? new[] { "Windfury Totem (CP130 reactive)" } : Array.Empty<string>())
                .Concat(configured.MaintenancePolicies.Select(x => x.AbilityKey + " (assigned upkeep)"))
                .ToList(),
            EffectiveBossArmor = EffectiveArmor(configured, support),
            MeanDps = batch.Summary.DamagePerSecond.Mean,
            MedianDps = batch.Summary.DamagePerSecond.Median,
            EffectiveCharacterStats = BuildReportedStats(configured, effectiveStats),
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
                    ? "MAINTENANCE: Shouts/debuffs spend Rage/GCD; Sunder changes armor, Battle Shout changes AP, and maintained Thunder Clap slows subsequent boss swings by 20%. Thunder's direct damage, Demo's AP-to-damage conversion, hit checks, and party-wide aura propagation remain unsupported."
                    : "SUPPORT ALPHA: Unassigned selected buffs/debuffs are pre-applied at full uptime; select a maintenance assignment to model their cast costs.",
                request.EnableStanceDancing
                    ? $"STANCE DANCING: Optional Fury Overpower and Arms Battle-only actions use stance swaps (DPS mode). Stances have a separate {request.StanceSwitchGlobalCooldownSeconds:0.##}s cooldown and 10 + 3/rank Rage retention. Tank preview instead starts Defensive."
                    : "STANCE DANCING: DPS Arms uses Berserker and Battle-only dances; tank preview starts Defensive; Fury's optional Overpower dance is disabled.",
                "MAJOR COOLDOWN GCD: Death Wish and Recklessness use a 1.5s GCD. Blood Fury and Berserking are currently modeled as off-GCD.",
                "MAJOR COOLDOWNS: Explicit schedules and final-window holds are supported. Temporary haste and incoming-damage/Fear-related side effects are still partially modeled.",
                "WEAPONMASTER: Matching Axe/Polearm weapons grant crit, single Mace/Staff armor ignore, and Sword weapons can trigger seeded extra attacks. Mixed-weapon per-hit armor/crit still needs refinement; sword extra-attacks use the same normalized weapon Rage generation as basic attacks.",
                "ARMS STANCE POLICY: DPS Arms prefers Berserker and dances to Battle for eligible priority skills; the tank preview stays Defensive. Berserker's +10% damage taken now applies when enemy attacks are enabled.",
                "FLURRY: Melee crits grant three faster swings (5% per rank); active MH/OH timers are proportionally rescheduled. Flurry and Berserking haste combine additively in this beta preview.",
                request.AdditionalTargets.Count > 0 ? "MULTI-TARGET: Scripted boss adds are separate live actors, with their own armor and avoidance. Adds spawn/despawn by time; damage to adds counts toward total encounter DPS. Whirlwind uses its existing four-target effect and Cleave uses existing nearby-target links. Adds do not yet use a health-based death model." : "SINGLE-TARGET: No scripted adds configured.",
                "SWEEPING STRIKES: Five 20s charges, 30s cooldown and 30 Rage cost, Battle-only. Landed melee strikes echo resolved damage onto one nearby enemy. Initial model does not roll secondary armor/hit separately, and excludes Whirlwind to avoid uncertain proc chaining.",
                configured.EnableBossAutoAttacks
                    ? $"BOSS MELEE: Fixed-duration solo analysis scores all incoming damage, including strikes beyond maximum health. Dodge, Parry, Block, stance mitigation, Rage and optional parry haste are active. Boss parry haste: {configured.BossParryHaste}; player parry haste: {configured.PlayerParryHaste}. Configured boss specials use class-independent encounter AI; no true healers or movement AI yet."
                    : "BOSS MELEE: Disabled (legacy nonattacking training target). Incoming reaction talents cannot trigger.",
                "THREAT: Defensive Stance base 1.3x and shield-only Defiance (+5% per rank multiplicative) are modeled. Revenge and Shield Slam high-threat bonuses currently need exact Forever server values. Taunt, healer threat and aggro swaps are not modeled.",
                configured.EnableBossAutoAttacks
                    ? $"FIXED-DURATION SOLO TANK ANALYSIS: Incoming hits do not drain the Warrior's finite HP. DTPS and max {configured.TankDamageWindowSeconds:0.##}s burst / max health are meaningful; conventional death rate is intentionally not interpreted as a survival chance. Threshold: {configured.TankDamageWarningPercent:0.##}% HP. A real healer model is separate future work."
                    : "NO INCOMING ATTACKS: The DPS encounter continues through its configured duration.",
                "PROTECTION: Shield Slam base range is 421–439 plus Block Value (Forever client). Extra threat and 50% magic dispel are still not calibrated; CC, interrupt/silence and Charge interactions with boss casts are not yet wired.",
                "BOSS ABILITIES CP123: Reusable cast, phase and source-health gates; interrupts can cancel interruptible casts by combat event. Warrior assigned major cooldowns can respond to boss cast telegraphs. Area exposure remains static; interrupts are not yet automatically cast by a player rotation, and specials have no physical hit table. Solo damage is scored without draining HP.",
                "TWO-HANDED AND BASTION: Static damage contributions now affect actual resolved damage without corrupting the base weapon snapshot. Two-handed damage currently uses a generic physical modifier, and shield recognition is a preview; exact per-attack behavior remains partial.",
                "GORE DRINKER: Its next three landed melee attacks heal the Warrior after eligible activations. In a DPS encounter without boss attacks, a full-health Warrior receives no effective healing.",
                configured.EnableWindfuryTotem
                    ? "WINDFURY TOTEM (CP130): 20% seeded main-hand landed-attack proc, one extra melee roll with level-60 bonus AP and normalized Rage. Baseline coefficients and proc eligibility are configurable; no party range or air-totem conflict simulation in this solo preview."
                    : "WINDFURY TOTEM: Not enabled. To compare a party with a Shaman, enable it under Warrior support.",
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
                    : "Frontal attacks allow parry/block. Optional boss parry haste can advance the enemy's next swing.",
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
                configured.TankStatMode == "derived"
                    ? "TANK DEFENSE: Gear/rating/buff/talent-derived avoidance and block; baseline avoidance and Agility->Dodge remain explicit unverified calibrations; defense adjustment occurs only during incoming rolls."
                    : "TANK DEFENSE: Legacy manual dodge/parry/block values retained for saved CP122 comparisons.",
                "Optional Slam delays the main-hand swing; Cleave is allowed but a one-target encounter provides no secondary target.",
                "The training target has an effectively inexhaustible actual HP pool and the simulation always runs for the selected duration."
            ]
        };
    }

    /// <summary>Run both active classes in the same event engine. The original solo
    /// paths and all their result semantics are unchanged.</summary>
    public WarriorPriestIntegratedResponse RunIntegrated(
        WarriorPriestIntegratedRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Warrior);
        ArgumentNullException.ThrowIfNull(request.Priest);
        if (!request.Warrior.TankMode || request.Warrior.Preset != "protection" ||
            !request.Warrior.EnableBossAutoAttacks || !request.Warrior.ShieldEquipped)
            throw new ArgumentException("CP128 requires a shield-equipped Protection Warrior with boss melee enabled.");
        if (!request.Priest.EnableExtendedPriest || !request.Priest.EnableHealer ||
            !request.Priest.UseCharacterDerivedPriestStats ||
            !request.Priest.UsePriestTalentBuild ||
            !request.Priest.UseSpecializationHealingPolicy ||
            !request.Priest.UseAdaptiveHealingPolicy)
            throw new ArgumentException("CP128 requires the CP127.3 extended, derived-stat, talent-validated specialization healer mode.");
        if (request.Warrior.DurationSeconds != request.Priest.DurationSeconds)
            throw new ArgumentException("Warrior and Priest durations must match in the shared encounter.");
        Validate(request.Warrior);
        request.Priest.PriestTalents.Validate(request.Priest.PriestCharacter?.Level ?? 60);
        if (request.Priest.PriestCharacter is null ||
            request.Priest.PriestCharacter.ClassKey != "priest" ||
            request.Priest.PriestCharacter.Level != 60)
            throw new ArgumentException("A valid level-60 Priest CharacterProfile is required.");
        var resolved = request.Warrior.EquipmentMode == "catalog"
            ? ForeverEquipmentCatalog.ResolveWarriorLoadout(request.Warrior)
            : request.Warrior;
        var configured = PrepareCharacter(resolved);
        WarriorRuntimePolicyValidator.Validate(configured);
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
        var effectiveStats = WarriorGearStatCalculator.Calculate(supportRequest).EffectiveStats;
        if (configured.StatSourceMode == "race-base")
        {
            var stamina = effectiveStats.Get("stamina");
            configured.MaximumHealth = 1689m + Math.Min(20m, stamina) +
                Math.Max(0m, stamina - 20m) * 10m;
        }
        var integration = new PriestCombatIntegration(request.Priest);
        var run = RunOne(configured, effectiveStats, support,
            request.Warrior.Seed, request.Warrior.CaptureRepresentativeTimeline,
            cancellationToken, integration);
        var actors = run.Summary.ActorSummaries;
        var warriorSummary = actors["warrior"];
        var priestSummary = actors["priest"];
        var bossSummary = actors["target"];
        return new WarriorPriestIntegratedResponse
        {
            Run = run,
            TankSurvived = !warriorSummary.Died,
            TankDeathTimeSeconds = warriorSummary.DeathTimeSeconds,
            TankEndingHealth = run.CustomMetrics.GetValueOrDefault("warrior-health-remaining"),
            TankMaximumHealth = run.CustomMetrics.GetValueOrDefault("warrior-health-maximum"),
            TankDamageTaken = warriorSummary.DamageTaken,
            TankBlockedDamage = warriorSummary.BlockedDamageReceived,
            TankThreat = run.CustomMetrics.GetValueOrDefault("boss-threat"),
            TankDamageDone = warriorSummary.DamageDone,
            BossMeleeDamageDone = bossSummary.DamageDoneByAbility.GetValueOrDefault("boss-main-hand-auto"),
            PriestDied = priestSummary.Died,
            PriestHealingDone = priestSummary.HealingDone,
            PriestOverhealingDone = priestSummary.OverhealingDone,
            PriestAbsorptionDone = priestSummary.AbsorptionDone,
            PriestThreat = run.CustomMetrics.GetValueOrDefault("priest-threat"),
            PriestEndingMana = run.CustomMetrics.GetValueOrDefault("priest-mana-remaining"),
            PriestMaximumMana = run.CustomMetrics.GetValueOrDefault("priest-mana-maximum"),
            PriestSpellCasts = priestSummary.Abilities.ToDictionary(x => x.Key,
                x => x.Value.CastCompletedCount, StringComparer.OrdinalIgnoreCase),
            RaidMemberDeaths = actors.Count(x => x.Key.StartsWith("raid-", StringComparison.OrdinalIgnoreCase)
                && x.Value.Died),
            ModelingNotes =
            [
                "Real HP and threat targeting are enabled: tank death, healer aggro and wipes can occur.",
                "Protection Warrior executes the existing CP123-127 rotation/defenses. Priest uses CP127.3 policies/spellbook.",
                "Warrior gear/rating and Priest spell coefficients still use documented provisional Forever assumptions.",
                "Single-run integrated scenario; full raid roster, multiple healers, tank swaps and healing optimization are later work."
            ]
        };
    }

    private static Dictionary<string, decimal> BuildReportedStats(WarriorSimRequest request, StatCollection stats)
    {
        var values = new Dictionary<string, decimal>(stats.Values);
        if (!request.EnableBossAutoAttacks) return values;
        // Mirror the actual actor after defensive setup; no double defense-skill conversion.
        var actor = CreateWarrior(request, request.Preset == "arms", stats);
        foreach (var key in new[]
        {
            ForeverCombatStatKeys.DodgeChancePercent,
            ForeverCombatStatKeys.ParryChancePercent,
            ForeverCombatStatKeys.BlockChancePercent,
            ForeverCombatStatKeys.BlockValue,
            ForeverCombatStatKeys.DefenseSkill
        })
            values[key] = actor.Stats.Get(key);
        return values;
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
        ValidateAdditionalTargets(request);
        if (request.BossAbilities is null || request.BossAbilities.Count > 20)
            throw new ArgumentException("BossAbilities must contain at most 20 abilities.");
        if (request.EncounterPhases is null || request.EncounterPhases.Count > 12 ||
            request.EncounterPhases.Any(p => p is null || string.IsNullOrWhiteSpace(p.Key) ||
                p.StartTimeSeconds < 0m || p.StartTimeSeconds >= request.DurationSeconds ||
                (p.EndTimeSeconds.HasValue && (p.EndTimeSeconds <= p.StartTimeSeconds ||
                    p.EndTimeSeconds > request.DurationSeconds))) ||
            request.EncounterPhases.Select(p => p.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
                request.EncounterPhases.Count)
            throw new ArgumentException("EncounterPhases must have unique keys and valid time windows.");
        if (request.BossAbilities.Any(ability => !string.Equals(ability.SourceActorKey, "target", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Warrior preview boss abilities must use sourceActorKey 'target'.");
        if (request.TankMode && !request.EnableBossAutoAttacks)
            throw new ArgumentException("TankMode requires EnableBossAutoAttacks.");
        if (request.TankStatMode is not ("manual" or "derived"))
            throw new ArgumentException("TankStatMode must be 'manual' or 'derived'.");
        CheckPercent(request.BaseDodgePercent, "BaseDodgePercent");
        CheckPercent(request.BaseParryPercent, "BaseParryPercent");
        CheckPercent(request.BaseBlockPercent, "BaseBlockPercent");
        if (request.ShieldIntrinsicBlockValue < 0m)
            throw new ArgumentException("ShieldIntrinsicBlockValue must be nonnegative.");
        if (request.ProvisionalDodgePercentPerAgility is < 0m or > 10m)
            throw new ArgumentException("ProvisionalDodgePercentPerAgility must be 0..10.");
        if (request.EnableBossAutoAttacks)
        {
            if (request.BossSwingSpeedSeconds < 0.5m || request.BossSwingSpeedSeconds > 10m ||
                request.BossFirstSwingSeconds < 0m || request.BossFirstSwingSeconds >= request.DurationSeconds ||
                request.BossSwingMinimumDamage < 0m ||
                request.BossSwingMaximumDamage < request.BossSwingMinimumDamage ||
                request.PlayerBlockValue < 0m ||
                request.IncomingRageExpectedCreatureHealth <= 0m)
                throw new ArgumentException("Boss swing speed, first swing, damage, block value or incoming-Rage reference health is invalid.");
            CheckPercent(request.BossMeleeCritPercent, "BossMeleeCritPercent");
            CheckPercent(request.BossMeleeCrushPercent, "BossMeleeCrushPercent");
            CheckPercent(request.PlayerDodgePercent, "PlayerDodgePercent");
            CheckPercent(request.PlayerParryPercent, "PlayerParryPercent");
            CheckPercent(request.PlayerBlockPercent, "PlayerBlockPercent");
            CheckPercent(request.IncomingRageExpectedArmorReductionPercent,
                "IncomingRageExpectedArmorReductionPercent");
            if (request.TankStatMode == "manual" &&
                request.PlayerDodgePercent + request.PlayerParryPercent + request.PlayerBlockPercent > 100m)
                throw new ArgumentException("Total player avoidance/block chance must not exceed 100%.");
        }
        var arms = string.Equals(request.Preset, "arms", StringComparison.OrdinalIgnoreCase);
        var fury = string.Equals(request.Preset, "fury", StringComparison.OrdinalIgnoreCase);
        var protection = string.Equals(request.Preset, "protection", StringComparison.OrdinalIgnoreCase);
        if (!arms && !fury && !protection)
            throw new ArgumentException("Preset must be 'arms', 'fury', or 'protection'.");
        if (protection && !request.TankMode)
            throw new ArgumentException("Protection preset requires TankMode with incoming boss attacks.");
        if (protection && !request.ShieldEquipped)
            throw new ArgumentException("Protection preset requires an equipped shield.");
        if (request.TankDamageWindowSeconds is <= 0m or > 30m ||
            request.TankDamageWarningPercent is <= 0m or > 1000m)
            throw new ArgumentException("Tank window must be 0–30s and warning percent greater than 0.");
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
        if (request.ShieldEquipped && (request.DualWield || request.IsTwoHanded))
            throw new ArgumentException("A shield requires a one-handed main-hand weapon and no off-hand weapon.");
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
        CheckPercent(request.WindfuryTotemProcChancePercent, "WindfuryTotemProcChancePercent");
        if (request.WindfuryTotemBonusAttackPower < 0m ||
            request.WindfuryTotemInternalCooldownSeconds < 0m)
            throw new ArgumentException("Windfury Totem bonus AP/cooldown cannot be negative.");
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

    private static void ValidateAdditionalTargets(WarriorSimRequest request)
    {
        if (request.AdditionalTargets is null || request.AdditionalTargets.Count > 3)
            throw new ArgumentException("AdditionalTargets supports at most three adds.");
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "warrior", "target" };
        foreach (var add in request.AdditionalTargets)
        {
            if (add is null || string.IsNullOrWhiteSpace(add.Key) ||
                !System.Text.RegularExpressions.Regex.IsMatch(add.Key, "^[a-z][a-z0-9-]{0,31}$") ||
                !keys.Add(add.Key) || add.SpawnTimeSeconds < 0m ||
                add.SpawnTimeSeconds >= request.DurationSeconds ||
                add.DespawnTimeSeconds <= add.SpawnTimeSeconds ||
                add.DespawnTimeSeconds > request.DurationSeconds ||
                add.Level is < 1 or > 100 || add.Armor < 0m ||
                add.DodgePercent is < 0m or > 100m ||
                add.ParryPercent is < 0m or > 100m ||
                add.BlockPercent is < 0m or > 100m)
                throw new ArgumentException("Invalid additional target: require a distinct lowercase key, in-fight window, level, armor and 0–100% avoidance.");
        }
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

    private static SimulationRunResult RunOne(WarriorSimRequest request, StatCollection effectiveStats, ForeverSupportPlan support, int seed, bool captureTimeline, CancellationToken cancellationToken,
        PriestCombatIntegration? priestIntegration = null)
    {
        var arms = string.Equals(request.Preset, "arms", StringComparison.OrdinalIgnoreCase);
        var encounter = new EncounterProfile
        {
            Name = "Warrior Multi-Target DPS Preview",
            RulesetKey = "wow-forever",
            DurationSeconds = request.DurationSeconds,
            BossAbilities = request.BossAbilities,
            Phases = request.EncounterPhases
        };
        // Actors are materialized directly below; EncounterProfile.Targets is
        // deliberately empty until generic encounter materialization is supported.
        // Cleave picks the first additional live enemy linked to its current
        // target. The entire configured pack shares melee-range positioning.
        var targetsInCleaveRange = request.AdditionalTargets.Where(a => a.InCleaveRange)
            .Select(a => a.Key).Prepend(TargetKey).ToArray();
        for (var i = 0; i < targetsInCleaveRange.Length; i++)
            for (var j = i + 1; j < targetsInCleaveRange.Length; j++)
                encounter.TargetProximityLinks.Add(new TargetProximityLink
                {
                    TargetAKey = targetsInCleaveRange[i], TargetBKey = targetsInCleaveRange[j], InCleaveRange = true
                });
        var context = new SimulationContext(new SimulationRunOptions
        {
            Seed = seed, DurationSeconds = request.DurationSeconds,
            PrimaryActorKey = WarriorKey, CaptureTimeline = captureTimeline,
            // Guard a single-target DPS preview against runaway event queues.
            // Multi-actor raid runners can configure their own higher budget.
            MaximumProcessedEvents = priestIntegration is null ? 20_000 : 50_000, CancellationToken = cancellationToken
        }, encounter);
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
        if (priestIntegration is not null) priestIntegration.CreateActors(context);
        foreach (var add in request.AdditionalTargets)
        {
            var actor = new SimulationActorState
            {
                Key = add.Key, Name = add.Name, TeamKey = "enemy", Level = add.Level
            };
            actor.InitializeHealth(WarriorAddLifecycleProcessor.PreviewHealth,
                add.SpawnTimeSeconds == 0m ? null : 0m);
            actor.Stats.Set(ForeverCombatStatKeys.Armor, add.Armor);
            actor.Stats.Set(ForeverCombatStatKeys.DodgeChancePercent, add.DodgePercent);
            actor.Stats.Set(ForeverCombatStatKeys.ParryChancePercent, add.ParryPercent);
            actor.Stats.Set(ForeverCombatStatKeys.BlockChancePercent, add.BlockPercent);
            context.AddActor(actor);
        }
        context.SetScriptedTargetHealth(TargetKey, healthTimeline);

        var ruleset = ForeverCombatRulesetFactory.Create();
        if (request.EnableBossAutoAttacks)
        {
            var creature = ruleset.RollRules[ForeverCombatResolutionTypes.CreatureMeleeAuto];
            creature.BaseCriticalChancePercent = request.BossMeleeCritPercent;
            creature.BaseCrushingChancePercent = request.BossMeleeCrushPercent;
        }
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
        var absorbs = new AbsorbManager();
        ICombatRollResolver combatRolls = new RulesetCombatRollResolver(ruleset,
            new ForeverCombatRollContextAdjustmentProvider());
        if (priestIntegration is not null)
            combatRolls = new PriestHealingRollResolver(combatRolls,
                priestIntegration.HealingCritPercent);
        var executor = new AbilityExecutor(auraManager, combatRolls,
            new RulesetDamageMitigationResolver(ruleset),
            absorbManager: absorbs,
            abilityTimingProvider: new ForeverAbilityTimingProvider());
        var autoProcessor = new AutoAttackProcessor(executor, new ForeverAutoAttackTimingProvider());
        var mainHand = CreateMainHandAuto(request);
        AutoAttackDefinition? offHand = request.DualWield ? CreateOffHandAuto(request) : null;
        var weaponTalents = new WarriorWeaponTalentProcessor(request, autoProcessor, executor, mainHand, offHand);
        var windfuryTotem = request.EnableWindfuryTotem
            ? new TriggeredMeleeExtraAttackProcessor(executor, WarriorKey, mainHand,
                "windfury-totem-extra", request.WindfuryTotemProcChancePercent,
                request.WindfuryTotemBonusAttackPower, 1,
                request.WindfuryTotemInternalCooldownSeconds,
                grantsAutoAttackResources: true,
                // Provisional Forever interpretation: main-hand melee specials can
                // trigger Windfury as they do in Classic. Bloodthirst's damage
                // has no WeaponHandKey, so explicitly opt it in without changing
                // its other proc eligibility or damage semantics.
                additionalMainHandSpecialAbilityKeys: ["bloodthirst"])
            : null;
        var rotation = BuildRotation(request, arms);
        var runtimeProcessor = new WarriorRuntimeActionProcessor(
            executor, auraManager, request, warrior, target, autoProcessor);
        var bossProcessor = new WarriorBossAttackProcessor(request,
            new RulesetCombatRollResolver(ruleset, new ForeverCombatRollContextAdjustmentProvider()),
            new RulesetDamageMitigationResolver(ruleset),
            at => runtimeProcessor.IsMaintainedAt("thunder-clap", at),
            useRealHealth: priestIntegration is not null,
            selectByThreat: priestIntegration is not null,
            absorbs: priestIntegration is null ? null : absorbs);
        var retainedRage = 10m + (request.TalentMode == "tree"
            ? ForeverWarriorTalentCatalog.Rank(request, "improved-tactical-mastery") * 3m : 0m);
        var furyDance = !arms && request.EnableStanceDancing &&
            (request.RotationMode != "custom" || request.CustomRotation.Any(rule =>
                rule.Enabled && string.Equals(rule.Action, "overpower", StringComparison.OrdinalIgnoreCase)));
        ICombatEventProcessor[] danceProcessors = !request.TankMode && arms && request.Level >= 30
            ? [new WarriorArmsStancePriorityProcessor(executor, rotation, retainedRage)]
            : !request.TankMode && furyDance
                ? [new WarriorStanceDanceProcessor(executor,
                    request.MaximumRageForOverpowerDance, retainedRage)]
                : [];
        var protectionProcessor = new WarriorProtectionProcessor(request);
        var spikeProcessor = new WarriorTankSpikeProcessor(
            request.TankDamageWindowSeconds, request.TankDamageWarningPercent);
        var items = new CombatUseItemProcessor(request.CombatItems, executor, warrior, TargetKey, "warrior");
        var rotationProcessor = new PriorityRotationExecutor(rotation,
            WarriorKey, TargetKey, executor, autoAttackProcessor: autoProcessor);
        var healerProcessors = priestIntegration?.BuildProcessors(context, executor,
            auraManager, absorbs) ?? [];
        var engine = new SimulationEngine(
        [
            auraManager, absorbs, executor, autoProcessor,
            weaponTalents,
            ..(windfuryTotem is null ? Array.Empty<ICombatEventProcessor>() : new ICombatEventProcessor[] { windfuryTotem }),
            new WarriorAddLifecycleProcessor(),
            new WarriorSweepingStrikesProcessor(),
            bossProcessor,
            new EncounterTimelineProcessor(),
            new EncounterBossAbilityProcessor(auraManager),
            new ScriptedEncounterDamageProcessor(new RulesetDamageMitigationResolver(ruleset),
                absorbManager: absorbs,
                preserveHealth: actor => priestIntegration is null && actor.Key == WarriorKey,
                modifyRawDamage: (ctx, actor, ev, raw) =>
                {
                    if (actor.Key != WarriorKey || ev.SourceActorKey != TargetKey) return raw;
                    if (string.Equals(ev.SchoolKey, "physical", StringComparison.OrdinalIgnoreCase))
                    {
                        if (actor.ActiveAuras.Any(a => a.Definition.Key == ForeverWarriorAuraKeys.BerserkerStance &&
                            a.IsActiveAt(ctx.CurrentTimeSeconds))) raw *= 1.10m;
                        else if (actor.ActiveAuras.Any(a => a.Definition.Key == ForeverWarriorAuraKeys.DefensiveStance &&
                            a.IsActiveAt(ctx.CurrentTimeSeconds))) raw *= 0.90m;
                    }
                    if (actor.Stats.Get(WarriorProtectionProcessor.ShieldWallActiveStat) > 0m)
                        raw *= 0.40m;
                    return raw;
                }),
            new WarriorParryHasteProcessor(autoProcessor, request.PlayerParryHaste),
            protectionProcessor,
            spikeProcessor,
            new DamageTakenResourceGenerationProcessor(),
            new WarriorDamageThreatProcessor(request.EnableBossAutoAttacks, request),
            new WarriorIncomingTalentProcessor(request),
            new CriticalStrikeRollingDamageProcessor(auraManager),
            new ReactiveAbilityOpportunityProcessor(),
            ..danceProcessors,
            runtimeProcessor,
            items,
            new WarriorRageTalentProcessor(
                configuredRageTalentRank: request.TalentMode == "tree"
                    ? ForeverWarriorTalentCatalog.Rank(request, "unbridled-wrath") : 0,
                twoHandedWeapon: request.IsTwoHanded),
            rotationProcessor,
            ..healerProcessors
        ]);

        var completed = engine.Run(context, started =>
        {
            var initialStance = request.TankMode
                ? ForeverWarriorAuraKeys.DefensiveStance
                : request.Level >= 30
                    ? ForeverWarriorAuraKeys.BerserkerStance
                    : ForeverWarriorAuraKeys.BattleStance;
            auraManager.ApplyAura(started, warrior,
                new AuraDefinition
                {
                    Key = initialStance,
                    Name = request.TankMode ? "Defensive Stance" :
                        (request.Level >= 30 ? "Berserker Stance" : "Battle Stance"),
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
            if (request.EnableBossAutoAttacks) bossProcessor.ScheduleFirst(started);
            runtimeProcessor.ScheduleInitialDecisions(started);
            priestIntegration?.ScheduleFirst(started);
            foreach (var add in request.AdditionalTargets)
            {
                if (add.SpawnTimeSeconds > 0m)
                    started.ScheduleEvent(new CombatEvent
                    {
                        TimeSeconds = add.SpawnTimeSeconds, Type = CombatEventType.RotationDecision,
                        SourceActorKey = WarriorKey, TargetActorKey = add.Key,
                        EffectKey = WarriorAddLifecycleProcessor.SpawnEffect, IsInternal = true,
                        Description = $"{add.Name} spawns."
                    });
                if (add.DespawnTimeSeconds < request.DurationSeconds)
                    started.ScheduleEvent(new CombatEvent
                    {
                        TimeSeconds = add.DespawnTimeSeconds, Type = CombatEventType.RotationDecision,
                        SourceActorKey = WarriorKey, TargetActorKey = add.Key,
                        EffectKey = WarriorAddLifecycleProcessor.DespawnEffect, IsInternal = true,
                        Description = $"{add.Name} despawns."
                    });
            }
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
        completed.CustomMetrics["tank-max-window-damage"] = spikeProcessor.MaximumDamage;
        completed.CustomMetrics["tank-max-window-percent"] = spikeProcessor.MaximumHealthPercent;
        completed.CustomMetrics["tank-window-warnings"] = spikeProcessor.ThresholdCrossings;
        completed.CustomMetrics["boss-threat"] = target.ThreatTable.GetThreat(WarriorKey);
        completed.CustomMetrics["warrior-health-remaining"] = warrior.CurrentHealth;
        completed.CustomMetrics["warrior-health-maximum"] = warrior.MaximumHealth;
        if (priestIntegration is not null)
        {
            priestIntegration.Finish(context, completed);
            completed.CustomMetrics["priest-threat"] = target.ThreatTable.GetThreat("priest");
        }
        return completed;
    }

    private static SimulationActorState CreateWarrior(WarriorSimRequest request, bool arms, StatCollection effectiveStats)
    {
        var warrior = new SimulationActorState
        {
            Key = WarriorKey, Name = "Level 60 Warrior", TeamKey = "raid",
            AssignedRole = request.TankMode ? SimulationType.Tank : SimulationType.Dps, Level = request.Level
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
        if (request.EnableBossAutoAttacks)
        {
            ForeverWarriorDamageTakenRageFactory.Configure(warrior,
                request.IncomingRageExpectedCreatureHealth,
                request.IncomingRageExpectedArmorReductionPercent);
            var deflection = ForeverWarriorTalentCatalog.Rank(request, "deflection");
            var shieldSpec = ForeverWarriorTalentCatalog.Rank(request, "shield-specialization");
            var anticipation = 4m * ForeverWarriorTalentCatalog.Rank(request, "anticipation");
            if (request.TankStatMode == "derived")
            {
                // The catalog resolver supplies shield intrinsic value only in derived
                // mode; the stat pipeline supplies Strength, gear, enchants and buffs.
                var defense = ForeverTankDefenseCalculator.Calculate(effectiveStats,
                    request.ShieldEquipped, request.BaseDodgePercent,
                    request.BaseParryPercent, request.BaseBlockPercent,
                    request.ShieldIntrinsicBlockValue,
                    deflection, shieldSpec, anticipation,
                    request.ProvisionalDodgePercentPerAgility);
                warrior.Stats.Set(ForeverCombatStatKeys.DodgeChancePercent, defense.DodgePercent);
                warrior.Stats.Set(ForeverCombatStatKeys.ParryChancePercent, defense.ParryPercent);
                warrior.Stats.Set(ForeverCombatStatKeys.BlockChancePercent, defense.BlockPercent);
                warrior.Stats.Set(ForeverCombatStatKeys.BlockValue, defense.BlockValue);
                warrior.Stats.Set(ForeverCombatStatKeys.DefenseSkill,
                    request.Level * 5m + defense.BonusDefenseSkill);
            }
            else
            {
                // Preserves CP122 imports and A/B comparisons exactly.
                warrior.Stats.Set(ForeverCombatStatKeys.DodgeChancePercent, request.PlayerDodgePercent);
                warrior.Stats.Set(ForeverCombatStatKeys.ParryChancePercent,
                    request.PlayerParryPercent + deflection);
                warrior.Stats.Set(ForeverCombatStatKeys.BlockChancePercent,
                    request.ShieldEquipped ? request.PlayerBlockPercent + shieldSpec : 0m);
                warrior.Stats.Set(ForeverCombatStatKeys.BlockValue,
                    request.ShieldEquipped ? request.PlayerBlockValue : 0m);
                warrior.Stats.Set(ForeverCombatStatKeys.DefenseSkill,
                    request.Level * 5m + effectiveStats.Get(ForeverCombatStatKeys.DefenseSkill) + anticipation);
            }
        }
        if (request.TankMode)
            warrior.Stats.Add("physical-damage-bonus-percent", -10m);
        if (request.TalentMode == "tree")
        {
            var twoHandedRank = request.IsTwoHanded ? ForeverWarriorTalentCatalog.Rank(request, "two-handed-weapon-specialization") : 0;
            var hasShield = request.ShieldEquipped;
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
        if (request.Preset == "protection")
        {
            foreach (var ability in WarriorProtectionAbilityFactory.Create(request))
            {
                if (!warrior.Abilities.ContainsKey(ability.Key))
                    warrior.AddAbility(ability);
            }
            warrior.AddReactiveOpportunityDefinition(new ReactiveAbilityOpportunityDefinition
            {
                OpportunityKey = WarriorProtectionAbilityFactory.RevengeOpportunityKey,
                AbilityKey = "revenge", DodgeWindowSeconds = 5m
            });
        }
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
        else if (request.Preset == "fury")
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
        if (request.TalentMode == "tree" && arms &&
            ForeverWarriorTalentCatalog.Rank(request, "sweeping-strikes") > 0)
            warrior.AddAbility(new AbilityDefinition
            {
                Key = WarriorSweepingStrikesProcessor.AbilityKey,
                Name = "Sweeping Strikes", RequiredLevel = 30,
                IsOffGlobalCooldown = true, CooldownSeconds = 30m,
                GlobalCooldownSeconds = 0m,
                RequiredSourceAuraKeys = [ForeverWarriorAuraKeys.BattleStance],
                ResourceCosts = [new AbilityResourceCost { ResourceKey = "rage", Amount = 30m }]
            });
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
        // Oct 8 Forever beta: Dual Wield Specialization no longer grants
        // bonus off-hand Rage generation. Off-hand damage and hit effects are
        // modeled independently from the base swing's Rage resource stream.
        return definition;
    }

    private static RotationProfile BuildProtectionRotation(WarriorSimRequest request)
    {
        var result = new RotationProfile
        {
            Name = "Warrior Protection Starter", RulesetKey = "wow-forever",
            ClassKey = ForeverCharacterClassKeys.Warrior, SimulationType = SimulationType.Tank
        };
        var p = 0;
        // Shield Block is off-GCD but has its own cooldown/cost and two charges.
        result.Entries.Add(new RotationEntry { Priority = ++p, AbilityKey = "shield-block" });
        if (request.TalentMode == "tree" && ForeverWarriorTalentCatalog.Rank(request, "shield-slam") > 0)
            result.Entries.Add(new RotationEntry { Priority = ++p, AbilityKey = "shield-slam" });
        result.Entries.Add(new RotationEntry { Priority = ++p, AbilityKey = "revenge" });
        if (request.UseBloodrage)
            result.Entries.Add(new RotationEntry
            {
                Priority = ++p, AbilityKey = "bloodrage",
                Conditions = [new RotationConditionDefinition
                {
                    ConditionType = RotationConditionTypes.SourceResourceCurrent, Key = "rage",
                    ComparisonOperator = RotationComparisonOperators.LessThanOrEqual, Value = 30m
                }]
            });
        if (request.UseHeroicStrike)
            result.Entries.Add(new RotationEntry
            {
                Priority = ++p, ActionType = RotationActionTypes.QueueNextSwingReplacement,
                NextSwingReplacementKey = "heroic-strike", AutoAttackKey = MainHandAutoKey,
                Conditions = [new RotationConditionDefinition
                {
                    ConditionType = RotationConditionTypes.SourceResourceCurrent, Key = "rage",
                    ComparisonOperator = RotationComparisonOperators.GreaterThanOrEqual, Value = 65m
                }]
            });
        return result;
    }

    private static RotationProfile BuildRotation(WarriorSimRequest request, bool arms)
    {
        if (request.RotationMode == "custom")
            return WarriorRotationRules.BuildCustom(request);
        if (request.Preset == "protection")
            return BuildProtectionRotation(request);

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
        if (arms && request.TalentMode == "tree" && request.AdditionalTargets.Count > 0 &&
            ForeverWarriorTalentCatalog.Rank(request, "sweeping-strikes") > 0)
            rotation.Entries.Add(new RotationEntry
            {
                Priority = ++priority, AbilityKey = WarriorSweepingStrikesProcessor.AbilityKey,
                Conditions = [new RotationConditionDefinition
                {
                    ConditionType = RotationConditionTypes.ActiveEnemyCount,
                    ComparisonOperator = RotationComparisonOperators.GreaterThanOrEqual, Value = 2m
                }]
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
        if (request.UseCleave)
            rotation.Entries.Add(new RotationEntry
            {
                Priority = ++priority,
                ActionType = RotationActionTypes.QueueNextSwingReplacement,
                NextSwingReplacementKey = ForeverCleaveFactory.AbilityKey,
                AutoAttackKey = MainHandAutoKey,
                Conditions =
                [
                    new RotationConditionDefinition
                    {
                        ConditionType = RotationConditionTypes.ActiveEnemyCount,
                        ComparisonOperator = RotationComparisonOperators.GreaterThanOrEqual, Value = 2m
                    },
                    new RotationConditionDefinition
                    {
                        ConditionType = RotationConditionTypes.SourceResourceCurrent,
                        Key = "rage", ComparisonOperator = RotationComparisonOperators.GreaterThanOrEqual,
                        Value = request.HeroicStrikeMinimumRage
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
