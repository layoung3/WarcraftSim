using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Items;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Shared solo item policy. It never manufactures resources directly: successful
/// uses execute real AbilityDefinition effects in the same encounter event queue.
/// The normal ability engine enforces item cooldown groups and actor input locks.
/// Only equipped/prepared inventory is counted; each run has fresh state.
/// </summary>
public sealed class CombatUseItemProcessor : ICombatEventProcessor
{
    private readonly CombatUseItemsOptions _options;
    private readonly AbilityExecutor _executor;
    private readonly string _actorKey;
    private readonly string _targetKey;
    private readonly Dictionary<string, int> _remaining = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _uses = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, int> Uses => _uses;
    public IReadOnlyList<string> SelectionReasons => _selectionReasons;
    private readonly List<string> _selectionReasons = [];

    public CombatUseItemProcessor(CombatUseItemsOptions options, AbilityExecutor executor,
        SimulationActorState actor, string targetKey, string classKey)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _actorKey = actor.Key;
        _targetKey = targetKey;
        _options.Validate(classKey.ToLowerInvariant());
        Add(ForeverCombatItemCatalog.ManaRuby, options.ManaRubyCharges);
        Add(ForeverCombatItemCatalog.DarkRune, options.DarkRuneCharges);
        Add(ForeverCombatItemCatalog.DemonicRune, options.DemonicRuneCharges);
        Add(ForeverCombatItemCatalog.MightyRagePotion, options.MightyRagePotionCharges);
        Add(ForeverCombatItemCatalog.MajorHealingPotion, options.MajorHealingPotionCharges);
        Add(ForeverCombatItemCatalog.DenseDynamite, options.DenseDynamiteCharges);
        Add(ForeverCombatItemCatalog.GoblinSapperCharge, options.GoblinSapperCharges);
        var catalog = ForeverCombatItemCatalog.CreateAll();
        foreach (var definition in catalog.Where(a => _remaining.ContainsKey(a.Key)))
            actor.AddAbility(definition);
    }

    private void Add(string key, int charges)
    {
        if (charges > 0) _remaining.Add(key, charges);
    }

    // Item effectiveness values are expectations from the current catalog's
    // fixed damage/resource ranges, not unverified live-server hit estimates.
    // Scoring is performed for every decision, after checking cooldown and inventory.
    private bool Available(string key, SimulationActorState actor, decimal now)
    {
        if (!_remaining.TryGetValue(key, out var count) || count <= 0 ||
            !actor.Abilities.TryGetValue(key, out var state))
            return false;
        var definition = state.Definition;
        return state.IsReady(now) &&
            actor.IsSharedCooldownReady(definition.SharedCooldownGroupKey, now);
    }

    public void Process(SimulationContext context, CombatEvent ev)
    {
        if (ev.Type != CombatEventType.RotationDecision ||
            !string.Equals(ev.SourceActorKey, _actorKey, StringComparison.OrdinalIgnoreCase))
            return;
        var actor = context.GetActor(_actorKey);
        if (actor is null || !actor.IsAlive) return;
        var now = context.CurrentTimeSeconds;
        actor.Resources.TryGetValue("mana", out var mana);
        decimal deficit = mana is null ? 0m : Math.Max(0m, mana.Maximum - mana.Current);
        decimal manaPercent = mana is { Maximum: > 0m } ? mana.Current * 100m / mana.Maximum : 100m;
        decimal rage = actor.Resources.TryGetValue("rage", out var rageState) ? rageState.Current : 100m;

        // A gem and a rune do not share a cooldown. Runes share their own
        // group, and their health cost is checked before scoring them.
        // Sort by expected USABLE mana (not item name), using health cost
        // only for a tie. Avoid full-mana waste and overcap opportunities.
        var resources = new List<(string Key,decimal Useful,decimal HealthCost)>();
        if (mana is not null && deficit > 0m)
        {
            if (manaPercent <= _options.ManaRubyBelowManaPercent &&
                Available(ForeverCombatItemCatalog.ManaRuby, actor, now))
                resources.Add((ForeverCombatItemCatalog.ManaRuby,Math.Min(deficit,1100m),0m));
            if (manaPercent <= _options.RuneBelowManaPercent &&
                actor.CurrentHealth > _options.MinimumHealthToUseRune + 1000m)
                foreach(var key in new[]{ForeverCombatItemCatalog.DarkRune,ForeverCombatItemCatalog.DemonicRune})
                    if(Available(key,actor,now))
                        resources.Add((key,Math.Min(deficit,1200m),800m));
        }
        foreach(var choice in resources.OrderByDescending(x=>x.Useful)
                     .ThenBy(x=>x.HealthCost).ThenBy(x=>x.Key,StringComparer.Ordinal))
        {
            if (choice.Useful < 1m) continue;
            if (Try(choice.Key,true,context,
                $"mana deficit {deficit:0}, expected useful recovery {choice.Useful:0}, health cost expectation {choice.HealthCost:0}"))
                return;
        }

        if (actor.MaximumHealth > 0m && Try(ForeverCombatItemCatalog.MajorHealingPotion,
                actor.CurrentHealth * 100m / actor.MaximumHealth <=
                    _options.MajorHealingPotionBelowHealthPercent &&
                actor.MaximumHealth-actor.CurrentHealth >= 1050m, context)) return;
        if (actor.Resources.ContainsKey("rage") && Try(ForeverCombatItemCatalog.MightyRagePotion,
                rage <= _options.MightyRageBelowRage, context)) return;

        if (now < _options.FirstExplosiveUseSeconds) return;
        // Expected single-target damage; shared engine cooldown guarantees
        // at most one explosive activation. Sapper has an additional personal
        // 5 minute cooldown and a self-damage safety requirement.
        var explosives = new List<(string Key,decimal ExpectedDamage)>();
        if (Available(ForeverCombatItemCatalog.GoblinSapperCharge,actor,now) &&
            actor.CurrentHealth > _options.MinimumHealthToUseSapper + 625m)
            explosives.Add((ForeverCombatItemCatalog.GoblinSapperCharge,600m));
        if (Available(ForeverCombatItemCatalog.DenseDynamite,actor,now))
            explosives.Add((ForeverCombatItemCatalog.DenseDynamite,400m));
        foreach(var choice in explosives.OrderByDescending(x=>x.ExpectedDamage)
                     .ThenBy(x=>x.Key,StringComparer.Ordinal))
            if(Try(choice.Key,true,context,
                $"expected single-target fire damage {choice.ExpectedDamage:0}; shared explosive cooldown; health safety checked"))
                return;
    }

    private bool Try(string key, bool condition, SimulationContext context, string? reason = null)
    {
        if (!condition || !_remaining.TryGetValue(key, out var count) || count <= 0)
            return false;
        var result = _executor.TryStartAbility(context, _actorKey, _targetKey, key);
        if (!result.Success) return false;
        _remaining[key] = count - 1;
        _uses[key] = _uses.GetValueOrDefault(key) + 1;
        if(reason is not null) _selectionReasons.Add($"{context.CurrentTimeSeconds:0.###}s {key}: {reason}");
        return true;
    }
}
