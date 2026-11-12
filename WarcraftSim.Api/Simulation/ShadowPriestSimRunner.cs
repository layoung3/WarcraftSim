using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Healing;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Priest;

namespace WarcraftSim.Api.Simulation;

/// <summary>CP129 first true Shadow DPS actor over the shared Core combat engine.</summary>
public sealed class ShadowPriestSimRunner
{
    public ShadowPriestSimResponse Run(ShadowPriestSimRequest request,
        CancellationToken cancellationToken = default)
    {
        var prepared=Prepare(request,cancellationToken);
        request=prepared.Request;
        var (context,priest,boss,derived,maximumMana,spellPower,crit,baseline,form,baseDamageBonus)=
            (prepared.Context,prepared.Actor,prepared.Target,prepared.Derived,
             prepared.Derived.MaximumMana,prepared.SpellPower,prepared.Crit,prepared.Baseline,
             prepared.Form,prepared.BaseDamageBonus);
        var auraManager = new AuraManager();
        var majorCooldowns = new PriestMajorCooldownProcessor(priest, auraManager);
        var roll = new ShadowPriestRollResolver(
            Math.Min(100m, request.BaseSpellHitPercent + request.Talents.Rank("shadow-focus") * 2m +
                priest.Stats.Get("spell-hit-percent")),
            // The magic critical bonus is 50%, doubled by active Shadowform.
            Math.Clamp(crit, 0m, 100m), form ? 2m : 1.5m, majorCooldowns);
        var executor = new AbilityExecutor(auraManager, roll, new ShadowPriestNoMitigation());
        var rotation = prepared.Rotation;
        var priority = new PriorityRotationExecutor(rotation, "priest", "target", executor,
            reactToTargetStateChanges: true);
        var regen = new FiveSecondManaRegenerationProcessor("priest",
            derived.SpiritRegenerationPerSecond, derived.ManaPerFiveSeconds,
            request.ManaRegenerationWhileCastingFraction);
        var reactive = new ForeverPriestShadowReactiveProcessor("priest", auraManager,
            request.Talents, request.Tuning, baseDamageBonus);
        // Must precede AbilityExecutor so each channel tick uses its own growth
        // multiplier rather than a static bonus on every Mind Flay tick.
        var progressiveFlay = new ShadowPriestProgressiveMindFlayProcessor(
            priest, request.Talents.Rank("improved-mind-flay"));
        var engine = new SimulationEngine([progressiveFlay, executor, auraManager,
            majorCooldowns, reactive, regen, new ThreatManager(new PriestShadowDamageThreat()), priority]);
        regen.Initialize(context);
        var run = engine.Run(context);
        var actorSummary = run.Summary.ActorSummaries["priest"];
                return new ShadowPriestSimResponse
        {
            Dps = actorSummary.DamageDone / request.DurationSeconds,
            DamageDone = actorSummary.DamageDone,
            HealingDone = actorSummary.HealingDone,
            EndingMana = priest.Resources["mana"].Current,
            MaximumMana = maximumMana, MaximumHealth = priest.MaximumHealth, EndingHealth = priest.CurrentHealth,
            SpellPower = spellPower, SpellCritPercent = crit,
            BossEndingHealthPercent = boss.MaximumHealth > 0m ?
                boss.CurrentHealth * 100m / boss.MaximumHealth : 0m,
            MaximumShadowWeavingStacks = reactive.MaximumStacksReached,
            DamageByAbility = new Dictionary<string, decimal>(actorSummary.DamageDoneByAbility,
                StringComparer.OrdinalIgnoreCase),
            PriestDied = !priest.IsAlive, Run = run,
            ModelingNotes =
            [
                BaselineNote(baseline),
                "CP129: spell bases/mana costs, level-63 hit and regen are explicit provisional values.",
                "70532 Shadow Weaving has three 33/67/100% ranks; SW:D uses 18s client spell cooldown and 10% of caster maximum HP backlash; Vampiric Embrace uses 30s client spell cooldown. September notes differ.",
                "Shadow Weaving modifies only the Priest's shadow damage, not raid-wide damage.",
                "Shadowform is assumed active from the pull when talented; damage, mana and critical bonus are applied.",
                "Periodic damage is crit-enabled; proc chance and internal RNG use the configured seed.",
                "Devouring Contagion cost reduction works; on-death multi-target plague spread is deferred.",
                "No trinket procs, wand auto-attacks, boss resistances or raid buffs are modeled in this first Shadow preview."
            ]
        };
    }

