using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;
using WarcraftSim.Data.Forever.Druid;

namespace WarcraftSim.Api.Simulation;

public sealed class FeralDruidFormTransition
{
    public decimal AtSeconds { get; set; }
    // "cat", "bear" (Dire Bear), "caster", or "moonkin".
    public string Form { get; set; } = "cat";
}

public sealed class FeralDruidSimRequest
{
    public int Seed { get; set; } = 144001;
    public decimal DurationSeconds { get; set; } = 120m;
    public bool CaptureTimeline { get; set; }
    public string Role { get; set; } = "cat";
    public decimal MaximumHealth { get; set; } = 5500m;
    public decimal TargetMaximumHealth { get; set; } = 1000000m;
    public decimal MaximumMana { get; set; } = 4000m;
    // Optional preview positional and stealth conditions (not movement AI).
    public bool StartInProwl { get; set; }
    public string PreferredCatOpener { get; set; } = "ravage";
    public bool UseBearUtility { get; set; }
    // Optional shared Druid effects in the stationary boss test (no AI).
    public bool UseFaerieFire { get; set; }
    public bool UseThorns { get; set; }
    public decimal TargetArmor { get; set; } // zero preserves old no-armor previews
    public bool UseFeralCooldowns { get; set; }
    public bool TargetBehind { get; set; } = true;
    public decimal CompetingThreatOnBoss { get; set; }
    public int AdditionalTargets { get; set; }
    public List<FeralDruidFormTransition> FormTransitions { get; set; } = [];
    // Solo Bear tank mode. Boss hit events are scored without consuming player
    // health, matching the existing fixed-duration Protection Warrior approach.
    public bool EnableBossAttacks { get; set; }
    public decimal BossHitDamage { get; set; } = 850m;
    public decimal BossSwingSeconds { get; set; } = 2.5m;
    public decimal BearBaseArmor { get; set; } = 4000m;
    public decimal BearDodgePercent { get; set; } = 5m;
    // Blizzard confirms the incoming-Rage formula shape for both Warriors and
    // Druids; the exact level-60 expected health/Armor inputs are provisional.
    public decimal IncomingRageExpectedCreatureHealth { get; set; } = 10000m;
    public decimal IncomingRageExpectedArmorReductionPercent { get; set; } = 35m;
    public decimal TankBurstWindowSeconds { get; set; } = 6m;
    public ForeverFeralDruidTalents Talents { get; set; } = new();
    public ForeverFeralDruidTuning Tuning { get; set; } = new();
}
public sealed class FeralDruidSimResponse
{
    public decimal Dps { get; init; }
    public decimal DamageDone { get; init; }
    public decimal EndingMana { get; init; }
    public decimal EndingEnergy { get; init; }
    public decimal EndingRage { get; init; }
    public int ComboPoints { get; init; }
    public int LacerateStacks { get; init; }
    public int OmenProcs { get; init; }
    public int ClearcastsUsed { get; init; }
    public int FormChanges { get; init; }
    public decimal DamageTaken { get; init; }
    public decimal ArmorPreventedDamage { get; init; }
    public decimal DodgedIncomingHits { get; init; }
    public decimal MaximumBurstDamage { get; init; }
    public decimal MaximumBurstPercentOfHealth { get; init; }
    public decimal FrenziedRegenerationHealing { get; init; }
    public decimal ProjectedEndingHealth { get; init; }
    public decimal? ProjectedHealthZeroAtSeconds { get; init; }
    public decimal MinimumProjectedHealth { get; init; }
    public decimal EffectiveSelfHealing { get; init; }
    public decimal WastedSelfHealing { get; init; }
    public decimal ThreatGenerated { get; init; }
    public decimal ThreatPerSecond { get; init; }
    public Dictionary<string, decimal> DamageByAbility { get; init; } = new();
    public SimulationRunResult Run { get; init; } = new();
}
/// <summary>
/// Initial feral Cat / Dire Bear execution paths. Level-60 damage parameters and
/// proc rates are configurable previews, NOT verified game client coefficient data.
/// No physical mitigation or boss-attack model is claimed in this DPS training run.
/// </summary>
public sealed class FeralDruidSimRunner
{
    public FeralDruidSimResponse Run(FeralDruidSimRequest request, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Talents is null || request.Tuning is null ||
            request.Role is not ("cat" or "bear") || request.DurationSeconds is <= 0m or > 1200m ||
            request.MaximumHealth <= 0m || request.TargetMaximumHealth <= 0m ||
            request.MaximumMana <= 0m || request.AdditionalTargets is < 0 or > 8 ||
            request.PreferredCatOpener is not ("ravage" or "pounce") ||
            request.CompetingThreatOnBoss < 0m || request.TargetArmor < 0m || request.BossHitDamage < 0m || request.BossSwingSeconds <= 0m ||
            request.BearBaseArmor < 0m || request.BearDodgePercent is < 0m or > 100m ||
            request.IncomingRageExpectedCreatureHealth <= 0m ||
            request.IncomingRageExpectedArmorReductionPercent is <0m or >100m ||
            request.TankBurstWindowSeconds <= 0m ||
            request.FormTransitions is null ||
            request.FormTransitions.Any(x => x is null || x.AtSeconds < 6m ||
                x.AtSeconds >= request.DurationSeconds ||
                x.Form is not ("cat" or "bear" or "caster" or "moonkin")))
            throw new ArgumentException("Invalid Feral simulation request.");
        request.Talents.Validate(); request.Tuning.Validate();
        var cat = request.Role == "cat";
        var form = cat ? ForeverDruidForm.Cat : ForeverDruidForm.DireBear;
        var derived = ForeverDruidDerivedStats.HeartOfTheWild(request.Tuning,
            request.Talents.Rank("heart-of-the-wild"), form,
            request.MaximumHealth, request.MaximumMana);
        var effectiveTuning = request.Tuning.WithAttackPower(derived.AttackPower);
        var ctx = new SimulationContext(new SimulationRunOptions {
            Seed = request.Seed, DurationSeconds = request.DurationSeconds,
            PrimaryActorKey = "druid", CaptureTimeline = request.CaptureTimeline,
            MaximumProcessedEvents = 150000, CancellationToken = token
        });
        var druid = new SimulationActorState { Key = "druid", Name = "Feral Druid",
            TeamKey = "raid", Level = 60, MaximumHealth = derived.Health };
        druid.InitializeHealth(derived.Health);
        druid.AddResource(new ResourceState("mana", derived.Mana, derived.Mana));
        druid.AddResource(new ResourceState("energy", 100m, cat ? 0m : 0m));
        druid.AddResource(new ResourceState("rage", 100m, cat ? 0m : 20m));
        if(request.EnableBossAttacks)
            ForeverDruidDamageTakenRageFactory.Configure(druid,
                request.IncomingRageExpectedCreatureHealth,
                request.IncomingRageExpectedArmorReductionPercent);
        ctx.AddActor(druid);
        var target = new SimulationActorState { Key = "target", TeamKey = "enemy",
            Name = "Feral training boss", Level = 63, MaximumHealth = request.TargetMaximumHealth };
        target.InitializeHealth(target.MaximumHealth); ctx.AddActor(target);
        for (var i = 1; i <= request.AdditionalTargets; i++)
        {
            var add = new SimulationActorState { Key = $"target-{i}", TeamKey = "enemy",
                Name = "Training add", Level = 63, MaximumHealth = request.TargetMaximumHealth };
            add.InitializeHealth(add.MaximumHealth); ctx.AddActor(add);
        }
        foreach (var ability in ForeverFeralDruidSpellbook.Create(request.Talents, effectiveTuning))
            druid.AddAbility(ability);
        var rotation = new RotationProfile { Name = "Feral mechanics preview", ClassKey = "druid",
            RulesetKey = "wow-forever", SimulationType = SimulationType.Dps };
        var catPriorities = new[] { "ravage", "pounce", "shifting-power", "rip", "ferocious-bite", "rake", "shred", "claw" };
        // Primal Bite cleaves during Berserk. In the multi-target preset, Swipe
        // otherwise consumes the same Rage first on every decision, preventing
        // that talent interaction from ever reaching the secondary-hit processor.
        // Prefer the talent-granted attack when equipped; without it, retain
        // Swipe-first multi-target behavior.
        var bearPriorities = request.AdditionalTargets > 0
            ? request.Talents.Rank("primal-bite") > 0 && request.Talents.Rank("berserk") > 0
                ? new[] { "enrage", "primal-bite", "swipe", "lacerate", "maul" }
                : new[] { "enrage", "swipe", "primal-bite", "lacerate", "maul" }
            : new[] { "enrage", "primal-bite", "lacerate", "maul", "swipe" };
        var priority = request.FormTransitions.Count == 0
            ? (cat ? catPriorities : bearPriorities)
            : catPriorities.Concat(bearPriorities).ToArray();
        if (request.UseFeralCooldowns)
        {
            // In the multi-target Berserk preset, Primal Bite must have an
            // opportunity to spend the opening Bear Rage before Enrage's GCD
            // and Frenzied Regeneration's periodic Rage drain. Otherwise an
            // otherwise valid Berserk cleave never executes in this scenario.
            // Preserve the usual rotation for all other configurations.
            var prioritizeBerserkBite = !cat && request.FormTransitions.Count == 0 &&
                request.AdditionalTargets > 0 &&
                request.Talents.Rank("primal-bite") > 0 &&
                request.Talents.Rank("berserk") > 0;
            priority = prioritizeBerserkBite
                ? new[] { "berserk", "primal-bite", "frenzied-regeneration" }
                    .Concat(priority.Where(x => x != "primal-bite")).ToArray()
                : new[] { "berserk", "frenzied-regeneration" }.Concat(priority).ToArray();
        }
        if (request.UseFaerieFire)
            priority = new[] { "faerie-fire" }.Concat(priority).ToArray();
        if (request.UseBearUtility)
            priority = new[] { "growl", "challenging-roar", "feral-charge", "demoralizing-roar", "bash" }
                .Concat(priority).ToArray();
        var index = 0;
        foreach (var ability in priority.Where(x => druid.Abilities.ContainsKey(x)))
            rotation.Entries.Add(new RotationEntry { Priority = index++, AbilityKey = ability });
        var auras = new AuraManager();
        FeralDruidCombatProcessor? mechanics = null;
        var rolls = new FeralRolls(request, () => mechanics);
        var executor = new AbilityExecutor(auras, rolls,
            new FeralArmorMitigation(request.TargetArmor, effectiveTuning));
        var auto = new AutoAttackProcessor(executor);
        AutoAttackDefinition AttackForForm(ForeverDruidForm activeForm) => new() {
            Key = "feral-auto", Name = "Feral form swing", WeaponHandKey = WeaponHandKeys.MainHand,
            SwingIntervalSeconds = ForeverDruidFormState.SwingSeconds(activeForm),
            DamageEffect = new AbilityEffectDefinition {
                Key = "swing", EffectType = AbilityEffectTypes.DirectDamage,
                TargetType = AbilityTargetTypes.Enemy, SchoolKey = "physical",
                ResolutionType = CombatResolutionTypes.Melee, WeaponHandKey = WeaponHandKeys.MainHand,
                CanMiss = true, CanCrit = true,
                MinimumValue = ForeverDruidFormState.WeaponDamage(request.Tuning.WeaponDps, activeForm),
                MaximumValue = ForeverDruidFormState.WeaponDamage(request.Tuning.WeaponDps, activeForm)
            }
        };
        var formState = new ForeverDruidFormState(request.Talents.Rank("furor"), request.Tuning.ShapeshiftMana);
        var attacks = AttackForForm(form);
        var failedShifts = 0;
        void AttemptFormChange(SimulationContext started, ForeverDruidForm destination)
        {
            var previous = formState.Form;
            if (!formState.Shift(druid, destination, started.CurrentTimeSeconds))
            {
                failedShifts++;
                return;
            }
            auto.Stop(started, druid.Key, "feral-auto");
            if (destination != ForeverDruidForm.Cat)
                auras.RemoveAuras(started, druid, "druid-prowl", druid.Key,
                    "Prowl ended by shifting form");
            auras.RemoveAuras(started, druid, ForeverDruidFormState.AuraKey(previous),
                druid.Key, "shapeshift transition");
            auras.ApplyAura(started, druid, new AuraDefinition {
                Key = ForeverDruidFormState.AuraKey(destination),
                Name = $"{destination} Form", DurationSeconds = 3600m
            }, druid.Key, "shapeshift", "form-transition");
            if (ForeverDruidFormState.SwingSeconds(destination) > 0m)
            {
                // AutoAttackProcessor requires the same registered definition
                // reference for a given key. Mutate it only after stopping the
                // previous swing, then start a fresh instance with new timing.
                attacks.SwingIntervalSeconds = ForeverDruidFormState.SwingSeconds(destination);
                var formWeaponDamage = ForeverDruidFormState.WeaponDamage(
                    request.Tuning.WeaponDps, destination);
                attacks.DamageEffect.MinimumValue = formWeaponDamage;
                attacks.DamageEffect.MaximumValue = formWeaponDamage;
                if (!auto.Start(started, druid.Key, "target", attacks))
                    throw new InvalidOperationException("Could not start form-specific swing.");
            }
        }
        FeralDruidTankMetricsProcessor? projectedTank = null;
        var forcedTargets = new ForcedTargetManager();
        mechanics = new FeralDruidCombatProcessor(druid, request, rotation, formState, auras,
            AttemptFormChange, started => {
                // No ordinary melee swing is allowed to break the opener before
                // a Prowl-only ability starts. Resume the auto stream afterward.
                if (!auto.Start(started, druid.Key, "target", attacks))
                    throw new InvalidOperationException("Feral opener could not resume swings.");
            }, recovery => projectedTank?.ApplyRecovery(recovery) ?? 0m, forcedTargets);
        var decisions = new PriorityRotationExecutor(rotation, "druid", "target", executor, true);
        var swipeCleave = new FeralSwipeCleaveProcessor(druid.Key, executor);
        var thorns = new FeralThornsProcessor(druid.Key, request.UseThorns,
            request.Tuning.ThornsBaseDamage + request.Tuning.ThornsSpellPower * .06m);
        var tank = new FeralDruidTankMetricsProcessor("druid", "target",
            request.TankBurstWindowSeconds, () => formState.Form is
                ForeverDruidForm.Bear or ForeverDruidForm.DireBear,
            request.Tuning.BearBaseThreatMultiplier,request.Tuning.PrimalBiteThreatMultiplier,
            derived.Health);
        projectedTank = tank;
        decimal armorPreventedDamage = 0m;
        decimal dodgedHits = 0m;
        // Use the normal scripted-encounter mitigation resolver so timeline
        // RawAmount remains the boss's 850 (or configured) pre-mitigation hit.
        DamageMitigationResult ResolveIncoming(SimulationContext context, SimulationActorState defender,
            decimal incomingRaw)
        {
            if (defender.Key != druid.Key ||
                formState.Form is not (ForeverDruidForm.Bear or ForeverDruidForm.DireBear))
                return DamageMitigationResult.Unmitigated(incomingRaw);
            if ((decimal)context.Random.NextDouble() * 100m < request.BearDodgePercent +
                request.Talents.Rank("natural-reaction") * 2m +
                request.Talents.Rank("feral-swiftness") * 2m)
            {
                dodgedHits++;
                if (request.Talents.Rank("natural-reaction") > 0 &&
                    (decimal)context.Random.NextDouble() * 100m <
                    request.Talents.Rank("natural-reaction") * 20m)
                    druid.Resources["rage"].Gain(5m);
                return new DamageMitigationResult { RawAmount = incomingRaw, FinalAmount = 0m,
                    MitigatedAmount = incomingRaw, ReductionPercent = 100m };
            }
            var armor = request.BearBaseArmor + ForeverDruidDerivedStats.ThickHideBonusArmor(
                request.Talents.Rank("thick-hide"), druid.Level,
                request.Tuning.DefenseSkill, request.Tuning.ThickHideFormArmorMultiplier);
            var weakened = context.GetActor("target")?.ActiveAuras.Any(a =>
                a.Definition.Key == "druid-demoralizing-roar" &&
                a.IsActiveAt(context.CurrentTimeSeconds)) == true;
            var afterDebuff = weakened ? incomingRaw * .90m : incomingRaw;
            var incomingArmorFraction=ForeverArmorMitigationMath.ClampArmorReductionFraction(
                armor/(armor+request.Tuning.TankArmorReductionConstant));
            var reduced = afterDebuff * (1m-incomingArmorFraction);
            armorPreventedDamage += afterDebuff - reduced;
            return new DamageMitigationResult {
                RawAmount = incomingRaw, FinalAmount = reduced,
                MitigatedAmount = incomingRaw - reduced,
                ReductionPercent = incomingRaw > 0m ? (incomingRaw - reduced) * 100m / incomingRaw : 0m,
                DefenseValue = armor
            };
        }
        var incoming = new ScriptedEncounterDamageProcessor(
            new FeralIncomingBossMitigation(ResolveIncoming),
            preserveHealth: actor => request.EnableBossAttacks && actor.Key == druid.Key);
        var engine = new SimulationEngine([executor, auras, auto, incoming,
            new FeralBearIncomingRageProcessor(formState, druid.Key),
            mechanics,swipeCleave,tank,forcedTargets,thorns,decisions]);
        var run = engine.Run(ctx, started => {
            if (!formState.Shift(druid, form, started.CurrentTimeSeconds))
                throw new InvalidOperationException("Unable to shift into selected Feral form.");
            auras.ApplyAura(started, druid, new AuraDefinition { Key = ForeverDruidFormState.AuraKey(form),
                Name = $"{form} Form", DurationSeconds = 3600m }, druid.Key, "shapeshift", "form-state");
            if (request.CompetingThreatOnBoss > 0m)
                target.ThreatTable.SetThreat("competing-tank", request.CompetingThreatOnBoss);
            if (request.UseThorns)
                auras.ApplyAura(started, druid, new AuraDefinition {
                    Key = "druid-thorns", Name = "Thorns", DurationSeconds = 3600m
                }, druid.Key, "thorns", "precast-thorns");
            if (request.StartInProwl && cat)
                auras.ApplyAura(started, druid, new AuraDefinition {
                    Key = "druid-prowl", Name = "Prowl", DurationSeconds = 3600m
                }, druid.Key, "prowl", "opening-stealth");
            if (!(request.StartInProwl && cat) &&
                !auto.Start(started, "druid", "target", attacks))
                throw new InvalidOperationException("Feral auto attack stream failed to start.");
            if (request.EnableBossAttacks)
                for (var at = request.BossSwingSeconds; at < request.DurationSeconds;
                     at += request.BossSwingSeconds)
                    started.ScheduleEvent(new CombatEvent {
                        Type = CombatEventType.ScriptedDamage, TimeSeconds = at,
                        SourceActorKey = "target", TargetActorKey = "druid",
                        EncounterEventKey = "feral-boss-melee", SchoolKey = "physical",
                        MitigationType = DamageMitigationTypes.Armor,
                        RawAmount = request.BossHitDamage
                    });
            foreach (var change in request.FormTransitions)
                started.ScheduleEvent(new CombatEvent {
                    Type = CombatEventType.RotationDecision, TimeSeconds = change.AtSeconds,
                    SourceActorKey = druid.Key, TargetActorKey = druid.Key,
                    EffectKey = "feral-form-shift:" + change.Form, IsInternal = true
                });
        });
        var summary = run.Summary.ActorSummaries["druid"];
        run.CustomMetrics["feral-frenzied-regeneration-healing"] = mechanics.FrenziedRegenerationHealing;
        run.CustomMetrics["feral-combo-points"] = mechanics.ComboPoints;
        run.CustomMetrics["feral-lacerate-stacks"] = mechanics.LacerateStacks;
        run.CustomMetrics["feral-omen-procs"] = mechanics.OmenProcs;
        run.CustomMetrics["feral-clearcasts-used"] = mechanics.ClearcastsUsed;
        run.CustomMetrics["feral-failed-form-shifts"] = failedShifts;
        run.CustomMetrics["feral-heart-of-the-wild-attack-power"] = derived.AttackPower;
        run.CustomMetrics["feral-heart-of-the-wild-maximum-health"] = derived.Health;
        run.CustomMetrics["feral-heart-of-the-wild-maximum-mana"] = derived.Mana;
        run.CustomMetrics["bear-thick-hide-bonus-armor"] = ForeverDruidDerivedStats.ThickHideBonusArmor(
            request.Talents.Rank("thick-hide"), druid.Level, request.Tuning.DefenseSkill,
            request.Tuning.ThickHideFormArmorMultiplier);
        run.CustomMetrics["bear-incoming-damage"] = tank.TotalIncoming;
        run.CustomMetrics["bear-max-burst-damage"] = tank.MaximumBurst;
        run.CustomMetrics["feral-threat-generated"] = tank.Threat;
        run.CustomMetrics["bear-threat-generated"] = cat ? 0m : tank.Threat;
        run.CustomMetrics["bear-shared-threat-table"] = target.ThreatTable.GetThreat(druid.Key);
        run.CustomMetrics["bear-swipe-secondary-hits"] = swipeCleave.SecondaryHits;
        run.CustomMetrics["bear-armor-prevented-damage"] = armorPreventedDamage;
        run.CustomMetrics["bear-dodged-hits"] = dodgedHits;
        run.CustomMetrics["bear-projected-ending-health"] = tank.ProjectedHealth;
        run.CustomMetrics["bear-projected-minimum-health"] = tank.MinimumProjectedHealth;
        run.CustomMetrics["bear-projected-zero-at-seconds"] = tank.ProjectedHealthZeroAtSeconds ?? -1m;
        run.CustomMetrics["bear-projected-effective-self-healing"] = tank.EffectiveSelfHealing;
        run.CustomMetrics["bear-projected-overhealing"] = tank.WastedSelfHealing;
        run.CustomMetrics["bear-projected-health-damage"] = tank.TotalProjectedDamage;
        run.CustomMetrics["druid-thorns-hits"] = thorns.Hits;
        return new FeralDruidSimResponse {
            Dps = summary.DamageDone / request.DurationSeconds, DamageDone = summary.DamageDone,
            EndingMana = druid.Resources["mana"].Current,
            EndingEnergy = druid.Resources["energy"].Current,
            EndingRage = druid.Resources["rage"].Current,
            ComboPoints = mechanics.ComboPoints, LacerateStacks = mechanics.LacerateStacks,
            OmenProcs = mechanics.OmenProcs, ClearcastsUsed = mechanics.ClearcastsUsed,
            FormChanges = formState.Shifts,
            DamageTaken = tank.TotalIncoming,
            ArmorPreventedDamage = armorPreventedDamage,
            DodgedIncomingHits = dodgedHits,
            MaximumBurstDamage = tank.MaximumBurst,
            MaximumBurstPercentOfHealth = tank.MaximumBurst * 100m / druid.MaximumHealth,
            FrenziedRegenerationHealing = mechanics.FrenziedRegenerationHealing,
            ProjectedEndingHealth = tank.ProjectedHealth,
            MinimumProjectedHealth = tank.MinimumProjectedHealth,
            ProjectedHealthZeroAtSeconds = tank.ProjectedHealthZeroAtSeconds,
            EffectiveSelfHealing = tank.EffectiveSelfHealing,
            WastedSelfHealing = tank.WastedSelfHealing,
            ThreatGenerated = tank.Threat, ThreatPerSecond = tank.Threat / request.DurationSeconds,
            DamageByAbility = new Dictionary<string, decimal>(summary.DamageDoneByAbility,
                StringComparer.OrdinalIgnoreCase), Run = run
        };
    }
    private sealed class FeralRolls(FeralDruidSimRequest request,
        Func<FeralDruidCombatProcessor?> mechanics) : ICombatRollResolver
    {
        public CombatRollResult Resolve(SimulationContext context, SimulationActorState src,
            SimulationActorState target, AbilityDefinition ability, AbilityEffectDefinition effect) =>
            Resolve(context, src, target, ability, effect, CombatEffectDeliveryType.Direct);
        public CombatRollResult Resolve(SimulationContext context, SimulationActorState src,
            SimulationActorState target, AbilityDefinition ability, AbilityEffectDefinition effect,
            CombatEffectDeliveryType delivery)
        {
            if (effect.ResolutionType == CombatResolutionTypes.AlwaysHits) return CombatRollResult.Hit();
            if (effect.CanMiss && (decimal)context.Random.NextDouble() * 100m >= request.Tuning.MeleeHitPercent)
                return CombatRollResult.Miss();
            var critical = effect.CanCrit && (decimal)context.Random.NextDouble() * 100m <
                request.Tuning.MeleeCritPercent + request.Talents.Rank("sharpened-claws") * 2m +
                (request.Talents.Rank("leader-of-the-pack") > 0 ? 3m : 0m) +
                ((ability.Key is "claw" or "shred" or "rake" or "pounce" or "ravage") &&
                 src.ActiveAuras.Any(a => a.Definition.Key == "druid-berserk" &&
                    a.IsActiveAt(context.CurrentTimeSeconds)) ? 100m : 0m);
            var multiplier = mechanics()?.DamageMultiplier(ability.Key, delivery) ?? 1m;
            // Rend and Tear increases animal-form melee damage to a bleeding
            // target. Rank magnitude remains provisional and centralized.
            if (request.Talents.Rank("rend-and-tear") > 0 &&
                effect.SchoolKey == "physical" && delivery != CombatEffectDeliveryType.Periodic &&
                target.ActiveAuras.Any(a => a.IsActiveAt(context.CurrentTimeSeconds) &&
                    (a.Definition.Key is "rake" or "rip" or "lacerate" or "druid-pounce-bleed")))
                multiplier *= 1m + .03m * request.Talents.Rank("rend-and-tear");
            return new CombatRollResult { Landed = true, IsCritical = critical,
                ResultKey = critical ? CombatResultTypes.Critical : CombatResultTypes.Hit,
                AmountMultiplier = multiplier * (critical ? 2m + request.Talents.Rank("predatory-strikes") * .20m / 3m +
                request.Talents.Rank("natural-instinct") * .10m : 1m) };
        }
    }
    private sealed class FeralArmorMitigation(decimal armor, ForeverFeralDruidTuning tuning)
        : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext ctx, SimulationActorState src,
            SimulationActorState target, AbilityDefinition ability, AbilityEffectDefinition effect,
            decimal rawAmount)
        {
            // Bleeds are unaffected by armor; apply only to direct physical hits.
            if (armor <= 0m || effect.SchoolKey != "physical" ||
                effect.EffectType == AbilityEffectTypes.PeriodicDamage)
                return DamageMitigationResult.Unmitigated(rawAmount);
            var faerie = target.ActiveAuras.Any(a => a.Definition.Key == "druid-faerie-fire" &&
                a.IsActiveAt(ctx.CurrentTimeSeconds));
            var remaining = Math.Max(0m, armor - (faerie ? tuning.FaerieFireArmorReduction : 0m));
            var armorReduction=ForeverArmorMitigationMath.ClampArmorReductionFraction(
                remaining/(remaining+tuning.TankArmorReductionConstant));
            var dealt = rawAmount * (1m-armorReduction);
            return new DamageMitigationResult {
                RawAmount = rawAmount, FinalAmount = dealt,
                MitigatedAmount = rawAmount - dealt,
                ReductionPercent = rawAmount > 0m ? (rawAmount - dealt) * 100m / rawAmount : 0m,
                DefenseValue = remaining
            };
        }
    }
}

