using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Data.Forever.Abilities.Warrior;
using WarcraftSim.Data.Forever.Characters;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Safe, server-side translator from the Warrior alpha's configurable priority
/// list to the existing class-neutral Core rotation engine.
/// </summary>
public static class WarriorRotationRules
{
    private const string Rage = "rage";
    private const string AutoAttack = "main-hand-auto";
    private static readonly HashSet<string> AllActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "bloodrage", "overpower", "execute", "mortal-strike", "bloodthirst",
        "whirlwind", "rend", "slam", "heroic-strike", "cleave"
    };

    // These alpha abilities expose meaningful cooldown windows. Do not allow
    // an always-ready/no-cooldown action as a dependency.
    private static readonly HashSet<string> CooldownActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "bloodrage", "overpower", "mortal-strike", "bloodthirst", "whirlwind"
    };

    public static void Validate(WarriorSimRequest request)
    {
        if (request.RotationMode is not ("automatic" or "custom"))
            throw new ArgumentException("RotationMode must be 'automatic' or 'custom'.");
        if (request.RotationMode != "custom") return;
        if (request.CustomRotation is null || request.CustomRotation.Count is < 1 or > 25)
            throw new ArgumentException("A custom rotation must contain 1 to 25 rules.");
        var arms = request.Preset.Equals("arms", StringComparison.OrdinalIgnoreCase);
        foreach (var rule in request.CustomRotation)
        {
            if (rule is null || !AllActions.Contains(rule.Action ?? ""))
                throw new ArgumentException("Custom rotation contains an unsupported action.");
            // Supported-action validation above guarantees Action is non-null.
            var key = rule.Action!.ToLowerInvariant();
            // Keep disabled rows as editable drafts even when their action is
            // not currently enabled/registered by this loadout.
            if (rule.Enabled && ((key == "mortal-strike" || key == "overpower" || key == "rend") && !arms ||
                (key == "bloodthirst" || key == "whirlwind") && arms))
                throw new ArgumentException($"'{key}' is not registered for the selected rotation preset.");
            if (rule.Enabled && (key == "bloodrage" && !request.UseBloodrage ||
                key == "execute" && !request.UseExecute ||
                key == "rend" && !request.UseRend ||
                key == "slam" && !request.UseSlam ||
                key == "heroic-strike" && !request.UseHeroicStrike ||
                key == "cleave" && !request.UseCleave))
                throw new ArgumentException($"'{key}' is disabled in the selected ability setup.");
            var cooldownRequirement = rule.OnlyWhenAbilityOnCooldown;
            if (!string.IsNullOrWhiteSpace(cooldownRequirement))
            {
                var dependency = cooldownRequirement.Trim().ToLowerInvariant();
                if (!CooldownActions.Contains(dependency) ||
                    (arms && dependency is "bloodthirst" or "whirlwind") ||
                    (!arms && dependency is "mortal-strike" or "overpower" or "rend") ||
                    (dependency == "bloodrage" && !request.UseBloodrage) ||
                    (dependency == "execute" && !request.UseExecute) ||
                    (dependency == "rend" && !request.UseRend) ||
                    (dependency == "slam" && !request.UseSlam))
                    throw new ArgumentException($"Cooldown dependency '{dependency}' is not available as a castable ability for this setup.");
            }
            CheckOptional(rule.MinimumRage, 0m, 100m, "MinimumRage");
            CheckOptional(rule.MaximumRage, 0m, 100m, "MaximumRage");
            CheckOptional(rule.TargetHealthAtOrBelowPercent, 0m, 100m, "TargetHealthAtOrBelowPercent");
            CheckOptional(rule.MinimumAttackPower, 0m, 1_000_000m, "MinimumAttackPower");
            CheckOptional(rule.MaximumAttackPower, 0m, 1_000_000m, "MaximumAttackPower");
            if (rule.MinimumRage > rule.MaximumRage ||
                rule.MinimumAttackPower > rule.MaximumAttackPower)
                throw new ArgumentException($"The bounds for '{key}' are reversed.");
        }
        if (!request.CustomRotation.Any(rule => rule.Enabled))
            throw new ArgumentException("At least one custom rotation rule must be enabled.");
    }

    private static void CheckOptional(decimal? number, decimal min, decimal max, string name)
    {
        if (number is not null && (number < min || number > max))
            throw new ArgumentException($"{name} must be between {min} and {max}.");
    }

    public static RotationProfile BuildCustom(WarriorSimRequest request)
    {
        Validate(request);
        if (request.RotationMode != "custom")
            throw new ArgumentException("BuildCustom requires the custom rotation mode.");
        var profile = new RotationProfile
        {
            Name = "Warrior Custom Priority",
            RulesetKey = "wow-forever",
            ClassKey = ForeverCharacterClassKeys.Warrior,
            SimulationType = SimulationType.Dps
        };
        var order = 0;
        foreach (var rule in request.CustomRotation)
        {
            var action = rule.Action.ToLowerInvariant();
            var queued = action is "heroic-strike" or "cleave";
            var entry = new RotationEntry
            {
                Priority = ++order,
                IsEnabled = rule.Enabled,
                ActionType = queued ? RotationActionTypes.QueueNextSwingReplacement : RotationActionTypes.Ability,
                AbilityKey = queued ? "" : action,
                NextSwingReplacementKey = queued ? action : "",
                AutoAttackKey = queued ? AutoAttack : ""
            };
            Add(entry, RotationConditionTypes.SourceResourceCurrent, Rage,
                RotationComparisonOperators.GreaterThanOrEqual, rule.MinimumRage);
            Add(entry, RotationConditionTypes.SourceResourceCurrent, Rage,
                RotationComparisonOperators.LessThanOrEqual, rule.MaximumRage);
            Add(entry, RotationConditionTypes.TargetHealthPercent, null,
                RotationComparisonOperators.LessThanOrEqual, rule.TargetHealthAtOrBelowPercent);
            Add(entry, RotationConditionTypes.SourceStatValue, ForeverCombatStatKeys.AttackPower,
                RotationComparisonOperators.GreaterThanOrEqual, rule.MinimumAttackPower);
            Add(entry, RotationConditionTypes.SourceStatValue, ForeverCombatStatKeys.AttackPower,
                RotationComparisonOperators.LessThanOrEqual, rule.MaximumAttackPower);
            var cooldownRequirement = rule.OnlyWhenAbilityOnCooldown;
            if (!string.IsNullOrWhiteSpace(cooldownRequirement))
                entry.Conditions.Add(new RotationConditionDefinition
                {
                    ConditionType = RotationConditionTypes.SourceAbilityOnCooldown,
                    Key = cooldownRequirement.Trim().ToLowerInvariant()
                });
            // In both automatic and custom modes, Execute is an execute-phase
            // action. The API must never interpret a custom health gate as
            // permission to execute outside that phase.
            if (action == "execute")
                Add(entry, RotationConditionTypes.TargetHealthPercent, null,
                    RotationComparisonOperators.LessThanOrEqual, 20m);
            // A simple aura-missing gate ensures that merely dragging Rend to
            // the top does not clip it once every GCD.
            if (action == "rend")
                entry.Conditions.Add(new RotationConditionDefinition
                {
                    ConditionType = RotationConditionTypes.TargetAuraMissing,
                    Key = ForeverRendFactory.AuraKey
                });
            profile.Entries.Add(entry);
        }
        return profile;
    }

    private static void Add(RotationEntry entry, string kind, string? key,
        string op, decimal? value)
    {
        if (!value.HasValue) return;
        entry.Conditions.Add(new RotationConditionDefinition
        {
            ConditionType = kind, Key = key, ComparisonOperator = op, Value = value
        });
    }
}
