namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Per-owner/per-target state for the existing Survival event-based bleed.
/// It DOES NOT schedule ticks or calculate combat damage. The owning Survival
/// processor schedules seven ordinary events and uses this ledger only to
/// invalidate prior generations on the SAME target. This implements the
/// November 2 source rule that even a weaker fresh bleed replaces the old
/// bleed from the same Hunter, with no carryover/rollover damage.
/// </summary>
public sealed class SurvivalLaceratingBleedLedger
{
    public sealed record Application(int Generation,decimal TickDamage,
        decimal HitDamage);
    private readonly Dictionary<string,Application> _targets =
        new(StringComparer.OrdinalIgnoreCase);

    public Application Replace(string targetKey,decimal mongooseDamage)
    {
        if(string.IsNullOrWhiteSpace(targetKey)||mongooseDamage<=0m)
            throw new ArgumentException("Lacerating Strikes requires a landed Mongoose Bite and target.");
        var next=_targets.TryGetValue(targetKey,out var old)?old.Generation+1:1;
        var app=new Application(next, mongooseDamage*.40m/7m, mongooseDamage);
        _targets[targetKey]=app;
        return app;
    }

    public bool IsCurrent(string targetKey,int? generation) =>
        generation.HasValue && _targets.TryGetValue(targetKey,out var active) &&
        active.Generation==generation;

    public Application? Current(string targetKey)=>
        _targets.TryGetValue(targetKey,out var value)?value:null;
}
