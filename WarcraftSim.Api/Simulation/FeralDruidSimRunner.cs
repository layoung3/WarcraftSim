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
    public int AdditionalTargets { get; set; }
    public List<FeralDruidFormTransition> FormTransitions { get; set; } = [];
    // Solo Bear tank mode. Boss hit events are scored without consuming player
    // health, matching the existing fixed-duration Protection Warrior approach.
    public bool EnableBossAttacks { get; set; }
    public decimal BossHitDamage { get; set; } = 850m;
    public decimal BossSwingSeconds { get; set; } = 2.5m;
    public decimal BearBaseArmor { get; set; } = 4000m;
    public decimal BearDodgePercent { get; set; } = 5m;
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
            request.BossHitDamage < 0m || request.BossSwingSeconds <= 0m ||
            request.BearBaseArmor < 0m || request.BearDodgePercent is < 0m or > 100m ||
            request.TankBurstWindowSeconds <= 0m ||
            request.FormTransitions is null ||
            request.FormTransitions.Any(x => x is null || x.AtSeconds < 6m ||
                x.AtSeconds >= request.DurationSeconds ||
                x.Form is not ("cat" or "bear" or "caster" or "moonkin")))
            throw new ArgumentException("Invalid Feral simulation request.");
        request.Talents.Validate(); request.Tuning.Validate();
        var cat = request.Role == "cat";
        var form = cat ? ForeverDruidForm.Cat : ForeverDruidForm.DireBear;
        var ctx = new SimulationContext(new SimulationRunOptions {
            Seed = request.Seed, DurationSeconds = request.DurationSeconds,
            PrimaryActorKey = "druid", CaptureTimeline = request.CaptureTimeline,
            MaximumProcessedEvents = 150000, CancellationToken = token
        });
        var druid = new SimulationActorState { Key = "druid", Name = "Feral Druid",
            TeamKey = "raid", Level = 60, MaximumHealth = request.MaximumHealth };
        druid.InitializeHealth(request.MaximumHealth);
        druid.AddResource(new ResourceState("mana", request.MaximumMana, request.MaximumMana));
        druid.AddResource(new ResourceState("energy", 100m, cat ? 0m : 0m));
        druid.AddResource(new ResourceState("rage", 100m, cat ? 0m : 20m));
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
        foreach (var ability in ForeverFeralDruidSpellbook.Create(request.Talents, request.Tuning))
            druid.AddAbility(ability);
        var rotation = new RotationProfile { Name = "Feral mechanics preview", ClassKey = "druid",
            RulesetKey = "wow-forever", SimulationType = SimulationType.Dps };
        var catPriorities = new[] { "shifting-power", "rip", "ferocious-bite", "rake", "shred", "claw" };
        var bearPriorities = request.AdditionalTargets > 0
            ? new[] { "enrage", "swipe", "primal-bite", "lacerate", "maul" }
            : new[] { "enrage", "primal-bite", "lacerate", "maul", "swipe" };
        var priority = request.FormTransitions.Count == 0
            ? (cat ? catPriorities : bearPriorities)
            : catPriorities.Concat(bearPriorities).ToArray();
        var index = 0;
        foreach (var ability in priority.Where(x => druid.Abilities.ContainsKey(x)))
            rotation.Entries.Add(new RotationEntry { Priority = index++, AbilityKey = ability });
        var auras = new AuraManager();
        FeralDruidCombatProcessor? mechanics = null;
        var rolls = new FeralRolls(request, () => mechanics);
        var executor = new AbilityExecutor(auras, rolls, new Unmitigated());
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
        mechanics = new FeralDruidCombatProcessor(druid, request, rotation, formState, auras,
            AttemptFormChange);
        var decisions = new PriorityRotationExecutor(rotation, "druid", "target", executor, true);
        var swipeCleave = new FeralSwipeCleaveProcessor(druid.Key, executor);
        var tank = new FeralDruidTankMetricsProcessor("druid", "target",
            request.TankBurstWindowSeconds, () => formState.Form is
                ForeverDruidForm.Bear or ForeverDruidForm.DireBear,
            request.Tuning.BearBaseThreatMultiplier);
        decimal armorPreventedDamage = 0m;
        decimal dodgedHits = 0m;
        decimal ResolveIncoming(SimulationContext context, SimulationActorState defender,
            CombatEvent ev, decimal raw)
        {
            if (defender.Key != druid.Key ||
                formState.Form is not (ForeverDruidForm.Bear or ForeverDruidForm.DireBear))
                return raw;
            if ((decimal)context.Random.NextDouble() * 100m < request.BearDodgePercent)
            {
                dodgedHits++;
                return 0m;
            }
            var armor = request.BearBaseArmor * (1m + request.Talents.Rank("thick-hide") *
                request.Tuning.ThickHideArmorPercentPerRank / 100m);
            var reduced = raw * request.Tuning.TankArmorReductionConstant /
                (armor + request.Tuning.TankArmorReductionConstant);
            armorPreventedDamage += raw - reduced;
            return reduced;
        }
        var incoming = new ScriptedEncounterDamageProcessor(new Unmitigated(),
            preserveHealth: actor => request.EnableBossAttacks && actor.Key == druid.Key,
            modifyRawDamage: ResolveIncoming);
        var engine = new SimulationEngine([executor, auras, auto, incoming, mechanics,
            swipeCleave, tank, decisions]);
        var run = engine.Run(ctx, started => {
            if (!formState.Shift(druid, form, started.CurrentTimeSeconds))
                throw new InvalidOperationException("Unable to shift into selected Feral form.");
            auras.ApplyAura(started, druid, new AuraDefinition { Key = ForeverDruidFormState.AuraKey(form),
                Name = $"{form} Form", DurationSeconds = 3600m }, druid.Key, "shapeshift", "form-state");
            if (!auto.Start(started, "druid", "target", attacks))
                throw new InvalidOperationException("Feral auto attack stream failed to start.");
            if (request.EnableBossAttacks)
                for (var at = request.BossSwingSeconds; at < request.DurationSeconds;
                     at += request.BossSwingSeconds)
                    started.ScheduleEvent(new CombatEvent {
                        Type = CombatEventType.ScriptedDamage, TimeSeconds = at,
                        SourceActorKey = "target", TargetActorKey = "druid",
                        EncounterEventKey = "feral-boss-melee", SchoolKey = "physical",
                        MitigationType = DamageMitigationTypes.None,
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
        run.CustomMetrics["feral-combo-points"] = mechanics.ComboPoints;
        run.CustomMetrics["feral-lacerate-stacks"] = mechanics.LacerateStacks;
        run.CustomMetrics["feral-omen-procs"] = mechanics.OmenProcs;
        run.CustomMetrics["feral-clearcasts-used"] = mechanics.ClearcastsUsed;
        run.CustomMetrics["feral-failed-form-shifts"] = failedShifts;
        run.CustomMetrics["bear-incoming-damage"] = tank.TotalIncoming;
        run.CustomMetrics["bear-max-burst-damage"] = tank.MaximumBurst;
        run.CustomMetrics["bear-threat-generated"] = tank.Threat;
        run.CustomMetrics["bear-shared-threat-table"] = target.ThreatTable.GetThreat(druid.Key);
        run.CustomMetrics["bear-swipe-secondary-hits"] = swipeCleave.SecondaryHits;
        run.CustomMetrics["bear-armor-prevented-damage"] = armorPreventedDamage;
        run.CustomMetrics["bear-dodged-hits"] = dodgedHits;
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
                request.Tuning.MeleeCritPercent + request.Talents.Rank("sharpened-claws") * 2m;
            var multiplier = mechanics()?.DamageMultiplier(ability.Key, delivery) ?? 1m;
            return new CombatRollResult { Landed = true, IsCritical = critical,
                ResultKey = critical ? CombatResultTypes.Critical : CombatResultTypes.Hit,
                AmountMultiplier = multiplier * (critical ? 2m : 1m) };
        }
    }
    private sealed class Unmitigated : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext ctx, SimulationActorState src,
            SimulationActorState target, AbilityDefinition ability, AbilityEffectDefinition effect,
            decimal rawAmount) => DamageMitigationResult.Unmitigated(rawAmount);
    }
}

