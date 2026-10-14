using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;
using WarcraftSim.Data.Forever.Abilities.Warrior;

namespace WarcraftSim.Api.Simulation;

/// <summary>Shared policy validator. The action-policy transport is Warrior-specific;
/// action timing and aura ownership live in the Core engine.</summary>
public static class WarriorRuntimePolicyValidator
{
    internal static readonly IReadOnlyDictionary<string, (decimal cost, decimal duration, decimal cooldown)> Maintenance =
        new Dictionary<string, (decimal, decimal, decimal)>(StringComparer.OrdinalIgnoreCase)
        {
            ["sunder-armor"] = (15m, 30m, 0m),
            ["battle-shout"] = (10m, 180m, 0m),
            ["demoralizing-shout"] = (10m, 45m, 0m),
            ["thunder-clap"] = (20m, 30m, 6m)
        };
    internal static readonly IReadOnlyDictionary<string, (decimal cost, decimal duration, decimal cooldown)> Cooldowns =
        new Dictionary<string, (decimal, decimal, decimal)>(StringComparer.OrdinalIgnoreCase)
        {
            ["blood-fury"] = (0m, 15m, 120m),
            ["berserking"] = (0m, 10m, 180m),
            ["death-wish"] = (10m, 30m, 180m),
            ["recklessness"] = (0m, 15m, 1800m),
            ["shield-wall"] = (0m, 12m, 900m),
            ["last-stand"] = (0m, 20m, 180m)
        };

