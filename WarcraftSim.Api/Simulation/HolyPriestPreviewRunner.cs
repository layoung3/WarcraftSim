using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Stats;
using WarcraftSim.Core.Encounters;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Healing;
using WarcraftSim.Data.Forever.Priest;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// First isolated tank-plus-healer encounter. Reuses CP124's shared boss
/// melee, the ordinary cast/heal/resource pipeline, and the class-independent
/// healer policy. The tank is a training actor without Warrior rotation yet.
/// </summary>
public sealed class HolyPriestPreviewRunner
{
    public HolyPriestPreviewResponse Run(
        HolyPriestPreviewRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);
        // Guard against a complete setup accidentally being supplied as the
        // nested CharacterProfile. System.Text.Json would otherwise create a
        // default level-1 profile and report a misleading talent-budget error.
        if ((request.UseCharacterDerivedPriestStats || request.UsePriestTalentBuild) &&
            request.PriestCharacter is { } profile &&
            (!string.Equals(profile.ClassKey, "priest", StringComparison.OrdinalIgnoreCase) ||
             profile.Level is < 1 or > 60))
            throw new ArgumentException(
                "PriestCharacter must be a nested CharacterProfile with classKey 'priest' " +
                "and level 1–60. To import a complete healer setup, use the " +
                "Complete healer setup importer rather than the CharacterProfile field.");
        if (request.UsePriestTalentBuild)
            request.PriestTalents.Validate(request.PriestCharacter?.Level ?? 60);
        int rank(string key, int fallback) => request.UsePriestTalentBuild
            ? request.PriestTalents.Rank(key) : fallback;
        var derived = request.UseCharacterDerivedPriestStats
            ? ForeverPriestCharacterCalculator.Calculate(request.PriestCharacter!,
                request.PriestStatAssumptions,
                rank("mental-strength", 0), rank("spiritual-guidance", 0))
            : null;
        decimal maximumMana = derived?.MaximumMana ?? request.PriestMaximumMana;
        decimal healingCritPercent = derived?.HealingCritPercent ?? request.PriestHealingCritPercent;
        decimal spiritRegen = derived?.SpiritRegenerationPerSecond ?? request.PriestSpiritRegenPerSecond;
        decimal mp5 = derived?.ManaPerFiveSeconds ?? request.PriestMp5;
        decimal meditationFraction = request.UsePriestTalentBuild
            ? rank("meditation", 0) switch { 0 => 0m, 1 => .17m, 2 => .33m, _ => .50m }
            : request.MeditationCastingSpiritFraction;
        decimal litanyRefund = request.UsePriestTalentBuild
            ? rank("litany-of-light", 0) * .05m : request.LitanyRefundFraction;
        var context = new SimulationContext(new SimulationRunOptions
        {
            Seed = request.Seed,
            DurationSeconds = request.DurationSeconds,
            PrimaryActorKey = "tank",
            CaptureTimeline = request.CaptureTimeline,
            MaximumProcessedEvents = 20000,
            CancellationToken = cancellationToken
        });

        static SimulationActorState NewActor(string key, string team, decimal maxHp) => new()
        {
            Key = key, Name = key, TeamKey = team, Level = 60,
            MaximumHealth = maxHp
        };
        var boss = NewActor("boss", "enemy", 1000000m);
        var tank = NewActor("tank", "raid", request.TankMaximumHealth);
        var priest = NewActor("priest", "raid", request.HealerMaximumHealth);
        boss.InitializeHealth(boss.MaximumHealth);
        tank.InitializeHealth(tank.MaximumHealth);
        priest.InitializeHealth(priest.MaximumHealth,
            request.EnableExtendedPriest
                ? priest.MaximumHealth * request.PriestInitialHealthPercent / 100m
                : priest.MaximumHealth);
        priest.AddResource(new ResourceState("mana", maximumMana,
            maximumMana, request.PriestManaRegenPerSecond));
        if (derived is not null)
            priest.Stats = new StatCollection { Values = new Dictionary<string, decimal>(
                derived.EffectiveStats, StringComparer.OrdinalIgnoreCase) };
        context.AddActor(boss);
        context.AddActor(tank);
        context.AddActor(priest);
        if (request.EnableExtendedPriest)
        {
            for (var index = 1; index <= request.AdditionalRaidMembers; index++)
            {
                var ally = NewActor($"raid-{index}", "raid", request.RaidMemberMaximumHealth);
                ally.InitializeHealth(ally.MaximumHealth,
                    ally.MaximumHealth * request.RaidMemberInitialHealthPercent / 100m);
                context.AddActor(ally);
            }
        }

