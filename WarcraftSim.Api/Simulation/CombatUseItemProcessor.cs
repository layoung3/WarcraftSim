using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
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
    private readonly Dictionary<string, decimal> _maximumRestoration = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, int> Uses => _uses;
    public IReadOnlyDictionary<string, int> Remaining => _remaining;
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
        Add(ForeverCombatItemCatalog.ManaCitrine, options.ManaCitrineCharges);
        Add(ForeverCombatItemCatalog.ManaJade, options.ManaJadeCharges);
        Add(ForeverCombatItemCatalog.ManaAgate, options.ManaAgateCharges);
        Add(ForeverCombatItemCatalog.MajorManaPotion, options.MajorManaPotionCharges);
        Add(ForeverCombatItemCatalog.SuperiorManaPotion, options.SuperiorManaPotionCharges);
        Add(ForeverCombatItemCatalog.SuperiorHealingPotion, options.SuperiorHealingPotionCharges);
        Add(ForeverCombatItemCatalog.MinorHealthstone, options.MinorHealthstoneCharges);
        Add(ForeverCombatItemCatalog.LesserHealthstone, options.LesserHealthstoneCharges);
        Add(ForeverCombatItemCatalog.Healthstone, options.HealthstoneCharges);
        Add(ForeverCombatItemCatalog.GreaterHealthstone, options.GreaterHealthstoneCharges);
        Add(ForeverCombatItemCatalog.MajorHealthstone, options.MajorHealthstoneCharges);
        Add(ForeverCombatItemCatalog.DarkRune, options.DarkRuneCharges);
        Add(ForeverCombatItemCatalog.DemonicRune, options.DemonicRuneCharges);
        Add(ForeverCombatItemCatalog.MightyRagePotion, options.MightyRagePotionCharges);
        Add(ForeverCombatItemCatalog.MajorHealingPotion, options.MajorHealingPotionCharges);
        Add(ForeverCombatItemCatalog.DenseDynamite, options.DenseDynamiteCharges);
        Add(ForeverCombatItemCatalog.GoblinSapperCharge, options.GoblinSapperCharges);
        var catalog = ForeverCombatItemCatalog.CreateAll();
        foreach (var definition in catalog.Where(a => _remaining.ContainsKey(a.Key)))
        {
            // Pull the maximum directly from executable ability data. Avoid
            // maintaining a competing set of potion/gem ranges in the scheduler.
            foreach(var effect in definition.Effects.Where(x=>
                x.EffectType==AbilityEffectTypes.ResourceChange &&
                x.ResourceChangeOperation==ResourceChangeOperationTypes.Gain ||
                x.EffectType==AbilityEffectTypes.DirectHealing))
                _maximumRestoration[definition.Key]=Math.Max(
                    _maximumRestoration.GetValueOrDefault(definition.Key),effect.MaximumValue);
            actor.AddAbility(definition);
        }
    }

    private void Add(string key, int charges)
    {
        if (charges > 0 && !_options.DisabledItemKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
            _remaining.Add(key, charges);
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
        bool FitsMaximum(string item,decimal missing) =>
            _options.RestorationPolicy=="legacy-half-mean" ||
            missing>=_maximumRestoration.GetValueOrDefault(item,decimal.MaxValue);

        // A gem and a rune do not share a cooldown. Runes share their own
        // group, and their health cost is checked before scoring them.
        // Sort by expected USABLE mana (not item name), using health cost
        // only for a tie. Avoid full-mana waste and overcap opportunities.
        var resources = new List<(string Key,decimal Useful,decimal HealthCost)>();
        if (mana is not null && deficit > 0m)
        {
            if (manaPercent <= _options.ManaRubyBelowManaPercent)
                foreach(var (key,mean) in new[]{
                    (ForeverCombatItemCatalog.ManaRuby,1100m),
                    (ForeverCombatItemCatalog.ManaCitrine,850m),
                    (ForeverCombatItemCatalog.ManaJade,600m),
                    (ForeverCombatItemCatalog.ManaAgate,400m)})
                    if(Available(key,actor,now))
                        resources.Add((key,Math.Min(deficit,mean),0m));
            if (manaPercent <= _options.RuneBelowManaPercent &&
                actor.CurrentHealth > _options.MinimumHealthToUseRune + 1000m)
                foreach(var key in new[]{ForeverCombatItemCatalog.DarkRune,ForeverCombatItemCatalog.DemonicRune})
                    if(Available(key,actor,now))
                        resources.Add((key,Math.Min(deficit,1200m),800m));
        }
        if (mana is not null && deficit > 0m &&
            manaPercent <= _options.MajorManaPotionBelowManaPercent &&
            Available(ForeverCombatItemCatalog.MajorManaPotion,actor,now) &&
            FitsMaximum(ForeverCombatItemCatalog.MajorManaPotion,deficit))
            resources.Add((ForeverCombatItemCatalog.MajorManaPotion,
                Math.Min(deficit,1800m),0m));
        if (mana is not null && deficit > 0m &&
            manaPercent <= _options.SuperiorManaPotionBelowManaPercent &&
            Available(ForeverCombatItemCatalog.SuperiorManaPotion,actor,now) &&
            FitsMaximum(ForeverCombatItemCatalog.SuperiorManaPotion,deficit))
            resources.Add((ForeverCombatItemCatalog.SuperiorManaPotion,
                Math.Min(deficit,1200m),0m));
        foreach(var choice in resources.OrderByDescending(x=>x.Useful)
                     .ThenBy(x=>x.HealthCost).ThenBy(x=>x.Key,StringComparer.Ordinal))
        {
            // Preserve a meaningful part of the restoration; don't spend a
            // shared cooldown for a sliver of mana near full capacity.
            var minimumUseful = choice.Key switch {
                ForeverCombatItemCatalog.ManaRuby => 550m,
                ForeverCombatItemCatalog.ManaCitrine => 425m,
                ForeverCombatItemCatalog.ManaJade => 300m,
                ForeverCombatItemCatalog.ManaAgate => 200m,
                ForeverCombatItemCatalog.MajorManaPotion => 900m,
                ForeverCombatItemCatalog.SuperiorManaPotion => 600m,
                _ => 600m
            };
            if (!FitsMaximum(choice.Key,deficit) ||
                (_options.RestorationPolicy=="legacy-half-mean" && choice.Useful < minimumUseful))
                continue;
            if (Try(choice.Key,true,context,
                $"mana deficit {deficit:0}, expected useful recovery {choice.Useful:0}, health cost expectation {choice.HealthCost:0}"))
                return;
        }

        // The strongest prepared Healthstone that fits; cooldown category is
        // shared with mana gems but independent of the combat-potion group.
        var healthMissing=Math.Max(0m,actor.MaximumHealth-actor.CurrentHealth);
        if(actor.MaximumHealth>0m &&
           actor.CurrentHealth*100m/actor.MaximumHealth<=_options.HealthstoneBelowHealthPercent)
        {
            foreach(var key in new[]{ForeverCombatItemCatalog.MajorHealthstone,
                ForeverCombatItemCatalog.GreaterHealthstone,
                ForeverCombatItemCatalog.Healthstone,
                ForeverCombatItemCatalog.LesserHealthstone,
                ForeverCombatItemCatalog.MinorHealthstone})
                if(Available(key,actor,now) && FitsMaximum(key,healthMissing) &&
                   Try(key,true,context,
                       "Health deficit "+healthMissing.ToString("0")+"; "+_options.RestorationPolicy))
                    return;
        }
        if (actor.MaximumHealth > 0m && Try(ForeverCombatItemCatalog.MajorHealingPotion,
                actor.CurrentHealth * 100m / actor.MaximumHealth <=
                    _options.MajorHealingPotionBelowHealthPercent &&
                actor.MaximumHealth-actor.CurrentHealth >= 1050m &&
                (FitsMaximum(ForeverCombatItemCatalog.MajorHealingPotion,
                    actor.MaximumHealth-actor.CurrentHealth) ||
                    _options.AllowEmergencyHealingOvercap),context,
                "Health deficit "+(actor.MaximumHealth-actor.CurrentHealth).ToString("0")+
                "; "+_options.RestorationPolicy+
                (_options.AllowEmergencyHealingOvercap?" (emergency exception enabled)":""))) return;
        if (actor.MaximumHealth > 0m && Try(ForeverCombatItemCatalog.SuperiorHealingPotion,
                actor.CurrentHealth * 100m / actor.MaximumHealth <=
                    _options.SuperiorHealingPotionBelowHealthPercent &&
                actor.MaximumHealth-actor.CurrentHealth >= 700m &&
                (FitsMaximum(ForeverCombatItemCatalog.SuperiorHealingPotion,
                    actor.MaximumHealth-actor.CurrentHealth) ||
                    _options.AllowEmergencyHealingOvercap),context,
                "Health deficit "+(actor.MaximumHealth-actor.CurrentHealth).ToString("0")+
                "; "+_options.RestorationPolicy)) return;
        if (rageState is not null && Try(ForeverCombatItemCatalog.MightyRagePotion,
                rage <= _options.MightyRageBelowRage &&
                FitsMaximum(ForeverCombatItemCatalog.MightyRagePotion,
                    Math.Max(0m,rageState.Maximum-rage)),
                context,"Rage deficit "+(rageState.Maximum-rage).ToString("0")+
                "; "+_options.RestorationPolicy)) return;

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
