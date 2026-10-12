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

    public WarriorSimResponse Run(WarriorSimRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);
        // Resolve IDs once, prior to seeded iterations. All runs use the same
        // server-owned snapshot of stats, enchants and weapon characteristics.
        var configured = request.EquipmentMode == "catalog"
            ? ForeverEquipmentCatalog.ResolveWarriorLoadout(request)
            : request;
        var support = ForeverSupportEffectCatalog.Resolve(configured);
        var calculated = WarriorGearStatCalculator.Calculate(configured);
        var effectiveStats = calculated.EffectiveStats;
        var isArms = string.Equals(configured.Preset, "arms", StringComparison.OrdinalIgnoreCase);
        var batch = new SimulationBatchRunner().Run(
            new SimulationBatchOptions
            {
                Iterations = request.Iterations,
                BaseSeed = request.Seed,
                SimulationType = SimulationType.Dps
            },
            (seed, representative) => RunOne(
                configured, effectiveStats, support, seed, representative && configured.CaptureRepresentativeTimeline));

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
            ActiveSupportEffects = support.SelectedEffects.Select(x => x.Name).ToList(),
            EffectiveBossArmor = Math.Max(0m, request.TargetArmor - support.TotalArmorReduction),
            MeanDps = batch.Summary.DamagePerSecond.Mean,
            MedianDps = batch.Summary.DamagePerSecond.Median,
            EffectiveCharacterStats = new Dictionary<string, decimal>(effectiveStats.Values),
            StatContributions = calculated.StatContributions,
            Batch = batch,
            ModelingNotes =
            [
                "SUPPORT ALPHA: Selected raid buffs and consumables are pre-applied at full uptime. Boss armor debuffs start fully stacked. No application GCD/Rage, refresh cost, expiry, positioning or party-composition checks are simulated.",
                "Only the modeled static bonuses listed in the support catalog are counted; unknown and proc-based effects are rejected.",
                request.EquipmentMode == "catalog"
                    ? "CATALOG: Item IDs resolve to a curated Forever beta snapshot. MH/OH damage and speed come from equipment; recognized static item/enchant/set bonuses flow into effective stats. Other item effects, procs, stamina-to-HP, and unlisted items are NOT modeled. AP/crit/hit/haste inputs exclude gear."
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
                "The target health percent is scripted by time for this single-character sim. Default execute is the final configured seconds; custom milestones replace that schedule.",
                "Deep Wounds currently uses the configurable 2-second tick cadence assumption.",
                "Optional Slam delays the main-hand swing; Cleave is allowed but a one-target encounter provides no secondary target.",
                "The training target has an effectively inexhaustible actual HP pool and the simulation always runs for the selected duration."
            ]
        };
    }

    public static void Validate(WarriorSimRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
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
        if (request.DurationSeconds is < 1m or > 600m)
            throw new ArgumentException("DurationSeconds must be from 1 through 600.");
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
        if (request.ImpaleRank > 0 && request.DeepWoundsRank != 3)
            throw new ArgumentException("Impale requires Deep Wounds rank 3.");
        if (fury && request.BloodthrillRank > 0)
            throw new ArgumentException("The Fury preset does not switch stances for Overpower; use Arms to model Bloodthrill.");
        // Validate the effective scripted phase schedule up front, before batch iteration.
        _ = BuildTargetHealthTimeline(request);
        WarriorGearStatCalculator.Validate(request);
        _ = ForeverSupportEffectCatalog.Resolve(request);
        WarriorRotationRules.Validate(request);
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

    private static SimulationRunResult RunOne(WarriorSimRequest request, StatCollection effectiveStats, ForeverSupportPlan support, int seed, bool captureTimeline)
    {
        var arms = string.Equals(request.Preset, "arms", StringComparison.OrdinalIgnoreCase);
        var context = new SimulationContext(new SimulationRunOptions
        {
            Seed = seed, DurationSeconds = request.DurationSeconds,
            PrimaryActorKey = WarriorKey, CaptureTimeline = captureTimeline
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
            Math.Max(0m, request.TargetArmor - support.TotalArmorReduction));
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
        var rotation = BuildRotation(request, arms);
        var rotationProcessor = new PriorityRotationExecutor(rotation,
            WarriorKey, TargetKey, executor, autoAttackProcessor: autoProcessor);
        var engine = new SimulationEngine(
        [
            auraManager, executor, autoProcessor,
            new CriticalStrikeRollingDamageProcessor(auraManager),
            new ReactiveAbilityOpportunityProcessor(),
            rotationProcessor
        ]);

        var mainHand = CreateMainHandAuto(request);
        AutoAttackDefinition? offHand = request.DualWield ? CreateOffHandAuto(request) : null;
        return engine.Run(context, started =>
        {
            auraManager.ApplyAura(started, warrior,
                new AuraDefinition
                {
                    Key = arms ? ForeverWarriorAuraKeys.BattleStance : ForeverWarriorAuraKeys.BerserkerStance,
                    Name = arms ? "Battle Stance" : "Berserker Stance",
                    DurationSeconds = request.DurationSeconds + 1m
                }, WarriorKey, null, null);
            if (!autoProcessor.Start(started, WarriorKey, TargetKey, mainHand, request.ImmediateOpeningSwing ? 0m : null))
                throw new InvalidOperationException("Could not start the main-hand attack stream.");
            if (offHand is not null && !autoProcessor.Start(started, WarriorKey, TargetKey, offHand, request.ImmediateOpeningSwing ? 0m : null))
                throw new InvalidOperationException("Could not start the off-hand attack stream.");
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
        if (request.UseBloodrage)
            warrior.AddAbility(ForeverBloodrageFactory.Create(request.MaximumHealth));
        if (arms)
        {
            var overpower = ForeverOverpowerFactory.CreateForLevel(request.Level,
                request.MainHandMinimumDamage, request.MainHandMaximumDamage,
                NormalizedSpeed(request), MainHandSkillKey,
                improvedOverpowerRank: request.ImprovedOverpowerRank,
                impaleRank: request.ImpaleRank, deepWoundsRank: request.DeepWoundsRank);
            warrior.AddAbility(overpower);
            warrior.AddReactiveOpportunityDefinition(
                ForeverOverpowerFactory.CreateOpportunity(request.BloodthrillRank));
            var mortalStrike = ForeverMortalStrikeFactory.CreateForLevel(request.Level,
                request.MainHandMinimumDamage, request.MainHandMaximumDamage,
                NormalizedSpeed(request), MainHandSkillKey);
            ForeverWarriorArmsTalentConfigurator.ApplyImpale(
                mortalStrike, request.ImpaleRank, request.DeepWoundsRank);
            warrior.AddAbility(mortalStrike);
            if (request.UseRend)
                warrior.AddAbility(ForeverRendFactory.CreateForLevel(request.Level,
                    request.RendTotalAttackPowerCoefficient, request.ImprovedRendRank));
        }
        else
        {
            var bloodthirst = ForeverBloodthirstFactory.CreateForLevel(request.Level, MainHandSkillKey);
            ForeverWarriorArmsTalentConfigurator.ApplyImpale(
                bloodthirst, request.ImpaleRank, request.DeepWoundsRank);
            warrior.AddAbility(bloodthirst);
            var whirlwind = request.DualWield
                ? ForeverWhirlwindFactory.CreateDualWield(
                    request.MainHandMinimumDamage, request.MainHandMaximumDamage,
                    NormalizedSpeed(request), MainHandSkillKey,
                    request.OffHandMinimumDamage, request.OffHandMaximumDamage,
                    ForeverNormalizedWeaponSpeeds.OneHanded, OffHandSkillKey)
                : ForeverWhirlwindFactory.CreateSingleWeapon(
                    request.MainHandMinimumDamage, request.MainHandMaximumDamage,
                    NormalizedSpeed(request), MainHandSkillKey);
            ForeverWarriorArmsTalentConfigurator.ApplyImpale(
                whirlwind, request.ImpaleRank, request.DeepWoundsRank);
            warrior.AddAbility(whirlwind);
        }
        if (request.UseSlam)
            warrior.AddAbility(ForeverSlamFactory.CreateForLevel(request.Level,
                request.MainHandMinimumDamage, request.MainHandMaximumDamage,
                request.MainHandSpeedSeconds, MainHandSkillKey));
        if (request.UseCleave)
            warrior.AddNextSwingReplacement(ForeverCleaveFactory.CreateForLevel(
                request.Level, request.MainHandMinimumDamage,
                request.MainHandMaximumDamage, request.MainHandSpeedSeconds,
                MainHandSkillKey));
        if (request.UseExecute)
        {
            var execute = ForeverExecuteFactory.CreateForLevel(request.Level, MainHandSkillKey);
            ForeverWarriorArmsTalentConfigurator.ApplyImpale(
                execute, request.ImpaleRank, request.DeepWoundsRank);
            warrior.AddAbility(execute);
        }
        if (request.UseHeroicStrike)
            warrior.AddNextSwingReplacement(ForeverHeroicStrikeFactory.CreateForLevel(
                request.Level, request.MainHandMinimumDamage,
                request.MainHandMaximumDamage, request.MainHandSpeedSeconds,
                MainHandSkillKey));
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

    private static AutoAttackDefinition CreateOffHandAuto(WarriorSimRequest request) =>
        ForeverWarriorAutoAttackFactory.CreateDualWieldOffHand(
            OffHandAutoKey, "Off-hand auto attack",
            request.OffHandMinimumDamage, request.OffHandMaximumDamage,
            request.OffHandSpeedSeconds, OffHandSkillKey);

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