        if (!request.EnableExtendedPriest)
        {
            priest.AddAbility(CreateHeal("flash-heal", "Flash Heal",
                request.FlashHealAmount, request.FlashHealManaCost, request.FlashHealCastSeconds));
            priest.AddAbility(CreateHeal("greater-heal", "Greater Heal",
                request.GreaterHealAmount, request.GreaterHealManaCost,
                request.GreaterHealCastSeconds));
        }
        else
        {
            var tuning = new ForeverPriestHealingTuning
            {
                FlashHealAmount = request.FlashHealAmount,
                FlashHealCost = request.FlashHealManaCost,
                GreaterHealAmount = request.GreaterHealAmount,
                GreaterHealCost = request.GreaterHealManaCost,
                RenewTickAmount = request.RenewTickAmount,
                ShieldAbsorbAmount = request.ShieldAbsorbAmount,
                PrayerOfMendingAmount = request.PrayerOfMendingAmount,
                PrayerOfHealingAmount = request.PrayerOfHealingAmount,
                BindingHealAmount = request.BindingHealAmount,
                PenanceTickAmount = request.PenanceTickAmount,
                ImprovedRenewRanks = rank("improved-renew", request.ImprovedRenewRanks),
                ImprovedShieldRanks = rank("improved-power-word-shield", request.ImprovedShieldRanks),
                SpiritualHealingRanks = rank("spiritual-healing", request.SpiritualHealingRanks),
                DivineFuryRanks = rank("divine-fury", request.DivineFuryRanks),
                ImprovedHealingRanks = rank("improved-healing", request.ImprovedHealingRanks),
                TwinDisciplinesRanks = rank("twin-disciplines", request.TwinDisciplinesRanks),
                SoulWarding = rank("soul-warding", request.SoulWarding ? 1 : 0) > 0,
                MentalAgilityRanks = rank("mental-agility", 0),
                BonusHealingPower = derived?.HealingPower ?? 0m,
                UseLegacySpiritualHealingScaling = !request.UsePriestTalentBuild,
                UseLegacyImprovedShieldScaling = !request.UsePriestTalentBuild,
                RestrictTalentAbilities = request.UsePriestTalentBuild,
                HasPenanceTalent = rank("penance", 0) > 0,
                HasBindingHealTalent = rank("binding-heal", 0) > 0,
                HasPrayerOfMendingTalent = rank("prayer-of-mending", 0) > 0
            };
            foreach (var spell in ForeverPriestHealingSpellbook.Create(tuning))
                priest.AddAbility(spell);
            if (request.UsePriestTalentBuild && rank("inner-focus",0)>0)
                priest.AddAbility(ForeverPriestMajorCooldowns.InnerFocus());
            if (request.UsePriestTalentBuild && rank("power-infusion",0)>0)
                priest.AddAbility(ForeverPriestMajorCooldowns.PowerInfusion());
        }

