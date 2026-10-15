using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Stats;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Healing;
using WarcraftSim.Data.Forever.Priest;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Attaches the existing CP127.3 Priest spellbook and specialization policy to
/// an already-running encounter context. Reuses the encounter's executor,
/// aura manager, absorption pool and threat table; no second event engine.
/// </summary>
internal sealed class PriestCombatIntegration
{
    private readonly HolyPriestPreviewRequest _request;
    private readonly ForeverPriestCalculatedStats _derived;
    private readonly decimal _maximumMana;
    private readonly decimal _crit;
    private readonly decimal _spiritRegen;
    private readonly decimal _mp5;
    private HealerPriorityProcessor? _healer;
    private FiveSecondManaRegenerationProcessor? _mana;

    public decimal HealingCritPercent => _crit;

    public PriestCombatIntegration(HolyPriestPreviewRequest request)
    {
        _request = request;
        int rank(string key) => request.PriestTalents.Rank(key);
        _derived = ForeverPriestCharacterCalculator.Calculate(
            request.PriestCharacter!, request.PriestStatAssumptions,
            rank("mental-strength"), rank("spiritual-guidance"));
        _maximumMana = _derived.MaximumMana;
        _crit = _derived.HealingCritPercent;
        _spiritRegen = _derived.SpiritRegenerationPerSecond;
        _mp5 = _derived.ManaPerFiveSeconds;
    }

    public void CreateActors(SimulationContext context)
    {
        var priest = new SimulationActorState
        {
            Key = "priest", Name = "Level 60 Priest", TeamKey = "raid", Level = 60,
            MaximumHealth = _request.HealerMaximumHealth,
            Stats = new StatCollection
            {
                Values = new Dictionary<string, decimal>(
                    _derived.EffectiveStats, StringComparer.OrdinalIgnoreCase)
            }
        };
        priest.InitializeHealth(priest.MaximumHealth,
            priest.MaximumHealth * _request.PriestInitialHealthPercent / 100m);
        priest.AddResource(new ResourceState("mana", _maximumMana,
            _maximumMana, _request.PriestManaRegenPerSecond));
        context.AddActor(priest);
        for (var index = 1; index <= _request.AdditionalRaidMembers; index++)
        {
            var ally = new SimulationActorState
            {
                Key = $"raid-{index}", Name = $"raid-{index}", TeamKey = "raid",
                Level = 60, MaximumHealth = _request.RaidMemberMaximumHealth
            };
            ally.InitializeHealth(ally.MaximumHealth,
                ally.MaximumHealth * _request.RaidMemberInitialHealthPercent / 100m);
            context.AddActor(ally);
        }
    }

