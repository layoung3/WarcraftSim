using WarcraftSim.Core.Abilities;
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
        priest.AddResource(new ResourceState("mana", request.PriestMaximumMana,
            request.PriestMaximumMana, request.PriestManaRegenPerSecond));
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
                ImprovedRenewRanks = request.ImprovedRenewRanks,
                ImprovedShieldRanks = request.ImprovedShieldRanks,
                SpiritualHealingRanks = request.SpiritualHealingRanks,
                DivineFuryRanks = request.DivineFuryRanks,
                ImprovedHealingRanks = request.ImprovedHealingRanks,
                TwinDisciplinesRanks = request.TwinDisciplinesRanks,
                SoulWarding = request.SoulWarding
            };
            foreach (var spell in ForeverPriestHealingSpellbook.Create(tuning))
                priest.AddAbility(spell);
        }

        var roll = request.EnableExtendedPriest && request.EnableHealingCriticals
            ? (ICombatRollResolver)new ControlledHealingCritResolver(request.PriestHealingCritPercent)
            : new GuaranteedHitResolver();
        var mitigation = new NoDamageMitigation();
        var auraManager = new AuraManager();
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
        var healer = new HealerPriorityProcessor(new HealerPriorityDefinition
        {
            HealerActorKey = priest.Key, EligibleTeamKey = "raid",
            DecisionIntervalSeconds = 0.25m, Rules = rules
        }, executor);

        FiveSecondManaRegenerationProcessor? manaRule = null;
        var processors = new List<ICombatEventProcessor> { executor, auraManager, absorbManager, melee };
        if (request.EnableExtendedPriest)
        {
            manaRule = new FiveSecondManaRegenerationProcessor(priest.Key,
                request.PriestSpiritRegenPerSecond, request.PriestMp5,
                request.MeditationCastingSpiritFraction);
            manaRule.Initialize(context);
            processors.Add(manaRule);
            processors.Add(new ThreatManager(new HealingThreatResolver(request.HealingThreatCoefficient)));
            if (request.UsePrayerOfMending)
                processors.Add(new PrayerOfMendingProcessor(priest.Key,
                    request.PrayerOfMendingAmount, auraManager));
            if (request.UseDivineAegis)
                processors.Add(new DivineAegisProcessor(priest.Key,
                    request.DivineAegisFraction, absorbManager));
            if (request.LitanyRefundFraction > 0m)
                processors.Add(new AlternatingHealManaRefundProcessor(priest.Key,
                    request.LitanyRefundFraction));
        }
        if (request.EnableHealer) processors.Add(healer);
        var run = new SimulationEngine(processors).Run(context, ctx =>
        {
            melee.ScheduleFirst(ctx);
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
            Run = run
        };
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
            r.LitanyRefundFraction is < 0m or > 1m)
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

    private sealed class ControlledHealingCritResolver(decimal chance) : ICombatRollResolver
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
            if (effect.CanCrit &&
                (effect.EffectType == AbilityEffectTypes.DirectHealing ||
                 effect.EffectType == AbilityEffectTypes.PeriodicHealing) &&
                context.Random.NextDouble() * 100.0 < (double)chance)
                return CombatRollResult.Critical(1.5m);
            return CombatRollResult.Hit();
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
    public SimulationRunResult Run { get; init; } = new();
}
