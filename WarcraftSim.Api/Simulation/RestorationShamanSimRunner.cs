using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Healing;
using WarcraftSim.Data.Forever.Shaman;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// CP143 level-60 Restoration healing execution path using the same core engine,
/// healer-priority processor and scripted-encounter-damage pipeline as Priest.
/// The encounter is intentionally a synthetic deterministic test scenario.
/// </summary>
public sealed class RestorationShamanSimRunner
{
    public RestorationShamanSimResponse Run(RestorationShamanSimRequest request,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Talents is null || request.Tuning is null ||
            request.DurationSeconds is <= 0m or > 1200m ||
            request.AdditionalAllies is < 0 or > 8 ||
            request.AllyMaximumHealth <= 0m ||
            request.AllyInitialHealthPercent is < 1m or > 100m ||
            request.IncomingDamage < 0m || request.DamageEverySeconds <= 0m ||
            request.CasterIncomingDamage < 0m ||
            request.CasterDamageEverySeconds <= 0m ||
            request.FirstCasterDamageAtSeconds < 0m ||
            request.FirstDamageAtSeconds < 0m || request.AllyPhysicalArmor < 0m ||
            request.TotemicRecallAtSeconds < 0m ||
            request.CallOfElementsTotems is null ||
            request.CallOfAncestorsTotems is null ||
            request.CallOfSpiritsTotems is null ||
            request.ChainHealEligibleTargetKeys is null ||
            request.ChainHealEligibleTargetKeys.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Invalid Restoration healing scenario.");
        request.Talents.Validate(); request.Tuning.Validate();
        var t = request.Tuning;
        var ctx = new SimulationContext(new SimulationRunOptions {
            Seed = request.Seed, DurationSeconds = request.DurationSeconds,
            PrimaryActorKey = "shaman", CaptureTimeline = request.CaptureTimeline,
            MaximumProcessedEvents = 150000, CancellationToken = token
        });
        var increasedHealth = t.MaximumHealth * (1m + request.Talents.Rank("improved-reincarnation") * .02m);
        var shaman = NewActor("shaman", "raid", increasedHealth, increasedHealth);
        shaman.AddResource(new ResourceState("mana", t.MaximumMana, t.MaximumMana));
        var tank = NewActor("tank", "raid", request.AllyMaximumHealth,
            request.AllyMaximumHealth * request.AllyInitialHealthPercent / 100m);
        var enemy = NewActor("boss", "enemy", 1000000m, 1000000m);
        ctx.AddActor(shaman); ctx.AddActor(tank); ctx.AddActor(enemy);
        for (var i = 1; i <= request.AdditionalAllies; i++)
        {
            var ally = NewActor($"raid-{i}", "raid", request.AllyMaximumHealth,
                request.AllyMaximumHealth * request.AllyInitialHealthPercent / 100m);
            ctx.AddActor(ally);
        }
        foreach (var ability in ForeverRestorationShamanSpellbook.Create(request.Talents, t))
            shaman.AddAbility(ability);
        if(request.UseSharedTotemLifecycle)
        {
            var elemental=ForeverElementalShamanSpellbook.Create(
                new ForeverElementalShamanTalents(),new ForeverElementalShamanTuning(),0m);
            foreach(var key in new[] {"searing-totem","mana-spring-totem"})
                shaman.AddAbility(elemental.Single(a=>a.Key==key));
            shaman.AddAbility(ForeverShamanTotemCalls.StrengthOfEarth());
            shaman.AddAbility(ForeverShamanTotemCalls.Stoneskin());
            shaman.AddAbility(ForeverShamanSharedAbilities.CreateWindfuryTotem());
            var focus=1m-.05m*request.Talents.Rank("totemic-focus");
            // The healing-stream spellbook already discounts that one totem.
            foreach(var key in new[] {"searing-totem","mana-spring-totem",
                "strength-of-earth-totem","stoneskin-totem","windfury-totem",
                "mana-tide-totem"})
                if(shaman.Abilities.TryGetValue(key,out var existing))
                    foreach(var cost in existing.Definition.ResourceCosts.Where(x=>x.ResourceKey=="mana"))
                        cost.Amount*=focus;
            var definitions=shaman.Abilities.Values.Select(x=>x.Definition).ToArray();
            shaman.AddAbility(ForeverShamanTotemCalls.Call("call-of-elements",
                definitions,request.CallOfElementsTotems));
            shaman.AddAbility(ForeverShamanTotemCalls.Call("call-of-ancestors",
                definitions,request.CallOfAncestorsTotems));
            shaman.AddAbility(ForeverShamanTotemCalls.Call("call-of-spirits",
                definitions,request.CallOfSpiritsTotems));
            shaman.AddAbility(ForeverShamanTotemCalls.Recall());
        }
        var auras = new AuraManager();
        var rolls = new RestorationHealingRollResolver(t.HealingCritPercent,
            request.Talents.Rank("tidal-mastery"));
        var mitigation = new RestorationNoMitigation();
        var absorbs = new AbsorbManager();
        var executor = new AbilityExecutor(auras, rolls, mitigation, absorbManager: absorbs);
        // Whitelist represents explicit nearby allies in a positioning-free scenario.
        // Null preserves the original unlimited-range preview, rather than fabricating
        // coordinates or distances.
        IReadOnlySet<string>? bounceEligibility = request.ChainHealEligibleTargetKeys.Count == 0
            ? null : new HashSet<string>(request.ChainHealEligibleTargetKeys,
                StringComparer.OrdinalIgnoreCase);
        var healingEffects = new RestorationShamanCombatProcessor(shaman.Key, t, auras,
            request.Talents, bounceEligibility);
        // Route incoming physical hits through the existing mitigation pipeline,
        // preserving raw vs prevented damage attribution in CombatEvent.
        var armorMitigation = new RestorationTrainingArmorMitigation(
            shaman.Key, request.AllyPhysicalArmor, t, request.Talents);
        var manaRegen = new FiveSecondManaRegenerationProcessor(shaman.Key,
            t.SpiritRegenPerSecond, t.Mp5, request.Talents.Rank("mindfulness") / 3m);
        manaRegen.Initialize(ctx);
        var rules = new List<HealerPriorityRule>();
        void add(string key, decimal hp, int minInjured = 1, string? skipAura = null,
            string? requiredTarget = null, bool allowFull = false)
        {
            if (!shaman.Abilities.ContainsKey(key)) return;
            rules.Add(new HealerPriorityRule {
                Priority = rules.Count, AbilityKey = key, CastAtOrBelowHealthPercent = hp,
                MinimumInjuredTeamMembers = minInjured,
                SkipWhenTargetHasAuraKey = skipAura,
                RequiredTargetActorKey = requiredTarget, AllowUninjuredTargets = allowFull,
                MinimumMissingHealth = allowFull ? 0m : 1m
            });
        }
        add("water-shield", 100m, skipAura: "water-shield", requiredTarget: shaman.Key,
            allowFull: true);
        if(request.UseSharedTotemLifecycle)
        {
            void call(bool enabled,string key,IReadOnlyList<string> chosen)
            {
                if(!enabled)return;
                add(key,100m,skipAura:ForeverShamanTotemCalls.SelectedPresetAura(chosen),
                    requiredTarget:shaman.Key,allowFull:true);
            }
            call(request.UseCallOfElements,"call-of-elements",request.CallOfElementsTotems);
            call(request.UseCallOfAncestors,"call-of-ancestors",request.CallOfAncestorsTotems);
            call(request.UseCallOfSpirits,"call-of-spirits",request.CallOfSpiritsTotems);
        }
        // Cast Mana Tide on cooldown for this mechanics preview, not optimized use.
        add("mana-tide-totem", 100m, requiredTarget: shaman.Key, allowFull: true);
        add("nature-swiftness", 100m, skipAura: "nature-swiftness-ready",
            requiredTarget: shaman.Key, allowFull: true);
        // Establish the totem as soon as the Water slot is free; do not wait
        // until all active single-target heals have exhausted the mana pool.
        add("healing-stream-totem", 100m, skipAura: "healing-stream-totem",
            requiredTarget: shaman.Key, allowFull: true);
        add("riptide", 90m, skipAura: "riptide");
        add("chain-heal", 88m, minInjured: 2);
        add("healing-wave", 63m);
        add("lesser-healing-wave", 98m);
        var healer = new HealerPriorityProcessor(new HealerPriorityDefinition {
            HealerActorKey = shaman.Key, EligibleTeamKey = "raid",
            DecisionIntervalSeconds = .25m, Rules = rules
        }, executor);
        var slots=request.UseSharedTotemLifecycle ?
            new ShamanTotemLifecycleProcessor(shaman,auras) : null;
        var fireTotem=request.UseSharedTotemLifecycle ?
            new ShamanFireTotemProcessor(shaman,auras) : null;
        var recall=request.UseSharedTotemLifecycle && request.UseTotemicRecall ?
            new RestorationShamanTotemRecallProcessor(shaman,executor,slots!,
                request.TotemicRecallAtSeconds) : null;
        var processors = new List<ICombatEventProcessor> {
            executor, auras,
            ..(fireTotem is null ? [] : new ICombatEventProcessor[]{fireTotem}),
            ..(slots is null ? [] : new ICombatEventProcessor[]{slots}),
            ..(recall is null ? [] : new ICombatEventProcessor[]{recall}),
            absorbs, new ScriptedEncounterDamageProcessor(armorMitigation, absorbs),
            manaRegen, healingEffects, new ThreatManager(new HealingThreatResolver(.5m *
                (1m - request.Talents.Rank("natural-grace") * .05m))), healer
        };
        var run = new SimulationEngine(processors).Run(ctx, started => {
            healer.ScheduleFirst(started);
            if (request.CasterIncomingDamage > 0m)
                for (decimal at = request.FirstCasterDamageAtSeconds;
                     at < request.DurationSeconds; at += request.CasterDamageEverySeconds)
                    started.ScheduleEvent(new CombatEvent {
                        TimeSeconds = at, Type = CombatEventType.ScriptedDamage,
                        SourceActorKey = enemy.Key, TargetActorKey = shaman.Key,
                        EncounterEventKey = "restoration-caster-pressure",
                        SchoolKey = "physical", MitigationType = DamageMitigationTypes.None,
                        RawAmount = request.CasterIncomingDamage,
                        Description = "Opt-in Water Shield caster hit fixture"
                    });
            if (request.IncomingDamage <= 0m) return;
            for (decimal time = request.FirstDamageAtSeconds;
                time < request.DurationSeconds; time += request.DamageEverySeconds)
            {
                var targets = request.DamageAdditionalAllies
                    ? started.Actors.Values.Where(a => a.Key == "tank" || a.Key.StartsWith("raid-", StringComparison.Ordinal)).ToList()
                    : new List<SimulationActorState> { tank };
                foreach (var actor in targets)
                    started.ScheduleEvent(new CombatEvent {
                        TimeSeconds = time, Type = CombatEventType.ScriptedDamage,
                        SourceActorKey = enemy.Key, TargetActorKey = actor.Key,
                        EncounterEventKey = "restoration-training-pressure",
                        SchoolKey = "physical", MitigationType = request.AllyPhysicalArmor > 0m
                            ? DamageMitigationTypes.Armor : DamageMitigationTypes.None,
                        RawAmount = request.IncomingDamage,
                        Description = "Deterministic healing training damage"
                    });
            }
        });
        shaman.RefreshResources(request.DurationSeconds);
        var summary = run.Summary.ActorSummaries[shaman.Key];
        summary.EndingResources["mana"] = shaman.Resources["mana"].Current;
        run.CustomMetrics["shaman-armor-prevented-damage"] = armorMitigation.PreventedDamage;
        run.CustomMetrics["shaman-healing-stream-pulses"] = healingEffects.HealingStreamPulses;
        run.CustomMetrics["shaman-chain-heal-bounces"] = healingEffects.ChainBounces;
        run.CustomMetrics["shaman-water-shield-mana"] = healingEffects.ShieldManaRestored;
        run.CustomMetrics["shaman-mana-tide-mana"] = healingEffects.ManaTideRestored;
        run.CustomMetrics["shaman-nature-swiftness-heals"] = healingEffects.NatureSwiftnessConsumed;
        run.CustomMetrics["shaman-totem-placements"] = slots?.Placements ?? 0m;
        run.CustomMetrics["shaman-totem-replacements"] = slots?.Replacements ?? 0m;
        run.CustomMetrics["shaman-totem-recall-mana"] = slots?.RecallManaGained ?? 0m;
        return new RestorationShamanSimResponse {
            HealingDone = summary.HealingDone, OverhealingDone = summary.OverhealingDone,
            EndingMana = shaman.Resources["mana"].Current, MaximumMana = t.MaximumMana,
            ManaRestoredFromWaterShield = healingEffects.ShieldManaRestored,
            ManaRestoredFromManaTide = healingEffects.ManaTideRestored,
            ChainHealBounces = healingEffects.ChainBounces,
            ArmorPreventedDamage = armorMitigation.PreventedDamage,
            HealingByAbility = new Dictionary<string, decimal>(summary.HealingDoneByAbility,
                StringComparer.OrdinalIgnoreCase),
            AllyEndingHealth = ctx.Actors.Values.Where(a => a.TeamKey == "raid")
                .ToDictionary(a => a.Key, a => a.CurrentHealth, StringComparer.OrdinalIgnoreCase),
            Run = run,
            ModelingNotes = [
                "CP143 Restoration: healing uses real friendly HP and scripted incoming damage; Chain Heal follows lowest-health-percentage eligible allies in this abstracted proximity-free scenario.",
                "Riptide direct and periodic healing, three-charge Water Shield and Mana Tide returns are modeled. Water Shield procs on caster hits or eligible outgoing nonperiodic critical spells (2% maximum Mana, 3.5-second internal cooldown).",
                "CP166 Stage 4D: Healing Stream affects every eligible living group member on each 2-second pulse, lasts 300 seconds and replaces Mana Tide's Water slot. Location/30-yard range remain unmodeled.",
                "Ancestral Healing rank 1/2/3 grants 8/17/25% target armor for 15 seconds, subject to the shared 75% armor mitigation ceiling. Natural Grace modifies healing threat and Improved Reincarnation increases maximum health.",
                "CP145: Physical scripted damage respects optional ally armor and Ancestral Healing's triggered armor aura; armor rank magnitude and constant remain configurable assumptions.",
                "CP145: Chain Heal bounce eligibility may be explicitly restricted to a named set of allies; no unverified geometric ranges are inferred.",
                "CP147: Healing Focus is recognized at its official 3-point cap, but spell pushback requires an incoming-caster-pressure model not present in this healing preview; it is not classified as connected.",
                "Only mechanically wired talent effects are counted as complete. Noncombat and unsupported talent behaviors remain explicitly deferred.",
                "CP167 opt-in Restoration totem calls share the Elemental/Enhancement four-slot manager; Water-slot healing/mana effects retain their existing combat processor. Terrain and raid-range checks remain deferred."
            ]
        };
    }