    // Exact isolated setup; read-only previews never execute combat events.
    internal static ShadowPrecombatPreparation Prepare(ShadowPriestSimRequest request,
        CancellationToken cancellationToken=default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request = ShadowPriestSimulationRequestSnapshot.Create(request);
        if (request.DurationSeconds <= 0m || request.DurationSeconds > 1200m ||
            request.TargetMaximumHealth <= 0m || request.PriestMaximumHealth <= 0m ||
            request.TargetInitialHealthPercent is <= 0m or > 100m ||
            request.BaseSpellHitPercent is < 0m or > 100m ||
            request.ManaRegenerationWhileCastingFraction is < 0m or > 1m ||
            request.SpellCritOverridePercent > 100m || request.SpellPowerOverride < -1m)
            throw new ArgumentException("Invalid Shadow Priest encounter parameters.");
        if (request.Character is null || request.Talents is null || request.Tuning is null ||
            request.StatAssumptions is null)
            throw new ArgumentException("Shadow Priest requires character, talents, tuning and stat assumptions.");
        if (request.Tuning.ShadowWordDeathBacklashFraction is < 0m or > 1m)
            throw new ArgumentException("SW:D maximum-health backlash fraction must be 0–1.");
        if (request.Character.Level != 60 || request.Character.SpecializationKey != "shadow")
            throw new ArgumentException("CP129 supports a level-60 Shadow Priest character.");
        request.Talents.Validate(request.Character.Level);

        var baseline = ForeverLevel60Baseline.Resolve(request.Level60Baseline,
            request.UseReviewedLevel60Baseline, request.Character.Race ?? "", "priest",
            request.UseProvisionalLevel60Baseline);
        if (baseline is not null)
        {
            if (request.Character.Equipment.Items.Any(item => item.ItemId.HasValue ||
                item.EnchantSpellId.HasValue || item.TemporaryEnchantSpellId.HasValue))
                throw new ArgumentException("Prepared Shadow catalog equipment and enchants are not enabled; use explicitly authored stat budgets.");
            request.Character = CasterSimulationRequestSnapshot.GenerateBaseline(request.Character, baseline);
        }
        var derived = ForeverPriestCharacterCalculator.Calculate(request.Character,
            request.StatAssumptions,
            observedNakedMaximumMana: baseline?.ObservedUnbuffedMaximumMana,
            nakedIntellect: baseline?.ObservedUnbuffedMaximumMana.HasValue == true ? baseline.Intellect : null);
        var maximumMana = derived.MaximumMana;
        var maximumHealth = request.PriestMaximumHealth;
        if (baseline is not null)
        {
            maximumHealth = (baseline.ObservedUnbuffedMaximumHealth ?? maximumHealth) +
                10m * (derived.EffectiveStats.GetValueOrDefault("stamina") - baseline.Stamina) +
                derived.EffectiveStats.GetValueOrDefault("health");
            if (maximumMana <= 0m || maximumHealth <= 0m)
                throw new ArgumentException("Prepared Shadow maximum Mana and Health must be positive.");
        }
        var spellPower = request.SpellPowerOverride >= 0m ? request.SpellPowerOverride :
            Math.Max(0m, derived.EffectiveStats.GetValueOrDefault("spell-power") +
                derived.EffectiveStats.GetValueOrDefault("shadow-power"));
        var crit = request.SpellCritOverridePercent >= 0m ? request.SpellCritOverridePercent :
            derived.HealingCritPercent;
        var context = new SimulationContext(new SimulationRunOptions
        {
            Seed = request.Seed, DurationSeconds = request.DurationSeconds,
            PrimaryActorKey = "priest", CaptureTimeline = request.CaptureTimeline,
            MaximumProcessedEvents = 30000, CancellationToken = cancellationToken
        });
        var priest = new SimulationActorState
        {
            Key = "priest", Name = request.Character.Name, TeamKey = "raid",
            Level = 60, MaximumHealth = maximumHealth,
            Stats = new StatCollection { Values = new Dictionary<string, decimal>(
                derived.EffectiveStats, StringComparer.OrdinalIgnoreCase) }
        };
        priest.InitializeHealth(priest.MaximumHealth);
        priest.AddResource(new ResourceState("mana", maximumMana, maximumMana));
        var boss = new SimulationActorState
        {
            Key = "target", Name = "Level 63 target (Shadow preview)", TeamKey = "enemy",
            Level = 63, MaximumHealth = request.TargetMaximumHealth
        };
        boss.InitializeHealth(request.TargetMaximumHealth,
            request.TargetMaximumHealth * request.TargetInitialHealthPercent / 100m);
        context.AddActor(priest);
        context.AddActor(boss);
        var form = request.Talents.Rank("shadowform") > 0;
        // Darkness and active Shadowform are independent school multipliers.
        var baseDamageBonus = 100m * ((1m + request.Talents.Rank("darkness") * .02m) *
            (form ? 1.10m : 1m) - 1m);
        priest.Stats.Set("shadow-damage-bonus-percent", baseDamageBonus);
        foreach (var ability in ForeverPriestShadowSpellbook.Create(request.Tuning,
            request.Talents, spellPower)) priest.AddAbility(ability);
        if (request.Talents.Rank("inner-focus") > 0)
            priest.AddAbility(ForeverPriestMajorCooldowns.InnerFocus());
        if (request.Talents.Rank("power-infusion") > 0)
            priest.AddAbility(ForeverPriestMajorCooldowns.PowerInfusion());

        return new(request,context,priest,boss,derived,spellPower,crit,baseline,
            form,baseDamageBonus,BuildRotation(request,priest));
    }