internal sealed class FeralDruidCombatProcessor : ICombatEventProcessor
{
    private readonly SimulationActorState _druid;
    private readonly FeralDruidSimRequest _request;
    private readonly RotationProfile _rotation;
    private readonly ForeverDruidFormState _form;
    private readonly AuraManager _auras;
    private readonly Action<SimulationContext, ForeverDruidForm>? _shiftTo;
    private readonly HashSet<Guid> _generators = [];
    private readonly HashSet<Guid> _finishers = [];
    private int _lastFinisherPoints;
    private decimal _nextOmen;
    private readonly Dictionary<string, decimal> _baseCosts;
    public int ComboPoints { get; private set; }
    public int LacerateStacks { get; private set; }
    public int OmenProcs { get; private set; }
    public int ClearcastsUsed { get; private set; }
    public FeralDruidCombatProcessor(SimulationActorState druid,FeralDruidSimRequest request,
        RotationProfile rotation,ForeverDruidFormState form,AuraManager auras,
        Action<SimulationContext, ForeverDruidForm>? shiftTo = null)
    {
        _druid = druid; _request = request; _rotation = rotation; _form = form; _auras = auras;
        _shiftTo = shiftTo;
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
        if (ev.Type == CombatEventType.RotationDecision)
        {
            var target = context.GetActor("target");
            var haveRip = target?.ActiveAuras.Any(a => a.Definition.Key == "rip" &&
                a.IsActiveAt(context.CurrentTimeSeconds)) ?? false;
            foreach (var entry in _rotation.Entries)
                entry.IsEnabled = entry.AbilityKey switch {
                    "rip" => ComboPoints >= 4 && !haveRip,
                    "ferocious-bite" => ComboPoints >= 4 && !haveRip,
                    "rake" => target is not null && !target.ActiveAuras.Any(a =>
                        a.Definition.Key == "rake" && a.IsActiveAt(context.CurrentTimeSeconds)),
                    "lacerate" => LacerateStacks < 5 || target is not null &&
                        !target.ActiveAuras.Any(a => a.Definition.Key == "lacerate" &&
                            a.IsActiveAt(context.CurrentTimeSeconds)),
                    _ => true
                };
            var free = _druid.ActiveAuras.Any(a => a.Definition.Key == "druid-clearcasting" &&
                a.IsActiveAt(context.CurrentTimeSeconds));
            foreach (var (abilityKey, cost) in _baseCosts)
                _druid.Abilities[abilityKey].Definition.ResourceCosts[0].Amount =
                    free && abilityKey is not "shifting-power" ? 0m : cost;
            return;
        }
        if (ev.Type == CombatEventType.Damage && ev.TargetActorKey == _druid.Key &&
            ev.SourceActorKey == "target" && ev.Amount is > 0m &&
            _form.Form is ForeverDruidForm.Bear or ForeverDruidForm.DireBear)
            _druid.Resources["rage"].Gain(5m); // explicit provisional incoming-rage preview
        if (ev.SourceActorKey != _druid.Key) return;
        if (ev.Type == CombatEventType.AbilityCastCompleted)
        {
            if (ev.AbilityKey is "rip" or "ferocious-bite" && ev.AbilityExecutionId is Guid fin &&
                _finishers.Add(fin))
            {
                _lastFinisherPoints = ComboPoints;
                ComboPoints = 0;
            }
            if (ev.AbilityKey == "enrage")
            {
                _druid.Resources["rage"].Gain(10m);
            }
            if (ev.AbilityKey == "shifting-power")
                _druid.Resources["energy"].Gain(40m);
            if (ev.AbilityKey != "shifting-power" &&
                _druid.ActiveAuras.Any(a => a.Definition.Key == "druid-clearcasting" &&
                    a.IsActiveAt(context.CurrentTimeSeconds)))
            {
                _auras.RemoveAuras(context, _druid, "druid-clearcasting", _druid.Key, "free cast consumed");
                ClearcastsUsed++;
            }
        }
        if (ev.Type == CombatEventType.AuraApplied && ev.AbilityKey == "lacerate" &&
            ev.TargetActorKey == "target")
            LacerateStacks = Math.Min(5,LacerateStacks+1);
        if (ev.Type == CombatEventType.AuraRemoved && ev.AbilityKey == "lacerate" &&
            ev.TargetActorKey == "target")
            LacerateStacks = 0;
        if (ev.Type == CombatEventType.Damage && ev.Amount is > 0m && !ev.IsPeriodic)
        {
            if (ev.AbilityKey is "claw" or "shred" or "rake" &&
                ev.AbilityExecutionId is Guid id && _generators.Add(id))
                ComboPoints = Math.Min(5,ComboPoints+1);
            if (_form.Form is ForeverDruidForm.Bear or ForeverDruidForm.DireBear &&
                ev.AbilityKey == "feral-auto")
                _druid.Resources["rage"].Gain(ev.IsCritical ? 9m : 5m);
            if (context.CurrentTimeSeconds >= _nextOmen &&
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
