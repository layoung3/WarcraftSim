using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>First multi-target Sweeping Strikes pass: 30 Rage / 30s cooldown /
/// 20s buff / five eligible melee strikes. A landed strike echoes its resolved
/// damage onto one nearby enemy. This preview does not resolve a second melee
/// roll or secondary mitigation; refine when server beta proc data is verified.</summary>
public sealed class WarriorSweepingStrikesProcessor : ICombatEventProcessor
{
    public const string AbilityKey = "sweeping-strikes";
    public const string EchoKey = "sweeping-strikes-echo";
    private int _charges;
    private decimal _expiresAt;
    private readonly HashSet<(decimal Time, string Ability, string Hand, Guid? Execution)> _handled = [];
    private decimal _lastDamageTime = -1m;

    public void Process(SimulationContext context, CombatEvent ev)
    {
        if (ev.SourceActorKey != "warrior") return;
        if (ev.Type == CombatEventType.AbilityCastCompleted && ev.AbilityKey == AbilityKey)
        {
            _charges = 5;
            _expiresAt = context.CurrentTimeSeconds + 20m;
            return;
        }
        if (_charges <= 0 || context.CurrentTimeSeconds >= _expiresAt ||
            ev.Type != CombatEventType.Damage || ev.IsPeriodic ||
            ev.AbilityKey is EchoKey or AbilityKey or "whirlwind" ||
            ev.WeaponHandKey is not ("main-hand" or "off-hand") ||
            (ev.Amount ?? 0m) <= 0m || ev.TargetActorKey is null) return;

        // Cleave generates multiple resolved target hits from one swing.
        // Prevent it from consuming multiple charges at the same instant.
        if (_lastDamageTime != context.CurrentTimeSeconds)
        {
            _handled.Clear();
            _lastDamageTime = context.CurrentTimeSeconds;
        }
        var swing = (context.CurrentTimeSeconds, ev.AbilityKey ?? "", ev.WeaponHandKey, ev.AbilityExecutionId);
        if (!_handled.Add(swing)) return;

        var primary = context.GetActor(ev.TargetActorKey);
        if (primary is null || !primary.IsAlive || context.Encounter is null) return;
        var neighbors = context.Encounter.TargetProximityLinks
            .Where(link => link.InCleaveRange &&
                (link.TargetAKey == primary.Key || link.TargetBKey == primary.Key))
            .Select(link => link.TargetAKey == primary.Key ? link.TargetBKey : link.TargetAKey)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(key => key, StringComparer.OrdinalIgnoreCase);
        var secondary = neighbors.Select(context.GetActor)
            .FirstOrDefault(a => a is not null && a.IsAlive && a.TeamKey != "raid");
        if (secondary is null) return;  // No charges spent without an available second enemy.

        _charges--;
        // A secondary strike may be absorbed independently of the primary.
        // Preserve the currently provisional copied post-mitigation damage rule,
        // but never bypass the shared absorb/health resolution or omit metadata.
        var rawCopy = ev.Amount.GetValueOrDefault();
        var resolution = new AbsorbManager().ApplyDamage(context, secondary, rawCopy,
            "warrior", EchoKey, "echo-hit");
        context.EmitEvent(new CombatEvent
        {
            TimeSeconds = context.CurrentTimeSeconds, Type = CombatEventType.Damage,
            SourceActorKey = "warrior", TargetActorKey = secondary.Key,
            AbilityKey = EchoKey, EffectKey = "echo-hit", SchoolKey = "physical",
            ResultKey = "hit", RawAmount = rawCopy, MitigatedAmount = 0m,
            BlockedAmount = 0m, AbsorbedAmount = resolution.AbsorbedDamage,
            Amount = resolution.HealthDamage, 
            Description = $"Sweeping Strikes echoed a landed melee strike; {_charges} charges left."
        });
    }
}
