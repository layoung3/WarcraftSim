using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Data.Forever.Shaman;

/// <summary>
/// Historical Forever Elemental Devastation: a direct spell critical grants
/// 3/6/9 melee critical percentage points for ten seconds. The source is
/// gunba/wow-forever-sim at 1cc5ae1e9abdbf941b7326d2d90421822dce31ba,
/// sim/shaman/talents.go; this does not certify current-server values.
/// </summary>
public sealed class ShamanElementalDevastationProcessor : ICombatEventProcessor
{
    public const string AuraKey = "shaman-elemental-devastation";
    public const string EffectKey = "elemental-devastation-melee-crit";
    private readonly SimulationActorState _owner;
    private readonly AuraManager _auras;
    private readonly int _rank;
    private readonly bool _classWide;

    public int Procs { get; private set; }

    public ShamanElementalDevastationProcessor(SimulationActorState owner,
        AuraManager auras, int rank, bool classWide = false)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(auras);
        if (rank is < 0 or > 3)
            throw new ArgumentOutOfRangeException(nameof(rank));
        _owner = owner;
        _auras = auras;
        _rank = rank;
        _classWide=classWide;
    }

    // Read the live, owned aura at the roll. Do not modify the base character
    // sheet or spell critical chance, and do not rely on an expiry callback
    // preceding other events queued for the exact same timestamp.
    public decimal MeleeCriticalChanceBonus(SimulationActorState actor, decimal timeSeconds) =>
        actor == _owner && _rank > 0 && _owner.ActiveAuras.Any(a =>
            a.Definition.Key == AuraKey && a.SourceActorKey == _owner.Key &&
            a.TargetActorKey == _owner.Key && a.IsActiveAt(timeSeconds))
            ? 3m * _rank : 0m;

    public void Process(SimulationContext context, CombatEvent e)
    {
        if (_rank == 0 || !_owner.IsAlive || e.Type != CombatEventType.Damage ||
            e.SourceActorKey != _owner.Key || e.ResultKey != "critical" || !e.IsCritical ||
            e.EffectDeliveryType != CombatEffectDeliveryType.Direct ||
            e.ResolutionType != CombatResolutionTypes.Spell || e.WeaponHandKey is not null ||
            !(e.AbilityKey is ("lightning-bolt" or "lightning-bolt-rank7" or
                "maelstrom-lightning-bolt" or "chain-lightning" or "earth-shock" or
                "flame-shock" or "lava-burst") || _classWide && e.AbilityKey is ("frost-shock" or "fire-nova")))
            return;

        // This explicit set covers the two existing Shaman spellbooks. It
        // excludes passive Overload, weapon imbues, Shield, Fire Nova, totem
        // proxies and other triggered casts even if their damage is critical.
        // A landed critical reduced to zero damage still qualifies: the
        // historical trigger tests its outcome, not positive damage dealt.
        Procs++;
        _auras.ApplyAura(context, _owner, new AuraDefinition
        {
            Key = AuraKey,
            Name = $"Elemental Devastation (+{3 * _rank}% melee critical chance)",
            DurationSeconds = 10m
        }, _owner.Key, "elemental-devastation", EffectKey);
    }
}