// Adapt the Bear mitigation calculation to the shared scripted encounter pipeline.
internal sealed class FeralIncomingBossMitigation(
    Func<SimulationContext, SimulationActorState, decimal, DamageMitigationResult> resolve)
    : IDamageMitigationResolver
{
    public DamageMitigationResult Resolve(SimulationContext context, SimulationActorState source,
        SimulationActorState target, AbilityDefinition ability, AbilityEffectDefinition effect,
        decimal rawAmount) => resolve(context, target, rawAmount);
}

internal sealed class FeralDruidCombatProcessor : ICombatEventProcessor
{
    private readonly SimulationActorState _druid;
    private readonly FeralDruidSimRequest _request;
    private readonly RotationProfile _rotation;
    private readonly ForeverDruidFormState _form;
    private readonly AuraManager _auras;
    private readonly Action<SimulationContext, ForeverDruidForm>? _shiftTo;
    private readonly Action<SimulationContext>? _resumeAutoAfterOpener;
    private readonly Func<decimal, decimal>? _recovery;
    private readonly ForcedTargetManager? _forcedTargets;
    private readonly HashSet<Guid> _generators = [];
    private readonly HashSet<Guid> _finishers = [];
    private int _lastFinisherPoints;
    private decimal _nextOmen;
    public decimal FrenziedRegenerationHealing { get; private set; }
    private decimal _frenziedUntil;
    private readonly Dictionary<string, decimal> _baseCosts;
    public int ComboPoints { get; private set; }
    public int LacerateStacks { get; private set; }
    public int OmenProcs { get; private set; }
    public int ClearcastsUsed { get; private set; }
    public FeralDruidCombatProcessor(SimulationActorState druid,FeralDruidSimRequest request,
        RotationProfile rotation,ForeverDruidFormState form,AuraManager auras,
        Action<SimulationContext, ForeverDruidForm>? shiftTo = null,
        Action<SimulationContext>? resumeAutoAfterOpener = null,
        Func<decimal, decimal>? recovery = null, ForcedTargetManager? forcedTargets = null)
    {
        _druid = druid; _request = request; _rotation = rotation; _form = form; _auras = auras;
        _shiftTo = shiftTo;
        _resumeAutoAfterOpener = resumeAutoAfterOpener;
        _recovery = recovery;
        _forcedTargets = forcedTargets;
        _baseCosts = druid.Abilities.Where(x => x.Value.Definition.ResourceCosts.Count > 0)
            .ToDictionary(x => x.Key, x => x.Value.Definition.ResourceCosts[0].Amount,
                StringComparer.OrdinalIgnoreCase);
    }
    public decimal DamageMultiplier(string ability,CombatEffectDeliveryType delivery) => ability switch
    {
        "ferocious-bite" => Math.Max(1,ComboPoints > 0 ? ComboPoints : _lastFinisherPoints),
        "rip" => Math.Max(1,_lastFinisherPoints),
        "lacerate" when delivery == CombatEffectDeliveryType.Periodic => Math.Max(1,LacerateStacks),
        "lacerate" => LacerateStacks,
        _ => 1m
    };
    public void Process(SimulationContext context,CombatEvent ev)
    {
        if (ev.Type == CombatEventType.RotationDecision &&
            ev.SourceActorKey == _druid.Key && ev.EffectKey is { } key &&
            key.StartsWith("feral-form-shift:", StringComparison.Ordinal))
        {
            var to = key["feral-form-shift:".Length..] switch {
                "cat" => ForeverDruidForm.Cat,
                "bear" => ForeverDruidForm.DireBear,
                "moonkin" => ForeverDruidForm.Moonkin,
                _ => ForeverDruidForm.Caster
            };
            _shiftTo?.Invoke(context, to);
            return;
        }
        if (ev.Type == CombatEventType.RotationDecision &&
            ev.EffectKey == "feral-frenzied-regeneration-pulse" &&
            ev.SourceActorKey == _druid.Key)
        {
            if (_form.Form is not (ForeverDruidForm.Bear or ForeverDruidForm.DireBear) ||
                context.CurrentTimeSeconds > _frenziedUntil) return;
            var amount = Math.Min(10m, _druid.Resources["rage"].Current);
            if (amount <= 0m) return;
            _druid.Resources["rage"].Spend(amount);
            context.EmitEvent(new CombatEvent {
                Type = CombatEventType.ResourceChanged, TimeSeconds = context.CurrentTimeSeconds,
                SourceActorKey = _druid.Key, TargetActorKey = _druid.Key,
                AbilityKey = "frenzied-regeneration", ResourceKey = "rage",
                Amount = -amount, Description = $"Frenzied Regeneration consumed {amount:0.##} Rage."
            });
            var healing = _druid.MaximumHealth * amount / 100m;
            FrenziedRegenerationHealing += healing;
            var effective = _recovery?.Invoke(healing) ?? 0m;
            // Make projected healing visible in the standard healing breakdown.
            // The tank's true health is preserved in this fixed-duration preview.
            context.EmitEvent(new CombatEvent {
                Type = CombatEventType.Healing, TimeSeconds = context.CurrentTimeSeconds,
                SourceActorKey = _druid.Key, TargetActorKey = _druid.Key,
                AbilityKey = "frenzied-regeneration", EffectKey = "projected-recovery",
                Amount = effective, RawAmount = healing,
                OverhealingAmount = healing - effective,
                Description = $"Frenzied Regeneration restored {effective:0.##} projected health."
            });
            // Solo-tank health is intentionally preserved by the boss script.
            // Record available recovery separately; real missing health can
            // still receive effective direct healing when present.
            _druid.Heal(healing);
            return;
        }
        if (ev.Type == CombatEventType.RotationDecision)
        {
            var target = context.GetActor("target");
            var haveRip = target?.ActiveAuras.Any(a => a.Definition.Key == "rip" &&
                a.IsActiveAt(context.CurrentTimeSeconds)) ?? false;
            var inProwl = _druid.ActiveAuras.Any(a => a.Definition.Key == "druid-prowl" &&
                a.IsActiveAt(context.CurrentTimeSeconds));
            foreach (var entry in _rotation.Entries)
                entry.IsEnabled = entry.AbilityKey switch {
                    "ravage" or "pounce" => _request.StartInProwl && inProwl &&
                        entry.AbilityKey == _request.PreferredCatOpener &&
                        (entry.AbilityKey != "ravage" || _request.TargetBehind),
                    "shred" => !inProwl && _request.TargetBehind,
                    "claw" => !inProwl,
                    "growl" => _request.UseBearUtility && target is not null &&
                        target.ThreatTable.GetHighestThreatValue(target.ThreatTable.Entries.Keys) >
                        target.ThreatTable.GetThreat(_druid.Key),
                    "faerie-fire" => _request.UseFaerieFire && target is not null &&
                        !target.ActiveAuras.Any(a => a.Definition.Key == "druid-faerie-fire" &&
                            a.IsActiveAt(context.CurrentTimeSeconds)),
                    "challenging-roar" => _request.UseBearUtility && target is not null &&
                        target.ThreatTable.GetHighestThreatValue(target.ThreatTable.Entries.Keys) >
                        target.ThreatTable.GetThreat(_druid.Key) &&
                        !(target.ForcedTarget?.IsActiveAt(context.CurrentTimeSeconds) ?? false),
                    "feral-charge" => _request.UseBearUtility && target is not null &&
                        !target.ActiveAuras.Any(a => a.Definition.Key == "druid-feral-charge-root" &&
                            a.IsActiveAt(context.CurrentTimeSeconds)),
                    "demoralizing-roar" => _request.UseBearUtility && target is not null &&
                        !target.ActiveAuras.Any(a => a.Definition.Key == "druid-demoralizing-roar" &&
                            a.IsActiveAt(context.CurrentTimeSeconds)),
                    "berserk" => _request.UseFeralCooldowns && !inProwl &&
                        (_form.Form is ForeverDruidForm.Cat or ForeverDruidForm.Bear or ForeverDruidForm.DireBear),
                    "frenzied-regeneration" => _request.UseFeralCooldowns &&
                        (_form.Form is ForeverDruidForm.Bear or ForeverDruidForm.DireBear) &&
                        _druid.Resources["rage"].Current > 0m &&
                        !_druid.ActiveAuras.Any(a => a.Definition.Key == "druid-frenzied-regeneration" &&
                            a.IsActiveAt(context.CurrentTimeSeconds)),
                    "shifting-power" => !inProwl && _form.Form == ForeverDruidForm.Cat &&
                        _druid.Resources["energy"].Current < 100m,
                    "rip" => !inProwl && ComboPoints >= 4 && !haveRip,
                    "ferocious-bite" => !inProwl && ComboPoints >= 4 && !haveRip,
                    "rake" => !inProwl && target is not null && !target.ActiveAuras.Any(a =>
                        a.Definition.Key == "rake" && a.IsActiveAt(context.CurrentTimeSeconds)),
                    "lacerate" => LacerateStacks < 5 || target is not null &&
                        !target.ActiveAuras.Any(a => a.Definition.Key == "lacerate" &&
                            a.IsActiveAt(context.CurrentTimeSeconds)),
                    _ => true
                };
            if (_druid.Abilities.TryGetValue("primal-bite", out var primalBite))
                primalBite.Definition.CooldownSeconds =
                    _druid.ActiveAuras.Any(a => a.Definition.Key == "druid-berserk" &&
                        a.IsActiveAt(context.CurrentTimeSeconds)) ? 0m : 6m;
            var free = _druid.ActiveAuras.Any(a => a.Definition.Key == "druid-clearcasting" &&
                a.IsActiveAt(context.CurrentTimeSeconds));
            foreach (var (abilityKey, cost) in _baseCosts)
                _druid.Abilities[abilityKey].Definition.ResourceCosts[0].Amount =
                    free && abilityKey is not "shifting-power" ? 0m : cost;
            return;
        }
            // Incoming Bear Rage is generated by the shared normalized
            // damage-taken resource processor, not a flat five-Rage preview.
        if (ev.SourceActorKey != _druid.Key) return;
        if (ev.Type == CombatEventType.AbilityCastCompleted)
        {
            if (ev.AbilityKey is "rip" or "ferocious-bite" && ev.AbilityExecutionId is Guid fin &&
                _finishers.Add(fin))
            {
                _lastFinisherPoints = ComboPoints;
                ComboPoints = 0;
            }
            if (ev.AbilityKey is "pounce" or "ravage")
            {
                _auras.RemoveAuras(context, _druid, "druid-prowl", _druid.Key, "opener broke Prowl");
                _resumeAutoAfterOpener?.Invoke(context);
            }
            if (ev.AbilityKey == "cower")
            {
                var boss = context.GetActor("target");
                if (boss != null) boss.ThreatTable.SetThreat(_druid.Key,
                    Math.Max(0m, boss.ThreatTable.GetThreat(_druid.Key) - 600m));
            }
            if (ev.AbilityKey == "growl")
            {
                var boss = context.GetActor("target");
                if (boss != null)
                    boss.ThreatTable.SetThreat(_druid.Key,
                        Math.Max(boss.ThreatTable.GetThreat(_druid.Key),
                            boss.ThreatTable.GetHighestThreatValue(boss.ThreatTable.Entries.Keys)));
            }
            if (ev.AbilityKey == "growl" && _forcedTargets is not null)
            {
                var boss = context.GetActor("target");
                if (boss is not null)
                    _forcedTargets.ApplyForcedTarget(context, boss, _druid, 3m,
                        _druid.Key, "growl", "forced-target");
            }
            if (ev.AbilityKey == "challenging-roar" && _forcedTargets is not null)
            {
                foreach (var enemy in context.Actors.Values.Where(a =>
                    a.TeamKey != _druid.TeamKey && a.IsAlive))
                    _forcedTargets.ApplyForcedTarget(context, enemy, _druid, 6m,
                        _druid.Key, "challenging-roar", "aoe-forced-target");
            }
            if (ev.AbilityKey == "demoralizing-roar")
            {
                foreach (var enemy in context.Actors.Values.Where(a => a.TeamKey != _druid.TeamKey))
                    enemy.ThreatTable.AddThreat(_druid.Key, 50m); // provisional flat threat
            }
            if (ev.AbilityKey == "frenzied-regeneration")
            {
                _frenziedUntil = context.CurrentTimeSeconds +
                    _request.Tuning.FrenziedRegenerationDurationSeconds;
                // The timestamp gate keeps the final pulse valid even when
                // the aura expires at the same simulation instant.
                for (var second = 1; second <= 10; second++)
                    if (context.CurrentTimeSeconds + second <= context.Options.DurationSeconds)
                        context.ScheduleEvent(new CombatEvent {
                            Type = CombatEventType.RotationDecision,
                            TimeSeconds = context.CurrentTimeSeconds + second,
                            SourceActorKey = _druid.Key, TargetActorKey = _druid.Key,
                            EffectKey = "feral-frenzied-regeneration-pulse", IsInternal = true
                        });
            }
            if (ev.AbilityKey == "enrage")
            {
                _druid.Resources["rage"].Gain(10m);
            }
            if (ev.AbilityKey == "shifting-power")
            {
                var energy = _druid.Resources["energy"];
                var before = energy.Current;
                energy.Gain(40m);
                context.EmitEvent(new CombatEvent {
                    Type = CombatEventType.ResourceChanged, TimeSeconds = context.CurrentTimeSeconds,
                    SourceActorKey = _druid.Key, TargetActorKey = _druid.Key,
                    AbilityKey = "shifting-power", ResourceKey = "energy",
                    Amount = energy.Current - before,
                    Description = "Shifting Power restored Cat Energy."
                });
            }
            if (ev.AbilityKey != "shifting-power" &&
                _druid.ActiveAuras.Any(a => a.Definition.Key == "druid-clearcasting" &&
                    a.IsActiveAt(context.CurrentTimeSeconds)))
            {
                _auras.RemoveAuras(context, _druid, "druid-clearcasting", _druid.Key, "free cast consumed");
                ClearcastsUsed++;
            }
        }
        if (ev.Type == CombatEventType.AuraApplied && ev.AbilityKey == "pounce" &&
            ev.TargetActorKey == "target")
            ComboPoints = Math.Min(5, ComboPoints + 1);
        if (ev.Type == CombatEventType.AuraApplied && ev.AbilityKey == "lacerate" &&
            ev.TargetActorKey == "target")
            LacerateStacks = Math.Min(5,LacerateStacks+1);
        if (ev.Type == CombatEventType.AuraRemoved && ev.AbilityKey == "lacerate" &&
            ev.TargetActorKey == "target")
            LacerateStacks = 0;
        if (ev.Type == CombatEventType.Damage && ev.Amount is > 0m && !ev.IsPeriodic)
        {
            if (ev.AbilityKey is "claw" or "shred" or "rake" or "pounce" or "ravage" &&
                ev.AbilityExecutionId is Guid id && _generators.Add(id))
                {
                    ComboPoints = Math.Min(5,ComboPoints+1);
                    if (ev.IsCritical && _request.Talents.Rank("blood-frenzy") > 0 &&
                        (decimal)context.Random.NextDouble() * 100m <
                            50m * _request.Talents.Rank("blood-frenzy"))
                        ComboPoints = Math.Min(5, ComboPoints + 1);
                }
            if ((_form.Form is ForeverDruidForm.Bear or ForeverDruidForm.DireBear) &&
                ev.IsCritical && _request.Talents.Rank("blood-frenzy") > 0 &&
                (decimal)context.Random.NextDouble() * 100m <
                    50m * _request.Talents.Rank("blood-frenzy"))
                _druid.Resources["rage"].Gain(5m);
            if (_form.Form is ForeverDruidForm.Bear or ForeverDruidForm.DireBear &&
                ev.AbilityKey == "feral-auto")
                _druid.Resources["rage"].Gain(_request.Tuning.BearAutoRageOnHit *
                    (ev.IsCritical ? 2m : 1m));
            if (ev.AbilityKey != "thorns" && context.CurrentTimeSeconds >= _nextOmen &&
                (decimal)context.Random.NextDouble() * 100m < _request.Tuning.OmenProcPercent)
            {
                _nextOmen = context.CurrentTimeSeconds + 10m;
                OmenProcs++;
                _auras.ApplyAura(context, _druid, new AuraDefinition {
                    Key = "druid-clearcasting",Name = "Clearcasting",DurationSeconds = 15m
                },_druid.Key,"omen-of-clarity","clearcasting");
            }
        }
    }
}

internal sealed class FeralBearIncomingRageProcessor(
    ForeverDruidFormState formState,string druidActorKey) : ICombatEventProcessor
{
    private readonly DamageTakenResourceGenerationProcessor _shared=new();
    public void Process(SimulationContext context,CombatEvent ev)
    {
        // Reuses armor-independent Rage and absorbs/block arithmetic, while
        // preventing caster/Cat form from receiving Bear incoming Rage.
        if(ev.Type!=CombatEventType.Damage ||
           ev.TargetActorKey!=druidActorKey ||
           formState.Form is not (ForeverDruidForm.Bear or ForeverDruidForm.DireBear))
            return;
        _shared.Process(context,ev);
    }
}