    private static SimulationActorState NewActor(string key, string team, decimal maximum, decimal current)
    {
        var actor = new SimulationActorState {
            Key = key, Name = key, TeamKey = team, Level = 60, MaximumHealth = maximum
        };
        actor.InitializeHealth(maximum, current);
        return actor;
    }

    private sealed class RestorationHealingRollResolver(decimal critChance, int mastery) : ICombatRollResolver
    {
        public CombatRollResult Resolve(SimulationContext ctx, SimulationActorState src,
            SimulationActorState target, AbilityDefinition ability, AbilityEffectDefinition effect) =>
            Resolve(ctx, src, target, ability, effect, CombatEffectDeliveryType.Direct);

        public CombatRollResult Resolve(SimulationContext ctx, SimulationActorState src,
            SimulationActorState target, AbilityDefinition ability, AbilityEffectDefinition effect,
            CombatEffectDeliveryType delivery)
        {
            if (effect.EffectType is not (AbilityEffectTypes.DirectHealing or AbilityEffectTypes.PeriodicHealing))
                return CombatRollResult.Hit();
            var isCritical = effect.CanCrit && (decimal)ctx.Random.NextDouble() * 100m <
                Math.Min(100m, critChance + mastery);
            var multiplier = isCritical ? 1.5m : 1m;
            if (ability.Key == "chain-heal" && target.ActiveAuras.Any(a =>
                a.Definition.Key == "riptide" && a.IsActiveAt(ctx.CurrentTimeSeconds)))
                multiplier *= 1.25m;
            return isCritical ? CombatRollResult.Critical(multiplier) :
                new CombatRollResult { Landed = true, IsCritical = false,
                    ResultKey = CombatResultTypes.Hit, AmountMultiplier = multiplier };
        }
    }