    private static string BaselineNote(ForeverLevel60BaselineObservation? baseline)=>
        baseline is null ? "Legacy authored Shadow inputs; no naked baseline selected." :
                    $"Prepared Shadow baseline: {baseline.VerificationStatus}; build {baseline.ClientBuild}; source {baseline.SourceReference}. " +
                    (baseline.ObservedUnbuffedMaximumMana.HasValue ? "Supplied naked Mana plus Priest Intellect and flat Mana deltas. " :
                        "Mana uses explicit Priest stat assumptions, including the first twenty Intellect points; not observed naked Mana. ") +
                    (baseline.ObservedUnbuffedMaximumHealth.HasValue ? "Supplied naked Health plus authored contributions. " :
                        "Health uses the manual scenario cap plus provisional post-baseline Stamina and flat Health deltas; not observed naked Health. ") +
                    (baseline.VerificationStatus == "provisional-documented-proxy" ? "Historical proxy values are NOT verified for Forever. " :
                        "User-supplied provenance is not server attestation. ") +
                    "Catalog equipment is not enabled; racial passives and combat coefficients are not certified by this baseline.";

    public static ShadowPrecombatPreview Preview(ShadowPriestSimRequest request)
    {
        var prepared=Prepare(request);
        var stats=new StatCollection {Values=new(prepared.Actor.Stats.Values,
            prepared.Actor.Stats.Values.Comparer)};
        stats.Set("health",prepared.Actor.MaximumHealth);
        stats.Set("mana",prepared.Derived.MaximumMana);
        stats.Set("spell-power",prepared.SpellPower);
        stats.Set("spell-hit-chance-percent",Math.Min(100m,prepared.Request.BaseSpellHitPercent+
            prepared.Request.Talents.Rank("shadow-focus")*2m+prepared.Actor.Stats.Get("spell-hit-percent")));
        stats.Set("spell-critical-chance-percent",Math.Clamp(prepared.Crit,0m,100m));
        return new(stats,prepared.Derived.MaximumMana,prepared.Actor.MaximumHealth,
            prepared.SpellPower,prepared.Baseline is null ? null : ForeverLevel60Baseline.Summary(prepared.Baseline),
            prepared.Baseline?.VerificationStatus??"explicit-user-profile-not-client-verified",
            [BaselineNote(prepared.Baseline),
                "Display-only exact Shadow precombat snapshot. Spell power includes the explicit override or spell-power plus shadow-power; crit includes the explicit override or Priest derived crit. Ability-specific crit and dynamic procs are excluded. Fixed toggle-driven priority only; no custom rotation, shared combat items, selected external modifiers or equipment haste execution. Existing Priest regeneration assumptions remain provisional, not current-client certification."],
            prepared.Rotation,prepared.Derived.SpiritRegenerationPerSecond,prepared.Derived.ManaPerFiveSeconds);
    }

    private static RotationProfile BuildRotation(ShadowPriestSimRequest request, SimulationActorState priest)
    {
        var rotation = new RotationProfile
        {
            Name = "Shadow Priest — CP129 priority", ClassKey = "priest",
            RulesetKey = "wow-forever", SimulationType = SimulationType.Dps
        };
        void add(int priority, string key, params RotationConditionDefinition[] conditions)
        {
            if (!priest.Abilities.ContainsKey(key)) return;
            rotation.Entries.Add(new RotationEntry
            {
                Priority = priority, AbilityKey = key, Conditions = conditions.ToList()
            });
        }
        static RotationConditionDefinition missing(string key) => new()
        {
            ConditionType = RotationConditionTypes.TargetAuraMissing, Key = key
        };
        if (request.UseInnerFocus) add(-2, "inner-focus");
        if (request.UsePowerInfusion) add(-1, "power-infusion");
        if (request.UseVampiricEmbrace)
            add(0, "vampiric-embrace", missing("vampiric-embrace"));
        if (request.UseShadowWordPain)
            add(1, "shadow-word-pain", missing("shadow-word-pain"));
        if (request.UseDevouringPlague)
            add(2, "devouring-plague", missing("devouring-plague"));
        if (request.UseShadowWordDeathExecute)
            add(3, "shadow-word-death", new RotationConditionDefinition
            {
                ConditionType = RotationConditionTypes.TargetHealthPercent,
                ComparisonOperator = RotationComparisonOperators.LessThanOrEqual, Value = 20m
            });
        if (request.UseMindBlast) add(4, "mind-blast");
        if (request.UseMindFlay) add(5, "mind-flay");
        return rotation;
    }