        var auraManager = new AuraManager();
        var majorCooldowns = new PriestMajorCooldownProcessor(priest, auraManager);
        var roll = request.EnableExtendedPriest && (request.EnableHealingCriticals ||
            priest.Abilities.ContainsKey("inner-focus") ||
            priest.Abilities.ContainsKey("power-infusion"))
            ? (ICombatRollResolver)new ControlledHealingCritResolver(
                request.EnableHealingCriticals ? healingCritPercent : 0m, majorCooldowns)
            : new GuaranteedHitResolver();
        var mitigation = new NoDamageMitigation();
        var absorbManager = new AbsorbManager();
        var executor = new AbilityExecutor(auraManager, roll, mitigation,
            absorbManager: absorbManager);
        var melee = new EncounterBossMeleeProcessor(new EncounterBossMeleeDefinition
        {
            BossActorKey = boss.Key,
            AbilityKey = "boss-main-hand-auto",
            PreferredTargetActorKey = tank.Key,
            EligibleTargetTeamKey = tank.TeamKey,
            TargetMode = EncounterBossMeleeTargetModes.Fixed,
            FirstSwingSeconds = 1m,
            SwingSpeedSeconds = request.BossSwingIntervalSeconds,
            MinimumDamage = request.BossSwingDamage,
            MaximumDamage = request.BossSwingDamage,
            PreserveTargetHealth = false,
            EnableBossParryHaste = false
        }, roll, mitigation);
        var rules = new List<HealerPriorityRule>();
        if (request.EnableExtendedPriest)
        {
            if (request.UsePrayerOfHealing) rules.Add(new HealerPriorityRule
            {
                AbilityKey = "prayer-of-healing", Priority = 0,
                CastAtOrBelowHealthPercent = 80m, MinimumInjuredTeamMembers = 3
            });
            if (request.UsePrayerOfMending) rules.Add(new HealerPriorityRule
            {
                AbilityKey = "prayer-of-mending", Priority = 1,
                CastAtOrBelowHealthPercent = 94m,
                SkipWhenTargetHasAuraKey = "prayer-of-mending"
            });
            if (request.UsePowerWordShield) rules.Add(new HealerPriorityRule
            {
                AbilityKey = "power-word-shield", Priority = 2,
                CastAtOrBelowHealthPercent = 58m,
                SkipWhenTargetHasAuraKey = "weakened-soul",
                SkipWhenTargetHasAbsorbKey = "power-word-shield"
            });
            if (request.UsePenance) rules.Add(new HealerPriorityRule
            {
                AbilityKey = "penance", Priority = 3, CastAtOrBelowHealthPercent = 72m
            });
            if (request.UseBindingHeal) rules.Add(new HealerPriorityRule
            {
                AbilityKey = "binding-heal", Priority = 4, CastAtOrBelowHealthPercent = 62m
            });
            if (request.UseRenew) rules.Add(new HealerPriorityRule
            {
                AbilityKey = "renew", Priority = 5, CastAtOrBelowHealthPercent = 96m,
                SkipWhenTargetHasAuraKey = "renew"
            });
        }
        rules.Add(new HealerPriorityRule
        {
            Priority = 6, AbilityKey = "greater-heal",
            CastAtOrBelowHealthPercent = request.GreaterHealHealthThresholdPercent,
            MinimumMissingHealth = 1m
        });
        rules.Add(new HealerPriorityRule
        {
            Priority = 7, AbilityKey = "flash-heal",
            CastAtOrBelowHealthPercent = request.FlashHealHealthThresholdPercent,
            MinimumMissingHealth = 1m
        });
        if (request.UseAdaptiveHealingPolicy && request.EnableExtendedPriest)
            rules = request.UseSpecializationHealingPolicy
                ? CreateSpecializationRules(request, priest)
                : CreateAdaptiveRules(request, priest);
        var healer = new HealerPriorityProcessor(new HealerPriorityDefinition
        {
            HealerActorKey = priest.Key, EligibleTeamKey = "raid",
            DecisionIntervalSeconds = 0.25m, Rules = rules,
            ForecastTargetActorKey = tank.Key,
            ForecastIncomingDamagePerSecond = request.UseAdaptiveHealingPolicy
                ? request.BossSwingDamage / request.BossSwingIntervalSeconds : 0m,
            ForecastSeconds = request.UseAdaptiveHealingPolicy ? request.ForecastHorizonSeconds : 0m
        }, executor);