    private sealed class RestorationTrainingArmorMitigation : IDamageMitigationResolver
    {
        private readonly string _shaman;
        private readonly decimal _baseArmor;
        private readonly ForeverRestorationShamanTuning _tuning;
        private readonly ForeverRestorationShamanTalents _talents;
        public decimal PreventedDamage { get; private set; }

        public RestorationTrainingArmorMitigation(string shaman, decimal baseArmor,
            ForeverRestorationShamanTuning tuning, ForeverRestorationShamanTalents talents)
        {
            _shaman = shaman; _baseArmor = baseArmor;
            _tuning = tuning; _talents = talents;
        }
        public DamageMitigationResult Resolve(SimulationContext ctx,
            SimulationActorState source, SimulationActorState target,
            AbilityDefinition ability, AbilityEffectDefinition effect, decimal rawAmount)
        {
            if (_baseArmor <= 0m || effect.SchoolKey != "physical")
                return DamageMitigationResult.Unmitigated(rawAmount);
            var ancestral = target.ActiveAuras.Any(a =>
                a.Definition.Key == "ancestral-healing-armor" &&
                a.SourceActorKey == _shaman && a.IsActiveAt(ctx.CurrentTimeSeconds));
            // Client 70532: Ancestral Healing is 8/17/25% armor
            // (not 8% per rank); retain custom calibration overrides.
            var rank = _talents.Rank("ancestral-healing");
            var bonus = !ancestral ? 0m :
                _tuning.AncestralHealingArmorPercentPerRank == 8m
                    ? rank switch { 0=>0m,1=>.08m,2=>.17m,_=>.25m }
                    : rank * _tuning.AncestralHealingArmorPercentPerRank / 100m;
            var armor = _baseArmor * (1m + bonus);
            var prevented = rawAmount *
                ForeverArmorMitigationMath.ClampArmorReductionFraction(
                    armor / (armor + _tuning.ArmorReductionConstant));
            PreventedDamage += prevented;
            return new DamageMitigationResult {
                RawAmount = rawAmount, FinalAmount = rawAmount - prevented,
                MitigatedAmount = prevented, ReductionPercent = rawAmount <= 0m
                    ? 0m : prevented * 100m / rawAmount,
                DefenseValue = armor
            };
        }
    }

    private sealed class RestorationNoMitigation : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext ctx, SimulationActorState src,
            SimulationActorState target, AbilityDefinition ability, AbilityEffectDefinition effect,
            decimal rawAmount) => DamageMitigationResult.Unmitigated(rawAmount);
    }
}