    private sealed class PriestShadowDamageThreat : IThreatGenerationResolver
    {
        public IReadOnlyList<ThreatGenerationContribution> Resolve(
            SimulationContext context, CombatEvent e)
        {
            if (e.Type != CombatEventType.Damage || e.SourceActorKey != "priest" ||
                e.TargetActorKey is null || e.Amount is not > 0m ||
                context.GetActor(e.TargetActorKey) is not { TeamKey: "enemy" })
                return [];
            // Current Forever removed Shadow Affinity, so use ordinary
            // damage-based threat. SW:D self-backlash is not enemy threat.
            return [new ThreatGenerationContribution {
                ThreatOwnerActorKey = e.TargetActorKey,
                ThreatSourceActorKey = "priest",
                Amount = e.Amount.Value
            }];
        }
    }

    private sealed class ShadowPriestRollResolver(decimal hit, decimal crit,
        decimal criticalMultiplier, PriestMajorCooldownProcessor major) : ICombatRollResolver
    {
        public CombatRollResult Resolve(SimulationContext context, SimulationActorState source,
            SimulationActorState target, AbilityDefinition ability, AbilityEffectDefinition effect) =>
            Resolve(context, source, target, ability, effect, CombatEffectDeliveryType.Direct);
        public CombatRollResult Resolve(SimulationContext context, SimulationActorState source,
            SimulationActorState target, AbilityDefinition ability, AbilityEffectDefinition effect,
            CombatEffectDeliveryType deliveryType)
        {
            if (effect.CanMiss && (decimal)context.Random.NextDouble() * 100m >= hit)
                return CombatRollResult.Miss();
            var power = major.PowerInfusionActive(context) ? 1.2m : 1m;
            if (!effect.CanCrit) return new CombatRollResult {
                Landed=true, ResultKey=CombatResultTypes.Hit, AmountMultiplier=power };
            var bonus = effect.BonusCriticalChancePercent;
            if (deliveryType == CombatEffectDeliveryType.Direct &&
                major.IsInnerFocusEmpowering(ability) &&
                effect.EffectType == AbilityEffectTypes.DirectDamage)
                bonus += 25m;
            if (ability.Key == "shadow-word-death" && target.MaximumHealth > 0m &&
                target.CurrentHealth * 100m / target.MaximumHealth > 20m)
                bonus = 0m;
            if ((decimal)context.Random.NextDouble() * 100m < Math.Min(100m, crit + bonus))
                return CombatRollResult.Critical(criticalMultiplier * power);
            return new CombatRollResult {Landed=true, ResultKey=CombatResultTypes.Hit,
                AmountMultiplier=power};
        }
    }

    private sealed class ShadowPriestNoMitigation : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext context,
            SimulationActorState source, SimulationActorState target,
            AbilityDefinition ability, AbilityEffectDefinition effect, decimal rawAmount) =>
            DamageMitigationResult.Unmitigated(rawAmount);
    }
}

internal sealed record ShadowPrecombatPreparation(ShadowPriestSimRequest Request,
    SimulationContext Context,SimulationActorState Actor,SimulationActorState Target,
    ForeverPriestCalculatedStats Derived,decimal SpellPower,decimal Crit,
    ForeverLevel60BaselineObservation? Baseline,bool Form,decimal BaseDamageBonus,RotationProfile Rotation);
public sealed record ShadowPrecombatPreview(StatCollection EffectiveStats,decimal MaximumMana,
    decimal MaximumHealth,decimal SpellPower,object? Baseline,string SourceStatus,
    IReadOnlyList<string> ModelingNotes,RotationProfile Rotation,
    decimal SpiritRegenerationPerSecond,decimal ManaPerFiveSeconds)
{
    public decimal HardcastHastePercent=>0m;
    public string StatPreviewScope=>"shadow-exact-precombat-display-only-not-current-client-certified";
    public string RotationPreviewScope=>"fixed-toggle-priority-no-custom-rotation";
}