        FiveSecondManaRegenerationProcessor? manaRule = null;
        var processors = new List<ICombatEventProcessor> { executor, auraManager, majorCooldowns, absorbManager, melee };
        if (request.EnableExtendedPriest)
        {
            manaRule = new FiveSecondManaRegenerationProcessor(priest.Key,
                spiritRegen, mp5, meditationFraction);
            manaRule.Initialize(context);
            processors.Add(manaRule);
            processors.Add(new ThreatManager(new HealingThreatResolver(request.HealingThreatCoefficient)));
            if (request.UsePrayerOfMending && priest.Abilities.ContainsKey("prayer-of-mending"))
            {
                var prayerHeal = request.PrayerOfMendingAmount;
                if (derived is not null || request.UsePriestTalentBuild)
                {
                    decimal scale = request.UsePriestTalentBuild
                        ? rank("spiritual-healing", 0) switch
                            { 0 => 1m, 1 => 1.03m, 2 => 1.07m, _ => 1.10m }
                        : 1m + request.SpiritualHealingRanks * .05m;
                    prayerHeal = (prayerHeal + (derived?.HealingPower ?? 0m) * .25m) * scale;
                }
                processors.Add(new PrayerOfMendingProcessor(priest.Key,
                    prayerHeal, auraManager));
            }
            if (request.UseDivineAegis && rank("divine-aegis", request.UseDivineAegis ? 3 : 0) > 0)
                processors.Add(new DivineAegisProcessor(priest.Key,
                    request.UsePriestTalentBuild ? rank("divine-aegis", 0) * .05m
                        : request.DivineAegisFraction, absorbManager));
            if (litanyRefund > 0m)
                processors.Add(new AlternatingHealManaRefundProcessor(priest.Key,
                    litanyRefund));
        }
        if (request.RaidPulseDamage > 0m)
            processors.Add(new ScriptedEncounterDamageProcessor(mitigation, absorbManager));
        if (request.EnableHealer)
        {
            if (request.UsePriestTalentBuild && request.UseMajorCooldowns)
                processors.Add(new PriestHealerCooldownUseProcessor(priest, executor));
            processors.Add(healer);
        }
        var run = new SimulationEngine(processors).Run(context, ctx =>
        {
            melee.ScheduleFirst(ctx);
            if (request.RaidPulseDamage > 0m) ScheduleRaidPulses(ctx, request);
            if (request.EnableHealer) healer.ScheduleFirst(ctx);
        });
        priest.RefreshResources(request.DurationSeconds);

