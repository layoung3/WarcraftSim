using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;

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
            ["recklessness"] = (0m, 15m, 1800m)
        };

    public static void Validate(WarriorSimRequest request)
    {
        if (request.MaintenancePolicies is null || request.MaintenancePolicies.Count > 4 ||
            request.MajorCooldowns is null || request.MajorCooldowns.Count > 4)
            throw new ArgumentException("Runtime maintenance and cooldown policy lists must each contain at most four actions.");
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

    public WarriorRuntimeActionProcessor(AbilityExecutor executor, AuraManager auras,
        WarriorSimRequest request, SimulationActorState actor, SimulationActorState target)
    {
        _executor = executor;
        _auras = auras;
        _request = request;
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
            yield return MakeAbility(policy.AbilityKey, cost, cooldown, offGcd: false);
        }
        foreach (var policy in request.MajorCooldowns)
        {
            var (cost, _, cooldown) = WarriorRuntimePolicyValidator.Cooldowns[policy.AbilityKey];
            yield return MakeAbility(policy.AbilityKey, cost, cooldown, offGcd: true);
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
            else
            {
                Schedule(context, policy.OpeningDelaySeconds);
                if (policy.HoldLastUse && policy.FinalWindowSeconds > 0m)
                    Schedule(context, _request.DurationSeconds - policy.FinalWindowSeconds);
            }
        }
    }

    public void Process(SimulationContext context, CombatEvent ev)
    {
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
            var target = key is "battle-shout" or "blood-fury" or "berserking" or "death-wish" or "recklessness"
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
                    Schedule(context, now + WarriorRuntimePolicyValidator.Cooldowns[key].cooldown);
                Schedule(context, state.ExpiresAt);
            }
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
        actor.Stats.Set(ForeverCombatStatKeys.AttackCriticalChancePercent,
            _baseCrit + (Active("recklessness") ? 100m : 0m));
        actor.Stats.Set(ForeverCombatStatKeys.HastePercent,
            _baseHaste + (Active("berserking") ? 10m : 0m));
        actor.Stats.Set("physical-damage-bonus-percent",
            _basePhysicalBonus + (Active("death-wish") ? 20m : 0m));
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
