using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Data.Forever.Shaman;

/// <summary>Per-run Flurry, Maelstrom Weapon and Stormstrike nature follow-up.
/// All proc decisions use the simulation's seeded RNG, not expected values.</summary>
public sealed class ForeverEnhancementShamanReactiveProcessor : ICombatEventProcessor
{
    private readonly string _actorKey;
    private readonly AutoAttackProcessor _autos;
    private readonly AuraManager _auras;
    private readonly ForeverEnhancementShamanTalents _talents;
    private readonly ForeverEnhancementShamanTuning _tuning;
    private int _flurryCharges;
    private int _maelstromStacks;
    private decimal _stormstrikeExpires;
    public int MaxMaelstromStacks { get; private set; }
    public int MaelstromInstantBolts { get; private set; }

    public ForeverEnhancementShamanReactiveProcessor(string actorKey,
        AutoAttackProcessor autos, AuraManager auras,
        ForeverEnhancementShamanTalents talents, ForeverEnhancementShamanTuning tuning)
    {
        _actorKey = actorKey; _autos = autos; _auras = auras;
        _talents = talents; _tuning = tuning;
    }
    public void Process(SimulationContext context, CombatEvent e)
    {
        var actor = context.GetActor(_actorKey);
        if (actor is null || !actor.IsAlive) return;
        // A five-stack proc can expire unspent; do not lock future proc generation.
        if (_maelstromStacks == 5 &&
            !actor.ActiveAuras.Any(a => a.Definition.Key == "maelstrom-ready" &&
                a.IsActiveAt(context.CurrentTimeSeconds)))
            _maelstromStacks = 0;
        if (_stormstrikeExpires > 0m && context.CurrentTimeSeconds >= _stormstrikeExpires)
        {
            actor.Stats.Set("shaman-stormstrike-nature-percent", 0m);
            _stormstrikeExpires = 0m;
        }
        if (e.Type == CombatEventType.AbilityCastCompleted && e.SourceActorKey == _actorKey &&
            e.AbilityKey == "maelstrom-lightning-bolt")
        {
            MaelstromInstantBolts++;
            _maelstromStacks = 0;
            _auras.RemoveAuras(context, actor, "maelstrom-ready", actor.Key);
        }
        if (e.Type == CombatEventType.AutoAttackSwing && e.SourceActorKey == _actorKey &&
            e.AbilityKey is ("main-hand-auto" or "off-hand-auto") && _flurryCharges > 0)
        {
            _flurryCharges--;
            if (_flurryCharges == 0) UpdateFlurry(context, actor);
        }
        if (e.Type != CombatEventType.Damage || e.SourceActorKey != _actorKey || e.Amount is not > 0m)
            return;
        if (e.AbilityKey == "stormstrike" && e.WeaponHandKey == "main-hand")
        {
            actor.Stats.Set("shaman-stormstrike-nature-percent", 20m);
            _stormstrikeExpires = context.CurrentTimeSeconds + 12m;
        }
        else if (e.SchoolKey == "nature" && _stormstrikeExpires > 0m)
        {
            // The spell resolving this damage already read the +20% stat.
            actor.Stats.Set("shaman-stormstrike-nature-percent", 0m);
            _stormstrikeExpires = 0m;
        }
        // Either equipped hand can trigger melee-crit Flurry or Maelstrom.
        // Totem and Windfury background swings are nonrecursive by design;
        // only real weapon hit events enter this specialization listener.
        if (e.WeaponHandKey is not ("main-hand" or "off-hand") ||
            e.IsPeriodic || e.IsChannelTick) return;
        var flurryRank = _talents.Rank("flurry");
        if (flurryRank > 0 && e.IsCritical)
        {
            _flurryCharges = 3;
            UpdateFlurry(context, actor);
        }
        var maelstromRank = _talents.Rank("maelstrom-weapon");
        if (maelstromRank > 0 && _maelstromStacks < 5 &&
            (decimal)context.Random.NextDouble() * 100m <
                _tuning.MaelstromChancePerMeleePercentPerRank * maelstromRank)
        {
            _maelstromStacks++;
            MaxMaelstromStacks = Math.Max(MaxMaelstromStacks, _maelstromStacks);
            if (_maelstromStacks == 5)
                _auras.ApplyAura(context, actor, new AuraDefinition
                {
                    Key = "maelstrom-ready", Name = "Maelstrom Weapon (5 stacks)",
                    DurationSeconds = 30m
                }, actor.Key, "maelstrom-weapon", null);
        }
    }
    private void UpdateFlurry(SimulationContext context, SimulationActorState actor)
    {
        var oldBonus = actor.Stats.Get("shaman-flurry-haste-percent");
        var newBonus = _flurryCharges > 0 ? 5m * _talents.Rank("flurry") : 0m;
        if (oldBonus == newBonus) return;
        var haste = actor.Stats.Get(ForeverCombatStatKeys.HastePercent);
        actor.Stats.Set("shaman-flurry-haste-percent", newBonus);
        actor.Stats.Set(ForeverCombatStatKeys.HastePercent, haste - oldBonus + newBonus);
        _autos.RescheduleForHasteChange(context, actor.Key, haste, haste - oldBonus + newBonus);
    }
}