        var priestSummary = run.Summary.ActorSummaries[priest.Key];
        priestSummary.EndingResources["mana"] = priest.Resources["mana"].Current;
        var tankSummary = run.Summary.ActorSummaries[tank.Key];
        return new HolyPriestPreviewResponse
        {
            TankSurvived = tank.IsAlive,
            TankEndingHealth = tank.CurrentHealth,
            PriestEndingMana = priest.Resources["mana"].Current,
            HealingDone = priestSummary.HealingDone,
            OverhealingDone = priestSummary.OverhealingDone,
            TankDamageTaken = tankSummary.DamageTaken,
            FlashHealCasts = priestSummary.Abilities.TryGetValue("flash-heal", out var flash)
                ? flash.CastCompletedCount : 0,
            GreaterHealCasts = priestSummary.Abilities.TryGetValue("greater-heal", out var greater)
                ? greater.CastCompletedCount : 0,
            PriestSpellCasts = priestSummary.Abilities.ToDictionary(pair => pair.Key,
                pair => pair.Value.CastCompletedCount, StringComparer.OrdinalIgnoreCase),
            AbsorptionDone = priestSummary.AbsorptionDone,
            HealingThreatOnBoss = boss.ThreatTable.GetThreat(priest.Key),
            DerivedPriestStats = derived,
            PriestMaximumManaUsed = maximumMana,
            PriestHealingCritPercentUsed = healingCritPercent,
            RaidDamageTaken = run.Summary.ActorSummaries
                .Where(pair => pair.Key.StartsWith("raid-", StringComparison.OrdinalIgnoreCase) ||
                    pair.Key.Equals(priest.Key, StringComparison.OrdinalIgnoreCase))
                .Sum(pair => pair.Value.DamageTaken),
            RaidMemberDeaths = run.Summary.ActorSummaries.Count(pair =>
                pair.Key.StartsWith("raid-", StringComparison.OrdinalIgnoreCase) && pair.Value.Died),
            Run = run
        };
    }

    /// <summary>
    /// Schedules deterministic, absorb-aware AoE pressure against secondary
    /// living raid actors. The shared ScriptedEncounterDamageProcessor resolves
    /// real HP and death events; no duplicate health path is introduced.
    /// </summary>
    internal static void ScheduleRaidPulses(SimulationContext context,
        HolyPriestPreviewRequest request, string bossActorKey = "boss")
    {
        var members = Enumerable.Range(1, Math.Min(request.AdditionalRaidMembers,
                request.RaidPulseTargetCount))
            .Select(index => $"raid-{index}").ToList();
        if (request.RaidPulseIncludePriest) members.Add("priest");
        if (members.Count == 0) return;
        for (decimal time = request.RaidPulseFirstSeconds;
             time < request.DurationSeconds; time += request.RaidPulseIntervalSeconds)
        {
            foreach (var member in members)
            {
                context.ScheduleEvent(new CombatEvent
                {
                    TimeSeconds = time, Type = CombatEventType.ScriptedDamage,
                    SourceActorKey = bossActorKey, TargetActorKey = member,
                    EncounterEventKey = "healer-raid-pulse", SchoolKey = "shadow",
                    MitigationType = DamageMitigationTypes.None,
                    RawAmount = request.RaidPulseDamage,
                    Description = "Configured raid-wide damage pulse"
                });
            }
        }
    }

    /// <summary>
    /// CP127.3 opt-in role-aware policies. Each spell is still executed by
    /// AbilityExecutor and gated by its talent availability, costs and cooldown.
    /// These are behavioral heuristics, NOT verified player rotations.
    /// </summary>
    internal static List<HealerPriorityRule> CreateSpecializationRules(
        HolyPriestPreviewRequest request, SimulationActorState priest)
    {
        var discipline = string.Equals(request.PriestCharacter?.SpecializationKey,
            "discipline", StringComparison.OrdinalIgnoreCase);
        var rules = new List<HealerPriorityRule>();
        void add(string ability, decimal health, Action<HealerPriorityRule>? configure = null)
        {
            if (!priest.Abilities.ContainsKey(ability)) return;
            var rule = new HealerPriorityRule
            {
                AbilityKey = ability, Priority = rules.Count,
                CastAtOrBelowHealthPercent = health
            };
            configure?.Invoke(rule);
            rules.Add(rule);
        }
        void tank(HealerPriorityRule rule)
        {
            rule.RequiredTargetActorKey = "tank";
            rule.UseIncomingDamageForecast = true;
        }
        void shield(HealerPriorityRule rule)
        {
            tank(rule);
            rule.SkipWhenTargetHasAuraKey = "weakened-soul";
            rule.SkipWhenTargetHasAbsorbKey = "power-word-shield";
            rule.MinimumMissingHealth = 0m;
            rule.AllowUninjuredTargets = true;
        }
        void group(HealerPriorityRule rule)
        {
            rule.MinimumInjuredTeamMembers = 3;
            rule.MinimumMissingHealth = request.PrayerOfHealingAmount * .40m;
            rule.MinimumManaPercent = request.MinimumSecondaryTargetManaPercent;
        }
        void binding(HealerPriorityRule rule)
        {
            rule.MaximumHealerHealthPercent = 85m;
            rule.ExcludeHealerAsTarget = true;
            rule.MinimumManaPercent = request.MinimumSecondaryTargetManaPercent;
        }
        if (discipline)
        {
            // Prevent imminent tank death first, then actively use Penance.
            add("flash-heal", 38m, tank);
            if (request.UsePowerWordShield) add("power-word-shield", 94m, shield);
            if (request.UsePenance) add("penance", 93m, tank);
            if (request.UseBindingHeal) add("binding-heal", 84m, binding);
            add("greater-heal", 45m, rule =>
            {
                tank(rule);
                rule.MaximumExpectedOverhealPercent = 25m;
            });
            if (request.UsePrayerOfHealing) add("prayer-of-healing", 85m, group);
            if (request.UseRenew) add("renew", 77m, rule =>
            {
                tank(rule);
                rule.SkipWhenTargetHasAuraKey = "renew";
            });
            add("flash-heal", 82m, rule =>
            {
                tank(rule);
                rule.MaximumExpectedOverhealPercent = 40m;
            });
        }
        else
        {
            // Holy keeps Mending/Renew on a pressured tank and reserves AoE
            // healing for actual multi-target injuries instead of full HP.
            add("flash-heal", 38m, tank);
            if (request.UsePrayerOfHealing) add("prayer-of-healing", 89m, group);
            if (request.UsePrayerOfMending) add("prayer-of-mending", 96m, rule =>
            {
                tank(rule);
                rule.SkipWhenTargetHasAuraKey = "prayer-of-mending";
            });
            if (request.UseRenew) add("renew", 96m, rule =>
            {
                tank(rule);
                rule.SkipWhenTargetHasAuraKey = "renew";
            });
            if (request.UseBindingHeal) add("binding-heal", 88m, binding);
            if (request.UsePowerWordShield) add("power-word-shield", 64m, shield);
            add("greater-heal", 46m, rule =>
            {
                tank(rule);
                rule.MaximumExpectedOverhealPercent = 25m;
            });
            add("flash-heal", 81m, rule =>
            {
                tank(rule);
                rule.MaximumExpectedOverhealPercent = 40m;
            });
            if (request.UseRenew) add("renew", 75m, rule =>
            {
                rule.SkipWhenTargetHasAuraKey = "renew";
                rule.MinimumManaPercent = request.MinimumSecondaryTargetManaPercent;
            });
        }
        add("heal", 87m, rule =>
        {
            rule.MaximumExpectedOverhealPercent = 30m;
            rule.MinimumManaPercent = 5m;
        });
        add("flash-heal", 88m, rule =>
        {
            rule.MaximumExpectedOverhealPercent = 65m;
        });
        return rules;
    }

    internal static List<HealerPriorityRule> CreateAdaptiveRules(
        HolyPriestPreviewRequest request, SimulationActorState priest)
    {
        var rules = new List<HealerPriorityRule>();
        int priority = 0;
        void add(string key, decimal hp, Action<HealerPriorityRule>? configure = null)
        {
            if (!priest.Abilities.ContainsKey(key)) return;
            var rule = new HealerPriorityRule
            {
                AbilityKey = key, Priority = priority++, CastAtOrBelowHealthPercent = hp
            };
            configure?.Invoke(rule);
            rules.Add(rule);
        }
        // Survival decisions before steady-state raid topping. At fixed boss
        // cadence, shield can cover the next hit rather than only reacting to
        // a sub-58% tank, and requires the absence of Weakened Soul.
        if (request.UsePowerWordShield) add("power-word-shield", 86m, rule =>
        {
            rule.UseIncomingDamageForecast = true;
            rule.RequiredTargetActorKey = "tank";
            rule.AllowUninjuredTargets = true;
            rule.MinimumMissingHealth = 0m;
            rule.SkipWhenTargetHasAuraKey = "weakened-soul";
            rule.SkipWhenTargetHasAbsorbKey = "power-word-shield";
        });
        if (request.UsePenance) add("penance", 63m, rule =>
        {
            rule.RequiredTargetActorKey = "tank";
            rule.UseIncomingDamageForecast = true;
        });
        add("greater-heal", 48m, rule =>
        {
            rule.RequiredTargetActorKey = "tank";
            rule.UseIncomingDamageForecast = true;
            rule.MaximumExpectedOverhealPercent = 25m;
        });
        add("flash-heal", 76m, rule =>
        {
            rule.RequiredTargetActorKey = "tank";
            rule.UseIncomingDamageForecast = true;
            rule.MaximumExpectedOverhealPercent = 45m;
        });
        if (request.UsePrayerOfMending) add("prayer-of-mending", 87m, rule =>
        {
            rule.RequiredTargetActorKey = "tank";
            rule.SkipWhenTargetHasAuraKey = "prayer-of-mending";
        });
        if (request.UseRenew) add("renew", 80m, rule =>
        {
            rule.RequiredTargetActorKey = "tank";
            rule.SkipWhenTargetHasAuraKey = "renew";
        });
        // Reserve discretionary group healing for sustained mana and genuine
        // injuries, not for three allies who started 35% injured but take no
        // subsequent damage. Emergency single-target rules above come first.
        if (request.UsePrayerOfHealing) add("prayer-of-healing", 70m, rule =>
        {
            rule.MinimumInjuredTeamMembers = 3;
            rule.MinimumMissingHealth = request.PrayerOfHealingAmount * .75m;
            rule.MinimumManaPercent = request.MinimumSecondaryTargetManaPercent;
        });
        if (request.UseBindingHeal) add("binding-heal", 62m, rule =>
        {
            rule.MaximumHealerHealthPercent = 85m;
            rule.MinimumManaPercent = request.MinimumSecondaryTargetManaPercent;
        });
        if (request.UseRenew) add("renew", 69m, rule =>
        {
            rule.SkipWhenTargetHasAuraKey = "renew";
            rule.MinimumManaPercent = request.MinimumSecondaryTargetManaPercent;
        });
        add("heal", 83m, rule =>
        {
            rule.MinimumManaPercent = 5m;
            rule.MaximumExpectedOverhealPercent = 30m;
        });
        add("flash-heal", 88m, rule =>
        {
            rule.MaximumExpectedOverhealPercent = 65m;
        });
        return rules;
    }

    private static AbilityDefinition CreateHeal(string key, string name,
        decimal amount, decimal mana, decimal castSeconds) => new()
    {
        Key = key, Name = name, ClassKey = "priest", SpecializationKey = "holy",
        CastTimeSeconds = castSeconds, GlobalCooldownSeconds = 1.5m,
        ResourceCosts = [new AbilityResourceCost { ResourceKey = "mana", Amount = mana }],
        Effects = [new AbilityEffectDefinition
        {
            Key = key + "-heal", EffectType = AbilityEffectTypes.DirectHealing,
            TargetType = AbilityTargetTypes.Friendly, SchoolKey = "holy",
            ResolutionType = CombatResolutionTypes.Spell,
            CanMiss = false, CanCrit = false,
            MinimumValue = amount, MaximumValue = amount
        }]
    };

    private static void Validate(HolyPriestPreviewRequest r)
    {
        if (r.DurationSeconds <= 0m || r.DurationSeconds > 1800m ||
            r.TankMaximumHealth <= 0m || r.HealerMaximumHealth <= 0m ||
            r.BossSwingDamage < 0m || r.BossSwingIntervalSeconds <= 0m ||
            r.PriestMaximumMana <= 0m || r.PriestManaRegenPerSecond < 0m ||
            r.FlashHealAmount <= 0m || r.GreaterHealAmount <= 0m ||
            r.FlashHealManaCost < 0m || r.GreaterHealManaCost < 0m ||
            r.FlashHealCastSeconds <= 0m || r.GreaterHealCastSeconds <= 0m ||
            r.FlashHealHealthThresholdPercent is < 0m or > 100m ||
            r.GreaterHealHealthThresholdPercent is < 0m or > 100m ||
            r.AdditionalRaidMembers is < 0 or > 4 ||
            r.RaidMemberMaximumHealth <= 0m ||
            r.RaidMemberInitialHealthPercent is < 1m or > 100m ||
            r.PriestInitialHealthPercent is < 1m or > 100m ||
            r.PriestHealingCritPercent is < 0m or > 100m ||
            r.PriestSpiritRegenPerSecond < 0m || r.PriestMp5 < 0m ||
            r.MeditationCastingSpiritFraction is < 0m or > 1m ||
            r.HealingThreatCoefficient < 0m ||
            r.DivineAegisFraction is < 0m or > 1m ||
            r.LitanyRefundFraction is < 0m or > 1m ||
            r.ForecastHorizonSeconds is < 0m or > 10m ||
            r.MinimumSecondaryTargetManaPercent is < 0m or > 100m ||
            r.RaidPulseDamage < 0m ||
            r.RaidPulseIntervalSeconds <= 0m ||
            r.RaidPulseFirstSeconds < 0m ||
            r.RaidPulseTargetCount is < 0 or > 4 ||
            (r.RaidPulseDamage > 0m && !r.EnableExtendedPriest) ||
            (r.UseSpecializationHealingPolicy &&
                (!r.UseAdaptiveHealingPolicy || !r.EnableExtendedPriest ||
                 r.PriestCharacter is null ||
                 !(string.Equals(r.PriestCharacter.SpecializationKey, "holy",
                      StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(r.PriestCharacter.SpecializationKey, "discipline",
                      StringComparison.OrdinalIgnoreCase)))) ||
            (r.UseCharacterDerivedPriestStats && r.PriestCharacter is null) ||
            (r.UsePriestTalentBuild && r.PriestTalents is null))
            throw new ArgumentException("Invalid healer preview settings.");
    }

    // A deterministic control scenario deliberately isolates healing and cast
    // decisions from the uncalibrated Forever avoidance/critical formulas.
    private sealed class GuaranteedHitResolver : ICombatRollResolver
    {
        public CombatRollResult Resolve(SimulationContext context,
            SimulationActorState source, SimulationActorState target,
            AbilityDefinition ability, AbilityEffectDefinition effect) => CombatRollResult.Hit();
    }

    private sealed class ControlledHealingCritResolver(decimal chance,
        PriestMajorCooldownProcessor major) : ICombatRollResolver
    {
        public CombatRollResult Resolve(SimulationContext context,
            SimulationActorState source, SimulationActorState target,
            AbilityDefinition ability, AbilityEffectDefinition effect)
            => Resolve(context, source, target, ability, effect, CombatEffectDeliveryType.Direct);

        public CombatRollResult Resolve(SimulationContext context,
            SimulationActorState source, SimulationActorState target,
            AbilityDefinition ability, AbilityEffectDefinition effect,
            CombatEffectDeliveryType deliveryType)
        {
            var empowered = major.IsInnerFocusEmpowering(ability) &&
                deliveryType == CombatEffectDeliveryType.Direct &&
                effect.EffectType == AbilityEffectTypes.DirectHealing;
            var power = major.PowerInfusionActive(context) ? 1.2m : 1m;
            if (effect.CanCrit &&
                (effect.EffectType == AbilityEffectTypes.DirectHealing ||
                 effect.EffectType == AbilityEffectTypes.PeriodicHealing) &&
                context.Random.NextDouble() * 100.0 < (double)Math.Min(100m,
                    chance + (empowered ? 25m : 0m)))
                return CombatRollResult.Critical(1.5m * power);
            return new CombatRollResult { Landed = true, ResultKey = CombatResultTypes.Hit,
                AmountMultiplier = power };
        }
    }

    private sealed class NoDamageMitigation : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext context,
            SimulationActorState source, SimulationActorState target,
            AbilityDefinition ability, AbilityEffectDefinition effect,
            decimal rawAmount) => DamageMitigationResult.Unmitigated(rawAmount);
    }
}

public sealed class HolyPriestPreviewResponse
{
    public bool TankSurvived { get; init; }
    public decimal TankEndingHealth { get; init; }
    public decimal PriestEndingMana { get; init; }
    public decimal HealingDone { get; init; }
    public decimal OverhealingDone { get; init; }
    public decimal TankDamageTaken { get; init; }
    public int FlashHealCasts { get; init; }
    public int GreaterHealCasts { get; init; }
    public Dictionary<string, int> PriestSpellCasts { get; init; } = new();
    public decimal AbsorptionDone { get; init; }
    public decimal HealingThreatOnBoss { get; init; }
    public ForeverPriestCalculatedStats? DerivedPriestStats { get; init; }
    public decimal PriestMaximumManaUsed { get; init; }
    public decimal PriestHealingCritPercentUsed { get; init; }
    public decimal RaidDamageTaken { get; init; }
    public int RaidMemberDeaths { get; init; }
    public SimulationRunResult Run { get; init; } = new();
}