    public List<ICombatEventProcessor> BuildProcessors(SimulationContext context,
        AbilityExecutor executor, AuraManager auraManager, AbsorbManager absorbs)
    {
        var priest = context.GetActor("priest")!;
        int rank(string key) => _request.PriestTalents.Rank(key);
        var tuning = new ForeverPriestHealingTuning
        {
            FlashHealAmount = _request.FlashHealAmount,
            FlashHealCost = _request.FlashHealManaCost,
            GreaterHealAmount = _request.GreaterHealAmount,
            GreaterHealCost = _request.GreaterHealManaCost,
            RenewTickAmount = _request.RenewTickAmount,
            ShieldAbsorbAmount = _request.ShieldAbsorbAmount,
            PrayerOfMendingAmount = _request.PrayerOfMendingAmount,
            PrayerOfHealingAmount = _request.PrayerOfHealingAmount,
            BindingHealAmount = _request.BindingHealAmount,
            PenanceTickAmount = _request.PenanceTickAmount,
            ImprovedRenewRanks = rank("improved-renew"),
            ImprovedShieldRanks = rank("improved-power-word-shield"),
            SpiritualHealingRanks = rank("spiritual-healing"),
            DivineFuryRanks = rank("divine-fury"),
            ImprovedHealingRanks = rank("improved-healing"),
            TwinDisciplinesRanks = rank("twin-disciplines"),
            SoulWarding = rank("soul-warding") > 0,
            MentalAgilityRanks = rank("mental-agility"),
            BonusHealingPower = _derived.HealingPower,
            UseLegacySpiritualHealingScaling = false,
            UseLegacyImprovedShieldScaling = false,
            RestrictTalentAbilities = true,
            HasPenanceTalent = rank("penance") > 0,
            HasBindingHealTalent = rank("binding-heal") > 0,
            HasPrayerOfMendingTalent = rank("prayer-of-mending") > 0
        };
        foreach (var spell in ForeverPriestHealingSpellbook.Create(tuning))
            priest.AddAbility(spell);
        var rules = HolyPriestPreviewRunner.CreateSpecializationRules(_request, priest);
        foreach (var rule in rules)
            if (string.Equals(rule.RequiredTargetActorKey, "tank", StringComparison.OrdinalIgnoreCase))
                rule.RequiredTargetActorKey = "warrior";
        _healer = new HealerPriorityProcessor(new HealerPriorityDefinition
        {
            HealerActorKey = "priest", EligibleTeamKey = "raid",
            DecisionIntervalSeconds = .25m, Rules = rules,
            ForecastTargetActorKey = "warrior",
            ForecastIncomingDamagePerSecond =
                _request.BossSwingDamage / _request.BossSwingIntervalSeconds,
            ForecastSeconds = _request.ForecastHorizonSeconds
        }, executor);
        var meditate = rank("meditation") switch
        {
            0 => 0m, 1 => .17m, 2 => .33m, _ => .50m
        };
        _mana = new FiveSecondManaRegenerationProcessor("priest",
            _spiritRegen, _mp5, meditate);
        _mana.Initialize(context);
        var processors = new List<ICombatEventProcessor>
        {
            _mana,
            new ThreatManager(new HealingThreatResolver(_request.HealingThreatCoefficient))
        };
        if (_request.UsePrayerOfMending && priest.Abilities.ContainsKey("prayer-of-mending"))
        {
            var scale = rank("spiritual-healing") switch
            {
                0 => 1m, 1 => 1.03m, 2 => 1.07m, _ => 1.10m
            };
            processors.Add(new PrayerOfMendingProcessor("priest",
                (_request.PrayerOfMendingAmount + _derived.HealingPower * .25m) * scale,
                auraManager));
        }
        if (_request.UseDivineAegis && rank("divine-aegis") > 0)
            processors.Add(new DivineAegisProcessor("priest",
                rank("divine-aegis") * .05m, absorbs));
        if (rank("litany-of-light") > 0)
            processors.Add(new AlternatingHealManaRefundProcessor("priest",
                rank("litany-of-light") * .05m));
        processors.Add(_healer);
        return processors;
    }

    public void ScheduleFirst(SimulationContext context)
    {
        if (_request.RaidPulseDamage > 0m)
            HolyPriestPreviewRunner.ScheduleRaidPulses(context, _request, "target");
        _healer?.ScheduleFirst(context);
    }

    public void Finish(SimulationContext context, SimulationRunResult run)
    {
        var priest = context.GetActor("priest")!;
        priest.RefreshResources(context.Options.DurationSeconds);
        var mana = priest.Resources["mana"];
        run.Summary.ActorSummaries["priest"].EndingResources["mana"] = mana.Current;
        run.CustomMetrics["priest-mana-remaining"] = mana.Current;
        run.CustomMetrics["priest-mana-maximum"] = mana.Maximum;
    }
}

/// <summary>Uses normal Warrior/ruleset rolls for offensive actions, and the
/// CP126 preview's explicit healing crit model for Priest healing events.</summary>
internal sealed class PriestHealingRollResolver(ICombatRollResolver combat, decimal priestCritPercent)
    : ICombatRollResolver
{
    public CombatRollResult Resolve(SimulationContext context, SimulationActorState source,
        SimulationActorState target, AbilityDefinition ability, AbilityEffectDefinition effect) =>
        Resolve(context, source, target, ability, effect, CombatEffectDeliveryType.Direct);

    public CombatRollResult Resolve(SimulationContext context, SimulationActorState source,
        SimulationActorState target, AbilityDefinition ability, AbilityEffectDefinition effect,
        CombatEffectDeliveryType deliveryType)
    {
        if (source.Key == "priest" &&
            effect.EffectType is AbilityEffectTypes.DirectHealing or AbilityEffectTypes.PeriodicHealing)
        {
            if (effect.CanCrit && context.Random.NextDouble() * 100.0 < (double)priestCritPercent)
                return CombatRollResult.Critical(1.5m);
            return CombatRollResult.Hit();
        }
        return combat.Resolve(context, source, target, ability, effect, deliveryType);
    }
}