    public static void Validate(WarriorSimRequest request)
    {
        if (request.MaintenancePolicies is null || request.MaintenancePolicies.Count > 4 ||
            request.MajorCooldowns is null || request.MajorCooldowns.Count > 6)
            throw new ArgumentException("Runtime maintenance and cooldown policy lists must each contain at most four maintenance and six cooldown actions.");
        if (request.MaintenancePolicies.Any(p => p is null || !Maintenance.ContainsKey(p.AbilityKey)) ||
            request.MaintenancePolicies.GroupBy(p => p.AbilityKey, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            throw new ArgumentException("Unknown or duplicated maintenance assignment.");
        if (request.MajorCooldowns.Any(p => p is null || !Cooldowns.ContainsKey(p.AbilityKey)) ||
            request.MajorCooldowns.GroupBy(p => p.AbilityKey, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            throw new ArgumentException("Unknown or duplicated major cooldown policy.");
        foreach (var policy in request.MaintenancePolicies)
        {
            if (policy.DesiredStacks != (policy.AbilityKey == "sunder-armor" ? Math.Clamp(policy.DesiredStacks, 1, 5) : 1))
                throw new ArgumentException($"Invalid desired stack count for {policy.AbilityKey}.");
            var duration = Maintenance[policy.AbilityKey].duration;
            if (policy.RefreshWhenRemainingSeconds < 0m || policy.RefreshWhenRemainingSeconds >= duration)
                throw new ArgumentException($"Refresh threshold for {policy.AbilityKey} must be shorter than its aura duration.");
        }
        if (request.MaintenancePolicies.Any(p => p.AbilityKey == "sunder-armor") &&
            request.BossDebuffIds.Any(id => string.Equals(id, "expose-armor", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Assigned Sunder cannot be combined with externally assumed Expose Armor in this model.");
        foreach (var policy in request.MajorCooldowns)
        {
            if (policy.Mode == "at-times")
            {
                // Opening delay and the final hold window are not used for
                // explicitly scheduled casts. In particular, their defaults
                // must not invalidate a shorter test encounter.
                if (policy.PlannedUseSeconds is null ||
                    policy.PlannedUseSeconds.Count is 0 or > 10 ||
                    policy.PlannedUseSeconds.Any(t => t < 0m || t >= request.DurationSeconds) ||
                    policy.PlannedUseSeconds.Distinct().Count() != policy.PlannedUseSeconds.Count)
                    throw new ArgumentException($"Invalid planned use times for {policy.AbilityKey}.");
            }
            else if (policy.Mode == "on-boss-cast")
            {
                if (policy.TriggerBossAbilityKeys is null ||
                    policy.TriggerBossAbilityKeys.Count > 20 ||
                    policy.TriggerBossAbilityKeys.Any(key => !request.BossAbilities.Any(b =>
                        string.Equals(b.Key, key, StringComparison.OrdinalIgnoreCase))) ||
                    policy.TriggerMinimumRawDamagePercentOfMaxHealth is < 0m or > 1000m)
                    throw new ArgumentException($"Invalid boss-cast reaction policy for {policy.AbilityKey}.");
            }
            else if (policy.Mode == "on-cooldown")
            {
                if (policy.OpeningDelaySeconds < 0m ||
                    policy.OpeningDelaySeconds >= request.DurationSeconds ||
                    (policy.HoldLastUse &&
                     (policy.FinalWindowSeconds < 0m || policy.FinalWindowSeconds > request.DurationSeconds)))
                    throw new ArgumentException($"Invalid on-cooldown timing for {policy.AbilityKey}.");
            }
            else
            {
                throw new ArgumentException($"Unknown cooldown timing mode for {policy.AbilityKey}.");
            }
            if (policy.AbilityKey == "blood-fury" && request.Race != "orc")
                throw new ArgumentException("Blood Fury is an Orc racial.");
            if (policy.AbilityKey == "berserking" && request.Race != "troll")
                throw new ArgumentException("Berserking is a Troll racial.");
            if (policy.AbilityKey == "shield-wall" && !request.ShieldEquipped)
                throw new ArgumentException("Shield Wall requires an equipped shield.");
            if (policy.AbilityKey == "last-stand" &&
                ForeverWarriorTalentCatalog.Rank(request, "last-stand") == 0)
                throw new ArgumentException("Last Stand requires its Protection talent.");
            if (policy.AbilityKey == "death-wish" &&
                (request.TalentMode != "tree" || ForeverWarriorTalentCatalog.Rank(request, "death-wish") == 0))
                throw new ArgumentException("Death Wish requires its selected Fury talent.");
        }
    }
}

/// <summary>
/// Applies per-actor upkeep casts, real Rage costs/GCDs, and timed burst states.
/// This policy executor runs immediately ahead of a normal priority rotation.
/// It is deliberately scoped to a single player/target pair; the same effect
/// ownership model can later feed a multi-actor raid context.
/// </summary>
public sealed class WarriorRuntimeActionProcessor : ICombatEventProcessor
{
    private sealed class ActiveState
    {
        public decimal ExpiresAt;
        public int Stacks;
        public int Uses;
    }

    private const string ActorKey = "warrior";
    private const string TargetKey = "target";
    private readonly AbilityExecutor _executor;
    private readonly AuraManager _auras;
    private readonly WarriorSimRequest _request;
    private readonly Dictionary<string, ActiveState> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly decimal _baseAp, _baseCrit, _baseHaste, _basePhysicalBonus, _baseTargetArmor;
    private readonly AutoAttackProcessor? _autoAttacks;

    public WarriorRuntimeActionProcessor(AbilityExecutor executor, AuraManager auras,
        WarriorSimRequest request, SimulationActorState actor, SimulationActorState target,
        AutoAttackProcessor? autoAttacks = null)
    {
        _executor = executor;
        _auras = auras;
        _request = request;
        _autoAttacks = autoAttacks;
        _baseAp = actor.Stats.Get(ForeverCombatStatKeys.AttackPower);
        _baseCrit = actor.Stats.Get(ForeverCombatStatKeys.AttackCriticalChancePercent);
        _baseHaste = actor.Stats.Get(ForeverCombatStatKeys.HastePercent);
        _basePhysicalBonus = actor.Stats.Get("physical-damage-bonus-percent");
        _baseTargetArmor = target.Stats.Get(ForeverCombatStatKeys.Armor);
        foreach (var key in request.MaintenancePolicies.Select(x => x.AbilityKey)
            .Concat(request.MajorCooldowns.Select(x => x.AbilityKey)))
            _states.Add(key, new ActiveState());
    }

    public static IEnumerable<AbilityDefinition> CreateAbilities(WarriorSimRequest request)
    {
        foreach (var policy in request.MaintenancePolicies)
        {
            var (cost, _, cooldown) = WarriorRuntimePolicyValidator.Maintenance[policy.AbilityKey];
            var talents = request.TalentMode == "tree";
            var rank = (string id) => talents ? ForeverWarriorTalentCatalog.Rank(request, id) : 0;
            if (policy.AbilityKey == "sunder-armor") cost = Math.Max(0m, cost - rank("improved-sunder-armor"));
            if (policy.AbilityKey == "thunder-clap") cost = Math.Max(0m, cost - 2m * rank("improved-thunder-clap"));
            if (policy.AbilityKey is "battle-shout" or "demoralizing-shout")
                cost = Math.Max(0m, cost * (1m - 0.05m * rank("booming-voice")));
            var ability = MakeAbility(policy.AbilityKey, cost, cooldown, offGcd: false);
            if (policy.AbilityKey == "thunder-clap")
            {
                // Forever Rank 6: Battle OR Defensive Stance, never Berserker.
                // RequiredSourceAuraKeys has ALL-of semantics: listing both
                // stances there would make the ability impossible to cast.
                // The current Warrior sim initializes exactly one of the three
                // stances, so excluding Berserker permits both valid stances.
                ability.ForbiddenSourceAuraKeys.Add(ForeverWarriorAuraKeys.BerserkerStance);
            }
            yield return ability;
        }
        foreach (var policy in request.MajorCooldowns)
        {
            var (cost, _, cooldown) = WarriorRuntimePolicyValidator.Cooldowns[policy.AbilityKey];
            if (policy.AbilityKey == "shield-wall")
                cooldown = Math.Max(0m, cooldown - 330m * ForeverWarriorTalentCatalog.Rank(request, "improved-shield-wall"));
            // Beta client: Death Wish and Recklessness consume the ordinary
            // global cooldown. Only the two selected offensive racials are
            // treated as off-GCD in this currently modeled cooldown roster.
            var offGcd = policy.AbilityKey is "blood-fury" or "berserking" or "shield-wall" or "last-stand";
            var ability = MakeAbility(policy.AbilityKey, cost, cooldown, offGcd);
            if (policy.AbilityKey == "recklessness")
                ability.RequiredSourceAuraKeys.Add(ForeverWarriorAuraKeys.BerserkerStance);
            if (policy.AbilityKey == "shield-wall")
                ability.RequiredSourceAuraKeys.Add(ForeverWarriorAuraKeys.DefensiveStance);
            yield return ability;
        }
    }

    private static AbilityDefinition MakeAbility(string key, decimal cost, decimal cooldown, bool offGcd) =>
        new()
        {
            Key = key, Name = string.Join(" ", key.Split('-').Select(x => char.ToUpperInvariant(x[0]) + x[1..])),
            RequiredLevel = 1, CooldownSeconds = cooldown,
            IsOffGlobalCooldown = offGcd, GlobalCooldownSeconds = offGcd ? 0m : 1.5m,
            ResourceCosts = cost > 0m ? [new AbilityResourceCost { ResourceKey = "rage", Amount = cost }] : []
        };

    public void ScheduleInitialDecisions(SimulationContext context)
    {
        foreach (var policy in _request.MajorCooldowns)
        {
            if (policy.Mode == "at-times")
                foreach (var time in policy.PlannedUseSeconds)
                    Schedule(context, time);
            else if (policy.Mode == "on-cooldown")
            {
                Schedule(context, policy.OpeningDelaySeconds);
                if (policy.HoldLastUse && policy.FinalWindowSeconds > 0m)
                    Schedule(context, _request.DurationSeconds - policy.FinalWindowSeconds);
            }
        }
    }

    public void Process(SimulationContext context, CombatEvent ev)
    {
        if (ev.Type == CombatEventType.EncounterBossCastStarted &&
            ev.EncounterEventKey is { } bossAbilityKey &&
            ev.SourceActorKey == TargetKey)
        {
            var reactingWarrior = context.GetActor(ActorKey);
            var bossAbility = _request.BossAbilities.FirstOrDefault(a =>
                string.Equals(a.Key, bossAbilityKey, StringComparison.OrdinalIgnoreCase));
            if (reactingWarrior is not { IsAlive: true } || bossAbility is null) return;
            var anticipatedRaw = (bossAbility.DirectDamage +
                bossAbility.ChannelTickCount * bossAbility.ChannelTickDamage +
                (bossAbility.DotTickIntervalSeconds > 0m
                    ? Math.Floor(bossAbility.DebuffDurationSeconds /
                        bossAbility.DotTickIntervalSeconds) * bossAbility.DotTickDamage
                    : 0m)) * bossAbility.AreaExposurePercent / 100m;
            foreach (var policy in _request.MajorCooldowns.Where(p => p.Mode == "on-boss-cast"))
            {
                if (policy.TriggerBossAbilityKeys.Count > 0 &&
                    !policy.TriggerBossAbilityKeys.Contains(bossAbilityKey, StringComparer.OrdinalIgnoreCase)) continue;
                if (anticipatedRaw * 100m < reactingWarrior.MaximumHealth *
                    policy.TriggerMinimumRawDamagePercentOfMaxHealth) continue;
                // Obey real ability cooldown, GCD, talent and stance gating.
                if (_executor.TryStartAbility(context, ActorKey, TargetKey, policy.AbilityKey).Success)
                    break;
            }
            return;
        }
        if (ev.Type == CombatEventType.AbilityCastCompleted && ev.SourceActorKey == ActorKey &&
            ev.AbilityKey is { } key && _states.TryGetValue(key, out var state))
        {
            var now = context.CurrentTimeSeconds;
            var duration = WarriorRuntimePolicyValidator.Maintenance.TryGetValue(key, out var maintenance)
                ? maintenance.duration : WarriorRuntimePolicyValidator.Cooldowns[key].duration;
            state.Stacks = key == "sunder-armor" ? Math.Min(5, state.Stacks + 1) : 1;
            state.ExpiresAt = now + duration;
            state.Uses++;
            var actor = context.GetActor(ActorKey)!;
            var target = key is "battle-shout" or "blood-fury" or "berserking" or "death-wish" or "recklessness" or "shield-wall" or "last-stand"
                ? actor : context.GetActor(TargetKey)!;
            _auras.ApplyAura(context, target, new AuraDefinition
            {
                Key = "policy-" + key, Name = key, DurationSeconds = duration,
                StackingMode = key == "sunder-armor" ? AuraStackingMode.Stack : AuraStackingMode.Refresh,
                MaxStacks = key == "sunder-armor" ? 5 : 1
            }, ActorKey, key, null);
            UpdateStats(context);
            if (WarriorRuntimePolicyValidator.Maintenance.ContainsKey(key))
            {
                var policy = _request.MaintenancePolicies.Single(x => x.AbilityKey == key);
                Schedule(context, Math.Max(now, state.ExpiresAt - policy.RefreshWhenRemainingSeconds));
                Schedule(context, state.ExpiresAt);
            }
            else
            {
                var policy = _request.MajorCooldowns.Single(x => x.AbilityKey == key);
                if (policy.Mode == "on-cooldown")
                    Schedule(context, now + (key == "shield-wall"
                        ? actor.Abilities[key].Definition.CooldownSeconds
                        : WarriorRuntimePolicyValidator.Cooldowns[key].cooldown));
                Schedule(context, state.ExpiresAt);
            }
            return;
        }
        if (ev.Type == CombatEventType.AbilityCastCompleted && ev.SourceActorKey == ActorKey &&
            ev.AbilityKey is "enter-battle-stance" or "enter-berserker-stance")
        {
            UpdateStats(context);
            return;
        }
        if (ev.Type is not (CombatEventType.RotationDecision or CombatEventType.AuraExpiration))
            return;
        if (ev.Type == CombatEventType.AuraExpiration)
        {
            UpdateStats(context);
            return;
        }
        if (ev.SourceActorKey != ActorKey) return;
        UpdateStats(context);
        var warrior = context.GetActor(ActorKey);
        if (warrior is null || !warrior.IsAlive || !warrior.IsInputReady(context.CurrentTimeSeconds)) return;
        var nowTime = context.CurrentTimeSeconds;
        foreach (var policy in _request.MajorCooldowns)
        {
            var majorState = _states[policy.AbilityKey];
            if (ShouldUseMajor(policy, majorState, nowTime))
            {
                var use = _executor.TryStartAbility(context, ActorKey, TargetKey, policy.AbilityKey);
                // Only one burst activation per decision; re-check other cooldowns on completion.
                if (use.Success) return;
            }
        }
        foreach (var policy in _request.MaintenancePolicies)
        {
            var maintenanceState = _states[policy.AbilityKey];
            var due = maintenanceState.ExpiresAt <= nowTime ||
                maintenanceState.Stacks < policy.DesiredStacks ||
                maintenanceState.ExpiresAt - nowTime <= policy.RefreshWhenRemainingSeconds;
            if (!due) continue;
            if (_executor.TryStartAbility(context, ActorKey, TargetKey, policy.AbilityKey).Success)
                return;
        }
    }

    private bool ShouldUseMajor(WarriorMajorCooldownPolicy policy, ActiveState state, decimal now)
    {
        if (policy.Mode == "on-boss-cast") return false;
        if (policy.Mode == "at-times")
        {
            var planned = policy.PlannedUseSeconds.OrderBy(x => x).ToList();
            return state.Uses < planned.Count && now >= planned[state.Uses];
        }
        if (now < policy.OpeningDelaySeconds) return false;
        if (!policy.HoldLastUse || policy.FinalWindowSeconds <= 0m) return true;
        var finalStart = _request.DurationSeconds - policy.FinalWindowSeconds;
        var cooldown = WarriorRuntimePolicyValidator.Cooldowns[policy.AbilityKey].cooldown;
        // Reserve the final use only if using it now would block the final window.
        return now >= finalStart || now + cooldown <= finalStart;
    }

    /// <summary>Whether the Warrior actively maintains the debuff at this time.
    /// External full-uptime raid debuffs are not tracked by this policy executor.</summary>
    public bool IsMaintainedAt(string key, decimal at) =>
        _states.TryGetValue(key, out var state) &&
        state.Stacks > 0 && state.ExpiresAt > at;

    public void RefreshDynamicStats(SimulationContext context) => UpdateStats(context);

    private void UpdateStats(SimulationContext context)
    {
        var now = context.CurrentTimeSeconds;
        foreach (var state in _states.Values)
            if (state.ExpiresAt <= now) state.Stacks = 0;
        bool Active(string key) => _states.TryGetValue(key, out var s) && s.Stacks > 0 && s.ExpiresAt > now;
        var actor = context.GetActor(ActorKey);
        var target = context.GetActor(TargetKey);
        if (actor is null || target is null) return;
        decimal attackPower = _baseAp + (Active("battle-shout") ? 139m : 0m);
        if (Active("blood-fury")) attackPower *= 1.10m;
        actor.Stats.Set(ForeverCombatStatKeys.AttackPower, attackPower);
        var berserker = actor.ActiveAuras.Any(aura =>
            aura.IsActiveAt(now) && string.Equals(aura.Definition.Key,
                ForeverWarriorAuraKeys.BerserkerStance, StringComparison.OrdinalIgnoreCase));
        actor.Stats.Set(ForeverCombatStatKeys.AttackCriticalChancePercent,
            _baseCrit + (berserker ? 3m : 0m) + (Active("recklessness") ? 100m : 0m));
        var oldHaste = actor.Stats.Get(ForeverCombatStatKeys.HastePercent);
        var newHaste = _baseHaste + (Active("berserking") ? 10m : 0m) +
            actor.Stats.Get(WarriorWeaponTalentProcessor.FlurryHasteStat);
        actor.Stats.Set(ForeverCombatStatKeys.HastePercent, newHaste);
        _autoAttacks?.RescheduleForHasteChange(context, actor.Key, oldHaste, newHaste);
        actor.Stats.Set("physical-damage-bonus-percent",
            _basePhysicalBonus + (Active("death-wish") ? 20m : 0m) +
            actor.Stats.Get(WarriorIncomingTalentProcessor.EnrageBonusKey));
        var stacks = Active("sunder-armor") ? _states["sunder-armor"].Stacks : 0;
        target.Stats.Set(ForeverCombatStatKeys.Armor, Math.Max(0m, _baseTargetArmor - 450m * stacks));
    }

    private static void Schedule(SimulationContext context, decimal when)
    {
        if (when <= context.CurrentTimeSeconds || when >= context.Options.DurationSeconds) return;
        context.ScheduleEvent(new CombatEvent
        {
            TimeSeconds = when, Type = CombatEventType.RotationDecision,
            SourceActorKey = ActorKey, TargetActorKey = TargetKey,
            IsInternal = true, Description = "Runtime action-policy reassessment"
        });
    }
}
