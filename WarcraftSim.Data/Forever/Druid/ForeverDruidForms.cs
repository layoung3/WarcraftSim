using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Data.Forever.Druid;

public enum ForeverDruidForm { Caster, Cat, Bear, DireBear, Moonkin }

public sealed record ForeverDruidResourceChange(string ResourceKey, decimal Amount, decimal TotalAfter);

public sealed record ForeverDruidFormShiftResult(bool Succeeded,
    IReadOnlyList<ForeverDruidResourceChange> FurorChanges);

/// <summary>
/// Reusable, mutable per-actor shapeshift state; this is not an equipment model.
/// Weapon DPS carries through animal forms at 1.0/2.5-second fixed swing speeds
/// (Forever official Druid deep dive). Historical pinned Furor restores a rank
/// fraction of last-Cat Energy plus eligible Caster/Moonkin time on Cat entry.
/// Continuous time is a local approximation, not verified game-client tick timing.
/// </summary>
public sealed class ForeverDruidFormState
{
    public ForeverDruidForm Form { get; private set; } = ForeverDruidForm.Caster;
    public int FurorRank { get; }
    public decimal ShiftManaCost { get; }
    public int Shifts { get; private set; }
    public decimal NextShiftAvailableAtSeconds { get; private set; }
    private decimal _lastCatEnergy;
    private decimal _eligibleSeconds;
    private decimal _lastTransitionAtSeconds;
    private static readonly ForeverDruidFormShiftResult Failed = new(false,
        Array.Empty<ForeverDruidResourceChange>());
    public ForeverDruidFormState(int furorRank = 0, decimal shiftManaCost = 250m)
    {
        if (furorRank is < 0 or > 5 || shiftManaCost < 0m)
            throw new ArgumentException("Invalid Druid form configuration.");
        FurorRank = furorRank;
        ShiftManaCost = shiftManaCost;
    }
    public static string AuraKey(ForeverDruidForm form) => form switch
    {
        ForeverDruidForm.Cat => "druid-cat-form",
        ForeverDruidForm.Bear => "druid-bear-form",
        ForeverDruidForm.DireBear => "druid-dire-bear-form",
        ForeverDruidForm.Moonkin => "druid-moonkin-form",
        _ => "druid-caster-form"
    };
    public static decimal SwingSeconds(ForeverDruidForm form) => form switch
    {
        ForeverDruidForm.Cat => 1m,
        ForeverDruidForm.Bear or ForeverDruidForm.DireBear => 2.5m,
        _ => 0m
    };
    public static decimal WeaponDamage(decimal equippedWeaponDps, ForeverDruidForm form)
    {
        if (equippedWeaponDps < 0m) throw new ArgumentOutOfRangeException(nameof(equippedWeaponDps));
        return equippedWeaponDps * SwingSeconds(form);
    }
    // Establish the scenario's authored starting form/resources. This pays the
    // existing shift cost and owns the first lockout, but is not a talent proc.
    // An inactive starting Energy pool must not fabricate prior Cat history.
    public bool EstablishInitialForm(SimulationActorState actor, ForeverDruidForm initial, decimal now)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (Shifts != 0 || Form != ForeverDruidForm.Caster || initial == Form ||
            now < NextShiftAvailableAtSeconds || !Enum.IsDefined(initial)) return false;
        if (!PayShift(actor, now)) return false;
        CompleteShift(actor, initial, now);
        return true;
    }

    public ForeverDruidFormShiftResult Shift(SimulationActorState actor,
        ForeverDruidForm next, decimal now, Random random)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(random);
        if (Form == next || now < NextShiftAvailableAtSeconds || !Enum.IsDefined(next)) return Failed;
        if (!PayShift(actor, now)) return Failed;

        // Only successful transitions commit history. Refresh in PayShift runs
        // first, so leaving Cat snapshots the actual post-regeneration balance.
        if (Form == ForeverDruidForm.Cat)
        {
            _lastCatEnergy = actor.Resources.TryGetValue("energy", out var catEnergy)
                ? catEnergy.Current : 0m;
            _eligibleSeconds = 0m;
        }
        else if (Form is ForeverDruidForm.Caster or ForeverDruidForm.Moonkin)
            _eligibleSeconds += now - _lastTransitionAtSeconds;

        var changes = new List<ForeverDruidResourceChange>();
        if (actor.Resources.TryGetValue("energy", out var energy))
        {
            if (FurorRank == 0 && next is (ForeverDruidForm.Cat or
                ForeverDruidForm.Bear or ForeverDruidForm.DireBear))
            {
                // Preserve ordinary no-talent reset behavior and its existing
                // event contract; it is not a Furor-attributed resource grant.
                energy.SetCurrent(0m);
            }
            else if (FurorRank > 0 && next == ForeverDruidForm.Cat)
            {
                // gunba Druid tree 1cc5ae1e: 20r% retained + 2r/sec, cap20r.
                // Use saved history, NOT the current inactive pool, which at
                // rank5 already presents regeneration. This avoids double gain.
                var before = energy.Current;
                energy.SetCurrent(Math.Min(20m * FurorRank,
                    .2m * FurorRank * _lastCatEnergy + 2m * FurorRank * _eligibleSeconds));
                var delta = energy.Current - before;
                if (delta != 0m) changes.Add(new("energy", delta, energy.Current));
            }
        }
        if (next == ForeverDruidForm.Cat) _eligibleSeconds = 0m;

        if (next is ForeverDruidForm.Bear or ForeverDruidForm.DireBear && FurorRank > 0)
        {
            // One shared run RNG draw at partial ranks, even at a full/missing
            // Rage pool. Rank0/rank5 use no draw. Failed/noop shifts never roll.
            var proc = FurorRank == 5 || (decimal)random.NextDouble() * 100m < 20m * FurorRank;
            if (proc && actor.Resources.TryGetValue("rage", out var rage))
            {
                var gained = rage.Gain(10m);
                if (gained > 0m) changes.Add(new("rage", gained, rage.Current));
            }
        }
        CompleteShift(actor, next, now);
        return new(true, changes.AsReadOnly());
    }

    private bool PayShift(SimulationActorState actor, decimal now)
    {
        // Preserve passive refresh even on affordability failure, but do not
        // consume RNG, update history or start a lockout on a failed attempt.
        actor.RefreshResources(now);
        if (!actor.Resources.TryGetValue("mana", out var mana) || !mana.CanSpend(ShiftManaCost))
            return false;
        return mana.Spend(ShiftManaCost);
    }

    private void CompleteShift(SimulationActorState actor, ForeverDruidForm next, decimal now)
    {
        if (actor.Resources.TryGetValue("energy", out var energy))
            energy.RegenerationPerSecond = next == ForeverDruidForm.Cat ||
                next is not (ForeverDruidForm.Bear or ForeverDruidForm.DireBear) && FurorRank == 5
                ? 10m : 0m;
        Form = next;
        _lastTransitionAtSeconds = now;
        NextShiftAvailableAtSeconds = now + 6m;
        Shifts++;
    }
}
