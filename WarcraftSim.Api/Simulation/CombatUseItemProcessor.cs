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

    public void Process(SimulationContext context, CombatEvent ev)
    {
        if (ev.Type != CombatEventType.RotationDecision ||
            !string.Equals(ev.SourceActorKey, _actorKey, StringComparison.OrdinalIgnoreCase))
            return;
        var actor = context.GetActor(_actorKey);
        if (actor is null || !actor.IsAlive) return;
        decimal missingManaPercent = actor.Resources.TryGetValue("mana", out var mana) && mana.Maximum > 0m
            ? (mana.Maximum - mana.Current) * 100m / mana.Maximum : 0m;
        decimal rage = actor.Resources.TryGetValue("rage", out var rageState) ? rageState.Current : 100m;
        var now = context.CurrentTimeSeconds;

        // Exactly one attempted item per decision; cooldown/inventory state is
        // enforced before the next item is considered. Resource items are preferred.
        if (mana is not null && Try(ForeverCombatItemCatalog.ManaRuby,
                missingManaPercent >= 100m - _options.ManaRubyBelowManaPercent &&
                mana.Maximum - mana.Current >= 1000m, context)) return;
        if (mana is not null && Try(ForeverCombatItemCatalog.DarkRune,
                missingManaPercent >= 100m - _options.RuneBelowManaPercent &&
                mana.Maximum - mana.Current >= 900m &&
                actor.CurrentHealth > _options.MinimumHealthToUseRune + 1000m, context)) return;
        if (mana is not null && Try(ForeverCombatItemCatalog.DemonicRune,
                missingManaPercent >= 100m - _options.RuneBelowManaPercent &&
                mana.Maximum - mana.Current >= 900m &&
                actor.CurrentHealth > _options.MinimumHealthToUseRune + 1000m, context)) return;
        if (actor.Resources.ContainsKey("rage") && Try(ForeverCombatItemCatalog.MightyRagePotion,
                rage <= _options.MightyRageBelowRage, context)) return;
        if (now >= _options.FirstExplosiveUseSeconds && Try(ForeverCombatItemCatalog.GoblinSapperCharge,
                actor.CurrentHealth > _options.MinimumHealthToUseSapper + 625m, context)) return;
        if (now >= _options.FirstExplosiveUseSeconds)
            Try(ForeverCombatItemCatalog.DenseDynamite, true, context);
    }

    private bool Try(string key, bool condition, SimulationContext context)
    {
        if (!condition || !_remaining.TryGetValue(key, out var count) || count <= 0)
            return false;
        var result = _executor.TryStartAbility(context, _actorKey, _targetKey, key);
        if (!result.Success) return false;
        _remaining[key] = count - 1;
        _uses[key] = _uses.GetValueOrDefault(key) + 1;
        return true;
    }
}
